using Blazix.Primitives.Interop;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

namespace Blazix.Primitives.Tests.Components;

/// <summary>
/// The items report (M3 track 1, E2; ADR 04 Amendments 2026-10-26, chosen by the user): with <c>ReportItems</c> the
/// module reports the items' ids in index order on each publication that changed them, and
/// <see cref="CompositeRootBehavior"/> joins them to its registrations, exposing them in order with one change event per
/// report. Values never cross to JavaScript. Each report is a self-contained sequence, joined when it arrives (design
/// points 1 and 3, option (a); settled by the orchestrator 2026-10-26).
/// </summary>
public sealed class CompositeItemsReportTests : IDisposable
{
    private readonly CompositeTestContext host = new();

    private BunitContext Context => host.Context;

    /// <inheritdoc />
    public void Dispose() => host.Dispose();

    [Fact(DisplayName = "joins the reported ids to its registrations in order, ignoring unknown ids, with one change event per report")]
    [Trait("Requirement", "CMP-EVT-03")]
    [Trait("Requirement", "CMP-STA-08")]
    public void Joins_the_reported_ids_to_its_registrations_in_order()
    {
        IRenderedComponent<TestCompositeRoot> cut = Context.Render<TestCompositeRoot>(p => p
            .Add(x => x.ReportItems, true)
            .AddChildContent(CompositeItems.Of("a", "b", "c")));
        string[] ids = CompositeTestContext.RenderedItemIds(cut);
        CompositeRootBehavior behavior = cut.Instance.Behavior;
        int changes = 0;
        behavior.ItemsChanged += () => changes++;
        Assert.True(CompositeTestContext.Parse((string)Assert.Single(host.Attaches).Arguments[1]!).GetProperty("reportItems").GetBoolean());
        Assert.Empty(behavior.OrderedItems);

        cut.InvokeAsync(() => host.Twin.OnItemsChange($$"""{"ids":["{{ids[2]}}","unknown","{{ids[0]}}"]}"""));

        Assert.Equal([ids[2], ids[0]], behavior.OrderedItems.Select(item => item.Registration.Id));
        Assert.Equal(1, changes);

        cut.InvokeAsync(() => host.Twin.OnItemsChange($$"""{"ids":["{{ids[0]}}","{{ids[1]}}","{{ids[2]}}"]}"""));

        Assert.Equal(ids, behavior.OrderedItems.Select(item => item.Registration.Id));
        Assert.Equal(2, changes);
    }

    [Fact(DisplayName = "drops a removed item from the joined order before the next report")]
    [Trait("Requirement", "CMP-EVT-03")]
    public void Drops_a_removed_item_from_the_joined_order()
    {
        IRenderedComponent<TestCompositeRoot> cut = Context.Render<TestCompositeRoot>(p => p
            .Add(x => x.ReportItems, true)
            .AddChildContent(CompositeItems.Of("a", "b")));
        string[] ids = CompositeTestContext.RenderedItemIds(cut);
        cut.InvokeAsync(() => host.Twin.OnItemsChange($$"""{"ids":["{{ids[0]}}","{{ids[1]}}"]}"""));

        cut.Render(p => p
            .Add(x => x.ReportItems, true)
            .AddChildContent(CompositeItems.Of("b")));

        Assert.Equal([ids[1]], cut.Instance.Behavior.OrderedItems.Select(item => item.Registration.Id));
    }

    // On WebAssembly the first report runs re-entrantly inside the C# `attach` call, and on Server it reaches C# ahead of
    // the attach's completion: either way the behaviour has no handle yet. The mock reports from inside `attach`.
    [Fact(DisplayName = "applies a report that arrives before AttachAsync returns")]
    [Trait("Requirement", "CMP-EVT-03")]
    [Trait("Requirement", "CMP-STA-10")]
    public void Applies_a_report_that_arrives_before_attach_returns()
    {
        using BunitContext context = new();
        context.Services.AddBlazixPrimitives();
        BunitJSModuleInterop module = context.JSInterop.SetupModule(CompositeTestContext.ModulePath);
        string[]? reported = null;
        BunitJSModuleInterop handle = module.SetupModule("attach", invocation =>
        {
            if (reported is null)
            {
                // The ids in reverse, so the joined order is the report's and not the registrations'.
                reported = [.. CompositeTestContext.ItemIds((string)invocation.Arguments[1]!).Reverse()];
                CompositeTwin twin = ((DotNetObjectReference<CompositeTwin>)invocation.Arguments[2]!).Value;
                twin.OnItemsChange("{\"ids\":[" + string.Join(",", reported.Select(id => $"\"{id}\"")) + "]}");
            }

            return true;
        });
        handle.SetupVoid("update", _ => true).SetVoidResult();

        IRenderedComponent<TestCompositeRoot> cut = context.Render<TestCompositeRoot>(p => p
            .Add(x => x.ReportItems, true)
            .AddChildContent(CompositeItems.Of("a", "b")));
        string[] ids = CompositeTestContext.RenderedItemIds(cut);

        Assert.Equal([ids[1], ids[0]], reported!);
        Assert.Equal([ids[1], ids[0]], cut.Instance.Behavior.OrderedItems.Select(item => item.Registration.Id));
        Assert.Empty(handle.Invocations["update"]);
    }

    // On WebAssembly a report from inside `update` runs re-entrantly inside the C# call; the part re-renders on it, and its
    // after-render finds nothing new to send.
    [Fact(DisplayName = "a report during an update sends no second update")]
    [Trait("Requirement", "CMP-EVT-03")]
    [Trait("Requirement", "CMP-STA-10")]
    public void A_report_during_an_update_sends_no_second_update()
    {
        using BunitContext context = new();
        context.Services.AddBlazixPrimitives();
        BunitJSModuleInterop module = context.JSInterop.SetupModule(CompositeTestContext.ModulePath);
        BunitJSModuleInterop handle = module.SetupModule("attach", _ => true);
        CompositeTwin? twin = null;
        bool reported = false;
        handle.SetupVoid("update", invocation =>
        {
            if (!reported && twin is not null)
            {
                // The module reports the ids the update carries, in order, from inside the call.
                reported = true;
                string[] sent = CompositeTestContext.ItemIds((string)invocation.Arguments[0]!);
                twin.OnItemsChange("{\"ids\":[" + string.Join(",", sent.Select(id => $"\"{id}\"")) + "]}");
            }

            return true;
        }).SetVoidResult();
        handle.SetupVoid("dispose", _ => true).SetVoidResult();

        IRenderedComponent<TestCompositeRoot> cut = context.Render<TestCompositeRoot>(p => p
            .Add(x => x.ReportItems, true)
            .AddChildContent(CompositeItems.Of("a")));
        twin = ((DotNetObjectReference<CompositeTwin>)Assert.Single(module.Invocations["attach"]).Arguments[2]!).Value;
        int renders = 0;
        cut.Instance.Behavior.ItemsChanged += () =>
        {
            renders++;
            cut.Instance.Refresh();
        };

        cut.Render(p => p
            .Add(x => x.ReportItems, true)
            .AddChildContent(CompositeItems.Of("a", "b")));

        Assert.Equal(CompositeTestContext.RenderedItemIds(cut), cut.Instance.Behavior.OrderedItems.Select(item => item.Registration.Id));
        Assert.Equal(1, renders);
        Assert.Single(handle.Invocations["update"]);
    }
}

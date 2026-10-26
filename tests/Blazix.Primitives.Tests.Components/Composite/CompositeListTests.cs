using Bunit;
using Microsoft.AspNetCore.Components;

namespace Blazix.Primitives.Tests.Components;

/// <summary>
/// The registration-only list (M3 track 1, E1; ADR 04 Amendments 2026-10-26, chosen by the user): a root-less
/// <c>CompositeList</c> (TabsRoot's panels, AccordionRoot's items) attaches the <c>composite</c> module in its
/// registration-only mode and reads its items' indexes from the order report. An item's index is <c>-1</c> until the
/// first report and in prerender (CMP-STA-15); its part re-renders only when its index changes (ADR 16 rule 8). An item
/// without a list is inert (CMP-STR-03). Every list item renders an id (design point 2) and the list cascades its own
/// context (design point 6), option (a) both; pending the user.
/// </summary>
public sealed class CompositeListTests : IDisposable
{
    private readonly CompositeTestContext host = new();

    private BunitContext Context => host.Context;

    /// <inheritdoc />
    public void Dispose() => host.Dispose();

    [Fact(DisplayName = "renders an item that is not wrapped in a list")]
    [Trait("Requirement", "CMP-STR-03")]
    public void Renders_an_item_that_is_not_wrapped_in_a_list()
    {
        IRenderedComponent<TestCompositeListItem> cut = Context.Render<TestCompositeListItem>(p => p.AddUnmatched("data-testid", "orphan"));

        Assert.Equal("-1", cut.Find("[data-testid=\"orphan\"]").GetAttribute("data-index"));
        Assert.Empty(Context.JSInterop.Invocations);
        Exception? error = Record.Exception(() => Context.DisposeComponentsAsync().GetAwaiter().GetResult());
        Assert.Null(error);
    }

    [Fact(DisplayName = "an item's index is -1 until the report, then the reported index, re-rendering only the items whose index changed")]
    [Trait("Requirement", "CMP-STR-03")]
    [Trait("Requirement", "CMP-STA-15")]
    [Trait("Requirement", "CMP-STA-10")]
    public void An_items_index_is_minus_one_until_the_report_then_the_reported_index()
    {
        IRenderedComponent<TestCompositeList> cut = Context.Render<TestCompositeList>(p => p.AddChildContent(Items("a", "b", "c")));
        string[] ids = RenderedIds(cut);
        string attached = (string)Assert.Single(host.Attaches).Arguments[1]!;
        Assert.True(CompositeTestContext.Parse(attached).GetProperty("registrationOnly").GetBoolean());
        Assert.Equal(ids, CompositeTestContext.ItemIds(attached));
        Assert.Equal(["-1", "-1", "-1"], Indexes(cut));
        IReadOnlyList<IRenderedComponent<TestCompositeListItem>> items = cut.FindComponents<TestCompositeListItem>();
        int[] before = [.. items.Select(item => item.Instance.Renders)];

        cut.InvokeAsync(() => host.Twin.OnItemsChange($$"""{"ids":["{{ids[0]}}","{{ids[1]}}","{{ids[2]}}"]}"""));
        Assert.Equal(["0", "1", "2"], Indexes(cut));

        cut.InvokeAsync(() => host.Twin.OnItemsChange($$"""{"ids":["{{ids[1]}}","{{ids[0]}}","{{ids[2]}}"]}"""));
        Assert.Equal(["1", "0", "2"], Indexes(cut));
        Assert.Equal([before[0] + 2, before[1] + 2, before[2] + 1], items.Select(item => item.Instance.Renders));
        Assert.Empty(host.UpdateOptions);
    }

    [Fact(DisplayName = "a batch of items sends one update, an unreported item keeps -1, and an unknown id is ignored")]
    [Trait("Requirement", "CMP-STR-03")]
    [Trait("Requirement", "CMP-STA-10")]
    public void A_batch_of_items_sends_one_update()
    {
        IRenderedComponent<TestCompositeList> cut = Context.Render<TestCompositeList>(p => p.AddChildContent(Items("a")));
        string a = RenderedIds(cut)[0];
        cut.InvokeAsync(() => host.Twin.OnItemsChange($$"""{"ids":["{{a}}"]}"""));

        cut.Render(p => p.AddChildContent(Items("a", "b", "c")));
        string update = Assert.Single(host.UpdateOptions);
        string[] ids = RenderedIds(cut);
        Assert.Equal(ids, CompositeTestContext.ItemIds(update));
        Assert.Equal(["0", "-1", "-1"], Indexes(cut));

        cut.InvokeAsync(() => host.Twin.OnItemsChange($$"""{"ids":["unknown","{{ids[2]}}","{{ids[0]}}"]}"""));
        Assert.Equal(["2", "-1", "1"], Indexes(cut));

        cut.Render(p => p.AddChildContent(Items("a", "b", "c")));
        Assert.Single(host.UpdateOptions);
    }

    private static string[] RenderedIds(IRenderedComponent<TestCompositeList> cut) => [.. cut.FindAll("[data-entry]").Select(element => element.Id!)];

    private static string[] Indexes(IRenderedComponent<TestCompositeList> cut) => [.. cut.FindAll("[data-entry]").Select(element => element.GetAttribute("data-index")!)];

    private static RenderFragment Items(params string[] labels) => builder =>
    {
        foreach (string label in labels)
        {
            builder.OpenComponent<TestCompositeListItem>(0);
            builder.SetKey(label);
            builder.AddAttribute(1, "data-entry", label);
            builder.CloseComponent();
        }
    };
}

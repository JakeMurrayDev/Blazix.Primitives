using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace Blazix.Primitives.Tests.Components;

/// <summary>
/// Option (i) of ADR 04's note of 2026-10-22, pending the user: the consumer's C# <c>onkeydown</c> on the focused item and
/// on the root run for a key the composite handles. On WebAssembly they run inside the <c>navigate</c> decision, item
/// first, on one shared event, so a <c>PreventPartHandler()</c> in either stops the key (Base UI's flag lives on the shared
/// synthetic event, <c>merge-props/mergeProps.ts:210-231</c>); on Server the module's handled-key report runs them a round
/// trip late. The module sends the report only while the root or an item has such a handler. Ancestors never see the key.
/// </summary>
public sealed class CompositeKeyHandlerTests : IDisposable
{
    private readonly CompositeTestContext host = new();
    private readonly List<string> runs = [];

    private BunitContext Context => host.Context;

    /// <inheritdoc />
    public void Dispose() => host.Dispose();

    private static string KeyDownJson(string key, string targetId) => $$"""
        {"kind":"KeyboardEvent","type":"keydown","key":"{{key}}","code":"{{key}}","altKey":false,"ctrlKey":false,
         "metaKey":false,"shiftKey":false,"pointerType":null,"detail":0,"buttons":0,
         "target":{"id":"{{targetId}}","tagName":"DIV"},"defaultPrevented":false,"timeStamp":3}
        """;

    [Fact(DisplayName = "sends reportHandledKeys only while the root or an item has a consumer onkeydown")]
    [Trait("Requirement", "CMP-EVT-12")]
    public void Sends_reportHandledKeys_only_while_a_consumer_key_handler_exists()
    {
        IRenderedComponent<TestCompositeRoot> cut = Context.Render<TestCompositeRoot>(p => p.AddChildContent(Items(itemHandler: false)));
        Assert.False(CompositeTestContext.Parse((string)Assert.Single(host.Attaches).Arguments[1]!).GetProperty("reportHandledKeys").GetBoolean());

        cut.Render(p => p.AddChildContent(Items(itemHandler: true)));
        Assert.True(CompositeTestContext.Parse(Assert.Single(host.UpdateOptions)).GetProperty("reportHandledKeys").GetBoolean());

        cut.Render(p => p
            .AddUnmatched("onkeydown", RootHandler(veto: false))
            .AddChildContent(Items(itemHandler: false)));
        Assert.Equal(2, host.UpdateOptions.Count);
        Assert.True(CompositeTestContext.Parse(host.UpdateOptions[1]).GetProperty("reportHandledKeys").GetBoolean());
    }

    [Fact(DisplayName = "a handled-key report runs the item's consumer onkeydown, then the root's (Server)")]
    [Trait("Requirement", "CMP-EVT-12")]
    public void A_handled_key_report_runs_the_items_then_the_roots_handler()
    {
        IRenderedComponent<TestCompositeRoot> cut = Context.Render<TestCompositeRoot>(p => p
            .AddUnmatched("onkeydown", RootHandler(veto: false))
            .AddChildContent(Items(itemHandler: true)));
        string first = cut.FindAll("[data-item]")[0].Id!;

        cut.InvokeAsync(() => host.Twin.OnKeyHandled($$"""{"event":{{KeyDownJson("ArrowDown", first)}}}"""));

        Assert.Equal(["item a: ArrowDown", "root: ArrowDown"], runs);
    }

    [Fact(DisplayName = "the navigate decision runs the item's consumer onkeydown, then the root's, on one event")]
    [Trait("Requirement", "CMP-EVT-12")]
    [Trait("Requirement", "EVD-EVT-26")]
    public void The_navigate_decision_runs_the_items_then_the_roots_handler()
    {
        IRenderedComponent<TestCompositeRoot> cut = Context.Render<TestCompositeRoot>(p => p
            .AddUnmatched("onkeydown", RootHandler(veto: false))
            .AddChildContent(Items(itemHandler: true)));
        string second = cut.FindAll("[data-item]")[1].Id!;

        Assert.Equal("run", host.Twin.Decide(CompositeTwin.NavigatePoint, KeyDownJson("ArrowUp", second)));
        Assert.Equal(["item b: ArrowUp", "root: ArrowUp"], runs);
    }

    [Fact(DisplayName = "a PreventPartHandler() in the item's onkeydown stops the key, and the root's handler still runs")]
    [Trait("Requirement", "CMP-EVT-12")]
    [Trait("Requirement", "EVD-EVT-26")]
    public void An_items_veto_stops_the_key_and_the_roots_handler_still_runs()
    {
        IRenderedComponent<TestCompositeRoot> cut = Context.Render<TestCompositeRoot>(p => p
            .AddUnmatched("onkeydown", RootHandler(veto: false))
            .AddChildContent(Items(itemHandler: true, itemVeto: true)));
        string first = cut.FindAll("[data-item]")[0].Id!;

        Assert.Equal("stop", host.Twin.Decide(CompositeTwin.NavigatePoint, KeyDownJson("ArrowDown", first)));
        Assert.Equal(["item a: ArrowDown", "root: ArrowDown"], runs);

        // The vetoed key is not stopped, so Blazor dispatches it; neither handler runs a second time.
        cut.FindAll("[data-item]")[0].KeyDown(new KeyboardEventArgs { Key = "ArrowDown", Code = "ArrowDown" });
        Assert.Equal(2, runs.Count);
    }

    [Fact(DisplayName = "an item declines its consumer onkeydown and runs it once from its own handler")]
    [Trait("Requirement", "CMP-EVT-12")]
    public void An_item_declines_its_consumer_onkeydown_and_runs_it_once()
    {
        IRenderedComponent<TestCompositeRoot> cut = Context.Render<TestCompositeRoot>(p => p
            .Add(x => x.StopEventPropagation, false)
            .AddChildContent(Items(itemHandler: true)));
        AngleSharp.Dom.IElement first = cut.FindAll("[data-item]")[0];

        first.KeyDown(new KeyboardEventArgs { Key = "Enter", Code = "Enter" });
        Assert.Equal(["item a: Enter"], runs);

        // Without stopEventPropagation the decision ran it, and Blazor's dispatch of the same key skips it once.
        host.Twin.Decide(CompositeTwin.NavigatePoint, KeyDownJson("ArrowDown", first.Id!));
        first.KeyDown(new KeyboardEventArgs { Key = "ArrowDown", Code = "ArrowDown" });
        Assert.Equal(["item a: Enter", "item a: ArrowDown"], runs);
    }

    private EventCallback<KeyboardEventArgs> RootHandler(bool veto) => EventCallback.Factory.Create<KeyboardEventArgs>(this, (KeyboardEventArgs e) =>
    {
        runs.Add($"root: {e.Key}");
        if (veto)
        {
            e.PreventPartHandler();
        }
    });

    private RenderFragment Items(bool itemHandler, bool itemVeto = false) => builder =>
    {
        foreach (string label in new[] { "a", "b" })
        {
            builder.OpenComponent<TestCompositeItem>(0);
            builder.SetKey(label);
            builder.AddAttribute(1, "data-item", label);
            if (itemHandler)
            {
                builder.AddAttribute(2, "onkeydown", EventCallback.Factory.Create<KeyboardEventArgs>(this, (KeyboardEventArgs e) =>
                {
                    runs.Add($"item {label}: {e.Key}");
                    if (itemVeto)
                    {
                        e.PreventPartHandler();
                    }
                }));
            }

            builder.CloseComponent();
        }
    };
}

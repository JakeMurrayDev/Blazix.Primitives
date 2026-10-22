using Blazix.Primitives.Interop;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace Blazix.Primitives.Tests.Components;

/// <summary>
/// The C# half of the composite (L2, the test-only hosts): what <see cref="CompositeRootBehavior"/> and
/// <see cref="CompositeItemBehavior"/> render and send. Titles are CompositeRoot.test.tsx's and CompositeList.test.tsx's
/// where the row stays at L2 (CMP-STA-10, CMP-STA-15, CMP-DOM-03, CMP-I18N-01, CMP-EVT-01), restated where React's
/// mechanics (Strict Mode, hydration) become the twin contract (ADR 06 Amendments 2026-10-18 rule 2); the port's own
/// titles cover one `update` per render batch, the root's consumer `onkeydown` gate (ADR 02 Amendments 2026-10-21) and
/// the guard of an item outside a root.
/// </summary>
public sealed class CompositeRootTests : IDisposable
{
    private readonly CompositeTestContext host = new();

    private BunitContext Context => host.Context;

    /// <inheritdoc />
    public void Dispose() => host.Dispose();

    private static string KeyDownJson(string key, string targetId = "base-ui-1") => $$"""
        {"kind":"KeyboardEvent","type":"keydown","key":"{{key}}","code":"{{key}}","altKey":false,"ctrlKey":false,
         "metaKey":false,"shiftKey":false,"pointerType":null,"detail":0,"buttons":0,
         "target":{"id":"{{targetId}}","tagName":"DIV"},"defaultPrevented":false,"timeStamp":3}
        """;

    private const string FocusJson = """
        {"kind":"FocusEvent","type":"focusin","key":null,"code":null,"altKey":false,"ctrlKey":false,
         "metaKey":false,"shiftKey":false,"pointerType":null,"detail":0,"buttons":0,
         "target":{"id":"base-ui-2","tagName":"SPAN"},"defaultPrevented":false,"timeStamp":4}
        """;

    // --- CMP-DOM-03, CMP-STA-15 -------------------------------------------------------------------------------------

    [Fact(DisplayName = "does not add aria-orientation when orientation is set")]
    [Trait("Requirement", "CMP-DOM-03")]
    public void Does_not_add_aria_orientation_when_orientation_is_set()
    {
        IRenderedComponent<TestCompositeRoot> cut = Context.Render<TestCompositeRoot>(p => p
            .Add(x => x.Orientation, Blazix.Primitives.Orientation.Horizontal)
            .AddChildContent(CompositeItems.Of("1", "2")));

        AngleSharp.Dom.IElement root = cut.Find("div");
        Assert.False(root.HasAttribute("aria-orientation"));
        // bUnit shows the element reference capture as a `blazor:` attribute; the part renders none of its own.
        Assert.DoesNotContain(root.Attributes, attribute => !attribute.Name.StartsWith("blazor:", StringComparison.Ordinal));
    }

    // Port, 2026-10-22: Base UI's hydration under Strict Mode becomes the handover (ADR 06 Amendments 2026-10-18 rule 2,
    // ADR 04 Amendments 2026-10-22): C# renders tabindex="-1" on every item on every render, in prerender and in either
    // interactive mode, and never another value, so Blazor never writes the attribute the module owns after attach.
    [Fact(DisplayName = "hydrates a server-rendered list without a mismatch under Strict Mode")]
    [Trait("Requirement", "CMP-STA-15")]
    public void Hydrates_a_server_rendered_list_without_a_mismatch_under_strict_mode()
    {
        IRenderedComponent<TestCompositeRoot> cut = Context.Render<TestCompositeRoot>(p => p.AddChildContent(CompositeItems.Of("a", "b")));
        string first = cut.Markup;

        Assert.All(cut.FindAll("[data-item]"), item => Assert.Equal("-1", item.GetAttribute("tabindex")));

        cut.InvokeAsync(cut.Instance.Refresh);
        IRenderedComponent<TestCompositeItem> item = cut.FindComponents<TestCompositeItem>()[0];
        item.InvokeAsync(item.Instance.Refresh);

        Assert.Equal(first, cut.Markup);
        Assert.All(cut.FindAll("[data-item]"), item => Assert.Equal("-1", item.GetAttribute("tabindex")));
    }

    [Fact(DisplayName = "an item's consumer tabindex wins over the rendered -1 and travels in its registration")]
    [Trait("Requirement", "RAD-DOM-16")]
    public void An_items_consumer_tabindex_wins_and_travels_in_its_registration()
    {
        IRenderedComponent<TestCompositeRoot> cut = Context.Render<TestCompositeRoot>(p => p.AddChildContent(builder =>
        {
            builder.OpenComponent<TestCompositeItem>(0);
            builder.AddAttribute(1, "data-item", "a");
            builder.AddAttribute(2, "tabindex", "5");
            builder.CloseComponent();
        }));

        Assert.Equal("5", cut.Find("[data-item]").GetAttribute("tabindex"));
        string id = cut.Find("[data-item]").Id!;
        Assert.Contains($$"""
            "items":[{"id":"{{id}}","tabIndex":"5"}]
            """.Trim(), (string)Assert.Single(host.Attaches).Arguments[1]!);
    }

    // --- CMP-STA-10 -------------------------------------------------------------------------------------------------

    [Fact(DisplayName = "attaches the module once with the items, and disposes the handle once")]
    [Trait("Requirement", "CMP-STA-10")]
    [Trait("Verifies", "ADR06-3")]
    public void Attaches_the_module_once_with_the_items_and_disposes_the_handle_once()
    {
        IRenderedComponent<TestCompositeRoot> cut = Context.Render<TestCompositeRoot>(p => p.AddChildContent(CompositeItems.Of("a", "b", "c")));
        cut.InvokeAsync(cut.Instance.Refresh);
        cut.InvokeAsync(cut.Instance.Refresh);

        JSRuntimeInvocation attach = Assert.Single(host.Attaches);
        Assert.Equal(cut.Instance.Element, Assert.IsType<ElementReference>(attach.Arguments[0]));
        Assert.Equal(CompositeTestContext.RenderedItemIds(cut), CompositeTestContext.ItemIds((string)attach.Arguments[1]!));
        Assert.IsType<DotNetObjectReference<CompositeTwin>>(attach.Arguments[2]);
        Assert.Empty(host.UpdateOptions);

        cut.Instance.Dispose();
        cut.Instance.Behavior.Dispose();
        Assert.Equal(1, host.Disposes);
    }

    [Fact(DisplayName = "a batch of three items sends one update, a removal sends one, an unchanged render none")]
    [Trait("Requirement", "CMP-STA-10")]
    public void A_batch_of_three_items_sends_one_update_a_removal_one_and_an_unchanged_render_none()
    {
        IRenderedComponent<TestCompositeRoot> cut = Context.Render<TestCompositeRoot>(p => p.AddChildContent(CompositeItems.Of("a")));

        cut.Render(p => p.AddChildContent(CompositeItems.Of("a", "b", "c", "d")));
        string added = Assert.Single(host.UpdateOptions);
        Assert.Equal(CompositeTestContext.RenderedItemIds(cut), CompositeTestContext.ItemIds(added));

        cut.Render(p => p.AddChildContent(CompositeItems.Of("a", "b", "d")));
        Assert.Equal(2, host.UpdateOptions.Count);
        Assert.Equal(CompositeTestContext.RenderedItemIds(cut), CompositeTestContext.ItemIds(host.UpdateOptions[1]));

        cut.InvokeAsync(cut.Instance.Refresh);
        cut.Render(p => p.AddChildContent(CompositeItems.Of("a", "b", "d")));
        Assert.Equal(2, host.UpdateOptions.Count);
    }

    [Fact(DisplayName = "updates refs when an item mounts from a nested state update")]
    [Trait("Requirement", "CMP-STA-10")]
    public void Updates_refs_when_an_item_mounts_from_a_nested_state_update()
    {
        IRenderedComponent<TestCompositeRoot> cut = Context.Render<TestCompositeRoot>(p => p.AddChildContent(Nested(initiallyHidden: true)));
        Assert.Equal(2, CompositeTestContext.ItemIds((string)Assert.Single(host.Attaches).Arguments[1]!).Length);

        cut.InvokeAsync(() => cut.FindComponent<HideableItem>().Instance.SetHidden(false));

        string update = Assert.Single(host.UpdateOptions);
        Assert.Equal(CompositeTestContext.RenderedItemIds(cut).Order(), CompositeTestContext.ItemIds(update).Order());
        Assert.Equal(3, CompositeTestContext.ItemIds(update).Length);
    }

    // The removal runs no item's after-render: the item's dispose asks the root to render, and the root's after-render
    // sends the one update.
    [Fact(DisplayName = "updates refs when an item unmounts from a nested state update")]
    [Trait("Requirement", "CMP-STA-10")]
    public void Updates_refs_when_an_item_unmounts_from_a_nested_state_update()
    {
        IRenderedComponent<TestCompositeRoot> cut = Context.Render<TestCompositeRoot>(p => p.AddChildContent(Nested(initiallyHidden: false)));
        Assert.Equal(3, CompositeTestContext.ItemIds((string)Assert.Single(host.Attaches).Arguments[1]!).Length);

        cut.InvokeAsync(() => cut.FindComponent<HideableItem>().Instance.SetHidden(true));

        string update = Assert.Single(host.UpdateOptions);
        Assert.Equal(CompositeTestContext.RenderedItemIds(cut).Order(), CompositeTestContext.ItemIds(update).Order());
        Assert.Equal(2, CompositeTestContext.ItemIds(update).Length);
    }

    // Port, 2026-10-22: Strict Mode's replay has no counterpart; the batch that replaces the items sends one update with
    // the new items, and an item that hides itself without the root rendering sends one more.
    [Fact(DisplayName = "keeps syncing refs after replacing items under Strict Mode")]
    [Trait("Requirement", "CMP-STA-10")]
    public void Keeps_syncing_refs_after_replacing_items_under_strict_mode()
    {
        IRenderedComponent<TestCompositeRoot> cut = Context.Render<TestCompositeRoot>(p => p.AddChildContent(Hideables("a", "b")));

        cut.Render(p => p.AddChildContent(Hideables("c", "d")));
        string replaced = Assert.Single(host.UpdateOptions);
        Assert.Equal(CompositeTestContext.RenderedItemIds(cut).Order(), CompositeTestContext.ItemIds(replaced).Order());

        cut.InvokeAsync(() => cut.FindComponents<HideableItem>()[1].Instance.SetHidden(true));

        Assert.Equal(2, host.UpdateOptions.Count);
        Assert.Equal(CompositeTestContext.RenderedItemIds(cut), CompositeTestContext.ItemIds(host.UpdateOptions[1]));
    }

    // The new element keeps the item's id, so the options do not change; the item reports its new element and the root
    // sends one update, which makes the module resolve the id again.
    [Fact(DisplayName = "registers a replacement render target")]
    [Trait("Requirement", "CMP-STA-10")]
    public void Registers_a_replacement_render_target()
    {
        IRenderedComponent<TestCompositeRoot> cut = Context.Render<TestCompositeRoot>(p => p.AddChildContent(builder =>
        {
            builder.OpenComponent<HideableItem>(0);
            builder.AddAttribute(1, nameof(HideableItem.Label), "item");
            builder.CloseComponent();
        }));
        string attached = (string)Assert.Single(host.Attaches).Arguments[1]!;

        cut.FindComponent<TestCompositeItem>().Render(p => p.Add(x => x.Tag, "button"));

        Assert.Equal("BUTTON", cut.Find("[data-item]").TagName);
        Assert.Equal(attached, Assert.Single(host.UpdateOptions));
    }

    // --- CMP-I18N-01 ------------------------------------------------------------------------------------------------

    // The C# half of the title: the root reads its direction from the nearest DirectionProvider (CMP-I18N-01); the key
    // mapping is the module's (L3, and L4 on the composite-rtl fixture).
    [Fact(DisplayName = "horizontal orientation")]
    [Trait("Requirement", "CMP-I18N-01")]
    public void Horizontal_orientation_reads_the_direction_of_the_nearest_provider()
    {
        Context.Render<DirectionProvider>(p => p
            .Add(x => x.Direction, TextDirection.Rtl)
            .AddChildContent<TestCompositeRoot>(root => root
                .Add(x => x.Orientation, Blazix.Primitives.Orientation.Horizontal)
                .AddChildContent(CompositeItems.Of("1", "2", "3"))));

        JsonAssert(Assert.Single(host.Attaches), "direction", "rtl");
        JsonAssert(Assert.Single(host.Attaches), "orientation", "horizontal");
    }

    [Fact(DisplayName = "reads ltr without a provider and sends one update when the provider's direction changes")]
    [Trait("Requirement", "CMP-I18N-01")]
    [Trait("Requirement", "UTL-I18N-01")]
    public void Reads_ltr_without_a_provider_and_sends_one_update_when_the_direction_changes()
    {
        Context.Render<TestCompositeRoot>(p => p.AddChildContent(CompositeItems.Of("1")));
        JsonAssert(Assert.Single(host.Attaches), "direction", "ltr");

        IRenderedComponent<DirectionProvider> provider = Context.Render<DirectionProvider>(p => p
            .Add(x => x.Direction, TextDirection.Rtl)
            .AddChildContent<IsolatedRoot>());
        Assert.Equal(2, host.Attaches.Count);
        JsonAssert(host.Attaches[1], "direction", "rtl");

        provider.Render(p => p.Add(x => x.Direction, TextDirection.Ltr));

        string update = Assert.Single(host.UpdateOptions);
        Assert.Equal("ltr", CompositeTestContext.Parse(update).GetProperty("direction").GetString());
    }

    // --- CMP-EVT-01 -------------------------------------------------------------------------------------------------

    // Port, 2026-10-22: the highlight is JavaScript's (ADR 04 Amendments 2026-10-22); C# mirrors the module's report.
    // The first publication's report of the marked item's index drives the mirror once, and a repeated report nothing.
    [Fact(DisplayName = "uses an active item explicit index as the initial highlighted index")]
    [Trait("Requirement", "CMP-EVT-01")]
    public void Uses_an_active_item_explicit_index_as_the_initial_highlighted_index()
    {
        List<int> changes = [];
        IRenderedComponent<TestCompositeRoot> cut = Context.Render<TestCompositeRoot>(p => p
            .Add(x => x.ReportHighlight, true)
            .Add(x => x.OnHighlightChange, (int index) => changes.Add(index))
            .AddChildContent(CompositeItems.Of("zero", "one", "two")));
        JsonAssert(Assert.Single(host.Attaches), "reportHighlight", true);
        Assert.Equal(0, cut.Instance.HighlightedIndex);

        cut.InvokeAsync(() => host.Twin.OnHighlightChange("""{"index":2}"""));
        cut.InvokeAsync(() => host.Twin.OnHighlightChange("""{"index":2}"""));

        Assert.Equal(2, cut.Instance.HighlightedIndex);
        Assert.Equal(new[] { 2 }, changes);
    }

    [Fact(DisplayName = "raises FocusLeft with the event of the leaving blur")]
    [Trait("Requirement", "RDG-FOC-04")]
    public void Raises_focus_left_with_the_event_of_the_leaving_blur()
    {
        List<EventSnapshot> left = [];
        IRenderedComponent<TestCompositeRoot> cut = Context.Render<TestCompositeRoot>(p => p
            .Add(x => x.ReportFocusLeave, true)
            .Add(x => x.OnFocusLeave, (EventSnapshot snapshot) => left.Add(snapshot))
            .AddChildContent(CompositeItems.Of("a")));

        cut.InvokeAsync(() => host.Twin.OnFocusLeave($$"""{"event":{{FocusJson.Replace("focusin", "focusout")}}}"""));

        Assert.Equal("focusout", Assert.Single(left).Type);
    }

    // --- the root's consumer onkeydown (ADR 02 Amendments 2026-10-21; EVD-EVT-26, CMP-EVT-12) ----------------------

    [Fact(DisplayName = "the root declines the consumer's onkeydown and runs it once from its own handler")]
    [Trait("Requirement", "CMP-EVT-12")]
    public void The_root_declines_the_consumers_onkeydown_and_runs_it_once()
    {
        int runs = 0;
        IRenderedComponent<TestCompositeRoot> cut = Context.Render<TestCompositeRoot>(p => p
            .Add(x => x.StopEventPropagation, false)
            .AddUnmatched("onkeydown", EventCallback.Factory.Create<KeyboardEventArgs>(this, () => runs++))
            .AddChildContent(CompositeItems.Of("a", "b")));

        cut.Find("div").KeyDown(new KeyboardEventArgs { Key = "Enter", Code = "Enter" });

        Assert.Equal(1, runs);
    }

    [Fact(DisplayName = "the navigate decision runs the consumer's onkeydown inside the dispatch, and Blazor's dispatch of that key does not run it again")]
    [Trait("Requirement", "CMP-EVT-12")]
    [Trait("Requirement", "EVD-EVT-26")]
    public void The_navigate_decision_runs_the_consumers_onkeydown_once()
    {
        int runs = 0;
        IRenderedComponent<TestCompositeRoot> cut = Context.Render<TestCompositeRoot>(p => p
            .Add(x => x.StopEventPropagation, false)
            .AddUnmatched("onkeydown", EventCallback.Factory.Create<KeyboardEventArgs>(this, () => runs++))
            .AddChildContent(CompositeItems.Of("a", "b")));

        string verdict = host.Twin.Decide(CompositeTwin.NavigatePoint, KeyDownJson("ArrowDown"));
        Assert.Equal("run", verdict);
        Assert.Equal(1, runs);

        cut.Find("div").KeyDown(new KeyboardEventArgs { Key = "ArrowDown", Code = "ArrowDown" });
        Assert.Equal(1, runs);

        cut.Find("div").KeyDown(new KeyboardEventArgs { Key = "ArrowDown", Code = "ArrowDown" });
        Assert.Equal(2, runs);
    }

    [Fact(DisplayName = "the navigate decision answers stop when the consumer's onkeydown calls PreventPartHandler()")]
    [Trait("Requirement", "CMP-EVT-12")]
    [Trait("Requirement", "EVD-EVT-26")]
    public void The_navigate_decision_answers_stop_after_PreventPartHandler()
    {
        int runs = 0;
        IRenderedComponent<TestCompositeRoot> cut = Context.Render<TestCompositeRoot>(p => p
            .AddUnmatched("onkeydown", EventCallback.Factory.Create<KeyboardEventArgs>(this, (KeyboardEventArgs e) =>
            {
                runs++;
                e.PreventPartHandler();
            }))
            .AddChildContent(CompositeItems.Of("a", "b")));

        Assert.Equal("stop", host.Twin.Decide(CompositeTwin.NavigatePoint, KeyDownJson("ArrowRight")));

        // A vetoed key is not stopped, so Blazor dispatches it; the handler already ran.
        cut.Find("div").KeyDown(new KeyboardEventArgs { Key = "ArrowRight", Code = "ArrowRight" });
        Assert.Equal(1, runs);
    }

    // With stopEventPropagation (the default) a key the module handles never reaches Blazor's dispatcher, so the decision
    // leaves no marker that would swallow the next identical key.
    [Fact(DisplayName = "a run verdict with stopEventPropagation leaves no one-shot marker behind")]
    [Trait("Requirement", "CMP-EVT-12")]
    public void A_run_verdict_with_stop_event_propagation_leaves_no_marker()
    {
        int runs = 0;
        IRenderedComponent<TestCompositeRoot> cut = Context.Render<TestCompositeRoot>(p => p
            .AddUnmatched("onkeydown", EventCallback.Factory.Create<KeyboardEventArgs>(this, () => runs++))
            .AddChildContent(CompositeItems.Of("a", "b")));

        Assert.Equal("run", host.Twin.Decide(CompositeTwin.NavigatePoint, KeyDownJson("ArrowDown")));
        cut.Find("div").KeyDown(new KeyboardEventArgs { Key = "ArrowDown", Code = "ArrowDown" });

        Assert.Equal(2, runs);
    }

    [Fact(DisplayName = "the select decision runs the part's selection hook and answers its verdict")]
    [Trait("Requirement", "RAD-FOC-05")]
    public void The_select_decision_runs_the_parts_selection_hook()
    {
        List<string?> targets = [];
        Context.Render<TestCompositeRoot>(p => p
            .Add(x => x.SelectOnNavigation, true)
            .Add(x => x.SelectionDecider, snapshot =>
            {
                targets.Add(snapshot.Target?.Id);
                FocusEventArgs args = new() { Type = snapshot.Type };
                args.PreventPartHandler();
                return args.PartHandlerPrevented() ? DecisionPolicy.Stop : DecisionPolicy.Run;
            })
            .AddChildContent(CompositeItems.Of("a")));

        Assert.Equal("stop", host.Twin.Decide(CompositeTwin.SelectPoint, FocusJson));
        Assert.Equal(new string?[] { "base-ui-2" }, targets);
    }

    // --- CMP-STR-02, CMP-EDGE-01 (internal guards) --------------------------------------------------------------------

    [Fact(DisplayName = "an item rendered outside a composite root throws")]
    [Trait("Requirement", "CMP-STR-02")]
    [Trait("Requirement", "CMP-EDGE-01")]
    public void An_item_rendered_outside_a_composite_root_throws()
    {
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => Context.Render<TestCompositeItem>());

        Assert.Equal(
            "Blazix: `TestCompositeItem` must be placed within a composite root. A composite item registers with the nearest root, which owns its tab stop.",
            error.Message);
    }

    private static void JsonAssert(JSRuntimeInvocation attach, string name, string expected)
    {
        Assert.Equal(expected, CompositeTestContext.Parse((string)attach.Arguments[1]!).GetProperty(name).GetString());
    }

    private static void JsonAssert(JSRuntimeInvocation attach, string name, bool expected)
    {
        Assert.Equal(expected, CompositeTestContext.Parse((string)attach.Arguments[1]!).GetProperty(name).GetBoolean());
    }

    private static RenderFragment Nested(bool initiallyHidden) => builder =>
    {
        builder.OpenComponent<TestCompositeItem>(0);
        builder.AddAttribute(1, "data-item", "first");
        builder.CloseComponent();
        builder.OpenComponent<HideableItem>(2);
        builder.AddAttribute(3, nameof(HideableItem.Label), "nested");
        builder.AddAttribute(4, nameof(HideableItem.InitiallyHidden), initiallyHidden);
        builder.CloseComponent();
        builder.OpenComponent<TestCompositeItem>(5);
        builder.AddAttribute(6, "data-item", "last");
        builder.CloseComponent();
    };

    private static RenderFragment Hideables(params string[] labels) => builder =>
    {
        foreach (string label in labels)
        {
            builder.OpenComponent<HideableItem>(0);
            builder.SetKey(label);
            builder.AddAttribute(1, nameof(HideableItem.Label), label);
            builder.CloseComponent();
        }
    };

    /// <summary>A root whose parent never re-renders it, so only the provider's notification can reach it.</summary>
    private sealed class IsolatedRoot : ComponentBase
    {
        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.OpenComponent<TestCompositeRoot>(0);
            builder.AddAttribute(1, "ChildContent", CompositeItems.Of("x"));
            builder.CloseComponent();
        }

        protected override bool ShouldRender() => false;
    }
}

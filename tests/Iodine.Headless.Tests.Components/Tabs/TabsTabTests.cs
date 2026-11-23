using AngleSharp.Dom;
using Blazix.Primitives.Interop;
using Blazix.Primitives.Rendering;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using static Blazix.Primitives.Tests.Components.TabsTestContext;

// The namespace is the project's root: a namespace named after the family would hide the parts' types.
namespace Blazix.Primitives.Tests.Components;

// TabsTab.test.tsx, the [L2] rows and the Tab's C# reactions to the `tabs` module (issue 104): its element, ids and ARIA,
// the button mechanism in composite mode, the composite item, and the activation the module hands over (DecideSelectAsync,
// WebAssembly) or reports (SelectAsync, Server; ADR 04 Amendments 2026-10-26). Port titles are marked as such.
/// <summary>The tab: its element, its attributes and its activation.</summary>
public sealed class TabsTabTests : IDisposable
{
    private readonly TabsTestContext test = new();

    private BunitContext Context => test.Context;

    /// <inheritdoc />
    public void Dispose() => test.Dispose();

    [Fact(DisplayName = "throws a descriptive error when rendered outside <Tabs.List>")]
    [Trait("Requirement", "TAB-EDGE-02")]
    [Trait("Requirement", "TAB-STR-03")]
    public void Throws_a_descriptive_error_when_rendered_outside_Tabs_List()
    {
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() =>
            Context.Render<TabsRoot<string>>(p => p.AddChildContent(Tab("1"))));

        Assert.Equal("Blazix: The `TabsList` context is missing. `TabsTab` and `TabsIndicator` must be placed within `<TabsList>`.", error.Message);
    }

    [Fact(DisplayName = "port: a tab outside a root throws the root's error first")]
    [Trait("Requirement", "TAB-EDGE-01")]
    [Trait("Requirement", "TAB-STR-03")]
    public void Port_a_tab_outside_a_root_throws_the_roots_error_first()
    {
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => Context.Render(Tab("1")));

        Assert.Equal("Blazix: The `TabsRoot` context is missing. Tabs parts must be placed within `<TabsRoot>`.", error.Message);
    }

    [Fact(DisplayName = "port: a tab whose TValue differs from its root's throws")]
    [Trait("Requirement", "TAB-API-07")]
    public void Port_a_tab_whose_TValue_differs_from_its_roots_throws()
    {
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() =>
            Context.Render<TabsRoot<int?>>(p => p.Add(x => x.Value, 1).AddChildContent(List(Tab(1)))));

        Assert.StartsWith("Blazix: A `TabsTab` must have the `TValue` of its `TabsRoot`.", error.Message, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "port: a native tab is a button of type button with aria-disabled and never the disabled attribute")]
    [Trait("Requirement", "TAB-DOM-10")]
    [Trait("Requirement", "TAB-ARIA-03")]
    [Trait("Requirement", "TAB-ARIA-06")]
    [Trait("Requirement", "TAB-DOM-06")]
    [Trait("Requirement", "TAB-API-08")]
    public void Port_a_native_tab_is_a_button_with_aria_disabled_and_never_disabled()
    {
        IRenderedComponent<TabsRoot<int>> cut = Context.Render<TabsRoot<int>>(p => p
            .Add(x => x.Value, 0)
            .AddChildContent(List(Tab(0), Tab(1, t => t.Add(x => x.Disabled, true)))));

        IReadOnlyList<IElement> tabs = Tabs(cut);
        Assert.All(tabs, tab =>
        {
            Assert.Equal("BUTTON", tab.TagName);
            Assert.Equal("button", tab.GetAttribute("type"));
            Assert.Equal("tab", tab.GetAttribute("role"));
            Assert.False(tab.HasAttribute("disabled"));
        });
        Assert.Equal("false", tabs[0].GetAttribute("aria-disabled"));
        Assert.False(tabs[0].HasAttribute(TabsTabDataAttributes.Disabled));
        Assert.Equal("true", tabs[1].GetAttribute("aria-disabled"));
        Assert.True(tabs[1].HasAttribute(TabsTabDataAttributes.Disabled));
        Assert.Contains("\"focusableWhenDisabled\":true", test.ButtonModule.Invocations["attach"][1].Arguments[1] as string, StringComparison.Ordinal);
        Assert.Contains("\"composite\":true", test.ButtonModule.Invocations["attach"][1].Arguments[1] as string, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "renders as an anchor and toggles selection when `nativeButton` is false")]
    [Trait("Requirement", "TAB-API-09")]
    [Trait("Requirement", "TAB-ARIA-03")]
    [Trait("Requirement", "TAB-KEY-16")]
    public async Task Renders_as_an_anchor_and_toggles_selection_when_nativeButton_is_false()
    {
        RenderFragment Anchor(string href) => builder =>
        {
            builder.OpenElement(0, "a");
            builder.AddAttribute(1, "href", href);
            builder.CloseElement();
        };

        IRenderedComponent<TabsRoot<string>> cut = Context.Render<TabsRoot<string>>(p => p
            .Add(x => x.DefaultValue, "overview")
            .AddChildContent(List(
                Tab("overview", t => t.Add(x => x.NativeButton, false).AddUnmatched("Render", Anchor("#overview")).AddChildContent("Overview")),
                Tab("details", t => t.Add(x => x.NativeButton, false).AddUnmatched("Render", Anchor("#details")).AddChildContent("Details")))));

        IReadOnlyList<IElement> tabs = Tabs(cut);
        Assert.Equal("A", tabs[0].TagName);
        Assert.Equal("#overview", tabs[0].GetAttribute("href"));
        Assert.False(tabs[0].HasAttribute("type"));
        Assert.False(tabs[0].HasAttribute("aria-disabled"));
        Assert.Equal("true", tabs[0].GetAttribute("aria-selected"));
        Assert.Equal("false", tabs[1].GetAttribute("aria-selected"));

        await test.SelectAsync(cut, tabs[1].Id!, "right");

        Assert.Equal("false", Tabs(cut)[0].GetAttribute("aria-selected"));
        Assert.Equal("true", Tabs(cut)[1].GetAttribute("aria-selected"));
    }

    [Fact(DisplayName = "port: every tab has an id, the consumer's verbatim or a generated one")]
    [Trait("Requirement", "TAB-DOM-04")]
    [Trait("Requirement", "TAB-API-10")]
    public void Port_every_tab_has_an_id()
    {
        IRenderedComponent<TabsRoot<int>> cut = Context.Render<TabsRoot<int>>(p => p
            .Add(x => x.Value, 0)
            .AddChildContent(List(Tab(0), Tab(1, t => t.AddUnmatched("id", "explicit-id")))));

        IReadOnlyList<IElement> tabs = Tabs(cut);
        Assert.StartsWith("base-ui-", tabs[0].Id, StringComparison.Ordinal);
        Assert.Equal("explicit-id", tabs[1].Id);
        Assert.Contains("\"id\":\"explicit-id\"", test.LastCompositeOptions, StringComparison.Ordinal);
        Assert.Contains($"\"id\":\"{tabs[0].Id}\"", test.LastCompositeOptions, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "port: every tab renders tabindex -1, the active marker and the handover direction")]
    [Trait("Requirement", "TAB-DOM-05")]
    [Trait("Requirement", "TAB-DOM-07")]
    [Trait("Requirement", "TAB-DOM-08")]
    [Trait("Requirement", "TAB-DOM-09")]
    public void Port_every_tab_renders_the_handover_values_and_the_active_marker()
    {
        IRenderedComponent<TabsRoot<int>> cut = Context.Render<TabsRoot<int>>(p => p
            .Add(x => x.Value, 1)
            .AddChildContent(List(Tab(0), Tab(1))));

        IReadOnlyList<IElement> tabs = Tabs(cut);
        Assert.All(tabs, tab =>
        {
            Assert.Equal("-1", tab.GetAttribute("tabindex"));
            Assert.Equal("horizontal", tab.GetAttribute(TabsTabDataAttributes.Orientation));
            Assert.Equal("none", tab.GetAttribute(TabsTabDataAttributes.ActivationDirection));
        });
        Assert.False(tabs[0].HasAttribute(CompositeAttributes.ActiveItem));
        Assert.Equal(string.Empty, tabs[1].GetAttribute(CompositeAttributes.ActiveItem));
        Assert.False(tabs[0].HasAttribute(TabsTabDataAttributes.Active));
        Assert.Equal(string.Empty, tabs[1].GetAttribute(TabsTabDataAttributes.Active));
    }

    // TabsTab.test.tsx "exposes tab activation direction through the render prop" (browser only): the browser half is L4;
    // here the C# reaction to the module's report (ADR 01's RND-STA-01 row): the state the render function receives
    // follows the report while the rendered attribute keeps the handover value, which the module owns.
    [Fact(DisplayName = "port: the render function's state mirrors the reported activation direction")]
    [Trait("Requirement", "TAB-STA-20")]
    [Trait("Requirement", "TAB-STA-14")]
    public async Task Port_the_render_functions_state_mirrors_the_reported_activation_direction()
    {
        List<TabsTabState> states = [];
        RenderFragment<RenderProps<TabsTabState>> render = props => builder =>
        {
            states.Add(props.State);
            builder.OpenElement(0, "button");
            builder.AddMultipleAttributes(1, props.Attributes);
            builder.CloseElement();
        };

        IRenderedComponent<TabsRoot<int>> cut = Context.Render<TabsRoot<int>>(p => p
            .Add(x => x.DefaultValue, 0)
            .AddChildContent(List(Tab(0), Tab(1, t => t.Add(x => x.RenderFunction, render)))));

        Assert.Equal(TabsActivationDirection.None, states[^1].TabActivationDirection);
        Assert.False(states[^1].Active);

        await test.SelectAsync(cut, Tabs(cut)[1].Id!, "right");
        await test.ReportDirectionAsync(cut, "right");

        Assert.Equal(TabsActivationDirection.Right, states[^1].TabActivationDirection);
        Assert.True(states[^1].Active);
        Assert.Equal("none", Tabs(cut)[1].GetAttribute(TabsTabDataAttributes.ActivationDirection));
    }

    [Fact(DisplayName = "port: a reported click activates the tab with reason none, its event and the measured direction")]
    [Trait("Requirement", "TAB-STA-03")]
    [Trait("Requirement", "TAB-EVT-01")]
    [Trait("Requirement", "TAB-EVT-02")]
    [Trait("Requirement", "TAB-PTR-01")]
    public async Task Port_a_reported_click_activates_the_tab()
    {
        List<ValueChangeEventArgs<int>> calls = [];
        IRenderedComponent<TabsRoot<int>> cut = Context.Render<TabsRoot<int>>(p => p
            .Add(x => x.DefaultValue, 0)
            .Add(x => x.OnValueChange, args => calls.Add(args))
            .AddChildContent(List(Tab(0), Tab(1))));

        await test.SelectAsync(cut, Tabs(cut)[1].Id!, "right");

        ValueChangeEventArgs<int> call = Assert.Single(calls);
        Assert.Equal(1, call.Value);
        TabsChangeEventDetails details = Assert.IsType<TabsChangeEventDetails>(call.Details);
        Assert.Equal(EventReason.None, details.Reason);
        Assert.Equal(TabsActivationDirection.Right, details.ActivationDirection);
        Assert.Equal("click", details.Event.Type);
        Assert.Equal(EventKind.PointerEvent, details.Event.Kind);
        Assert.Null(details.Trigger);
        Assert.Equal("true", Tabs(cut)[1].GetAttribute("aria-selected"));
    }

    [Fact(DisplayName = "port: the selection decision activates the tab inside the dispatch and the focus event is the details' event")]
    [Trait("Requirement", "TAB-KEY-10")]
    [Trait("Requirement", "TAB-EVT-02")]
    [Trait("Requirement", "TAB-PTR-01")]
    public async Task Port_the_selection_decision_activates_the_tab_inside_the_dispatch()
    {
        List<ValueChangeEventArgs<int>> calls = [];
        IRenderedComponent<TabsRoot<int>> cut = Context.Render<TabsRoot<int>>(p => p
            .Add(x => x.DefaultValue, 0)
            .Add(x => x.OnValueChange, args => calls.Add(args))
            .AddChildContent(Part<TabsList>(l => l.Add(x => x.ActivateOnFocus, true).AddChildContent(Fragments(Tab(0), Tab(1))))));

        string verdict = await test.DecideSelectAsync(cut, Tabs(cut)[1].Id!, "left", FocusJson(Tabs(cut)[1].Id));

        Assert.Equal("run", verdict);
        ValueChangeEventArgs<int> call = Assert.Single(calls);
        TabsChangeEventDetails details = Assert.IsType<TabsChangeEventDetails>(call.Details);
        Assert.Equal(TabsActivationDirection.Left, details.ActivationDirection);
        Assert.Equal(EventKind.FocusEvent, details.Event.Kind);
        Assert.Equal("focusin", details.Event.Type);
        Assert.Equal("true", Tabs(cut)[1].GetAttribute("aria-selected"));
        Assert.Contains("\"activateOnFocus\":true", test.TabsAttaches[0], StringComparison.Ordinal);
    }

    [Fact(DisplayName = "port: an active or disabled tab requests nothing")]
    [Trait("Requirement", "TAB-PTR-02")]
    [Trait("Requirement", "TAB-PTR-03")]
    [Trait("Requirement", "TAB-API-08")]
    public async Task Port_an_active_or_disabled_tab_requests_nothing()
    {
        int calls = 0;
        IRenderedComponent<TabsRoot<int>> cut = Context.Render<TabsRoot<int>>(p => p
            .Add(x => x.DefaultValue, 0)
            .Add(x => x.OnValueChange, _ => calls++)
            .AddChildContent(List(Tab(0), Tab(1, t => t.Add(x => x.Disabled, true)))));

        await test.SelectAsync(cut, Tabs(cut)[0].Id!);
        await test.DecideSelectAsync(cut, Tabs(cut)[1].Id!);
        await test.SelectAsync(cut, Tabs(cut)[1].Id!);

        Assert.Equal(0, calls);
        Assert.Equal("true", Tabs(cut)[0].GetAttribute("aria-selected"));
        Assert.Equal("false", Tabs(cut)[1].GetAttribute("aria-selected"));
    }

    [Fact(DisplayName = "port: the consumer's onclick runs before the activation, and PreventPartHandler() in it vetoes the activation")]
    [Trait("Requirement", "TAB-PTR-09")]
    public async Task Port_the_consumers_onclick_vetoes_the_activation()
    {
        List<string> calls = [];
        bool veto = true;
        IRenderedComponent<TabsRoot<int>> cut = Context.Render<TabsRoot<int>>(p => p
            .Add(x => x.DefaultValue, 0)
            .Add(x => x.OnValueChange, args => calls.Add($"change {args.Value}"))
            .AddChildContent(List(
                Tab(0),
                Tab(1, t => t.AddUnmatched("onclick", EventCallback.Factory.Create<MouseEventArgs>(this, e =>
                {
                    calls.Add("click");
                    if (veto)
                    {
                        e.PreventPartHandler();
                    }
                }))))));

        // The tab declines the consumer's handler: the module runs it.
        Assert.False(Tabs(cut)[1].HasAttribute("blazor:onclick"));
        Assert.Contains("\"reportClicks\":true", test.TabsAttaches[0], StringComparison.Ordinal);

        Assert.Equal("stop", await test.DecideSelectAsync(cut, Tabs(cut)[1].Id!, "right"));
        await test.SelectAsync(cut, Tabs(cut)[1].Id!, "right");
        Assert.Equal(["click", "click"], calls);
        Assert.Equal("false", Tabs(cut)[1].GetAttribute("aria-selected"));

        veto = false;
        await test.SelectAsync(cut, Tabs(cut)[1].Id!, "right");
        Assert.Equal(["click", "click", "click", "change 1"], calls);
        Assert.Equal("true", Tabs(cut)[1].GetAttribute("aria-selected"));
    }

    [Fact(DisplayName = "port: the consumer's onfocus runs for a reported focus that activates nothing")]
    [Trait("Requirement", "TAB-PTR-09")]
    public async Task Port_the_consumers_onfocus_runs_for_a_reported_focus()
    {
        List<string?> calls = [];
        IRenderedComponent<TabsRoot<int>> cut = Context.Render<TabsRoot<int>>(p => p
            .Add(x => x.DefaultValue, 0)
            .AddChildContent(List(Tab(0, t => t.AddUnmatched("onfocus", EventCallback.Factory.Create<FocusEventArgs>(this, e => calls.Add(e.Type)))))));

        Assert.False(Tabs(cut)[0].HasAttribute("blazor:onfocus"));
        Assert.Contains("\"reportFocus\":true", test.TabsAttaches[0], StringComparison.Ordinal);

        await test.ReportTabEventAsync(cut, Tabs(cut)[0].Id!, FocusJson(Tabs(cut)[0].Id));

        Assert.Equal(["focusin"], calls);
    }

    [Fact(DisplayName = "port: a canceled activation keeps an uncontrolled selection")]
    [Trait("Requirement", "TAB-EVT-03")]
    [Trait("Requirement", "TAB-STA-03")]
    public async Task Port_a_canceled_activation_keeps_an_uncontrolled_selection()
    {
        IRenderedComponent<TabsRoot<int>> cut = Context.Render<TabsRoot<int>>(p => p
            .Add(x => x.DefaultValue, 0)
            .Add(x => x.OnValueChange, args => args.Details.Cancel())
            .AddChildContent(List(Tab(0), Tab(1))));

        await test.DecideSelectAsync(cut, Tabs(cut)[1].Id!, "right");
        await test.SelectAsync(cut, Tabs(cut)[1].Id!, "right");

        Assert.Equal("true", Tabs(cut)[0].GetAttribute("aria-selected"));
        Assert.Equal("false", Tabs(cut)[1].GetAttribute("aria-selected"));
    }

    [Fact(DisplayName = "port: a controlled root reports the activation and commits nothing")]
    [Trait("Requirement", "TAB-STA-03")]
    [Trait("Requirement", "TAB-KEY-14")]
    [Trait("Requirement", "TAB-API-01")]
    public async Task Port_a_controlled_root_reports_the_activation_and_commits_nothing()
    {
        List<int> changes = [];
        List<int> bound = [];
        IRenderedComponent<TabsRoot<int>> cut = Context.Render<TabsRoot<int>>(p => p
            .Add(x => x.Value, 0)
            .Add(x => x.OnValueChange, args => changes.Add(args.Value))
            .Add(x => x.ValueChanged, value => bound.Add(value))
            .AddChildContent(List(Tab(0), Tab(1))));

        await test.SelectAsync(cut, Tabs(cut)[1].Id!, "right");

        Assert.Equal([1], changes);
        Assert.Equal([1], bound);
        Assert.Equal("true", Tabs(cut)[0].GetAttribute("aria-selected"));
        Assert.Contains("\"controlled\":true", test.TabsAttaches[0], StringComparison.Ordinal);
    }

    // TabsTab.test.tsx "settles when the rendered component recreates its merged ref", restated under ADR 06 Amendments
    // 2026-10-18 rule 2 (ADR 13 §3): React re-creates the ref callback, a Blazor substitute replaces the element. The
    // registration survives and the composite resolves the new element (the resize observation is L4).
    [Fact(DisplayName = "port: a tab whose rendered element is replaced keeps its registration")]
    [Trait("Requirement", "TAB-EDGE-11")]
    [Trait("Requirement", "TAB-DOM-09")]
    public async Task Port_a_tab_whose_rendered_element_is_replaced_keeps_its_registration()
    {
        RenderFragment Element(string tag) => builder =>
        {
            builder.OpenElement(0, tag);
            builder.CloseElement();
        };

        string tag = "a";
        RenderFragment Build() => List(
            Tab("overview", t => t.Add(x => x.NativeButton, false).AddUnmatched("Render", Element(tag))),
            Tab("details"));

        IRenderedComponent<TabsRoot<string>> cut = Context.Render<TabsRoot<string>>(p => p
            .Add(x => x.DefaultValue, "overview")
            .AddChildContent(Build()));
        string id = Tabs(cut)[0].Id!;
        int updates = test.CompositeUpdates.Count;

        tag = "span";
        cut.Render(p => p.Add(x => x.DefaultValue, "overview").AddChildContent(Build()));

        Assert.Equal("SPAN", Tabs(cut)[0].TagName);
        Assert.Equal(id, Tabs(cut)[0].Id);
        Assert.True(test.CompositeUpdates.Count > updates);
        Assert.Contains($"\"id\":\"{id}\"", test.LastCompositeOptions, StringComparison.Ordinal);

        await test.SelectAsync(cut, Tabs(cut)[1].Id!);
        await test.SelectAsync(cut, id);
        Assert.Equal("true", Tabs(cut)[0].GetAttribute("aria-selected"));
    }
}

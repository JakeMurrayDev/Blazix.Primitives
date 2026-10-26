using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using static Blazix.Primitives.Tests.Components.TabsTestContext;

// The namespace is the project's root: a namespace named after the family would hide the parts' types.
namespace Blazix.Primitives.Tests.Components;

// TabsList.test.tsx, the [L2] rows (issue 104), and the List's configuration of the composite root and of its `tabs`
// handle (TAB §6.1, §11; ADR 04 Amendments 2026-10-26). The keyboard and the tab stop are L4 (tests/browser/tabs.spec.ts).
/// <summary>The tab list: its element, its ARIA and its two twins' options.</summary>
public sealed class TabsListTests : IDisposable
{
    private readonly TabsTestContext test = new();

    private BunitContext Context => test.Context;

    /// <inheritdoc />
    public void Dispose() => test.Dispose();

    [Fact(DisplayName = "sets the aria-selected attribute on the active tab")]
    [Trait("Requirement", "TAB-ARIA-04")]
    [Trait("Requirement", "TAB-STA-03")]
    public async Task Sets_the_aria_selected_attribute_on_the_active_tab()
    {
        IRenderedComponent<TabsRoot<int>> cut = Context.Render<TabsRoot<int>>(p => p
            .Add(x => x.DefaultValue, 1)
            .AddChildContent(List(Tab(1), Tab(2), Tab(3))));

        Assert.Equal(["true", "false", "false"], Tabs(cut).Select(tab => tab.GetAttribute("aria-selected")));

        await test.SelectAsync(cut, Tabs(cut)[1].Id!, "right");
        Assert.Equal(["false", "true", "false"], Tabs(cut).Select(tab => tab.GetAttribute("aria-selected")));

        await test.SelectAsync(cut, Tabs(cut)[2].Id!, "right");
        Assert.Equal(["false", "false", "true"], Tabs(cut).Select(tab => tab.GetAttribute("aria-selected")));

        await test.SelectAsync(cut, Tabs(cut)[0].Id!, "left");
        Assert.Equal(["true", "false", "false"], Tabs(cut).Select(tab => tab.GetAttribute("aria-selected")));
    }

    [Fact(DisplayName = "can be named via `aria-label`")]
    [Trait("Requirement", "TAB-ARIA-10")]
    [Trait("Requirement", "TAB-ARIA-01")]
    public void Can_be_named_via_aria_label()
    {
        IRenderedComponent<TabsRoot<int>> cut = Context.Render<TabsRoot<int>>(p => p
            .Add(x => x.DefaultValue, 0)
            .AddChildContent(Part<TabsList>(l => l.AddUnmatched("aria-label", "string label").AddChildContent(Tab(0)))));

        IElement list = cut.Find("[role=tablist]");
        Assert.Equal("string label", list.GetAttribute("aria-label"));
        Assert.False(list.HasAttribute("aria-labelledby"));
    }

    [Fact(DisplayName = "can be named via `aria-labelledby`")]
    [Trait("Requirement", "TAB-ARIA-10")]
    public void Can_be_named_via_aria_labelledby()
    {
        IRenderedComponent<TabsRoot<int>> cut = Context.Render<TabsRoot<int>>(p => p
            .Add(x => x.DefaultValue, 0)
            .AddChildContent(Part<TabsList>(l => l.AddUnmatched("aria-labelledby", "label-id").AddChildContent(Tab(0)))));

        IElement list = cut.Find("[role=tablist]");
        Assert.Equal("label-id", list.GetAttribute("aria-labelledby"));
        Assert.False(list.HasAttribute("aria-label"));
    }

    [Fact(DisplayName = "port: the consumer's role wins over tablist")]
    [Trait("Requirement", "TAB-ARIA-01")]
    public void Port_the_consumers_role_wins_over_tablist()
    {
        IRenderedComponent<TabsRoot<int>> cut = Context.Render<TabsRoot<int>>(p => p
            .Add(x => x.DefaultValue, 0)
            .AddChildContent(Part<TabsList>(l => l.AddUnmatched("role", "toolbar").AddUnmatched("data-testid", "list"))));

        Assert.Equal("toolbar", cut.Find("[data-testid=list]").GetAttribute("role"));
    }

    [Fact(DisplayName = "port: the list renders the root's orientation and the handover direction and no tabindex")]
    [Trait("Requirement", "TAB-DOM-03")]
    [Trait("Requirement", "TAB-STR-02")]
    public void Port_the_list_renders_the_roots_orientation_and_the_handover_direction()
    {
        IRenderedComponent<TabsRoot<int>> cut = Context.Render<TabsRoot<int>>(p => p
            .Add(x => x.DefaultValue, 0)
            .Add(x => x.Orientation, Blazix.Primitives.Orientation.Vertical)
            .AddChildContent(List(Tab(0))));

        IElement list = cut.Find("[role=tablist]");
        Assert.Equal("DIV", list.TagName);
        Assert.Equal("vertical", list.GetAttribute(TabsListDataAttributes.Orientation));
        Assert.Equal("none", list.GetAttribute(TabsListDataAttributes.ActivationDirection));
        Assert.False(list.HasAttribute("tabindex"));
    }

    [Fact(DisplayName = "port: the list configures the composite root for tabs")]
    [Trait("Requirement", "TAB-KEY-01")]
    [Trait("Requirement", "TAB-KEY-07")]
    [Trait("Requirement", "TAB-API-06")]
    [Trait("Requirement", "TAB-PTR-08")]
    public void Port_the_list_configures_the_composite_root_for_tabs()
    {
        IRenderedComponent<TabsRoot<int>> cut = Context.Render<TabsRoot<int>>(p => p
            .Add(x => x.DefaultValue, 0)
            .AddChildContent(Part<TabsList>(l => l.Add(x => x.LoopFocus, false).AddChildContent(Fragments(Tab(0), Tab(1))))));

        string options = test.LastCompositeOptions;
        Assert.Contains("\"orientation\":\"horizontal\"", options, StringComparison.Ordinal);
        Assert.Contains("\"loopFocus\":false", options, StringComparison.Ordinal);
        Assert.Contains("\"enableHomeAndEndKeys\":true", options, StringComparison.Ordinal);
        Assert.Contains("\"stopEventPropagation\":true", options, StringComparison.Ordinal);
        Assert.Contains("\"highlightItemOnHover\":false", options, StringComparison.Ordinal);
        Assert.Contains("\"modifierKeys\":[]", options, StringComparison.Ordinal);
        Assert.Contains("\"disabledIndices\":[]", options, StringComparison.Ordinal);

        cut.Render(p => p.Add(x => x.DefaultValue, 0).AddChildContent(Part<TabsList>(l => l.Add(x => x.LoopFocus, true).AddChildContent(Fragments(Tab(0), Tab(1))))));
        Assert.Contains("\"loopFocus\":true", test.LastCompositeOptions, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "port: the list attaches its tabs handle with the activation options and sends their changes")]
    [Trait("Requirement", "TAB-API-05")]
    [Trait("Requirement", "TAB-STA-15")]
    public void Port_the_list_attaches_its_tabs_handle()
    {
        IRenderedComponent<TabsRoot<int>> cut = Context.Render<TabsRoot<int>>(p => p
            .Add(x => x.DefaultValue, 0)
            .Add(x => x.Orientation, Blazix.Primitives.Orientation.Vertical)
            .AddChildContent(List(Tab(0))));

        Assert.Equal(
            """{"part":"list","orientation":"vertical","activateOnFocus":false,"controlled":false,"reportClicks":false,"reportFocus":false}""",
            Assert.Single(test.TabsAttaches));

        cut.Render(p => p.Add(x => x.DefaultValue, 0).Add(x => x.Orientation, Blazix.Primitives.Orientation.Vertical).AddChildContent(Part<TabsList>(l => l.Add(x => x.ActivateOnFocus, true).AddChildContent(Tab(0)))));

        Assert.Equal(
            """{"part":"list","orientation":"vertical","activateOnFocus":true,"controlled":false,"reportClicks":false,"reportFocus":false}""",
            Assert.Single(test.TabsUpdates));
    }

    // TAB-KEY-09, EVD-EVT-26 (option (i) of ADR 04's note of 2026-10-22): the List declines its consumer onkeydown, which
    // the composite's `navigate` decision runs inside the dispatch on WebAssembly; the key itself is L4.
    [Fact(DisplayName = "port: the list's consumer onkeydown runs inside the navigation decision and can veto it")]
    [Trait("Requirement", "TAB-KEY-09")]
    public async Task Port_the_lists_consumer_onkeydown_runs_inside_the_navigation_decision()
    {
        List<string> keys = [];
        IRenderedComponent<TabsRoot<int>> cut = Context.Render<TabsRoot<int>>(p => p
            .Add(x => x.DefaultValue, 0)
            .AddChildContent(Part<TabsList>(l => l
                .AddUnmatched("onkeydown", EventCallback.Factory.Create<KeyboardEventArgs>(this, e =>
                {
                    keys.Add(e.Key);
                    e.PreventPartHandler();
                }))
                .AddChildContent(Fragments(Tab(0), Tab(1))))));

        Assert.Contains("\"reportHandledKeys\":true", test.LastCompositeOptions, StringComparison.Ordinal);
        string tabId = Tabs(cut)[0].Id!;
        string verdict = await cut.InvokeAsync(() => test.CompositeTwin().Decide(
            Blazix.Primitives.Interop.CompositeTwin.NavigatePoint,
            $$$"""{"kind":"KeyboardEvent","type":"keydown","key":"ArrowRight","code":"ArrowRight","target":{"id":"{{{tabId}}}","tagName":"BUTTON"}}"""));

        Assert.Equal("stop", verdict);
        Assert.Equal(["ArrowRight"], keys);
    }

    // TAB-DOM-11's attribute clause (TabsList.test.tsx "skips a natively disabled tab in a single keypress" is L4 for the
    // arrow-key clause): a substituted button that is natively disabled keeps `disabled` while the Tab is enabled.
    [Fact(DisplayName = "port: a substituted natively disabled button keeps its disabled attribute while the tab is enabled")]
    [Trait("Requirement", "TAB-DOM-11")]
    public void Port_a_substituted_natively_disabled_button_keeps_its_disabled_attribute()
    {
        RenderFragment disabledButton = builder =>
        {
            builder.OpenElement(0, "button");
            builder.AddAttribute(1, "type", "button");
            builder.AddAttribute(2, "disabled", true);
            builder.CloseElement();
        };

        IRenderedComponent<TabsRoot<int>> cut = Context.Render<TabsRoot<int>>(p => p
            .Add(x => x.Value, 0)
            .AddChildContent(List(Tab(0), Tab(1, t => t.AddUnmatched("Render", disabledButton)), Tab(2))));

        IReadOnlyList<IElement> tabs = Tabs(cut);
        Assert.True(tabs[1].HasAttribute("disabled"));
        Assert.Equal("false", tabs[1].GetAttribute("aria-disabled"));
        Assert.False(tabs[0].HasAttribute("disabled"));
    }
}

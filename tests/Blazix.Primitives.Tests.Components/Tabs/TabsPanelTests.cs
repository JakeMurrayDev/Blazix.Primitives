using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components;
using static Blazix.Primitives.Tests.Components.TabsTestContext;

// The namespace is the project's root: a namespace named after the family would hide the parts' types.
namespace Blazix.Primitives.Tests.Components;

// TabsPanel.test.tsx, the [L2] rows (issue 104), and the Panel's C# half of the transition machine over TransitionsTwin
// (TAB §9; the transitions themselves are L4). A panel's exit reaches C# as the `transitions` module's report
// (CompleteExitAsync). Port titles are marked as such.
/// <summary>The tab panel: its element, its registration and its transition handover.</summary>
public sealed class TabsPanelTests : IDisposable
{
    private readonly TabsTestContext test = new();

    private BunitContext Context => test.Context;

    /// <inheritdoc />
    public void Dispose() => test.Dispose();

    [Fact(DisplayName = "throws a descriptive error when rendered outside <Tabs.Root>")]
    [Trait("Requirement", "TAB-EDGE-01")]
    [Trait("Requirement", "TAB-STR-05")]
    public void Throws_a_descriptive_error_when_rendered_outside_Tabs_Root()
    {
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => Context.Render(KeptPanel("1")));

        Assert.Equal("Blazix: The `TabsRoot` context is missing. Tabs parts must be placed within `<TabsRoot>`.", error.Message);
    }

    [Fact(DisplayName = "port: a list or an indicator outside a root throws the root's error")]
    [Trait("Requirement", "TAB-EDGE-01")]
    public void Port_a_list_or_an_indicator_outside_a_root_throws_the_roots_error()
    {
        const string message = "Blazix: The `TabsRoot` context is missing. Tabs parts must be placed within `<TabsRoot>`.";
        Assert.Equal(message, Assert.Throws<InvalidOperationException>(() => Context.Render<TabsList>()).Message);
        Assert.Equal(message, Assert.Throws<InvalidOperationException>(() => Context.Render<TabsIndicator>()).Message);
    }

    [Fact(DisplayName = "keeps the surviving registration when a shadowed panel unmounts")]
    [Trait("Requirement", "TAB-STA-21")]
    [Trait("Requirement", "TAB-STR-05")]
    [Trait("Requirement", "TAB-ARIA-05")]
    public void Keeps_the_surviving_registration_when_a_shadowed_panel_unmounts()
    {
        RenderFragment Build(bool shadowedMounted) => Fragments(
            List(Tab("a"), Tab("b")),
            shadowedMounted ? Panel("b", p => p.Add(x => x.KeepMounted, true).AddUnmatched("data-testid", "shadowed")) : _ => { },
            Panel("b", p => p.Add(x => x.KeepMounted, true).AddUnmatched("data-testid", "owner")));

        IRenderedComponent<TabsRoot<string>> cut = Context.Render<TabsRoot<string>>(p => p
            .Add(x => x.Value, "a")
            .AddChildContent(Build(shadowedMounted: true)));

        // The last panel to register owns the value.
        string ownerId = cut.Find("[data-testid=owner]").Id!;
        Assert.Equal(ownerId, Tabs(cut)[1].GetAttribute("aria-controls"));

        cut.Render(p => p.Add(x => x.Value, "a").AddChildContent(Build(shadowedMounted: false)));

        Assert.Empty(cut.FindAll("[data-testid=shadowed]"));
        Assert.Equal(ownerId, Tabs(cut)[1].GetAttribute("aria-controls"));
    }

    [Fact(DisplayName = "port: an open panel is a tab stop and a closed kept panel is hidden, inert and out of the tab order")]
    [Trait("Requirement", "TAB-DOM-14")]
    [Trait("Requirement", "TAB-DOM-15")]
    [Trait("Requirement", "TAB-DOM-16")]
    [Trait("Requirement", "TAB-FOC-09")]
    [Trait("Requirement", "TAB-ARIA-07")]
    public void Port_an_open_panel_is_a_tab_stop_and_a_closed_kept_panel_is_hidden_and_inert()
    {
        IRenderedComponent<TabsRoot<int>> cut = Context.Render<TabsRoot<int>>(p => p
            .Add(x => x.Value, 0)
            .AddChildContent(Fragments(List(Tab(0), Tab(1)), KeptPanel(0), KeptPanel(1))));

        IReadOnlyList<IElement> panels = Panels(cut);
        Assert.Equal("0", panels[0].GetAttribute("tabindex"));
        Assert.False(panels[0].HasAttribute("inert"));
        Assert.False(panels[0].HasAttribute("hidden"));
        Assert.False(panels[0].HasAttribute(TabsPanelDataAttributes.Hidden));
        Assert.Equal("-1", panels[1].GetAttribute("tabindex"));
        Assert.True(panels[1].HasAttribute("inert"));
        Assert.True(panels[1].HasAttribute("hidden"));
        Assert.Equal(string.Empty, panels[1].GetAttribute(TabsPanelDataAttributes.Hidden));
        Assert.All(panels, panel =>
        {
            Assert.Equal("DIV", panel.TagName);
            Assert.Equal("horizontal", panel.GetAttribute(TabsPanelDataAttributes.Orientation));
            Assert.Equal("none", panel.GetAttribute(TabsPanelDataAttributes.ActivationDirection));
        });
    }

    [Fact(DisplayName = "port: a closing panel is inert at once and stays mounted until its exit ends")]
    [Trait("Requirement", "TAB-ANI-03")]
    [Trait("Requirement", "TAB-DOM-12")]
    [Trait("Requirement", "TAB-DOM-15")]
    [Trait("Requirement", "TAB-STA-22")]
    public async Task Port_a_closing_panel_is_inert_at_once_and_stays_mounted_until_its_exit_ends()
    {
        IRenderedComponent<TabsRoot<int>> cut = Context.Render<TabsRoot<int>>(p => p
            .Add(x => x.DefaultValue, 0)
            .AddChildContent(Fragments(List(Tab(0), Tab(1)), Panel(0), Panel(1))));

        await test.SelectAsync(cut, Tabs(cut)[1].Id!, "right");

        IElement closing = cut.Find("[data-testid=panel-0]");
        Assert.True(closing.HasAttribute("inert"));
        Assert.Equal("-1", closing.GetAttribute("tabindex"));
        Assert.False(closing.HasAttribute("hidden"));

        await test.CompleteExitAsync(cut, panel: 0);

        Assert.Empty(cut.FindAll("[data-testid=panel-0]"));
        Assert.Single(Panels(cut));
    }

    [Fact(DisplayName = "port: a panel created open renders data-starting-style and attaches its transitions twin in the starting status")]
    [Trait("Requirement", "TAB-ANI-01")]
    [Trait("Requirement", "TAB-ANI-02")]
    [Trait("Requirement", "TAB-STA-22")]
    public async Task Port_a_panel_created_open_renders_data_starting_style()
    {
        IRenderedComponent<TabsRoot<string>> cut = Context.Render<TabsRoot<string>>(p => p
            .Add(x => x.DefaultValue, "one")
            .AddChildContent(Fragments(List(Tab("one"), Tab("two")), Panel("one"), Panel("two"))));

        // TAB-ANI-02: open on the first render, no starting phase.
        Assert.False(cut.Find("[data-testid=panel-one]").HasAttribute(TabsPanelDataAttributes.StartingStyle));
        Assert.Empty(cut.FindAll("[data-testid=panel-two]"));

        await test.SelectAsync(cut, Tabs(cut)[1].Id!, "right");

        Assert.Equal(string.Empty, cut.Find("[data-testid=panel-two]").GetAttribute(TabsPanelDataAttributes.StartingStyle));
        Assert.Equal(
            """{"open":true,"mounted":true,"status":"starting","enableIdleState":false,"deferEndingState":false,"keepMounted":false,"hiddenUntilFound":false,"hiddenWhenUnmounted":true,"suppressEndingStyle":false,"reportOpenComplete":false,"revealOnBeforeMatch":false,"unmountRequests":0,"externalExit":false,"openSelector":":not([inert])"}""",
            test.TransitionsAttaches[^1]);
    }

    [Fact(DisplayName = "port: the mirrored transition status reaches the panel's state")]
    [Trait("Requirement", "TAB-STA-20")]
    [Trait("Requirement", "TAB-DOM-18")]
    public async Task Port_the_mirrored_transition_status_reaches_the_panels_state()
    {
        List<TabsPanelState> states = [];
        IRenderedComponent<TabsRoot<string>> cut = Context.Render<TabsRoot<string>>(p => p
            .Add(x => x.DefaultValue, "one")
            .AddChildContent(Fragments(
                List(Tab("one"), Tab("two")),
                Panel("one", p => p.Add(x => x.ClassValue, (Func<TabsPanelState, string?>)(state =>
                {
                    states.Add(state);
                    return null;
                }))))));

        Assert.Null(states[^1].TransitionStatus);
        Assert.False(states[^1].Hidden);

        await test.SelectAsync(cut, Tabs(cut)[1].Id!, "right");
        await test.ReportTransitionAsync(cut, "ending", mounted: true, panel: 0);

        Assert.Equal(TransitionStatus.Ending, states[^1].TransitionStatus);
        Assert.Empty(cut.FindAll("[data-testid=panel-one][data-ending-style]"));
    }

    // TAB-EDGE-12 (Test: none) with §16 Q1 as 1.9.0 (design point 9 of the Tabs track, pending the user).
    [Fact(DisplayName = "port: a consumer id lands on the panel while aria-controls keeps the generated id")]
    [Trait("Requirement", "TAB-EDGE-12")]
    [Trait("Requirement", "TAB-DOM-13")]
    public void Port_a_consumer_id_lands_on_the_panel_while_aria_controls_keeps_the_generated_id()
    {
        IRenderedComponent<TabsRoot<int>> cut = Context.Render<TabsRoot<int>>(p => p
            .Add(x => x.Value, 0)
            .AddChildContent(Fragments(List(Tab(0)), Panel(0, p => p.AddUnmatched("id", "consumer-panel")))));

        Assert.Equal("consumer-panel", Assert.Single(Panels(cut)).Id);
        string? controls = Tabs(cut)[0].GetAttribute("aria-controls");
        Assert.StartsWith("base-ui-", controls, StringComparison.Ordinal);
        Assert.NotEqual("consumer-panel", controls);
    }
}

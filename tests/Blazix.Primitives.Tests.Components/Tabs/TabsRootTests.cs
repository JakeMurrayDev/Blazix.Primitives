using System.Text.RegularExpressions;
using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components;
using static Blazix.Primitives.Tests.Components.TabsTestContext;
using LibraryOrientation = Blazix.Primitives.Orientation;

// The namespace is the project's root: a namespace named after the family would hide the parts' types.
namespace Blazix.Primitives.Tests.Components;

// TabsRoot.test.tsx, the [L2] rows that need no composite extension of track 1 (issue 104 deliverable 9): the element,
// the value, the panel registry and aria-controls, the orientation. A click reaches C# as the `tabs` module's report
// (SelectAsync, Server) or its `select` decision (DecideSelectAsync, WebAssembly); the browser halves are
// tests/browser/tabs.spec.ts. Port titles are marked as such.
/// <summary>The tabs root: its element, its value and the wiring between its tabs and panels.</summary>
public sealed class TabsRootTests : IDisposable
{
    private readonly TabsTestContext test = new();

    private BunitContext Context => test.Context;

    /// <inheritdoc />
    public void Dispose() => test.Dispose();

    [Fact(DisplayName = "should accept a null child")]
    [Trait("Requirement", "TAB-STR-01")]
    public void Should_accept_a_null_child()
    {
        RenderFragment nothing = _ => { };
        IRenderedComponent<TabsRoot<int>> cut = Context.Render<TabsRoot<int>>(p => p
            .Add(x => x.Value, 0)
            .AddChildContent(Fragments(nothing, List(Tab(1)))));

        Assert.Single(Tabs(cut));
    }

    [Fact(DisplayName = "should support empty children")]
    [Trait("Requirement", "TAB-STR-01")]
    public void Should_support_empty_children()
    {
        IRenderedComponent<TabsRoot<int>> cut = Context.Render<TabsRoot<int>>(p => p.Add(x => x.Value, 1));

        IElement root = cut.Nodes.OfType<IElement>().Single();
        Assert.Equal("DIV", root.TagName);
        Assert.Empty(root.ChildNodes);
    }

    [Fact(DisplayName = "port: the root renders its orientation and the initial activation direction, and no id or handler")]
    [Trait("Requirement", "TAB-STR-01")]
    [Trait("Requirement", "TAB-DOM-01")]
    [Trait("Requirement", "TAB-DOM-02")]
    public void Port_the_root_renders_its_orientation_and_the_initial_activation_direction()
    {
        IRenderedComponent<TabsRoot<int>> cut = Context.Render<TabsRoot<int>>(p => p
            .Add(x => x.Value, 0)
            .Add(x => x.Orientation, LibraryOrientation.Vertical));

        IElement root = cut.Nodes.OfType<IElement>().Single();
        Assert.Equal("vertical", root.GetAttribute(TabsRootDataAttributes.Orientation));
        Assert.Equal("none", root.GetAttribute(TabsRootDataAttributes.ActivationDirection));
        Assert.False(root.HasAttribute("id"));
        Assert.Null(root.GetAttribute("data-tabactivationdirection"));
    }

    [Fact(DisplayName = "sets the aria-controls attribute on tabs to the corresponding tab panel id")]
    [Trait("Requirement", "TAB-ARIA-05")]
    [Trait("Requirement", "TAB-DOM-13")]
    [Trait("Requirement", "TAB-STA-21")]
    public void Sets_the_aria_controls_attribute_on_tabs_to_the_corresponding_tab_panel_id()
    {
        IRenderedComponent<TabsRoot<string>> cut = Context.Render<TabsRoot<string>>(p => p
            .Add(x => x.DefaultValue, "tab-0")
            .AddChildContent(Fragments(
                List(
                    Tab("tab-0"),
                    Tab("tab-1", t => t.AddUnmatched("id", "explicit-tab-id-1")),
                    Tab("tab-2"),
                    Tab("tab-3", t => t.AddUnmatched("id", "explicit-tab-id-3"))),
                KeptPanel("tab-1"),
                KeptPanel("tab-0"),
                KeptPanel("tab-2"),
                KeptPanel("tab-3"))));

        IReadOnlyList<IElement> tabs = Tabs(cut);
        IReadOnlyList<IElement> panels = Panels(cut);

        Assert.Equal(panels[1].Id, tabs[0].GetAttribute("aria-controls"));
        Assert.Equal(panels[0].Id, tabs[1].GetAttribute("aria-controls"));
        Assert.Equal(panels[2].Id, tabs[2].GetAttribute("aria-controls"));
        Assert.Equal(panels[3].Id, tabs[3].GetAttribute("aria-controls"));
        Assert.All(panels, panel => Assert.StartsWith("base-ui-", panel.Id, StringComparison.Ordinal));
    }

    [Fact(DisplayName = "syncs aria-controls to the mounted tab panel when keepMounted is false")]
    [Trait("Requirement", "TAB-ARIA-05")]
    [Trait("Requirement", "TAB-API-13")]
    [Trait("Requirement", "TAB-DOM-12")]
    [Trait("Requirement", "TAB-STA-21")]
    [Trait("Requirement", "TAB-ANI-05")]
    public async Task Syncs_aria_controls_to_the_mounted_tab_panel_when_keepMounted_is_false()
    {
        IRenderedComponent<TabsRoot<string>> cut = Context.Render<TabsRoot<string>>(p => p
            .Add(x => x.DefaultValue, "tab-0")
            .AddChildContent(Fragments(
                List(Tab("tab-0"), Tab("tab-1")),
                Panel("tab-0", p => p.AddChildContent("Panel 0")),
                Panel("tab-1", p => p.AddChildContent("Panel 1")))));

        IReadOnlyList<IElement> tabs = Tabs(cut);
        IElement firstPanel = Assert.Single(Panels(cut));
        Assert.Equal(firstPanel.Id, tabs[0].GetAttribute("aria-controls"));
        Assert.False(tabs[1].HasAttribute("aria-controls"));

        await test.SelectAsync(cut, tabs[1].Id!, "right");

        // The closing panel keeps its registration through its exit (TAB-ANI-05); its end removes it.
        IElement secondPanel = cut.Find("[data-testid=panel-tab-1]");
        Assert.Equal(secondPanel.Id, Tabs(cut)[1].GetAttribute("aria-controls"));
        Assert.Equal(firstPanel.Id, Tabs(cut)[0].GetAttribute("aria-controls"));

        await test.CompleteExitAsync(cut, panel: 0);

        IElement remaining = Assert.Single(Panels(cut));
        Assert.Equal("Panel 1", remaining.TextContent);
        Assert.False(Tabs(cut)[0].HasAttribute("aria-controls"));
        Assert.Equal(remaining.Id, Tabs(cut)[1].GetAttribute("aria-controls"));
    }

    // TabsRoot.test.tsx "cleans and replaces panel registrations in Strict Mode", restated under ADR 06 Amendments
    // 2026-10-18 rule 2: there is no Strict Mode, so the sequence a keyed panel goes through (replaced by another value,
    // removed, remounted) is the subject; the twin's idempotent attach is TabsTwinContractTests'.
    [Fact(DisplayName = "port: replaces and cleans panel registrations when a keyed panel is replaced, removed and remounted")]
    [Trait("Requirement", "TAB-STA-21")]
    [Trait("Requirement", "TAB-ARIA-05")]
    [Trait("Requirement", "TAB-ANI-05")]
    public void Port_replaces_and_cleans_panel_registrations()
    {
        RenderFragment Build(string key, string value, bool mounted) => Fragments(
            List(Tab("a"), Tab("b")),
            mounted
                ? builder =>
                {
                    builder.OpenComponent<TabsPanel<string>>(0);
                    builder.SetKey(key);
                    builder.AddComponentParameter(1, nameof(TabsPanel<string>.Value), value);
                    builder.AddComponentParameter(2, nameof(TabsPanel<string>.KeepMounted), true);
                    builder.CloseComponent();
                }
                : _ => { });

        IRenderedComponent<TabsRoot<string>> cut = Context.Render<TabsRoot<string>>(p => p
            .Add(x => x.Value, "a")
            .AddChildContent(Build("panel-a", "a", mounted: true)));

        Assert.Equal(Panels(cut)[0].Id, Tabs(cut)[0].GetAttribute("aria-controls"));
        Assert.False(Tabs(cut)[1].HasAttribute("aria-controls"));

        cut.Render(p => p.Add(x => x.Value, "a").AddChildContent(Build("panel-b", "b", mounted: true)));
        Assert.False(Tabs(cut)[0].HasAttribute("aria-controls"));
        Assert.Equal(Panels(cut)[0].Id, Tabs(cut)[1].GetAttribute("aria-controls"));

        cut.Render(p => p.Add(x => x.Value, "a").AddChildContent(Build("panel-b", "b", mounted: false)));
        Assert.False(Tabs(cut)[0].HasAttribute("aria-controls"));
        Assert.False(Tabs(cut)[1].HasAttribute("aria-controls"));

        cut.Render(p => p.Add(x => x.Value, "a").AddChildContent(Build("panel-c", "b", mounted: true)));
        Assert.False(Tabs(cut)[0].HasAttribute("aria-controls"));
        Assert.Equal(Panels(cut)[0].Id, Tabs(cut)[1].GetAttribute("aria-controls"));
    }

    [Fact(DisplayName = "should pass selected prop to children")]
    [Trait("Requirement", "TAB-API-01")]
    [Trait("Requirement", "TAB-STA-01")]
    [Trait("Requirement", "TAB-ARIA-04")]
    public void Should_pass_selected_prop_to_children()
    {
        IRenderedComponent<TabsRoot<int>> cut = Context.Render<TabsRoot<int>>(p => p
            .Add(x => x.Value, 1)
            .AddChildContent(List(Tab(0), Tab(1))));

        IReadOnlyList<IElement> tabs = Tabs(cut);
        Assert.Equal("false", tabs[0].GetAttribute("aria-selected"));
        Assert.Equal("true", tabs[1].GetAttribute("aria-selected"));
        Assert.False(tabs[0].HasAttribute(TabsTabDataAttributes.Active));
        Assert.True(tabs[1].HasAttribute(TabsTabDataAttributes.Active));
    }

    // TAB-API-07/12, TAB-EDGE-07 with ADR 08 Amendments 2026-10-26: Base UI's `[0, '1', { value: 2 }, () => 3, Symbol('4'),
    // /5/]` as a TabsRoot<object>, each value a distinct instance compared by reference. The aria-labelledby clause waits
    // for track 1's ordered-ids report (E2), listed open by ID in the pull request.
    [Fact(DisplayName = "should support values of different types")]
    [Trait("Requirement", "TAB-API-07")]
    [Trait("Requirement", "TAB-API-12")]
    [Trait("Requirement", "TAB-EDGE-07")]
    public async Task Should_support_values_of_different_types()
    {
        Func<int> function = () => 3;
        object[] values = [0, "1", new { value = 2 }, function, new object(), new Regex("5")];

        IRenderedComponent<TabsRoot<object>> cut = Context.Render<TabsRoot<object>>(p => p.AddChildContent(Fragments(
            List([.. values.Select(value => Part<TabsTab<object>>(t => t.Add(x => x.Value, value)))]),
            Fragments([.. values.Select(value => Part<TabsPanel<object>>(t => t.Add(x => x.Value, value).Add(x => x.KeepMounted, true)))]))));

        for (int index = 0; index < values.Length; index++)
        {
            await test.SelectAsync(cut, Tabs(cut)[index].Id!);
            await test.CompleteExitsAsync(cut);

            Assert.Equal("true", Tabs(cut)[index].GetAttribute("aria-selected"));
            Assert.False(Panels(cut)[index].HasAttribute("hidden"));
        }
    }

    [Fact(DisplayName = "should support a function as the default value")]
    [Trait("Requirement", "TAB-STA-01")]
    [Trait("Requirement", "TAB-API-02")]
    [Trait("Requirement", "TAB-EDGE-07")]
    public void Should_support_a_function_as_the_default_value()
    {
        Func<int> functionValue = () => 1;
        Func<int> other = () => 0;

        IRenderedComponent<TabsRoot<Func<int>>> cut = Context.Render<TabsRoot<Func<int>>>(p => p
            .Add(x => x.DefaultValue, functionValue)
            .AddChildContent(List(
                Part<TabsTab<Func<int>>>(t => t.Add(x => x.Value, other)),
                Part<TabsTab<Func<int>>>(t => t.Add(x => x.Value, functionValue)))));

        Assert.Equal("false", Tabs(cut)[0].GetAttribute("aria-selected"));
        Assert.Equal("true", Tabs(cut)[1].GetAttribute("aria-selected"));
    }

    [Fact(DisplayName = "should still honor explicit value prop even if it points to a disabled tab")]
    [Trait("Requirement", "TAB-API-01")]
    [Trait("Requirement", "TAB-STA-13")]
    [Trait("Requirement", "TAB-EDGE-13")]
    public void Should_still_honor_explicit_value_prop_even_if_it_points_to_a_disabled_tab()
    {
        IRenderedComponent<TabsRoot<int>> cut = Context.Render<TabsRoot<int>>(p => p
            .Add(x => x.Value, 0)
            .AddChildContent(Fragments(
                List(Tab(0, t => t.Add(x => x.Disabled, true)), Tab(1), Tab(2)),
                Panel(0),
                Panel(1),
                Panel(2))));

        IReadOnlyList<IElement> tabs = Tabs(cut);
        Assert.Equal("true", tabs[0].GetAttribute("aria-selected"));
        Assert.Equal("false", tabs[1].GetAttribute("aria-selected"));
        Assert.Equal("false", tabs[2].GetAttribute("aria-selected"));
        Assert.Equal("panel-0", Assert.Single(Panels(cut)).GetAttribute("data-testid"));
    }

    [Fact(DisplayName = "does not add aria-orientation by default")]
    [Trait("Requirement", "TAB-ARIA-01")]
    [Trait("Requirement", "TAB-ARIA-02")]
    [Trait("Requirement", "TAB-API-04")]
    public void Does_not_add_aria_orientation_by_default()
    {
        IRenderedComponent<TabsRoot<int>> cut = Context.Render<TabsRoot<int>>(p => p
            .Add(x => x.Value, 0)
            .AddChildContent(Part<TabsList>(l => l.AddChildContent(Part<TabsRoot<int>>()))));

        IElement list = cut.Find("[role=tablist]");
        Assert.False(list.HasAttribute("aria-orientation"));
        Assert.Equal("horizontal", list.GetAttribute(TabsListDataAttributes.Orientation));
    }

    [Fact(DisplayName = "adds the proper aria-orientation when vertical")]
    [Trait("Requirement", "TAB-ARIA-02")]
    [Trait("Requirement", "TAB-API-04")]
    public void Adds_the_proper_aria_orientation_when_vertical()
    {
        IRenderedComponent<TabsRoot<int>> cut = Context.Render<TabsRoot<int>>(p => p
            .Add(x => x.Value, 0)
            .Add(x => x.Orientation, LibraryOrientation.Vertical)
            .AddChildContent(Part<TabsList>(l => l.AddChildContent(Part<TabsRoot<int>>()))));

        Assert.Equal("vertical", cut.Find("[role=tablist]").GetAttribute("aria-orientation"));
        Assert.Contains("\"orientation\":\"vertical\"", test.CompositeAttaches[0], StringComparison.Ordinal);
    }

    [Fact(DisplayName = "selects the clicked tab")]
    [Trait("Requirement", "TAB-DOM-14")]
    [Trait("Requirement", "TAB-API-13")]
    [Trait("Requirement", "TAB-STA-03")]
    public async Task Selects_the_clicked_tab()
    {
        IRenderedComponent<TabsRoot<int>> cut = Context.Render<TabsRoot<int>>(p => p
            .Add(x => x.DefaultValue, 0)
            .AddChildContent(Fragments(
                List(Tab(0), Tab(1), Tab(2)),
                KeptPanel(0),
                KeptPanel(1),
                KeptPanel(2))));

        await test.SelectAsync(cut, Tabs(cut)[1].Id!, "right");
        await test.CompleteExitAsync(cut, panel: 0);

        IReadOnlyList<IElement> panels = Panels(cut);
        Assert.True(panels[0].HasAttribute("hidden"));
        Assert.True(panels[0].HasAttribute(TabsPanelDataAttributes.Hidden));
        Assert.False(panels[1].HasAttribute("hidden"));
        Assert.False(panels[1].HasAttribute(TabsPanelDataAttributes.Hidden));
        Assert.True(panels[2].HasAttribute("hidden"));
    }

    // TAB-STR-06's aria-controls clause; the aria-labelledby clause waits for E2 and the arrow-key clause is L4.
    [Fact(DisplayName = "keeps a nested root independent from the one hosting its panel")]
    [Trait("Requirement", "TAB-STR-06")]
    public async Task Keeps_a_nested_root_independent_from_the_one_hosting_its_panel()
    {
        RenderFragment inner = Part<TabsRoot<string>>(r => r
            .Add(x => x.DefaultValue, "inner-1")
            .AddChildContent(Fragments(
                Part<TabsList>(l => l.AddUnmatched("data-testid", "inner-list").AddChildContent(Fragments(Tab("inner-1"), Tab("inner-2")))),
                Panel("inner-1", p => p.AddChildContent("Inner panel 1")),
                Panel("inner-2", p => p.AddChildContent("Inner panel 2")))));

        IRenderedComponent<TabsRoot<string>> cut = Context.Render<TabsRoot<string>>(p => p
            .Add(x => x.DefaultValue, "outer-1")
            .AddChildContent(Fragments(
                Part<TabsList>(l => l.AddUnmatched("data-testid", "outer-list").AddChildContent(Fragments(Tab("outer-1"), Tab("outer-2")))),
                Panel("outer-1", p => p.AddChildContent(inner)),
                Panel("outer-2", p => p.AddChildContent("Outer panel 2")))));

        IElement outerTab1 = cut.Find("[data-testid=tab-outer-1]");
        IElement innerTab1 = cut.Find("[data-testid=tab-inner-1]");
        Assert.Equal(cut.Find("[data-testid=panel-outer-1]").Id, outerTab1.GetAttribute("aria-controls"));
        Assert.Equal(cut.Find("[data-testid=panel-inner-1]").Id, innerTab1.GetAttribute("aria-controls"));

        // The inner list's activation leaves the outer selection.
        await test.SelectAsync(cut, cut.Find("[data-testid=tab-inner-2]").Id!, "right", list: cut.Find("[data-testid=inner-list]"));
        Assert.Equal("true", cut.Find("[data-testid=tab-inner-2]").GetAttribute("aria-selected"));
        Assert.Equal("true", cut.Find("[data-testid=tab-outer-1]").GetAttribute("aria-selected"));
        Assert.Equal("false", cut.Find("[data-testid=tab-outer-2]").GetAttribute("aria-selected"));

        await test.SelectAsync(cut, cut.Find("[data-testid=tab-outer-2]").Id!, "right", list: cut.Find("[data-testid=outer-list]"));
        await test.CompleteExitsAsync(cut);
        Assert.Empty(cut.FindAll("[data-testid=inner-list]"));
        Assert.False(cut.Find("[data-testid=panel-outer-2]").HasAttribute("hidden"));
    }
}

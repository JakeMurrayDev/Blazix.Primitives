using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components;
using static Blazix.Primitives.Tests.Components.TabsTestContext;

// The namespace is the project's root: a namespace named after the family would hide the parts' types.
namespace Blazix.Primitives.Tests.Components;

// TabsRoot.test.tsx's [L2] rows that read the Tab registry (track 1's E2: the `composite` module reports the List's Tabs
// in document order) and TabsPanel.test.tsx's `data-index` (E1: the Root's registration-only list of Panels). A report
// is driven by hand with the ids the page renders (ReportTabsAsync, ReportPanelsAsync); on Server it arrives after paint
// plus a round trip (ADR 01's "EVD-STA-07, CMP-STA-10, FFI registration" row, tabs.md §9). An automatic change to nothing
// is "nothing selected" (ADR 08 Amendments 2026-10-26): `null` for a nullable TValue, which these titles use where Base UI
// asserts `null`.
/// <summary>The tab registry of a tabs root: the automatic selection, its notification, and the lookups it serves.</summary>
public sealed class TabsAutomaticSelectionTests : IDisposable
{
    private readonly TabsTestContext test = new();
    private readonly List<(object? Value, TabsChangeEventDetails Details)> changes = [];

    private BunitContext Context => test.Context;

    /// <inheritdoc />
    public void Dispose() => test.Dispose();

    [Fact(DisplayName = "sets the aria-labelledby attribute on tab panels to the corresponding tab id")]
    [Trait("Requirement", "TAB-ARIA-07")]
    [Trait("Requirement", "TAB-ARIA-08")]
    [Trait("Requirement", "TAB-API-10")]
    public async Task Sets_the_aria_labelledby_attribute_on_tab_panels_to_the_corresponding_tab_id()
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

        await test.ReportTabsAsync(cut);

        IReadOnlyList<IElement> tabs = Tabs(cut);
        IReadOnlyList<IElement> panels = Panels(cut);
        Assert.Equal("explicit-tab-id-1", tabs[1].Id);
        Assert.Equal(tabs[1].Id, panels[0].GetAttribute("aria-labelledby"));
        Assert.Equal(tabs[0].Id, panels[1].GetAttribute("aria-labelledby"));
        Assert.Equal(tabs[2].Id, panels[2].GetAttribute("aria-labelledby"));
        Assert.Equal(tabs[3].Id, panels[3].GetAttribute("aria-labelledby"));
    }

    [Fact(DisplayName = "sets aria-controls on the first tab when no value is provided")]
    [Trait("Requirement", "TAB-API-02")]
    [Trait("Requirement", "TAB-DOM-04")]
    [Trait("Requirement", "TAB-ARIA-05")]
    [Trait("Requirement", "TAB-ARIA-08")]
    public async Task Sets_aria_controls_on_the_first_tab_when_no_value_is_provided()
    {
        IRenderedComponent<TabsRoot<int>> cut = Context.Render<TabsRoot<int>>(p => p
            .AddChildContent(Fragments(List(Tab(0), Tab(1)), KeptPanel(0), KeptPanel(1))));

        await test.ReportTabsAsync(cut);

        IReadOnlyList<IElement> tabs = Tabs(cut);
        IReadOnlyList<IElement> panels = Panels(cut);
        Assert.Equal(panels[0].Id, tabs[0].GetAttribute("aria-controls"));
        Assert.Equal(panels[1].Id, tabs[1].GetAttribute("aria-controls"));
        Assert.Equal(tabs[0].Id, panels[0].GetAttribute("aria-labelledby"));
        Assert.Equal(tabs[1].Id, panels[1].GetAttribute("aria-labelledby"));
    }

    [Fact(DisplayName = "should select the second tab when the first one is disabled")]
    [Trait("Requirement", "TAB-API-03")]
    [Trait("Requirement", "TAB-STA-06")]
    [Trait("Requirement", "TAB-DOM-14")]
    public async Task Should_select_the_second_tab_when_the_first_one_is_disabled()
    {
        IRenderedComponent<TabsRoot<int>> cut = Context.Render<TabsRoot<int>>(p => p
            .AddChildContent(Fragments(
                List(Tab(0, t => t.Add(x => x.Disabled, true).AddChildContent("Disabled tab")), Tab(1, t => t.AddChildContent("Enabled tab"))),
                Panel(0, p => p.Add(x => x.KeepMounted, true).AddChildContent("Disabled panel")),
                Panel(1, p => p.Add(x => x.KeepMounted, true).AddChildContent("Enabled panel")))));

        await test.ReportTabsAsync(cut);
        await test.CompleteExitsAsync(cut);

        IReadOnlyList<IElement> tabs = Tabs(cut);
        IReadOnlyList<IElement> panels = Panels(cut);
        Assert.Equal("false", tabs[0].GetAttribute("aria-selected"));
        Assert.Equal("true", tabs[1].GetAttribute("aria-selected"));
        Assert.True(panels[0].HasAttribute("hidden"));
        Assert.False(panels[1].HasAttribute("hidden"));
        Assert.Equal("Enabled panel", panels[1].TextContent);
    }

    [Fact(DisplayName = "should select the third tab when first two tabs are disabled")]
    [Trait("Requirement", "TAB-STA-05")]
    [Trait("Requirement", "TAB-STA-06")]
    public async Task Should_select_the_third_tab_when_first_two_tabs_are_disabled()
    {
        IRenderedComponent<TabsRoot<int>> cut = Context.Render<TabsRoot<int>>(p => p
            .AddChildContent(Fragments(
                List(Tab(0, Disabled), Tab(1, Disabled), Tab(2), Tab(3)),
                Panel(0),
                Panel(1),
                Panel(2),
                Panel(3))));

        await test.ReportTabsAsync(cut);

        Assert.Equal(["false", "false", "true", "false"], Selected(cut));
    }

    [Fact(DisplayName = "should still honor explicit defaultValue even if it points to a disabled tab")]
    [Trait("Requirement", "TAB-STA-07")]
    [Trait("Requirement", "TAB-EDGE-13")]
    public async Task Should_still_honor_explicit_defaultValue_even_if_it_points_to_a_disabled_tab()
    {
        IRenderedComponent<TabsRoot<int>> cut = Context.Render<TabsRoot<int>>(p => p
            .Add(x => x.DefaultValue, 0)
            .Add(x => x.OnValueChange, Record<int>())
            .AddChildContent(Fragments(List(Tab(0, Disabled), Tab(1), Tab(2)), Panel(0), Panel(1), Panel(2))));

        await test.ReportTabsAsync(cut);

        Assert.Equal(["true", "false", "false"], Selected(cut));
        Assert.Empty(changes);
    }

    [Fact(DisplayName = "does not select any tab when all tabs are disabled")]
    [Trait("Requirement", "TAB-STA-02")]
    [Trait("Requirement", "TAB-STA-06")]
    [Trait("Requirement", "TAB-ARIA-04")]
    [Trait("Requirement", "TAB-DOM-14")]
    public async Task Does_not_select_any_tab_when_all_tabs_are_disabled()
    {
        IRenderedComponent<TabsRoot<int>> cut = Context.Render<TabsRoot<int>>(p => p
            .AddChildContent(Fragments(
                List(Tab(0, Disabled), Tab(1, Disabled), Tab(2, Disabled)),
                KeptPanel(0),
                KeptPanel(1),
                KeptPanel(2))));

        await test.ReportTabsAsync(cut);
        await test.CompleteExitsAsync(cut);

        Assert.Equal(["false", "false", "false"], Selected(cut));
        Assert.All(Panels(cut), panel => Assert.True(panel.HasAttribute("hidden")));
    }

    [Fact(DisplayName = "calls onValueChange when auto-selecting the first tab on mount")]
    [Trait("Requirement", "TAB-STA-08")]
    [Trait("Requirement", "TAB-EVT-04")]
    public async Task Calls_onValueChange_when_auto_selecting_the_first_tab_on_mount()
    {
        IRenderedComponent<TabsRoot<int>> cut = Context.Render<TabsRoot<int>>(p => p
            .Add(x => x.OnValueChange, Record<int>())
            .AddChildContent(List(Tab(0), Tab(1), Tab(2))));

        await test.ReportTabsAsync(cut);

        (object? value, TabsChangeEventDetails details) = Assert.Single(changes);
        Assert.Equal(0, value);
        Assert.Equal(EventReason.Initial, details.Reason);
        Assert.Equal(TabsActivationDirection.None, details.ActivationDirection);
        Assert.Equal("true", Tabs(cut)[0].GetAttribute("aria-selected"));
    }

    [Fact(DisplayName = "calls onValueChange with the selected value when the implicit default matches a later tab")]
    [Trait("Requirement", "TAB-STA-08")]
    public async Task Calls_onValueChange_with_the_selected_value_when_the_implicit_default_matches_a_later_tab()
    {
        IRenderedComponent<TabsRoot<int>> cut = Context.Render<TabsRoot<int>>(p => p
            .Add(x => x.OnValueChange, Record<int>())
            .AddChildContent(List(Tab(1), Tab(0), Tab(2))));

        await test.ReportTabsAsync(cut);

        (object? value, TabsChangeEventDetails details) = Assert.Single(changes);
        Assert.Equal(0, value);
        Assert.Equal(EventReason.Initial, details.Reason);
        Assert.Equal(["false", "true", "false"], Selected(cut));
    }

    [Fact(DisplayName = "calls onValueChange when the implicit first tab is disabled")]
    [Trait("Requirement", "TAB-STA-08")]
    [Trait("Requirement", "TAB-STA-09")]
    public async Task Calls_onValueChange_when_the_implicit_first_tab_is_disabled()
    {
        IRenderedComponent<TabsRoot<int>> cut = Context.Render<TabsRoot<int>>(p => p
            .Add(x => x.OnValueChange, Record<int>())
            .AddChildContent(List(Tab(0, Disabled), Tab(1), Tab(2))));

        await test.ReportTabsAsync(cut);

        (object? value, TabsChangeEventDetails details) = Assert.Single(changes);
        Assert.Equal(1, value);
        Assert.Equal(EventReason.Initial, details.Reason);
        Assert.Equal(TabsActivationDirection.None, details.ActivationDirection);
        Assert.Equal("true", Tabs(cut)[1].GetAttribute("aria-selected"));
    }

    [Fact(DisplayName = "does not cancel automatic value changes")]
    [Trait("Requirement", "TAB-EVT-01")]
    [Trait("Requirement", "TAB-EVT-04")]
    public async Task Does_not_cancel_automatic_value_changes()
    {
        EventCallback<ValueChangeEventArgs<int>> cancel = EventCallback.Factory.Create<ValueChangeEventArgs<int>>(this, args =>
        {
            changes.Add((args.Value, (TabsChangeEventDetails)args.Details));
            args.Details.Cancel();
        });
        IRenderedComponent<TabsRoot<int>> cut = Context.Render<TabsRoot<int>>(p => p
            .Add(x => x.OnValueChange, cancel)
            .AddChildContent(List(Tab(0, Disabled), Tab(1))));

        await test.ReportTabsAsync(cut);

        (object? value, TabsChangeEventDetails details) = Assert.Single(changes);
        Assert.Equal(1, value);
        Assert.Equal(EventReason.Initial, details.Reason);
        Assert.Equal(EventKind.Event, details.Event.Kind);
        Assert.Equal("base-ui", details.Event.Type);
        Assert.Null(details.Trigger);
        Assert.Equal(TabsActivationDirection.None, details.ActivationDirection);
        Assert.Equal(["false", "true"], Selected(cut));
    }

    [Fact(DisplayName = "calls onValueChange with null when all tabs are initially disabled")]
    [Trait("Requirement", "TAB-STA-06")]
    [Trait("Requirement", "TAB-STA-08")]
    [Trait("Requirement", "TAB-EVT-04")]
    public async Task Calls_onValueChange_with_null_when_all_tabs_are_initially_disabled()
    {
        IRenderedComponent<TabsRoot<int?>> cut = Context.Render<TabsRoot<int?>>(p => p
            .Add(x => x.OnValueChange, Record<int?>())
            .AddChildContent(List(Tab<int?>(0, Disabled), Tab<int?>(1, Disabled))));

        await test.ReportTabsAsync(cut);

        (object? value, TabsChangeEventDetails details) = Assert.Single(changes);
        Assert.Null(value);
        Assert.Equal(EventReason.Initial, details.Reason);
        Assert.Equal(TabsActivationDirection.None, details.ActivationDirection);
        Assert.Equal(["false", "false"], Selected(cut));
    }

    [Fact(DisplayName = "port: a root of a non-nullable value reports its default and selects nothing when every tab is disabled")]
    [Trait("Requirement", "TAB-STA-06")]
    [Trait("Requirement", "TAB-STA-08")]
    public async Task Port_a_root_of_a_non_nullable_value_reports_its_default_and_selects_nothing()
    {
        IRenderedComponent<TabsRoot<int>> cut = Context.Render<TabsRoot<int>>(p => p
            .Add(x => x.OnValueChange, Record<int>())
            .AddChildContent(Fragments(List(Tab(0, Disabled), Tab(1, Disabled)), KeptPanel(0))));

        await test.ReportTabsAsync(cut);
        await test.CompleteExitsAsync(cut);

        // ADR 08 Amendments 2026-10-26: default(int) is 0, a Tab's value here, and still nothing is selected.
        (object? value, TabsChangeEventDetails details) = Assert.Single(changes);
        Assert.Equal(0, value);
        Assert.Equal(EventReason.Initial, details.Reason);
        Assert.Equal(["false", "false"], Selected(cut));
        Assert.True(Panels(cut)[0].HasAttribute("hidden"));
    }

    [Fact(DisplayName = "does not emit missing when an enabled tab appears after all tabs were disabled")]
    [Trait("Requirement", "TAB-STA-12")]
    [Trait("Requirement", "TAB-EVT-05")]
    public async Task Does_not_emit_missing_when_an_enabled_tab_appears_after_all_tabs_were_disabled()
    {
        EventCallback<ValueChangeEventArgs<int?>> record = Record<int?>();
        IRenderedComponent<TabsRoot<int?>> cut = Context.Render<TabsRoot<int?>>(p => p
            .Add(x => x.OnValueChange, record)
            .AddChildContent(List(Tab<int?>(0, Disabled), Tab<int?>(1, Disabled))));
        await test.ReportTabsAsync(cut);
        Assert.Null(Assert.Single(changes).Value);

        cut.Render(p => p.Add(x => x.OnValueChange, record).AddChildContent(List(Tab<int?>(0, Disabled), Tab<int?>(1))));

        Assert.Single(changes);
        Assert.Equal(["false", "false"], Selected(cut));
    }

    [Fact(DisplayName = "does not call onValueChange on initial render when defaultValue is null")]
    [Trait("Requirement", "TAB-API-02")]
    [Trait("Requirement", "TAB-STA-02")]
    [Trait("Requirement", "TAB-STA-08")]
    public async Task Does_not_call_onValueChange_on_initial_render_when_defaultValue_is_null()
    {
        IRenderedComponent<TabsRoot<int?>> cut = Context.Render<TabsRoot<int?>>(p => p
            .Add(x => x.DefaultValue, null)
            .Add(x => x.OnValueChange, Record<int?>())
            .AddChildContent(List(Tab<int?>(0), Tab<int?>(1))));

        await test.ReportTabsAsync(cut);

        Assert.Empty(changes);
        Assert.Equal(["false", "false"], Selected(cut));
    }

    // In the port the counterpart of `defaultValue={undefined}` is a root given no DefaultValue: a supplied DefaultValue,
    // null included, is explicit (TAB-API-02's note).
    [Fact(DisplayName = "treats defaultValue={undefined} as an implicit default when the first tab is disabled")]
    [Trait("Requirement", "TAB-API-02")]
    [Trait("Requirement", "TAB-STA-08")]
    public async Task Treats_an_omitted_defaultValue_as_an_implicit_default_when_the_first_tab_is_disabled()
    {
        IRenderedComponent<TabsRoot<int>> cut = Context.Render<TabsRoot<int>>(p => p
            .Add(x => x.OnValueChange, Record<int>())
            .AddChildContent(List(Tab(0, Disabled), Tab(1), Tab(2))));

        await test.ReportTabsAsync(cut);

        (object? value, TabsChangeEventDetails details) = Assert.Single(changes);
        Assert.Equal(1, value);
        Assert.Equal(EventReason.Initial, details.Reason);
        Assert.Equal(TabsActivationDirection.None, details.ActivationDirection);
        Assert.Equal(["false", "true", "false"], Selected(cut));
    }

    [Fact(DisplayName = "calls onValueChange when an explicit disabled default becomes disabled again")]
    [Trait("Requirement", "TAB-STA-07")]
    [Trait("Requirement", "TAB-STA-09")]
    public async Task Calls_onValueChange_when_an_explicit_disabled_default_becomes_disabled_again()
    {
        EventCallback<ValueChangeEventArgs<int>> record = Record<int>();
        RenderFragment Content(bool disableFirst) => List(Tab(0, t => t.Add(x => x.Disabled, disableFirst)), Tab(1));
        IRenderedComponent<TabsRoot<int>> cut = Context.Render<TabsRoot<int>>(p => p
            .Add(x => x.DefaultValue, 0)
            .Add(x => x.OnValueChange, record)
            .AddChildContent(Content(true)));
        await test.ReportTabsAsync(cut);
        Assert.Empty(changes);
        Assert.Equal("true", Tabs(cut)[0].GetAttribute("aria-selected"));

        cut.Render(p => p.Add(x => x.DefaultValue, 0).Add(x => x.OnValueChange, record).AddChildContent(Content(false)));
        Assert.Empty(changes);
        Assert.Equal("true", Tabs(cut)[0].GetAttribute("aria-selected"));

        cut.Render(p => p.Add(x => x.DefaultValue, 0).Add(x => x.OnValueChange, record).AddChildContent(Content(true)));
        (object? value, TabsChangeEventDetails details) = Assert.Single(changes);
        Assert.Equal(1, value);
        Assert.Equal(EventReason.Disabled, details.Reason);
        Assert.Equal("true", Tabs(cut)[1].GetAttribute("aria-selected"));
    }

    [Fact(DisplayName = "calls onValueChange when the selected tab becomes disabled with keepMounted panels")]
    [Trait("Requirement", "TAB-STA-06")]
    [Trait("Requirement", "TAB-DOM-14")]
    public async Task Calls_onValueChange_when_the_selected_tab_becomes_disabled_with_keepMounted_panels()
    {
        EventCallback<ValueChangeEventArgs<int>> record = Record<int>();
        RenderFragment Content(bool disableFirst) => Fragments(
            List(Tab(0, t => t.Add(x => x.Disabled, disableFirst)), Tab(1)),
            KeptPanel(0),
            KeptPanel(1));
        IRenderedComponent<TabsRoot<int>> cut = Context.Render<TabsRoot<int>>(p => p
            .Add(x => x.DefaultValue, 0)
            .Add(x => x.OnValueChange, record)
            .AddChildContent(Content(false)));
        await test.ReportTabsAsync(cut);

        cut.Render(p => p.Add(x => x.DefaultValue, 0).Add(x => x.OnValueChange, record).AddChildContent(Content(true)));
        await test.CompleteExitsAsync(cut);

        (object? value, TabsChangeEventDetails details) = Assert.Single(changes);
        Assert.Equal(1, value);
        Assert.Equal(EventReason.Disabled, details.Reason);
        IReadOnlyList<IElement> panels = Panels(cut);
        Assert.True(panels[0].HasAttribute("hidden"));
        Assert.False(panels[1].HasAttribute("hidden"));
    }

    [Fact(DisplayName = "calls onValueChange with null when the selected tab is removed and no tabs remain")]
    [Trait("Requirement", "TAB-STA-11")]
    [Trait("Requirement", "TAB-EDGE-10")]
    public async Task Calls_onValueChange_with_null_when_the_selected_tab_is_removed_and_no_tabs_remain()
    {
        EventCallback<ValueChangeEventArgs<int?>> record = Record<int?>();
        RenderFragment Content(bool showTab) => Fragments(
            showTab ? List(Tab<int?>(0)) : List(),
            Panel<int?>(0, p => p.Add(x => x.KeepMounted, true).AddChildContent("Panel 0")));
        IRenderedComponent<TabsRoot<int?>> cut = Context.Render<TabsRoot<int?>>(p => p
            .Add(x => x.DefaultValue, 0)
            .Add(x => x.OnValueChange, record)
            .AddChildContent(Content(true)));
        await test.ReportTabsAsync(cut);
        Assert.False(Assert.Single(Panels(cut)).HasAttribute("hidden"));

        cut.Render(p => p.Add(x => x.DefaultValue, 0).Add(x => x.OnValueChange, record).AddChildContent(Content(false)));
        await test.ReportTabsAsync(cut);
        await test.CompleteExitsAsync(cut);

        (object? value, TabsChangeEventDetails details) = Assert.Single(changes);
        Assert.Null(value);
        Assert.Equal(EventReason.Missing, details.Reason);
        Assert.Empty(Tabs(cut));
        Assert.True(Assert.Single(Panels(cut)).HasAttribute("hidden"));
    }

    [Fact(DisplayName = "calls onValueChange with null when a populated tab list is replaced by an empty one")]
    [Trait("Requirement", "TAB-STA-11")]
    [Trait("Requirement", "TAB-ARIA-08")]
    [Trait("Requirement", "TAB-EDGE-10")]
    public async Task Calls_onValueChange_with_null_when_a_populated_tab_list_is_replaced_by_an_empty_one()
    {
        EventCallback<ValueChangeEventArgs<int?>> record = Record<int?>();
        RenderFragment Content(bool empty) => Fragments(
            KeyedList(empty ? "empty" : "populated", empty ? [] : [Tab<int?>(0)]),
            Panel<int?>(0, p => p.Add(x => x.KeepMounted, true).AddChildContent("Panel 0")));
        IRenderedComponent<TabsRoot<int?>> cut = Context.Render<TabsRoot<int?>>(p => p
            .Add(x => x.DefaultValue, 0)
            .Add(x => x.OnValueChange, record)
            .AddChildContent(Content(false)));
        await test.ReportTabsAsync(cut);
        Assert.False(Assert.Single(Panels(cut)).HasAttribute("hidden"));

        cut.Render(p => p.Add(x => x.DefaultValue, 0).Add(x => x.OnValueChange, record).AddChildContent(Content(true)));
        await test.ReportTabsAsync(cut, cut.Find("[role=tablist]"));
        await test.CompleteExitsAsync(cut);

        Assert.Contains(changes, change => change.Value is null && change.Details.Reason == EventReason.Missing);
        IElement panel = Assert.Single(Panels(cut));
        Assert.True(panel.HasAttribute("hidden"));
        Assert.False(panel.HasAttribute("aria-labelledby"));
    }

    [Fact(DisplayName = "calls onValueChange when an explicit defaultValue points at a tab that is never present")]
    [Trait("Requirement", "TAB-STA-07")]
    public async Task Calls_onValueChange_when_an_explicit_defaultValue_points_at_a_tab_that_is_never_present()
    {
        IRenderedComponent<TabsRoot<int>> cut = Context.Render<TabsRoot<int>>(p => p
            .Add(x => x.DefaultValue, 0)
            .Add(x => x.OnValueChange, Record<int>())
            .AddChildContent(List(Tab(1), Tab(2))));

        await test.ReportTabsAsync(cut);

        (object? value, TabsChangeEventDetails details) = Assert.Single(changes);
        Assert.Equal(1, value);
        Assert.Equal(EventReason.Missing, details.Reason);
        Assert.Equal("true", Tabs(cut)[0].GetAttribute("aria-selected"));
    }

    [Fact(DisplayName = "does not emit a second change when the fallback resolves to the current value")]
    [Trait("Requirement", "TAB-STA-05")]
    [Trait("Requirement", "TAB-STA-10")]
    [Trait("Requirement", "TAB-EVT-05")]
    [Trait("Requirement", "TAB-EDGE-06")]
    public async Task Does_not_emit_a_second_change_when_the_fallback_resolves_to_the_current_value()
    {
        IRenderedComponent<TabsRoot<string>> cut = Context.Render<TabsRoot<string>>(p => p
            .Add(x => x.OnValueChange, Record<string>())
            .AddChildContent(List(Tab("a", t => t.Add(x => x.Disabled, true).AddChildContent("Stale duplicate")), Tab("a", t => t.AddChildContent("A")))));

        await test.ReportTabsAsync(cut);
        await test.ReportTabsAsync(cut);

        (object? value, TabsChangeEventDetails details) = Assert.Single(changes);
        Assert.Equal("a", value);
        Assert.Equal(EventReason.Initial, details.Reason);
    }

    [Fact(DisplayName = "does not call onValueChange when a controlled selected tab becomes disabled")]
    [Trait("Requirement", "TAB-API-01")]
    [Trait("Requirement", "TAB-STA-13")]
    [Trait("Requirement", "TAB-EVT-05")]
    public async Task Does_not_call_onValueChange_when_a_controlled_selected_tab_becomes_disabled()
    {
        EventCallback<ValueChangeEventArgs<int>> record = Record<int>();
        RenderFragment Content(bool disableFirst) => List(Tab(0, t => t.Add(x => x.Disabled, disableFirst)), Tab(1));
        IRenderedComponent<TabsRoot<int>> cut = Context.Render<TabsRoot<int>>(p => p
            .Add(x => x.Value, 0)
            .Add(x => x.OnValueChange, record)
            .AddChildContent(Content(false)));
        await test.ReportTabsAsync(cut);

        cut.Render(p => p.Add(x => x.Value, 0).Add(x => x.OnValueChange, record).AddChildContent(Content(true)));

        Assert.Empty(changes);
        Assert.Equal(["true", "false"], Selected(cut));
    }

    [Fact(DisplayName = "sets the panel index data attribute")]
    [Trait("Requirement", "TAB-DOM-17")]
    public async Task Sets_the_panel_index_data_attribute()
    {
        IRenderedComponent<TabsRoot<string>> cut = Context.Render<TabsRoot<string>>(p => p
            .Add(x => x.DefaultValue, "one")
            .AddChildContent(Fragments(List(Tab("one")), Panel("one"))));
        Assert.Equal("-1", Assert.Single(Panels(cut)).GetAttribute("data-index"));

        await test.ReportPanelsAsync(cut);

        Assert.Equal("0", Assert.Single(Panels(cut)).GetAttribute("data-index"));
    }

    [Fact(DisplayName = "port: data-index counts the rendered panels only, in document order")]
    [Trait("Requirement", "TAB-DOM-17")]
    public async Task Port_data_index_counts_the_rendered_panels_only()
    {
        IRenderedComponent<TabsRoot<int>> cut = Context.Render<TabsRoot<int>>(p => p
            .Add(x => x.DefaultValue, 1)
            .AddChildContent(Fragments(List(Tab(0), Tab(1), Tab(2)), Panel(0), KeptPanel(1), KeptPanel(2))));

        await test.ReportPanelsAsync(cut);

        // §16 Q5, transcribed: Panel 0 is not rendered, so the two kept Panels are 0 and 1.
        Assert.Equal(["panel-1", "panel-2"], Panels(cut).Select(panel => panel.GetAttribute("data-testid")));
        Assert.Equal(["0", "1"], Panels(cut).Select(panel => panel.GetAttribute("data-index")));
        Assert.Contains("\"registrationOnly\":true", Assert.Single(test.PanelListOptions.Take(1)), StringComparison.Ordinal);
    }

    private static void Disabled<TValue>(FragmentParameters<TabsTab<TValue>> tab) => tab.Add(x => x.Disabled, true);

    private static string?[] Selected<TComponent>(IRenderedComponent<TComponent> cut)
        where TComponent : IComponent
        => [.. Tabs(cut).Select(tab => tab.GetAttribute("aria-selected"))];

    private static RenderFragment KeyedList(string key, RenderFragment[] content) => builder =>
    {
        builder.OpenComponent<TabsList>(0);
        builder.SetKey(key);
        builder.AddComponentParameter(1, nameof(TabsList.ChildContent), Fragments(content));
        builder.CloseComponent();
    };

    private EventCallback<ValueChangeEventArgs<TValue>> Record<TValue>() =>
        EventCallback.Factory.Create<ValueChangeEventArgs<TValue>>(this, args => changes.Add((args.Value, (TabsChangeEventDetails)args.Details)));
}

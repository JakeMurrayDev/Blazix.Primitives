using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components;
using static Blazix.Primitives.Tests.Components.TabsTestContext;

// The namespace is the project's root: a namespace named after the family would hide the parts' types.
namespace Blazix.Primitives.Tests.Components;

// TabsIndicator.test.tsx, the [L2] clauses (issue 104): whether the element renders, what C# renders under the
// handover, what the Indicator's `tabs` handle is told, and the C# reaction to the module's measurement report (ADR 04
// §2 and Amendments 2026-10-26; design point 2 of the Tabs track, settled by the orchestrator 2026-10-26). The
// measurement itself is L3 and L4.
/// <summary>The tab indicator: its element, its handover and its mirrored measurement.</summary>
public sealed class TabsIndicatorTests : IDisposable
{
    private readonly TabsTestContext test = new();

    private BunitContext Context => test.Context;

    /// <inheritdoc />
    public void Dispose() => test.Dispose();

    [Fact(DisplayName = "port: throws a descriptive error when rendered outside <Tabs.List>")]
    [Trait("Requirement", "TAB-EDGE-02")]
    [Trait("Requirement", "TAB-STR-04")]
    public void Port_throws_a_descriptive_error_when_rendered_outside_Tabs_List()
    {
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() =>
            Context.Render<TabsRoot<int>>(p => p.Add(x => x.Value, 0).AddChildContent(Part<TabsIndicator>())));

        Assert.Equal("Blazix: The `TabsList` context is missing. `TabsTab` and `TabsIndicator` must be placed within `<TabsList>`.", error.Message);
    }

    // TabsIndicator.test.tsx "should not render when no tab is active" (browser only in Base UI): the element's presence is
    // C#'s, so the title runs here; the measurement after a value is set is L4.
    [Fact(DisplayName = "should not render when no tab is active")]
    [Trait("Requirement", "TAB-DOM-19")]
    [Trait("Requirement", "TAB-STA-02")]
    public void Should_not_render_when_no_tab_is_active()
    {
        RenderFragment Build() => List(Tab<int?>(0), Tab<int?>(1), Part<TabsIndicator>(i => i.AddUnmatched("data-testid", "indicator")));

        IRenderedComponent<TabsRoot<int?>> cut = Context.Render<TabsRoot<int?>>(p => p
            .Add(x => x.Value, null)
            .AddChildContent(Build()));

        Assert.Empty(cut.FindAll("[data-testid=indicator]"));

        cut.Render(p => p.Add(x => x.Value, 1).AddChildContent(Build()));

        Assert.Single(cut.FindAll("[data-testid=indicator]"));
    }

    [Fact(DisplayName = "port: the indicator renders hidden, without variables, with role presentation and the handover direction")]
    [Trait("Requirement", "TAB-DOM-20")]
    [Trait("Requirement", "TAB-DOM-22")]
    [Trait("Requirement", "TAB-DOM-23")]
    [Trait("Requirement", "TAB-ARIA-09")]
    [Trait("Requirement", "TAB-EDGE-09")]
    public void Port_the_indicator_renders_hidden_without_variables()
    {
        IRenderedComponent<TabsRoot<int>> cut = Context.Render<TabsRoot<int>>(p => p
            .Add(x => x.Value, 0)
            .AddChildContent(List(Tab(0), Part<TabsIndicator>(i => i.AddUnmatched("data-testid", "indicator")))));

        IElement indicator = cut.Find("[data-testid=indicator]");
        Assert.Equal("SPAN", indicator.TagName);
        Assert.Equal("presentation", indicator.GetAttribute("role"));
        Assert.True(indicator.HasAttribute("hidden"));
        Assert.False(indicator.HasAttribute("style"));
        Assert.Equal("horizontal", indicator.GetAttribute(TabsIndicatorDataAttributes.Orientation));
        Assert.Equal("none", indicator.GetAttribute(TabsIndicatorDataAttributes.ActivationDirection));
        Assert.Equal("""{"part":"indicator","orientation":"horizontal","activateOnFocus":false,"controlled":false,"reportClicks":false,"reportFocus":false}""", test.TabsAttaches[^1]);
    }

    [Fact(DisplayName = "port: the consumer's style and hidden are rendered under the handover and sent to the module")]
    [Trait("Requirement", "TAB-DOM-23")]
    [Trait("Requirement", "TAB-DOM-32")]
    public void Port_the_consumers_style_and_hidden_are_rendered_under_the_handover_and_sent_to_the_module()
    {
        RenderFragment Build(string style, bool hidden) => List(Tab(0), Part<TabsIndicator>(i => i
            .AddUnmatched("data-testid", "indicator")
            .AddUnmatched("style", style)
            .AddUnmatched("hidden", hidden)));

        IRenderedComponent<TabsRoot<int>> cut = Context.Render<TabsRoot<int>>(p => p
            .Add(x => x.Value, 0)
            .AddChildContent(Build("color: red", hidden: false)));

        IElement indicator = cut.Find("[data-testid=indicator]");
        Assert.Equal("color: red", indicator.GetAttribute("style"));
        Assert.False(indicator.HasAttribute("hidden"));
        Assert.Contains("\"hidden\":false", test.TabsAttaches[^1], StringComparison.Ordinal);
        Assert.Contains("\"style\":\"color: red\"", test.TabsAttaches[^1], StringComparison.Ordinal);

        cut.Render(p => p.Add(x => x.Value, 0).AddChildContent(Build("color: blue", hidden: true)));

        // Frozen from attach: the module applies the new values.
        indicator = cut.Find("[data-testid=indicator]");
        Assert.Equal("color: red", indicator.GetAttribute("style"));
        Assert.False(indicator.HasAttribute("hidden"));
        string update = Assert.Single(test.TabsUpdates);
        Assert.Contains("\"hidden\":true", update, StringComparison.Ordinal);
        Assert.Contains("\"style\":\"color: blue\"", update, StringComparison.Ordinal);
    }

    // TabsIndicator.test.tsx "exposes null active tab state when the selected value has no matching tab" (its measurement is
    // L4) and "perf: single tab resize does not fan out excessive indicator rerenders", restated as a mirror count (issue 104
    // deliverable 10): a report that changes the measurement renders the Indicator once, the List and the Tabs not at all.
    [Fact(DisplayName = "port: a measurement report reaches the render function's state once and renders only the indicator")]
    [Trait("Requirement", "TAB-STA-20")]
    [Trait("Requirement", "TAB-DOM-24")]
    [Trait("Requirement", "TAB-DOM-31")]
    public async Task Port_a_measurement_report_reaches_the_render_functions_state_once()
    {
        // Each part's class function runs once per render of that part (bUnit's RenderCount also counts a child's).
        List<TabsIndicatorState> states = [];
        int rootRenders = 0;
        int listRenders = 0;
        int tabRenders = 0;
        RenderFragment<Blazix.Primitives.Rendering.RenderProps<TabsIndicatorState>> render = props => builder =>
        {
            states.Add(props.State);
            builder.OpenElement(0, "span");
            builder.AddMultipleAttributes(1, props.Attributes);
            builder.CloseElement();
        };
        Func<TabsTabState, string?> countTab = _ =>
        {
            tabRenders++;
            return null;
        };

        IRenderedComponent<TabsRoot<int>> cut = Context.Render<TabsRoot<int>>(p => p
            .Add(x => x.Value, 0)
            .Add(x => x.ClassValue, _ =>
            {
                rootRenders++;
                return null;
            })
            .AddChildContent(Part<TabsList>(l => l
                .Add(x => x.ClassValue, (Func<TabsListState, string?>)(_ =>
                {
                    listRenders++;
                    return null;
                }))
                .AddChildContent(Fragments(
                    Tab(0, t => t.Add(x => x.ClassValue, countTab)),
                    Tab(1, t => t.Add(x => x.ClassValue, countTab)),
                    Part<TabsIndicator>(i => i.Add(x => x.RenderFunction, render)))))));

        Assert.Null(states[^1].ActiveTabPosition);
        Assert.Null(states[^1].ActiveTabSize);
        (int Root, int List, int Tab, int Indicator) before = (rootRenders, listRenders, tabRenders, states.Count);

        await test.ReportMeasurementAsync(cut, new TabsTabPosition(10, 80.5, 0, 0), new TabsTabSize(50.25, 30));

        Assert.Equal((before.Root, before.List, before.Tab, before.Indicator + 1), (rootRenders, listRenders, tabRenders, states.Count));
        Assert.Equal(new TabsTabPosition(10, 80.5, 0, 0), states[^1].ActiveTabPosition);
        Assert.Equal(new TabsTabSize(50.25, 30), states[^1].ActiveTabSize);

        await test.ReportMeasurementAsync(cut, null, null);

        Assert.Equal(before.Indicator + 2, states.Count);
        Assert.Null(states[^1].ActiveTabPosition);
        Assert.Null(states[^1].ActiveTabSize);
    }
}

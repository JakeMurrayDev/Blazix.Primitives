using AngleSharp.Dom;
using Blazix.Primitives.Interop;
using Bunit;

// The namespace is the project's root: a namespace named after the family would hide the parts' types.
namespace Blazix.Primitives.Tests.Components;

// CheckboxIndicator.test.tsx, the rows C# observes (issue 81): when the element is rendered, what C# renders at its
// creation (the handover of data-starting-style, ADR 04 Amendments 2026-10-18), the twin's options, and the mirror of the
// module's status and completion reports. The frames themselves are the transitions module's (tests/browser/checkbox.spec.ts).
/// <summary>The checkbox indicator: its presence, its state and its transitions twin.</summary>
public sealed class CheckboxIndicatorTests : IDisposable
{
    private const string IndicatorSelector = "[data-testid=indicator]";
    private const string OpenSelector = "[data-checked], [data-indeterminate]";

    private readonly CheckboxTestContext test = new();

    private BunitContext Context => test.Context;

    /// <inheritdoc />
    public void Dispose() => test.Dispose();

    [Fact(DisplayName = "throws a descriptive error when rendered outside <Checkbox.Root>")]
    [Trait("Requirement", "CHK-EDGE-01")]
    [Trait("Requirement", "CHK-STR-02")]
    public void Throws_a_descriptive_error_when_rendered_outside_Checkbox_Root()
    {
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => Context.Render<CheckboxIndicator>());

        Assert.Equal("Blazix: The `CheckboxRoot` context is missing. Checkbox parts must be placed within `<CheckboxRoot>`.", error.Message);
    }

    [Fact(DisplayName = "should not render indicator by default")]
    [Trait("Requirement", "CHK-ANI-01")]
    public void Should_not_render_indicator_by_default()
    {
        IRenderedComponent<CheckboxRoot> cut = RenderRoot();

        Assert.Empty(cut.FindAll(IndicatorSelector));
        Assert.Empty(test.TransitionsModule.Invocations["attach"]);
    }

    [Fact(DisplayName = "should render indicator when checked")]
    [Trait("Requirement", "CHK-ANI-01")]
    [Trait("Requirement", "CHK-ANI-02")]
    public void Should_render_indicator_when_checked()
    {
        IRenderedComponent<CheckboxRoot> cut = RenderRoot(@checked: true);

        // CHK-ANI-02: checked at mount, so no starting phase is rendered or handed over.
        IElement indicator = cut.Find(IndicatorSelector);
        Assert.False(indicator.HasAttribute("data-starting-style"));
        Assert.Equal(
            $$"""{"open":true,"mounted":true,"enableIdleState":false,"deferEndingState":false,"keepMounted":false,"hiddenUntilFound":false,"hiddenWhenUnmounted":false,"suppressEndingStyle":false,"reportOpenComplete":false,"revealOnBeforeMatch":false,"unmountRequests":0,"externalExit":false,"openSelector":"{{OpenSelector}}"}""",
            Assert.Single(test.TransitionsAttaches));
    }

    [Fact(DisplayName = "should keep indicator mounted when unchecked")]
    [Trait("Requirement", "CHK-API-18")]
    [Trait("Requirement", "CHK-ANI-07")]
    public void Should_keep_indicator_mounted_when_unchecked()
    {
        IRenderedComponent<CheckboxRoot> cut = RenderRoot(keepMounted: true);

        IElement indicator = cut.Find(IndicatorSelector);
        Assert.Equal(string.Empty, indicator.GetAttribute("data-unchecked"));
        Assert.False(indicator.HasAttribute("hidden"));
        Assert.False(indicator.HasAttribute("style"));
        Assert.Equal(
            $$"""{"open":false,"mounted":false,"enableIdleState":false,"deferEndingState":false,"keepMounted":true,"hiddenUntilFound":false,"hiddenWhenUnmounted":false,"suppressEndingStyle":false,"reportOpenComplete":false,"revealOnBeforeMatch":false,"unmountRequests":0,"externalExit":false,"openSelector":"{{OpenSelector}}"}""",
            Assert.Single(test.TransitionsAttaches));
    }

    [Fact(DisplayName = "should keep indicator mounted when checked")]
    [Trait("Requirement", "CHK-API-18")]
    public void Should_keep_indicator_mounted_when_checked()
    {
        IRenderedComponent<CheckboxRoot> cut = RenderRoot(@checked: true, keepMounted: true);

        Assert.Equal(string.Empty, cut.Find(IndicatorSelector).GetAttribute("data-checked"));
    }

    [Fact(DisplayName = "should keep indicator mounted when indeterminate")]
    [Trait("Requirement", "CHK-API-18")]
    [Trait("Requirement", "CHK-API-04")]
    public void Should_keep_indicator_mounted_when_indeterminate()
    {
        IRenderedComponent<CheckboxRoot> cut = RenderRoot(indeterminate: true, keepMounted: true);

        Assert.Equal(string.Empty, cut.Find(IndicatorSelector).GetAttribute("data-indeterminate"));
    }

    [Fact(DisplayName = "port: an indeterminate box renders its indicator without keepMounted")]
    [Trait("Requirement", "CHK-API-04")]
    [Trait("Requirement", "CHK-ANI-01")]
    public void Port_an_indeterminate_box_renders_its_indicator()
    {
        IRenderedComponent<CheckboxRoot> cut = RenderRoot(indeterminate: true);

        Assert.Single(cut.FindAll(IndicatorSelector));
    }

    [Fact(DisplayName = "port: an indicator a toggle mounts is created with data-starting-style and hands it over on attach")]
    [Trait("Requirement", "CHK-ANI-03")]
    [Trait("Requirement", "CHK-STA-02")]
    public async Task Port_an_indicator_a_toggle_mounts_is_created_with_data_starting_style()
    {
        List<TransitionStatus?> statuses = [];
        IRenderedComponent<CheckboxRoot> cut = RenderRoot(classValue: state =>
        {
            statuses.Add(state.TransitionStatus);
            return null;
        });

        await test.ToggleAsync(cut, true);

        Assert.Equal(string.Empty, cut.Find(IndicatorSelector).GetAttribute("data-starting-style"));
        Assert.Equal(
            $$"""{"open":true,"mounted":true,"status":"starting","enableIdleState":false,"deferEndingState":false,"keepMounted":false,"hiddenUntilFound":false,"hiddenWhenUnmounted":false,"suppressEndingStyle":false,"reportOpenComplete":false,"revealOnBeforeMatch":false,"unmountRequests":0,"externalExit":false,"openSelector":"{{OpenSelector}}"}""",
            Assert.Single(test.TransitionsAttaches));
        Assert.Equal(TransitionStatus.Starting, statuses[^1]);

        // The module clears the attribute on the next frame and reports it; C# mirrors the status (CHK-STA-02's lag).
        await test.ReportTransitionAsync(cut, null, mounted: true);
        Assert.Null(statuses[^1]);
    }

    [Fact(DisplayName = "port: an unchecked indicator stays through its exit and is removed by the completion report")]
    [Trait("Requirement", "CHK-ANI-04")]
    [Trait("Requirement", "CHK-ANI-01")]
    public async Task Port_an_unchecked_indicator_stays_through_its_exit()
    {
        List<TransitionStatus?> statuses = [];
        IRenderedComponent<CheckboxRoot> cut = RenderRoot(@checked: true, classValue: state =>
        {
            statuses.Add(state.TransitionStatus);
            return null;
        });

        await test.ToggleAsync(cut, false);

        // Still in the document, closed (data-unchecked, which the module's open selector reads), with the ending status.
        IElement indicator = cut.Find(IndicatorSelector);
        Assert.Equal(string.Empty, indicator.GetAttribute("data-unchecked"));
        Assert.False(indicator.HasAttribute("data-ending-style"));
        Assert.Equal(TransitionStatus.Ending, statuses[^1]);

        await test.CompleteExitAsync(cut);

        Assert.Empty(cut.FindAll(IndicatorSelector));
        Assert.Single(test.TransitionsHandle.Invocations["dispose"]);
    }

    [Fact(DisplayName = "port: a box checked again during the exit keeps its indicator without a starting phase")]
    [Trait("Requirement", "CHK-ANI-08")]
    public async Task Port_a_box_checked_again_during_the_exit_keeps_its_indicator()
    {
        IRenderedComponent<CheckboxRoot> cut = RenderRoot(@checked: true);

        await test.ToggleAsync(cut, false);
        await test.ToggleAsync(cut, true);

        // The same element, still attached once: the module aborts the exit and clears data-ending-style itself.
        IElement indicator = cut.Find(IndicatorSelector);
        Assert.Equal(string.Empty, indicator.GetAttribute("data-checked"));
        Assert.False(indicator.HasAttribute("data-starting-style"));
        Assert.Single(test.TransitionsAttaches);
        Assert.Empty(test.TransitionsHandle.Invocations["dispose"]);
    }

    [Fact(DisplayName = "port: a kept indicator is never removed, and its exit still reports the ending status")]
    [Trait("Requirement", "CHK-ANI-07")]
    [Trait("Requirement", "CHK-API-18")]
    public async Task Port_a_kept_indicator_is_never_removed()
    {
        List<TransitionStatus?> statuses = [];
        IRenderedComponent<CheckboxRoot> cut = RenderRoot(@checked: true, keepMounted: true, classValue: state =>
        {
            statuses.Add(state.TransitionStatus);
            return null;
        });

        await test.ToggleAsync(cut, false);
        Assert.Equal(TransitionStatus.Ending, statuses[^1]);
        await test.CompleteExitAsync(cut);

        Assert.Single(cut.FindAll(IndicatorSelector));
        Assert.Null(statuses[^1]);
        Assert.Empty(test.TransitionsHandle.Invocations["dispose"]);
    }

    [Fact(DisplayName = "port: several indicators of one box each attach their own twin")]
    [Trait("Requirement", "CHK-STR-02")]
    public void Port_several_indicators_of_one_box_each_attach_their_own_twin()
    {
        IRenderedComponent<CheckboxRoot> cut = test.Context.Render<CheckboxRoot>(p => p
            .Add(x => x.Checked, true)
            .AddChildContent(builder =>
            {
                builder.OpenComponent<CheckboxIndicator>(0);
                builder.CloseComponent();
                builder.OpenComponent<CheckboxIndicator>(1);
                builder.CloseComponent();
            }));

        Assert.Equal(2, cut.FindAll("[role=checkbox] > span").Count);
        Assert.Equal(2, test.TransitionsAttaches.Count);
    }

    private IRenderedComponent<CheckboxRoot> RenderRoot(bool? @checked = null, bool indeterminate = false, bool keepMounted = false, Func<CheckboxIndicatorState, string?>? classValue = null)
    {
        return Context.Render<CheckboxRoot>(p =>
        {
            if (@checked is { } value)
            {
                p.Add(x => x.DefaultChecked, value);
            }

            p.Add(x => x.Indeterminate, indeterminate);
            p.AddChildContent<CheckboxIndicator>(i =>
            {
                i.Add(x => x.KeepMounted, keepMounted).AddUnmatched("data-testid", "indicator");
                if (classValue is not null)
                {
                    i.Add(x => x.ClassValue, classValue);
                }
            });
        });
    }
}

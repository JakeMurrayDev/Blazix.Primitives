// Family tests use the project's root namespace.
namespace Blazix.Primitives.Tests;

/// <summary>
/// The popup store's selectors at L1 (utils/popups/store.test.ts, titles verbatim): <c>isOpenedByTrigger</c> reads the
/// effective open state (PSH-STA-02, PSH-STA-08) and the derived transition status reads <c>starting</c> while open and
/// not yet mounted (PSH-STA-05, TRN-STA-14).
/// </summary>
public sealed class PopupStoreSelectorTests
{
    private static PopupStoreState<NoPayload> CreateState(bool open = false, bool? openProp = null, bool mounted = false, TransitionStatus? transitionStatus = null) =>
        PopupStoreState<NoPayload>.Initial(null) with { ActiveTriggerId = "trigger", Open = open, OpenProp = openProp, Mounted = mounted, TransitionStatus = transitionStatus };

    [Fact(DisplayName = "uses the controlled open state when present")]
    [Trait("Requirement", "PSH-STA-02")]
    public void Uses_the_controlled_open_state_when_present()
    {
        Assert.True(CreateState(open: false, openProp: true).IsOpenedByTrigger("trigger"));
        Assert.False(CreateState(open: true, openProp: false).IsOpenedByTrigger("trigger"));
    }

    [Fact(DisplayName = "uses the internal open state when uncontrolled")]
    [Trait("Requirement", "PSH-STA-02")]
    public void Uses_the_internal_open_state_when_uncontrolled()
    {
        Assert.True(CreateState(open: true).IsOpenedByTrigger("trigger"));
        Assert.False(CreateState(open: false).IsOpenedByTrigger("trigger"));
    }

    [Fact(DisplayName = "requires the trigger to be active")]
    [Trait("Requirement", "PSH-STA-08")]
    public void Requires_the_trigger_to_be_active()
    {
        Assert.True(CreateState(open: false, openProp: true).IsOpenedByTrigger("trigger"));
        Assert.False(CreateState(open: false, openProp: true).IsOpenedByTrigger("other-trigger"));
    }

    [Fact(DisplayName = "reports the starting phase while the popup is open but not yet mounted")]
    [Trait("Requirement", "PSH-STA-05")]
    public void Reports_the_starting_phase_while_the_popup_is_open_but_not_yet_mounted()
    {
        Assert.Equal(TransitionStatus.Starting, CreateState(open: true, mounted: false).DerivedTransitionStatus);
    }

    [Fact(DisplayName = "uses the controlled open state to detect the starting phase")]
    [Trait("Requirement", "PSH-STA-05")]
    public void Uses_the_controlled_open_state_to_detect_the_starting_phase()
    {
        Assert.Equal(TransitionStatus.Starting, CreateState(open: false, openProp: true, mounted: false).DerivedTransitionStatus);
    }

    [Fact(DisplayName = "keeps the stored status once the opening cycle is mounted")]
    [Trait("Requirement", "PSH-STA-05")]
    public void Keeps_the_stored_status_once_the_opening_cycle_is_mounted()
    {
        Assert.Equal(TransitionStatus.Starting, CreateState(open: true, mounted: true, transitionStatus: TransitionStatus.Starting).DerivedTransitionStatus);
        Assert.Null(CreateState(open: true, mounted: true, transitionStatus: null).DerivedTransitionStatus);
    }

    [Fact(DisplayName = "does not start a new enter phase when an exit is reversed")]
    [Trait("Requirement", "PSH-STA-05")]
    public void Does_not_start_a_new_enter_phase_when_an_exit_is_reversed()
    {
        Assert.Equal(TransitionStatus.Ending, CreateState(open: true, mounted: true, transitionStatus: TransitionStatus.Ending).DerivedTransitionStatus);
    }

    [Fact(DisplayName = "keeps the stored status while the popup is closed")]
    [Trait("Requirement", "PSH-STA-05")]
    public void Keeps_the_stored_status_while_the_popup_is_closed()
    {
        Assert.Equal(TransitionStatus.Ending, CreateState(open: false, mounted: true, transitionStatus: TransitionStatus.Ending).DerivedTransitionStatus);
        Assert.Null(CreateState(open: false, mounted: false, transitionStatus: null).DerivedTransitionStatus);
    }
}

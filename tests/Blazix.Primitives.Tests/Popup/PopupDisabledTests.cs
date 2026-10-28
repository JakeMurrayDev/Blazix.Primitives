// Family tests use the project's root namespace.
namespace Blazix.Primitives.Tests;

/// <summary>
/// A family root's disabled state on the shared store (TIP-STA-02, TIP-API-09, TIP-DIS-06; tooltip/root/TooltipRoot.tsx:68-82:
/// the transitions run on <c>!disabled &amp;&amp; openState</c>): the effective open state is the stored or controlled one
/// and not disabled, so a disabled store reports closed, does not mount, and starts its exit when disabled while open,
/// while the stored open state is left to the family's disabled close. Design point 13.7 of M4 track 6 (pending the user).
/// </summary>
public sealed class PopupDisabledTests
{
    [Fact(DisplayName = "port: a store disabled before its first open reports closed and does not mount, while its stored open state is the default")]
    [Trait("Requirement", "TIP-STA-02")]
    [Trait("Requirement", "TIP-EDGE-10")]
    public void A_store_disabled_before_its_first_open_reports_closed_and_does_not_mount()
    {
        TestFamilyStore<NoPayload> store = new();

        store.SetDisabled(true);
        store.ApplyRootParameters(defaultOpen: true, defaultTriggerId: null);

        Assert.True(store.Popup.Open);
        Assert.True(store.Popup.Disabled);
        Assert.False(store.Popup.EffectiveOpen);
        Assert.False(store.IsOpen);
        Assert.False(store.Popup.Mounted);
        Assert.Null(store.Popup.TransitionStatus);
    }

    [Fact(DisplayName = "port: disabling an open store starts its exit and keeps its stored open state")]
    [Trait("Requirement", "TIP-STA-02")]
    [Trait("Requirement", "TIP-API-09")]
    public void Disabling_an_open_store_starts_its_exit_and_keeps_its_stored_open_state()
    {
        TestFamilyStore<NoPayload> store = new();
        store.ApplyRootParameters(defaultOpen: true, defaultTriggerId: null);
        Assert.True(store.Popup.Mounted);

        store.SetDisabled(true);

        Assert.True(store.Popup.Open);
        Assert.False(store.IsOpen);
        Assert.True(store.Popup.Mounted);
        Assert.Equal(TransitionStatus.Ending, store.Popup.TransitionStatus);
        Assert.False(store.Popup.IsOpenedByTrigger(null));
    }

    [Fact(DisplayName = "port: an open request applied while disabled stores the open state without mounting, and re-enabling mounts it")]
    [Trait("Requirement", "TIP-STA-02")]
    [Trait("Requirement", "TIP-API-09")]
    public async Task An_open_request_applied_while_disabled_stores_the_open_state_without_mounting()
    {
        TestFamilyStore<NoPayload> store = new();
        store.ApplyRootParameters(defaultOpen: false, defaultTriggerId: null);
        store.SetDisabled(true);

        bool applied = await store.RequestOpenAsync(true, new OpenChangeEventDetails(EventReason.TriggerHover));

        Assert.True(applied);
        Assert.True(store.Popup.Open);
        Assert.False(store.IsOpen);
        Assert.False(store.Popup.Mounted);

        store.SetDisabled(false);

        Assert.True(store.IsOpen);
        Assert.True(store.Popup.Mounted);
        Assert.Equal(TransitionStatus.Starting, store.Popup.DerivedTransitionStatus);
    }
}

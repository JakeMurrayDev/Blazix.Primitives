using System.Reflection;
using Blazix.Primitives.Interop;
using Microsoft.AspNetCore.Components;

// Family tests use the project's root namespace.
namespace Blazix.Primitives.Tests;

/// <summary>
/// The tooltip store at L1 (tooltip/store/TooltipStore.ts; TIP §4.1): its initial values (TIP-STA-01), the instant type of
/// EVD-EVT-17 with the stale value replicated (TIP-STA-05, TIP §16 Q13), the delay group's override carried by the open
/// request (TIP-STA-06/07; ADR 04 Amendments 2026-10-27), the last reason (TIP-STA-04), the forwarded trigger state
/// (TIP-STA-09) and the type-level checks (TIP-POS-09, TIP-API-23).
/// </summary>
public sealed class TooltipStoreTests
{
    private static TooltipStore<NoPayload> Store(bool defaultOpen = false)
    {
        TooltipStore<NoPayload> store = new("base-ui-1", "base-ui-2");
        store.ApplyRootParameters(defaultOpen, defaultTriggerId: null);
        return store;
    }

    private static Task<bool> Request(TooltipStore<NoPayload> store, bool open, EventReason reason, bool instant = false) =>
        store.RequestAsync(new OpenChangeRequest(open, new OpenChangeEventDetails(reason), instant, null));

    [Fact(DisplayName = "opens initially and remains uncontrolled")]
    [Trait("Requirement", "TIP-STA-01")]
    public async Task Opens_initially_and_remains_uncontrolled()
    {
        TooltipStore<NoPayload> store = new("base-ui-1", "base-ui-2");
        TooltipStoreState<NoPayload> initial = store.State;

        Assert.Null(initial.InstantType);
        Assert.Equal(TooltipTrackCursorAxis.None, initial.TrackCursorAxis);
        Assert.False(initial.DisableHoverablePopup);
        Assert.Null(initial.LastOpenChangeReason);
        Assert.True(initial.CloseOnClick);
        Assert.Equal(0, initial.CloseDelay);
        Assert.False(initial.Popup.Disabled);
        Assert.True(store.CloseOnActiveTriggerUnmount);
        Assert.Equal("Tooltip", store.ComponentName);

        store.ApplyRootParameters(defaultOpen: true, defaultTriggerId: null);

        Assert.True(store.IsOpen);
        Assert.Null(store.Popup.OpenProp);
        Assert.True(store.Popup.Mounted);

        await Request(store, false, EventReason.TriggerHover);

        Assert.False(store.IsOpen);
    }

    [Theory(DisplayName = "port: the instant type follows the reason table of EVD-EVT-17")]
    [InlineData(true, EventReason.TriggerFocus, TooltipInstantType.Focus)]
    [InlineData(false, EventReason.TriggerPress, TooltipInstantType.Dismiss)]
    [InlineData(false, EventReason.EscapeKey, TooltipInstantType.Dismiss)]
    [InlineData(true, EventReason.TriggerHover, null)]
    [InlineData(false, EventReason.TriggerHover, null)]
    [Trait("Requirement", "TIP-STA-05")]
    [Trait("Requirement", "TIP-ANI-08")]
    public async Task The_instant_type_follows_the_reason_table(bool open, EventReason reason, TooltipInstantType? expected)
    {
        TooltipStore<NoPayload> store = Store(defaultOpen: !open);
        if (expected is null)
        {
            // A hover change clears a value set before it.
            await Request(store, !open, EventReason.TriggerFocus);
            await Request(store, open, EventReason.TriggerFocus);
        }

        await Request(store, open, reason);

        Assert.Equal(expected, store.State.InstantType);
        Assert.Equal(reason, store.State.LastOpenChangeReason);
    }

    [Theory(DisplayName = "port: a close or open with an unmapped reason keeps the stale instant type")]
    [InlineData(false, EventReason.OutsidePress)]
    [InlineData(false, EventReason.TriggerFocus)]
    [InlineData(false, EventReason.ImperativeAction)]
    [InlineData(false, EventReason.Disabled)]
    [InlineData(true, EventReason.ImperativeAction)]
    [Trait("Requirement", "TIP-STA-05")]
    public async Task A_change_with_an_unmapped_reason_keeps_the_stale_instant_type(bool open, EventReason reason)
    {
        TooltipStore<NoPayload> store = Store();
        await Request(store, true, EventReason.TriggerFocus);
        if (open)
        {
            await Request(store, false, EventReason.OutsidePress);
        }

        await Request(store, open, reason);

        Assert.Equal(TooltipInstantType.Focus, store.State.InstantType);
    }

    [Fact(DisplayName = "port: an open in the group's instant phase renders delay with its open state and a later ordinary close restores the value it replaced")]
    [Trait("Requirement", "TIP-STA-06")]
    [Trait("Requirement", "TIP-STA-07")]
    [Trait("Requirement", "TIP-ANI-03")]
    public async Task An_open_in_the_instant_phase_renders_delay_and_an_ordinary_close_restores()
    {
        TooltipStore<NoPayload> store = Store();
        List<(bool Open, TooltipInstantType? Instant)> writes = [];
        using IDisposable subscription = store.Subscribe(state => writes.Add((state.Popup.EffectiveOpen, state.InstantType)));

        await Request(store, true, EventReason.TriggerHover, instant: true);

        Assert.Equal((true, TooltipInstantType.Delay), writes.Single(write => write.Open));
        Assert.Equal(TooltipInstantType.Delay, store.State.InstantType);

        await Request(store, false, EventReason.TriggerHover);

        Assert.Equal(TransitionStatus.Ending, store.Popup.TransitionStatus);
        Assert.Null(store.State.InstantType);
    }

    [Fact(DisplayName = "port: the group's close of the previous member renders delay for its exit")]
    [Trait("Requirement", "TIP-STA-06")]
    [Trait("Requirement", "TIP-DIS-04")]
    public async Task The_groups_close_of_the_previous_member_renders_delay_for_its_exit()
    {
        TooltipStore<NoPayload> store = Store();
        await Request(store, true, EventReason.TriggerFocus);

        await Request(store, false, EventReason.None, instant: true);

        Assert.Equal(TransitionStatus.Ending, store.Popup.TransitionStatus);
        Assert.Equal(TooltipInstantType.Delay, store.State.InstantType);

        await Request(store, true, EventReason.ImperativeAction);

        // The value the override replaced comes back with the first change it no longer covers, stale (TIP §16 Q13).
        Assert.Equal(TooltipInstantType.Focus, store.State.InstantType);
    }

    [Fact(DisplayName = "port: a close with reason none of an unmounted tooltip keeps its instant type unset and records the reason")]
    [Trait("Requirement", "TIP-STA-06")]
    public async Task A_close_with_reason_none_of_an_unmounted_tooltip_keeps_its_instant_type_unset()
    {
        TooltipStore<NoPayload> store = Store();

        await Request(store, false, EventReason.None);

        Assert.False(store.Popup.Mounted);
        Assert.Null(store.State.InstantType);
        Assert.Equal(EventReason.None, store.State.LastOpenChangeReason);
    }

    [Fact(DisplayName = "port: the forwarded trigger state and the root's inputs are written only when they change")]
    [Trait("Requirement", "TIP-STA-09")]
    [Trait("Requirement", "TIP-STA-16")]
    public void The_forwarded_state_and_the_roots_inputs_are_written_only_when_they_change()
    {
        TooltipStore<NoPayload> store = Store();
        int writes = 0;
        using IDisposable subscription = store.Subscribe(_ => writes++);

        store.SetForwardedState(closeOnClick: false, closeDelay: 250);
        store.SetForwardedState(closeOnClick: false, closeDelay: 250);
        store.SetRootInputs(TooltipTrackCursorAxis.X, disableHoverablePopup: true);
        store.SetRootInputs(TooltipTrackCursorAxis.X, disableHoverablePopup: true);

        Assert.Equal(2, writes);
        Assert.False(store.State.CloseOnClick);
        Assert.Equal(250, store.State.CloseDelay);
        Assert.Equal(TooltipTrackCursorAxis.X, store.State.TrackCursorAxis);
        Assert.True(store.State.DisableHoverablePopup);
    }

    [Theory(DisplayName = "port: the positioner renders tracking-cursor whenever the cursor is tracked")]
    [InlineData(TooltipTrackCursorAxis.None, TooltipInstantType.Focus)]
    [InlineData(TooltipTrackCursorAxis.X, TooltipInstantType.TrackingCursor)]
    [InlineData(TooltipTrackCursorAxis.Y, TooltipInstantType.TrackingCursor)]
    [InlineData(TooltipTrackCursorAxis.Both, TooltipInstantType.TrackingCursor)]
    [Trait("Requirement", "TIP-STA-12")]
    public async Task The_positioner_renders_tracking_cursor_whenever_the_cursor_is_tracked(TooltipTrackCursorAxis axis, TooltipInstantType expected)
    {
        TooltipStore<NoPayload> store = Store();
        store.SetRootInputs(axis, disableHoverablePopup: false);

        await Request(store, true, EventReason.TriggerFocus);

        Assert.Equal(expected, store.State.PositionerInstantType);
        Assert.Equal(TooltipInstantType.Focus, store.State.InstantType);
    }

    [Fact(DisplayName = "port: TooltipPositioner declares no KeepMounted parameter, which lives on TooltipPortal")]
    [Trait("Requirement", "TIP-POS-09")]
    public void TooltipPositioner_declares_no_KeepMounted_parameter()
    {
        Assert.NotNull(typeof(TooltipPortal).GetProperty(nameof(TooltipPortal.KeepMounted))?.GetCustomAttribute<ParameterAttribute>());
        Assert.Null(typeof(TooltipPositioner).GetProperty("KeepMounted", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance));
    }

    [Fact(DisplayName = "port: the handle carries the payload type of its root and triggers")]
    [Trait("Requirement", "TIP-API-23")]
    [Trait("Requirement", "TIP-API-12")]
    public void The_handle_carries_the_payload_type_of_its_root_and_triggers()
    {
        Assert.Equal(typeof(PopupHandle<int>), typeof(TooltipHandle<int>).BaseType);
        Assert.Equal(typeof(TooltipHandle<int>), typeof(TooltipRoot<int>).GetProperty(nameof(TooltipRoot<int>.Handle))!.PropertyType);
        Assert.Equal(typeof(TooltipHandle<int>), typeof(TooltipTrigger<int>).GetProperty(nameof(TooltipTrigger<int>.Handle))!.PropertyType);
        Assert.Equal(typeof(int), typeof(TooltipTrigger<int>).GetProperty(nameof(TooltipTrigger<int>.Payload))!.PropertyType);
        Assert.Equal(typeof(RenderFragment<int>), typeof(TooltipRoot<int>).GetProperty(nameof(TooltipRoot<int>.PayloadContent))!.PropertyType);
        Assert.False(new TooltipHandle<int>().IsOpen);
    }
}

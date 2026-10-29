using System.Reflection;
using Microsoft.AspNetCore.Components;

// Family tests use the project's root namespace.
namespace Blazix.Primitives.Tests;

/// <summary>
/// The preview card store at L1 (preview-card/store/PreviewCardStore.ts; PRV §4.1): its initial values (PRV-STA-01), the
/// instant type of EVD-EVT-17 with the stale value replicated (PRV-STA-10, PRV §16 Q4), the forwarded close delay that
/// is never reset (PRV-STA-11, PRV §16 Q6), the millisecond fallbacks (PRV-API-10/11) and the type-level checks
/// (PRV-API-19, PRV-API-07/21).
/// </summary>
public sealed class PreviewCardStoreTests
{
    private static PreviewCardStore<NoPayload> Store(bool defaultOpen = false)
    {
        PreviewCardStore<NoPayload> store = new("base-ui-1", "base-ui-2");
        store.ApplyRootParameters(defaultOpen, defaultTriggerId: null);
        return store;
    }

    private static Task<bool> Request(PreviewCardStore<NoPayload> store, bool open, EventReason reason) =>
        store.RequestOpenAsync(open, new OpenChangeEventDetails(reason));

    [Fact(DisplayName = "port: the store starts closed with no instant type, the 300 ms close delay and no viewport")]
    [Trait("Requirement", "PRV-STA-01")]
    public void The_store_starts_closed_with_no_instant_type_the_300_ms_close_delay_and_no_viewport()
    {
        PreviewCardStore<NoPayload> store = new("base-ui-1", "base-ui-2");
        PreviewCardStoreState<NoPayload> initial = store.State;

        Assert.Null(initial.InstantType);
        Assert.Equal(300, initial.CloseDelay);
        Assert.False(initial.AdaptiveOrigin);
        Assert.False(store.IsOpen);
        Assert.True(store.CloseOnActiveTriggerUnmount);
        Assert.Equal("PreviewCard", store.ComponentName);

        // The inert fallback store a handle exposes before a root attaches has the same initial state (PSH-STA-37).
        PreviewCardStore<NoPayload> fallback = new(null, null, inert: true);
        Assert.Equal(300, fallback.State.CloseDelay);
        Assert.False(fallback.IsOpen);
    }

    [Theory(DisplayName = "port: the instant type follows the reason table of EVD-EVT-17")]
    [InlineData(true, EventReason.TriggerFocus, PreviewCardInstantType.Focus)]
    [InlineData(false, EventReason.EscapeKey, PreviewCardInstantType.Dismiss)]
    [InlineData(false, EventReason.TriggerPress, PreviewCardInstantType.Dismiss)]
    [InlineData(true, EventReason.TriggerHover, null)]
    [InlineData(false, EventReason.TriggerHover, null)]
    [Trait("Requirement", "PRV-STA-10")]
    public async Task The_instant_type_follows_the_reason_table(bool open, EventReason reason, PreviewCardInstantType? expected)
    {
        PreviewCardStore<NoPayload> store = Store(defaultOpen: !open);
        if (expected is null)
        {
            // A hover change clears a value set before it.
            await Request(store, !open, EventReason.TriggerFocus);
            await Request(store, open, EventReason.TriggerFocus);
        }

        await Request(store, open, reason);

        Assert.Equal(expected, store.State.InstantType);
    }

    [Theory(DisplayName = "port: a change with an unmapped reason keeps the stale instant type")]
    [InlineData(false, EventReason.OutsidePress)]
    [InlineData(false, EventReason.TriggerFocus)]
    [InlineData(false, EventReason.ImperativeAction)]
    [InlineData(false, EventReason.None)]
    [InlineData(true, EventReason.ImperativeAction)]
    [Trait("Requirement", "PRV-STA-10")]
    [Trait("Requirement", "PRV-EDGE-11")]
    public async Task A_change_with_an_unmapped_reason_keeps_the_stale_instant_type(bool open, EventReason reason)
    {
        // PRV §16 Q4, replicated: the focus value of the last open survives a close (or a reopen) with an unmapped reason.
        PreviewCardStore<NoPayload> store = Store();
        await Request(store, true, EventReason.TriggerFocus);
        if (open)
        {
            await Request(store, false, EventReason.OutsidePress);
        }

        await Request(store, open, reason);

        Assert.Equal(PreviewCardInstantType.Focus, store.State.InstantType);
    }

    [Fact(DisplayName = "port: the forwarded close delay is written only when it changes and is not reset on close")]
    [Trait("Requirement", "PRV-STA-11")]
    public async Task The_forwarded_close_delay_is_written_only_when_it_changes_and_is_not_reset_on_close()
    {
        PreviewCardStore<NoPayload> store = Store(defaultOpen: true);
        int writes = 0;
        using IDisposable subscription = store.Subscribe(_ => writes++);

        store.SetCloseDelay(100);
        store.SetCloseDelay(100);
        await Request(store, false, EventReason.TriggerHover);

        Assert.Equal(100, store.State.CloseDelay);
        Assert.True(writes >= 2);
        int afterClose = writes;
        store.SetCloseDelay(100);
        Assert.Equal(afterClose, writes);
    }

    [Theory(DisplayName = "port: a trigger's open delay falls back to 600 ms and its close delay to 300 ms")]
    [InlineData(null, null, 600, 300)]
    [InlineData(0, 0, 0, 0)]
    [InlineData(100, 50, 100, 50)]
    [Trait("Requirement", "PRV-API-10")]
    [Trait("Requirement", "PRV-API-11")]
    public void A_triggers_delays_fall_back_to_600_and_300(int? delay, int? closeDelay, int open, int close)
    {
        Assert.Equal(open, PreviewCardDelays.Open(delay is { } d ? d : null));
        Assert.Equal(close, PreviewCardDelays.Close(closeDelay is { } c ? c : null));
    }

    [Fact(DisplayName = "port: PreviewCardPositioner declares no KeepMounted parameter, which lives on PreviewCardPortal")]
    [Trait("Requirement", "PRV-API-19")]
    public void PreviewCardPositioner_declares_no_KeepMounted_parameter()
    {
        Assert.NotNull(typeof(PreviewCardPortal).GetProperty(nameof(PreviewCardPortal.KeepMounted))?.GetCustomAttribute<ParameterAttribute>());
        Assert.Null(typeof(PreviewCardPositioner).GetProperty("KeepMounted", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance));
    }

    [Fact(DisplayName = "port: the handle carries the payload type of its root and triggers")]
    [Trait("Requirement", "PRV-API-07")]
    [Trait("Requirement", "PRV-API-21")]
    public void The_handle_carries_the_payload_type_of_its_root_and_triggers()
    {
        Assert.Equal(typeof(PopupHandle<int>), typeof(PreviewCardHandle<int>).BaseType);
        Assert.Equal(typeof(PreviewCardHandle<int>), typeof(PreviewCardRoot<int>).GetProperty(nameof(PreviewCardRoot<int>.Handle))!.PropertyType);
        Assert.Equal(typeof(PreviewCardHandle<int>), typeof(PreviewCardTrigger<int>).GetProperty(nameof(PreviewCardTrigger<int>.Handle))!.PropertyType);
        Assert.Equal(typeof(int), typeof(PreviewCardTrigger<int>).GetProperty(nameof(PreviewCardTrigger<int>.Payload))!.PropertyType);
        Assert.Equal(typeof(RenderFragment<int>), typeof(PreviewCardRoot<int>).GetProperty(nameof(PreviewCardRoot<int>.PayloadContent))!.PropertyType);
        Assert.False(new PreviewCardHandle<int>().IsOpen);
    }
}

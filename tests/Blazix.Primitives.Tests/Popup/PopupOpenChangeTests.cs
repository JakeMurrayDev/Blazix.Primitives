using Microsoft.AspNetCore.Components;

// Family tests use the project's root namespace.
namespace Blazix.Primitives.Tests;

/// <summary>
/// The committed open state and the open-change sequence at L1 (utils/popups/popupStoreUtils.test.tsx, the
/// "getPopupOpenState" and "applyPopupOpenChange" blocks, titles verbatim; PSH-ALG-01/02, PSH-STA-12..14, PSH-EVT-01,
/// PSH-ANI-04, PSH-DIS-06), on the test-only family's store, which records the sequence's steps. The relay of the change
/// (ADR 04 Amendments 2026-10-27) stands for Base UI's dispatchOpenChange. Callbacks are created without a receiver, so they
/// run directly, as the store runs them on the renderer's context.
/// </summary>
public sealed class PopupOpenChangeTests
{
    private static PopupStoreState<NoPayload> Initial() => PopupStoreState<NoPayload>.Initial(null);

    private static PopupTriggerElement Button() => new(new object(), new ElementReference("trigger-element"));

    [Fact(DisplayName = "clears a previous unmount-prevention request when opening")]
    [Trait("Requirement", "PSH-STA-12")]
    public void Clears_a_previous_unmount_prevention_request_when_opening()
    {
        PopupStoreState<NoPayload> state = Initial() with { PreventUnmountingOnClose = true };

        PopupStoreState<NoPayload> next = TestFamilyStore<NoPayload>.CreateOpenState(state, open: true, trigger: null, triggerId: null, preventUnmountOnClose: false);

        Assert.False(next.PreventUnmountingOnClose);
        Assert.True(state.PreventUnmountingOnClose);
    }

    [Fact(DisplayName = "sets the unmount-prevention request when closing")]
    [Trait("Requirement", "PSH-STA-12")]
    public void Sets_the_unmount_prevention_request_when_closing()
    {
        PopupStoreState<NoPayload> next = TestFamilyStore<NoPayload>.CreateOpenState(Initial(), open: false, trigger: null, triggerId: null, preventUnmountOnClose: true);

        Assert.True(next.PreventUnmountingOnClose);
    }

    [Fact(DisplayName = "preserves the active trigger when closing without a trigger")]
    [Trait("Requirement", "PSH-STA-13")]
    public void Preserves_the_active_trigger_when_closing_without_a_trigger()
    {
        PopupTriggerElement trigger = Button();
        PopupStoreState<NoPayload> state = Initial() with { ActiveTriggerId = "trigger-id", ActiveTriggerElement = trigger };

        PopupStoreState<NoPayload> next = TestFamilyStore<NoPayload>.CreateOpenState(state, open: false, trigger: null, triggerId: null, preventUnmountOnClose: false);

        Assert.Equal("trigger-id", next.ActiveTriggerId);
        Assert.Equal(trigger, next.ActiveTriggerElement);
    }

    [Fact(DisplayName = "records whether an open request carried a trigger")]
    [Trait("Requirement", "PSH-STA-14")]
    public void Records_whether_an_open_request_carried_a_trigger()
    {
        PopupStoreState<NoPayload> state = Initial();
        PopupTriggerElement trigger = Button();

        Assert.True(TestFamilyStore<NoPayload>.CreateOpenState(state, open: true, trigger: null, triggerId: null, preventUnmountOnClose: false).OpenedWithoutTrigger);
        Assert.False(TestFamilyStore<NoPayload>.CreateOpenState(state, open: true, trigger, "trigger-id", preventUnmountOnClose: false).OpenedWithoutTrigger);
    }

    [Fact(DisplayName = "keeps the trigger-less open flag through a close request")]
    [Trait("Requirement", "PSH-STA-14")]
    public void Keeps_the_trigger_less_open_flag_through_a_close_request()
    {
        // A controlled root may decline the close and stay open; the root clears the flag once effectively closed.
        PopupStoreState<NoPayload> state = Initial() with { OpenedWithoutTrigger = true };

        Assert.True(TestFamilyStore<NoPayload>.CreateOpenState(state, open: false, trigger: null, triggerId: null, preventUnmountOnClose: false).OpenedWithoutTrigger);
    }

    [Fact(DisplayName = "runs the full sequence in order when the change is not canceled")]
    [Trait("Requirement", "PSH-ALG-01")]
    [Trait("Requirement", "PSH-EVT-01")]
    public async Task Runs_the_full_sequence_in_order_when_the_change_is_not_canceled()
    {
        TestFamilyStore<NoPayload> store = new();
        store.ApplyRootParameters(defaultOpen: false, defaultTriggerId: null);
        OpenChangeEventDetails details = new(EventReason.TriggerFocus);
        ValueChangeEventArgs<bool>? received = null;
        store.OnOpenChange = new EventCallback<ValueChangeEventArgs<bool>>(null, (Action<ValueChangeEventArgs<bool>>)(args =>
        {
            store.Order.Add("onOpenChange");
            received = args;
        }));
        store.Subscribe(_ => store.Order.Add("notified"));

        bool applied = await store.RequestOpenAsync(true, details);

        Assert.True(applied);
        Assert.True(received?.Value);
        Assert.Same(details, received?.Details);
        Assert.Equal(["onOpenChange", "onBeforeDispatch", "dispatchOpenChange", "update", "notified"], store.Order);
        Assert.Equal(new Blazix.Primitives.Interop.OpenChangeRelay(1, "trigger-focus", EventKind.Event, "base-ui", null, null), store.Popup.OpenChange);
        Assert.True(store.Popup.Open);
    }

    [Fact(DisplayName = "notifies onOpenChange but short-circuits before dispatch when canceled")]
    [Trait("Requirement", "PSH-DIS-06")]
    [Trait("Requirement", "PSH-EVT-01")]
    public async Task Notifies_onOpenChange_but_short_circuits_before_dispatch_when_canceled()
    {
        TestFamilyStore<NoPayload> store = new();
        store.ApplyRootParameters(defaultOpen: false, defaultTriggerId: null);
        int calls = 0;
        store.OnOpenChange = new EventCallback<ValueChangeEventArgs<bool>>(null, (Action<ValueChangeEventArgs<bool>>)(args =>
        {
            calls++;
            args.Details.Cancel();
        }));
        PopupStoreState<NoPayload> before = store.Popup;

        bool applied = await store.RequestOpenAsync(true, new OpenChangeEventDetails(EventReason.TriggerPress));

        Assert.False(applied);
        Assert.Equal(1, calls);
        Assert.Empty(store.Order);
        Assert.Same(before, store.Popup);
        Assert.Equal(0, store.OpenChangeRevision);
    }

    [Fact(DisplayName = "merges extraState into the update with `open` always reflecting nextOpen")]
    [Trait("Requirement", "PSH-EVT-01")]
    public async Task Merges_extraState_into_the_update_with_open_always_reflecting_nextOpen()
    {
        TestFamilyStore<NoPayload> store = new();
        store.ApplyRootParameters(defaultOpen: false, defaultTriggerId: null);

        await store.RequestOpenAsync(true, new OpenChangeEventDetails(EventReason.TriggerFocus));

        Assert.True(store.State.Popup.Open);
        Assert.Equal("trigger-focus", store.State.LastReason);
    }

    [Fact(DisplayName = "maps the change reason to instantType")]
    [Trait("Requirement", "PSH-ANI-04")]
    public void Maps_the_change_reason_to_instantType()
    {
        Assert.Equal(PopupInstantChange.Focus, TestFamilyStore<NoPayload>.InstantChange(true, EventReason.TriggerFocus));
        Assert.Equal(PopupInstantChange.Dismiss, TestFamilyStore<NoPayload>.InstantChange(false, EventReason.TriggerPress));
        Assert.Equal(PopupInstantChange.Dismiss, TestFamilyStore<NoPayload>.InstantChange(false, EventReason.EscapeKey));
        Assert.Equal(PopupInstantChange.Clear, TestFamilyStore<NoPayload>.InstantChange(true, EventReason.TriggerHover));
        Assert.Equal(PopupInstantChange.Unchanged, TestFamilyStore<NoPayload>.InstantChange(true, EventReason.None));
    }

    // ADR 05 §4, ADR 16 Amendments 2026-10-27: the payload lives in the store's context, so the record's comparison never
    // touches it; writing a value-type payload allocates the new record and nothing for the payload itself.
    [Fact(DisplayName = "port: a value-type payload is written without boxing and moves the payload version")]
    [Trait("Requirement", "ADR16-7")]
    public void A_value_type_payload_is_written_without_boxing_and_moves_the_payload_version()
    {
        TestFamilyStore<int> store = new();
        store.ApplyRootParameters(defaultOpen: false, defaultTriggerId: null);
        store.SetPayload(1);
        store.SetPayload(2);

        long before = GC.GetAllocatedBytesForCurrentThread();
        store.SetPayload(2);
        long unchanged = GC.GetAllocatedBytesForCurrentThread() - before;
        before = GC.GetAllocatedBytesForCurrentThread();
        store.SetPayload(3);
        long changed = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Equal(3, store.Payload);
        Assert.Equal(3, store.Popup.PayloadVersion);
        Assert.Equal(0, unchanged);
        // First measurement 2026-10-27 (arm64, .NET 11 RC2, Release): 216 bytes, the new shared record (its 19 members) and
        // the new family record; nothing for the int payload. The budget only tightens.
        Assert.True(changed <= 216, $"a payload write allocated {changed} bytes; the budget is 216");
    }
}

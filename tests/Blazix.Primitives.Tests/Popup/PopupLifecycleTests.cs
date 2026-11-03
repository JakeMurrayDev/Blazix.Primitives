using Blazix.Primitives.Interop;
using Microsoft.AspNetCore.Components;

// Family tests use the project's root namespace.
namespace Blazix.Primitives.Tests;

/// <summary>
/// The open lifecycle at L1 (ADR 05 Amendments 2026-11-03; FLT-STR-11, the port's standalone root store): the state's
/// derivations and equality, and the open-change sequence on a test-only record that holds the lifecycle alone. The
/// sequence's title is popupStoreUtils.test.tsx's, re-run on the lifecycle (PSH-ALG-01). Callbacks are created without a
/// receiver, so they run directly, as the store runs them on the renderer's context.
/// </summary>
public sealed class PopupLifecycleTests
{
    [Fact(DisplayName = "port: the lifecycle starts closed and unmounted, with the floating id and no committed change")]
    [Trait("Requirement", "FLT-STR-11")]
    public void The_lifecycle_starts_closed_and_unmounted_with_the_floating_id_and_no_committed_change()
    {
        PopupLifecycleState state = PopupLifecycleState.Initial("popup-1");

        Assert.False(state.Open);
        Assert.False(state.EffectiveOpen);
        Assert.False(state.Mounted);
        Assert.Null(state.TransitionStatus);
        Assert.Null(state.DerivedTransitionStatus);
        Assert.Equal("popup-1", state.FloatingId);
        Assert.Null(state.OpenChange);
        Assert.Null(state.OpenMethod);
        Assert.Equal(0, state.UnmountRequests);
    }

    [Fact(DisplayName = "port: the effective open state is the controlled value when controlled, else the internal one, and closed while disabled")]
    [Trait("Requirement", "PSH-STA-02")]
    [Trait("Requirement", "TIP-STA-02")]
    public void The_effective_open_state_is_the_controlled_value_when_controlled_else_the_internal_one_and_closed_while_disabled()
    {
        PopupLifecycleState state = PopupLifecycleState.Initial(null);

        Assert.True((state with { Open = true }).EffectiveOpen);
        Assert.False((state with { Open = true, OpenProp = false }).EffectiveOpen);
        Assert.True((state with { Open = false, OpenProp = true }).EffectiveOpen);
        Assert.False((state with { Open = true, Disabled = true }).EffectiveOpen);
    }

    [Fact(DisplayName = "port: the derived status is starting while effectively open and not yet mounted, else the stored one")]
    [Trait("Requirement", "PSH-STA-05")]
    [Trait("Requirement", "TRN-STA-14")]
    public void The_derived_status_is_starting_while_effectively_open_and_not_yet_mounted_else_the_stored_one()
    {
        PopupLifecycleState state = PopupLifecycleState.Initial(null);

        Assert.Equal(TransitionStatus.Starting, (state with { Open = true }).DerivedTransitionStatus);
        Assert.Equal(TransitionStatus.Ending, (state with { Mounted = true, TransitionStatus = TransitionStatus.Ending }).DerivedTransitionStatus);
        Assert.Equal(TransitionStatus.Idle, (state with { Open = true, Mounted = true, TransitionStatus = TransitionStatus.Idle }).DerivedTransitionStatus);
    }

    // The generated ShallowEquals of a record that holds the lifecycle compares it through this operator, so it must see
    // every member; the relay compares by reference, as the generator compares a record member.
    [Fact(DisplayName = "port: two lifecycle states are equal only when every member is")]
    [Trait("Requirement", "FLT-STR-11")]
    public void Two_lifecycle_states_are_equal_only_when_every_member_is()
    {
        PopupLifecycleState state = PopupLifecycleState.Initial("popup-1");
        OpenChangeRelay relay = new(1, "trigger-press", EventKind.Event, "click", "mouse", null);
        PopupLifecycleState[] changed =
        [
            state with { Open = true },
            state with { OpenProp = false },
            state with { Mounted = true },
            state with { TransitionStatus = TransitionStatus.Idle },
            state with { FloatingId = "popup-2" },
            state with { PreventUnmountingOnClose = true },
            state with { UnmountRequests = 1 },
            state with { OpenChange = relay },
            state with { OpenMethod = InteractionType.Mouse },
            state with { Disabled = true },
        ];

        Assert.True(state == PopupLifecycleState.Initial("popup-1"));
        Assert.True((state with { OpenChange = relay }) == (state with { OpenChange = relay }));
        Assert.False((state with { OpenChange = relay }) == (state with { OpenChange = relay with { } }));
        foreach (PopupLifecycleState other in changed)
        {
            Assert.True(state != other);
            Assert.False(state.Equals(other));
        }
    }

    [Fact(DisplayName = "runs the full sequence in order when the change is not canceled")]
    [Trait("Requirement", "PSH-ALG-01")]
    [Trait("Requirement", "PSH-EVT-01")]
    [Trait("Requirement", "FLT-STR-11")]
    public async Task Runs_the_full_sequence_in_order_when_the_change_is_not_canceled()
    {
        TestLifecycleStore store = new();
        store.ApplyOpenParameters(openProp: null, defaultOpen: false);
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
        Assert.Equal(new OpenChangeRelay(1, "trigger-focus", EventKind.Event, "base-ui", null, null), store.Lifecycle.OpenChange);
        Assert.True(store.Lifecycle.Open);
        Assert.Equal("trigger-focus", store.State.LastReason);
    }

    // FLT-EVT-02's order stays the port's for the families that compose the lifecycle (ADR 05 Amendments 2026-11-03): the
    // relay follows the commit, so a canceled change moves no revision and is never relayed.
    [Fact(DisplayName = "port: a canceled change writes nothing and moves no relay revision")]
    [Trait("Requirement", "FLT-EVT-02")]
    [Trait("Requirement", "PSH-DIS-06")]
    public async Task A_canceled_change_writes_nothing_and_moves_no_relay_revision()
    {
        TestLifecycleStore store = new();
        store.ApplyOpenParameters(openProp: null, defaultOpen: false);
        await store.RequestOpenAsync(true, new OpenChangeEventDetails(EventReason.TriggerPress));
        TestLifecycleStoreState before = store.State;
        store.Order.Clear();
        store.OnOpenChange = new EventCallback<ValueChangeEventArgs<bool>>(null, (Action<ValueChangeEventArgs<bool>>)(args => args.Details.Cancel()));

        bool applied = await store.RequestOpenAsync(false, new OpenChangeEventDetails(EventReason.EscapeKey));

        Assert.False(applied);
        Assert.Empty(store.Order);
        Assert.Same(before, store.State);
        Assert.Equal(1, store.OpenChangeRevision);
        Assert.Equal(1, store.Lifecycle.OpenChange?.Revision);
    }

    // NavigationMenu's constraint (ADR 05 Amendments 2026-11-03; ADR 08 Amendments 2026-11-03 (2)): a family whose open state
    // is derived from its own value runs its own consumer step, with cancel, and the lifecycle commits after it in ADR 05
    // §2's order (consumer, cancel, the family step, relay, one write) without its Controlled<bool>.
    [Fact(DisplayName = "port: a derived open state runs the family's own consumer step, then the lifecycle's commit")]
    [Trait("Requirement", "FLT-STR-11")]
    [Trait("Requirement", "PSH-ALG-01")]
    public async Task A_derived_open_state_runs_the_familys_own_consumer_step_then_the_lifecycles_commit()
    {
        TestDerivedLifecycleStore store = new() { RequestedValue = "item-1" };
        store.ApplyOpenParameters(openProp: null, defaultOpen: false);
        store.Subscribe(_ => store.Order.Add("notified"));

        bool applied = await store.RequestOpenAsync(true, new OpenChangeEventDetails(EventReason.TriggerPress));

        Assert.True(applied);
        Assert.Equal("item-1", store.Value);
        Assert.Equal(["onValueChange", "onBeforeDispatch", "dispatchOpenChange", "update", "notified"], store.Order);
        Assert.True(store.IsOpen);
        Assert.False(store.OpenState.Value);
        Assert.Equal(1, store.Lifecycle.OpenChange?.Revision);

        store.Order.Clear();
        store.OnValueChange = (_, _) => false;
        Assert.False(await store.RequestOpenAsync(false, new OpenChangeEventDetails(EventReason.OutsidePress)));
        Assert.Equal(["onValueChange"], store.Order);
        Assert.True(store.IsOpen);
        Assert.Equal("item-1", store.Value);
    }

    [Fact(DisplayName = "port: a commit after the family's own consumer step relays and writes once")]
    [Trait("Requirement", "FLT-STR-11")]
    public void A_commit_after_the_familys_own_consumer_step_relays_and_writes_once()
    {
        TestLifecycleStore store = new();
        store.ApplyOpenParameters(openProp: null, defaultOpen: false);
        int notifications = 0;
        store.Subscribe(_ => notifications++);

        store.CommitOpenChange(true, new OpenChangeEventDetails(EventReason.TriggerHover));

        Assert.Equal(1, notifications);
        Assert.Equal(["onBeforeDispatch", "dispatchOpenChange", "update"], store.Order);
        // Mounted and the status are evaluated at the write that flips the effective open state (TRN-ALG-05).
        Assert.True(store.IsOpen);
        Assert.True(store.Lifecycle.Mounted);
        Assert.Equal(TransitionStatus.Starting, store.Lifecycle.DerivedTransitionStatus);
        Assert.Equal(new OpenChangeRelay(1, "trigger-hover", EventKind.Event, "base-ui", null, null), store.Lifecycle.OpenChange);
    }

    // TRN-ANI-22's mechanism (useUnmountAfterClose.ts:102-116, useOpenChangeComplete.tsx:24-26): the unmount action while
    // the popup is closed and still animating out unmounts it at once; the exit's later completion runs no second callback.
    [Fact(DisplayName = "port: an unmount while closing unmounts in the same call and the later exit completion fires no second callback")]
    [Trait("Requirement", "TRN-ANI-22")]
    [Trait("Requirement", "TRN-EVT-08")]
    public async Task An_unmount_while_closing_unmounts_in_the_same_call_and_the_later_exit_completion_fires_no_second_callback()
    {
        TestLifecycleStore store = new();
        store.ApplyOpenParameters(openProp: null, defaultOpen: true);
        List<bool> completions = [];
        store.OnOpenChangeComplete = new EventCallback<bool>(null, (Action<bool>)completions.Add);
        await store.RequestOpenAsync(false, new OpenChangeEventDetails(EventReason.EscapeKey));
        store.MirrorStatus(TransitionStatus.Ending);
        Assert.True(store.Lifecycle.Mounted);

        Task unmounted = store.UnmountAsync();

        Assert.True(unmounted.IsCompleted);
        Assert.False(store.Lifecycle.Mounted);
        Assert.Null(store.Lifecycle.TransitionStatus);
        Assert.Equal(1, store.Lifecycle.UnmountRequests);
        Assert.Equal([false], completions);
        Assert.Contains("onUnmount", store.Order);

        await store.CompleteTransitionAsync(false);
        Assert.Equal([false], completions);
    }

    // PSH-PTR-02 and PSH-PTR-03's Root-level variant: the interaction type is written only while closed, and the close's
    // reset runs at the render-batch boundary.
    [Fact(DisplayName = "port: the open interaction type is written only while closed and is reset at the boundary after a close")]
    [Trait("Requirement", "PSH-PTR-02")]
    [Trait("Requirement", "PSH-PTR-03")]
    public async Task The_open_interaction_type_is_written_only_while_closed_and_is_reset_at_the_boundary_after_a_close()
    {
        TestLifecycleStore store = new();
        store.ApplyOpenParameters(openProp: null, defaultOpen: false);
        store.SetOpenInteractionType(InteractionType.Keyboard);
        await store.RequestOpenAsync(true, new OpenChangeEventDetails(EventReason.TriggerPress));
        store.SetOpenInteractionType(InteractionType.Mouse);
        Assert.Equal(InteractionType.Keyboard, store.Lifecycle.OpenMethod);

        await store.RequestOpenAsync(false, new OpenChangeEventDetails(EventReason.TriggerPress));
        Assert.Equal(InteractionType.Keyboard, store.Lifecycle.OpenMethod);
        await store.AtBatchBoundaryAsync();

        Assert.Null(store.Lifecycle.OpenMethod);
    }

    // PSH-PTR-03: the open interaction type resets only when the popup closes. A boundary before an open (an awaiting
    // consumer callback between the press and the commit, say) keeps the type the opening interaction recorded.
    [Fact(DisplayName = "port: the open interaction type recorded before an open survives a boundary before the open commits")]
    [Trait("Requirement", "PSH-PTR-02")]
    [Trait("Requirement", "PSH-PTR-03")]
    public async Task The_open_interaction_type_recorded_before_an_open_survives_a_boundary_before_the_open_commits()
    {
        TestLifecycleStore store = new();
        store.ApplyOpenParameters(openProp: null, defaultOpen: false);
        store.SetOpenInteractionType(InteractionType.Touch);

        await store.AtBatchBoundaryAsync();
        await store.RequestOpenAsync(true, new OpenChangeEventDetails(EventReason.TriggerPress));

        Assert.Equal(InteractionType.Touch, store.Lifecycle.OpenMethod);
    }

    // NavigationMenu's switch (PTR-07; ADR 04 Amendments 2026-10-27, the relay): a commit while already open moves the relay's
    // revision with the switch's reason and leaves the open, mounted and transition state as they were.
    [Fact(DisplayName = "port: a commit that keeps the popup open relays the next revision with its reason and changes nothing else of the lifecycle")]
    [Trait("Requirement", "FLT-STR-11")]
    [Trait("Requirement", "FLT-EVT-02")]
    public async Task A_commit_that_keeps_the_popup_open_relays_the_next_revision_with_its_reason_and_changes_nothing_else_of_the_lifecycle()
    {
        TestLifecycleStore store = new();
        store.ApplyOpenParameters(openProp: null, defaultOpen: false);
        await store.RequestOpenAsync(true, new OpenChangeEventDetails(EventReason.TriggerPress));
        store.MirrorStatus(TransitionStatus.Idle);
        PopupLifecycleState before = store.Lifecycle;

        store.CommitOpenChange(true, new OpenChangeEventDetails(EventReason.TriggerHover));

        Assert.Equal(new OpenChangeRelay(2, "trigger-hover", EventKind.Event, "base-ui", null, null), store.Lifecycle.OpenChange);
        Assert.True(store.Lifecycle == (before with { OpenChange = store.Lifecycle.OpenChange }));
    }

    // NavigationMenu's second exit watcher (the inline nested Root's Viewport target): two exit completions racing run the
    // unmount and its callback once.
    [Fact(DisplayName = "port: two exit completions racing unmount the popup once and complete once")]
    [Trait("Requirement", "TRN-ANI-22")]
    [Trait("Requirement", "TRN-EVT-01")]
    public async Task Two_exit_completions_racing_unmount_the_popup_once_and_complete_once()
    {
        TestLifecycleStore store = new();
        store.ApplyOpenParameters(openProp: null, defaultOpen: true);
        List<bool> completions = [];
        store.OnOpenChangeComplete = new EventCallback<bool>(null, (Action<bool>)completions.Add);
        await store.RequestOpenAsync(false, new OpenChangeEventDetails(EventReason.OutsidePress));

        await Task.WhenAll(store.CompleteTransitionAsync(false), store.CompleteTransitionAsync(false));

        Assert.False(store.Lifecycle.Mounted);
        Assert.Equal(1, store.Lifecycle.UnmountRequests);
        Assert.Equal([false], completions);
        Assert.Equal(1, store.Order.Count(step => step == "onUnmount"));
    }

    // ADR 16 rule 7 (UTL-STA-11): a lifecycle write that changes nothing is a no-op and allocates nothing, and so is a
    // mirrored status that did not change.
    [Fact(DisplayName = "port: an unchanged lifecycle write allocates nothing")]
    [Trait("Requirement", "ADR16-7")]
    public void An_unchanged_lifecycle_write_allocates_nothing()
    {
        TestLifecycleStore store = new();
        store.ApplyOpenParameters(openProp: null, defaultOpen: true);
        store.MirrorStatus(TransitionStatus.Idle);
        PopupLifecycleState equal = store.Lifecycle with { };
        TestLifecycleStoreState state = store.State;
        int notifications = 0;
        store.Subscribe(_ => notifications++);
        store.WriteLifecycle(equal);
        store.MirrorStatus(TransitionStatus.Idle);
        store.ApplyOpenParameters(openProp: null, defaultOpen: true);

        long before = GC.GetAllocatedBytesForCurrentThread();
        store.WriteLifecycle(equal);
        store.WriteLifecycle(store.Lifecycle);
        store.MirrorStatus(TransitionStatus.Idle);
        store.ApplyOpenParameters(openProp: null, defaultOpen: true);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Equal(0, allocated);
        Assert.Same(state, store.State);
        Assert.Equal(0, notifications);
    }
}

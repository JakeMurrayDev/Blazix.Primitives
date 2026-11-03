using Blazix.Primitives.Interop;
using Blazix.Primitives.Store;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace Blazix.Primitives;

// Base UI's popup store (utils/popups/store.ts, popupStoreUtils.ts), the PSH C# half (ADR 05 Amendments 2026-10-27, chosen
// by the user 2026-10-27). A family subclasses it for its record and its own steps of the open-change sequence. Its fields
// and properties are the store's non-reactive context (UTL-STA-15): the trigger registry, the controlled open and trigger
// id, the payload, the callbacks, the inert flag, the session token and the relay's revision.
// - The open-change sequence (PSH-ALG-01 = EVD-ALG-04, ADR 05 §2): the consumer's OnOpenChange first, a cancel honoured,
//   the family's pre-dispatch step, the relay (ADR 04 Amendments 2026-10-27), one write (PSH-ALG-02 = EVD-ALG-05).
// - Registration (PSH-ALG-04/05) by id at parameter set (settled by the orchestrator 2026-10-27 under ADR 01 Amendments
//   2026-10-22); the reconciliation (PSH-ALG-03) at the render-batch boundary (ADR 01 decision 3's amendment), brought by
//   the first popup part to render in the batch (ADR 16 Amendments 2026-10-27, design point 8): a part that selects the
//   open or mounted state registers as a boundary part, and while none is registered the store asks the root to render on
//   the reconciliation's inputs (BoundaryRequested).
// - The transitions plumbing (TRN-ALG-05, PSH-STA-44/45, TRN-ANI-19..27): mounted and the status evaluated at the write
//   that changes the effective open state, as useTransitionStatus does at render; the status mirrored from the popup's
//   `transitions` handle; the unmount cleanup in Base UI's order.
// - The inert fallback store a handle exposes while no root is attached (ADR 05 Amendments 2026-10-27): IsInert skips
//   every write; the registry stays live (UTL's NullStore as a flag; no Store/ edit).
// - M5 track 2 (ADR 05 Amendments 2026-11-03, chosen by the user 2026-11-03, and 2026-11-04, design point 1 (a2), settled by
//   the orchestrator 2026-11-04): the open lifecycle (the controlled open, the sequence and its commit, the relay and its
//   revision, the transitions plumbing, the unmount, issue 148's announcement, the boundary parts) is the base class's,
//   PopupLifecycleStore<TState>; this class keeps the trigger registry, the trigger id, the payload, the handles' interface,
//   SetDisabled and the reconciliation, and joins them to the base's commit, unmount and publication in the same writes.
/// <summary>Holds the state of one popup, its trigger registry and its payload, and runs its open-change sequence.</summary>
/// <typeparam name="TState">The type of the family's store record.</typeparam>
/// <typeparam name="TPayload">The type of the payload the popup's triggers carry.</typeparam>
internal class PopupStore<TState, TPayload> : PopupLifecycleStore<TState>, IPopupStore<TPayload>
    where TState : class, IStoreState<TState>, IPopupStoreState<TState, TPayload>
{
    private readonly PopupTriggerRegistry triggers = new();
    private readonly Controlled<string?> triggerId;
    private TPayload? payload;
    private bool hasPayload;
    private string? resolvedActiveTriggerId;
    private bool reconciliationPending;
    private bool reconciling;
    private bool rootApplied;

    /// <summary>Initializes a new instance of the <see cref="PopupStore{TState, TPayload}"/> class.</summary>
    /// <param name="state">The family's initial record.</param>
    /// <param name="componentName">The family's name in diagnostics, such as <c>Popover</c>.</param>
    /// <param name="sessionToken">The session token the root drew, which the browser-side behaviors key the popup's session by.</param>
    /// <param name="inert">Whether this is a handle's fallback store, whose writes are skipped.</param>
    /// <param name="logger">The root's logger, or <see langword="null"/>.</param>
    public PopupStore(TState state, string componentName, string? sessionToken, bool inert = false, ILogger? logger = null)
        : base(state, componentName, sessionToken, inert, logger)
    {
        triggerId = new Controlled<string?>(componentName, "triggerId", logger);
    }

    /// <inheritdoc />
    public PopupTriggerRegistry Triggers => triggers;

    /// <summary>Gets the shared popup state of the current record.</summary>
    public PopupStoreState<TPayload> Popup => State.Popup;

    /// <summary>Gets the payload the popup renders: the active trigger's, or the imperative one.</summary>
    public TPayload? Payload => payload;

    /// <summary>Gets a value indicating whether a payload was written.</summary>
    public bool HasPayload => hasPayload;

    /// <summary>Gets the root's controlled-or-uncontrolled active trigger id.</summary>
    public Controlled<string?> TriggerIdState => triggerId;

    // PSH §6.1: Tooltip and PreviewCard set it (PSH-STA-23).
    /// <summary>Gets or sets a value indicating whether the popup closes when its resolved active trigger unregisters.</summary>
    public bool CloseOnActiveTriggerUnmount { get; set; }

    /// <summary>Gets a value indicating whether a write changed the reconciliation's inputs since it last ran.</summary>
    public bool ReconciliationPending => reconciliationPending;

    // EVD-ALG-05 (createPopupOpenState, popupStoreUtils.ts:189-226): the committed open state of a request.
    /// <summary>Derives the shared state a committed open change writes.</summary>
    /// <param name="state">The current shared state.</param>
    /// <param name="open">The requested open state.</param>
    /// <param name="trigger">The trigger the request carries, or <see langword="null"/>.</param>
    /// <param name="triggerId">The id of that trigger, or <see langword="null"/> when it has none.</param>
    /// <param name="preventUnmountOnClose">Whether the request's callback called <c>PreventUnmountOnClose()</c>.</param>
    /// <returns>The state with the open state, the unmount opt-out, the active trigger and the trigger-less flag applied.</returns>
    public static PopupStoreState<TPayload> CreateOpenState(PopupStoreState<TPayload> state, bool open, PopupTriggerElement? trigger, string? triggerId, bool preventUnmountOnClose)
    {
        ArgumentNullException.ThrowIfNull(state);
        return WithTriggerState(state, open, trigger, triggerId) with { Lifecycle = CreateOpenState(state.Lifecycle, open, preventUnmountOnClose) };
    }

    // EVD-EVT-17, PSH-ANI-04 (popupStoreUtils.ts:261-304): the shared stores' instant phase. A family maps the change to its
    // own enum (TooltipInstantType, PreviewCardInstantType); Popover's own sequence differs (POP).
    /// <summary>Gets how a committed open change moves the shared stores' instant phase.</summary>
    /// <param name="open">The committed open state.</param>
    /// <param name="reason">The change's reason.</param>
    /// <returns>The change to apply to the instant phase.</returns>
    public static PopupInstantChange InstantChange(bool open, EventReason reason)
    {
        if (open && reason == EventReason.TriggerFocus)
        {
            return PopupInstantChange.Focus;
        }

        if (!open && reason is EventReason.TriggerPress or EventReason.EscapeKey)
        {
            return PopupInstantChange.Dismiss;
        }

        return reason == EventReason.TriggerHover ? PopupInstantChange.Clear : PopupInstantChange.Unchanged;
    }

    // PSH-STA-01, PSH-STA-44 (popupStoreUtils.ts:598-605); ADR 08 Amendments 2026-10-27 (Open/DefaultOpen and TriggerId/
    // DefaultTriggerId, controlled iff supplied at the first parameter set). The root calls it after setting
    // OpenState and TriggerIdState from its parameters.
    /// <summary>Applies the root's open and trigger-id parameters to the record.</summary>
    /// <param name="defaultOpen">The root's <c>DefaultOpen</c>.</param>
    /// <param name="defaultTriggerId">The root's <c>DefaultTriggerId</c>.</param>
    public void ApplyRootParameters(bool defaultOpen, string? defaultTriggerId)
    {
        bool? openProp = OpenState.IsControlled ? OpenState.Value : null;
        bool triggerIdControlled = triggerId.IsControlled;
        string? triggerIdProp = triggerIdControlled ? triggerId.Value : null;
        PopupStoreState<TPayload> current = State.Popup;
        if (!rootApplied)
        {
            rootApplied = true;
            // The store is seeded before any part subscribes, so an initially open root is mounted with no enter
            // transition (PSH-ANI-02, TRN-STA-02); a root disabled from its first render is not (TIP-STA-02, TIP-EDGE-10).
            bool initialOpen = PopupStoreState<TPayload>.IsEffectivelyOpen(current.Disabled, openProp, defaultOpen);
            (bool mounted, TransitionStatus? status) = TransitionStatusRules.Initial(initialOpen, enableIdleState: false, animateInitialOpen: false);
            SetState(TState.WithPopup(State, current with
            {
                Lifecycle = current.Lifecycle with { Open = defaultOpen, OpenProp = openProp, Mounted = mounted, TransitionStatus = status },
                ActiveTriggerId = defaultTriggerId,
                TriggerIdControlled = triggerIdControlled,
                TriggerIdProp = triggerIdProp,
            }));
            return;
        }

        if (current.OpenProp == openProp && current.TriggerIdControlled == triggerIdControlled && string.Equals(current.TriggerIdProp, triggerIdProp, StringComparison.Ordinal))
        {
            return;
        }

        WritePopup(current with { OpenProp = openProp, TriggerIdControlled = triggerIdControlled, TriggerIdProp = triggerIdProp });
    }

    // PSH-ALG-04 (popupStoreUtils.ts:117-178): register(element) with a non-null element.
    /// <summary>Registers a trigger element under its id and keeps the trigger count while open.</summary>
    /// <param name="id">The trigger id.</param>
    /// <param name="element">The trigger element.</param>
    public void RegisterTrigger(string id, PopupTriggerElement element)
    {
        ArgumentNullException.ThrowIfNull(id);
        triggers.Add(id, element);
        SyncTriggerCount();
    }

    /// <summary>Unregisters a trigger element, only while the registry still maps <paramref name="id"/> to it.</summary>
    /// <param name="id">The trigger id.</param>
    /// <param name="element">The trigger element that registered.</param>
    /// <param name="disposing">Whether the trigger is being disposed, so no render of the trigger's will reach the batch boundary.</param>
    public void UnregisterTrigger(string id, PopupTriggerElement element, bool disposing = false)
    {
        ArgumentNullException.ThrowIfNull(id);
        if (triggers.TryGetById(id, out PopupTriggerElement registered) && registered == element)
        {
            triggers.Delete(id);
            SyncTriggerCount();
            if (disposing && reconciliationPending)
            {
                RequestBoundary();
            }
        }
    }

    // PSH-ALG-05 (popupStoreUtils.ts:323-394), PSH-STA-28/29.
    /// <summary>Applies a registering trigger's ownership and forwarded state.</summary>
    /// <param name="id">The trigger id.</param>
    /// <param name="element">The trigger element.</param>
    /// <param name="payloadSupplied">Whether the trigger supplies a payload.</param>
    /// <param name="triggerPayload">The trigger's payload.</param>
    /// <param name="forward">The family's forwarded fields, applied with the payload, or <see langword="null"/>.</param>
    public void ApplyTriggerData(string id, PopupTriggerElement element, bool payloadSupplied, TPayload? triggerPayload, Func<TState, TState>? forward = null)
    {
        PopupStoreState<TPayload> state = State.Popup;
        string? activeId = state.EffectiveActiveTriggerId;
        bool isOpen = state.EffectiveOpen;
        if (string.Equals(activeId, id, StringComparison.Ordinal))
        {
            if (isOpen)
            {
                ForwardPayload(payloadSupplied, triggerPayload);
                if (forward is not null)
                {
                    Write(forward(State));
                }
            }

            if (State.Popup.ActiveTriggerElement != element)
            {
                WritePopup(State.Popup with { ActiveTriggerElement = element });
            }

            return;
        }

        if (activeId is null && isOpen && !state.OpenedWithoutTrigger)
        {
            // A trigger that registers into a popup already open (a controlled or default open) claims it; one opened
            // deliberately without a trigger stays unassociated (PSH-STA-19).
            ForwardPayload(payloadSupplied, triggerPayload);
            if (forward is not null)
            {
                Write(forward(State));
            }

            WritePopup(State.Popup with { ActiveTriggerId = id, ActiveTriggerElement = element });
        }
    }

    // PSH-ALG-05's second half, PSH-STA-30: while the trigger owns the mounted popup, a change of its forwarded values is
    // written post-commit with its current element.
    /// <summary>Writes a mounted-by-trigger trigger's forwarded state after a change of its values.</summary>
    /// <param name="element">The trigger element.</param>
    /// <param name="payloadSupplied">Whether the trigger supplies a payload.</param>
    /// <param name="triggerPayload">The trigger's payload.</param>
    /// <param name="forward">The family's forwarded fields, applied with the payload, or <see langword="null"/>.</param>
    public void ForwardTriggerData(PopupTriggerElement element, bool payloadSupplied, TPayload? triggerPayload, Func<TState, TState>? forward = null)
    {
        ForwardPayload(payloadSupplied, triggerPayload);
        if (forward is not null)
        {
            Write(forward(State));
        }

        if (State.Popup.ActiveTriggerElement != element)
        {
            WritePopup(State.Popup with { ActiveTriggerElement = element });
        }
    }

    /// <inheritdoc />
    public void SetPayload(TPayload? payload)
    {
        if (IsInert || (hasPayload && Equality.Is(this.payload, payload)))
        {
            return;
        }

        this.payload = payload;
        hasPayload = true;
        WritePopup(State.Popup with { PayloadVersion = State.Popup.PayloadVersion + 1 });
    }

    /// <summary>Clears the payload, as a trigger without one does when it becomes active.</summary>
    public void ClearPayload()
    {
        if (IsInert || !hasPayload)
        {
            return;
        }

        payload = default;
        hasPayload = false;
        WritePopup(State.Popup with { PayloadVersion = State.Popup.PayloadVersion + 1 });
    }

    // TIP-STA-02, TIP-STA-16 (useSyncedValues: the root's `disabled`, post-commit); design point 13.7 of M4 track 6
    // (settled by the orchestrator 2026-10-28). The write that flips the effective open state runs the transition evaluation, so disabling an
    // open popup starts its exit; the stored open state stays for the family's disabled close (TIP-DIS-06).
    /// <summary>Records whether the popup's root is disabled, which keeps the popup effectively closed.</summary>
    /// <param name="disabled">Whether the root is disabled.</param>
    public void SetDisabled(bool disabled)
    {
        PopupStoreState<TPayload> state = State.Popup;
        if (state.Disabled != disabled)
        {
            WritePopup(state with { Disabled = disabled });
        }
    }

    // PSH-STA-04.
    /// <summary>Records the popup element and the id it renders.</summary>
    /// <param name="element">The popup element, or <see langword="null"/> before its first render.</param>
    /// <param name="id">The id the popup element renders, or <see langword="null"/> for none.</param>
    public void SetPopupElement(ElementReference? element, string? id)
    {
        PopupStoreState<TPayload> state = State.Popup;
        if (state.PopupElementKnown && SameElement(state.PopupElement, element) && string.Equals(state.PopupElementId, id, StringComparison.Ordinal))
        {
            return;
        }

        WritePopup(state with { PopupElementKnown = true, PopupElement = element, PopupElementId = id });
    }

    /// <summary>Forgets the popup element, when the popup part leaves the document.</summary>
    public void ClearPopupElement()
    {
        PopupStoreState<TPayload> state = State.Popup;
        if (state.PopupElementKnown)
        {
            WritePopup(state with { PopupElementKnown = false, PopupElement = null, PopupElementId = null });
        }
    }

    /// <summary>Records the positioner element.</summary>
    /// <param name="element">The positioner element, or <see langword="null"/>.</param>
    public void SetPositionerElement(ElementReference? element)
    {
        PopupStoreState<TPayload> state = State.Popup;
        if (!SameElement(state.PositionerElement, element))
        {
            WritePopup(state with { PositionerElement = element });
        }
    }

    // The render-batch boundary (ADR 01 decision 3's amendment): the reconciliation of PSH-ALG-03 when its inputs changed
    // (the open method's reset included), then a pending unmount action. Called from the OnAfterRender of the popup parts'
    // behaviors.
    /// <inheritdoc />
    /// <remarks>The reconciliation runs when a write changed its inputs, before a pending unmount action.</remarks>
    public override Task AtBatchBoundaryAsync()
    {
        MarkBatchBoundary();
        Task reconciled = reconciliationPending ? ReconcileAsync() : Task.CompletedTask;
        Task unmounted = RunPendingUnmountAsync();
        return reconciled.IsCompletedSuccessfully ? unmounted : Task.WhenAll(reconciled, unmounted);
    }

    // PSH-ALG-03 (useImplicitActiveTrigger, popupStoreUtils.ts:423-555), PSH-STA-16/18..25, PSH-STA-41. resolvedActiveTriggerId
    // persists across runs and names the active id last seen registered. The microtask of PSH-STA-23 is the batch boundary
    // this runs at (ADR 01 decision 3's amendment, the PSH-STA-23 row): every parameter set of the batch has run, so a
    // same-batch replacement with the lost id has registered already.
    /// <summary>Reconciles the active trigger with the registry, as the root does after each commit.</summary>
    /// <returns>A task that completes when a close requested for a lost active trigger has run.</returns>
    public Task ReconcileAsync()
    {
        reconciliationPending = false;
        PopupStoreState<TPayload> state = State.Popup;
        if (!state.EffectiveOpen)
        {
            resolvedActiveTriggerId = null;
            if (state.TriggerCount != 0 || state.OpenedWithoutTrigger || state.OpenMethod is not null)
            {
                WriteReconciled(state with { TriggerCount = 0, OpenedWithoutTrigger = false, OpenMethod = null });
            }

            return Task.CompletedTask;
        }

        int triggerCount = triggers.Count;
        PopupStoreState<TPayload> next = state.TriggerCount != triggerCount ? state with { TriggerCount = triggerCount } : state;
        string? currentId = state.EffectiveActiveTriggerId;
        string? lostId = null;
        bool claimed = false;
        if (!string.IsNullOrEmpty(currentId))
        {
            if (!triggers.TryGetById(currentId, out PopupTriggerElement registered))
            {
                foreach (KeyValuePair<string, PopupTriggerElement> entry in triggers.Entries)
                {
                    if (state.ActiveTriggerElement is { } activeElement && entry.Value == activeElement)
                    {
                        next = next with { ActiveTriggerId = entry.Key, ActiveTriggerElement = entry.Value };
                        resolvedActiveTriggerId = entry.Key;
                        claimed = true;
                        break;
                    }
                }

                if (!claimed)
                {
                    if (string.Equals(resolvedActiveTriggerId, currentId, StringComparison.Ordinal))
                    {
                        lostId = currentId;
                    }
                    else
                    {
                        resolvedActiveTriggerId = null;
                    }
                }
            }
            else
            {
                resolvedActiveTriggerId = currentId;
                if (state.ActiveTriggerElement != registered)
                {
                    next = next with { ActiveTriggerElement = registered };
                }
            }
        }
        else
        {
            resolvedActiveTriggerId = null;
        }

        if (lostId is null && string.IsNullOrEmpty(currentId) && !state.OpenedWithoutTrigger && triggerCount == 1)
        {
            KeyValuePair<string, PopupTriggerElement> only = triggers.Entries[0];
            next = next with { ActiveTriggerId = only.Key, ActiveTriggerElement = only.Value };
            resolvedActiveTriggerId = only.Key;
        }

        if (!ReferenceEquals(next, state))
        {
            WriteReconciled(next);
        }

        return lostId is not null && CloseOnActiveTriggerUnmount ? CloseForLostTriggerAsync(lostId) : Task.CompletedTask;
    }

    /// <summary>Writes a new shared state, unless it equals the current one member by member.</summary>
    /// <param name="next">The shared state to write.</param>
    protected internal void WritePopup(PopupStoreState<TPayload> next)
    {
        ArgumentNullException.ThrowIfNull(next);

        // ADR 16 rule 7: an equal record changes nothing, so no family record is built for it.
        PopupStoreState<TPayload> current = State.Popup;
        if (ReferenceEquals(current, next) || PopupStoreState<TPayload>.ShallowEquals(current, next))
        {
            return;
        }

        Write(TState.WithPopup(State, next));
    }

    /// <inheritdoc />
    private protected override TState WithCommittedOpen(TState state, TState applied, in PopupLifecycleState lifecycle, bool open, OpenChangeEventDetails details)
    {
        // The family's step writes only its own fields; the shared state of the commit is derived from the state before it.
        PopupTriggerElement? trigger = ResolveTrigger(details.Trigger);
        PopupStoreState<TPayload> popup = WithTriggerState(state.Popup, open, trigger, details.Trigger?.Id) with { Lifecycle = lifecycle };
        return TState.WithPopup(applied, popup);
    }

    // PSH-STA-45, TRN-ANI-27 (popupStoreUtils.ts:586-595): the unmount clears the active trigger in the lifecycle's write.
    /// <inheritdoc />
    private protected override TState WithUnmounted(TState state, in PopupLifecycleState lifecycle) =>
        TState.WithPopup(state, state.Popup with { Lifecycle = lifecycle, ActiveTriggerId = null, ActiveTriggerElement = null });

    /// <inheritdoc />
    private protected override void PublishWrite(TState current, TState next)
    {
        // The nested record compares by reference in the family's ShallowEquals, so it is compared here member by member
        // first, keeping a write that changes nothing a no-op (UTL-STA-11).
        if (ReferenceEquals(current.Popup, next.Popup) || PopupStoreState<TPayload>.ShallowEquals(current.Popup, next.Popup))
        {
            if (!ReferenceEquals(current.Popup, next.Popup))
            {
                next = TState.WithPopup(next, current.Popup);
            }

            if (ReferenceEquals(current, next) || TState.ShallowEquals(current, next))
            {
                return;
            }
        }
        else if (TrackReconciliationInputs(current.Popup, next.Popup) && !HasBoundaryPart && !reconciling)
        {
            // Design point 8's fallback (ADR 16 Amendments 2026-10-27): with no popup part to bring the boundary, the root
            // renders on the reconciliation's inputs, once the new state is published.
            SetState(next);
            RequestBoundary();
            return;
        }

        SetState(next);
    }

    // EVD-ALG-05's trigger half: the active trigger and the trigger-less flag of a committed open change.
    private static PopupStoreState<TPayload> WithTriggerState(PopupStoreState<TPayload> state, bool open, PopupTriggerElement? trigger, string? triggerId)
    {
        string? activeTriggerId = state.ActiveTriggerId;
        PopupTriggerElement? activeTriggerElement = state.ActiveTriggerElement;
        // A close without a trigger keeps the previous one, so exit animations and focus return still target it.
        if (!string.IsNullOrEmpty(triggerId) || open)
        {
            activeTriggerId = string.IsNullOrEmpty(triggerId) ? null : triggerId;
            activeTriggerElement = trigger;
        }

        return state with
        {
            ActiveTriggerId = activeTriggerId,
            ActiveTriggerElement = activeTriggerElement,
            OpenedWithoutTrigger = open ? trigger is null : state.OpenedWithoutTrigger,
        };
    }

    private static bool SameElement(ElementReference? left, ElementReference? right)
    {
        return left is { } l
            ? right is { } r && string.Equals(l.Id, r.Id, StringComparison.Ordinal) && ReferenceEquals(l.Context, r.Context)
            : right is null;
    }

    // syncTriggerCount (popupStoreUtils.ts:117-122): no write while closed (PSH-STA-17).
    private void SyncTriggerCount()
    {
        PopupStoreState<TPayload> state = State.Popup;
        if (state.EffectiveOpen && state.TriggerCount != triggers.Count)
        {
            WritePopup(state with { TriggerCount = triggers.Count });
        }
    }

    private bool TrackReconciliationInputs(PopupStoreState<TPayload> current, PopupStoreState<TPayload> next)
    {
        if (current.EffectiveOpen != next.EffectiveOpen
            || current.TriggerCount != next.TriggerCount
            || !string.Equals(current.EffectiveActiveTriggerId, next.EffectiveActiveTriggerId, StringComparison.Ordinal)
            || current.ActiveTriggerElement != next.ActiveTriggerElement)
        {
            reconciliationPending = true;
            return true;
        }

        return false;
    }

    private void ForwardPayload(bool payloadSupplied, TPayload? triggerPayload)
    {
        if (payloadSupplied)
        {
            SetPayload(triggerPayload);
        }
        else
        {
            ClearPayload();
        }
    }

    // The registration that a request's trigger names: the registered element under its id, else the element itself as an
    // unregistered one (a focus guard, say), which the registry never matches.
    private PopupTriggerElement? ResolveTrigger(EventTrigger? trigger)
    {
        if (trigger is null)
        {
            return null;
        }

        return triggers.TryGetById(trigger.Id, out PopupTriggerElement registered) ? registered : new PopupTriggerElement(trigger, trigger.Element);
    }

    // The reconciliation's own result needs no further boundary: running it again on that state changes nothing.
    private void WriteReconciled(PopupStoreState<TPayload> next)
    {
        reconciling = true;
        try
        {
            WritePopup(next);
        }
        finally
        {
            reconciling = false;
            reconciliationPending = false;
        }
    }

    // PSH-STA-23/25, PSH-DIS-05: a close with reason none and no event; a declined close keeps the ownership.
    private async Task CloseForLostTriggerAsync(string lostId)
    {
        PopupStoreState<TPayload> state = State.Popup;
        if (!state.EffectiveOpen || !string.Equals(state.EffectiveActiveTriggerId, lostId, StringComparison.Ordinal) || triggers.TryGetById(lostId, out _))
        {
            return;
        }

        if (await RequestOpenAsync(false, new OpenChangeEventDetails(EventReason.None)))
        {
            WritePopup(State.Popup with { ActiveTriggerId = null, ActiveTriggerElement = null });
        }
    }
}

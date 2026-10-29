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
/// <summary>Holds the state of one popup, its trigger registry and its payload, and runs its open-change sequence.</summary>
/// <typeparam name="TState">The type of the family's store record.</typeparam>
/// <typeparam name="TPayload">The type of the payload the popup's triggers carry.</typeparam>
internal class PopupStore<TState, TPayload> : ComponentStore<TState>, IPopupStore<TPayload>
    where TState : class, IStoreState<TState>, IPopupStoreState<TState, TPayload>
{
    private readonly PopupTriggerRegistry triggers = new();
    private readonly Controlled<bool> open;
    private readonly Controlled<string?> triggerId;
    private readonly List<Action<bool>> unmountPreventionListeners = [];
    private TPayload? payload;
    private bool hasPayload;
    private int openChangeRevision;
    private int boundaryRevision;
    private bool lastChangeClosed;
    private string? resolvedActiveTriggerId;
    private bool reconciliationPending;
    private bool unmountPending;
    private int boundaryParts;
    private bool reconciling;
    private bool rootApplied;

    /// <summary>Occurs when the store needs a render-batch boundary that no part's render will bring: the root renders.</summary>
    public event Action? BoundaryRequested;

    /// <summary>Initializes a new instance of the <see cref="PopupStore{TState, TPayload}"/> class.</summary>
    /// <param name="state">The family's initial record.</param>
    /// <param name="componentName">The family's name in diagnostics, such as <c>Popover</c>.</param>
    /// <param name="sessionToken">The session token the root drew, which the browser-side behaviors key the popup's session by.</param>
    /// <param name="inert">Whether this is a handle's fallback store, whose writes are skipped.</param>
    /// <param name="logger">The root's logger, or <see langword="null"/>.</param>
    public PopupStore(TState state, string componentName, string? sessionToken, bool inert = false, ILogger? logger = null)
        : base(state)
    {
        ArgumentNullException.ThrowIfNull(componentName);
        ComponentName = componentName;
        SessionToken = sessionToken;
        IsInert = inert;
        Logger = logger;
        open = new Controlled<bool>(componentName, "open", logger);
        triggerId = new Controlled<string?>(componentName, "triggerId", logger);
    }

    /// <summary>Gets the family's name in diagnostics.</summary>
    public string ComponentName { get; }

    /// <summary>Gets the session token of the popup, never rendered; <see langword="null"/> for a fallback store.</summary>
    public string? SessionToken { get; }

    /// <summary>Gets a value indicating whether this is a handle's fallback store, whose writes are skipped.</summary>
    public bool IsInert { get; }

    /// <inheritdoc />
    public PopupTriggerRegistry Triggers => triggers;

    /// <summary>Gets the shared popup state of the current record.</summary>
    public PopupStoreState<TPayload> Popup => State.Popup;

    /// <inheritdoc />
    public bool IsOpen => State.Popup.EffectiveOpen;

    /// <summary>Gets the payload the popup renders: the active trigger's, or the imperative one.</summary>
    public TPayload? Payload => payload;

    /// <summary>Gets a value indicating whether a payload was written.</summary>
    public bool HasPayload => hasPayload;

    /// <summary>Gets the root's controlled-or-uncontrolled open state.</summary>
    public Controlled<bool> OpenState => open;

    /// <summary>Gets the root's controlled-or-uncontrolled active trigger id.</summary>
    public Controlled<string?> TriggerIdState => triggerId;

    /// <summary>Gets or sets the consumer's open-change callback, called before a change is applied.</summary>
    public EventCallback<ValueChangeEventArgs<bool>> OnOpenChange { get; set; }

    /// <summary>Gets or sets the <c>@bind-Open</c> callback.</summary>
    public EventCallback<bool> OpenChanged { get; set; }

    /// <summary>Gets or sets the callback called once each enter or exit transition finishes.</summary>
    public EventCallback<bool> OnOpenChangeComplete { get; set; }

    // PSH §6.1: Tooltip and PreviewCard set it (PSH-STA-23).
    /// <summary>Gets or sets a value indicating whether the popup closes when its resolved active trigger unregisters.</summary>
    public bool CloseOnActiveTriggerUnmount { get; set; }

    // ADR 05 Amendments 2026-10-27: Dialog's rule (DLG-EVT-05, TRN-EVT-07) writes the flag the moment
    // PreventUnmountOnClose() is called, even when the same callback then cancels; the Dialog track sets this seam and
    // documents it. The shared stores read the call after the callback (PSH-STA-12, TRN-ANI-25).
    /// <summary>Gets or sets a value indicating whether an unmount opt-out survives the cancellation of its own close.</summary>
    public bool UnmountPreventionAtCall { get; set; }

    /// <summary>Gets a value indicating whether a write changed the reconciliation's inputs since it last ran.</summary>
    public bool ReconciliationPending => reconciliationPending;

    /// <summary>Gets a value indicating whether a popup part that brings the render-batch boundary is registered.</summary>
    public bool HasBoundaryPart => boundaryParts > 0;

    /// <summary>Gets the revision of the last committed open change.</summary>
    public int OpenChangeRevision => openChangeRevision;

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
        bool preventUnmounting = state.PreventUnmountingOnClose;
        if (open)
        {
            // Opening starts a new close cycle.
            preventUnmounting = false;
        }
        else if (preventUnmountOnClose)
        {
            preventUnmounting = true;
        }

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
            Open = open,
            PreventUnmountingOnClose = preventUnmounting,
            ActiveTriggerId = activeTriggerId,
            ActiveTriggerElement = activeTriggerElement,
            OpenedWithoutTrigger = open ? trigger is null : state.OpenedWithoutTrigger,
        };
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
        bool? openProp = open.IsControlled ? open.Value : null;
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
                Open = defaultOpen,
                OpenProp = openProp,
                ActiveTriggerId = defaultTriggerId,
                TriggerIdControlled = triggerIdControlled,
                TriggerIdProp = triggerIdProp,
                Mounted = mounted,
                TransitionStatus = status,
            }));
            return;
        }

        if (current.OpenProp == openProp && current.TriggerIdControlled == triggerIdControlled && string.Equals(current.TriggerIdProp, triggerIdProp, StringComparison.Ordinal))
        {
            return;
        }

        WritePopup(current with { OpenProp = openProp, TriggerIdControlled = triggerIdControlled, TriggerIdProp = triggerIdProp });
    }

    /// <inheritdoc />
    /// <remarks>
    /// The consumer's <see cref="OnOpenChange"/> runs first; a canceled request stops there. Then
    /// <see cref="OpenChanged"/> runs, the family's <see cref="BeforeDispatch"/> step, the relay of the change, and one
    /// write of the committed state.
    /// </remarks>
    public async Task<bool> RequestOpenAsync(bool open, OpenChangeEventDetails details)
    {
        ArgumentNullException.ThrowIfNull(details);
        if (IsInert)
        {
            return false;
        }

        PrepareOpenChange(open, details);

        // Issue 148 (issue 147's (B1), chosen by the user 2026-10-29, ADR 04 Amendments 2026-10-29): a close's opt-out is
        // announced at the PreventUnmountOnClose() call, before any render the consumer's callback makes; a close the
        // callback then cancels sends the reset, except under Dialog's call-time opt-out, which keeps it for the next close.
        bool announced = false;
        if (!open)
        {
            details.UnmountPreventionCalled = () =>
            {
                announced = true;
                RaiseUnmountPreventionChanging(true);
            };
        }

        bool applied;
        try
        {
            applied = await this.open.RequestAsync(open, details, OnOpenChange, OpenChanged);
        }
        finally
        {
            details.UnmountPreventionCalled = null;
        }

        if (!applied)
        {
            if (UnmountPreventionAtCall && !open && details.UnmountOnClosePrevented && !State.Popup.PreventUnmountingOnClose)
            {
                WritePopup(State.Popup with { PreventUnmountingOnClose = true });
            }
            else if (announced && !State.Popup.PreventUnmountingOnClose)
            {
                RaiseUnmountPreventionChanging(false);
            }

            return false;
        }

        BeforeDispatch(open, details);
        PopupTriggerElement? trigger = ResolveTrigger(details.Trigger);
        string? requestTriggerId = details.Trigger?.Id;
        OpenChangeRelay relay = OpenChangeRelay.From(++openChangeRevision, details.Reason, details.Event, requestTriggerId);
        if (open && lastChangeClosed && State.Popup.OpenChange is { } close && close.Revision > boundaryRevision)
        {
            // ADR 04 Amendments 2026-10-27 (the relay's Server semantics, item 2): a close the batch committed before this
            // open travels with it, so the modules see the session end before the reopen.
            relay = relay with { CoalescedCloseRevision = close.Revision, CoalescedCloseReason = close.Reason };
        }

        lastChangeClosed = !open;
        OnOpenChangeDispatched(relay);

        TState state = State;
        PopupStoreState<TPayload> popup = CreateOpenState(state.Popup, open, trigger, requestTriggerId, details.UnmountOnClosePrevented) with { OpenChange = relay };
        Write(TState.WithPopup(ApplyOpenChange(state, open, details), popup));
        return true;
    }

    /// <summary>Closes the popup with the reason <c>imperative-action</c> and no trigger, as Base UI's <c>actionsRef.close()</c> does.</summary>
    /// <returns>A task that completes when the request has run.</returns>
    public Task<bool> CloseAsync() => RequestOpenAsync(false, new OpenChangeEventDetails(EventReason.ImperativeAction));

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

    // PSH-PTR-02: the open interaction type is written only while the popup is closed (a closing click keeps it).
    /// <summary>Records how the user is opening the popup.</summary>
    /// <param name="type">The interaction type.</param>
    public void SetOpenInteractionType(InteractionType type)
    {
        PopupStoreState<TPayload> state = State.Popup;
        if (!state.EffectiveOpen && state.OpenMethod != type)
        {
            WritePopup(state with { OpenMethod = type });
        }
    }

    // TRN-ALG-01's mirrored status (ADR 04 §2): the popup's `transitions` handle reports each change once.
    /// <summary>Mirrors the transition status the popup element's browser-side behavior reported.</summary>
    /// <param name="status">The reported status.</param>
    public void MirrorStatus(TransitionStatus? status)
    {
        // ADR 16 rule 7: a report that changes nothing allocates nothing.
        if (State.Popup.TransitionStatus != status)
        {
            WritePopup(State.Popup with { TransitionStatus = status });
        }
    }

    // TRN-EVT-01/02, TRN-ANI-18 (useOpenChangeComplete): the popup's handle reports each completion once; a completion that
    // arrives after the open state flipped is dropped.
    /// <summary>Applies a finished enter or exit transition of the popup element.</summary>
    /// <param name="opened"><see langword="true"/> for a finished enter; <see langword="false"/> for a finished exit.</param>
    /// <returns>A task that completes when the callbacks have run.</returns>
    public Task CompleteTransitionAsync(bool opened)
    {
        bool isOpen = State.Popup.EffectiveOpen;
        if (opened)
        {
            return isOpen ? OnOpenChangeComplete.InvokeAsync(true) : Task.CompletedTask;
        }

        return isOpen || State.Popup.PreventUnmountingOnClose ? Task.CompletedTask : RunUnmountAsync();
    }

    // TRN-ALG-05's unmount action (Base UI's forceUnmount; TRN-EVT-08, TRN-ANI-19..22): a no-op once unmounted; while open, it
    // waits for the batch boundary and runs only if the committed state is then closed.
    /// <summary>Ends the closing phase now, as Base UI's <c>actionsRef.unmount()</c> does.</summary>
    /// <returns>A task that completes when the unmount cleanup has run, or at once when it waits for the boundary or does nothing.</returns>
    public Task UnmountAsync()
    {
        if (!State.Popup.Mounted)
        {
            return Task.CompletedTask;
        }

        if (State.Popup.EffectiveOpen)
        {
            unmountPending = true;
            RequestBoundary();
            return Task.CompletedTask;
        }

        return RunUnmountAsync();
    }

    // Issue 147 ((B1) with issue 148, chosen by the user 2026-10-29, ADR 04 Amendments 2026-10-29): the popup part hears the
    // opt-out at the PreventUnmountOnClose() call, a canceled close's reset, and every write that changes the recorded value
    // before it is published, so it announces each to its transitions module ahead of any listener's render, and so of the
    // close's batch (a listener renders synchronously when no batch is in progress).
    /// <summary>Registers a listener for a change of whether the current close keeps the popup mounted, called at the call and before a write is published.</summary>
    /// <param name="listener">The listener, called with the new value.</param>
    /// <returns>The registration; disposing it unregisters the listener.</returns>
    public IDisposable OnUnmountPreventionChanging(Action<bool> listener)
    {
        ArgumentNullException.ThrowIfNull(listener);
        unmountPreventionListeners.Add(listener);
        return new UnmountPreventionRegistration(this, listener);
    }

    // Design point 8 (ADR 16 Amendments 2026-10-27): a part that selects the open or mounted state renders on every write
    // that changes the reconciliation's inputs and runs AtBatchBoundaryAsync after it; from its subscription on, the root
    // stops rendering on those inputs.
    /// <summary>Registers a popup part that selects the open or mounted state and runs the render-batch boundary after its renders.</summary>
    /// <returns>The registration; disposing it unregisters the part.</returns>
    public IDisposable RegisterBoundaryPart()
    {
        boundaryParts++;
        return new BoundaryPartRegistration(this);
    }

    // The render-batch boundary (ADR 01 decision 3's amendment): the reconciliation of PSH-ALG-03 when its inputs changed,
    // then a pending unmount action. Called from the OnAfterRender of the popup parts' behaviors.
    /// <summary>Runs the work that waits for the end of a render batch: the reconciliation and a pending unmount action.</summary>
    /// <returns>A task that completes when a close the reconciliation requested has run.</returns>
    public Task AtBatchBoundaryAsync()
    {
        boundaryRevision = openChangeRevision;
        Task reconciled = reconciliationPending ? ReconcileAsync() : Task.CompletedTask;
        if (!unmountPending)
        {
            return reconciled;
        }

        unmountPending = false;
        PopupStoreState<TPayload> state = State.Popup;
        Task unmounted = !state.EffectiveOpen && state.Mounted ? RunUnmountAsync() : Task.CompletedTask;
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

    /// <summary>Runs the family's checks before a request's callback, such as filling in the active trigger of a close.</summary>
    /// <param name="open">The requested open state.</param>
    /// <param name="details">The request's details, which the step may complete.</param>
    protected virtual void PrepareOpenChange(bool open, OpenChangeEventDetails details)
    {
    }

    /// <summary>Runs the family's work between an accepted request and its relay, such as capturing the hovered line.</summary>
    /// <param name="open">The requested open state.</param>
    /// <param name="details">The request's details.</param>
    protected virtual void BeforeDispatch(bool open, OpenChangeEventDetails details)
    {
    }

    /// <summary>Runs after the relay of an accepted request is built, before its write.</summary>
    /// <param name="relay">The relay of the change.</param>
    protected virtual void OnOpenChangeDispatched(OpenChangeRelay relay)
    {
    }

    /// <summary>Applies the family's fields of a committed open change, such as the last reason or the instant phase.</summary>
    /// <param name="state">The current record.</param>
    /// <param name="open">The committed open state.</param>
    /// <param name="details">The change's details.</param>
    /// <returns>The record with the family's fields applied; the shared state is replaced after this step.</returns>
    protected virtual TState ApplyOpenChange(TState state, bool open, OpenChangeEventDetails details) => state;

    /// <summary>Runs the family's cleanup when the popup unmounts, between the store's reset and the completion callback.</summary>
    protected virtual void OnUnmount()
    {
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

    /// <summary>Writes a new family record, unless it equals the current one member by member.</summary>
    /// <param name="next">The record to write.</param>
    protected internal void Write(TState next)
    {
        ArgumentNullException.ThrowIfNull(next);
        if (IsInert)
        {
            return;
        }

        TState current = State;
        PopupStoreState<TPayload> popup = WithTransition(current.Popup, next.Popup);
        if (!ReferenceEquals(popup, next.Popup))
        {
            next = TState.WithPopup(next, popup);
        }

        if (current.Popup.PreventUnmountingOnClose != next.Popup.PreventUnmountingOnClose)
        {
            RaiseUnmountPreventionChanging(next.Popup.PreventUnmountingOnClose);
        }

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
        else if (TrackReconciliationInputs(current.Popup, next.Popup) && boundaryParts == 0 && !reconciling)
        {
            // Design point 8's fallback (ADR 16 Amendments 2026-10-27): with no popup part to bring the boundary, the root
            // renders on the reconciliation's inputs, once the new state is published.
            SetState(next);
            RequestBoundary();
            return;
        }

        SetState(next);
    }

    // TRN-ALG-01 evaluated at the write that flips the effective open state (useTransitionStatus at render; the Collapsible
    // precedent), and TRN-ANI-24: reopening by any route clears the stored opt-out.
    private static PopupStoreState<TPayload> WithTransition(PopupStoreState<TPayload> current, PopupStoreState<TPayload> next)
    {
        if (current.EffectiveOpen == next.EffectiveOpen)
        {
            return next;
        }

        (bool mounted, TransitionStatus? status) = TransitionStatusRules.Evaluate(next.EffectiveOpen, next.Mounted, next.TransitionStatus, deferEndingState: false);
        bool preventUnmounting = next.EffectiveOpen ? false : next.PreventUnmountingOnClose;
        return next.Mounted == mounted && next.TransitionStatus == status && next.PreventUnmountingOnClose == preventUnmounting
            ? next
            : next with { Mounted = mounted, TransitionStatus = status, PreventUnmountingOnClose = preventUnmounting };
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

    private void RequestBoundary() => BoundaryRequested?.Invoke();

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

    // PSH-STA-45, TRN-ANI-27 (popupStoreUtils.ts:586-595): clear the active trigger, mounted and the opt-out, then the
    // family's cleanup, then the completion callback. UnmountRequests moves so the popup's `transitions` handle, waiting on an
    // opted-out exit, ends it.
    private async Task RunUnmountAsync()
    {
        PopupStoreState<TPayload> state = State.Popup;
        if (!state.Mounted)
        {
            return;
        }

        WritePopup(state with
        {
            ActiveTriggerId = null,
            ActiveTriggerElement = null,
            Mounted = false,
            PreventUnmountingOnClose = false,
            TransitionStatus = null,
            UnmountRequests = state.UnmountRequests + 1,
        });
        OnUnmount();
        await OnOpenChangeComplete.InvokeAsync(false);
    }

    private void RaiseUnmountPreventionChanging(bool prevented)
    {
        foreach (Action<bool> listener in unmountPreventionListeners.ToArray())
        {
            listener(prevented);
        }
    }

    private sealed class UnmountPreventionRegistration(PopupStore<TState, TPayload> store, Action<bool> listener) : IDisposable
    {
        /// <inheritdoc />
        public void Dispose() => store.unmountPreventionListeners.Remove(listener);
    }

    private sealed class BoundaryPartRegistration(PopupStore<TState, TPayload> store) : IDisposable
    {
        private bool disposed;

        /// <inheritdoc />
        public void Dispose()
        {
            if (!disposed)
            {
                disposed = true;
                store.boundaryParts--;
            }
        }
    }
}

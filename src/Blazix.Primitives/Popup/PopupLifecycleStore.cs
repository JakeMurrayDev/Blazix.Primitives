using Blazix.Primitives.Interop;
using Blazix.Primitives.Store;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace Blazix.Primitives;

// FLT-STR-11, the port's standalone root store (useFloatingRootContext.ts:24-77, FloatingRootStore.ts:106-135), extracted
// from PopupStore (ADR 05 Amendments 2026-11-03, chosen by the user 2026-11-03; M5 track 2 design point 1, shape (a2),
// pending the orchestrator). PopupStore<TState, TPayload> derives from it and keeps the trigger registry, the payload and
// the handles; Select's, Combobox's and NavigationMenu's stores derive from it directly. Its fields and properties are the
// store's non-reactive context (UTL-STA-15): the controlled open, the callbacks, the inert flag, the session token, the
// relay's revision, the unmount-prevention listeners and the boundary parts.
// - The open-change sequence (PSH-ALG-01 = EVD-ALG-04, ADR 05 §2): the consumer step first, a cancel honoured, then the
//   family's pre-dispatch step, the relay (ADR 04 Amendments 2026-10-27), one write (PSH-ALG-02 = EVD-ALG-05). The consumer
//   step is RunConsumerStepAsync (the Controlled<bool> request by default); a family whose open is derived from its own value
//   (NavigationMenu, ADR 08 Amendments 2026-11-03 (2)) overrides it, or calls CommitOpenChange after its own step.
// - FLT-EVT-02's order stays the port's (ADR 05 Amendments 2026-11-03): the relay follows the commit, so a canceled change
//   sends no `openchange`.
// - The transitions plumbing (TRN-ALG-05, PSH-STA-44/45, TRN-ANI-19..27), issue 148's announcement and the render-batch
//   boundary (ADR 01 decision 3's amendment; ADR 16 Amendments 2026-10-27, design point 8) as PopupStore had them.
/// <summary>Holds the open lifecycle of one popup and runs its open-change sequence, its transitions and its unmount.</summary>
/// <typeparam name="TState">The type of the store record.</typeparam>
internal class PopupLifecycleStore<TState> : ComponentStore<TState>
    where TState : class, IStoreState<TState>, IPopupLifecycleState<TState>
{
    private readonly Controlled<bool> openState;
    private readonly List<Action<bool>> unmountPreventionListeners = [];
    private readonly Action announceUnmountPrevention;
    private int openChangeRevision;
    private int boundaryRevision;
    private bool lastChangeClosed;
    private bool unmountPending;
    private int boundaryParts;
    private bool seeded;

    /// <summary>Occurs when the store needs a render-batch boundary that no part's render will bring: the root renders.</summary>
    public event Action? BoundaryRequested;

    /// <summary>Initializes a new instance of the <see cref="PopupLifecycleStore{TState}"/> class.</summary>
    /// <param name="state">The initial record.</param>
    /// <param name="componentName">The family's name in diagnostics, such as <c>Select</c>.</param>
    /// <param name="sessionToken">The session token the root drew, which the browser-side behaviors key the popup's session by.</param>
    /// <param name="inert">Whether this is a fallback store, whose writes are skipped.</param>
    /// <param name="logger">The root's logger, or <see langword="null"/>.</param>
    public PopupLifecycleStore(TState state, string componentName, string? sessionToken, bool inert = false, ILogger? logger = null)
        : base(state)
    {
        ArgumentNullException.ThrowIfNull(componentName);
        ComponentName = componentName;
        SessionToken = sessionToken;
        IsInert = inert;
        Logger = logger;
        openState = new Controlled<bool>(componentName, "open", logger);
        announceUnmountPrevention = () => RaiseUnmountPreventionChanging(true);
    }

    /// <summary>Gets the family's name in diagnostics.</summary>
    public string ComponentName { get; }

    /// <summary>Gets the session token of the popup, never rendered; <see langword="null"/> for a fallback store.</summary>
    public string? SessionToken { get; }

    /// <summary>Gets a value indicating whether this is a fallback store, whose writes are skipped.</summary>
    public bool IsInert { get; }

    /// <summary>Gets the open lifecycle of the current record.</summary>
    public PopupLifecycleState Lifecycle => State.Lifecycle;

    /// <summary>Gets a value indicating whether the popup is effectively open.</summary>
    public bool IsOpen => State.Lifecycle.EffectiveOpen;

    /// <summary>Gets the root's controlled-or-uncontrolled open state, which the default consumer step requests through.</summary>
    public Controlled<bool> OpenState => openState;

    /// <summary>Gets or sets the consumer's open-change callback, called before a change is applied.</summary>
    public EventCallback<ValueChangeEventArgs<bool>> OnOpenChange { get; set; }

    /// <summary>Gets or sets the <c>@bind-Open</c> callback.</summary>
    public EventCallback<bool> OpenChanged { get; set; }

    /// <summary>Gets or sets the callback called once each enter or exit transition finishes.</summary>
    public EventCallback<bool> OnOpenChangeComplete { get; set; }

    // ADR 05 Amendments 2026-10-27: Dialog's rule (DLG-EVT-05, TRN-EVT-07) writes the flag the moment
    // PreventUnmountOnClose() is called, even when the same callback then cancels; the shared stores read the call after
    // the callback (PSH-STA-12, TRN-ANI-25).
    /// <summary>Gets or sets a value indicating whether an unmount opt-out survives the cancellation of its own close.</summary>
    public bool UnmountPreventionAtCall { get; set; }

    /// <summary>Gets a value indicating whether a popup part that brings the render-batch boundary is registered.</summary>
    public bool HasBoundaryPart => boundaryParts > 0;

    /// <summary>Gets the revision of the last committed open change.</summary>
    public int OpenChangeRevision => openChangeRevision;

    // EVD-ALG-05's open half (createPopupOpenState, popupStoreUtils.ts:189-226): the committed open state of a request.
    /// <summary>Derives the open lifecycle a committed open change writes.</summary>
    /// <param name="state">The current open lifecycle.</param>
    /// <param name="open">The requested open state.</param>
    /// <param name="preventUnmountOnClose">Whether the request's callback called <c>PreventUnmountOnClose()</c>.</param>
    /// <returns>The open lifecycle with the open state and the unmount opt-out applied.</returns>
    public static PopupLifecycleState CreateOpenState(in PopupLifecycleState state, bool open, bool preventUnmountOnClose)
    {
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

        return state with { Open = open, PreventUnmountingOnClose = preventUnmounting };
    }

    // PSH-STA-01, PSH-STA-44 (popupStoreUtils.ts:598-605); ADR 08 Amendments 2026-10-27 (Open/DefaultOpen, controlled iff
    // supplied at the first parameter set). The store is seeded before any part subscribes, so an initially open root is
    // mounted with no enter transition (PSH-ANI-02, TRN-STA-02); a root disabled from its first render is not (TIP-EDGE-10).
    /// <summary>Applies the root's open parameters to the record: at the first call it seeds the open, mounted and transition state.</summary>
    /// <param name="openProp">The controlled open state, or <see langword="null"/> while uncontrolled.</param>
    /// <param name="defaultOpen">The open state an uncontrolled root starts with.</param>
    /// <remarks>
    /// A root that owns a <see cref="Controlled{T}"/> passes <see cref="OpenState"/>'s value while it is controlled; a root
    /// whose open state is derived from its own value passes the derived controlled state.
    /// </remarks>
    public void ApplyOpenParameters(bool? openProp, bool defaultOpen)
    {
        PopupLifecycleState current = State.Lifecycle;
        if (!seeded)
        {
            seeded = true;
            bool initialOpen = PopupLifecycleState.IsEffectivelyOpen(current.Disabled, openProp, defaultOpen);
            (bool mounted, TransitionStatus? status) = TransitionStatusRules.Initial(initialOpen, enableIdleState: false, animateInitialOpen: false);
            SetState(TState.WithLifecycle(State, current with { Open = defaultOpen, OpenProp = openProp, Mounted = mounted, TransitionStatus = status }));
            return;
        }

        if (current.OpenProp != openProp)
        {
            WriteLifecycle(current with { OpenProp = openProp });
        }
    }

    /// <summary>Runs the open-change sequence for a requested open state.</summary>
    /// <param name="open">The requested open state.</param>
    /// <param name="details">The details of the request.</param>
    /// <returns><see langword="true"/> if the change was applied; <see langword="false"/> if it was canceled or the store is inert.</returns>
    /// <remarks>
    /// The consumer step runs first; a canceled request stops there. Then the family's <see cref="BeforeDispatch"/> step, the
    /// relay of the change, and one write of the committed state.
    /// </remarks>
    public virtual async Task<bool> RequestOpenAsync(bool open, OpenChangeEventDetails details)
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
        // The hook is one cached delegate (ADR 16 rule 2): the details record the call, so it carries no state of the request.
        if (!open)
        {
            details.UnmountPreventionCalled = announceUnmountPrevention;
        }

        bool applied;
        try
        {
            applied = await RunConsumerStepAsync(open, details);
        }
        finally
        {
            details.UnmountPreventionCalled = null;
        }

        if (!applied)
        {
            PopupLifecycleState lifecycle = State.Lifecycle;
            if (UnmountPreventionAtCall && !open && details.UnmountOnClosePrevented && !lifecycle.PreventUnmountingOnClose)
            {
                WriteLifecycle(lifecycle with { PreventUnmountingOnClose = true });
            }
            else if (!open && details.UnmountOnClosePrevented && !lifecycle.PreventUnmountingOnClose)
            {
                RaiseUnmountPreventionChanging(false);
            }

            return false;
        }

        CommitOpenChange(open, details);
        return true;
    }

    /// <summary>Closes the popup with the reason <c>imperative-action</c> and no trigger, as Base UI's <c>actionsRef.close()</c> does.</summary>
    /// <returns>A task that completes when the request has run.</returns>
    public Task<bool> CloseAsync() => RequestOpenAsync(false, new OpenChangeEventDetails(EventReason.ImperativeAction));

    /// <summary>Commits an open change whose consumer step has already run and was not canceled.</summary>
    /// <param name="open">The committed open state.</param>
    /// <param name="details">The details of the change.</param>
    /// <remarks>
    /// Runs the family's <see cref="BeforeDispatch"/> step, builds the relay of the change, and writes the open state, the
    /// mounted state, the transition status and the relay in one write.
    /// </remarks>
    public void CommitOpenChange(bool open, OpenChangeEventDetails details)
    {
        ArgumentNullException.ThrowIfNull(details);
        if (IsInert)
        {
            return;
        }

        BeforeDispatch(open, details);
        OpenChangeRelay relay = OpenChangeRelay.From(++openChangeRevision, details.Reason, details.Event, details.Trigger?.Id);
        if (open && lastChangeClosed && State.Lifecycle.OpenChange is { } close && close.Revision > boundaryRevision)
        {
            // ADR 04 Amendments 2026-10-27 (the relay's Server semantics, item 2): a close the batch committed before this
            // open travels with it, so the modules see the session end before the reopen.
            relay = relay with { CoalescedCloseRevision = close.Revision, CoalescedCloseReason = close.Reason };
        }

        lastChangeClosed = !open;
        OnOpenChangeDispatched(relay);

        TState state = State;
        PopupLifecycleState lifecycle = CreateOpenState(state.Lifecycle, open, details.UnmountOnClosePrevented) with { OpenChange = relay };
        Write(TState.WithLifecycle(ApplyOpenChange(state, open, details), lifecycle));
    }

    // TRN-ALG-01's mirrored status (ADR 04 §2): the popup's `transitions` handle reports each change once.
    /// <summary>Mirrors the transition status the popup element's browser-side behavior reported.</summary>
    /// <param name="status">The reported status.</param>
    public void MirrorStatus(TransitionStatus? status)
    {
        // ADR 16 rule 7: a report that changes nothing allocates nothing.
        PopupLifecycleState lifecycle = State.Lifecycle;
        if (lifecycle.TransitionStatus != status)
        {
            WriteLifecycle(lifecycle with { TransitionStatus = status });
        }
    }

    // TRN-EVT-01/02, TRN-ANI-18 (useOpenChangeComplete): the popup's handle reports each completion once; a completion that
    // arrives after the open state flipped is dropped.
    /// <summary>Applies a finished enter or exit transition of the popup element.</summary>
    /// <param name="opened"><see langword="true"/> for a finished enter; <see langword="false"/> for a finished exit.</param>
    /// <returns>A task that completes when the callbacks have run.</returns>
    public Task CompleteTransitionAsync(bool opened)
    {
        PopupLifecycleState lifecycle = State.Lifecycle;
        if (opened)
        {
            return lifecycle.EffectiveOpen ? OnOpenChangeComplete.InvokeAsync(true) : Task.CompletedTask;
        }

        return lifecycle.EffectiveOpen || lifecycle.PreventUnmountingOnClose ? Task.CompletedTask : RunUnmountAsync();
    }

    // TRN-ALG-05's unmount action (Base UI's forceUnmount; TRN-EVT-08, TRN-ANI-19..22): a no-op once unmounted; while open, it
    // waits for the batch boundary and runs only if the committed state is then closed.
    /// <summary>Ends the closing phase now, as Base UI's <c>actionsRef.unmount()</c> does.</summary>
    /// <returns>A task that completes when the unmount cleanup has run, or at once when it waits for the boundary or does nothing.</returns>
    public Task UnmountAsync()
    {
        PopupLifecycleState lifecycle = State.Lifecycle;
        if (!lifecycle.Mounted)
        {
            return Task.CompletedTask;
        }

        if (lifecycle.EffectiveOpen)
        {
            unmountPending = true;
            RequestBoundary();
            return Task.CompletedTask;
        }

        return RunUnmountAsync();
    }

    // PSH-PTR-02: the open interaction type is written only while the popup is closed (a closing click keeps it).
    /// <summary>Records how the user is opening the popup.</summary>
    /// <param name="type">The interaction type.</param>
    public void SetOpenInteractionType(InteractionType type)
    {
        PopupLifecycleState lifecycle = State.Lifecycle;
        if (!lifecycle.EffectiveOpen && lifecycle.OpenMethod != type)
        {
            WriteLifecycle(lifecycle with { OpenMethod = type });
        }
    }

    // Issue 147 ((B1) with issue 148, chosen by the user 2026-10-29, ADR 04 Amendments 2026-10-29): the popup part hears the
    // opt-out at the PreventUnmountOnClose() call, a canceled close's reset, and every write that changes the recorded value
    // before it is published, so it announces each to its transitions module ahead of any listener's render.
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
    // that changes the boundary's inputs and runs AtBatchBoundaryAsync after it.
    /// <summary>Registers a popup part that selects the open or mounted state and runs the render-batch boundary after its renders.</summary>
    /// <returns>The registration; disposing it unregisters the part.</returns>
    public IDisposable RegisterBoundaryPart()
    {
        boundaryParts++;
        return new BoundaryPartRegistration(this);
    }

    // The render-batch boundary (ADR 01 decision 3's amendment): the close's reset of the open interaction type (PSH-PTR-03's
    // Root-level variant; PopupStore's reconciliation did it on main), then a pending unmount action. Called from the
    // OnAfterRender of the popup parts' behaviors and of the root.
    /// <summary>Runs the work that waits for the end of a render batch: a pending unmount action and the close's reset of the open interaction type.</summary>
    /// <returns>A task that completes when the boundary's work has run.</returns>
    public virtual Task AtBatchBoundaryAsync()
    {
        boundaryRevision = openChangeRevision;
        PopupLifecycleState lifecycle = State.Lifecycle;
        if (!lifecycle.EffectiveOpen && lifecycle.OpenMethod is not null)
        {
            WriteLifecycle(lifecycle with { OpenMethod = null });
        }

        if (!unmountPending)
        {
            return Task.CompletedTask;
        }

        unmountPending = false;
        lifecycle = State.Lifecycle;
        return !lifecycle.EffectiveOpen && lifecycle.Mounted ? RunUnmountAsync() : Task.CompletedTask;
    }

    /// <summary>Runs the consumer step of a request: the consumer's callback, its cancel, then <see cref="OpenChanged"/>.</summary>
    /// <param name="open">The requested open state.</param>
    /// <param name="details">The details of the request.</param>
    /// <returns><see langword="true"/> if the request was not canceled.</returns>
    /// <remarks>The default requests through <see cref="OpenState"/>; a family whose open state is derived from its own value runs its own callbacks instead.</remarks>
    protected virtual Task<bool> RunConsumerStepAsync(bool open, OpenChangeEventDetails details) => openState.RequestAsync(open, details, OnOpenChange, OpenChanged);

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
    /// <returns>The record with the family's fields applied; the open lifecycle is replaced after this step.</returns>
    protected virtual TState ApplyOpenChange(TState state, bool open, OpenChangeEventDetails details) => state;

    /// <summary>Runs the family's cleanup when the popup unmounts, between the store's reset and the completion callback.</summary>
    protected virtual void OnUnmount()
    {
    }

    /// <summary>Writes a new open lifecycle, unless it equals the current one member by member.</summary>
    /// <param name="next">The open lifecycle to write.</param>
    protected internal void WriteLifecycle(in PopupLifecycleState next)
    {
        // ADR 16 rule 7: an equal lifecycle changes nothing, so no record is built for it.
        if (State.Lifecycle == next)
        {
            return;
        }

        Write(TState.WithLifecycle(State, next));
    }

    /// <summary>Writes a new record, unless it equals the current one member by member.</summary>
    /// <param name="next">The record to write.</param>
    /// <remarks>The write that flips the effective open state evaluates the mounted state and the transition status.</remarks>
    protected internal void Write(TState next)
    {
        ArgumentNullException.ThrowIfNull(next);
        if (IsInert)
        {
            return;
        }

        TState current = State;
        PopupLifecycleState lifecycle = WithTransition(current.Lifecycle, next.Lifecycle);
        if (lifecycle != next.Lifecycle)
        {
            next = TState.WithLifecycle(next, lifecycle);
        }

        if (current.Lifecycle.PreventUnmountingOnClose != next.Lifecycle.PreventUnmountingOnClose)
        {
            RaiseUnmountPreventionChanging(next.Lifecycle.PreventUnmountingOnClose);
        }

        // The lifecycle compares member by member in the record's generated ShallowEquals (its == operator), so an equal
        // record is a no-op (UTL-STA-11).
        if (ReferenceEquals(current, next) || TState.ShallowEquals(current, next))
        {
            return;
        }

        SetState(next);
    }

    /// <summary>Asks the root for a render-batch boundary that no part's render will bring.</summary>
    protected void RequestBoundary() => BoundaryRequested?.Invoke();

    // TRN-ALG-01 evaluated at the write that flips the effective open state (useTransitionStatus at render; the Collapsible
    // precedent), and TRN-ANI-24: reopening by any route clears the stored opt-out.
    private static PopupLifecycleState WithTransition(in PopupLifecycleState current, in PopupLifecycleState next)
    {
        if (current.EffectiveOpen == next.EffectiveOpen)
        {
            return next;
        }

        (bool mounted, TransitionStatus? status) = TransitionStatusRules.Evaluate(next.EffectiveOpen, next.Mounted, next.TransitionStatus, deferEndingState: false);
        bool preventUnmounting = !next.EffectiveOpen && next.PreventUnmountingOnClose;
        return next.Mounted == mounted && next.TransitionStatus == status && next.PreventUnmountingOnClose == preventUnmounting
            ? next
            : next with { Mounted = mounted, TransitionStatus = status, PreventUnmountingOnClose = preventUnmounting };
    }

    // PSH-STA-45, TRN-ANI-27 (popupStoreUtils.ts:586-595): clear mounted and the opt-out, then the family's cleanup, then the
    // completion callback. UnmountRequests moves so the popup's `transitions` handle, waiting on an opted-out exit, ends it.
    private async Task RunUnmountAsync()
    {
        PopupLifecycleState lifecycle = State.Lifecycle;
        if (!lifecycle.Mounted)
        {
            return;
        }

        WriteLifecycle(lifecycle with
        {
            Mounted = false,
            PreventUnmountingOnClose = false,
            TransitionStatus = null,
            UnmountRequests = lifecycle.UnmountRequests + 1,
        });
        OnUnmount();
        await OnOpenChangeComplete.InvokeAsync(false);
    }

    // Each listener only sends its part's announcement and none registers or unregisters during the raise, so the list is
    // read in place, without a copy per raise (ADR 16).
    private void RaiseUnmountPreventionChanging(bool prevented)
    {
        for (int i = 0; i < unmountPreventionListeners.Count; i++)
        {
            unmountPreventionListeners[i](prevented);
        }
    }

    private sealed class UnmountPreventionRegistration(PopupLifecycleStore<TState> store, Action<bool> listener) : IDisposable
    {
        /// <inheritdoc />
        public void Dispose() => store.unmountPreventionListeners.Remove(listener);
    }

    private sealed class BoundaryPartRegistration(PopupLifecycleStore<TState> store) : IDisposable
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

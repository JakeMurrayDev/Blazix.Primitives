using Blazix.Primitives.Interop;
using Blazix.Primitives.Store;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace Blazix.Primitives;

// The reactive half of Base UI's useCollapsibleRoot (collapsible/root/useCollapsibleRoot.ts) with useTransitionStatus's
// render-time evaluation (TRN-ALG-01 init and evaluation step through TransitionStatusRules, ADR 01 Amendments
// 2026-10-21): what the Root's parts select. TransitionStatus is the Root's mirrored status (ADR 04 §2).
/// <summary>The store record of a collapsible: what its trigger and panel read.</summary>
/// <param name="Open">A value indicating whether the panel is open.</param>
/// <param name="Disabled">A value indicating whether the root ignores user interaction.</param>
/// <param name="Mounted">A value indicating whether the panel is present for transition purposes.</param>
/// <param name="TransitionStatus">The root's transition status, mirrored from its element.</param>
/// <param name="PanelId">The id the trigger's <c>aria-controls</c> references, or <see langword="null"/> after the panel unregistered.</param>
/// <param name="UnmountRequests">The number of exits the panel has ended, which the root's and the triggers' elements follow.</param>
[StoreState]
internal sealed partial record CollapsibleRootStoreState(
    bool Open,
    bool Disabled,
    bool Mounted,
    TransitionStatus? TransitionStatus,
    string? PanelId,
    int UnmountRequests) : IStoreState<CollapsibleRootStoreState>
{
    public static readonly Func<CollapsibleRootStoreState, bool> OpenSelector = state => state.Open;
    public static readonly Func<CollapsibleRootStoreState, bool> DisabledSelector = state => state.Disabled;
    public static readonly Func<CollapsibleRootStoreState, bool> MountedSelector = state => state.Mounted;
    public static readonly Func<CollapsibleRootStoreState, TransitionStatus?> StatusSelector = state => state.TransitionStatus;
    public static readonly Func<CollapsibleRootStoreState, string?> PanelIdSelector = state => state.PanelId;
    public static readonly Func<CollapsibleRootStoreState, int> UnmountRequestsSelector = state => state.UnmountRequests;
}

// Base UI's CollapsibleRootContext (collapsible/root/CollapsibleRootContext.ts) and useCollapsibleRoot: the open state as a
// request (COL-API-01..03, COL-STA-01..03, COL-EVT-01/03), the mounted flag and the Root status (COL-STA-04/05), the panel
// id registration (COL-STA-09, COL-ARIA-04, COL-EDGE-09; §14: undefined = never registered, null = unregistered). Cascaded
// with IsFixed (ADR 16 rule 9). Accordion.Item (M3) owns one too, with its open state driven by the accordion.
/// <summary>
/// The shared state of a collapsible: the open state and its requests, the mounted flag and transition status of the
/// panel, and the panel id the triggers reference.
/// </summary>
internal sealed class CollapsibleRootContext : ComponentStore<CollapsibleRootStoreState>
{
    private readonly Controlled<bool> open;
    private string? registeredPanelId;
    private bool panelUnregistered;

    /// <summary>Initializes a new instance of the <see cref="CollapsibleRootContext"/> class.</summary>
    /// <param name="defaultPanelId">The generated id a panel without an id of its own renders.</param>
    /// <param name="logger">The logger of the root's development warnings, or <see langword="null"/>.</param>
    public CollapsibleRootContext(string defaultPanelId, ILogger? logger)
        : base(new CollapsibleRootStoreState(Open: false, Disabled: false, Mounted: false, TransitionStatus: null, PanelId: defaultPanelId, UnmountRequests: 0))
    {
        DefaultPanelId = defaultPanelId;
        open = new Controlled<bool>("Collapsible", "open", logger);
        Logger = logger;
    }

    /// <summary>Gets the generated id a panel without an id of its own renders.</summary>
    /// <value>The root's default panel id.</value>
    public string DefaultPanelId { get; }

    /// <summary>Gets the open state's ownership and value.</summary>
    /// <value>The controlled value the root reads its <c>Open</c> and <c>DefaultOpen</c> parameters into.</value>
    public Controlled<bool> OpenState => open;

    /// <summary>Gets or sets the consumer's callback that a request runs first.</summary>
    /// <value>The <c>OnOpenChange</c> callback; the latest one is always the one invoked.</value>
    public EventCallback<ValueChangeEventArgs<bool>> OnOpenChange { get; set; }

    /// <summary>Gets or sets the binding callback that an accepted request runs.</summary>
    /// <value>The <c>OpenChanged</c> callback.</value>
    public EventCallback<bool> OpenChanged { get; set; }

    /// <summary>Applies the parameters of one parameter set: the open value the ownership resolves and the disabled flag.</summary>
    /// <param name="disabled">The root's <c>Disabled</c> parameter.</param>
    /// <param name="firstSet"><see langword="true"/> on the first parameter set, which renders the initial values.</param>
    public void ApplyParameters(bool disabled, bool firstSet)
    {
        if (firstSet)
        {
            // TRN-STA-02/13: an initially open root is mounted and idle, with no enter transition (animateInitialOpen false).
            (bool mounted, TransitionStatus? status) = TransitionStatusRules.Initial(open.Value, enableIdleState: true, animateInitialOpen: false);
            SetState(State with { Open = open.Value, Disabled = disabled, Mounted = mounted, TransitionStatus = status });
            return;
        }

        Update(static (state, values) => Evaluated(state, values.Open) with { Disabled = values.Disabled }, (Open: open.Value, Disabled: disabled));
    }

    /// <summary>Runs one open request: the consumer's callback, then the commit unless it was canceled.</summary>
    /// <param name="value">The requested open state.</param>
    /// <param name="details">The request's details.</param>
    /// <returns>A task whose result is <see langword="true"/> when the request was not canceled.</returns>
    public async Task<bool> RequestOpenAsync(bool value, ChangeEventDetails details)
    {
        bool applied = await open.RequestAsync(value, details, OnOpenChange, OpenChanged);
        if (applied)
        {
            ApplyOpen(open.Value);
        }

        return applied;
    }

    /// <summary>Applies a new open state and the render-time evaluation of the transition machine.</summary>
    /// <param name="value">The open state to show.</param>
    public void ApplyOpen(bool value)
    {
        Update(static (state, value) => Evaluated(state, value), value);
    }

    /// <summary>Takes the root's status from its element, where JavaScript flips it.</summary>
    /// <param name="status">The status the root's element shows.</param>
    public void MirrorStatus(TransitionStatus? status)
    {
        // ADR 16 rule 7: a report that changes nothing allocates nothing.
        if (State.TransitionStatus != status)
        {
            SetState(State with { TransitionStatus = status });
        }
    }

    // COL-STA-04, COL-ANI-09/10/11/18; useCollapsiblePanel's setMounted(false). A completion that arrives after a reopen is
    // dropped, as the panel's own check of the latest open state does.
    /// <summary>Ends the panel's exit: the panel is no longer mounted, and the root's and triggers' elements end theirs.</summary>
    public void CompleteExit()
    {
        CollapsibleRootStoreState state = State;
        if (!state.Mounted || state.Open)
        {
            return;
        }

        (bool mounted, TransitionStatus? status) = TransitionStatusRules.Evaluate(open: false, mounted: false, state.TransitionStatus, deferEndingState: true);
        SetState(state with { Mounted = mounted, TransitionStatus = status, UnmountRequests = state.UnmountRequests + 1 });
    }

    /// <summary>Ends an exit when the panel has no element while the root is ending (a render function withheld it).</summary>
    public void CompleteExitIfEnding()
    {
        CollapsibleRootStoreState state = State;
        if (!state.Open && state.Mounted && state.TransitionStatus == TransitionStatus.Ending)
        {
            CompleteExit();
        }
    }

    // CollapsiblePanel.tsx:64-69: register (registeredId ?? (current === null ? undefined : current)).
    /// <summary>Registers a panel and its id.</summary>
    /// <param name="registeredId">The panel's non-empty id, or <see langword="null"/> for a panel that uses the default id.</param>
    public void RegisterPanel(string? registeredId)
    {
        if (registeredId is not null)
        {
            registeredPanelId = registeredId;
            panelUnregistered = false;
        }
        else if (panelUnregistered)
        {
            panelUnregistered = false;
        }

        SyncPanelId();
    }

    // CollapsiblePanel.tsx:66-68: unregister only the id that is registered (current === registeredId ? null : current).
    /// <summary>Unregisters a panel.</summary>
    /// <param name="registeredId">The id the panel registered, or <see langword="null"/> for one that used the default id.</param>
    public void UnregisterPanel(string? registeredId)
    {
        bool registered = registeredId is null
            ? registeredPanelId is null && !panelUnregistered
            : !panelUnregistered && string.Equals(registeredPanelId, registeredId, StringComparison.Ordinal);
        if (registered)
        {
            registeredPanelId = null;
            panelUnregistered = true;
            SyncPanelId();
        }
    }

    private static CollapsibleRootStoreState Evaluated(CollapsibleRootStoreState state, bool value)
    {
        if (state.Open == value)
        {
            return state;
        }

        (bool mounted, TransitionStatus? status) = TransitionStatusRules.Evaluate(value, state.Mounted, state.TransitionStatus, deferEndingState: true);
        return state with { Open = value, Mounted = mounted, TransitionStatus = status };
    }

    // useCollapsibleRoot.ts:29: registered === null ? undefined : (registered ?? defaultPanelId).
    private void SyncPanelId()
    {
        string? panelId = panelUnregistered ? null : registeredPanelId ?? DefaultPanelId;
        Update(static (state, panelId) => state with { PanelId = panelId }, panelId);
    }
}

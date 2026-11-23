using Blazix.Primitives.Interop;
using Blazix.Primitives.Rendering;
using Blazix.Primitives.Store;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace Blazix.Primitives.Fixtures.Client.PopupFixtures;

/// <summary>
/// The standalone host's store record at L4 (M5 track 2, brief deliverable 1), the counterpart of the L2 project's
/// <c>TestLifecycleStoreState</c>: the open lifecycle as its one member, with no trigger registry or payload.
/// </summary>
[StoreState]
internal sealed partial record FixtureLifecycleStoreState(PopupLifecycleState Lifecycle)
    : IStoreState<FixtureLifecycleStoreState>, IPopupLifecycleState<FixtureLifecycleStoreState>
{
    public static readonly Func<FixtureLifecycleStoreState, bool> Open = static state => state.Lifecycle.EffectiveOpen;

    public static readonly Func<FixtureLifecycleStoreState, bool> Mounted = static state => state.Lifecycle.Mounted;

    public static readonly Func<FixtureLifecycleStoreState, TransitionStatus?> Status = static state => state.Lifecycle.DerivedTransitionStatus;

    public static readonly Func<FixtureLifecycleStoreState, int> UnmountRequests = static state => state.Lifecycle.UnmountRequests;

    public static readonly Func<FixtureLifecycleStoreState, OpenChangeRelay?> OpenChange = static state => state.Lifecycle.OpenChange;

    public static FixtureLifecycleStoreState WithLifecycle(FixtureLifecycleStoreState state, in PopupLifecycleState lifecycle) => state with { Lifecycle = lifecycle };
}

/// <summary>The standalone host's store at L4.</summary>
internal sealed class FixtureLifecycleStore(string? floatingId, string? sessionToken, ILogger? logger = null)
    : PopupLifecycleStore<FixtureLifecycleStoreState>(new FixtureLifecycleStoreState(PopupLifecycleState.Initial(floatingId)), "FixtureLifecycle", sessionToken, inert: false, logger);

/// <summary>
/// The standalone host's root at L4, the counterpart of the L2 project's <c>TestLifecycleRoot</c>: renders no element and
/// composes <see cref="PopupLifecycleRootBehavior{TStore, TState}"/> with the <c>interactions</c> session on the store's
/// token, Escape dismissal only, and no trigger registry, payload or handle. The committed state reaches the session from
/// the commit path (ADR 01 Amendments 2026-10-27), as <c>FixturePopupRoot</c> does.
/// </summary>
public sealed class FixtureLifecycleRoot : ComponentBase, IAsyncDisposable
{
    private static readonly InteractionsDismissOptions s_escapeOnly = new() { OutsidePress = false };

    private readonly PopupLifecycleRootBehavior<FixtureLifecycleStore, FixtureLifecycleStoreState> behavior;
    private InteractionsSessionBehavior? session;
    private SelectorSubscription<FixtureLifecycleStoreState>? committed;
    private bool rendered;

    public FixtureLifecycleRoot()
    {
        behavior = new((popupId, token, logger) => new FixtureLifecycleStore(popupId, token, logger), StateHasChanged);
    }

    [Inject]
    private PartIdGenerator Ids { get; set; } = null!;

    [Inject]
    private ILogger<FixtureLifecycleRoot> Logger { get; set; } = null!;

    [Inject]
    private IJSRuntime JSRuntime { get; set; } = null!;

    [Parameter]
    public bool Open { get; set; }

    [Parameter]
    public EventCallback<bool> OpenChanged { get; set; }

    [Parameter]
    public bool DefaultOpen { get; set; }

    [Parameter]
    public EventCallback<ValueChangeEventArgs<bool>> OnOpenChange { get; set; }

    [Parameter]
    public EventCallback<bool> OnOpenChangeComplete { get; set; }

    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>Gets a task that completes when the session handle is attached.</summary>
    public Task SessionAttached => session?.TwinAttached ?? Task.CompletedTask;

    internal FixtureLifecycleStore Store => behavior.Store;

    public override Task SetParametersAsync(ParameterView parameters)
    {
        behavior.EnsureStore(Ids, Logger);
        FixtureLifecycleStore store = behavior.Store;
        store.OpenState.SetParameters(parameters, "Open", "DefaultOpen");
        if (session is null)
        {
            session = new InteractionsSessionBehavior(JSRuntime, store.SessionToken!, RouteAsync, _ => Task.CompletedTask) { Dismiss = s_escapeOnly };
            committed = new SelectorSubscription<FixtureLifecycleStoreState>(store, OnCommitted);
            committed.Select(FixtureLifecycleStoreState.Open);
            committed.Select(FixtureLifecycleStoreState.OpenChange);
            committed.Select(FixtureLifecycleStoreState.Status);
        }

        return base.SetParametersAsync(parameters);
    }

    public async ValueTask DisposeAsync()
    {
        behavior.Dispose();
        committed?.Dispose();
        if (session is not null)
        {
            await session.DisposeAsync();
        }
    }

    /// <summary>Opens the popup with the reason <c>trigger-press</c> and no trigger.</summary>
    /// <returns>A task that completes when the request has run.</returns>
    public Task OpenAsync() => Store.RequestOpenAsync(true, new OpenChangeEventDetails(EventReason.TriggerPress));

    /// <summary>Runs the root's unmount action.</summary>
    /// <returns>A task that completes when the unmount cleanup has run.</returns>
    public Task UnmountAsync() => Store.UnmountAsync();

    protected override void OnParametersSet()
    {
        FixtureLifecycleStore store = behavior.Store;
        store.OnOpenChange = OnOpenChange;
        store.OpenChanged = OpenChanged;
        store.OnOpenChangeComplete = OnOpenChangeComplete;
        store.ApplyOpenParameters(store.OpenState.IsControlled ? store.OpenState.Value : null, DefaultOpen);
        behavior.ChildContent = ChildContent;
    }

    protected override void BuildRenderTree(RenderTreeBuilder builder) => behavior.BuildRenderTree(builder);

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        rendered = true;
        CopySessionInputs();
        Task sync = session!.AfterRenderAsync();
        await behavior.OnAfterRenderAsync();
        await sync;
    }

    private Task RouteAsync(OpenChangeRequest request) => behavior.Store.RequestOpenAsync(request.Open, request.Details);

    private void OnCommitted()
    {
        if (rendered)
        {
            CopySessionInputs();
            _ = session!.AfterRenderAsync();
        }
    }

    private void CopySessionInputs()
    {
        PopupLifecycleState lifecycle = behavior.Store.Lifecycle;
        session!.Open = lifecycle.EffectiveOpen;
        session.FloatingId = lifecycle.FloatingId;
        session.Ending = lifecycle.DerivedTransitionStatus == TransitionStatus.Ending;
        session.Relay = lifecycle.OpenChange;
    }
}

/// <summary>
/// The standalone host's popup at L4, the counterpart of the L2 project's <c>TestLifecyclePopup</c>: a <c>div</c> with
/// <c>role="dialog"</c> and the floating id, rendered while mounted, that registers as the store's boundary part and attaches
/// its <c>transitions</c> handle as the families' Popups do (<c>ReportOpenComplete</c>, <c>OpenSelector="[data-open]"</c>).
/// </summary>
public sealed class FixtureLifecyclePopup : PartBase<FixturePopupPopupState>
{
    private static readonly StateAttributesMapping<FixturePopupPopupState> s_mapping = PopupStateMapping.CreateOpen<FixturePopupPopupState>();

    private StoreSelection<FixtureLifecycleStoreState, bool>? open;
    private StoreSelection<FixtureLifecycleStoreState, bool>? mounted;
    private IDisposable? boundaryPart;
    private FixturePopupPopupState state = new(false);
    private TransitionsTwin? transitions;
    private TwinHandle<TransitionsOptions>? transitionsHandle;
    private TransitionsOptions? sentTransitions;
    private Task? attaching;

    [Inject]
    private IJSRuntime JSRuntime { get; set; } = null!;

    [CascadingParameter]
    private FixtureLifecycleStore? Store { get; set; }

    protected override string DefaultTagName => "div";

    protected override FixturePopupPopupState State
    {
        get
        {
            if (state.Open != open!.Value)
            {
                state = new FixturePopupPopupState(open.Value);
            }

            return state;
        }
    }

    protected override StateAttributesMapping<FixturePopupPopupState>? StateAttributesMapping => s_mapping;

    protected override void SubscribeSelectors()
    {
        SelectorSubscription<FixtureLifecycleStoreState> subscription = SubscribeTo(Store!);
        open = subscription.Select(FixtureLifecycleStoreState.Open);
        mounted = subscription.Select(FixtureLifecycleStoreState.Mounted);
        subscription.Select(FixtureLifecycleStoreState.Status);
        subscription.Select(FixtureLifecycleStoreState.UnmountRequests);
        boundaryPart = Store!.RegisterBoundaryPart();
    }

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        Enabled = mounted!.Value;
        base.BuildRenderTree(builder);
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (Element is { } element && Enabled)
        {
            await SyncTransitionsAsync(element);
        }
        else if (transitionsHandle is not null)
        {
            TwinHandle<TransitionsOptions> previous = transitionsHandle;
            transitionsHandle = null;
            sentTransitions = null;
            attaching = null;
            await previous.DisposeAsync();
        }

        await Store!.AtBatchBoundaryAsync();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            boundaryPart?.Dispose();
            boundaryPart = null;
            if (transitions is not null)
            {
                _ = transitions.DisposeAsync().AsTask();
                transitions = null;
            }
        }

        base.Dispose(disposing);
    }

    protected override void AddPartAttributes(AttributeList attributes)
    {
        attributes.Set("id", Store!.Lifecycle.FloatingId);
        attributes.Set("role", "dialog");
    }

    private async Task SyncTransitionsAsync(ElementReference element)
    {
        PopupLifecycleState lifecycle = Store!.Lifecycle;
        TransitionsOptions options = new()
        {
            Open = lifecycle.EffectiveOpen,
            Mounted = lifecycle.Mounted,
            Status = lifecycle.DerivedTransitionStatus,
            ReportOpenComplete = true,
            OpenSelector = "[data-open]",
            UnmountRequests = lifecycle.UnmountRequests,
        };
        if (transitions is null)
        {
            transitions = new TransitionsTwin(JSRuntime);
            transitions.StatusChanged += change => _ = InvokeAsync(() => Store.MirrorStatus(change.Status));
            transitions.TransitionCompleted += opened => _ = InvokeAsync(() => Store.CompleteTransitionAsync(opened));
        }

        if (transitionsHandle is null)
        {
            // A render during the pending first attach awaits it: TwinBase hands a repeated attach the pending handle.
            attaching ??= AttachTransitionsAsync(element, options);
            await attaching;
        }
        else if (options != sentTransitions)
        {
            sentTransitions = options;
            await transitionsHandle.UpdateAsync(options);
        }
    }

    private async Task AttachTransitionsAsync(ElementReference element, TransitionsOptions options)
    {
        sentTransitions = options;
        transitionsHandle = await transitions!.AttachAsync(element, options);
        attaching = null;
    }
}

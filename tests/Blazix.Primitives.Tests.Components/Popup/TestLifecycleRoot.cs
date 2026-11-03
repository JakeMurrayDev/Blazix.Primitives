using Blazix.Primitives.Interop;
using Blazix.Primitives.Rendering;
using Blazix.Primitives.Store;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace Blazix.Primitives.Tests.Components;

/// <summary>
/// The standalone test host's root (M5 track 2, brief deliverable 1): renders no element and composes
/// <see cref="PopupLifecycleRootBehavior{TStore, TState}"/> with an <c>interactions</c> session on the store's token, Escape
/// dismissal only, and no trigger registry, payload or handle. The committed state reaches the session from the commit path
/// (ADR 01 Amendments 2026-10-27), as <c>FixturePopupRoot</c> does.
/// </summary>
internal sealed class TestLifecycleRoot : ComponentBase, IAsyncDisposable
{
    private static readonly InteractionsDismissOptions s_escapeOnly = new() { OutsidePress = false };

    private readonly PopupLifecycleRootBehavior<TestLifecycleStore, TestLifecycleStoreState> behavior;
    private InteractionsSessionBehavior? session;
    private SelectorSubscription<TestLifecycleStoreState>? committed;
    private bool rendered;

    public TestLifecycleRoot()
    {
        behavior = new((popupId, token, logger) => new TestLifecycleStore(popupId, token, logger), StateHasChanged);
    }

    [Inject]
    private PartIdGenerator Ids { get; set; } = null!;

    [Inject]
    private ILogger<TestLifecycleRoot> Logger { get; set; } = null!;

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

    /// <summary>Gets or sets the family-owned consumer step of a derived open state; <see langword="false"/> cancels.</summary>
    [Parameter]
    public Func<bool, OpenChangeEventDetails, bool>? ValueStep { get; set; }

    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    internal TestLifecycleStore Store => behavior.Store;

    internal InteractionsSessionBehavior Session => session!;

    public int Renders { get; private set; }

    public override Task SetParametersAsync(ParameterView parameters)
    {
        behavior.EnsureStore(Ids, Logger);
        TestLifecycleStore store = behavior.Store;
        store.OpenState.SetParameters(parameters, "Open", "DefaultOpen");
        if (session is null)
        {
            session = new InteractionsSessionBehavior(JSRuntime, store.SessionToken!, RouteAsync, _ => Task.CompletedTask) { Dismiss = s_escapeOnly };
            committed = new SelectorSubscription<TestLifecycleStoreState>(store, OnCommitted);
            committed.Select(TestLifecycleStoreState.Open);
            committed.Select(TestLifecycleStoreState.OpenChange);
            committed.Select(TestLifecycleStoreState.Status);
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

    public Task<bool> RequestOpenAsync(bool open, OpenChangeEventDetails details) => Store.RequestOpenAsync(open, details);

    public Task UnmountAsync() => Store.UnmountAsync();

    protected override void OnParametersSet()
    {
        TestLifecycleStore store = behavior.Store;
        store.OnOpenChange = OnOpenChange;
        store.OpenChanged = OpenChanged;
        store.OnOpenChangeComplete = OnOpenChangeComplete;
        store.ValueStep = ValueStep;
        store.ApplyOpenParameters(store.OpenState.IsControlled ? store.OpenState.Value : null, DefaultOpen);
        behavior.ChildContent = ChildContent;
    }

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        Renders++;
        behavior.BuildRenderTree(builder);
    }

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

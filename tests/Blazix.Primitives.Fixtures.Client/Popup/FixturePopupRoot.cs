using Blazix.Primitives.Interop;
using Blazix.Primitives.Rendering;
using Blazix.Primitives.Store;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace Blazix.Primitives.Fixtures.Client.PopupFixtures;

/// <summary>
/// The test-only popup root at L4 (settled by the orchestrator 2026-10-27, plan §Settled "Test-only hosts"), the
/// counterpart of the L2 project's <c>TestPopupRoot</c>: renders no element and composes
/// <see cref="PopupRootBehavior{TStore, TState, TPayload}"/> with every shared root parameter, and the
/// <c>interactions</c> session on the store's token with Escape dismissal only (M4 track 3): the Escape titles of
/// <c>popup.spec.ts</c> and <c>interactions.spec.ts</c> run here.
/// </summary>
/// <typeparam name="TPayload">The type of the payload the popup's triggers carry.</typeparam>
[CascadingTypeParameter(nameof(TPayload))]
public sealed class FixturePopupRoot<TPayload> : ComponentBase, IAsyncDisposable
{
    // No outside press: the suites click the page's own buttons while the popup is open.
    private static readonly InteractionsDismissOptions s_escapeOnly = new() { OutsidePress = false };

    private readonly PopupRootBehavior<FixturePopupStore<TPayload>, FixturePopupStoreState<TPayload>, TPayload> behavior;
    private InteractionsSessionBehavior? session;
    private SelectorSubscription<FixturePopupStoreState<TPayload>>? committed;
    private bool rendered;

    public FixturePopupRoot()
    {
        behavior = new((popupId, token, logger) => new FixturePopupStore<TPayload>(popupId, token, logger: logger), StateHasChanged);
    }

    [Inject]
    private PartIdGenerator Ids { get; set; } = null!;

    [Inject]
    private ILogger<FixturePopupRoot<TPayload>> Logger { get; set; } = null!;

    [Inject]
    private IJSRuntime JSRuntime { get; set; } = null!;

    [Parameter]
    public bool Open { get; set; }

    [Parameter]
    public EventCallback<bool> OpenChanged { get; set; }

    [Parameter]
    public bool DefaultOpen { get; set; }

    [Parameter]
    public string? TriggerId { get; set; }

    [Parameter]
    public string? DefaultTriggerId { get; set; }

    [Parameter]
    public FixturePopupHandle<TPayload>? Handle { get; set; }

    [Parameter]
    public EventCallback<ValueChangeEventArgs<bool>> OnOpenChange { get; set; }

    [Parameter]
    public EventCallback<bool> OnOpenChangeComplete { get; set; }

    [Parameter]
    public bool CloseOnActiveTriggerUnmount { get; set; }

    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    [Parameter]
    public RenderFragment<TPayload?>? PayloadContent { get; set; }

    /// <summary>Gets a value indicating whether the popup is open.</summary>
    public bool IsOpen => behavior.Store.IsOpen;

    /// <summary>Gets a task that completes when the session handle is attached.</summary>
    public Task SessionAttached => session?.TwinAttached ?? Task.CompletedTask;

    public override Task SetParametersAsync(ParameterView parameters)
    {
        behavior.SetParameters(parameters, Ids, Logger);
        if (session is null)
        {
            FixturePopupStore<TPayload> store = behavior.Store;
            session = new InteractionsSessionBehavior(JSRuntime, store.SessionToken!, RouteAsync, DispatchExceptionAsync) { Dismiss = s_escapeOnly };

            // The commit path (ADR 01 Amendments 2026-10-27): a committed change reaches the session without a render of
            // the root, which renders only at a batch boundary the store requests.
            committed = new SelectorSubscription<FixturePopupStoreState<TPayload>>(store, OnCommitted);
            committed.Select(PopupSelectors<FixturePopupStoreState<TPayload>, TPayload>.Open);
            committed.Select(PopupSelectors<FixturePopupStoreState<TPayload>, TPayload>.ActiveTriggerId);
            committed.Select(PopupSelectors<FixturePopupStoreState<TPayload>, TPayload>.OpenChange);
            committed.Select(PopupSelectors<FixturePopupStoreState<TPayload>, TPayload>.TransitionStatus);
            committed.Select(PopupSelectors<FixturePopupStoreState<TPayload>, TPayload>.PopupId);
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

    protected override void OnParametersSet()
    {
        behavior.ApplyParameters(new PopupRootParameters<TPayload>(
            DefaultOpen,
            DefaultTriggerId,
            Handle,
            OnOpenChange,
            OpenChanged,
            OnOpenChangeComplete,
            CloseOnActiveTriggerUnmount,
            ChildContent,
            PayloadContent));
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
        PopupStoreState<TPayload> popup = behavior.Store.Popup;
        session!.Open = popup.EffectiveOpen;
        session.ActiveTriggerId = popup.EffectiveActiveTriggerId;
        session.FloatingId = popup.PopupId;
        session.Ending = popup.DerivedTransitionStatus == TransitionStatus.Ending;
        session.Relay = popup.OpenChange;
    }
}

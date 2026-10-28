using Blazix.Primitives.Interop;
using Blazix.Primitives.Rendering;
using Blazix.Primitives.Store;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace Blazix.Primitives;

// Base UI's PreviewCard.Root (preview-card/root/PreviewCardRoot.tsx); the contract is docs/components/preview-card.md §3.1
// and §4, in TooltipRoot's shape (M4 track 6).
// - The store, its handle attachment and its render-batch boundary are PopupRootBehavior's (M4 track 1). The root renders
//   no element (PRV-STR-01) and cascades, fixed, its store (for the triggers) and the non-generic PreviewCardRootContext
//   (for the parts below it). No `disabled` (PRV-EDGE-10).
// - The session (M4 track 3, design point 8: InteractionsSessionBehavior in Floating/) joins the store's token and holds
//   Escape and the sloppy outside press while the card is open, mounted or given a Handle (PRV-STA-16, PSH-STA-43); no
//   reference press, so EventReason.TriggerPress is never emitted (PRV-EVT-03, PRV §16 Q1). A Root inside another card's
//   Root joins its tree (ParentToken, PRV-STR-10), as PopoverRoot does: the port's tree is the session forest, so the
//   outer card's Root token stands for Base UI's outer Positioner node. The committed state reaches the session from the
//   commit path (ADR 01 Amendments 2026-10-27).
// - The payload clear (PRV-STA-12) runs in the commit path, so the payload template never renders a stale payload.
/// <summary>
/// Groups all parts of a preview card and owns its open state: Base UI's <c>PreviewCard.Root</c>. It renders no element.
/// </summary>
/// <typeparam name="TPayload">
/// The type of the payload the preview card's triggers carry; write <c>TPayload="NoPayload"</c> for a preview card without payloads.
/// </typeparam>
/// <remarks>
/// <para>
/// <see cref="OnOpenChange"/> receives a <see cref="ValueChangeEventArgs{TValue}"/> whose <c>Details</c> is an
/// <see cref="OpenChangeEventDetails"/>: cast it, as in <c>(OpenChangeEventDetails)args.Details</c>, to call
/// <see cref="OpenChangeEventDetails.PreventUnmountOnClose"/> or to read the trigger.
/// </para>
/// <para>
/// Two nested roots that both use <see cref="PayloadContent"/> must name its context, as in <c>Context="payload"</c>, or the
/// Razor compiler reports the clash of the default <c>context</c> name.
/// </para>
/// </remarks>
[CascadingTypeParameter(nameof(TPayload))]
public sealed class PreviewCardRoot<TPayload> : ComponentBase, IAsyncDisposable
{
    private static readonly InteractionsDismissOptions s_dismiss = new();

    private readonly PopupRootBehavior<PreviewCardStore<TPayload>, PreviewCardStoreState<TPayload>, TPayload> behavior;
    private readonly RenderFragment storeContent;
    private PreviewCardRootContext<TPayload>? context;
    private InteractionsSessionBehavior? session;
    private SelectorSubscription<PreviewCardStoreState<TPayload>>? committed;
    private bool rendered;

    /// <summary>Initializes a new instance of the <see cref="PreviewCardRoot{TPayload}"/> class.</summary>
    public PreviewCardRoot()
    {
        behavior = new((popupId, token, logger) => new PreviewCardStore<TPayload>(popupId, token, logger: logger), StateHasChanged);
        storeContent = behavior.BuildRenderTree;
    }

    [Inject]
    private PartIdGenerator Ids { get; set; } = null!;

    [Inject]
    private ILogger<PreviewCardRoot<TPayload>> Logger { get; set; } = null!;

    [Inject]
    private IJSRuntime JSRuntime { get; set; } = null!;

    [CascadingParameter]
    private PreviewCardRootContext? EnclosingRoot { get; set; }

    /// <summary>Gets or sets a value indicating whether the preview card is open, when you control it.</summary>
    /// <value>The open state; supplying it at the first render makes the open state yours, with <see cref="OnOpenChange"/> or <c>@bind-Open</c>.</value>
    [Parameter]
    public bool Open { get; set; }

    /// <summary>Gets or sets the callback <c>@bind-Open</c> uses.</summary>
    /// <value>Called with the new open state after <see cref="OnOpenChange"/> accepted it.</value>
    [Parameter]
    public EventCallback<bool> OpenChanged { get; set; }

    /// <summary>Gets or sets a value indicating whether the preview card is open at its first render, when you do not control it.</summary>
    /// <value><see langword="true"/> to open it initially. The default is <see langword="false"/>; later changes are ignored.</value>
    [Parameter]
    public bool DefaultOpen { get; set; }

    /// <summary>Gets or sets the callback called before the preview card opens or closes.</summary>
    /// <value>
    /// Called with the requested open state and its details (the reason, the event and the trigger); call
    /// <c>Details.Cancel()</c> to keep the current state.
    /// </value>
    [Parameter]
    public EventCallback<ValueChangeEventArgs<bool>> OnOpenChange { get; set; }

    /// <summary>Gets or sets the callback called after the popup's enter or exit transition finished.</summary>
    /// <value>Called with <see langword="true"/> after opening and <see langword="false"/> after closing.</value>
    [Parameter]
    public EventCallback<bool> OnOpenChangeComplete { get; set; }

    /// <summary>Gets or sets the id of the trigger the preview card is anchored to, when you control it.</summary>
    /// <value>
    /// A trigger's id, or <see langword="null"/> for none; supplying it at the first render makes the active trigger yours.
    /// Read the trigger of a change from <c>Details.Trigger.Id</c> in <see cref="OnOpenChange"/>.
    /// </value>
    [Parameter]
    public string? TriggerId { get; set; }

    /// <summary>Gets or sets the id of the trigger the preview card is anchored to at its first render, when you do not control it.</summary>
    /// <value>A trigger's id, or <see langword="null"/> (the default) for none.</value>
    [Parameter]
    public string? DefaultTriggerId { get; set; }

    /// <summary>Gets or sets the handle that connects detached triggers and opens and closes the preview card from code.</summary>
    /// <value>A <see cref="PreviewCardHandle{TPayload}"/> created with <see langword="new"/>, or <see langword="null"/>.</value>
    [Parameter]
    public PreviewCardHandle<TPayload>? Handle { get; set; }

    /// <summary>Gets or sets the content.</summary>
    /// <value>The child content, or <see langword="null"/>.</value>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>Gets or sets the content rendered from the active trigger's payload.</summary>
    /// <value>A template that receives the payload, or its default while no trigger with a payload is active; or <see langword="null"/>.</value>
    [Parameter]
    public RenderFragment<TPayload?>? PayloadContent { get; set; }

    /// <summary>Gets the store.</summary>
    internal PreviewCardStore<TPayload> Store => behavior.Store;

    // For the fixtures (tests/Blazix.Primitives.Fixtures.Client).
    /// <summary>Gets a task that completes when the session handle is attached.</summary>
    internal Task SessionAttached => session?.TwinAttached ?? Task.CompletedTask;

    /// <inheritdoc />
    public override Task SetParametersAsync(ParameterView parameters)
    {
        behavior.SetParameters(parameters, Ids, Logger);
        if (session is null)
        {
            PreviewCardStore<TPayload> store = behavior.Store;
            context = new PreviewCardRootContext<TPayload>(store);
            session = new InteractionsSessionBehavior(JSRuntime, store.SessionToken!, RouteAsync, DispatchExceptionAsync);

            // The commit path (ADR 01 Amendments 2026-10-27): a committed change reaches the session without a render of
            // the root, which renders only at a batch boundary the store requests.
            committed = new SelectorSubscription<PreviewCardStoreState<TPayload>>(store, OnCommitted);
            committed.Select(PopupSelectors<PreviewCardStoreState<TPayload>, TPayload>.Open);
            committed.Select(PopupSelectors<PreviewCardStoreState<TPayload>, TPayload>.Mounted);
            committed.Select(PopupSelectors<PreviewCardStoreState<TPayload>, TPayload>.ActiveTriggerId);
            committed.Select(PopupSelectors<PreviewCardStoreState<TPayload>, TPayload>.OpenChange);
            committed.Select(PopupSelectors<PreviewCardStoreState<TPayload>, TPayload>.TransitionStatus);
            committed.Select(PopupSelectors<PreviewCardStoreState<TPayload>, TPayload>.PopupId);
        }

        return base.SetParametersAsync(parameters);
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        PreviewCardStore<TPayload> store = behavior.Store;

        behavior.ApplyParameters(new PopupRootParameters<TPayload>(
            DefaultOpen,
            DefaultTriggerId,
            Handle,
            OnOpenChange,
            OpenChanged,
            OnOpenChangeComplete,
            CloseOnActiveTriggerUnmount: true,
            ChildContent,
            PayloadContent));
        session!.ParentToken = EnclosingRoot?.SessionToken;
    }

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.OpenComponent<CascadingValue<PreviewCardRootContext>>(0);
        builder.AddComponentParameter(1, nameof(CascadingValue<PreviewCardRootContext>.Value), context);
        builder.AddComponentParameter(2, nameof(CascadingValue<PreviewCardRootContext>.IsFixed), true);
        builder.AddComponentParameter(3, nameof(CascadingValue<PreviewCardRootContext>.ChildContent), storeContent);
        builder.CloseComponent();
    }

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        PreviewCardStore<TPayload> store = behavior.Store;
        rendered = true;
        CopySessionInputs();
        Task sync = session!.AfterRenderAsync();
        await behavior.OnAfterRenderAsync();
        await sync;
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        behavior.Dispose();
        committed?.Dispose();
        if (session is not null)
        {
            await session.DisposeAsync();
        }
    }

    /// <summary>Closes the preview card, as Base UI's <c>actionsRef.close()</c> does.</summary>
    /// <returns>A task that completes when the close request has run.</returns>
    /// <remarks>The request carries the reason <see cref="EventReason.ImperativeAction"/>, so <see cref="OnOpenChange"/> can cancel it.</remarks>
    public Task CloseAsync() => behavior.Store.CloseAsync();

    /// <summary>Ends the closing phase now, as Base UI's <c>actionsRef.unmount()</c> does.</summary>
    /// <remarks>
    /// Removes the popup (or hides it when its portal keeps it mounted) and calls <see cref="OnOpenChangeComplete"/> with
    /// <see langword="false"/>. Called while open, it waits for the end of the current render.
    /// </remarks>
    public void Unmount() => _ = behavior.Store.UnmountAsync();

    private Task RouteAsync(OpenChangeRequest request)
    {
        PreviewCardStore<TPayload> store = behavior.Store;
        if (request.InteractionType is { } interactionType)
        {
            store.SetOpenInteractionType(interactionType);
        }

        return store.RequestOpenAsync(request.Open, request.Details);
    }

    private void OnCommitted()
    {
        PreviewCardStore<TPayload> store = behavior.Store;
        PopupStoreState<TPayload> popup = store.Popup;

        // PRV-STA-12 (PreviewCardRoot.tsx:64-70): open with no active trigger clears the payload.
        if (popup.EffectiveOpen && popup.EffectiveActiveTriggerId is null && store.HasPayload)
        {
            store.ClearPayload();
        }

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
        session.ActiveTriggerId = popup.Mounted ? popup.EffectiveActiveTriggerId : null;
        session.FloatingId = popup.PopupId;
        session.Ending = popup.DerivedTransitionStatus == TransitionStatus.Ending;
        session.Relay = popup.OpenChange;

        // PRV-STA-16 (PreviewCardRoot.tsx:84-109): Escape and the outside press while open, mounted or given a Handle.
        bool interactions = popup.EffectiveOpen || popup.Mounted || Handle is not null;
        session.Dismiss = interactions ? s_dismiss : null;
    }
}

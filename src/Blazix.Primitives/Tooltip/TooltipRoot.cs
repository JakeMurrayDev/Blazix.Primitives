using Blazix.Primitives.Interop;
using Blazix.Primitives.Rendering;
using Blazix.Primitives.Store;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace Blazix.Primitives;

// Base UI's Tooltip.Root (tooltip/root/TooltipRoot.tsx); the contract is docs/components/tooltip.md §3.2 and §4.
// - The store, its handle attachment and its render-batch boundary are PopupRootBehavior's (M4 track 1). The root renders
//   no element (TIP-STR-02) and cascades, fixed, its store (for the triggers) and the non-generic TooltipRootContext (for
//   the parts below it).
// - The root's `disabled` keeps the tooltip effectively closed (TIP-STA-02; the shared record's Disabled, ADR 05 Amendments
//   2026-10-28, settled by the orchestrator 2026-10-28), written before the open parameters so a root disabled from its first render never
//   mounts (TIP-EDGE-10); the disabled close runs at the batch boundary (TIP-DIS-06; ADR 01 decision 4's C# state row).
// - The session (M4 track 3, design point 8: InteractionsSessionBehavior in Floating/) joins the store's token and holds
//   Escape and the sloppy outside press while TIP-STA-15's condition holds (no handle term; PSH-STA-43's Tooltip
//   exception). The committed state reaches it from the commit path (ADR 01 Amendments 2026-10-27); FixturePopupRoot of
//   the fixture project is the shape. Tooltip takes no part in the popup tree (TIP-DIS-08): no parent token.
// - The payload clear (TIP-STA-10) runs in the commit path, so the payload template never renders a stale payload.
/// <summary>
/// Groups all parts of a tooltip and owns its open state: Base UI's <c>Tooltip.Root</c>. It renders no element.
/// </summary>
/// <typeparam name="TPayload">
/// The type of the payload the tooltip's triggers carry; write <c>TPayload="NoPayload"</c> for a tooltip without payloads.
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
public sealed class TooltipRoot<TPayload> : ComponentBase, IAsyncDisposable
{
    private static readonly InteractionsDismissOptions s_dismiss = new();

    private readonly PopupRootBehavior<TooltipStore<TPayload>, TooltipStoreState<TPayload>, TPayload> behavior;
    private readonly RenderFragment storeContent;
    private TooltipRootContext<TPayload>? context;
    private InteractionsSessionBehavior? session;
    private SelectorSubscription<TooltipStoreState<TPayload>>? committed;
    private bool rendered;
    private bool? closedForDisabled;

    /// <summary>Initializes a new instance of the <see cref="TooltipRoot{TPayload}"/> class.</summary>
    public TooltipRoot()
    {
        behavior = new((popupId, token, logger) => new TooltipStore<TPayload>(popupId, token, logger: logger), StateHasChanged);
        storeContent = behavior.BuildRenderTree;
    }

    [Inject]
    private PartIdGenerator Ids { get; set; } = null!;

    [Inject]
    private ILogger<TooltipRoot<TPayload>> Logger { get; set; } = null!;

    [Inject]
    private IJSRuntime JSRuntime { get; set; } = null!;

    /// <summary>Gets or sets a value indicating whether the tooltip is open, when you control it.</summary>
    /// <value>The open state; supplying it at the first render makes the open state yours, with <see cref="OnOpenChange"/> or <c>@bind-Open</c>.</value>
    [Parameter]
    public bool Open { get; set; }

    /// <summary>Gets or sets the callback <c>@bind-Open</c> uses.</summary>
    /// <value>Called with the new open state after <see cref="OnOpenChange"/> accepted it.</value>
    [Parameter]
    public EventCallback<bool> OpenChanged { get; set; }

    /// <summary>Gets or sets a value indicating whether the tooltip is open at its first render, when you do not control it.</summary>
    /// <value><see langword="true"/> to open it initially. The default is <see langword="false"/>; later changes are ignored.</value>
    [Parameter]
    public bool DefaultOpen { get; set; }

    /// <summary>Gets or sets the callback called before the tooltip opens or closes.</summary>
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

    /// <summary>Gets or sets the id of the trigger the tooltip is anchored to, when you control it.</summary>
    /// <value>
    /// A trigger's id, or <see langword="null"/> for none; supplying it at the first render makes the active trigger yours.
    /// Read the trigger of a change from <c>Details.Trigger.Id</c> in <see cref="OnOpenChange"/>.
    /// </value>
    [Parameter]
    public string? TriggerId { get; set; }

    /// <summary>Gets or sets the id of the trigger the tooltip is anchored to at its first render, when you do not control it.</summary>
    /// <value>A trigger's id, or <see langword="null"/> (the default) for none.</value>
    [Parameter]
    public string? DefaultTriggerId { get; set; }

    /// <summary>Gets or sets the handle that connects detached triggers and opens and closes the tooltip from code.</summary>
    /// <value>A <see cref="TooltipHandle{TPayload}"/> created with <see langword="new"/>, or <see langword="null"/>.</value>
    [Parameter]
    public TooltipHandle<TPayload>? Handle { get; set; }

    /// <summary>Gets or sets a value indicating whether the tooltip is disabled.</summary>
    /// <value>
    /// <see langword="true"/> to keep the tooltip closed and disable every trigger that does not set its own
    /// <c>Disabled</c>; an open tooltip closes. The default is <see langword="false"/>.
    /// </value>
    [Parameter]
    public bool Disabled { get; set; }

    /// <summary>Gets or sets a value indicating whether the popup cannot be hovered.</summary>
    /// <value>
    /// <see langword="true"/> to close the tooltip when the pointer leaves the trigger, even toward the popup, and to give
    /// the positioner <c>pointer-events: none</c>. The default is <see langword="false"/>.
    /// </value>
    [Parameter]
    public bool DisableHoverablePopup { get; set; }

    /// <summary>Gets or sets the axes along which the popup follows the pointer instead of its trigger.</summary>
    /// <value>The axes. The default is <see cref="TooltipTrackCursorAxis.None"/>; <see cref="TooltipTrackCursorAxis.Both"/> also makes the popup unhoverable.</value>
    [Parameter]
    public TooltipTrackCursorAxis TrackCursorAxis { get; set; } = TooltipTrackCursorAxis.None;

    /// <summary>Gets or sets the content.</summary>
    /// <value>The child content, or <see langword="null"/>.</value>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>Gets or sets the content rendered from the active trigger's payload.</summary>
    /// <value>A template that receives the payload, or its default while no trigger with a payload is active; or <see langword="null"/>.</value>
    [Parameter]
    public RenderFragment<TPayload?>? PayloadContent { get; set; }

    /// <summary>Gets the store.</summary>
    internal TooltipStore<TPayload> Store => behavior.Store;

    // For the fixtures (tests/Blazix.Primitives.Fixtures.Client).
    /// <summary>Gets a task that completes when the session handle is attached.</summary>
    internal Task SessionAttached => session?.TwinAttached ?? Task.CompletedTask;

    /// <inheritdoc />
    public override Task SetParametersAsync(ParameterView parameters)
    {
        behavior.SetParameters(parameters, Ids, Logger);
        if (session is null)
        {
            TooltipStore<TPayload> store = behavior.Store;
            context = new TooltipRootContext<TPayload>(store);
            session = new InteractionsSessionBehavior(JSRuntime, store.SessionToken!, RouteAsync, DispatchExceptionAsync);

            // The commit path (ADR 01 Amendments 2026-10-27): a committed change reaches the session without a render of
            // the root, which renders only at a batch boundary the store requests.
            committed = new SelectorSubscription<TooltipStoreState<TPayload>>(store, OnCommitted);
            committed.Select(PopupSelectors<TooltipStoreState<TPayload>, TPayload>.Open);
            committed.Select(PopupSelectors<TooltipStoreState<TPayload>, TPayload>.Mounted);
            committed.Select(PopupSelectors<TooltipStoreState<TPayload>, TPayload>.ActiveTriggerId);
            committed.Select(PopupSelectors<TooltipStoreState<TPayload>, TPayload>.OpenChange);
            committed.Select(PopupSelectors<TooltipStoreState<TPayload>, TPayload>.TransitionStatus);
            committed.Select(PopupSelectors<TooltipStoreState<TPayload>, TPayload>.PopupId);
            committed.Select(TooltipSelectors<TPayload>.Disabled);
            committed.Select(TooltipSelectors<TPayload>.TrackCursorAxis);
        }

        return base.SetParametersAsync(parameters);
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        TooltipStore<TPayload> store = behavior.Store;

        // TIP-STA-16 (useSyncedValues). Disabled first: the first ApplyRootParameters seeds the mounted state from it.
        store.SetDisabled(Disabled);
        store.SetRootInputs(TrackCursorAxis, DisableHoverablePopup);
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
    }

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.OpenComponent<CascadingValue<TooltipRootContext>>(0);
        builder.AddComponentParameter(1, nameof(CascadingValue<TooltipRootContext>.Value), context);
        builder.AddComponentParameter(2, nameof(CascadingValue<TooltipRootContext>.IsFixed), true);
        builder.AddComponentParameter(3, nameof(CascadingValue<TooltipRootContext>.ChildContent), storeContent);
        builder.CloseComponent();
    }

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        TooltipStore<TPayload> store = behavior.Store;
        rendered = true;
        CopySessionInputs();
        Task sync = session!.AfterRenderAsync();
        await behavior.OnAfterRenderAsync();
        await CloseIfDisabledAsync(store);
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

    /// <summary>Closes the tooltip, as Base UI's <c>actionsRef.close()</c> does.</summary>
    /// <returns>A task that completes when the close request has run.</returns>
    /// <remarks>The request carries the reason <see cref="EventReason.ImperativeAction"/>, so <see cref="OnOpenChange"/> can cancel it.</remarks>
    public Task CloseAsync() => behavior.Store.CloseAsync();

    /// <summary>Ends the closing phase now, as Base UI's <c>actionsRef.unmount()</c> does.</summary>
    /// <remarks>
    /// Removes the popup (or hides it when its portal keeps it mounted) and calls <see cref="OnOpenChangeComplete"/> with
    /// <see langword="false"/>. Called while open, it waits for the end of the current render.
    /// </remarks>
    public void Unmount() => _ = behavior.Store.UnmountAsync();

    // TIP-DIS-06 (TooltipRoot.tsx:96-100): post-commit, once per change of the stored open state or of `disabled`; the
    // close carries no event and no trigger.
    private Task CloseIfDisabledAsync(TooltipStore<TPayload> store)
    {
        PopupStoreState<TPayload> popup = store.Popup;
        bool storedOpen = popup.OpenProp ?? popup.Open;
        bool closing = storedOpen && popup.Disabled;
        if (closedForDisabled == closing)
        {
            return Task.CompletedTask;
        }

        closedForDisabled = closing;
        return closing ? store.RequestOpenAsync(false, new OpenChangeEventDetails(EventReason.Disabled)) : Task.CompletedTask;
    }

    private Task RouteAsync(OpenChangeRequest request)
    {
        TooltipStore<TPayload> store = behavior.Store;
        if (request.InteractionType is { } interactionType)
        {
            store.SetOpenInteractionType(interactionType);
        }

        return store.RequestAsync(request);
    }

    private void OnCommitted()
    {
        TooltipStore<TPayload> store = behavior.Store;
        PopupStoreState<TPayload> popup = store.Popup;

        // TIP-STA-10 (TooltipRoot.tsx:120-126): open with no active trigger clears the payload.
        if (popup.EffectiveOpen && popup.EffectiveActiveTriggerId is null && store.HasPayload)
        {
            store.ClearPayload();
        }

        if (rendered)
        {
            CopySessionInputs();
            _ = session!.AfterRenderAsync();

            // A request applied while disabled (a trigger that opted back in, TIP §16 Q6) commits the stored open state
            // without a render of the root.
            _ = ObserveAsync(CloseIfDisabledAsync(store));
        }
    }

    private async Task ObserveAsync(Task task)
    {
        try
        {
            await task;
        }
        catch (Exception exception) when (!task.IsCanceled)
        {
            await DispatchExceptionAsync(exception);
        }
    }

    private void CopySessionInputs()
    {
        TooltipStoreState<TPayload> state = behavior.Store.State;
        PopupStoreState<TPayload> popup = state.Popup;
        session!.Open = popup.EffectiveOpen;
        session.ActiveTriggerId = popup.Mounted ? popup.EffectiveActiveTriggerId : null;
        session.FloatingId = popup.PopupId;
        session.Ending = popup.DerivedTransitionStatus == TransitionStatus.Ending;
        session.Relay = popup.OpenChange;

        // TIP-STA-15 (TooltipRoot.tsx:137, 260-263): Escape and the outside press while open, mounted or tracking, never
        // while disabled.
        bool interactions = popup.EffectiveOpen || popup.Mounted || (!popup.Disabled && state.TrackCursorAxis != TooltipTrackCursorAxis.None);
        session.Dismiss = interactions && !popup.Disabled ? s_dismiss : null;
    }
}

using Blazix.Primitives.Interop;
using Blazix.Primitives.Rendering;
using Blazix.Primitives.Store;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace Blazix.Primitives;

// Base UI's Popover.Root (popover/root/PopoverRoot.tsx); the contract is docs/components/popover.md §3.1, §4, §6.4.
// - An elementless ComponentBase (the DirectionProvider precedent): it composes PopupRootBehavior (the store, created at
//   the first parameter set with the popup id and the session token from PartIdGenerator; the handle; the render-batch
//   boundary) and cascades, fixed, its store (for the Triggers) and the non-generic PopoverRootContext (for every other
//   part; ADR 08 Amendments 2026-10-27, issue 116's finding: only the Root and the Trigger are generic).
// - The `interactions` session by token (ADR 04 Amendments 2026-10-27): Escape with Prevent, outside press { mouse:
//   intentional, or sloppy while Modal is trap-focus; touch: sloppy } (POP-DIS-01), no bubbles (POP-KEY-04); a Root inside
//   another popover's Root joins its tree (ParentToken, POP-STR-02). The dismissal is on while open, mounted or given a
//   Handle (POP-ANI-11; PopoverInteractions' shouldRenderInteractions).
// - The commit path (ADR 01 Amendments 2026-10-27): a committed change reaches the session without a render of the Root,
//   which renders only at a batch boundary the store asks for (the FixturePopupRoot shape of M4 track 3).
/// <summary>
/// Groups all parts of a popover and owns its open state, Base UI's <c>Popover.Root</c>. It renders no element.
/// </summary>
/// <typeparam name="TPayload">
/// The type of the payload the popover's triggers carry; write <c>TPayload="NoPayload"</c> for a popover whose triggers
/// carry none, or let a <see cref="Handle"/> infer it.
/// </typeparam>
/// <remarks>
/// <para>
/// <see cref="OnOpenChange"/> receives a <see cref="ValueChangeEventArgs{TValue}"/> whose <c>Details</c> is an
/// <see cref="OpenChangeEventDetails"/> at run time: cast it, as in
/// <c>((OpenChangeEventDetails)args.Details).PreventUnmountOnClose()</c>, to keep the popover mounted until
/// <see cref="Unmount"/> is called.
/// </para>
/// <para>
/// Two nested roots that both use <see cref="PayloadContent"/> must each name its parameter with <c>Context</c>, such as
/// <c>&lt;PayloadContent Context="payload"&gt;</c>, or the page fails to compile.
/// </para>
/// </remarks>
[CascadingTypeParameter(nameof(TPayload))]
public sealed class PopoverRoot<TPayload> : ComponentBase, IAsyncDisposable
{
    private static readonly InteractionsDismissOptions s_intentional = new() { OutsidePressEvent = new InteractionsPressEvents("intentional", "sloppy") };
    private static readonly InteractionsDismissOptions s_sloppy = new() { OutsidePressEvent = new InteractionsPressEvents("sloppy", "sloppy") };

    private readonly PopupRootBehavior<PopoverStore<TPayload>, PopoverStoreState<TPayload>, TPayload> behavior;
    private readonly RenderFragment content;
    private PopoverRootContext<TPayload>? context;
    private InteractionsSessionBehavior? session;
    private SelectorSubscription<PopoverStoreState<TPayload>>? committed;
    private bool rendered;

    /// <summary>Initializes a new instance of the <see cref="PopoverRoot{TPayload}"/> class.</summary>
    public PopoverRoot()
    {
        behavior = new((popupId, token, logger) => new PopoverStore<TPayload>(popupId, token, logger: logger), StateHasChanged);
        content = behavior.BuildRenderTree;
    }

    [Inject]
    private PartIdGenerator Ids { get; set; } = null!;

    [Inject]
    private ILogger<PopoverRoot<TPayload>> Logger { get; set; } = null!;

    [Inject]
    private IJSRuntime JSRuntime { get; set; } = null!;

    [CascadingParameter]
    private PopoverRootContext? EnclosingRoot { get; set; }

    // POP-API-01
    /// <summary>Gets or sets a value indicating whether the popover is open.</summary>
    /// <value>
    /// The open state the consumer owns, when supplied at the first render; then a trigger press or a dismissal only
    /// reports through <see cref="OnOpenChange"/> and <see cref="OpenChanged"/>. Use <c>@bind-Open</c> to follow them.
    /// </value>
    [Parameter]
    public bool Open { get; set; }

    /// <summary>Gets or sets the callback that receives the requested open state, for <c>@bind-Open</c>.</summary>
    /// <value>Invoked after <see cref="OnOpenChange"/> when the change is not canceled.</value>
    [Parameter]
    public EventCallback<bool> OpenChanged { get; set; }

    // POP-API-02
    /// <summary>Gets or sets a value indicating whether the popover is open at its first render, while <see cref="Open"/> is not supplied.</summary>
    /// <value><see langword="false"/> by default.</value>
    [Parameter]
    public bool DefaultOpen { get; set; }

    // POP-API-03
    /// <summary>Gets or sets the callback invoked before the popover opens or closes.</summary>
    /// <value>
    /// Receives the requested state and an <see cref="OpenChangeEventDetails"/> as its <c>Details</c>; calling
    /// <c>Details.Cancel()</c> keeps the current state.
    /// </value>
    [Parameter]
    public EventCallback<ValueChangeEventArgs<bool>> OnOpenChange { get; set; }

    // POP-API-04
    /// <summary>Gets or sets the callback invoked after the open or close transition finishes.</summary>
    /// <value>Receives <see langword="true"/> after opening and <see langword="false"/> after closing.</value>
    [Parameter]
    public EventCallback<bool> OnOpenChangeComplete { get; set; }

    // POP-API-06
    /// <summary>Gets or sets a value that determines whether the popover is modal while open.</summary>
    /// <value>
    /// <see langword="true"/> locks page scroll and blocks presses outside the popover, and traps focus while a
    /// <c>PopoverClose</c> is inside the popup; <see cref="ModalValue.TrapFocus"/> only traps focus;
    /// <see langword="false"/> or <see langword="null"/>, the default, leaves the page interactive.
    /// </value>
    [Parameter]
    public ModalValue? Modal { get; set; }

    // POP-API-07
    /// <summary>Gets or sets the id of the trigger the popover is associated with, while the consumer owns it.</summary>
    /// <value>
    /// A trigger's id, or <see langword="null"/> for no trigger; supplying it at the first render makes the consumer own
    /// the active trigger, which is useful with <see cref="Open"/>.
    /// </value>
    [Parameter]
    public string? TriggerId { get; set; }

    // POP-API-08
    /// <summary>Gets or sets the id of the trigger the popover is associated with at its first render.</summary>
    /// <value>A trigger's id, or <see langword="null"/>, the default; useful with <see cref="DefaultOpen"/>.</value>
    [Parameter]
    public string? DefaultTriggerId { get; set; }

    // POP-API-09
    /// <summary>Gets or sets the handle that connects triggers rendered outside the root and opens the popover from code.</summary>
    /// <value>A <see cref="PopoverHandle{TPayload}"/>, or <see langword="null"/>.</value>
    [Parameter]
    public PopoverHandle<TPayload>? Handle { get; set; }

    /// <summary>Gets or sets the content of the popover.</summary>
    /// <value>The triggers and the portal, or <see langword="null"/>.</value>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    // POP-API-10
    /// <summary>Gets or sets the content rendered with the payload of the active trigger.</summary>
    /// <value>
    /// A template that receives the active trigger's payload, or <see langword="null"/>; it renders after
    /// <see cref="ChildContent"/>.
    /// </value>
    [Parameter]
    public RenderFragment<TPayload?>? PayloadContent { get; set; }

    /// <summary>Gets a task that completes when the popover's session is attached in the browser.</summary>
    internal Task SessionAttached => session?.TwinAttached ?? Task.CompletedTask;

    /// <summary>Gets the root's store.</summary>
    internal PopoverStore<TPayload> Store => behavior.Store;

    /// <summary>Gets the number of option records the session has sent.</summary>
    internal int SessionSends => session?.Sends ?? 0;

    /// <inheritdoc />
    public override Task SetParametersAsync(ParameterView parameters)
    {
        behavior.SetParameters(parameters, Ids, Logger);
        if (session is null)
        {
            PopoverStore<TPayload> store = behavior.Store;
            context = new PopoverRootContext<TPayload>(store);
            session = new InteractionsSessionBehavior(JSRuntime, store.SessionToken!, RouteAsync, DispatchExceptionAsync);
            committed = new SelectorSubscription<PopoverStoreState<TPayload>>(store, OnCommitted);
            committed.Select(PopoverSelectors.Open);
            committed.Select(PopoverSelectors.Mounted);
            committed.Select(PopoverSelectors.ActiveTriggerId);
            committed.Select(PopoverSelectors.OpenChange);
            committed.Select(PopoverSelectors.TransitionStatus);
            committed.Select(PopoverSelectors.PopupId);
            committed.Select(PopoverSelectors.Modal);
        }

        return base.SetParametersAsync(parameters);
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        behavior.ApplyParameters(new PopupRootParameters<TPayload>(
            DefaultOpen,
            DefaultTriggerId,
            Handle,
            OnOpenChange,
            OpenChanged,
            OnOpenChangeComplete,
            CloseOnActiveTriggerUnmount: false,
            ChildContent,
            PayloadContent));
        context!.Handled = Handle is not null;
        behavior.Store.SetModal(ModalKind(Modal));
        session!.ParentToken = EnclosingRoot?.SessionToken;
    }

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.OpenComponent<CascadingValue<PopoverRootContext>>(0);
        builder.AddComponentParameter(1, nameof(CascadingValue<PopoverRootContext>.Value), context);
        builder.AddComponentParameter(2, nameof(CascadingValue<PopoverRootContext>.IsFixed), true);
        builder.AddComponentParameter(3, nameof(CascadingValue<PopoverRootContext>.ChildContent), content);
        builder.CloseComponent();
    }

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
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

    // POP-API-05 (actionsRef.close).
    /// <summary>Closes the popover, with the reason <see cref="EventReason.ImperativeAction"/> and no trigger.</summary>
    /// <returns>A task that completes when the close request has run.</returns>
    public Task CloseAsync() => InvokeAsync(() => behavior.Store.CloseAsync());

    // POP-API-05 (actionsRef.unmount), POP-ANI-05.
    /// <summary>
    /// Ends the closing phase of a popover whose close called <c>PreventUnmountOnClose()</c>, once its own exit animation
    /// has finished.
    /// </summary>
    /// <remarks>Whether the popup then leaves the page is decided by the portal's <c>KeepMounted</c>.</remarks>
    public void Unmount()
    {
        Task task = InvokeAsync(() => behavior.Store.UnmountAsync());
        if (!task.IsCompletedSuccessfully)
        {
            _ = task.ContinueWith(t => DispatchExceptionAsync(t.Exception!.GetBaseException()), CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default);
        }
    }

    private static PopoverModalKind ModalKind(ModalValue? modal)
    {
        if (modal is not { } value)
        {
            return PopoverModalKind.NonModal;
        }

        if (value.IsTrapFocus)
        {
            return PopoverModalKind.TrapFocus;
        }

        return value.TryGetValue(out bool on) && on ? PopoverModalKind.Modal : PopoverModalKind.NonModal;
    }

    // An interactions request (a press, a hover, Escape, an outside press, a focus-out): a press's interaction type is the
    // open method (PSH-PTR-01/02), written only while closed.
    private Task RouteAsync(OpenChangeRequest request)
    {
        PopoverStore<TPayload> store = behavior.Store;
        if (request.InteractionType is { } type)
        {
            store.SetOpenInteractionType(type);
        }

        return store.RequestOpenAsync(request.Open, request.Details);
    }

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
        PopoverStoreState<TPayload> state = behavior.Store.State;
        PopupStoreState<TPayload> popup = state.Popup;
        session!.Open = popup.EffectiveOpen;
        session.ActiveTriggerId = popup.Mounted || popup.EffectiveOpen ? popup.EffectiveActiveTriggerId : null;
        session.FloatingId = popup.PopupId;
        session.Ending = popup.DerivedTransitionStatus == TransitionStatus.Ending;
        session.Relay = popup.OpenChange;
        bool dismissible = popup.EffectiveOpen || popup.Mounted || Handle is not null;
        session.Dismiss = dismissible ? (state.Modal == PopoverModalKind.TrapFocus ? s_sloppy : s_intentional) : null;
    }
}

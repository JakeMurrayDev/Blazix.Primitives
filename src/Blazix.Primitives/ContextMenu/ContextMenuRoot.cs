using Blazix.Primitives.Interop;
using Blazix.Primitives.Rendering;
using Blazix.Primitives.Store;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace Blazix.Primitives;

// Base UI's ContextMenu.Root (context-menu/root/ContextMenuRoot.tsx); the contract is docs/components/context-menu.md §3.1,
// §4. Decision 12 of the M5 plan and ADR 08 Amendments 2026-11-03 (3) (chosen by the user 2026-11-03): an elementless
// ComponentBase over MenuRootBehavior<NoPayload>("ContextMenu"), never a MenuRoot (ADR 16 rule 10). It resolves its menu's
// parent from its own ContextMenuRootContext with no enclosing menu (CTX-STR-03, Base UI's `MenuRootContext.Provider
// value={undefined}`), so Menu's behaviours take the context-menu branches: modal (CTX-STA-05), the 500 ms grace (DIS-01),
// no arrow-key open, return focus without a trigger, `fixed` positioning. No Modal, Handle, TriggerId, DefaultTriggerId,
// PayloadContent or CloseParentOnEsc (ADR 08 Amendments 2026-11-03 (3); CTX-API-03..05, EDGE-03). The root's handle on the
// `context-menu` module (design points 1 (a), 2 (a) of M5 track 7) arms the release, sets the origin anchor and receives
// the backdrops and the root Positioner.
/// <summary>
/// Groups all parts of a context menu, which opens on a right click or a long press on its trigger, Base UI's
/// <c>ContextMenu.Root</c>. It renders no element.
/// </summary>
/// <remarks>
/// <see cref="OnOpenChange"/> receives a <see cref="ValueChangeEventArgs{TValue}"/> whose <c>Details</c> is an
/// <see cref="OpenChangeEventDetails"/> at run time: cast it, as in
/// <c>((OpenChangeEventDetails)args.Details).PreventUnmountOnClose()</c>, to keep the menu mounted until
/// <see cref="Unmount"/> is called.
/// </remarks>
public sealed class ContextMenuRoot : ComponentBase, IAsyncDisposable
{
    private MenuRootBehavior<NoPayload>? behavior;
    private ContextMenuRootContext? context;
    private RenderFragment? behaviorContent;
    private SelectorSubscription<MenuStoreState<NoPayload>>? positionerSubscription;
    private ContextMenuTwin? twin;
    private TwinHandle<ContextMenuOptions>? handle;
    private ElementReference? sentBackdrop;
    private ElementReference? sentInternalBackdrop;
    private ElementReference? sentPositioner;
    private bool attaching;
    private bool rendered;
    private bool disposed;

    [Inject]
    private PartIdGenerator Ids { get; set; } = null!;

    [Inject]
    private ILogger<ContextMenuRoot> Logger { get; set; } = null!;

    [Inject]
    private IJSRuntime JSRuntime { get; set; } = null!;

    [Inject]
    private PlatformFlags Platform { get; set; } = null!;

    [CascadingParameter]
    private IFloatingNodeContext? FloatingParent { get; set; }

    [CascadingParameter]
    private IMenuParentContext? EnclosingParent { get; set; }

    [CascadingParameter]
    private DirectionProviderContext? DirectionContext { get; set; }

    // CTX-API-01 (MNU-API-01)
    /// <summary>Gets or sets a value indicating whether the context menu is open.</summary>
    /// <value>
    /// The open state the consumer owns, when supplied at the first render; then a gesture or a dismissal only reports
    /// through <see cref="OnOpenChange"/> and <see cref="OpenChanged"/>. Use <c>@bind-Open</c> to follow them.
    /// </value>
    [Parameter]
    public bool Open { get; set; }

    /// <summary>Gets or sets the callback that receives the requested open state, for <c>@bind-Open</c>.</summary>
    /// <value>Invoked after <see cref="OnOpenChange"/> when the change is not canceled.</value>
    [Parameter]
    public EventCallback<bool> OpenChanged { get; set; }

    // CTX-API-01 (MNU-API-02)
    /// <summary>Gets or sets a value indicating whether the context menu is open at its first render, while <see cref="Open"/> is not supplied.</summary>
    /// <value><see langword="false"/> by default.</value>
    [Parameter]
    public bool DefaultOpen { get; set; }

    // CTX-API-02
    /// <summary>Gets or sets the callback invoked before the context menu opens or closes.</summary>
    /// <value>
    /// Receives the requested state and an <see cref="OpenChangeEventDetails"/> as its <c>Details</c>; calling
    /// <c>Details.Cancel()</c> keeps the current state.
    /// </value>
    [Parameter]
    public EventCallback<ValueChangeEventArgs<bool>> OnOpenChange { get; set; }

    // CTX-API-01 (MNU-API-04)
    /// <summary>Gets or sets the callback invoked after the open or close transition finishes.</summary>
    /// <value>Receives <see langword="true"/> after opening and <see langword="false"/> after closing.</value>
    [Parameter]
    public EventCallback<bool> OnOpenChangeComplete { get; set; }

    // CTX-API-01 (MNU-API-05); design point 5 (a) of M5 track 7: Menu's types.
    /// <summary>Gets or sets the callback invoked when an item is highlighted or the highlight is cleared.</summary>
    /// <value>
    /// Receives the highlighted item's rendered id, or <see langword="null"/>, and a <see cref="MenuHighlightEventDetails"/>
    /// with the reason (<see cref="EventReason.Keyboard"/>, <see cref="EventReason.Pointer"/> or <see cref="EventReason.None"/>)
    /// and the item's label.
    /// </value>
    [Parameter]
    public EventCallback<MenuItemHighlightedEventArgs> OnItemHighlighted { get; set; }

    // CTX-API-01 (MNU-API-06)
    /// <summary>Gets or sets a value indicating whether the arrow keys wrap from the last item to the first and back.</summary>
    /// <value><see langword="true"/> by default.</value>
    [Parameter]
    public bool LoopFocus { get; set; } = true;

    // CTX-API-01 (MNU-API-07)
    /// <summary>Gets or sets a value indicating whether moving the pointer over items highlights them.</summary>
    /// <value>
    /// <see langword="true"/> by default; <see langword="false"/> lets CSS <c>:hover</c> differ from the
    /// <c>data-highlighted</c> state.
    /// </value>
    [Parameter]
    public bool HighlightItemOnHover { get; set; } = true;

    // CTX-API-01 (MNU-API-09)
    /// <summary>Gets or sets the visual orientation of the menu, which picks the arrow keys that move the highlight.</summary>
    /// <value><see cref="Orientation.Vertical"/> by default.</value>
    [Parameter]
    public Orientation Orientation { get; set; } = Orientation.Vertical;

    // CTX-API-01 (MNU-API-10), CTX-PTR-02
    /// <summary>Gets or sets a value indicating whether the context menu ignores user interaction.</summary>
    /// <value>
    /// <see langword="false"/> by default; when <see langword="true"/>, a right click or a long press on the trigger does
    /// not open the menu and the browser's own context menu appears.
    /// </value>
    [Parameter]
    public bool Disabled { get; set; }

    /// <summary>Gets or sets the content of the context menu.</summary>
    /// <value>The trigger and the portal, or <see langword="null"/>.</value>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>Gets a task that completes when the menu's session is attached in the browser.</summary>
    internal Task SessionAttached => behavior?.SessionAttached ?? Task.CompletedTask;

    /// <summary>Gets the root's store.</summary>
    internal MenuStore<NoPayload> Store => behavior!.Store;

    /// <summary>Gets the context the root cascades to its menu and its trigger.</summary>
    internal ContextMenuRootContext Context => context!;

    /// <inheritdoc />
    public override Task SetParametersAsync(ParameterView parameters)
    {
        if (behavior is null)
        {
            behavior = new MenuRootBehavior<NoPayload>("ContextMenu", isSubmenu: false, JSRuntime, Platform, StateHasChanged, InvokeAsync, DispatchExceptionAsync);
            behavior.SetParameters(parameters, Ids, Logger);
            context = new ContextMenuRootContext(Ids.Next()) { Menu = behavior.Context };
            context.ElementsChanged += OnElementsChanged;
            behaviorContent = behavior.BuildRenderTree;
            positionerSubscription = new SelectorSubscription<MenuStoreState<NoPayload>>(behavior.Store, OnElementsChanged);
            positionerSubscription.Select(static state => state.Popup.PositionerElement);
        }
        else
        {
            behavior.SetParameters(parameters, Ids, Logger);
        }

        return base.SetParametersAsync(parameters);
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        ContextMenuRootContext rootContext = context!;
        rootContext.Enclosing = EnclosingParent;

        // CTX-STR-03: no enclosing menu, so a context menu is never a submenu of a menu it is rendered in.
        MenuParent parent = MenuParent.Resolve(isSubmenu: false, enclosingMenu: null, rootContext);
        behavior!.ApplyParameters(
            new MenuRootParameters<NoPayload>(
                new PopupRootParameters<NoPayload>(DefaultOpen, DefaultTriggerId: null, Handle: null, OnOpenChange, OpenChanged, OnOpenChangeComplete, CloseOnActiveTriggerUnmount: true, ChildContent, PayloadContent: null),
                Modal: null,
                Disabled,
                LoopFocus,
                HighlightItemOnHover,
                Orientation,
                CloseParentOnEsc: false,
                OnItemHighlighted),
            parent,
            FloatingParent,
            DirectionContext);
    }

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.OpenComponent<CascadingValue<ContextMenuRootContext>>(0);
        builder.AddComponentParameter(1, nameof(CascadingValue<ContextMenuRootContext>.Value), context);
        builder.AddComponentParameter(2, nameof(CascadingValue<ContextMenuRootContext>.IsFixed), true);
        builder.AddComponentParameter(3, nameof(CascadingValue<ContextMenuRootContext>.ChildContent), behaviorContent);
        builder.CloseComponent();
    }

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        rendered = true;
        Task sync = SyncHandleAsync();
        await behavior!.OnAfterRenderAsync(firstRender);
        await sync;
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        positionerSubscription?.Dispose();
        if (context is not null)
        {
            context.ElementsChanged -= OnElementsChanged;
        }

        if (handle is not null)
        {
            await handle.DisposeAsync();
        }

        if (twin is not null)
        {
            await twin.DisposeAsync();
        }

        if (behavior is not null)
        {
            await behavior.DisposeAsync();
        }
    }

    // MNU-API-12 (actionsRef.close).
    /// <summary>Closes the context menu, with the reason <see cref="EventReason.ImperativeAction"/>.</summary>
    /// <returns>A task that completes when the close request has run.</returns>
    public Task CloseAsync() => InvokeAsync(() => behavior!.CloseAsync());

    // MNU-API-12 (actionsRef.unmount), MNU-ANI-10.
    /// <summary>
    /// Ends the closing phase of a context menu whose close called <c>PreventUnmountOnClose()</c>, once its own exit
    /// animation has finished.
    /// </summary>
    /// <remarks>Whether the popup then leaves the page is decided by the portal's <c>KeepMounted</c>.</remarks>
    public void Unmount()
    {
        Task task = InvokeAsync(() => behavior!.UnmountAsync());
        if (!task.IsCompletedSuccessfully)
        {
            _ = task.ContinueWith(t => DispatchExceptionAsync(t.Exception!.GetBaseException()), CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default);
        }
    }

    private void OnElementsChanged()
    {
        if (rendered && !disposed)
        {
            _ = InvokeAsync(async () =>
            {
                try
                {
                    await SyncHandleAsync();
                }
                catch (Exception exception)
                {
                    await DispatchExceptionAsync(exception);
                }
            });
        }
    }

    // Design points 1 (a) and 2 (a) of M5 track 7: the root's handle, by the session token, then the elements whenever one
    // changed. The comparison runs before the record is built (ADR 16 rule 7).
    private async Task SyncHandleAsync()
    {
        ContextMenuRootContext rootContext = context!;
        ElementReference? backdrop = rootContext.BackdropElement;
        ElementReference? internalBackdrop = rootContext.InternalBackdropElement;
        ElementReference? positioner = behavior!.Store.State.Popup.PositionerElement;
        if (disposed || attaching || (handle is not null && Same(backdrop, sentBackdrop) && Same(internalBackdrop, sentInternalBackdrop) && Same(positioner, sentPositioner)))
        {
            return;
        }

        try
        {
            TwinReferences references = new();
            ContextMenuOptions options = new()
            {
                Part = ContextMenuTwin.RootPart,
                Token = behavior.Store.SessionToken,
                Backdrop = backdrop is { } b ? references.Add(b) : null,
                InternalBackdrop = internalBackdrop is { } i ? references.Add(i) : null,
                Positioner = positioner is { } p ? references.Add(p) : null,
            };
            sentBackdrop = backdrop;
            sentInternalBackdrop = internalBackdrop;
            sentPositioner = positioner;
            if (handle is null)
            {
                attaching = true;
                try
                {
                    twin ??= new ContextMenuTwin(JSRuntime);
                    handle = await twin.AttachTokenAsync(behavior.Store.SessionToken!, options, references);
                }
                finally
                {
                    attaching = false;
                }

                // An element that changed while the attach was in flight is sent now.
                await SyncHandleAsync();
            }
            else
            {
                await handle.UpdateAsync(options, references);
            }
        }
        catch (JSDisconnectedException)
        {
            // The circuit went away; nothing is left to tell.
        }
    }

    private static bool Same(ElementReference? a, ElementReference? b) =>
        a is null ? b is null : b is not null && string.Equals(a.Value.Id, b.Value.Id, StringComparison.Ordinal);
}

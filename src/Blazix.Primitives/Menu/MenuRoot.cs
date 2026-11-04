using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace Blazix.Primitives;

// Base UI's Menu.Root (menu/root/MenuRoot.tsx); the contract is docs/components/menu.md §3.1, §4, §6. An elementless
// ComponentBase (the PopoverRoot precedent) over MenuRootBehavior, which ContextMenuRoot (track 7) and MenuSubmenuRoot
// compose too (decision 12 of the M5 plan; ADR 08 Amendments 2026-11-03 (3)). The parent kind is resolved from the
// enclosing menu (a submenu's only), menubar or context menu (MNU-STR-19). Modal is `bool?` and OpenOnHover's twin on the
// Trigger likewise (design point 1 (a), settled by the orchestrator 2026-11-04; ADR 08 Amendments 2026-11-03 (4)).
/// <summary>
/// Groups all parts of a menu and owns its open state, Base UI's <c>Menu.Root</c>. It renders no element.
/// </summary>
/// <typeparam name="TPayload">
/// The type of the payload the menu's triggers carry; write <c>TPayload="NoPayload"</c> for a menu whose triggers carry
/// none, or let a <see cref="Handle"/> infer it.
/// </typeparam>
/// <remarks>
/// <para>
/// <see cref="OnOpenChange"/> receives a <see cref="ValueChangeEventArgs{TValue}"/> whose <c>Details</c> is an
/// <see cref="OpenChangeEventDetails"/> at run time: cast it, as in
/// <c>((OpenChangeEventDetails)args.Details).PreventUnmountOnClose()</c>, to keep the menu mounted until
/// <see cref="Unmount"/> is called.
/// </para>
/// <para>
/// Two nested roots that both use <see cref="PayloadContent"/> must each name its parameter with <c>Context</c>, such as
/// <c>&lt;PayloadContent Context="payload"&gt;</c>, or the page fails to compile.
/// </para>
/// </remarks>
[CascadingTypeParameter(nameof(TPayload))]
public sealed class MenuRoot<TPayload> : ComponentBase, IAsyncDisposable
{
    private MenuRootBehavior<TPayload>? behavior;

    [Inject]
    private PartIdGenerator Ids { get; set; } = null!;

    [Inject]
    private ILogger<MenuRoot<TPayload>> Logger { get; set; } = null!;

    [Inject]
    private IJSRuntime JSRuntime { get; set; } = null!;

    [Inject]
    private PlatformFlags Platform { get; set; } = null!;

    [CascadingParameter]
    private IFloatingNodeContext? FloatingParent { get; set; }

    [CascadingParameter]
    private MenuRootContext? EnclosingMenu { get; set; }

    [CascadingParameter]
    private IMenuParentContext? ParentContext { get; set; }

    [CascadingParameter]
    private DirectionProviderContext? DirectionContext { get; set; }

    // MNU-API-01
    /// <summary>Gets or sets a value indicating whether the menu is open.</summary>
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

    // MNU-API-02
    /// <summary>Gets or sets a value indicating whether the menu is open at its first render, while <see cref="Open"/> is not supplied.</summary>
    /// <value><see langword="false"/> by default.</value>
    [Parameter]
    public bool DefaultOpen { get; set; }

    // MNU-API-03
    /// <summary>Gets or sets the callback invoked before the menu opens or closes.</summary>
    /// <value>
    /// Receives the requested state and an <see cref="OpenChangeEventDetails"/> as its <c>Details</c>; calling
    /// <c>Details.Cancel()</c> keeps the current state.
    /// </value>
    [Parameter]
    public EventCallback<ValueChangeEventArgs<bool>> OnOpenChange { get; set; }

    // MNU-API-04
    /// <summary>Gets or sets the callback invoked after the open or close transition finishes.</summary>
    /// <value>Receives <see langword="true"/> after opening and <see langword="false"/> after closing.</value>
    [Parameter]
    public EventCallback<bool> OnOpenChangeComplete { get; set; }

    // MNU-API-05
    /// <summary>Gets or sets the callback invoked when an item is highlighted or the highlight is cleared.</summary>
    /// <value>
    /// Receives the highlighted item's rendered id, or <see langword="null"/>, and a <see cref="MenuHighlightEventDetails"/>
    /// with the reason (<see cref="EventReason.Keyboard"/>, <see cref="EventReason.Pointer"/> or <see cref="EventReason.None"/>)
    /// and the item's label.
    /// </value>
    [Parameter]
    public EventCallback<MenuItemHighlightedEventArgs> OnItemHighlighted { get; set; }

    // MNU-API-06
    /// <summary>Gets or sets a value indicating whether the arrow keys wrap from the last item to the first and back.</summary>
    /// <value><see langword="true"/> by default.</value>
    [Parameter]
    public bool LoopFocus { get; set; } = true;

    // MNU-API-07
    /// <summary>Gets or sets a value indicating whether moving the pointer over items highlights them.</summary>
    /// <value>
    /// <see langword="true"/> by default; <see langword="false"/> lets CSS <c>:hover</c> differ from the
    /// <c>data-highlighted</c> state.
    /// </value>
    [Parameter]
    public bool HighlightItemOnHover { get; set; } = true;

    // MNU-API-08
    /// <summary>Gets or sets a value indicating whether the menu is modal while open.</summary>
    /// <value>
    /// <see langword="null"/>, the default, or <see langword="true"/> locks page scroll and blocks presses outside the
    /// menu; <see langword="false"/> leaves the page interactive. A nested menu, or a menu opened by hover, is never modal.
    /// </value>
    [Parameter]
    public bool? Modal { get; set; }

    // MNU-API-09
    /// <summary>Gets or sets the visual orientation of the menu, which picks the arrow keys that move the highlight.</summary>
    /// <value><see cref="Orientation.Vertical"/> by default.</value>
    [Parameter]
    public Orientation Orientation { get; set; } = Orientation.Vertical;

    // MNU-API-10
    /// <summary>Gets or sets a value indicating whether the menu ignores user interaction.</summary>
    /// <value><see langword="false"/> by default.</value>
    [Parameter]
    public bool Disabled { get; set; }

    // MNU-API-11
    /// <summary>Gets or sets a value indicating whether Escape in a submenu also closes its parent menus.</summary>
    /// <value><see langword="false"/> by default; it has no effect on a top-level menu.</value>
    [Parameter]
    public bool CloseParentOnEsc { get; set; }

    // MNU-API-13
    /// <summary>Gets or sets the id of the trigger the menu is associated with, while the consumer owns it.</summary>
    /// <value>
    /// A trigger's id, or <see langword="null"/> for no trigger; supplying it at the first render makes the consumer own
    /// the active trigger, which is useful with <see cref="Open"/>.
    /// </value>
    [Parameter]
    public string? TriggerId { get; set; }

    /// <summary>Gets or sets the id of the trigger the menu is associated with at its first render.</summary>
    /// <value>A trigger's id, or <see langword="null"/>, the default; useful with <see cref="DefaultOpen"/>.</value>
    [Parameter]
    public string? DefaultTriggerId { get; set; }

    // MNU-API-14
    /// <summary>Gets or sets the handle that connects triggers rendered outside the root and opens the menu from code.</summary>
    /// <value>A <see cref="MenuHandle{TPayload}"/>, or <see langword="null"/>.</value>
    [Parameter]
    public MenuHandle<TPayload>? Handle { get; set; }

    /// <summary>Gets or sets the content of the menu.</summary>
    /// <value>The triggers and the portal, or <see langword="null"/>.</value>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    // MNU-API-15
    /// <summary>Gets or sets the content rendered with the payload of the active trigger.</summary>
    /// <value>
    /// A template that receives the active trigger's payload, or <see langword="null"/>; it renders after
    /// <see cref="ChildContent"/>.
    /// </value>
    [Parameter]
    public RenderFragment<TPayload?>? PayloadContent { get; set; }

    /// <summary>Gets a task that completes when the menu's session is attached in the browser.</summary>
    internal Task SessionAttached => behavior?.SessionAttached ?? Task.CompletedTask;

    /// <summary>Gets the root's store.</summary>
    internal MenuStore<TPayload> Store => behavior!.Store;

    /// <summary>Gets the root's behavior.</summary>
    internal MenuRootBehavior<TPayload> Behavior => behavior!;

    /// <inheritdoc />
    public override Task SetParametersAsync(ParameterView parameters)
    {
        behavior ??= new MenuRootBehavior<TPayload>("Menu", isSubmenu: false, JSRuntime, Platform, StateHasChanged, InvokeAsync, DispatchExceptionAsync);
        behavior.SetParameters(parameters, Ids, Logger);
        return base.SetParametersAsync(parameters);
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        MenuParent parent = MenuParent.Resolve(isSubmenu: false, EnclosingMenu, ParentContext);
        behavior!.ApplyParameters(
            new MenuRootParameters<TPayload>(
                new PopupRootParameters<TPayload>(DefaultOpen, DefaultTriggerId, Handle, OnOpenChange, OpenChanged, OnOpenChangeComplete, CloseOnActiveTriggerUnmount: true, ChildContent, PayloadContent),
                Modal,
                Disabled,
                LoopFocus,
                HighlightItemOnHover,
                Orientation,
                CloseParentOnEsc,
                OnItemHighlighted),
            parent,
            FloatingParent,
            DirectionContext);
    }

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder) => behavior!.BuildRenderTree(builder);

    /// <inheritdoc />
    protected override Task OnAfterRenderAsync(bool firstRender) => behavior!.OnAfterRenderAsync(firstRender);

    /// <inheritdoc />
    public ValueTask DisposeAsync() => behavior?.DisposeAsync() ?? ValueTask.CompletedTask;

    // MNU-API-12 (actionsRef.close).
    /// <summary>Closes the menu, with the reason <see cref="EventReason.ImperativeAction"/> and no trigger.</summary>
    /// <returns>A task that completes when the close request has run.</returns>
    public Task CloseAsync() => InvokeAsync(() => behavior!.CloseAsync());

    // MNU-API-12 (actionsRef.unmount), MNU-ANI-10.
    /// <summary>
    /// Ends the closing phase of a menu whose close called <c>PreventUnmountOnClose()</c>, once its own exit animation has
    /// finished.
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
}

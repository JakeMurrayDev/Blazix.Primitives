using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace Blazix.Primitives;

// Base UI's Menu.SubmenuRoot (menu/submenu-root/MenuSubmenuRoot.tsx); the contract is docs/components/menu.md §3.17
// (MNU-STR-11, STR-12, API-49). A separate menu with its own store (MNU-STR-12), a MenuStore<NoPayload> whose parent is the
// enclosing menu (MNU-STA-08), over the same MenuRootBehavior as MenuRoot. Its parameters are the Root's less Handle,
// TriggerId, DefaultTriggerId, the payload template and Modal (design point 1 (a), settled by the orchestrator 2026-11-04; Base UI's props type
// omits `modal`, MenuSubmenuRoot.tsx:41-66).
/// <summary>
/// Groups the parts of a submenu, Base UI's <c>Menu.SubmenuRoot</c>: a menu opened from an item of its parent menu. It
/// renders no element.
/// </summary>
/// <remarks>Place it inside a menu's popup, with a <see cref="MenuSubmenuTrigger"/> and the submenu's portal.</remarks>
public sealed class MenuSubmenuRoot : ComponentBase, IAsyncDisposable
{
    private MenuRootBehavior<NoPayload>? behavior;

    [Inject]
    private PartIdGenerator Ids { get; set; } = null!;

    [Inject]
    private ILogger<MenuSubmenuRoot> Logger { get; set; } = null!;

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

    /// <summary>Gets or sets a value indicating whether the submenu is open.</summary>
    /// <value>The open state the consumer owns, when supplied at the first render. Use <c>@bind-Open</c> to follow it.</value>
    [Parameter]
    public bool Open { get; set; }

    /// <summary>Gets or sets the callback that receives the requested open state, for <c>@bind-Open</c>.</summary>
    /// <value>Invoked after <see cref="OnOpenChange"/> when the change is not canceled.</value>
    [Parameter]
    public EventCallback<bool> OpenChanged { get; set; }

    /// <summary>Gets or sets a value indicating whether the submenu is open at its first render, while <see cref="Open"/> is not supplied.</summary>
    /// <value><see langword="false"/> by default.</value>
    [Parameter]
    public bool DefaultOpen { get; set; }

    /// <summary>Gets or sets the callback invoked before the submenu opens or closes.</summary>
    /// <value>Receives the requested state and an <see cref="OpenChangeEventDetails"/> as its <c>Details</c>.</value>
    [Parameter]
    public EventCallback<ValueChangeEventArgs<bool>> OnOpenChange { get; set; }

    /// <summary>Gets or sets the callback invoked after the open or close transition finishes.</summary>
    /// <value>Receives <see langword="true"/> after opening and <see langword="false"/> after closing.</value>
    [Parameter]
    public EventCallback<bool> OnOpenChangeComplete { get; set; }

    /// <summary>Gets or sets the callback invoked when an item of the submenu is highlighted or the highlight is cleared.</summary>
    /// <value>Receives the highlighted item's rendered id, or <see langword="null"/>, and the change's details.</value>
    [Parameter]
    public EventCallback<MenuItemHighlightedEventArgs> OnItemHighlighted { get; set; }

    /// <summary>Gets or sets a value indicating whether the arrow keys wrap from the last item to the first and back.</summary>
    /// <value><see langword="true"/> by default.</value>
    [Parameter]
    public bool LoopFocus { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether moving the pointer over items highlights them.</summary>
    /// <value><see langword="true"/> by default.</value>
    [Parameter]
    public bool HighlightItemOnHover { get; set; } = true;

    /// <summary>Gets or sets the visual orientation of the submenu.</summary>
    /// <value><see cref="Orientation.Vertical"/> by default.</value>
    [Parameter]
    public Orientation Orientation { get; set; } = Orientation.Vertical;

    /// <summary>Gets or sets a value indicating whether the submenu ignores user interaction.</summary>
    /// <value><see langword="false"/> by default.</value>
    [Parameter]
    public bool Disabled { get; set; }

    // MNU-API-49
    /// <summary>Gets or sets a value indicating whether Escape in the submenu also closes its parent menus.</summary>
    /// <value><see langword="false"/> by default, which closes only the submenu.</value>
    [Parameter]
    public bool CloseParentOnEsc { get; set; }

    /// <summary>Gets or sets the content of the submenu.</summary>
    /// <value>The submenu trigger and the submenu's portal, or <see langword="null"/>.</value>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>Gets a task that completes when the submenu's session is attached in the browser.</summary>
    internal Task SessionAttached => behavior?.SessionAttached ?? Task.CompletedTask;

    /// <summary>Gets the submenu's store.</summary>
    internal MenuStore<NoPayload> Store => behavior!.Store;

    /// <inheritdoc />
    public override Task SetParametersAsync(ParameterView parameters)
    {
        behavior ??= new MenuRootBehavior<NoPayload>("Menu", isSubmenu: true, JSRuntime, Platform, StateHasChanged, InvokeAsync, DispatchExceptionAsync);
        behavior.SetParameters(parameters, Ids, Logger);
        return base.SetParametersAsync(parameters);
    }

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">The submenu root is not inside a menu.</exception>
    protected override void OnParametersSet()
    {
        // MNU-EDGE-03: a submenu outside a menu throws the Root context error.
        MenuRootContext enclosing = MenuRootContext.Require(EnclosingMenu, "Menu");
        MenuParent parent = MenuParent.Resolve(isSubmenu: true, enclosing, ParentContext);
        behavior!.ApplyParameters(
            new MenuRootParameters<NoPayload>(
                new PopupRootParameters<NoPayload>(DefaultOpen, DefaultTriggerId: null, Handle: null, OnOpenChange, OpenChanged, OnOpenChangeComplete, CloseOnActiveTriggerUnmount: true, ChildContent, PayloadContent: null),
                Modal: null,
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
    protected override Task OnAfterRenderAsync(bool firstRender) => behavior!.OnAfterRenderAsync(firstRender, RendererInfo.IsInteractive);

    /// <inheritdoc />
    public ValueTask DisposeAsync() => behavior?.DisposeAsync() ?? ValueTask.CompletedTask;
}

using Blazix.Primitives.Interop;
using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace Blazix.Primitives;

// MNU §3.5 (menu/positioner/MenuPositioner.tsx:38-55): Popover's positioner parameters; `null` is read by parent kind
// (MNU-API-26..30): bottom/center at top level, inline-end/start for a submenu, by the bar's orientation in a menubar,
// start with -5/2 offsets, `fixed` and an arrow padding of 0 under a context menu.
/// <summary>Holds the parameters of a menu positioner, as its behavior applies them.</summary>
/// <param name="Anchor">The positioner's <c>Anchor</c>.</param>
/// <param name="PositionMethod">The positioner's <c>PositionMethod</c>.</param>
/// <param name="Side">The positioner's <c>Side</c>.</param>
/// <param name="Align">The positioner's <c>Align</c>.</param>
/// <param name="SideOffset">The positioner's <c>SideOffset</c>.</param>
/// <param name="AlignOffset">The positioner's <c>AlignOffset</c>.</param>
/// <param name="CollisionBoundary">The positioner's <c>CollisionBoundary</c>.</param>
/// <param name="CollisionPadding">The positioner's <c>CollisionPadding</c>.</param>
/// <param name="CollisionAvoidance">The positioner's <c>CollisionAvoidance</c>.</param>
/// <param name="ArrowPadding">The positioner's <c>ArrowPadding</c>.</param>
/// <param name="Sticky">The positioner's <c>Sticky</c>.</param>
/// <param name="DisableAnchorTracking">The positioner's <c>DisableAnchorTracking</c>.</param>
internal readonly record struct MenuPositionerParameters(
    AnchorValue? Anchor,
    PositionMethod? PositionMethod,
    Side? Side,
    Align? Align,
    OffsetValue? SideOffset,
    OffsetValue? AlignOffset,
    CollisionBoundaryValue? CollisionBoundary,
    CollisionPaddingValue? CollisionPadding,
    CollisionAvoidance? CollisionAvoidance,
    double? ArrowPadding,
    bool Sticky,
    bool DisableAnchorTracking);

// Base UI's Menu.Positioner (menu/positioner/MenuPositioner.tsx) as one internal behaviour (ADR 08 Amendments 2026-11-03
// (3)): ContextMenuPositioner (track 7) is a shell over it with the narrower parameter set. It composes PositionerBehavior
// (the `positioning` module; Side, Align and AnchorHidden mirrored), the internal backdrop before the element while modal
// and not hover-opened at top level, or under a modal bar (MNU-STR-17, DOM-25; the cut-out is the trigger, or the bar's
// content element), the `popup` module's positioner role (the trigger-change watch, the cut-out, the scroll lock: a modal
// non-hover open, a touch open only at viewport width - 20 px or more, MenuPositioner.tsx:245-253), and the
// `interactions` floating handle with Menu's options (the tree node, `itemhover`, the sibling close after the trigger's
// CloseDelay, the release dispatch; ADR 04 Amendments 2026-11-03 (4)).
/// <summary>The behavior of a menu positioner: its placement, its internal backdrop and its floating handle.</summary>
internal sealed class MenuPositionerBehavior : IAsyncDisposable
{
    // MenuPositioner.tsx:52 (DROPDOWN_COLLISION_AVOIDANCE) and :97 (POPUP_COLLISION_AVOIDANCE, a submenu's).
    private static readonly CollisionAvoidance s_dropdown = new(FallbackAxisSide: FallbackAxisSide.None);
    private static readonly CollisionAvoidance s_submenu = new(FallbackAxisSide: FallbackAxisSide.End);

    private readonly string familyName;
    private readonly IJSRuntime jsRuntime;
    private readonly Action requestRender;
    private readonly Func<Func<Task>, Task> invokeAsync;
    private readonly Func<Exception, Task> dispatchException;
    private readonly PositionerBehavior positioner;
    private readonly Action<ElementReference> captureInternalBackdrop;
    private InteractionsFloatingBehavior? floating;
    private PopupModuleAttachment? module;
    private MenuRootContext? root;
    private MenuPortalContext? portal;
    private PopupPortalContext? portalNode;
    private IMenuParentContext? parentContext;
    private MenuSubscription? subscription;
    private MenuSelection<bool>? open;
    private MenuSelection<bool>? mounted;
    private MenuSelection<TransitionStatus?>? status;
    private MenuSelection<MenuInstantType?>? instant;
    private MenuSelection<string?>? activeTriggerId;
    private MenuSelection<bool>? adaptiveOrigin;
    private MenuSelection<bool>? modal;
    private MenuSelection<bool>? hoverOpened;
    private MenuSelection<InteractionType?>? openMethod;
    private MenuSelection<int>? closeDelay;
    private MenuSelection<bool>? disabled;
    private MenuPositionerParameters parameters;
    private ElementReference? element;
    private bool backdropRendered;

    /// <summary>Initializes a new instance of the <see cref="MenuPositionerBehavior"/> class.</summary>
    /// <param name="familyName">The positioner's family, such as <c>Menu</c>.</param>
    /// <param name="jsRuntime">The runtime the twins import their modules through.</param>
    /// <param name="logger">The part's logger.</param>
    /// <param name="requestRender">Asks the part to render, after a change of the menu's state.</param>
    /// <param name="mirrorChanged">Tells the part that the mirrored placement changed, which renders only when its state record changed.</param>
    /// <param name="invokeAsync">Runs work on the part's renderer.</param>
    /// <param name="dispatchException">Routes an exception to the part.</param>
    public MenuPositionerBehavior(string familyName, IJSRuntime jsRuntime, ILogger logger, Action requestRender, Action mirrorChanged, Func<Func<Task>, Task> invokeAsync, Func<Exception, Task> dispatchException)
    {
        _ = logger;
        this.familyName = familyName;
        this.jsRuntime = jsRuntime;
        this.requestRender = requestRender;
        this.invokeAsync = invokeAsync;
        this.dispatchException = dispatchException;
        positioner = new PositionerBehavior(jsRuntime, mirrorChanged, action => invokeAsync(() =>
        {
            action();
            return Task.CompletedTask;
        }));
        captureInternalBackdrop = backdrop => root?.Parent.Context?.SetInternalBackdropElement(backdrop);
    }

    private MenuParentKind? Kind => root!.Parent.Kind;

    // MenuPositioner.tsx:264-268: the internal backdrop of a modal top-level or context menu not opened by hover, or of a
    // modal bar's menu.
    private bool RendersBackdrop => mounted!.Value && Kind switch
    {
        MenuParentKind.Menu => false,
        MenuParentKind.Menubar => root!.Parent.Context!.Modal,
        _ => modal!.Value && !hoverOpened!.Value,
    };

    /// <summary>Gets the root's context.</summary>
    public MenuRootContext Context => root!;

    /// <summary>Gets the context the positioner cascades to its arrow and popup.</summary>
    public PositionerContext PositionerContext => positioner.Context;

    /// <summary>Gets a value indicating whether the menu is open.</summary>
    public bool Open => open!.Value;

    /// <summary>Gets the mirrored side.</summary>
    public Side? Side => positioner.Mirror.Side;

    /// <summary>Gets the mirrored alignment.</summary>
    public Align? Align => positioner.Mirror.Align;

    /// <summary>Gets a value indicating whether the anchor is hidden, as mirrored.</summary>
    public bool AnchorHidden => positioner.Mirror.AnchorHidden;

    /// <summary>Gets a value indicating whether the menu is a submenu.</summary>
    public bool Nested => root!.Parent.Kind == MenuParentKind.Menu;

    /// <summary>Gets the instant type the positioner renders.</summary>
    public MenuInstantType? Instant => instant!.Value;

    /// <summary>Gets a task that completes once the positioner's handles have attached.</summary>
    public Task TwinAttached => Task.WhenAll(positioner.TwinAttached, floating!.TwinAttached, module!.Attached);

    /// <summary>Attaches the handles after the render and runs the batch boundary.</summary>
    /// <param name="element">The positioner's element.</param>
    /// <returns>A task that completes when the modules have their options.</returns>
    public async Task OnAfterRenderAsync(ElementReference? element)
    {
        this.element = element;
        MenuRootContext menu = root!;
        menu.SetPositionerElement(element);
        menu.SetPositionerRegistered(element is not null);
        bool isOpen = open!.Value;
        string? triggerId = activeTriggerId!.Value;
        if (!isOpen)
        {
            menu.ClearTriggerChangeWhenClosed();
        }

        if (!backdropRendered && Kind == MenuParentKind.ContextMenu)
        {
            menu.Parent.Context!.SetInternalBackdropElement(null);
        }

        // The three handles start in the same render; the positioning one awaits the portal's move inside its behavior.
        SetFloatingOptions();
        Task floatingSync = floating!.AfterRenderAsync(element is not null, element, null);
        Task moduleSync = SyncPopupModuleAsync(isOpen, triggerId);
        await positioner.AfterRenderAsync(element, null);
        await floatingSync;
        await moduleSync;
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        subscription?.Dispose();
        subscription = null;
        positioner.Dispose();
        root?.SetPositionerRegistered(false);
        if (root is not null && root.State.Mounted)
        {
            root.SetPositionerElement(null);
        }

        ValueTask floatingDisposed = floating?.DisposeAsync() ?? ValueTask.CompletedTask;
        ValueTask moduleDisposed = module?.DisposeAsync() ?? ValueTask.CompletedTask;
        await floatingDisposed;
        await moduleDisposed;
    }

    /// <summary>Checks the part's placement and subscribes to the root's state, once.</summary>
    /// <param name="root">The enclosing root's context, or <see langword="null"/>.</param>
    /// <param name="portal">The enclosing menu portal's context, or <see langword="null"/>.</param>
    /// <param name="portalNode">The enclosing portal node's context, or <see langword="null"/>.</param>
    /// <param name="parentContext">The enclosing menubar's or context menu's context, or <see langword="null"/>.</param>
    /// <exception cref="InvalidOperationException">The part is outside a root or a portal.</exception>
    public void Subscribe(MenuRootContext? root, MenuPortalContext? portal, PopupPortalContext? portalNode, IMenuParentContext? parentContext)
    {
        MenuRootContext menu = MenuRootContext.Require(root, familyName);

        // MNU-EDGE-03 (MenuPortalContext.ts:9): the positioner outside its portal.
        this.portal = portal ?? throw MenuParts.MissingPortal(familyName);
        this.root = menu;
        this.portalNode = portalNode;
        this.parentContext = parentContext;
        floating = new InteractionsFloatingBehavior(jsRuntime, request => menu.RequestOpenAsync(request.Open, request.Details), dispatchException);
        module = new PopupModuleAttachment(jsRuntime);
        module.Twin.TriggerChangeSettled += () => _ = invokeAsync(() =>
        {
            menu.CompleteTriggerChange();
            return Task.CompletedTask;
        });
        subscription = menu.Subscribe(requestRender);
        open = subscription.Select(MenuSelectors.Open);
        mounted = subscription.Select(MenuSelectors.Mounted);
        status = subscription.Select(MenuSelectors.TransitionStatus);
        instant = subscription.Select(MenuSelectors.InstantType);
        activeTriggerId = subscription.Select(MenuSelectors.ActiveTriggerId);
        adaptiveOrigin = subscription.Select(MenuSelectors.AdaptiveOrigin);
        modal = subscription.Select(MenuSelectors.Modal);
        hoverOpened = subscription.Select(MenuSelectors.HoverOpened);
        openMethod = subscription.Select(MenuSelectors.OpenMethod);
        closeDelay = subscription.Select(MenuSelectors.CloseDelay);
        disabled = subscription.Select(MenuSelectors.Disabled);
    }

    /// <summary>Applies the positioner's parameters.</summary>
    /// <param name="parameters">The positioner's parameters.</param>
    /// <param name="direction">The enclosing direction provider's context, or <see langword="null"/>.</param>
    public void Apply(in MenuPositionerParameters parameters, DirectionProviderContext? direction)
    {
        this.parameters = parameters;
        positioner.SetDirectionContext(direction);
        positioner.SetPortalContext(portalNode);
        floating!.PortalContext = portalNode;
        floating.SessionToken = root!.SessionToken;
        SetInputs();
    }

    /// <summary>Adds the positioner's attributes.</summary>
    /// <param name="attributes">The part's attributes.</param>
    public void AddAttributes(AttributeList attributes)
    {
        SetInputs();
        positioner.AddAttributes(attributes);
    }

    /// <summary>Finishes the positioner's attributes after the consumer's: the handover at attach.</summary>
    /// <param name="attributes">The part's attributes.</param>
    public void FinishAttributes(AttributeList attributes) => positioner.FinishAttributes(attributes);

    /// <summary>Renders the internal backdrop before the positioner.</summary>
    /// <param name="builder">The part's render tree builder.</param>
    public void BuildBeforeElement(RenderTreeBuilder builder)
    {
        backdropRendered = RendersBackdrop;
        if (backdropRendered)
        {
            PopupInternalBackdrop.Render(builder, root!.State.FloatingId + "-backdrop", inert: !open!.Value, Kind == MenuParentKind.ContextMenu ? captureInternalBackdrop : null);
        }
    }

    /// <summary>Wraps the positioner's content in the positioner's cascade.</summary>
    /// <param name="content">The consumer's content.</param>
    /// <returns>The content to render inside the positioner.</returns>
    public RenderFragment? CascadeContent(RenderFragment? content) => positioner.Context.Cascade(content);

    // MenuPositioner.tsx:79-128: the parameters read by parent kind.
    private void SetInputs()
    {
        MenuParent parent = root!.Parent;
        Side? side = parameters.Side;
        Align? align = parameters.Align;
        OffsetValue? sideOffset = parameters.SideOffset;
        OffsetValue? alignOffset = parameters.AlignOffset;
        CollisionAvoidance collisionAvoidance = parameters.CollisionAvoidance ?? s_dropdown;
        bool contextMenu = parent.Kind == MenuParentKind.ContextMenu;
        if (contextMenu)
        {
            align ??= Primitives.Align.Start;
            if (side is null && align != Primitives.Align.Center)
            {
                alignOffset ??= 2;
                sideOffset ??= -5;
            }
        }

        if (parent.Kind == MenuParentKind.Menu)
        {
            side ??= Primitives.Side.InlineEnd;
            align ??= Primitives.Align.Start;
            collisionAvoidance = parameters.CollisionAvoidance ?? s_submenu;
        }
        else if (parent.Kind == MenuParentKind.Menubar)
        {
            side ??= parent.Context!.Orientation == Orientation.Vertical ? Primitives.Side.InlineEnd : Primitives.Side.Bottom;
            align ??= Primitives.Align.Start;
        }

        positioner.SetInputs(new PositionerInputs
        {
            Anchor = parameters.Anchor,

            // MNU-EDGE-15 (MenuPositioner.tsx:59, 109): fixed inside any context menu, whatever the parent kind.
            PositionMethod = MenuParent.Find(parentContext, MenuParentKind.ContextMenu) is not null ? Primitives.PositionMethod.Fixed : parameters.PositionMethod,
            Side = side,
            SideOffset = sideOffset,
            Align = align,
            AlignOffset = alignOffset,
            CollisionBoundary = parameters.CollisionBoundary,
            CollisionPadding = parameters.CollisionPadding,
            Sticky = parameters.Sticky,
            ArrowPadding = contextMenu ? 0 : parameters.ArrowPadding,
            DisableAnchorTracking = parameters.DisableAnchorTracking,
            CollisionAvoidance = collisionAvoidance,
            Open = open!.Value,
            Mounted = mounted!.Value,
            KeepMounted = portal!.KeepMounted,
            TransitionStatus = status!.Value,
            AdaptiveOrigin = adaptiveOrigin!.Value,

            // MNU-POS-05 (MenuPositioner.tsx:131): a filterable menu keeps its placement while the results resize; M5 track 12
            // under the orchestrator's 2026-11-12 grant.
            LazyFlip = root!.Filter is not null ? "placement" : null,
            TriggerId = activeTriggerId!.Value,
            SessionToken = root.SessionToken,
            Shift = contextMenu ? new PositioningShift(CrossAxis: collisionAvoidance.Side != CollisionAvoidanceMode.Flip, RootBoundary: "layoutViewport") : null,
        });
    }

    // MenuPopup.tsx:123-126 (MNU-PTR-07): the floating hover, off under a context menu or a bar; Menu's floating additions
    // (ADR 04 Amendments 2026-11-03 (4)): the tree's item hover, a submenu's sibling close after its trigger's CloseDelay,
    // the release dispatch and a submenu's hover turned off by a pointer move over it.
    private void SetFloatingOptions()
    {
        MenuParentKind? kind = Kind;
        bool hover = !disabled!.Value && kind is not (MenuParentKind.ContextMenu or MenuParentKind.Menubar);
        InteractionsFloatingHoverOptions? hoverOptions = hover ? new InteractionsFloatingHoverOptions(closeDelay!.Value, RequireHoverEnabled: true) : null;
        InteractionsMenuFloatingOptions menu = new()
        {
            ItemHover = true,
            SiblingCloseDelay = kind == MenuParentKind.Menu ? closeDelay!.Value : null,
            Release = true,
            DisableHoverOnMove = kind == MenuParentKind.Menu,
        };
        InteractionsFloatingOptions current = floating!.Options;
        if (current.Node != true || !Equals(current.Hover, hoverOptions) || !Equals(current.Menu, menu))
        {
            floating.Options = new InteractionsFloatingOptions { Node = true, Hover = hoverOptions, Menu = menu };
        }
    }

    // MenuPositioner.tsx:245-283: the internal backdrop's cut-out (the trigger at top level, the bar's content element in a
    // bar) and the scroll lock (a modal non-hover open, or a modal bar's; a touch open only at viewport width - 20 px or
    // more, the module's width test).
    private async Task SyncPopupModuleAsync(bool isOpen, string? triggerId)
    {
        if (element is not { } target)
        {
            return;
        }

        await PopupPortalContext.WhenMoved(portalNode);
        MenuParentKind? kind = Kind;
        bool menubarModal = kind == MenuParentKind.Menubar && root!.Parent.Context!.Modal;
        bool popupModal = modal!.Value && !hoverOpened!.Value;
        await module!.SyncAsync(target, new PopupOptions
        {
            Positioner = new PopupPositionerOptions
            {
                WatchTriggerChange = isOpen,
                // MenuPositioner.tsx:271-276: a bar menu's backdrop cuts the bar out, a top-level menu's its trigger (the
                // orchestrator's grant of 2026-11-11, M5 track 8).
                Cutout = backdropRendered && kind is null or MenuParentKind.Menubar,
                CutoutElementId = backdropRendered && kind == MenuParentKind.Menubar ? root!.Parent.Context!.CutoutElementId : null,
                ScrollLock = isOpen && (menubarModal || popupModal),
                TouchOpen = openMethod!.Value == InteractionType.Touch,
                ActiveTriggerId = triggerId,
            },
        });
    }
}

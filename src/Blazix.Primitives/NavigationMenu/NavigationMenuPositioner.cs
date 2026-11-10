using Blazix.Primitives.Interop;
using Blazix.Primitives.Rendering;
using Blazix.Primitives.Store;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Blazix.Primitives;

// NAV-STA-16, §3.10 state object (navigation-menu/positioner/NavigationMenuPositioner.tsx:133-139). Side, Align and
// AnchorHidden are the `positioning` module's from attach (ADR 04 §2): mirrored. Instant is C#'s (design point 6, settled
// by the orchestrator 2026-11-04): a boolean, rendered as `data-instant` by the default rule (TRN-DOM-10; §16 Q9).
/// <summary>Represents the state of a navigation menu positioner, which the part exposes as data attributes and passes to its class and style functions.</summary>
/// <param name="Open">Whether the menu is open; projected as <c>data-open</c> or <c>data-closed</c>.</param>
/// <param name="Side">The side the popup is placed on, rendered as <c>data-side</c> by the browser-side behavior.</param>
/// <param name="Align">The alignment of the popup, rendered as <c>data-align</c> by the browser-side behavior.</param>
/// <param name="AnchorHidden">Whether the anchor is hidden, rendered as <c>data-anchor-hidden</c> by the browser-side behavior.</param>
/// <param name="Instant">Whether the positioner's transitions are suppressed for now; projected as <c>data-instant</c>.</param>
[PartState]
public sealed partial record NavigationMenuPositionerState(
    bool Open,
    [property: MirroredField] Side? Side,
    [property: MirroredField] Align? Align,
    [property: MirroredField] bool AnchorHidden,
    bool Instant);

// navigation-menu/positioner/NavigationMenuPositionerDataAttributes.ts (NAV-DOM-27, 28).
/// <summary>Provides the names of the data attributes a navigation menu positioner renders.</summary>
public static class NavigationMenuPositionerDataAttributes
{
    /// <summary>Represents the attribute present while the menu is open: <c>data-open</c>.</summary>
    public const string Open = PopupDataAttributes.Open;

    /// <summary>Represents the attribute present while the menu is closed: <c>data-closed</c>.</summary>
    public const string Closed = PopupDataAttributes.Closed;

    /// <summary>Represents the attribute present while the anchor is hidden: <c>data-anchor-hidden</c>.</summary>
    public const string AnchorHidden = PopupDataAttributes.AnchorHidden;

    /// <summary>Represents the attribute whose value is the side the popup is placed on: <c>data-side</c>.</summary>
    public const string Side = PopupDataAttributes.Side;

    /// <summary>Represents the attribute whose value is the alignment of the popup: <c>data-align</c>.</summary>
    public const string Align = PopupDataAttributes.Align;

    /// <summary>Represents the attribute present while the positioner's transitions are suppressed: <c>data-instant</c>.</summary>
    public const string Instant = "data-instant";
}

// navigation-menu/positioner/NavigationMenuPositionerCssVars.ts (NAV-DOM-29, NAV-POS-07).
/// <summary>Provides the names of the CSS variables a navigation menu positioner carries.</summary>
public static class NavigationMenuPositionerCssVars
{
    /// <summary>Represents the variable that holds the available width: <c>--available-width</c>.</summary>
    public const string AvailableWidth = "--available-width";

    /// <summary>Represents the variable that holds the available height: <c>--available-height</c>.</summary>
    public const string AvailableHeight = "--available-height";

    /// <summary>Represents the variable that holds the anchor's width: <c>--anchor-width</c>.</summary>
    public const string AnchorWidth = "--anchor-width";

    /// <summary>Represents the variable that holds the anchor's height: <c>--anchor-height</c>.</summary>
    public const string AnchorHeight = "--anchor-height";

    /// <summary>Represents the variable that holds the transform origin: <c>--transform-origin</c>.</summary>
    public const string TransformOrigin = "--transform-origin";

    /// <summary>Represents the variable that holds the positioner's width during a size transition: <c>--positioner-width</c>.</summary>
    public const string PositionerWidth = "--positioner-width";

    /// <summary>Represents the variable that holds the positioner's height during a size transition: <c>--positioner-height</c>.</summary>
    public const string PositionerHeight = "--positioner-height";
}

// Base UI's NavigationMenu.Positioner (navigation-menu/positioner/NavigationMenuPositioner.tsx); the contract is
// docs/components/navigation-menu.md §3.10, §5.10, §10.
// - Track 2's PositionerBehavior with the Popover parameter set (NAV-POS-01: bottom, center, padding 5, which equal the
//   module's defaults for null inputs); the collision avoidance by nesting when none is given (NAV-POS-05: fallback axis
//   side none top level, end nested); adaptive origin always (NAV-POS-04); the layout-viewport shift (NAV-POS-03, track 2's
//   input); KeepMounted from the Portal (NAV-POS-06); the anchor the active trigger (NAV-POS-02). The behavior renders
//   `hidden` while unmounted and `pointer-events: none` while closed (NAV-DOM-26), the handover at attach.
// - The interactions floating role (NAV-PTR-09): enabled while a trigger is active, with the Root's close delay.
// - The element registry (NAV-STA-08): its registration enables the dismissal and the click role (NAV-DIS-08).
// - Instant (NAV-ANI-13, ANI-14; design point 6): true while mounted open at first, cleared 0 ms after; each resize report
//   sets it and restarts a 100 ms timer, both C#'s, results that are state (ADR 04 §1).
// - The `navigation-menu` module's positioner handle: focus in and out (NAV-FOC-08) and the resize report.
/// <summary>
/// Positions the navigation menu's popup against the active trigger: Base UI's <c>NavigationMenu.Positioner</c>, rendered
/// as a <c>div</c>.
/// </summary>
public sealed class NavigationMenuPositioner : PartBase<NavigationMenuPositionerState>
{
    private const int ResizeInstantMilliseconds = 100;

    private static readonly StateAttributesMapping<NavigationMenuPositionerState> s_mapping = PopupStateMapping.CreateOpen<NavigationMenuPositionerState>();
    private static readonly PositioningShift s_shift = new(false, "layoutViewport");
    private static readonly CollisionAvoidance s_topLevelAvoidance = new(FallbackAxisSide: FallbackAxisSide.None);
    private static readonly CollisionAvoidance s_nestedAvoidance = new(FallbackAxisSide: FallbackAxisSide.End);

    private NavigationMenuRootContext? root;
    private PositionerBehavior? positioner;
    private InteractionsFloatingBehavior? floating;
    private NavigationMenuModule? module;
    private StoreSelection<NavigationMenuStoreState, bool>? open;
    private StoreSelection<NavigationMenuStoreState, bool>? mounted;
    private StoreSelection<NavigationMenuStoreState, TransitionStatus?>? status;
    private StoreSelection<NavigationMenuStoreState, string?>? activeTriggerId;
    private NavigationMenuPositionerState? state;
    private RenderFragment? cascadedContent;
    private RenderFragment? cascade;
    private CancellationTokenSource? initialInstant;
    private CancellationTokenSource? resizeInstant;
    private bool instant;
    private bool wasOpen;
    private bool disposed;
    private bool countedMount;

    [Inject]
    private IJSRuntime JSRuntime { get; set; } = null!;

    [CascadingParameter]
    private NavigationMenuRootContext? RootContext { get; set; }

    [CascadingParameter]
    private NavigationMenuPortalContext? PortalContext { get; set; }

    [CascadingParameter]
    private PopupPortalContext? Portal { get; set; }

    [CascadingParameter]
    private DirectionProviderContext? DirectionContext { get; set; }

    // NAV-POS-02
    /// <summary>Gets or sets the element the popup is positioned against.</summary>
    /// <value>An element, a virtual anchor or a callback; <see langword="null"/>, the default, is the active trigger.</value>
    [Parameter]
    public AnchorValue? Anchor { get; set; }

    /// <summary>Gets or sets the CSS position the positioner uses.</summary>
    /// <value><see cref="Primitives.PositionMethod.Absolute"/> when <see langword="null"/>, the default.</value>
    [Parameter]
    public PositionMethod? PositionMethod { get; set; }

    /// <summary>Gets or sets the side of the anchor the popup is placed on.</summary>
    /// <value><see cref="Primitives.Side.Bottom"/> when <see langword="null"/>, the default.</value>
    [Parameter]
    public Side? Side { get; set; }

    /// <summary>Gets or sets the distance between the anchor and the popup.</summary>
    /// <value>A number of pixels or a callback; 0 when <see langword="null"/>, the default.</value>
    [Parameter]
    public OffsetValue? SideOffset { get; set; }

    /// <summary>Gets or sets the alignment of the popup against the anchor.</summary>
    /// <value><see cref="Primitives.Align.Center"/> when <see langword="null"/>, the default.</value>
    [Parameter]
    public Align? Align { get; set; }

    /// <summary>Gets or sets the offset along the alignment axis.</summary>
    /// <value>A number of pixels or a callback; 0 when <see langword="null"/>, the default.</value>
    [Parameter]
    public OffsetValue? AlignOffset { get; set; }

    /// <summary>Gets or sets the area the popup is kept inside.</summary>
    /// <value>The clipping ancestors when <see langword="null"/>, the default.</value>
    [Parameter]
    public CollisionBoundaryValue? CollisionBoundary { get; set; }

    /// <summary>Gets or sets the space kept between the popup and the edges of the collision boundary.</summary>
    /// <value>5 pixels when <see langword="null"/>, the default.</value>
    [Parameter]
    public CollisionPaddingValue? CollisionPadding { get; set; }

    /// <summary>Gets or sets a value indicating whether the popup stays in view when the anchor scrolls out of it.</summary>
    /// <value><see langword="false"/> by default.</value>
    [Parameter]
    public bool Sticky { get; set; }

    /// <summary>Gets or sets the least distance between the arrow and the popup's edges.</summary>
    /// <value>5 pixels when <see langword="null"/>, the default.</value>
    [Parameter]
    public double? ArrowPadding { get; set; }

    /// <summary>Gets or sets a value indicating whether the popup stops following the anchor's moves.</summary>
    /// <value><see langword="false"/> by default.</value>
    [Parameter]
    public bool DisableAnchorTracking { get; set; }

    // NAV-POS-05
    /// <summary>Gets or sets how the popup avoids collisions.</summary>
    /// <value>
    /// <see langword="null"/>, the default: never fall back to the perpendicular axis for a top-level menu, and to the end of
    /// it for a nested one; a value replaces that default entirely.
    /// </value>
    [Parameter]
    public CollisionAvoidance? CollisionAvoidance { get; set; }

    /// <inheritdoc />
    protected override string DefaultTagName => "div";

    // The pipeline reads the state before the part's attributes, so the record follows the store and the mirror here.
    /// <inheritdoc />
    protected override NavigationMenuPositionerState State
    {
        get
        {
            PositionerMirror mirror = positioner!.Mirror;
            bool isOpen = open!.Value;
            if (state is null || state.Open != isOpen || state.Side != mirror.Side || state.Align != mirror.Align || state.AnchorHidden != mirror.AnchorHidden || state.Instant != instant)
            {
                state = new NavigationMenuPositionerState(isOpen, mirror.Side, mirror.Align, mirror.AnchorHidden, instant);
            }

            return state;
        }
    }

    /// <inheritdoc />
    protected override StateAttributesMapping<NavigationMenuPositionerState>? StateAttributesMapping => s_mapping;

    // ADR 02 decision 1: under a substitute callback the twins find the element by id.
    /// <inheritdoc />
    protected override bool UsesGeneratedId => RenderFunction is not null;

    /// <summary>Gets a task that completes once the positioner's handles have attached.</summary>
    internal Task TwinsAttached => Task.WhenAll(positioner!.TwinAttached, floating!.TwinAttached, module!.Attached);

    /// <summary>Gets the positioner's <c>navigation-menu</c> twin, whose reports the tests play.</summary>
    internal NavigationMenuTwin ModuleTwin => module!.Twin;

    /// <inheritdoc />
    public override Task SetParametersAsync(ParameterView parameters)
    {
        ChildContent = null;
        return base.SetParametersAsync(parameters);
    }

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        root = NavigationMenuRootContext.Require(RootContext);
        NavigationMenuPortalContext.Require(PortalContext);
        positioner = new PositionerBehavior(JSRuntime, OnMirrorChanged, InvokeAsync);
        floating = new InteractionsFloatingBehavior(JSRuntime, request => root.RouteAsync(request), DispatchExceptionAsync);
        module = new NavigationMenuModule(JSRuntime);
        module.Twin.Resized += () => _ = InvokeAsync(OnResized);
        base.OnInitialized();

        // NAV-ANI-13: a Positioner that mounts while open starts instant, cleared 0 ms after.
        instant = open!.Value;
        wasOpen = open.Value;
        if (instant)
        {
            initialInstant = new CancellationTokenSource();
            _ = ClearInitialInstantAsync(initialInstant.Token);
        }
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        // G4b: the Trigger's click role reads the mount from this render, before the registration of the after-render below. A
        // count, so a Positioner swapped in one render (`@key`) mounts before the old one's disposal subtracts its own.
        if (Enabled != countedMount)
        {
            countedMount = Enabled;
            root!.Store.CountPositionerMount(Enabled);
        }

        positioner!.SetDirectionContext(DirectionContext);
        positioner.SetPortalContext(Portal);
        floating!.PortalContext = Portal;
        floating.SessionToken = root!.SessionToken;
        SetInputs();
        if (cascade is null || !ReferenceEquals(cascadedContent, ChildContent))
        {
            cascadedContent = ChildContent;
            cascade = positioner.Context.Cascade(cascadedContent);
        }

        ChildContent = cascade;
    }

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        root!.Store.SetPositioner(Enabled && Element is not null);
        SetFloatingOptions();
        Task floatingSync = floating!.AfterRenderAsync(Enabled, Element, Id);
        Task moduleSync = Element is { } element ? SyncModuleAsync(element) : Task.CompletedTask;
        await positioner!.AfterRenderAsync(Element, Id);
        await floatingSync;
        await moduleSync;
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing && !disposed)
        {
            disposed = true;
            initialInstant?.Cancel();
            resizeInstant?.Cancel();
            positioner?.Dispose();
            if (floating is not null)
            {
                _ = floating.DisposeAsync().AsTask();
            }

            if (module is not null)
            {
                _ = module.DisposeAsync().AsTask();
            }

            root?.Store.SetPositioner(false);
            if (countedMount)
            {
                countedMount = false;
                root?.Store.CountPositionerMount(false);
            }
        }

        base.Dispose(disposing);
    }

    /// <inheritdoc />
    protected override void SubscribeSelectors()
    {
        SelectorSubscription<NavigationMenuStoreState> subscription = SubscribeTo(RootContext!.Store);
        open = subscription.Select(NavigationMenuSelectors.Open);
        mounted = subscription.Select(NavigationMenuSelectors.Mounted);
        status = subscription.Select(NavigationMenuSelectors.TransitionStatus);
        activeTriggerId = subscription.Select(NavigationMenuSelectors.ActiveTriggerId);
    }

    /// <inheritdoc />
    protected override void AddPartAttributes(AttributeList attributes)
    {
        SetInputs();
        positioner!.AddAttributes(attributes);
    }

    /// <inheritdoc />
    protected override void FinishAttributes(AttributeList attributes) => positioner!.FinishAttributes(attributes);

    private async Task SyncModuleAsync(ElementReference element)
    {
        await PopupPortalContext.WhenMoved(Portal);
        await module!.SyncAsync(element, new NavigationMenuOptions { Part = NavigationMenuTwin.PositionerPart }, root!.RootElement);
    }

    private void SetInputs()
    {
        bool isOpen = open!.Value;
        if (isOpen && !wasOpen && initialInstant is null && resizeInstant is null)
        {
            // An open that starts after mount is not initially open: no instant (NAV-ANI-13).
            instant = false;
        }

        wasOpen = isOpen;
        positioner!.SetInputs(new PositionerInputs
        {
            Anchor = Anchor,
            PositionMethod = PositionMethod,
            Side = Side,
            SideOffset = SideOffset,
            Align = Align,
            AlignOffset = AlignOffset,
            CollisionBoundary = CollisionBoundary,
            CollisionPadding = CollisionPadding,
            Sticky = Sticky,
            ArrowPadding = ArrowPadding,
            DisableAnchorTracking = DisableAnchorTracking,
            CollisionAvoidance = CollisionAvoidance ?? (root!.Nested ? s_nestedAvoidance : s_topLevelAvoidance),
            Open = isOpen,
            Mounted = mounted!.Value,
            KeepMounted = PortalContext!.KeepMounted,
            TransitionStatus = status!.Value,
            AdaptiveOrigin = true,
            TriggerId = activeTriggerId!.Value,
            SessionToken = root!.SessionToken,
            Shift = s_shift,
        });
    }

    // NAV-PTR-09 (NavigationMenuList.tsx:44-52): the hover-close of the floating element while a trigger is active.
    private void SetFloatingOptions()
    {
        bool active = activeTriggerId!.Value is not null;
        int? delay = active ? root!.CloseDelayMs : null;
        InteractionsFloatingOptions current = floating!.Options;
        if (current.Node != true || current.Hover?.CloseDelay != delay || (current.Hover is null) == active)
        {
            floating.Options = new InteractionsFloatingOptions { Node = true, Hover = active ? new InteractionsFloatingHoverOptions(delay) : null };
        }
    }

    private async Task ClearInitialInstantAsync(CancellationToken token)
    {
        await Task.Yield();
        if (token.IsCancellationRequested)
        {
            return;
        }

        await InvokeAsync(() =>
        {
            initialInstant = null;
            if (resizeInstant is null && instant && !disposed)
            {
                instant = false;
                StateHasChanged();
            }
        });
    }

    // NAV-ANI-14, NAV-DOM-28 (NavigationMenuPositioner.tsx:156-168).
    private void OnResized()
    {
        if (disposed || !open!.Value)
        {
            return;
        }

        resizeInstant?.Cancel();
        resizeInstant = new CancellationTokenSource();
        _ = ClearResizeInstantAsync(resizeInstant.Token);
        if (!instant)
        {
            instant = true;
            StateHasChanged();
        }
    }

    private async Task ClearResizeInstantAsync(CancellationToken token)
    {
        try
        {
            await Task.Delay(ResizeInstantMilliseconds, token);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        await InvokeAsync(() =>
        {
            if (token.IsCancellationRequested || disposed)
            {
                return;
            }

            resizeInstant = null;
            if (instant)
            {
                instant = false;
                StateHasChanged();
            }
        });
    }

    // A part renders on a mirror change only when its state record changed (track 2's rule).
    private void OnMirrorChanged()
    {
        NavigationMenuPositionerState? previous = state;
        if (previous is null || previous != State)
        {
            StateHasChanged();
        }
    }
}

// NAV-STA-16, §3.13 state object (navigation-menu/arrow/NavigationMenuArrow.tsx:27-32). Side, Align and Uncentered are the
// `positioning` module's from the Positioner's attach (ADR 04 §2): mirrored.
/// <summary>Represents the state of a navigation menu arrow, which the part exposes as data attributes and passes to its class and style functions.</summary>
/// <param name="Open">Whether the menu is open; projected as <c>data-open</c> or <c>data-closed</c>.</param>
/// <param name="Side">The side the popup is placed on, rendered as <c>data-side</c> by the browser-side behavior.</param>
/// <param name="Align">The alignment of the popup, rendered as <c>data-align</c> by the browser-side behavior.</param>
/// <param name="Uncentered">Whether the arrow cannot be centered on the anchor, rendered as <c>data-uncentered</c> by the browser-side behavior.</param>
[PartState]
public sealed partial record NavigationMenuArrowState(
    bool Open,
    [property: MirroredField] Side? Side,
    [property: MirroredField] Align? Align,
    [property: MirroredField] bool Uncentered);

// navigation-menu/arrow/NavigationMenuArrowDataAttributes.ts (NAV-DOM-37).
/// <summary>Provides the names of the data attributes a navigation menu arrow renders.</summary>
public static class NavigationMenuArrowDataAttributes
{
    /// <summary>Represents the attribute present while the menu is open: <c>data-open</c>.</summary>
    public const string Open = PopupDataAttributes.Open;

    /// <summary>Represents the attribute present while the menu is closed: <c>data-closed</c>.</summary>
    public const string Closed = PopupDataAttributes.Closed;

    /// <summary>Represents the attribute whose value is the side the popup is placed on: <c>data-side</c>.</summary>
    public const string Side = PopupDataAttributes.Side;

    /// <summary>Represents the attribute whose value is the alignment of the popup: <c>data-align</c>.</summary>
    public const string Align = PopupDataAttributes.Align;

    /// <summary>Represents the attribute present while the arrow cannot be centered on the anchor: <c>data-uncentered</c>.</summary>
    public const string Uncentered = "data-uncentered";
}

// Base UI's NavigationMenu.Arrow (navigation-menu/arrow/NavigationMenuArrow.tsx); the contract is
// docs/components/navigation-menu.md §3.13, §5.13: track 2's PositionerArrowBehavior (aria-hidden, the placement from the
// mirror until the Positioner's module writes it); NAV-EDGE-04 outside a Positioner; the navigation-menu handle writes
// `transition: none` while the popup starts (NAV-ANI-15).
/// <summary>
/// Represents an arrow that points from the navigation menu's popup to the active trigger: Base UI's
/// <c>NavigationMenu.Arrow</c>, rendered as a <c>div</c>.
/// </summary>
public sealed class NavigationMenuArrow : PartBase<NavigationMenuArrowState>
{
    private static readonly StateAttributesMapping<NavigationMenuArrowState> s_mapping = PopupStateMapping.CreateOpen<NavigationMenuArrowState>();

    private PositionerArrowBehavior? arrow;
    private NavigationMenuRootContext? root;
    private NavigationMenuModule? module;
    private StoreSelection<NavigationMenuStoreState, bool>? open;
    private NavigationMenuArrowState? state;
    private bool disposed;

    [Inject]
    private IJSRuntime JSRuntime { get; set; } = null!;

    [CascadingParameter]
    private NavigationMenuRootContext? RootContext { get; set; }

    [CascadingParameter]
    private PositionerContext? PositionerContext { get; set; }

    /// <inheritdoc />
    protected override string DefaultTagName => "div";

    /// <inheritdoc />
    protected override NavigationMenuArrowState State
    {
        get
        {
            PositionerMirror mirror = arrow!.Mirror;
            bool isOpen = open!.Value;
            if (state is null || state.Open != isOpen || state.Side != mirror.Side || state.Align != mirror.Align || state.Uncentered != mirror.Uncentered)
            {
                state = new NavigationMenuArrowState(isOpen, mirror.Side, mirror.Align, mirror.Uncentered);
            }

            return state;
        }
    }

    /// <inheritdoc />
    protected override StateAttributesMapping<NavigationMenuArrowState>? StateAttributesMapping => s_mapping;

    /// <inheritdoc />
    protected override bool UsesGeneratedId => true;

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        root = NavigationMenuRootContext.Require(RootContext);
        RequirePositioner(PositionerContext);
        arrow = new PositionerArrowBehavior(OnMirrorChanged);
        module = new NavigationMenuModule(JSRuntime);
        base.OnInitialized();
    }

    /// <inheritdoc />
    protected override void OnParametersSet() => arrow!.SetContext(PositionerContext!);

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        Task arrowSync = arrow!.AfterRenderAsync();
        if (Element is { } element && !disposed)
        {
            await module!.SyncAsync(element, new NavigationMenuOptions { Part = NavigationMenuTwin.ArrowPart }, root!.RootElement);
        }

        await arrowSync;
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing && !disposed)
        {
            disposed = true;
            arrow?.Dispose();
            if (module is not null)
            {
                _ = module.DisposeAsync().AsTask();
            }
        }

        base.Dispose(disposing);
    }

    /// <inheritdoc />
    protected override void SubscribeSelectors()
    {
        open = SubscribeTo(RootContext!.Store).Select(NavigationMenuSelectors.Open);
    }

    /// <inheritdoc />
    protected override void AddPartAttributes(AttributeList attributes) => arrow!.AddAttributes(attributes);

    /// <inheritdoc />
    protected override void FinishAttributes(AttributeList attributes) => arrow!.FinishAttributes(attributes, Id!);

    /// <summary>Throws the error of a part rendered outside a navigation menu positioner.</summary>
    /// <param name="context">The cascaded context, or <see langword="null"/>.</param>
    /// <returns>The context.</returns>
    /// <exception cref="InvalidOperationException"><paramref name="context"/> is <see langword="null"/>.</exception>
    internal static PositionerContext RequirePositioner(PositionerContext? context)
    {
        // NAV-EDGE-04: thrown in every build.
        return context ?? throw new InvalidOperationException(
            "Blazix: The `NavigationMenuPositioner` context is missing. `NavigationMenuPositioner` parts must be placed within `<NavigationMenuPositioner>`.");
    }

    // A part renders on a mirror change only when its state record changed (track 2's rule).
    private void OnMirrorChanged()
    {
        NavigationMenuArrowState? previous = state;
        if (previous is null || previous != State)
        {
            StateHasChanged();
        }
    }
}

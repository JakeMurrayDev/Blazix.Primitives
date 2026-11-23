using System.Globalization;
using Iodine.Headless.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace Iodine.Headless;

// DRW §3.11, DRW-STA-01 (drawer/popup/DrawerPopup.tsx:310-319): the popup's state object. TransitionStatus and Swiping are
// mirrored (ADR 04 §2): the `drawer` and `transitions` modules write data-swiping and the transition attributes (8b).
/// <summary>Represents the state of a drawer's popup, which the part exposes as data attributes and passes to its class and style functions.</summary>
/// <param name="Open">A value indicating whether the drawer is open; projected as <c>data-open</c> or <c>data-closed</c>.</param>
/// <param name="TransitionStatus">
/// The phase of the drawer's enter or exit transition, or <see langword="null"/> while none is in progress; its
/// <c>data-starting-style</c> and <c>data-ending-style</c> attributes are written by the browser-side behavior.
/// </param>
/// <param name="Expanded">A value indicating whether the active snap point is the number 1, the full height; projected as <c>data-expanded</c>.</param>
/// <param name="Nested">A value indicating whether the drawer is nested in another dialog; projected as <c>data-nested</c>.</param>
/// <param name="NestedDrawerOpen">A value indicating whether a drawer nested in this one is open; projected as <c>data-nested-drawer-open</c>.</param>
/// <param name="NestedDrawerSwiping">A value indicating whether a drawer nested in this one is being swiped; projected as <c>data-nested-drawer-swiping</c>.</param>
/// <param name="SwipeDirection">The direction that dismisses the drawer; projected as <c>data-swipe-direction</c>.</param>
/// <param name="Swiping">
/// A value indicating whether the popup is being swiped; its <c>data-swiping</c> attribute is written by the browser-side
/// behavior.
/// </param>
[PartState]
public sealed partial record DrawerPopupState(
    bool Open,
    [property: MirroredField] TransitionStatus? TransitionStatus,
    bool Expanded,
    bool Nested,
    bool NestedDrawerOpen,
    bool NestedDrawerSwiping,
    SwipeDirection SwipeDirection,
    [property: MirroredField] bool Swiping);

// DRW-STR-02 (drawer/popup/DrawerPopupDataAttributes.ts).
/// <summary>Provides the names of the data attributes a drawer's popup renders.</summary>
public static class DrawerPopupDataAttributes
{
    /// <summary>Represents the attribute present while the drawer is open: <c>data-open</c>.</summary>
    public const string Open = "data-open";

    /// <summary>Represents the attribute present while the drawer is closed: <c>data-closed</c>.</summary>
    public const string Closed = "data-closed";

    /// <summary>Represents the attribute present while the drawer begins animating in: <c>data-starting-style</c>.</summary>
    public const string StartingStyle = "data-starting-style";

    /// <summary>Represents the attribute present while the drawer is animating out: <c>data-ending-style</c>.</summary>
    public const string EndingStyle = "data-ending-style";

    /// <summary>Represents the attribute present while the active snap point is the full height: <c>data-expanded</c>.</summary>
    public const string Expanded = "data-expanded";

    /// <summary>Represents the attribute present while a drawer nested in this one is open: <c>data-nested-drawer-open</c>.</summary>
    public const string NestedDrawerOpen = "data-nested-drawer-open";

    /// <summary>Represents the attribute present while a drawer nested in this one is being swiped: <c>data-nested-drawer-swiping</c>.</summary>
    public const string NestedDrawerSwiping = "data-nested-drawer-swiping";

    /// <summary>Represents the attribute present from a swipe's dismissing release until the drawer resets: <c>data-swipe-dismiss</c>.</summary>
    public const string SwipeDismiss = "data-swipe-dismiss";

    /// <summary>Represents the attribute that holds the direction that dismisses the drawer: <c>data-swipe-direction</c>.</summary>
    public const string SwipeDirection = "data-swipe-direction";

    /// <summary>Represents the attribute present while the drawer is being swiped: <c>data-swiping</c>.</summary>
    public const string Swiping = "data-swiping";
}

// DRW-STR-02 (drawer/popup/DrawerPopupCssVars.ts).
/// <summary>Provides the names of the CSS variables a drawer's popup sets.</summary>
public static class DrawerPopupCssVars
{
    /// <summary>Represents the number of open drawers nested in this one: <c>--nested-drawers</c>.</summary>
    public const string NestedDrawers = "--nested-drawers";

    /// <summary>Represents the popup's measured height in pixels while a nested drawer is open or it animates out: <c>--drawer-height</c>.</summary>
    public const string Height = "--drawer-height";

    /// <summary>Represents the height of the frontmost drawer of the stack in pixels: <c>--drawer-frontmost-height</c>.</summary>
    public const string FrontmostHeight = "--drawer-frontmost-height";

    /// <summary>Represents the horizontal swipe movement in pixels: <c>--drawer-swipe-movement-x</c>.</summary>
    public const string SwipeMovementX = "--drawer-swipe-movement-x";

    /// <summary>Represents the vertical swipe movement in pixels: <c>--drawer-swipe-movement-y</c>.</summary>
    public const string SwipeMovementY = "--drawer-swipe-movement-y";

    /// <summary>Represents the offset of the active snap point in pixels: <c>--drawer-snap-point-offset</c>.</summary>
    public const string SnapPointOffset = "--drawer-snap-point-offset";

    /// <summary>Represents a factor from 0 to 1 derived from the release velocity, to shorten the exit transition: <c>--drawer-swipe-strength</c>.</summary>
    public const string SwipeStrength = "--drawer-swipe-strength";
}

// Base UI's Drawer.Popup (drawer/popup/DrawerPopup.tsx:116-410); the contract is docs/components/drawer.md (§3.11, §4.2;
// DRW-STR-03, STR-05 over dialog.md §3.6, §5.5, §6). Its own type over DialogPopupBehavior (decision 20 (a) of the M6
// plan, chosen by the user 2026-11-18):
// - DRW-API-07, FOC-01: a null InitialFocus names the popup's rendered id for every interaction type (DrawerPopup.tsx:306).
// - The style: --nested-drawers (DRW-DOM-08) and --drawer-frontmost-height while positive (DOM-10), rendered by C# in the
//   commit path (the dated §2 exception, decision 21 (a)), replace Dialog's --nested-dialogs (DLG-DOM-19 not adopted); the
//   consumer's style follows. The other variables are the `drawer` module's (8b).
// - DRW-EDGE-01: outside a DrawerViewport, a development warning after the first render.
/// <summary>
/// Represents a container for the drawer's contents, Base UI's <c>Drawer.Popup</c>, rendered as a <c>div</c> with the
/// <c>dialog</c> role.
/// </summary>
/// <remarks>
/// Place it inside a <see cref="DrawerViewport"/> inside the drawer's portal. Focus moves to the popup itself when the drawer
/// opens, stays inside it while the drawer is modal, and returns to the trigger when it closes.
/// </remarks>
public sealed class DrawerPopup : PartBase<DrawerPopupState>
{
    private static readonly StateDataAttribute[] s_expanded = [new(DrawerPopupDataAttributes.Expanded, string.Empty)];
    private static readonly StateDataAttribute[] s_nestedDrawerOpen = [new(DrawerPopupDataAttributes.NestedDrawerOpen, string.Empty)];
    private static readonly StateDataAttribute[] s_nestedDrawerSwiping = [new(DrawerPopupDataAttributes.NestedDrawerSwiping, string.Empty)];

    // drawer/popup/DrawerPopup.tsx stateAttributesMapping: the popup's open mapping; Nested keeps the default projection.
    private static readonly StateAttributesMapping<DrawerPopupState> s_mapping = PopupStateMapping.CreateOpen<DrawerPopupState>()
        .Map<bool>(nameof(DrawerPopupState.Expanded), static expanded => expanded ? s_expanded : null)
        .Map<bool>(nameof(DrawerPopupState.NestedDrawerOpen), static open => open ? s_nestedDrawerOpen : null)
        .Map<bool>(nameof(DrawerPopupState.NestedDrawerSwiping), static swiping => swiping ? s_nestedDrawerSwiping : null)
        .Map<SwipeDirection>(nameof(DrawerPopupState.SwipeDirection), DrawerSwipeDirections.Project);

    private DialogPopupBehavior? behavior;
    private DrawerPopupMeasurement? measurement;
    private IDisposable? drawerCount;
    private IDisposable? drawerStack;
    private Action? viewportChanged;
    private DrawerPopupState state = new(false, null, false, false, false, false, SwipeDirection.Down, false);
    private bool warnedViewport;

    [Inject]
    private IJSRuntime JSRuntime { get; set; } = null!;

    [Inject]
    private ILogger<DrawerPopup> Logger { get; set; } = null!;

    private bool HasConsumerId => AdditionalAttributes is not null && AdditionalAttributes.ContainsKey("id");

    private string? RenderedId => HasConsumerId ? AdditionalAttributes!["id"] as string : Context!.FloatingId;

    [CascadingParameter]
    private IDialogRootContext? Context { get; set; }

    [CascadingParameter]
    private DrawerRootContext? Drawer { get; set; }

    [CascadingParameter]
    private DrawerViewportContext? Viewport { get; set; }

    [CascadingParameter]
    private PopupPortalContext? Portal { get; set; }

    // DRW-API-07
    /// <summary>Gets or sets where focus moves when the drawer opens.</summary>
    /// <value>
    /// <see langword="null"/>, the default, for the popup itself; <see langword="true"/> for the first tabbable element inside
    /// the popup; <see langword="false"/> to leave focus where it is; an element or the id of an element to focus it.
    /// </value>
    [Parameter]
    public FocusTargetValue? InitialFocus { get; set; }

    /// <summary>Gets or sets a function that decides where focus moves when the drawer opens, from how it was opened.</summary>
    /// <value>
    /// A function called once per open with the interaction type (<see cref="InteractionType.Unknown"/> when it is not
    /// known). Its result is used as <see cref="InitialFocus"/> would be, except that <see langword="null"/> leaves focus
    /// where it is. When it is set, <see cref="InitialFocus"/> is ignored.
    /// </value>
    [Parameter]
    public Func<InteractionType, FocusTargetValue?>? InitialFocusResolver { get; set; }

    // DLG-API-21
    /// <summary>Gets or sets where focus moves when the drawer closes.</summary>
    /// <value>
    /// <see langword="null"/>, the default, or <see langword="true"/> for the trigger, else the element focused before the
    /// drawer opened; <see langword="false"/> to leave focus where it is; an element or the id of an element to focus it.
    /// </value>
    [Parameter]
    public FocusTargetValue? FinalFocus { get; set; }

    /// <summary>Gets or sets a function that decides where focus moves when the drawer closes, from how it was closed.</summary>
    /// <value>
    /// A function called once per close with the interaction type. Its result is used as <see cref="FinalFocus"/> would be,
    /// except that <see langword="null"/> leaves focus where it is. When it is set, <see cref="FinalFocus"/> is ignored.
    /// </value>
    [Parameter]
    public Func<InteractionType, FocusTargetValue?>? FinalFocusResolver { get; set; }

    /// <inheritdoc />
    protected override string DefaultTagName => "div";

    /// <inheritdoc />
    protected override DrawerPopupState State
    {
        get
        {
            IDialogRootContext root = behavior!.Context;
            DrawerRootContext drawer = Drawer!;
            bool nestedDrawerOpen = root.NestedOpenDrawerCount > 0;
            bool swiping = Viewport?.Swiping ?? false;
            bool expanded = drawer.ActiveSnapPoint is { IsFullHeight: true };
            if (state.Open != root.Open || state.TransitionStatus != root.TransitionStatus || state.Expanded != expanded || state.Nested != root.Nested
                || state.NestedDrawerOpen != nestedDrawerOpen || state.NestedDrawerSwiping != drawer.NestedSwiping || state.SwipeDirection != drawer.SwipeDirection
                || state.Swiping != swiping)
            {
                state = new DrawerPopupState(root.Open, root.TransitionStatus, expanded, root.Nested, nestedDrawerOpen, drawer.NestedSwiping, drawer.SwipeDirection, swiping);
            }

            return state;
        }
    }

    /// <inheritdoc />
    protected override StateAttributesMapping<DrawerPopupState>? StateAttributesMapping => s_mapping;

    // For the fixtures: completes once the transitions, focus and floating handles are attached.
    internal Task TwinAttached => behavior?.TwinAttached ?? Task.CompletedTask;

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        if (Context is null || Drawer is null)
        {
            throw DialogParts.MissingRoot("Drawer");
        }

        behavior = new DialogPopupBehavior("Drawer", JSRuntime, Logger, StateHasChanged, InvokeAsync, DispatchExceptionAsync);
        measurement = new DrawerPopupMeasurement(JSRuntime, Context, Drawer, InvokeAsync);
        base.OnInitialized();
    }

    // DRW-API-07, FOC-01: the popup's own id stands for Base UI's popupRef when InitialFocus is not given.
    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        string? id = RenderedId;
        FocusTargetValue? initial = InitialFocus;
        if (initial is null && InitialFocusResolver is null && id is not null)
        {
            initial = id;
        }
        behavior!.Apply(initial, InitialFocusResolver, FinalFocus, FinalFocusResolver, id);
    }

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        // DRW-EDGE-01 (DrawerPopup.tsx:137-147): after the first render, once, with nothing appended.
        if (firstRender && Viewport is null && !warnedViewport && DevelopmentWarnings.IsEnabled)
        {
            warnedViewport = true;
            DevelopmentWarnings.Write(
                Logger,
                "Iodine: `<DrawerPopup>` expected to be rendered within `<DrawerViewport>`. Omitting the viewport disables drawer swipe handling and touch scroll locking. Wrap `<DrawerPopup>` in `<DrawerViewport>`.");
        }

        // M6 track 8b: the Viewport's drawer handle writes the swipe attributes and styles on this element.
        Drawer!.SetPopupElement(Element);
        Task dialog = behavior!.OnAfterRenderAsync(Element, RenderedId);

        // Slice 3: the Popup's own handle measures it (DRW-STA-10), and the reports to the parent drawer follow the render.
        // Slice 5 (chosen by the user 2026-11-21, ADR 01's amendment of 2026-11-21): it starts beside the Dialog popup
        // behaviour's handles, not behind their imports and attaches, so a nested drawer's frontmost height reaches its
        // parent in eight one-way legs instead of sixteen.
        if (Element is { } element)
        {
            await measurement!.SyncAsync(element);
        }

        await dialog;
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            if (Element is { } element && string.Equals(Drawer?.PopupElement?.Id, element.Id, StringComparison.Ordinal))
            {
                Drawer!.SetPopupElement(null);
            }

            drawerCount?.Dispose();
            drawerStack?.Dispose();
            if (measurement is not null)
            {
                _ = measurement.DisposeAsync().AsTask();
            }

            if (viewportChanged is not null)
            {
                Viewport?.SetListener(null);
            }

            if (behavior is not null)
            {
                _ = behavior.DisposeAsync().AsTask();
            }
        }

        base.Dispose(disposing);
    }

    /// <inheritdoc />
    protected override void SubscribeSelectors()
    {
        behavior!.Subscribe(Context!, Portal);
        drawerCount = Context!.Subscribe(DialogFields.NestedOpenDrawerCount, StateHasChanged);
        drawerStack = Drawer!.Subscribe(DrawerFields.FrontmostHeight | DrawerFields.NestedSwiping | DrawerFields.Settings, StateHasChanged);
        if (Viewport is not null)
        {
            viewportChanged = StateHasChanged;
            Viewport.SetListener(viewportChanged);
        }
    }

    /// <inheritdoc />
    protected override void AddPartAttributes(AttributeList attributes)
    {
        behavior!.AddAttributes(attributes, HasConsumerId);
        attributes.Set("style", Style(Context!.NestedOpenDrawerCount, Drawer!.FrontmostHeight));
    }

    /// <inheritdoc />
    protected override void BuildBeforeElement(RenderTreeBuilder builder) => behavior!.BuildBeforeElement(builder);

    /// <inheritdoc />
    protected override void BuildAfterElement(RenderTreeBuilder builder) => behavior!.BuildAfterElement(builder);

    // DRW-DOM-08, DOM-10 (DrawerPopup.tsx:377, 381-383): the common counts are cached so a render without a frontmost height
    // allocates nothing; the frontmost height in pixels while positive.
    private static string Style(int nestedDrawers, double frontmostHeight)
    {
        string count = nestedDrawers switch
        {
            0 => DrawerPopupCssVars.NestedDrawers + ": 0",
            1 => DrawerPopupCssVars.NestedDrawers + ": 1",
            2 => DrawerPopupCssVars.NestedDrawers + ": 2",
            _ => DrawerPopupCssVars.NestedDrawers + ": " + nestedDrawers.ToString(CultureInfo.InvariantCulture),
        };
        return frontmostHeight > 0
            ? count + "; " + DrawerPopupCssVars.FrontmostHeight + ": " + frontmostHeight.ToString(CultureInfo.InvariantCulture) + "px"
            : count;
    }
}

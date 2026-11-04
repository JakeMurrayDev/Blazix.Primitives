using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace Blazix.Primitives;

// MNU §3.5 state object (menu/positioner/MenuPositioner.tsx:224-231). Side, Align and AnchorHidden are the `positioning`
// module's from attach (ADR 04 §2): mirrored. Nested and Instant are C#'s (MNU §16 Q3: projected generically).
/// <summary>Represents the state of a menu positioner, which the part exposes as data attributes and passes to its class and style functions.</summary>
/// <param name="Open">A value indicating whether the menu is open; projected as <c>data-open</c> or <c>data-closed</c>.</param>
/// <param name="Side">The side of the anchor the menu is placed against, or <see langword="null"/> before the first placement; projected as <c>data-side</c>.</param>
/// <param name="Align">The alignment of the menu against the anchor, or <see langword="null"/> before the first placement; projected as <c>data-align</c>.</param>
/// <param name="AnchorHidden">A value indicating whether the anchor is hidden from view; projected as <c>data-anchor-hidden</c>.</param>
/// <param name="Nested">A value indicating whether the menu is a submenu; projected as <c>data-nested</c>.</param>
/// <param name="Instant">Why the next transition is skipped, or <see langword="null"/>; projected as <c>data-instant</c>.</param>
[PartState]
public sealed partial record MenuPositionerState(
    bool Open,
    [property: MirroredField] Side? Side,
    [property: MirroredField] Align? Align,
    [property: MirroredField] bool AnchorHidden,
    bool Nested,
    MenuInstantType? Instant);

// menu/positioner/MenuPositionerDataAttributes.ts (MNU-DOM-04).
/// <summary>Provides the names of the data attributes a menu positioner renders.</summary>
public static class MenuPositionerDataAttributes
{
    /// <summary>Represents the attribute present while the menu is open: <c>data-open</c>.</summary>
    public const string Open = PopupDataAttributes.Open;

    /// <summary>Represents the attribute present while the menu is closed: <c>data-closed</c>.</summary>
    public const string Closed = PopupDataAttributes.Closed;

    /// <summary>Represents the attribute present while the anchor is hidden: <c>data-anchor-hidden</c>.</summary>
    public const string AnchorHidden = PopupDataAttributes.AnchorHidden;

    /// <summary>Represents the attribute that names the side of the anchor the menu is placed against: <c>data-side</c>.</summary>
    public const string Side = PopupDataAttributes.Side;

    /// <summary>Represents the attribute that names the alignment of the menu against the anchor: <c>data-align</c>.</summary>
    public const string Align = PopupDataAttributes.Align;

    /// <summary>Represents the attribute present while the menu is a submenu: <c>data-nested</c>.</summary>
    public const string Nested = "data-nested";

    /// <summary>Represents the attribute that names why the next transition is skipped: <c>data-instant</c>.</summary>
    public const string Instant = "data-instant";
}

// menu/positioner/MenuPositionerCssVars.ts (MNU-DOM-04); ADR 08 §3's <Part>CssVars name.
/// <summary>Provides the names of the CSS variables a menu positioner sets.</summary>
public static class MenuPositionerCssVars
{
    /// <summary>Represents the anchor's width: <c>--anchor-width</c>.</summary>
    public const string AnchorWidth = "--anchor-width";

    /// <summary>Represents the anchor's height: <c>--anchor-height</c>.</summary>
    public const string AnchorHeight = "--anchor-height";

    /// <summary>Represents the width available before the menu overflows its collision boundary: <c>--available-width</c>.</summary>
    public const string AvailableWidth = "--available-width";

    /// <summary>Represents the height available before the menu overflows its collision boundary: <c>--available-height</c>.</summary>
    public const string AvailableHeight = "--available-height";

    /// <summary>Represents the point the menu grows from, for transforms: <c>--transform-origin</c>.</summary>
    public const string TransformOrigin = "--transform-origin";

    /// <summary>Represents the positioner's width: <c>--positioner-width</c>.</summary>
    public const string PositionerWidth = "--positioner-width";

    /// <summary>Represents the positioner's height: <c>--positioner-height</c>.</summary>
    public const string PositionerHeight = "--positioner-height";
}

// Base UI's Menu.Positioner (menu/positioner/MenuPositioner.tsx); the contract is docs/components/menu.md §3.5, §10. A thin
// shell over MenuPositionerBehavior, which ContextMenuPositioner (track 7) shares; `null` parameters are read by parent
// kind (MNU-API-26..30).
/// <summary>Positions the menu against its anchor, Base UI's <c>Menu.Positioner</c>, rendered as a <c>div</c>.</summary>
public sealed class MenuPositioner : PartBase<MenuPositionerState>
{
    private static readonly StateAttributesMapping<MenuPositionerState> s_mapping = PopupStateMapping.CreateOpen<MenuPositionerState>();

    private MenuPositionerBehavior? behavior;
    private MenuPositionerState? state;

    [Inject]
    private IJSRuntime JSRuntime { get; set; } = null!;

    [Inject]
    private ILogger<MenuPositioner> Logger { get; set; } = null!;

    [CascadingParameter]
    private MenuRootContext? RootContext { get; set; }

    [CascadingParameter]
    private MenuPortalContext? PortalContext { get; set; }

    [CascadingParameter]
    private PopupPortalContext? Portal { get; set; }

    [CascadingParameter]
    private DirectionProviderContext? DirectionContext { get; set; }

    // MNU-API-30
    /// <summary>Gets or sets the element or point the menu is positioned against.</summary>
    /// <value>An element, an element's id or a virtual element; <see langword="null"/>, the default, for the active trigger.</value>
    [Parameter]
    public AnchorValue? Anchor { get; set; }

    // MNU-API-28
    /// <summary>Gets or sets the CSS position the positioner uses.</summary>
    /// <value><see cref="Blazix.Primitives.PositionMethod.Absolute"/> when <see langword="null"/>, the default.</value>
    [Parameter]
    public PositionMethod? PositionMethod { get; set; }

    // MNU-API-26
    /// <summary>Gets or sets the side of the anchor the menu is placed against.</summary>
    /// <value>
    /// <see langword="null"/>, the default, for <see cref="Blazix.Primitives.Side.Bottom"/>, or
    /// <see cref="Blazix.Primitives.Side.InlineEnd"/> for a submenu.
    /// </value>
    [Parameter]
    public Side? Side { get; set; }

    // MNU-API-27
    /// <summary>Gets or sets the distance between the anchor and the menu.</summary>
    /// <value>A number of pixels or a function of the placement; <see langword="null"/>, the default, for 0.</value>
    [Parameter]
    public OffsetValue? SideOffset { get; set; }

    // MNU-API-26
    /// <summary>Gets or sets how the menu is aligned against the anchor on the other axis.</summary>
    /// <value>
    /// <see langword="null"/>, the default, for <see cref="Blazix.Primitives.Align.Center"/>, or
    /// <see cref="Blazix.Primitives.Align.Start"/> for a submenu.
    /// </value>
    [Parameter]
    public Align? Align { get; set; }

    // MNU-API-27
    /// <summary>Gets or sets the offset along the alignment axis.</summary>
    /// <value>A number of pixels or a function of the placement; <see langword="null"/>, the default, for 0.</value>
    [Parameter]
    public OffsetValue? AlignOffset { get; set; }

    // MNU-API-28
    /// <summary>Gets or sets the area the menu is kept within.</summary>
    /// <value><see langword="null"/>, the default, for the clipping ancestors.</value>
    [Parameter]
    public CollisionBoundaryValue? CollisionBoundary { get; set; }

    // MNU-API-28
    /// <summary>Gets or sets the space kept between the menu and the edges of its collision boundary.</summary>
    /// <value><see langword="null"/>, the default, for 5 pixels.</value>
    [Parameter]
    public CollisionPaddingValue? CollisionPadding { get; set; }

    // MNU-API-28
    /// <summary>Gets or sets a value indicating whether the menu stays in view when its anchor scrolls away.</summary>
    /// <value><see langword="false"/> by default.</value>
    [Parameter]
    public bool Sticky { get; set; }

    // MNU-API-28
    /// <summary>Gets or sets the smallest distance between the arrow and the menu's corners.</summary>
    /// <value><see langword="null"/>, the default, for 5 pixels.</value>
    [Parameter]
    public double? ArrowPadding { get; set; }

    // MNU-API-28
    /// <summary>Gets or sets a value indicating whether the menu stops following its anchor's layout changes.</summary>
    /// <value><see langword="false"/> by default.</value>
    [Parameter]
    public bool DisableAnchorTracking { get; set; }

    // MNU-API-29
    /// <summary>Gets or sets how the menu avoids collisions with its boundary.</summary>
    /// <value><see langword="null"/>, the default, for a flip with no fallback axis, or with the end axis for a submenu.</value>
    [Parameter]
    public CollisionAvoidance? CollisionAvoidance { get; set; }

    /// <inheritdoc />
    protected override string DefaultTagName => "div";

    // The pipeline reads the state before the part's attributes, so the record follows the store and the mirror here.
    /// <inheritdoc />
    protected override MenuPositionerState State
    {
        get
        {
            MenuPositionerBehavior current = behavior!;
            if (state is null || state.Open != current.Open || state.Side != current.Side || state.Align != current.Align
                || state.AnchorHidden != current.AnchorHidden || state.Nested != current.Nested || state.Instant != current.Instant)
            {
                state = new MenuPositionerState(current.Open, current.Side, current.Align, current.AnchorHidden, current.Nested, current.Instant);
            }

            return state;
        }
    }

    /// <inheritdoc />
    protected override StateAttributesMapping<MenuPositionerState>? StateAttributesMapping => s_mapping;

    // ADR 02 decision 1: under a substitute callback the twins find the element by id.
    /// <inheritdoc />
    protected override bool UsesGeneratedId => RenderFunction is not null;

    /// <summary>Gets a task that completes once the positioner's handles have attached.</summary>
    internal Task TwinAttached => behavior?.TwinAttached ?? Task.CompletedTask;

    /// <inheritdoc />
    public override Task SetParametersAsync(ParameterView parameters)
    {
        // The part replaces ChildContent with its own cascade, so a parameter set without content must not keep it.
        ChildContent = null;
        return base.SetParametersAsync(parameters);
    }

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        behavior = new MenuPositionerBehavior("Menu", JSRuntime, Logger, StateHasChanged, OnMirrorChanged, InvokeAsync, DispatchExceptionAsync);
        behavior.Subscribe(RootContext, PortalContext, Portal);
        base.OnInitialized();
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        behavior!.Apply(
            new MenuPositionerParameters(Anchor, PositionMethod, Side, Align, SideOffset, AlignOffset, CollisionBoundary, CollisionPadding, CollisionAvoidance, ArrowPadding, Sticky, DisableAnchorTracking),
            DirectionContext);
        ChildContent = behavior.CascadeContent(ChildContent);
    }

    /// <inheritdoc />
    protected override Task OnAfterRenderAsync(bool firstRender) => behavior!.OnAfterRenderAsync(Element);

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing && behavior is not null)
        {
            _ = behavior.DisposeAsync().AsTask();
        }

        base.Dispose(disposing);
    }

    /// <inheritdoc />
    protected override void AddPartAttributes(AttributeList attributes) => behavior!.AddAttributes(attributes);

    /// <inheritdoc />
    protected override void FinishAttributes(AttributeList attributes) => behavior!.FinishAttributes(attributes);

    /// <inheritdoc />
    protected override void BuildBeforeElement(RenderTreeBuilder builder) => behavior!.BuildBeforeElement(builder);

    // A part renders on a mirror change only when its state record changed (track 2's rule).
    private void OnMirrorChanged()
    {
        MenuPositionerState? previous = state;
        if (previous is null || previous != State)
        {
            StateHasChanged();
        }
    }
}

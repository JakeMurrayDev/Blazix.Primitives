using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace Blazix.Primitives;

// CTX-STA-02 (context-menu/positioner/ContextMenuPositioner.tsx:10): Menu.Positioner's state object. Side, Align and
// AnchorHidden are the `positioning` module's from attach (ADR 04 §2): mirrored. Nested and Instant are C#'s. Design point 5
// (a) of M5 track 7: Menu's MenuInstantType.
/// <summary>Represents the state of a context menu positioner, which the part exposes as data attributes and passes to its class and style functions.</summary>
/// <param name="Open">A value indicating whether the menu is open; projected as <c>data-open</c> or <c>data-closed</c>.</param>
/// <param name="Side">The side of the anchor the menu is placed against, or <see langword="null"/> before the first placement; projected as <c>data-side</c>.</param>
/// <param name="Align">The alignment of the menu against the anchor, or <see langword="null"/> before the first placement; projected as <c>data-align</c>.</param>
/// <param name="AnchorHidden">A value indicating whether the anchor is hidden from view; projected as <c>data-anchor-hidden</c>.</param>
/// <param name="Nested">A value indicating whether the menu is a submenu; projected as <c>data-nested</c>.</param>
/// <param name="Instant">Why the next transition is skipped, or <see langword="null"/>; projected as <c>data-instant</c>.</param>
[PartState]
public sealed partial record ContextMenuPositionerState(
    bool Open,
    [property: MirroredField] Side? Side,
    [property: MirroredField] Align? Align,
    [property: MirroredField] bool AnchorHidden,
    bool Nested,
    MenuInstantType? Instant);

// context-menu/positioner/ContextMenuPositionerDataAttributes.ts (CTX-DOM-05, MNU-DOM-04).
/// <summary>Provides the names of the data attributes a context menu positioner renders.</summary>
public static class ContextMenuPositionerDataAttributes
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

// context-menu/positioner/ContextMenuPositionerCssVars.ts (MNU-POS-08, adopted); ADR 08 §3's <Part>CssVars name.
/// <summary>Provides the names of the CSS variables a context menu positioner sets.</summary>
public static class ContextMenuPositionerCssVars
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

// Base UI's ContextMenu.Positioner (context-menu/positioner/ContextMenuPositioner.tsx, which is Menu.Positioner with a
// narrower prop type); the contract is docs/components/context-menu.md §3.3, §10. A thin shell over MenuPositionerBehavior
// (decision 12 of the M5 plan, ADR 08 Amendments 2026-11-03 (3)), whose defaults read the parent kind: at the root, align
// start, -5 and 2 when no side is given and the align is not center, arrow padding 0, `fixed`, the layout viewport's
// shift (CTX-API-07..13, POS-01..07); in a submenu, Menu's. No PositionMethod (design point 4 (a) of M5 track 7: Base UI's
// is `@ignore @deprecated`, and `fixed` is forced under a context menu). An explicit Anchor beats the pointer (CTX-API-10).
/// <summary>
/// Positions the context menu against the pointer, a custom anchor or a submenu's trigger, Base UI's
/// <c>ContextMenu.Positioner</c>, rendered as a <c>div</c>.
/// </summary>
public sealed class ContextMenuPositioner : PartBase<ContextMenuPositionerState>
{
    private static readonly StateAttributesMapping<ContextMenuPositionerState> s_mapping = PopupStateMapping.CreateOpen<ContextMenuPositionerState>();

    private MenuPositionerBehavior? behavior;
    private ContextMenuPositionerState? state;

    [Inject]
    private IJSRuntime JSRuntime { get; set; } = null!;

    [Inject]
    private ILogger<ContextMenuPositioner> Logger { get; set; } = null!;

    [CascadingParameter]
    private MenuRootContext? RootContext { get; set; }

    [CascadingParameter]
    private MenuPortalContext? PortalContext { get; set; }

    [CascadingParameter]
    private PopupPortalContext? Portal { get; set; }

    [CascadingParameter]
    private DirectionProviderContext? DirectionContext { get; set; }

    [CascadingParameter]
    private IMenuParentContext? ParentContext { get; set; }

    // CTX-API-10
    /// <summary>Gets or sets the element or point the menu is positioned against.</summary>
    /// <value>
    /// An element, an element's id or a virtual element; <see langword="null"/>, the default, for the pointer at the root
    /// and the submenu trigger in a submenu.
    /// </value>
    [Parameter]
    public AnchorValue? Anchor { get; set; }

    // CTX-API-07
    /// <summary>Gets or sets the side of the anchor the menu is placed against.</summary>
    /// <value>
    /// <see langword="null"/>, the default, for <see cref="Blazix.Primitives.Side.Bottom"/>, or
    /// <see cref="Blazix.Primitives.Side.InlineEnd"/> for a submenu.
    /// </value>
    [Parameter]
    public Side? Side { get; set; }

    // CTX-API-09
    /// <summary>Gets or sets the distance between the anchor and the menu.</summary>
    /// <value>
    /// A number of pixels or a function of the placement; <see langword="null"/>, the default, for -5 at the root when no
    /// <see cref="Side"/> is given and the alignment is not <see cref="Blazix.Primitives.Align.Center"/>, otherwise 0.
    /// </value>
    [Parameter]
    public OffsetValue? SideOffset { get; set; }

    // CTX-API-08
    /// <summary>Gets or sets how the menu is aligned against the anchor on the other axis.</summary>
    /// <value><see langword="null"/>, the default, for <see cref="Blazix.Primitives.Align.Start"/>.</value>
    [Parameter]
    public Align? Align { get; set; }

    // CTX-API-09
    /// <summary>Gets or sets the offset along the alignment axis.</summary>
    /// <value>
    /// A number of pixels or a function of the placement; <see langword="null"/>, the default, for 2 at the root when no
    /// <see cref="Side"/> is given and the alignment is not <see cref="Blazix.Primitives.Align.Center"/>, otherwise 0.
    /// </value>
    [Parameter]
    public OffsetValue? AlignOffset { get; set; }

    // CTX-API-13
    /// <summary>Gets or sets the area the menu is kept within.</summary>
    /// <value><see langword="null"/>, the default, for the clipping ancestors.</value>
    [Parameter]
    public CollisionBoundaryValue? CollisionBoundary { get; set; }

    // CTX-API-13
    /// <summary>Gets or sets the space kept between the menu and the edges of its collision boundary.</summary>
    /// <value><see langword="null"/>, the default, for 5 pixels.</value>
    [Parameter]
    public CollisionPaddingValue? CollisionPadding { get; set; }

    // CTX-API-13
    /// <summary>Gets or sets a value indicating whether the menu stays in view when its anchor scrolls away.</summary>
    /// <value><see langword="false"/> by default.</value>
    [Parameter]
    public bool Sticky { get; set; }

    // CTX-API-11
    /// <summary>Gets or sets the smallest distance between the arrow and the menu's corners.</summary>
    /// <value>
    /// <see langword="null"/>, the default, for 5 pixels in a submenu; the root positioner always uses 0 and ignores it.
    /// </value>
    [Parameter]
    public double? ArrowPadding { get; set; }

    // CTX-API-13
    /// <summary>Gets or sets a value indicating whether the menu stops following its anchor's layout changes.</summary>
    /// <value><see langword="false"/> by default.</value>
    [Parameter]
    public bool DisableAnchorTracking { get; set; }

    // CTX-API-13
    /// <summary>Gets or sets how the menu avoids collisions with its boundary.</summary>
    /// <value><see langword="null"/>, the default, for a flip with no fallback axis, or with the end axis for a submenu.</value>
    [Parameter]
    public CollisionAvoidance? CollisionAvoidance { get; set; }

    /// <inheritdoc />
    protected override string DefaultTagName => "div";

    // The pipeline reads the state before the part's attributes, so the record follows the store and the mirror here.
    /// <inheritdoc />
    protected override ContextMenuPositionerState State
    {
        get
        {
            MenuPositionerBehavior current = behavior!;
            if (state is null || state.Open != current.Open || state.Side != current.Side || state.Align != current.Align
                || state.AnchorHidden != current.AnchorHidden || state.Nested != current.Nested || state.Instant != current.Instant)
            {
                state = new ContextMenuPositionerState(current.Open, current.Side, current.Align, current.AnchorHidden, current.Nested, current.Instant);
            }

            return state;
        }
    }

    /// <inheritdoc />
    protected override StateAttributesMapping<ContextMenuPositionerState>? StateAttributesMapping => s_mapping;

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
        behavior = new MenuPositionerBehavior("ContextMenu", JSRuntime, Logger, StateHasChanged, OnMirrorChanged, InvokeAsync, DispatchExceptionAsync);
        behavior.Subscribe(RootContext, PortalContext, Portal, ParentContext);
        base.OnInitialized();
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        behavior!.Apply(
            new MenuPositionerParameters(Anchor, PositionMethod: null, Side, Align, SideOffset, AlignOffset, CollisionBoundary, CollisionPadding, CollisionAvoidance, ArrowPadding, Sticky, DisableAnchorTracking),
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
        ContextMenuPositionerState? previous = state;
        if (previous is null || previous != State)
        {
            StateHasChanged();
        }
    }
}

namespace Blazix.Primitives.Interop;

// ADR 04 §1 (the `positioning` mechanism module), ADR 03 and ADR 04 Amendments 2026-10-27 (chosen by the user
// 2026-10-27): the options of one Positioner's handle. Values never cross as elements: the anchor and the Arrow are
// found by id; the element cases of AnchorValue and CollisionBoundaryValue travel through track 1's references array once
// it is on main. Value equality (ADR 04 and ADR 16 Amendments 2026-10-27): a re-render that builds equal options sends no
// update. The open and mounted inputs are read from the applied batch (design point 7, settled by the orchestrator
// 2026-10-27), so they are not fields here. The session token (design point 4) is inert until the `interactions` export
// exists (track 3).
/// <summary>The options of the <c>positioning</c> module, sent on <c>attach</c> and on every <c>update</c> of a positioner's handle.</summary>
internal sealed record PositioningOptions : TwinOptions
{
    /// <summary>Gets the popup's session token, which the module looks the popup's session up by.</summary>
    /// <value>The token the popup root drew, or <see langword="null"/>, the default, for none.</value>
    public string? Session { get; init; }

    /// <summary>Gets the selector the positioner matches while it is rendered open.</summary>
    /// <value>A CSS selector, or <see langword="null"/>, the default, for <c>[data-open]</c>.</value>
    public string? OpenSelector { get; init; }

    /// <summary>Gets a value indicating whether the popup stays in the document while closed.</summary>
    /// <value><see langword="true"/> to track the anchor only while mounted and to hide the positioner again on each mount; the default is <see langword="false"/>.</value>
    public bool KeepMounted { get; init; }

    /// <summary>Gets the popup's transition status.</summary>
    /// <value><c>starting</c>, which turns transitions off on the positioner, <c>ending</c>, <c>idle</c>, or <see langword="null"/>, the default, for none.</value>
    public string? TransitionStatus { get; init; }

    /// <summary>Gets the id of the active trigger, which the popup is positioned against when no anchor is given.</summary>
    /// <value>The trigger element's id, or <see langword="null"/>, the default, for none.</value>
    public string? TriggerId { get; init; }

    /// <summary>Gets the id of the element the popup is positioned against.</summary>
    /// <value>The anchor element's id, or <see langword="null"/>, the default, for none.</value>
    public string? AnchorId { get; init; }

    /// <summary>Gets the rectangle the popup is positioned against instead of an element.</summary>
    /// <value>The rectangle in viewport coordinates, or <see langword="null"/>, the default, for none.</value>
    public PositioningRect? VirtualAnchor { get; init; }

    /// <summary>Gets the CSS <c>position</c> of the positioner once positioned.</summary>
    /// <value><c>absolute</c>, the default, or <c>fixed</c>.</value>
    public string PositionMethod { get; init; } = "absolute";

    /// <summary>Gets the requested side of the anchor.</summary>
    /// <value><c>top</c>, <c>bottom</c>, the default, <c>left</c>, <c>right</c>, <c>inline-end</c> or <c>inline-start</c>.</value>
    public string Side { get; init; } = "bottom";

    /// <summary>Gets the requested alignment along the side.</summary>
    /// <value><c>start</c>, <c>center</c>, the default, or <c>end</c>.</value>
    public string Align { get; init; } = "center";

    /// <summary>Gets the distance between the anchor and the popup.</summary>
    /// <value>The distance in CSS pixels; the default is 0.</value>
    public double SideOffset { get; init; }

    /// <summary>Gets the distance along the alignment axis.</summary>
    /// <value>The distance in CSS pixels; the default is 0.</value>
    public double AlignOffset { get; init; }

    /// <summary>Gets the literal boundary the popup avoids overflowing.</summary>
    /// <value><c>clipping-ancestors</c>, or <see langword="null"/>, the default, when another boundary or none is given.</value>
    public string? CollisionBoundary { get; init; }

    /// <summary>Gets the boundary rectangle the popup avoids overflowing.</summary>
    /// <value>The rectangle in viewport coordinates, or <see langword="null"/>, the default, for none.</value>
    public PositioningRect? BoundaryRect { get; init; }

    /// <summary>Gets the space the popup keeps from every edge of its boundary.</summary>
    /// <value>The padding in CSS pixels, or <see langword="null"/>, the default, for 5 unless <see cref="CollisionPaddingSides"/> is given.</value>
    public double? CollisionPadding { get; init; }

    /// <summary>Gets the space the popup keeps from each edge of its boundary.</summary>
    /// <value>The padding per side, or <see langword="null"/>, the default, for none.</value>
    public PositioningPadding? CollisionPaddingSides { get; init; }

    /// <summary>Gets a value indicating whether the popup stays inside its boundary when the anchor leaves it.</summary>
    /// <value><see langword="true"/> to shift on the side axis without a limit; the default is <see langword="false"/>.</value>
    public bool Sticky { get; init; }

    /// <summary>Gets the distance between the arrow and the popup's edges.</summary>
    /// <value>The padding in CSS pixels; the default is 5.</value>
    public double ArrowPadding { get; init; } = 5;

    /// <summary>Gets a value indicating whether the popup stops following its anchor on scroll, resize and layout shift.</summary>
    /// <value><see langword="true"/> to turn anchor tracking off; the default is <see langword="false"/>.</value>
    public bool DisableAnchorTracking { get; init; }

    /// <summary>Gets how each axis avoids collisions with the boundary.</summary>
    /// <value>The modes, or <see langword="null"/>, the default, for flipping on both axes and falling back to the end side.</value>
    public PositioningCollisionAvoidance? CollisionAvoidance { get; init; }

    /// <summary>Gets the private shift options a context menu or navigation menu passes.</summary>
    /// <value>The options, or <see langword="null"/>, the default, for none.</value>
    public PositioningShift? Shift { get; init; }

    /// <summary>Gets whether a flipped placement is locked while the popup stays mounted.</summary>
    /// <value><c>side</c> to lock the side, <c>placement</c> to lock the alignment too, or <see langword="null"/>, the default, for no lock.</value>
    public string? LazyFlip { get; init; }

    /// <summary>Gets a value indicating whether a viewport part is present, which positions a transitioning popup from the far edges.</summary>
    /// <value><see langword="true"/> to add the adaptive origin; the default is <see langword="false"/>.</value>
    public bool AdaptiveOrigin { get; init; }

    /// <summary>Gets the reading direction, which resolves the logical sides.</summary>
    /// <value><c>ltr</c>, the default, or <c>rtl</c>.</value>
    public string Direction { get; init; } = "ltr";

    /// <summary>Gets the id of the popup's arrow element.</summary>
    /// <value>The arrow element's id, or <see langword="null"/>, the default, when the popup has no arrow.</value>
    public string? ArrowId { get; init; }

    /// <summary>Gets the consumer's merged style of the arrow, which the module composes under the arrow's position from the arrow's handover on.</summary>
    /// <value>The style string, or <see langword="null"/>, the default, for none.</value>
    public string? ArrowStyle { get; init; }
}

/// <summary>A rectangle in viewport coordinates, in CSS pixels.</summary>
/// <param name="X">The distance from the left edge of the viewport.</param>
/// <param name="Y">The distance from the top edge of the viewport.</param>
/// <param name="Width">The width.</param>
/// <param name="Height">The height.</param>
internal sealed record PositioningRect(double X, double Y, double Width, double Height);

/// <summary>A padding per side, in CSS pixels.</summary>
/// <param name="Top">The padding from the top edge.</param>
/// <param name="Right">The padding from the right edge.</param>
/// <param name="Bottom">The padding from the bottom edge.</param>
/// <param name="Left">The padding from the left edge.</param>
internal sealed record PositioningPadding(double Top, double Right, double Bottom, double Left);

/// <summary>The collision-avoidance modes of the two axes and the fallback axis side, as literals.</summary>
/// <param name="Side"><c>flip</c>, <c>shift</c> or <c>none</c>, or <see langword="null"/> for <c>flip</c>.</param>
/// <param name="Align"><c>flip</c>, <c>shift</c> or <c>none</c>, or <see langword="null"/> for <c>flip</c>.</param>
/// <param name="FallbackAxisSide"><c>start</c>, <c>end</c> or <c>none</c>, or <see langword="null"/> for <c>end</c>.</param>
internal sealed record PositioningCollisionAvoidance(string? Side, string? Align, string? FallbackAxisSide);

/// <summary>The private shift options.</summary>
/// <param name="CrossAxis">Whether the popup also shifts on the side axis.</param>
/// <param name="RootBoundary"><c>layoutViewport</c>, or <see langword="null"/> for the visual viewport.</param>
internal sealed record PositioningShift(bool CrossAxis, string? RootBoundary);

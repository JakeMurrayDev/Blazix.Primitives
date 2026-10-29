namespace Blazix.Primitives;

// What a family's Positioner part hands its PositionerBehavior on every parameter set (M4 track 2, brief deliverable 6):
// the twelve FLT §3.1 values, the popup's open, mounted and kept-mounted state, its transition status, whether a Viewport
// is present (PSH-POS-02), the active trigger's id (PSH-POS-01) and the session token (ADR 04 Amendments 2026-10-27). The
// direction comes from the cascaded DirectionProviderContext. A record, so an equal parameter set changes nothing.
/// <summary>The inputs of a positioner's behavior, filled by the part on every parameter set.</summary>
internal sealed record PositionerInputs
{
    /// <summary>Gets the anchor the popup is positioned against.</summary>
    /// <value>An element, an element id or a virtual rectangle; <see langword="null"/> for the active trigger.</value>
    public AnchorValue? Anchor { get; init; }

    /// <summary>Gets the CSS <c>position</c> once positioned.</summary>
    /// <value>The method, or <see langword="null"/> for <see cref="Primitives.PositionMethod.Absolute"/>.</value>
    public PositionMethod? PositionMethod { get; init; }

    /// <summary>Gets the requested side.</summary>
    /// <value>The side, or <see langword="null"/> for <see cref="Primitives.Side.Bottom"/>.</value>
    public Side? Side { get; init; }

    /// <summary>Gets the distance from the anchor.</summary>
    /// <value>A number of pixels or an offset computed on every pass, or <see langword="null"/> for 0.</value>
    public OffsetValue? SideOffset { get; init; }

    /// <summary>Gets the requested alignment.</summary>
    /// <value>The alignment, or <see langword="null"/> for <see cref="Primitives.Align.Center"/>.</value>
    public Align? Align { get; init; }

    /// <summary>Gets the distance along the alignment axis.</summary>
    /// <value>A number of pixels or an offset computed on every pass, or <see langword="null"/> for 0.</value>
    public OffsetValue? AlignOffset { get; init; }

    /// <summary>Gets the area the popup avoids overflowing.</summary>
    /// <value>The boundary, or <see langword="null"/> for the clipping ancestors.</value>
    public CollisionBoundaryValue? CollisionBoundary { get; init; }

    /// <summary>Gets the space kept from the boundary's edges.</summary>
    /// <value>The padding, or <see langword="null"/> for 5 pixels on every side.</value>
    public CollisionPaddingValue? CollisionPadding { get; init; }

    /// <summary>Gets a value indicating whether the popup stays inside the boundary when the anchor leaves it.</summary>
    /// <value><see langword="true"/> to keep the popup in view; the default is <see langword="false"/>.</value>
    public bool Sticky { get; init; }

    /// <summary>Gets the distance between the arrow and the popup's edges.</summary>
    /// <value>The padding in pixels, or <see langword="null"/> for 5.</value>
    public double? ArrowPadding { get; init; }

    /// <summary>Gets a value indicating whether anchor tracking is off.</summary>
    /// <value><see langword="true"/> to stop following the anchor; the default is <see langword="false"/>.</value>
    public bool DisableAnchorTracking { get; init; }

    /// <summary>Gets how each axis avoids collisions.</summary>
    /// <value>The modes, or <see langword="null"/> for flipping on both axes with the end side as the fallback.</value>
    public CollisionAvoidance? CollisionAvoidance { get; init; }

    /// <summary>Gets a value indicating whether the popup is open, which lets pointer events reach the positioner.</summary>
    /// <value><see langword="true"/> while open.</value>
    public bool Open { get; init; }

    /// <summary>Gets a value indicating whether the positioner is mounted; a kept-mounted positioner carries <c>hidden</c> otherwise.</summary>
    /// <value><see langword="true"/> while mounted.</value>
    public bool Mounted { get; init; }

    /// <summary>Gets a value indicating whether the popup stays in the document while closed.</summary>
    /// <value><see langword="true"/> for a kept-mounted popup.</value>
    public bool KeepMounted { get; init; }

    /// <summary>Gets the popup's transition status.</summary>
    /// <value>The status, or <see langword="null"/> for none.</value>
    public TransitionStatus? TransitionStatus { get; init; }

    /// <summary>Gets a value indicating whether a viewport part is present.</summary>
    /// <value><see langword="true"/> to position a transitioning popup from the far edges.</value>
    public bool AdaptiveOrigin { get; init; }

    /// <summary>Gets a value indicating whether a wrapping anchor is narrowed to one of its line boxes.</summary>
    /// <value><see langword="true"/> to position against the hovered line, else the line on the popup's side.</value>
    public bool HoveredLine { get; init; }

    /// <summary>Gets a value indicating whether a reopen while still mounted runs one positioning pass.</summary>
    /// <value><see langword="true"/> to re-anchor a popup that reopens during its exit or while kept mounted.</value>
    public bool PositionOnOpen { get; init; }

    /// <summary>Gets the id of the active trigger.</summary>
    /// <value>The trigger element's id, or <see langword="null"/> for none.</value>
    public string? TriggerId { get; init; }

    /// <summary>Gets the popup's session token.</summary>
    /// <value>The token the popup root drew, or <see langword="null"/> for none.</value>
    public string? SessionToken { get; init; }
}

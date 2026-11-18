namespace Blazix.Primitives;

// TST §3.2, the toast object's `positionerProps` (ToastManagerPositionerProps, useToastManager.ts:99-107: every
// Positioner prop except `toast`, plus `anchor?: Element | null`); TST-POS-02 (the second level of the three-level
// default) and TST-PTR-22 (`anchor !== undefined`, null included, disables swiping): whether Anchor was assigned is kept
// apart from its value, and takes part in equality, because "set to none" and "not set" are different settings.
/// <summary>Represents the positioning defaults that an anchored toast carries for its toast positioner.</summary>
/// <remarks>
/// A toast positioner uses each value here for a parameter it is not given, and its own default for a value that is
/// <see langword="null"/> here as well.
/// </remarks>
public sealed record ToastPositionerSettings
{
    private readonly AnchorValue? anchor;

    /// <summary>Gets the element to position the toast against.</summary>
    /// <value>
    /// The anchor, or <see langword="null"/>. Only an element reference anchors the toast. Assigning this property, even
    /// to <see langword="null"/>, also stops the toast from being swiped away.
    /// </value>
    public AnchorValue? Anchor
    {
        get => anchor;
        init
        {
            anchor = value;
            HasAnchor = true;
        }
    }

    /// <summary>Gets the CSS position strategy.</summary>
    /// <value>The strategy, or <see langword="null"/> for the positioner's default.</value>
    public PositionMethod? PositionMethod { get; init; }

    /// <summary>Gets the side of the anchor to place the toast on.</summary>
    /// <value>The side, or <see langword="null"/> for the positioner's default.</value>
    public Side? Side { get; init; }

    /// <summary>Gets the distance between the anchor and the toast.</summary>
    /// <value>The offset, or <see langword="null"/> for the positioner's default.</value>
    public OffsetValue? SideOffset { get; init; }

    /// <summary>Gets how the toast is aligned relative to the side.</summary>
    /// <value>The alignment, or <see langword="null"/> for the positioner's default.</value>
    public Align? Align { get; init; }

    /// <summary>Gets the offset along the alignment axis.</summary>
    /// <value>The offset, or <see langword="null"/> for the positioner's default.</value>
    public OffsetValue? AlignOffset { get; init; }

    /// <summary>Gets the area the toast is kept inside.</summary>
    /// <value>The boundary, or <see langword="null"/> for the positioner's default.</value>
    public CollisionBoundaryValue? CollisionBoundary { get; init; }

    /// <summary>Gets the space kept between the toast and the boundary's edges.</summary>
    /// <value>The padding, or <see langword="null"/> for the positioner's default.</value>
    public CollisionPaddingValue? CollisionPadding { get; init; }

    /// <summary>Gets a value indicating whether the toast stays in view when the anchor scrolls out.</summary>
    /// <value>The value, or <see langword="null"/> for the positioner's default.</value>
    public bool? Sticky { get; init; }

    /// <summary>Gets the minimum distance between the arrow and the toast's edges.</summary>
    /// <value>The padding in CSS pixels, or <see langword="null"/> for the positioner's default.</value>
    public double? ArrowPadding { get; init; }

    /// <summary>Gets a value indicating whether the toast stops following layout shifts of the anchor.</summary>
    /// <value>The value, or <see langword="null"/> for the positioner's default.</value>
    public bool? DisableAnchorTracking { get; init; }

    /// <summary>Gets how the toast avoids collisions with the boundary.</summary>
    /// <value>The strategy, or <see langword="null"/> for the positioner's default.</value>
    public CollisionAvoidance? CollisionAvoidance { get; init; }

    // TST-PTR-22, TST-API-14: an anchored toast has no swipe directions.
    internal bool HasAnchor { get; private init; }
}

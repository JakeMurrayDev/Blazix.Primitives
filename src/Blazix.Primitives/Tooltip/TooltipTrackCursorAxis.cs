namespace Blazix.Primitives;

// TIP-API-11, TIP-POS-03; ADR 08 Amendments 2026-10-27 (chosen by the user 2026-10-27).
/// <summary>Specifies the axes along which a tooltip follows the pointer instead of its trigger.</summary>
public enum TooltipTrackCursorAxis
{
    /// <summary>The tooltip is anchored to its trigger: <c>none</c>.</summary>
    None,

    /// <summary>The tooltip follows the pointer horizontally: <c>x</c>.</summary>
    X,

    /// <summary>The tooltip follows the pointer vertically: <c>y</c>.</summary>
    Y,

    /// <summary>The tooltip follows the pointer on both axes and cannot be hovered: <c>both</c>.</summary>
    Both,
}

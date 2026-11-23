namespace Blazix.Primitives;

// ADR 08 §3 (Enums row: `None` is a Base UI literal, so the member exists) and Amendments 2026-10-27; Base UI's
// `SideFlipMode.side`, `SideFlipMode.align`, `SideShiftMode.side` and `SideShiftMode.align` (`'flip' | 'shift' | 'none'`,
// internals/useAnchorPositioning.ts:76-125). FLT-POS-03..05.
/// <summary>Specifies how a positioned popup avoids a collision with its boundary along one axis.</summary>
public enum CollisionAvoidanceMode
{
    /// <summary>
    /// When there is not enough space, the popup moves to the opposite side, or swaps start and end alignment on the
    /// alignment axis.
    /// </summary>
    Flip,

    /// <summary>The popup keeps its side or alignment and is shifted to fit within the boundary.</summary>
    Shift,

    /// <summary>The popup keeps its side or alignment even if it overflows.</summary>
    None,
}

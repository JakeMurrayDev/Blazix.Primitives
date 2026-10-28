namespace Blazix.Primitives;

// ADR 08 §3 (Enums row: `None` is a Base UI literal, so the member exists) and Amendments 2026-10-27; Base UI's
// `fallbackAxisSide` (`'start' | 'end' | 'none'`, internals/useAnchorPositioning.ts:76-125). FLT-POS-04.
/// <summary>
/// Specifies whether a positioned popup falls back to a side on the perpendicular axis when neither side on its preferred
/// axis fits, and which logical side it prefers there.
/// </summary>
public enum FallbackAxisSide
{
    /// <summary>The logical start side on the perpendicular axis is preferred.</summary>
    Start,

    /// <summary>The logical end side on the perpendicular axis is preferred.</summary>
    End,

    /// <summary>The popup does not fall back to the perpendicular axis.</summary>
    None,
}

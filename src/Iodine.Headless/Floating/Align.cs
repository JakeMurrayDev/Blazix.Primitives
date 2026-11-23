namespace Blazix.Primitives;

// ADR 08 §3 (Enums row) and Amendments 2026-10-27; Base UI's `Align` (internals/useAnchorPositioning.ts:67). The
// literal is FloatingLiterals.ToLiteral.
/// <summary>Specifies how a positioned popup is aligned along the side of its anchor that it is placed against.</summary>
public enum Align
{
    /// <summary>The popup's start edge is aligned with the anchor's start edge.</summary>
    Start,

    /// <summary>The popup is centered on the anchor.</summary>
    Center,

    /// <summary>The popup's end edge is aligned with the anchor's end edge.</summary>
    End,
}

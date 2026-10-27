namespace Blazix.Primitives;

// ADR 08 §3 (Enums row, the example `Side.InlineStart`) and Amendments 2026-10-27; Base UI's `Side`
// (internals/useAnchorPositioning.ts:66). The literal is FloatingLiterals.ToLiteral.
/// <summary>Specifies the side of its anchor that a positioned popup is placed against.</summary>
/// <remarks>
/// <see cref="InlineStart"/> and <see cref="InlineEnd"/> are logical sides: they resolve to <see cref="Left"/> or
/// <see cref="Right"/> by the reading direction of the nearest <see cref="DirectionProvider"/>.
/// </remarks>
public enum Side
{
    /// <summary>Above the anchor.</summary>
    Top,

    /// <summary>Below the anchor.</summary>
    Bottom,

    /// <summary>To the left of the anchor.</summary>
    Left,

    /// <summary>To the right of the anchor.</summary>
    Right,

    /// <summary>After the anchor in the reading direction: to its right in left-to-right text, to its left in right-to-left text.</summary>
    InlineEnd,

    /// <summary>Before the anchor in the reading direction: to its left in left-to-right text, to its right in right-to-left text.</summary>
    InlineStart,
}

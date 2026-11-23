namespace Blazix.Primitives;

// UTL §3.1 (`TextDirection = 'ltr' | 'rtl'`), UTL-I18N-02; ADR 08 §3 Enums row and its note of 2026-10-22: a shared enum
// beside its first consumer, DirectionProvider, because the composite root, positioning and Slider read it. The members
// are the PascalCase of the literals; there is no sentinel member. The composite module receives the literal
// (CompositeLiterals).
/// <summary>Specifies the reading direction of text.</summary>
public enum TextDirection
{
    /// <summary>Text reads from left to right.</summary>
    Ltr,

    /// <summary>Text reads from right to left.</summary>
    Rtl,
}

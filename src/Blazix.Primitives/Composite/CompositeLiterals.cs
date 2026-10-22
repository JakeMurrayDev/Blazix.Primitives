namespace Blazix.Primitives;

// The literals the composite module reads (Interop/CompositeOptions): CMP §6.1 orientation 'horizontal' | 'vertical' |
// 'both' with the mechanism default 'both' (ADR 08 Amendments 2026-10-21, the shared Orientation enum has no Both; a
// missing value is the default, CMP §10 Q11); UTL §3.1 TextDirection 'ltr' | 'rtl'.
/// <summary>Converts the enums a composite root reads into the literals its browser-side behavior understands.</summary>
internal static class CompositeLiterals
{
    /// <summary>Returns the literal of an orientation.</summary>
    /// <param name="orientation">The orientation, or <see langword="null"/> for both axes.</param>
    /// <returns><c>horizontal</c>, <c>vertical</c>, or <c>both</c> for <see langword="null"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="orientation"/> is not a defined <see cref="Orientation"/> value.</exception>
    public static string ToLiteral(Orientation? orientation)
    {
        return orientation switch
        {
            null => "both",
            Orientation.Horizontal => "horizontal",
            Orientation.Vertical => "vertical",
            // The value is formatted as an integer: passing the enum as the actual value would box it.
            _ => throw new ArgumentOutOfRangeException(nameof(orientation), $"Blazix: `{(int)orientation.Value}` is not a member of `Orientation`."),
        };
    }

    /// <summary>Returns the literal of a reading direction.</summary>
    /// <param name="direction">The direction.</param>
    /// <returns><c>ltr</c> or <c>rtl</c>.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="direction"/> is not a defined <see cref="TextDirection"/> value.</exception>
    public static string ToLiteral(TextDirection direction)
    {
        return direction switch
        {
            TextDirection.Ltr => "ltr",
            TextDirection.Rtl => "rtl",
            _ => throw new ArgumentOutOfRangeException(nameof(direction), $"Blazix: `{(int)direction}` is not a member of `TextDirection`."),
        };
    }
}

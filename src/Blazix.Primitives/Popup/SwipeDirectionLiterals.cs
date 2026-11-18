namespace Blazix.Primitives;

// Red stub (M6 track 1): the shape is present, its values are wrong; the next commit replaces it.
/// <summary>Converts between <see cref="SwipeDirection"/> and the literals the browser-side behaviors use.</summary>
internal static class SwipeDirectionLiterals
{
    /// <summary>Converts a member to its literal.</summary>
    /// <param name="direction">The direction.</param>
    /// <returns>The literal.</returns>
    public static string ToLiteral(SwipeDirection direction)
    {
        _ = direction;
        return "";
    }

    /// <summary>Converts a literal to its member.</summary>
    /// <param name="literal">The literal.</param>
    /// <returns>The member, or <see langword="null"/>.</returns>
    public static SwipeDirection? FromLiteral(string? literal)
    {
        _ = literal;
        return null;
    }
}

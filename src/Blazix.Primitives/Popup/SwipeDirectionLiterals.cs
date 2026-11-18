namespace Blazix.Primitives;

// ADR 08 Amendments 2026-11-18, the Swipe line: the one place SwipeDirection meets the `swipe` module's literals
// (js/swipe.ts's `SwipeDirection`), the InteractionTypeLiterals and FloatingLiterals shape; an out-of-range member throws,
// as EventReason.ToLiteral() does.
/// <summary>Converts between <see cref="SwipeDirection"/> and the literals the browser-side behaviors use.</summary>
internal static class SwipeDirectionLiterals
{
    /// <summary>Converts a member to its literal.</summary>
    /// <param name="direction">The direction.</param>
    /// <returns><c>up</c>, <c>down</c>, <c>left</c> or <c>right</c>.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="direction"/> is not a member of <see cref="SwipeDirection"/>.</exception>
    public static string ToLiteral(SwipeDirection direction) => direction switch
    {
        SwipeDirection.Up => "up",
        SwipeDirection.Down => "down",
        SwipeDirection.Left => "left",
        SwipeDirection.Right => "right",
        _ => throw new ArgumentOutOfRangeException(nameof(direction)),
    };

    /// <summary>Converts a reported literal to its member.</summary>
    /// <param name="literal"><c>up</c>, <c>down</c>, <c>left</c> or <c>right</c>.</param>
    /// <returns>The member; <see langword="null"/> for any other literal or <see langword="null"/>.</returns>
    public static SwipeDirection? FromLiteral(string? literal) => literal switch
    {
        "up" => SwipeDirection.Up,
        "down" => SwipeDirection.Down,
        "left" => SwipeDirection.Left,
        "right" => SwipeDirection.Right,
        _ => null,
    };
}

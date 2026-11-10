namespace Blazix.Primitives;

// The one keydown gate of a part that carries two keyboard behaviors on one element (the ToolbarButton rule): Enter and
// Space reach the button behavior, whose `activate` decision may have run the consumer's handler for them; every other key
// reaches the list or composite behavior, whose `navigate` decision may have.
/// <summary>Provides the key predicates that route a menu part's <c>keydown</c> to one of its behaviors.</summary>
internal static class MenuKeys
{
    /// <summary>Determines whether a key is one the button behavior activates on.</summary>
    /// <param name="key">The key, as <c>KeyboardEvent.key</c> names it.</param>
    /// <returns><see langword="true"/> for Enter and Space.</returns>
    public static bool IsButtonKey(string? key) => key is "Enter" or " ";

    /// <summary>Determines whether a key is a composite navigation key.</summary>
    /// <param name="key">The key, as <c>KeyboardEvent.key</c> names it.</param>
    /// <returns><see langword="true"/> for the arrow keys, Home and End.</returns>
    public static bool IsNavigationKey(string? key) =>
        key is not null && (key.StartsWith("Arrow", StringComparison.Ordinal) || key is "Home" or "End");
}

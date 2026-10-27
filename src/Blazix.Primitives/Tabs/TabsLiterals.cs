namespace Blazix.Primitives;

// TAB-DOM-02/07/18/22, TAB-STA-15; tabs/tab/TabsTab.tsx:215 (Tabs.Tab.ActivationDirection). The literal is what the part
// renders as data-activation-direction under the handover and what the `tabs` module reports (ADR 04 Amendments
// 2026-10-26: the attribute is JavaScript's from attach, C# mirrors it).
/// <summary>Converts the activation direction to and from the literal its data attribute and browser-side behavior use.</summary>
internal static class TabsLiterals
{
    /// <summary>Returns the literal of an activation direction.</summary>
    /// <param name="direction">The direction.</param>
    /// <returns><c>left</c>, <c>right</c>, <c>up</c>, <c>down</c> or <c>none</c>.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="direction"/> is not a defined <see cref="TabsActivationDirection"/> value.</exception>
    public static string ToLiteral(TabsActivationDirection direction)
    {
        return direction switch
        {
            TabsActivationDirection.Left => "left",
            TabsActivationDirection.Right => "right",
            TabsActivationDirection.Up => "up",
            TabsActivationDirection.Down => "down",
            TabsActivationDirection.None => "none",
            // The value is formatted as an integer: passing the enum as the actual value would box it.
            _ => throw new ArgumentOutOfRangeException(nameof(direction), $"Blazix: `{(int)direction}` is not a member of `TabsActivationDirection`."),
        };
    }

    /// <summary>Reads the literal the browser-side behavior reported.</summary>
    /// <param name="literal">The literal, or <see langword="null"/>.</param>
    /// <returns>The direction; <see cref="TabsActivationDirection.None"/> for a missing or unknown literal.</returns>
    public static TabsActivationDirection ParseDirection(string? literal)
    {
        return literal switch
        {
            "left" => TabsActivationDirection.Left,
            "right" => TabsActivationDirection.Right,
            "up" => TabsActivationDirection.Up,
            "down" => TabsActivationDirection.Down,
            _ => TabsActivationDirection.None,
        };
    }
}

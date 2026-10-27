namespace Blazix.Primitives;

// ADR 08 Amendments 2026-10-27: the one place the literals of the `button` module's interaction report (ButtonInteraction,
// UBT-ALG-06) and of the popup modules' reports become InteractionType. Interop/CONTEXT.md's deferral ("the shared enums
// arrive with their first part") ends here.
/// <summary>Converts between <see cref="InteractionType"/> and the literals the browser-side behaviors report.</summary>
internal static class InteractionTypeLiterals
{
    /// <summary>Converts a reported literal to its member.</summary>
    /// <param name="literal"><c>mouse</c>, <c>touch</c>, <c>pen</c>, <c>keyboard</c>, or the empty string.</param>
    /// <returns>The member; <see cref="InteractionType.Unknown"/> for the empty string, an unrecognized pointer type or <see langword="null"/>.</returns>
    public static InteractionType FromLiteral(string? literal) => literal switch
    {
        "mouse" => InteractionType.Mouse,
        "touch" => InteractionType.Touch,
        "pen" => InteractionType.Pen,
        "keyboard" => InteractionType.Keyboard,
        _ => InteractionType.Unknown,
    };

    /// <summary>Converts a member to its literal.</summary>
    /// <param name="type">The interaction type.</param>
    /// <returns>The literal; the empty string for <see cref="InteractionType.Unknown"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="type"/> is not a member of <see cref="InteractionType"/>.</exception>
    public static string ToLiteral(InteractionType type) => type switch
    {
        InteractionType.Mouse => "mouse",
        InteractionType.Touch => "touch",
        InteractionType.Pen => "pen",
        InteractionType.Keyboard => "keyboard",
        InteractionType.Unknown => "",
        _ => throw new ArgumentOutOfRangeException(nameof(type)),
    };
}

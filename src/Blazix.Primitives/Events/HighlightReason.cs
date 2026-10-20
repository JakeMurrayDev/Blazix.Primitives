namespace Blazix.Primitives;

// EVD-ALG-11
/// <summary>
/// Provides Base UI's <c>getHighlightReason</c>: the reason for a highlight change, derived from the event that caused it.
/// </summary>
internal static class HighlightReason
{
    /// <summary>
    /// Derives the reason for a highlight change from the event that caused it.
    /// </summary>
    /// <param name="event">The snapshot of the event, or <see langword="null"/> when there was none.</param>
    /// <returns>
    /// <see cref="EventReason.Keyboard"/> for <c>key*</c> event types, <see cref="EventReason.Pointer"/> for <c>mouse*</c> and
    /// <c>pointer*</c> types, and <see cref="EventReason.None"/> for any other type or when <paramref name="event"/> is <see langword="null"/>.
    /// </returns>
    public static EventReason From(EventSnapshot? @event)
    {
        if (@event is null)
        {
            return EventReason.None;
        }

        string type = @event.Type;
        if (type.StartsWith("key", StringComparison.Ordinal))
        {
            return EventReason.Keyboard;
        }

        if (type.StartsWith("mouse", StringComparison.Ordinal) || type.StartsWith("pointer", StringComparison.Ordinal))
        {
            return EventReason.Pointer;
        }

        return EventReason.None;
    }
}

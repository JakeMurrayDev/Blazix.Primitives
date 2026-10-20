namespace Blazix.Primitives;

/// <summary>Base UI's <c>getHighlightReason</c> (EVD-ALG-11): the reason of a highlight change from the event that caused it.</summary>
internal static class HighlightReason
{
    /// <summary><see cref="EventReason.None"/> without an event; <see cref="EventReason.Keyboard"/> for <c>key*</c> types; <see cref="EventReason.Pointer"/> for <c>mouse*</c> and <c>pointer*</c> types; otherwise <see cref="EventReason.None"/>.</summary>
    public static EventReason From(EventSnapshot? @event)
    {
        throw new NotImplementedException();
    }
}

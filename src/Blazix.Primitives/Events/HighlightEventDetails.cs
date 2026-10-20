namespace Blazix.Primitives;

/// <summary>
/// The details of <c>OnItemHighlighted</c> (EVD §3.3, §3.7): <see cref="GenericEventDetails"/> plus the highlighted
/// <see cref="Index"/>, <c>-1</c> when the highlight was cleared.
/// </summary>
public class HighlightEventDetails : GenericEventDetails
{
    /// <summary>Creates highlight details for <paramref name="index"/>.</summary>
    public HighlightEventDetails(EventReason reason, int index, EventSnapshot? @event = null)
        : base(reason, @event)
    {
        Index = index;
    }

    /// <summary>The index of the highlighted item; <c>-1</c> when the highlight is cleared.</summary>
    public int Index { get; }
}

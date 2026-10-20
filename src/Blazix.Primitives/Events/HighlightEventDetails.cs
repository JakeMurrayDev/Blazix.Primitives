namespace Blazix.Primitives;

// EVD §3.3, §3.7
/// <summary>
/// Represents the details of an <c>OnItemHighlighted</c> notification: the reason, the native event and the index of the
/// highlighted item.
/// </summary>
public class HighlightEventDetails : GenericEventDetails
{
    /// <summary>
    /// Initializes a new instance of the <see cref="HighlightEventDetails"/> class.
    /// </summary>
    /// <param name="reason">The reason for the highlight change.</param>
    /// <param name="index">The index of the highlighted item, or <c>-1</c> when the highlight was cleared.</param>
    /// <param name="event">The snapshot of the native event that caused the change, or <see langword="null"/> when there was none.</param>
    public HighlightEventDetails(EventReason reason, int index, EventSnapshot? @event = null)
        : base(reason, @event)
    {
        Index = index;
    }

    /// <summary>
    /// Gets the index of the highlighted item.
    /// </summary>
    /// <value>The zero-based index of the highlighted item, or <c>-1</c> when the highlight was cleared.</value>
    public int Index { get; }
}

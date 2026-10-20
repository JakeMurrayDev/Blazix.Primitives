namespace Blazix.Primitives;

// EVD §3.7
/// <summary>
/// Represents the details of a <c>SliderRoot.OnValueChange</c> request, which add the index of the thumb that was active
/// at the time of the change.
/// </summary>
public class SliderChangeEventDetails : ChangeEventDetails
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SliderChangeEventDetails"/> class.
    /// </summary>
    /// <param name="reason">The reason for the request.</param>
    /// <param name="activeThumbIndex">The zero-based index of the thumb that was active at the time of the change.</param>
    /// <param name="event">The snapshot of the native event that caused the request, or <see langword="null"/> when there was none.</param>
    /// <param name="trigger">The element that started the request, or <see langword="null"/> when no element did.</param>
    public SliderChangeEventDetails(EventReason reason, int activeThumbIndex, EventSnapshot? @event = null, EventTrigger? trigger = null)
        : base(reason, @event, trigger)
    {
        ActiveThumbIndex = activeThumbIndex;
    }

    /// <summary>
    /// Gets the index of the thumb that was active at the time of the change.
    /// </summary>
    /// <value>The zero-based index of the active thumb.</value>
    public int ActiveThumbIndex { get; }
}

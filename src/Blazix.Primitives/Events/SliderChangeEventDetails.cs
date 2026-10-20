namespace Blazix.Primitives;

/// <summary>The details of <c>SliderRoot.OnValueChange</c>: <see cref="ChangeEventDetails"/> plus <see cref="ActiveThumbIndex"/>, the zero-based index of the thumb that was active at the time of the change (EVD §3.7).</summary>
public class SliderChangeEventDetails : ChangeEventDetails
{
    /// <summary>Creates Slider details for the thumb that was active at the time of the change.</summary>
    public SliderChangeEventDetails(EventReason reason, int activeThumbIndex, EventSnapshot? @event = null, EventTrigger? trigger = null)
        : base(reason, @event, trigger)
    {
        ActiveThumbIndex = activeThumbIndex;
    }

    /// <summary>The index of the active thumb at the time of the change.</summary>
    public int ActiveThumbIndex { get; }
}

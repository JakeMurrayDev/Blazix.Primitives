namespace Blazix.Primitives;

// EVD §3.7 (`direction`), NUM-EVT-03; ADR 08 Amendments 2026-11-18 (chosen by the user 2026-11-18): `Direction` after
// SliderChangeEventDetails.ActiveThumbIndex, an int? that is -1 or 1, null for a change with no direction.
/// <summary>
/// Represents the details of a <c>NumberFieldRoot.OnValueChange</c> request, which add the direction of a step.
/// </summary>
public class NumberFieldChangeEventDetails : ChangeEventDetails
{
    /// <summary>Initializes a new instance of the <see cref="NumberFieldChangeEventDetails"/> class.</summary>
    /// <param name="reason">The reason for the request.</param>
    /// <param name="direction">The direction of a step, <c>1</c> up or <c>-1</c> down, or <see langword="null"/> for a change that is not a step.</param>
    /// <param name="event">The snapshot of the native event that caused the request, or <see langword="null"/> when there was none.</param>
    /// <param name="trigger">The element that started the request, or <see langword="null"/> when no element did.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="direction"/> is neither <see langword="null"/>, <c>1</c> nor <c>-1</c>.</exception>
    public NumberFieldChangeEventDetails(EventReason reason, int? direction = null, EventSnapshot? @event = null, EventTrigger? trigger = null)
        : base(reason, @event, trigger)
    {
        if (direction is not (null or 1 or -1))
        {
            throw new ArgumentOutOfRangeException(nameof(direction), "Blazix: `direction` must be `1`, `-1` or `null`.");
        }

        Direction = direction;
    }

    /// <summary>Gets the direction of a step.</summary>
    /// <value>
    /// <c>1</c> for a step up and <c>-1</c> for a step down, by a key, a stepper button, the wheel or a scrub; or
    /// <see langword="null"/> for typing, pasting, clearing, Home and End, and the first step of an empty field.
    /// </value>
    public int? Direction { get; }
}

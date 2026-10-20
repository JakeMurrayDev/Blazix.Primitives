namespace Blazix.Primitives;

// EVD §3.2, EVD-API-02
/// <summary>
/// The details of a notification that cannot be canceled: <see cref="Reason"/> and
/// <see cref="Event"/> only, no cancel, propagation or trigger. Used by <c>OnValueCommitted</c>, <c>OnValueInvalid</c>,
/// <c>OnValueComplete</c> and <c>OnFormSubmit</c>.
/// </summary>
public class GenericEventDetails
{
    // EVD-ALG-02
    /// <summary>Creates details carrying <paramref name="event"/> or, when it is <c>null</c>, a fresh placeholder event.</summary>
    public GenericEventDetails(EventReason reason, EventSnapshot? @event = null)
    {
        Reason = reason;
        Event = @event ?? EventSnapshot.CreatePlaceholder();
    }

    /// <summary>Why the notification was made.</summary>
    public EventReason Reason { get; }

    /// <summary>The native event that caused the notification, or a fresh <c>base-ui</c> placeholder.</summary>
    public EventSnapshot Event { get; }
}

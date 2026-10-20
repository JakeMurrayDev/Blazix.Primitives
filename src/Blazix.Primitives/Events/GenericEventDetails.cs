namespace Blazix.Primitives;

/// <summary>
/// The details of a notification that cannot be canceled (EVD §3.2, EVD-API-02): <see cref="Reason"/> and
/// <see cref="Event"/> only, no cancel, propagation or trigger. Used by <c>OnValueCommitted</c>, <c>OnValueInvalid</c>,
/// <c>OnValueComplete</c> and <c>OnFormSubmit</c>.
/// </summary>
public class GenericEventDetails
{
    /// <summary>Creates details as EVD-ALG-02 does: the placeholder event when <paramref name="event"/> is <c>null</c>.</summary>
    public GenericEventDetails(EventReason reason, EventSnapshot? @event = null)
    {
        throw new NotImplementedException();
    }

    /// <summary>Why the notification was made.</summary>
    public EventReason Reason { get; }

    /// <summary>The native event that caused the notification, or a fresh <c>base-ui</c> placeholder.</summary>
    public EventSnapshot Event { get; }
}

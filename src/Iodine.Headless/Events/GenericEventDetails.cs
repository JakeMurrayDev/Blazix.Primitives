namespace Blazix.Primitives;

// EVD §3.2, EVD-API-02
/// <summary>
/// Represents the details of a notification that cannot be canceled: its reason and the native event that caused it.
/// </summary>
/// <remarks>
/// These details have no cancel, propagation or trigger members. <c>OnValueCommitted</c>, <c>OnValueInvalid</c>,
/// <c>OnValueComplete</c> and <c>OnFormSubmit</c> use them.
/// </remarks>
public class GenericEventDetails
{
    // EVD-ALG-02
    /// <summary>
    /// Initializes a new instance of the <see cref="GenericEventDetails"/> class.
    /// </summary>
    /// <param name="reason">The reason for the notification.</param>
    /// <param name="event">The snapshot of the native event that caused the notification, or <see langword="null"/> when there was none.</param>
    /// <remarks>When <paramref name="event"/> is <see langword="null"/>, <see cref="Event"/> is a fresh placeholder whose <see cref="EventSnapshot.Type"/> is <c>base-ui</c>.</remarks>
    public GenericEventDetails(EventReason reason, EventSnapshot? @event = null)
    {
        Reason = reason;
        Event = @event ?? EventSnapshot.CreatePlaceholder();
    }

    /// <summary>
    /// Gets the reason for the notification.
    /// </summary>
    /// <value>The cause of the notification.</value>
    public EventReason Reason { get; }

    /// <summary>
    /// Gets the snapshot of the native event that caused the notification.
    /// </summary>
    /// <value>The native event's snapshot, or a fresh placeholder whose <see cref="EventSnapshot.Type"/> is <c>base-ui</c>.</value>
    public EventSnapshot Event { get; }
}

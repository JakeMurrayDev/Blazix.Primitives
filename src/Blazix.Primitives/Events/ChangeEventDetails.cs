namespace Blazix.Primitives;

// EVD §3.1, ADR 05 §3; glossary "Details"; EVD-EVT-04/09; EVD-API-06
/// <summary>
/// Represents the details of a cancelable request: why it was made, which native event caused it, which element started
/// it, and whether it was canceled.
/// </summary>
/// <remarks>
/// One mutable object exists per request and is passed unchanged along a child-then-parent chain of callbacks.
/// <see cref="IsCanceled"/> and <see cref="IsPropagationAllowed"/> are live: they read the current flag, never a copy.
/// </remarks>
public class ChangeEventDetails
{
    private bool canceled;
    private bool propagationAllowed;

    // EVD-ALG-01
    /// <summary>
    /// Initializes a new instance of the <see cref="ChangeEventDetails"/> class that is not canceled and does not allow propagation.
    /// </summary>
    /// <param name="reason">The reason for the request.</param>
    /// <param name="event">The snapshot of the native event that caused the request, or <see langword="null"/> when there was none.</param>
    /// <param name="trigger">The element that started the request, or <see langword="null"/> when no element did.</param>
    /// <remarks>When <paramref name="event"/> is <see langword="null"/>, <see cref="Event"/> is a fresh placeholder whose <see cref="EventSnapshot.Type"/> is <c>base-ui</c>.</remarks>
    public ChangeEventDetails(EventReason reason, EventSnapshot? @event = null, EventTrigger? trigger = null)
    {
        Reason = reason;
        Event = @event ?? EventSnapshot.CreatePlaceholder();
        Trigger = trigger;
    }

    /// <summary>
    /// Gets the reason for the request.
    /// </summary>
    /// <value>The cause of the request.</value>
    public EventReason Reason { get; }

    // EVD-API-04/05
    /// <summary>
    /// Gets the snapshot of the native event that caused the request.
    /// </summary>
    /// <value>The native event's snapshot, or a fresh placeholder whose <see cref="EventSnapshot.Type"/> is <c>base-ui</c> when the request had no native event.</value>
    public EventSnapshot Event { get; internal set; }

    // EVD-API-07
    /// <summary>
    /// Gets the element that started the request.
    /// </summary>
    /// <value>The trigger, or <see langword="null"/> when no element started the request.</value>
    public EventTrigger? Trigger { get; internal set; }

    /// <summary>
    /// Gets a value that indicates whether <see cref="Cancel"/> has been called on this object.
    /// </summary>
    /// <value><see langword="true"/> after <see cref="Cancel"/> has been called; otherwise, <see langword="false"/>.</value>
    public bool IsCanceled => canceled;

    /// <summary>
    /// Gets a value that indicates whether <see cref="AllowPropagation"/> has been called on this object.
    /// </summary>
    /// <value><see langword="true"/> after <see cref="AllowPropagation"/> has been called; otherwise, <see langword="false"/>.</value>
    public bool IsPropagationAllowed => propagationAllowed;

    // EVD-EVT-05..09
    /// <summary>
    /// Cancels the request.
    /// </summary>
    /// <remarks>An uncontrolled part does not commit a canceled request, and the effects that follow a commit do not run.</remarks>
    public void Cancel() => canceled = true;

    // EVD §4.4
    /// <summary>
    /// Lets the native event keep propagating where the part would otherwise stop it.
    /// </summary>
    /// <remarks>Sets <see cref="IsPropagationAllowed"/>. The case it matters for is Escape on a nested popup.</remarks>
    public void AllowPropagation() => propagationAllowed = true;
}

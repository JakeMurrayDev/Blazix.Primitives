namespace Blazix.Primitives;

// EVD §3.1, ADR 05 §3; glossary "Details"; EVD-EVT-04/09; EVD-API-06
/// <summary>
/// The details of a cancelable request: one mutable object per request, passed
/// unchanged along a child-then-parent chain of callbacks. <see cref="IsCanceled"/> and
/// <see cref="IsPropagationAllowed"/> are live: they read the current flag, never a copy.
/// </summary>
public class ChangeEventDetails
{
    private bool canceled;
    private bool propagationAllowed;

    // EVD-ALG-01
    /// <summary>Creates details that are not canceled and do not allow propagation, carrying <paramref name="event"/> or, when it is <c>null</c>, a fresh placeholder event.</summary>
    public ChangeEventDetails(EventReason reason, EventSnapshot? @event = null, EventTrigger? trigger = null)
    {
        Reason = reason;
        Event = @event ?? EventSnapshot.CreatePlaceholder();
        Trigger = trigger;
    }

    /// <summary>Why the request was made.</summary>
    public EventReason Reason { get; }

    // EVD-API-04/05
    /// <summary>The native event that caused the request, or a fresh <c>base-ui</c> placeholder.</summary>
    public EventSnapshot Event { get; internal set; }

    // EVD-API-07
    /// <summary>The element that started the request, or <c>null</c>.</summary>
    public EventTrigger? Trigger { get; internal set; }

    /// <summary>Whether <see cref="Cancel"/> has been called on this object.</summary>
    public bool IsCanceled => canceled;

    /// <summary>Whether <see cref="AllowPropagation"/> has been called on this object.</summary>
    public bool IsPropagationAllowed => propagationAllowed;

    // EVD-EVT-05..09
    /// <summary>Cancels the request: an uncontrolled component does not commit and the follow-up effects do not run.</summary>
    public void Cancel() => canceled = true;

    // EVD §4.4
    /// <summary>Sets <see cref="IsPropagationAllowed"/>: the part then does not stop the native event's propagation where it otherwise would (Escape on a nested popup).</summary>
    public void AllowPropagation() => propagationAllowed = true;
}

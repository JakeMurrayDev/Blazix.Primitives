namespace Blazix.Primitives;

/// <summary>
/// The details of a cancelable request (EVD §3.1, ADR 05 §3; glossary "Details"): one mutable object per request, passed
/// unchanged along a child-then-parent chain of callbacks (EVD-EVT-04/09). <see cref="IsCanceled"/> and
/// <see cref="IsPropagationAllowed"/> are live: they read the current flag, never a copy (EVD-API-06).
/// </summary>
public class ChangeEventDetails
{
    private bool canceled;
    private bool propagationAllowed;

    /// <summary>Creates details as EVD-ALG-01 does: not canceled, propagation not allowed, the placeholder event when <paramref name="event"/> is <c>null</c>.</summary>
    public ChangeEventDetails(EventReason reason, EventSnapshot? @event = null, EventTrigger? trigger = null)
    {
        Reason = reason;
        Event = @event ?? EventSnapshot.CreatePlaceholder();
        Trigger = trigger;
    }

    /// <summary>Why the request was made.</summary>
    public EventReason Reason { get; }

    /// <summary>The native event that caused the request, or a fresh <c>base-ui</c> placeholder (EVD-API-04/05).</summary>
    public EventSnapshot Event { get; internal set; }

    /// <summary>The element that started the request, or <c>null</c> (EVD-API-07).</summary>
    public EventTrigger? Trigger { get; internal set; }

    /// <summary>Whether <see cref="Cancel"/> has been called on this object.</summary>
    public bool IsCanceled => canceled;

    /// <summary>Whether <see cref="AllowPropagation"/> has been called on this object.</summary>
    public bool IsPropagationAllowed => propagationAllowed;

    /// <summary>Cancels the request: an uncontrolled component does not commit and the follow-up effects do not run (EVD-EVT-05..09).</summary>
    public void Cancel() => canceled = true;

    /// <summary>Lets the native event keep propagating where the part would stop it (EVD §4.4).</summary>
    public void AllowPropagation() => propagationAllowed = true;
}

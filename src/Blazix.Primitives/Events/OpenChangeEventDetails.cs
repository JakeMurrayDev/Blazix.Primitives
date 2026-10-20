namespace Blazix.Primitives;

// EVD-API-09, ADR 05 §3; EVD-EVT-21; EVD-EDGE-07
/// <summary>
/// Represents the details of a popup's open-change request, which can also keep a closing popup mounted.
/// </summary>
/// <remarks>
/// The popup reads whether <see cref="PreventUnmountOnClose"/> was called after the callback returns, and honours it only
/// for a close that was not canceled. On parts whose details type is shared with a popup root but that never unmount
/// (menu checkbox item, menu radio group), the call has no effect.
/// </remarks>
public class OpenChangeEventDetails : ChangeEventDetails
{
    private bool unmountOnClosePrevented;

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenChangeEventDetails"/> class that is not canceled and does not allow propagation.
    /// </summary>
    /// <param name="reason">The reason for the request.</param>
    /// <param name="event">The snapshot of the native event that caused the request, or <see langword="null"/> when there was none.</param>
    /// <param name="trigger">The element that started the request, or <see langword="null"/> when no element did.</param>
    public OpenChangeEventDetails(EventReason reason, EventSnapshot? @event = null, EventTrigger? trigger = null)
        : base(reason, @event, trigger)
    {
    }

    /// <summary>
    /// Gets a value that indicates whether <see cref="PreventUnmountOnClose"/> has been called on this object.
    /// </summary>
    /// <value><see langword="true"/> after <see cref="PreventUnmountOnClose"/> has been called; otherwise, <see langword="false"/>.</value>
    internal bool UnmountOnClosePrevented => unmountOnClosePrevented;

    /// <summary>
    /// Keeps a closing popup mounted, in its closing phase, until the root's <c>unmount</c> action is called.
    /// </summary>
    public void PreventUnmountOnClose() => unmountOnClosePrevented = true;
}

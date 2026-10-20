namespace Blazix.Primitives;

/// <summary>
/// The details of a popup's open-change request (EVD-API-09, ADR 05 §3): <see cref="ChangeEventDetails"/> plus
/// <see cref="PreventUnmountOnClose"/>. The popup reads <see cref="UnmountOnClosePrevented"/> after the callback, and only
/// for a close that was not canceled (EVD-EVT-21). On parts whose type is shared with a popup root but that never unmount
/// (menu checkbox item, menu radio group) the call has no effect (EVD-EDGE-07).
/// </summary>
public class OpenChangeEventDetails : ChangeEventDetails
{
    /// <inheritdoc cref="ChangeEventDetails(EventReason, EventSnapshot?, EventTrigger?)"/>
    public OpenChangeEventDetails(EventReason reason, EventSnapshot? @event = null, EventTrigger? trigger = null)
        : base(reason, @event, trigger)
    {
    }

    /// <summary>Whether <see cref="PreventUnmountOnClose"/> has been called on this object.</summary>
    internal bool UnmountOnClosePrevented => throw new NotImplementedException();

    /// <summary>Keeps a closing popup mounted, in its closing phase, until the root's <c>unmount</c> action is called.</summary>
    public void PreventUnmountOnClose()
    {
        throw new NotImplementedException();
    }
}

namespace Blazix.Primitives;

// EVD §3.7
/// <summary>
/// Represents the details of a <c>TabsRoot.OnValueChange</c> request, which add the direction from the previously active
/// tab to the new one.
/// </summary>
public class TabsChangeEventDetails : ChangeEventDetails
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TabsChangeEventDetails"/> class.
    /// </summary>
    /// <param name="reason">The reason for the request.</param>
    /// <param name="activationDirection">The direction from the previously active tab to the new one.</param>
    /// <param name="event">The snapshot of the native event that caused the request, or <see langword="null"/> when there was none.</param>
    /// <param name="trigger">The element that started the request, or <see langword="null"/> when no element did.</param>
    public TabsChangeEventDetails(EventReason reason, TabsActivationDirection activationDirection, EventSnapshot? @event = null, EventTrigger? trigger = null)
        : base(reason, @event, trigger)
    {
        ActivationDirection = activationDirection;
    }

    /// <summary>
    /// Gets the direction from the previously active tab to the new one.
    /// </summary>
    /// <value>The activation direction; <see cref="TabsActivationDirection.None"/> for automatic changes.</value>
    public TabsActivationDirection ActivationDirection { get; internal set; }
}

// ADR 08 §3
/// <summary>
/// Specifies the direction from the previously active tab to the new one (Base UI's <c>'left' | 'right' | 'up' | 'down' | 'none'</c>).
/// </summary>
public enum TabsActivationDirection
{
    /// <summary>The new tab is to the left of the previous one (<c>left</c>).</summary>
    Left,

    /// <summary>The new tab is to the right of the previous one (<c>right</c>).</summary>
    Right,

    /// <summary>The new tab is above the previous one (<c>up</c>).</summary>
    Up,

    /// <summary>The new tab is below the previous one (<c>down</c>).</summary>
    Down,

    /// <summary>The change was automatic, so it has no direction (<c>none</c>).</summary>
    None,
}

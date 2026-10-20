namespace Blazix.Primitives;

// EVD §3.7
/// <summary>The details of <c>TabsRoot.OnValueChange</c>: <see cref="ChangeEventDetails"/> plus <see cref="ActivationDirection"/>, the direction from the previously active tab to the new one.</summary>
public class TabsChangeEventDetails : ChangeEventDetails
{
    /// <summary>Creates Tabs details with the direction from the previous tab to the new one.</summary>
    public TabsChangeEventDetails(EventReason reason, TabsActivationDirection activationDirection, EventSnapshot? @event = null, EventTrigger? trigger = null)
        : base(reason, @event, trigger)
    {
        ActivationDirection = activationDirection;
    }

    /// <summary>The direction from the previously active tab to the new one; <see cref="TabsActivationDirection.None"/> for automatic changes.</summary>
    public TabsActivationDirection ActivationDirection { get; internal set; }
}

// ADR 08 §3
/// <summary>Base UI's <c>'left' | 'right' | 'up' | 'down' | 'none'</c> activation direction of Tabs: the direction from the previously active tab to the new one.</summary>
public enum TabsActivationDirection
{
    /// <summary><c>left</c>.</summary>
    Left,

    /// <summary><c>right</c>.</summary>
    Right,

    /// <summary><c>up</c>.</summary>
    Up,

    /// <summary><c>down</c>.</summary>
    Down,

    /// <summary><c>none</c>: an automatic change.</summary>
    None,
}

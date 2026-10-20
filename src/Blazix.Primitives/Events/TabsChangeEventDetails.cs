namespace Blazix.Primitives;

/// <summary>The details of <c>TabsRoot.OnValueChange</c> (EVD §3.7): <see cref="ChangeEventDetails"/> plus <see cref="ActivationDirection"/>.</summary>
public class TabsChangeEventDetails : ChangeEventDetails
{
    /// <summary>Creates Tabs details with the direction from the previous tab to the new one.</summary>
    public TabsChangeEventDetails(EventReason reason, TabsActivationDirection activationDirection, EventSnapshot? @event = null, EventTrigger? trigger = null)
        : base(reason, @event, trigger)
    {
        throw new NotImplementedException();
    }

    /// <summary>The direction from the previously active tab to the new one; <see cref="TabsActivationDirection.None"/> for automatic changes.</summary>
    public TabsActivationDirection ActivationDirection { get; internal set; }
}

/// <summary>Base UI's <c>'left' | 'right' | 'up' | 'down' | 'none'</c> activation direction of Tabs (ADR 08 §3: one family's literal set).</summary>
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

using Microsoft.AspNetCore.Components;

namespace Blazix.Primitives;

// ADR 04 Amendments 2026-10-27 (chosen by the user 2026-10-27): the Arrow publishes its id through the positioner's
// context, and its mirror arrives in the Positioner's report. Cascaded fixed by the family's Positioner part (ADR 16 rule
// 9, BLZX2001); a change of the mirror reaches the Arrow through the listeners, never through the cascade. One Arrow per
// Positioner, the last registered winning (Base UI's single arrowRef).
/// <summary>The state a positioner shares with the arrow rendered inside it.</summary>
internal sealed class PositionerContext
{
    private readonly PositionerBehavior owner;
    private readonly List<Action> listeners = [];

    /// <summary>Initializes a new instance of the <see cref="PositionerContext"/> class.</summary>
    /// <param name="owner">The positioner behavior that owns the context.</param>
    internal PositionerContext(PositionerBehavior owner)
    {
        this.owner = owner;
    }

    /// <summary>Gets the placement the positioner's browser-side behavior last rendered.</summary>
    /// <value>The mirrored placement.</value>
    public PositionerMirror Mirror => owner.Mirror;

    /// <summary>Gets the arrow registered with the positioner.</summary>
    /// <value>The arrow's behavior, or <see langword="null"/> while no arrow is rendered.</value>
    internal PositionerArrowBehavior? Arrow { get; private set; }

    /// <summary>Adds a listener that runs after every change of the arrow's mirrored values.</summary>
    /// <param name="listener">The listener.</param>
    public void AddListener(Action listener) => listeners.Add(listener);

    /// <summary>Removes a listener added with <see cref="AddListener"/>.</summary>
    /// <param name="listener">The listener.</param>
    public void RemoveListener(Action listener) => listeners.Remove(listener);

    /// <summary>Registers the arrow rendered inside the positioner.</summary>
    /// <param name="arrow">The arrow's behavior.</param>
    internal void Register(PositionerArrowBehavior arrow)
    {
        Arrow = arrow;
        owner.MarkDirty();
    }

    /// <summary>Records that the arrow's id or style changed, so the positioner's next flush sends them.</summary>
    internal void ArrowChanged() => owner.MarkDirty();

    /// <summary>Removes the arrow, so the positioner's next update tells the browser-side behavior it is gone.</summary>
    /// <param name="arrow">The arrow's behavior.</param>
    internal void Unregister(PositionerArrowBehavior arrow)
    {
        if (ReferenceEquals(Arrow, arrow))
        {
            Arrow = null;
            owner.ScheduleFlush();
        }
    }

    /// <summary>Sends the positioner's options if the arrow's id or style changed them.</summary>
    /// <returns>A task that completes when the browser-side behavior has the options.</returns>
    internal Task FlushAsync() => owner.FlushAsync();

    /// <summary>Notifies the listeners that the arrow's mirrored values changed.</summary>
    internal void NotifyArrowChanged()
    {
        // A listener may remove itself or another: iterating backwards with a bounds check never skips one still there.
        for (int i = listeners.Count - 1; i >= 0; i--)
        {
            if (i < listeners.Count)
            {
                listeners[i]();
            }
        }
    }

    /// <summary>Gets the fragment that cascades this context to <paramref name="content"/>.</summary>
    /// <param name="content">The content that receives the context.</param>
    /// <returns>The fragment.</returns>
    internal RenderFragment Cascade(RenderFragment? content) => builder =>
    {
        builder.OpenComponent<CascadingValue<PositionerContext>>(0);
        builder.AddComponentParameter(1, nameof(CascadingValue<PositionerContext>.Value), this);
        builder.AddComponentParameter(2, nameof(CascadingValue<PositionerContext>.IsFixed), true);
        builder.AddComponentParameter(3, nameof(CascadingValue<PositionerContext>.ChildContent), content);
        builder.CloseComponent();
    };
}

using Microsoft.AspNetCore.Components;

namespace Blazix.Primitives;

// SWT-STR-02, SWT-STA-08, SWT-DOM-06, SWT-EDGE-01; switch/root/SwitchRootContext.ts. ADR 05 §1 keeps Switch on plain fields
// (a store was rejected for it), so the context is a listener list, cascaded fixed by SwitchRoot (ADR 16 rule 9; the
// Meter and Avatar precedent): a change reaches the thumbs through their listeners, never through the cascade, and the
// root notifies them before its own render, so every element lands in one render batch.
/// <summary>The state a switch root shares with its thumbs.</summary>
internal sealed class SwitchRootContext
{
    private readonly List<Action> listeners = [];

    /// <summary>Gets the thumbs' state, the root's state under the thumb's type.</summary>
    /// <value>The state of the last <see cref="SetState"/>.</value>
    public SwitchThumbState ThumbState { get; private set; } = new(false, false, false, false, false, false, null, false, false);

    /// <summary>Throws the error of a part rendered outside a switch root.</summary>
    /// <param name="context">The cascaded context, or <see langword="null"/>.</param>
    /// <returns>The context.</returns>
    /// <exception cref="InvalidOperationException"><paramref name="context"/> is <see langword="null"/>.</exception>
    public static SwitchRootContext Require(SwitchRootContext? context)
    {
        // SWT-EDGE-01, in every build; the port's words (2026-10-22 port note).
        return context ?? throw new InvalidOperationException(
            "Blazix: The `SwitchRoot` context is missing. Switch parts must be placed within `<SwitchRoot>`.");
    }

    /// <summary>Takes the root's new state and notifies the thumbs.</summary>
    /// <param name="state">The root's state; the caller passes a new instance only when a field changed.</param>
    public void SetState(SwitchRootState state)
    {
        ThumbState = new SwitchThumbState(state.Checked, state.Disabled, state.ReadOnly, state.Required, state.Touched, state.Dirty, state.Valid, state.Filled, state.Focused);
        // A listener may remove itself or another (a thumb disposed by the render it triggers): iterating backwards with a
        // bounds check never skips a listener that is still registered.
        for (int i = listeners.Count - 1; i >= 0; i--)
        {
            if (i < listeners.Count)
            {
                listeners[i]();
            }
        }
    }

    /// <summary>Adds a listener that runs after every change of <see cref="ThumbState"/>.</summary>
    /// <param name="listener">The listener.</param>
    public void AddListener(Action listener) => listeners.Add(listener);

    /// <summary>Removes a listener added with <see cref="AddListener"/>.</summary>
    /// <param name="listener">The listener.</param>
    public void RemoveListener(Action listener) => listeners.Remove(listener);

    /// <summary>Gets the fragment that cascades this context to <paramref name="content"/>.</summary>
    /// <param name="content">The content that receives the context.</param>
    /// <returns>The fragment.</returns>
    internal RenderFragment Cascade(RenderFragment? content) => builder =>
    {
        builder.OpenComponent<CascadingValue<SwitchRootContext>>(0);
        builder.AddComponentParameter(1, nameof(CascadingValue<SwitchRootContext>.Value), this);
        builder.AddComponentParameter(2, nameof(CascadingValue<SwitchRootContext>.IsFixed), true);
        builder.AddComponentParameter(3, nameof(CascadingValue<SwitchRootContext>.ChildContent), content);
        builder.CloseComponent();
    };
}

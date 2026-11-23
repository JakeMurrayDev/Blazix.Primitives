using Microsoft.AspNetCore.Components;

namespace Blazix.Primitives;

// CHK-STR-02, CHK-STA-02, CHK-DOM-08, CHK-EDGE-01; checkbox/root/CheckboxRootContext.ts. The Switch pattern (ADR 05 §1 keeps
// the family on plain fields): a listener list, cascaded fixed by CheckboxRoot (ADR 16 rule 9). A change reaches the
// indicators through their listeners, never through the cascade, and the root notifies them before its own render, so
// every element lands in one render batch.
/// <summary>The state a checkbox root shares with its indicators.</summary>
internal sealed class CheckboxRootContext
{
    private readonly List<Action> listeners = [];

    /// <summary>Gets the root's state.</summary>
    /// <value>The state of the last <see cref="SetState"/>.</value>
    public CheckboxRootState State { get; private set; } = new(false, false, false, false, false, false, false, null, false, false);

    /// <summary>Throws the error of a part rendered outside a checkbox root.</summary>
    /// <param name="context">The cascaded context, or <see langword="null"/>.</param>
    /// <returns>The context.</returns>
    /// <exception cref="InvalidOperationException"><paramref name="context"/> is <see langword="null"/>.</exception>
    public static CheckboxRootContext Require(CheckboxRootContext? context)
    {
        // CHK-EDGE-01, in every build; the port's words (2026-10-23 port note).
        return context ?? throw new InvalidOperationException(
            "Blazix: The `CheckboxRoot` context is missing. Checkbox parts must be placed within `<CheckboxRoot>`.");
    }

    /// <summary>Takes the root's new state and notifies the indicators.</summary>
    /// <param name="state">The root's state; the caller passes a new instance only when a field changed.</param>
    public void SetState(CheckboxRootState state)
    {
        State = state;
        // A listener may remove itself or another (an indicator disposed by the render it triggers): iterating backwards
        // with a bounds check never skips a listener that is still registered.
        for (int i = listeners.Count - 1; i >= 0; i--)
        {
            if (i < listeners.Count)
            {
                listeners[i]();
            }
        }
    }

    /// <summary>Adds a listener that runs after every change of <see cref="State"/>.</summary>
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
        builder.OpenComponent<CascadingValue<CheckboxRootContext>>(0);
        builder.AddComponentParameter(1, nameof(CascadingValue<CheckboxRootContext>.Value), this);
        builder.AddComponentParameter(2, nameof(CascadingValue<CheckboxRootContext>.IsFixed), true);
        builder.AddComponentParameter(3, nameof(CascadingValue<CheckboxRootContext>.ChildContent), content);
        builder.CloseComponent();
    };
}

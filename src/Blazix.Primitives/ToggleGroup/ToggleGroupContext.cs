using Microsoft.AspNetCore.Components;

namespace Blazix.Primitives;

// TGG §11 (the ToggleGroupContext a Toggle reads); toggle-group/ToggleGroupContext.ts:6-19. Design point 11.1 of M3 tracks
// 2-3, option (a), pending the user: a plain listener list cascaded fixed (ADR 16 rule 9; the Switch, Checkbox,
// CheckboxGroup and RadioGroup pattern). A change of the value (by reference, ADR 08 Amendments 2026-10-22), the disabled
// state or the initialized flag notifies every toggle, which compares what it renders and renders only when that changed;
// the group never renders for a selection (CheckboxGroup's G1).
/// <summary>The state a toggle group shares with the toggles in its subtree.</summary>
internal sealed class ToggleGroupContext
{
    private readonly List<Action> listeners = [];
    private readonly Func<string, bool, ChangeEventDetails, Task> request;

    /// <summary>Initializes a new instance of the <see cref="ToggleGroupContext"/> class.</summary>
    /// <param name="value">The group's value at its first render.</param>
    /// <param name="request">Runs the group's half of a toggle's request: its change callback, then its commit.</param>
    public ToggleGroupContext(IReadOnlyList<string> value, Func<string, bool, ChangeEventDetails, Task> request)
    {
        Value = value;
        this.request = request;
    }

    /// <summary>Gets the group's value.</summary>
    /// <value>The identities of the pressed toggles.</value>
    public IReadOnlyList<string> Value { get; private set; }

    /// <summary>Gets a value indicating whether every toggle of the group is disabled.</summary>
    /// <value>The group's effective disabled state.</value>
    public bool Disabled { get; private set; }

    // TGG-STA-07: gates TGL-EDGE-01 and nothing else.
    /// <summary>Gets a value indicating whether the consumer supplied the group's value or its default.</summary>
    /// <value><see langword="true"/> when <c>Value</c> or <c>DefaultValue</c> is supplied and not <see langword="null"/> in the current parameter set.</value>
    public bool IsValueInitialized { get; private set; }

    // TGL-STA-04: membership by ordinal string equality (`indexOf`), against the list as given (TGG-API-01: never copied).
    /// <summary>Determines whether the group's value holds an identity.</summary>
    /// <param name="identity">The toggle's identity.</param>
    /// <returns><see langword="true"/> when the value holds it.</returns>
    public bool Contains(string identity)
    {
        IReadOnlyList<string> value = Value;
        for (int i = 0; i < value.Count; i++)
        {
            if (string.Equals(value[i], identity, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    // TGG-EVT-01..05, TGL-EVT-05: the toggle's details instance reaches the group's callback unchanged.
    /// <summary>Runs the group's half of a toggle's request.</summary>
    /// <param name="identity">The toggle's identity.</param>
    /// <param name="nextPressed">The pressed state the toggle asks for.</param>
    /// <param name="details">The toggle's change details.</param>
    /// <returns>A task that completes when the group has notified its consumer and committed, unless canceled.</returns>
    public Task RequestAsync(string identity, bool nextPressed, ChangeEventDetails details) => request(identity, nextPressed, details);

    /// <summary>Takes the group's state and notifies the toggles when it changed.</summary>
    /// <param name="value">The value.</param>
    /// <param name="disabled">The group's effective disabled state.</param>
    /// <param name="isValueInitialized">Whether the consumer supplied the value or its default.</param>
    public void SetState(IReadOnlyList<string> value, bool disabled, bool isValueInitialized)
    {
        if (ReferenceEquals(value, Value) && disabled == Disabled && isValueInitialized == IsValueInitialized)
        {
            return;
        }

        Value = value;
        Disabled = disabled;
        IsValueInitialized = isValueInitialized;
        Notify();
    }

    /// <summary>Adds a listener that runs after every change of the group's state.</summary>
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
        builder.OpenComponent<CascadingValue<ToggleGroupContext>>(0);
        builder.AddComponentParameter(1, nameof(CascadingValue<ToggleGroupContext>.Value), this);
        builder.AddComponentParameter(2, nameof(CascadingValue<ToggleGroupContext>.IsFixed), true);
        builder.AddComponentParameter(3, nameof(CascadingValue<ToggleGroupContext>.ChildContent), content);
        builder.CloseComponent();
    };

    private void Notify()
    {
        // A listener may remove itself or another (a toggle disposed by the render it triggers): iterating backwards with a
        // bounds check never skips a listener that is still registered.
        for (int i = listeners.Count - 1; i >= 0; i--)
        {
            if (i < listeners.Count)
            {
                listeners[i]();
            }
        }
    }
}

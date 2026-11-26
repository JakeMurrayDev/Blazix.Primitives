using Microsoft.AspNetCore.Components;

namespace Iodine.Headless;

// NUM-STR-02, NUM-EDGE-01; number-field/root/NumberFieldRootContext.ts. The context the root cascades fixed (ADR 16 rule 9; the
// Switch precedent): the parts read the root's state and call its requests through it, and re-render from its listeners, so
// a change reaches every part in the root's render batch. The requests are the root's (NumberFieldRoot.*Async), the store of
// NUM §14 (the sync flag, the last changed value, the pending commit) is the root's fields.
/// <summary>The state and the requests a number field root shares with its parts.</summary>
internal sealed class NumberFieldRootContext
{
    private readonly List<Action> listeners = [];
    private readonly List<Action> inputListeners = [];

    /// <summary>Initializes a new instance of the <see cref="NumberFieldRootContext"/> class.</summary>
    /// <param name="root">The root.</param>
    public NumberFieldRootContext(NumberFieldRoot root)
    {
        Root = root;
    }

    /// <summary>Gets the root, whose requests and parameters the parts read.</summary>
    public NumberFieldRoot Root { get; }

    /// <summary>Gets the root's state, a new instance only when a field changed.</summary>
    public NumberFieldRootState State { get; private set; } = new(null, string.Empty, false, false, false, false, false, false, null, false, false);

    /// <summary>Gets a counter that grows with every new <see cref="State"/>, so a part rebuilds its own record only then.</summary>
    public int StateVersion { get; private set; }

    // By id, not Nullable.Equals: ElementReference is not IEquatable, so its equality boxes on every input render.
    /// <summary>Gets a value indicating whether two element references name the same element.</summary>
    /// <param name="first">The first reference, or <see langword="null"/>.</param>
    /// <param name="second">The second reference, or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when both are <see langword="null"/> or both have the same id.</returns>
    public static bool SameElement(ElementReference? first, ElementReference? second) =>
        string.Equals(first?.Id, second?.Id, StringComparison.Ordinal);

    /// <summary>Throws the error of a part rendered outside a number field root.</summary>
    /// <param name="context">The cascaded context, or <see langword="null"/>.</param>
    /// <returns>The context.</returns>
    /// <exception cref="InvalidOperationException"><paramref name="context"/> is <see langword="null"/>.</exception>
    public static NumberFieldRootContext Require(NumberFieldRootContext? context)
    {
        // NUM-EDGE-01, in every build; Base UI's words with the `Iodine: ` prefix (the AlertDialog and Select precedent).
        return context ?? throw new InvalidOperationException(
            "Iodine: `NumberFieldRootContext` is missing. NumberField parts must be placed within `<NumberFieldRoot>`.");
    }

    /// <summary>Takes the root's new state and notifies the parts.</summary>
    /// <param name="state">The root's state; the caller passes a new instance only when a field changed.</param>
    public void SetState(NumberFieldRootState state)
    {
        State = state;
        StateVersion++;
        Notify();
    }

    /// <summary>Notifies the parts without a state change, as when the root's parameters that a part renders changed.</summary>
    public void Notify()
    {
        // A listener may remove itself or another (a part disposed by the render it triggers): iterating backwards with a
        // bounds check never skips a listener that is still registered.
        for (int i = listeners.Count - 1; i >= 0; i--)
        {
            if (i < listeners.Count)
            {
                listeners[i]();
            }
        }
    }

    /// <summary>Adds a listener that runs after every change the parts render.</summary>
    /// <param name="listener">The listener.</param>
    public void AddListener(Action listener) => listeners.Add(listener);

    /// <summary>Removes a listener added with <see cref="AddListener"/>.</summary>
    /// <param name="listener">The listener.</param>
    public void RemoveListener(Action listener) => listeners.Remove(listener);

    // Issue 370: a scrub area rendered before the input attaches without it; the input's publication tells it to send it.
    /// <summary>Publishes the visible input's element and notifies the input listeners when it changed.</summary>
    /// <param name="element">The input's element, or <see langword="null"/> when the input left.</param>
    public void PublishInput(ElementReference? element)
    {
        if (SameElement(Root.InputElement, element))
        {
            return;
        }

        Root.InputElement = element;
        for (int i = inputListeners.Count - 1; i >= 0; i--)
        {
            if (i < inputListeners.Count)
            {
                inputListeners[i]();
            }
        }
    }

    /// <summary>Adds a listener that runs after the visible input's element changed.</summary>
    /// <param name="listener">The listener.</param>
    public void AddInputListener(Action listener) => inputListeners.Add(listener);

    /// <summary>Removes a listener added with <see cref="AddInputListener"/>.</summary>
    /// <param name="listener">The listener.</param>
    public void RemoveInputListener(Action listener) => inputListeners.Remove(listener);

    /// <summary>Gets the fragment that cascades this context to <paramref name="content"/>.</summary>
    /// <param name="content">The content that receives the context.</param>
    /// <returns>The fragment.</returns>
    internal RenderFragment Cascade(RenderFragment? content) => builder =>
    {
        builder.OpenComponent<CascadingValue<NumberFieldRootContext>>(0);
        builder.AddComponentParameter(1, nameof(CascadingValue<NumberFieldRootContext>.Value), this);
        builder.AddComponentParameter(2, nameof(CascadingValue<NumberFieldRootContext>.IsFixed), true);
        builder.AddComponentParameter(3, nameof(CascadingValue<NumberFieldRootContext>.ChildContent), content);
        builder.CloseComponent();
    };
}

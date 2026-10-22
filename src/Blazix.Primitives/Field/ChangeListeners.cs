namespace Blazix.Primitives;

// FLD-EDGE-13, FFI-EDGE-09 (one render per part per change). The field and fieldset scopes' subscriber list. Listeners run in
// registration order: an enclosing part registers in OnInitialized before the parts inside it, so it renders first, and a
// part its render re-renders (any part given a fragment) already shows the new state when its own listener runs, which then
// finds nothing to render. In reverse order every such part rendered twice: once from its listener, once from its parent.
/// <summary>The listeners of a field or fieldset scope, notified in registration order.</summary>
internal sealed class ChangeListeners
{
    private readonly List<Action> listeners = [];

    /// <summary>Adds a listener.</summary>
    /// <param name="listener">The listener.</param>
    public void Add(Action listener) => listeners.Add(listener);

    /// <summary>Removes a listener.</summary>
    /// <param name="listener">The listener.</param>
    public void Remove(Action listener) => listeners.Remove(listener);

    /// <summary>Removes every listener.</summary>
    public void Clear() => listeners.Clear();

    /// <summary>Runs every listener once, in registration order.</summary>
    /// <remarks>
    /// A listener may add or remove listeners (a part created or disposed by the render it triggers): an added listener
    /// runs too, and a removed one that has not run yet does not.
    /// </remarks>
    public void Notify()
    {
        for (int i = 0; i < listeners.Count; i++)
        {
            Action listener = listeners[i];
            listener();
            if (i < listeners.Count && ReferenceEquals(listeners[i], listener))
            {
                continue;
            }

            // Removals moved the listener that ran, or it removed itself and left its successor at i.
            int index = listeners.IndexOf(listener);
            i = index >= 0 ? index : i - 1;
        }
    }
}

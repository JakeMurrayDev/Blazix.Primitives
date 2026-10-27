namespace Blazix.Primitives;

// PSH-STA-33, PSH-EDGE-07; utils/popups/popupTriggerMap.ts. An insertion-ordered map from trigger id to the registered
// element. Base UI keeps its reverse index (element to id) only outside production; the port throws in every build
// (design point 6 of M4 track 1, settled by the orchestrator 2026-10-27 under ADR 02 Amendments 2026-10-20's always-on
// misuse errors; ADR 02 Amendments 2026-10-27), so the reverse index is always kept. Removing keeps the insertion order of
// the rest, as a JavaScript Map does; the registry of one popup holds a handful of triggers.
/// <summary>Keeps the trigger elements registered with one popup store, by trigger id, in registration order.</summary>
internal sealed class PopupTriggerRegistry
{
    private readonly List<KeyValuePair<string, PopupTriggerElement>> entries = [];
    private readonly Dictionary<PopupTriggerElement, string> idsByElement = [];

    /// <summary>Gets the number of registered triggers.</summary>
    public int Count => entries.Count;

    /// <summary>Gets the registrations, as id and element, in registration order.</summary>
    public IReadOnlyList<KeyValuePair<string, PopupTriggerElement>> Entries => entries;

    // popupTriggerMap.ts:36-60.
    /// <summary>Registers <paramref name="element"/> under <paramref name="id"/>, replacing the element registered under that id.</summary>
    /// <param name="id">The trigger id.</param>
    /// <param name="element">The trigger element.</param>
    /// <exception cref="ArgumentNullException"><paramref name="id"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException"><paramref name="element"/> is registered under another id.</exception>
    public void Add(string id, PopupTriggerElement element)
    {
        ArgumentNullException.ThrowIfNull(id);
        if (idsByElement.TryGetValue(element, out string? existingId) && !string.Equals(existingId, id, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Blazix: A trigger element cannot be registered under multiple IDs in `PopupTriggerMap`.");
        }

        int index = IndexOf(id);
        if (index >= 0)
        {
            PopupTriggerElement previous = entries[index].Value;
            if (previous != element)
            {
                // Reusing an id for a different element evicts the previous one, which loses its claim on the id.
                idsByElement.Remove(previous);
            }

            entries[index] = new(id, element);
        }
        else
        {
            entries.Add(new(id, element));
        }

        idsByElement[element] = id;
    }

    /// <summary>Removes the element registered under <paramref name="id"/>.</summary>
    /// <param name="id">The trigger id.</param>
    public void Delete(string id)
    {
        int index = IndexOf(id);
        if (index < 0)
        {
            return;
        }

        idsByElement.Remove(entries[index].Value);
        entries.RemoveAt(index);
    }

    /// <summary>Gets the element registered under <paramref name="id"/>.</summary>
    /// <param name="id">The trigger id.</param>
    /// <param name="element">When this method returns, the element if one is registered; otherwise, the default value.</param>
    /// <returns><see langword="true"/> if an element is registered under <paramref name="id"/>; otherwise, <see langword="false"/>.</returns>
    public bool TryGetById(string? id, out PopupTriggerElement element)
    {
        int index = id is null ? -1 : IndexOf(id);
        element = index >= 0 ? entries[index].Value : default;
        return index >= 0;
    }

    /// <summary>Gets the element registered under <paramref name="id"/>, or <see langword="null"/>.</summary>
    /// <param name="id">The trigger id.</param>
    /// <returns>The element, or <see langword="null"/> when none is registered under <paramref name="id"/>.</returns>
    public PopupTriggerElement? GetById(string? id) => TryGetById(id, out PopupTriggerElement element) ? element : null;

    /// <summary>Determines whether <paramref name="element"/> is registered.</summary>
    /// <param name="element">The element.</param>
    /// <returns><see langword="true"/> if it is registered under some id; otherwise, <see langword="false"/>.</returns>
    public bool HasElement(PopupTriggerElement element) => idsByElement.ContainsKey(element);

    /// <summary>Determines whether a registered element matches <paramref name="predicate"/>.</summary>
    /// <param name="predicate">The test.</param>
    /// <returns><see langword="true"/> if one does; otherwise, <see langword="false"/>.</returns>
    public bool HasMatchingElement(Func<PopupTriggerElement, bool> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        foreach (KeyValuePair<string, PopupTriggerElement> entry in entries)
        {
            if (predicate(entry.Value))
            {
                return true;
            }
        }

        return false;
    }

    private int IndexOf(string id)
    {
        for (int i = 0; i < entries.Count; i++)
        {
            if (string.Equals(entries[i].Key, id, StringComparison.Ordinal))
            {
                return i;
            }
        }

        return -1;
    }
}

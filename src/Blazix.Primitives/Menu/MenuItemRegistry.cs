namespace Blazix.Primitives;

// MNU-EVT-10 (design point 9 (b) of M5 track 6, settled by the orchestrator 2026-11-04): a menu's items by rendered id, with
// their explicit labels, which the root's highlight report reads; the popup's initial focus reads the count (a keyboard
// open of a list with items is highlighted, MenuPopup.tsx:84-96). Each item registers itself, keyed by its behavior, so an
// id change replaces its entry.
/// <summary>Records the rendered ids of a menu's items and their explicit labels.</summary>
internal sealed class MenuItemRegistry
{
    private readonly Dictionary<object, string> ids = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<string, string?> labels = new(StringComparer.Ordinal);

    /// <summary>Gets the number of registered items.</summary>
    public int Count => ids.Count;

    /// <summary>Records an item's rendered id and explicit label, replacing its previous registration.</summary>
    /// <param name="owner">The item's behavior.</param>
    /// <param name="id">The item's rendered id.</param>
    /// <param name="label">The item's explicit label, or <see langword="null"/>.</param>
    public void Set(object owner, string id, string? label)
    {
        if (ids.TryGetValue(owner, out string? previous) && !string.Equals(previous, id, StringComparison.Ordinal))
        {
            labels.Remove(previous);
        }

        ids[owner] = id;
        labels[id] = label;
    }

    /// <summary>Removes an item's registration.</summary>
    /// <param name="owner">The item's behavior.</param>
    public void Remove(object owner)
    {
        if (ids.Remove(owner, out string? id))
        {
            labels.Remove(id);
        }
    }

    /// <summary>Returns the explicit label of the item with a rendered id.</summary>
    /// <param name="id">The rendered id, or <see langword="null"/>.</param>
    /// <returns>The label, or <see langword="null"/> for an unknown id or an item without one.</returns>
    public string? LabelOf(string? id) => id is not null && labels.TryGetValue(id, out string? label) ? label : null;
}

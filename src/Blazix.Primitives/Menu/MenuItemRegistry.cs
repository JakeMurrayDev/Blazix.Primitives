namespace Blazix.Primitives;

// MNU-EVT-10 (design point 9 (b) of M5 track 6, settled by the orchestrator 2026-11-04): a menu's items by rendered id, with
// their explicit labels, which the root's highlight report reads; the popup's initial focus reads the count (a keyboard
// open of a list with items is highlighted, MenuPopup.tsx:84-96). Each item registers itself, keyed by its behavior, so an
// id change replaces its entry.
/// <summary>Records the rendered ids of a menu's items and their explicit labels.</summary>
internal sealed class MenuItemRegistry
{
    /// <summary>Gets the number of registered items.</summary>
    public int Count => throw new NotImplementedException();

    /// <summary>Records an item's rendered id and explicit label, replacing its previous registration.</summary>
    /// <param name="owner">The item's behavior.</param>
    /// <param name="id">The item's rendered id.</param>
    /// <param name="label">The item's explicit label, or <see langword="null"/>.</param>
    public void Set(object owner, string id, string? label) => throw new NotImplementedException();

    /// <summary>Removes an item's registration.</summary>
    /// <param name="owner">The item's behavior.</param>
    public void Remove(object owner) => throw new NotImplementedException();

    /// <summary>Returns the explicit label of the item with a rendered id.</summary>
    /// <param name="id">The rendered id, or <see langword="null"/>.</param>
    /// <returns>The label, or <see langword="null"/> for an unknown id or an item without one.</returns>
    public string? LabelOf(string? id) => throw new NotImplementedException();
}

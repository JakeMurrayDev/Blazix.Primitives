namespace Blazix.Primitives;

// A menu's items by rendered id, which the popup's initial focus counts: a keyboard open of a list with items is
// highlighted (MenuPopup.tsx:84-96). Each item registers itself, keyed by its behavior, so an id change replaces its entry.
// The highlight's label is the composite module's report (design point 9 (a) of M5 track 6, settled by the orchestrator
// 2026-11-04).
/// <summary>Records the rendered ids of a menu's items.</summary>
internal sealed class MenuItemRegistry
{
    private readonly Dictionary<object, string> ids = new(ReferenceEqualityComparer.Instance);

    /// <summary>Gets the number of registered items.</summary>
    public int Count => ids.Count;

    /// <summary>Records an item's rendered id, replacing its previous registration.</summary>
    /// <param name="owner">The item's behavior.</param>
    /// <param name="id">The item's rendered id.</param>
    public void Set(object owner, string id) => ids[owner] = id;

    /// <summary>Removes an item's registration.</summary>
    /// <param name="owner">The item's behavior.</param>
    public void Remove(object owner) => ids.Remove(owner);
}

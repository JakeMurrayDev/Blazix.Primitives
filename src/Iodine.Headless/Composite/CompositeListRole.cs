namespace Blazix.Primitives;

// CMP-STR-06's element roles as the list handles carry them (M5 track 1, issue 159; ADR 04 Amendments 2026-11-03 (1),
// chosen by the user; design point 1 (a)); the item role is the item's registration.
/// <summary>The role a composite root plays as a handle of a popup list.</summary>
internal enum CompositeListRole
{
    /// <summary>The element that holds the focus outside the list: the active trigger, or the input under virtual focus.</summary>
    Reference,

    /// <summary>A trigger that opens the list but is not its reference, such as an inactive or detached trigger.</summary>
    Trigger,

    /// <summary>The list's floating element, which holds the items and writes their highlight.</summary>
    Floating,
}

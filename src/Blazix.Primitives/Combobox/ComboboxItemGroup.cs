namespace Blazix.Primitives;

// CMB-API-12 (`items` as `{ value?, label?, items }[]`, AriaCombobox.tsx:1861-1871; internals/resolveValueLabel.tsx's Group
// shape). The group shape is the track's (ADR 08 Amendments 2026-11-03 (4); M5 track 10 design point 2, pending the
// orchestrator): a typed list per group keeps `TValue` inferred from `ItemGroups`.
/// <summary>Represents a group of combobox items, passed to the root's <c>ItemGroups</c>.</summary>
/// <typeparam name="TValue">The type of the items' values.</typeparam>
/// <param name="Value">The group's identity, also its label when <see cref="Label"/> is not set.</param>
/// <param name="Items">The group's items, in order.</param>
public sealed record ComboboxItemGroup<TValue>(string Value, IReadOnlyList<TValue> Items)
{
    /// <summary>Gets the group's label.</summary>
    /// <value>The text a group label shows, or <see langword="null"/>, the default, to show <see cref="Value"/>.</value>
    public string? Label { get; init; }
}

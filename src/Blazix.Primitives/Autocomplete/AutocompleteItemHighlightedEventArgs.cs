namespace Blazix.Primitives;

// AUT-API-15 (autocomplete/root/AutocompleteRoot.tsx:90-105; CMB-API-37, EVT-05): `onItemHighlighted(value | undefined,
// { reason, event, index })`, on the pattern of ComboboxItemHighlightedEventArgs (design point 8 of M5 track 10), named by
// M5 track 11 in its dated ADR 08 line. A clear is unambiguous for a value type, because the index is -1.
/// <summary>Represents the arguments of an autocomplete's <c>OnItemHighlighted</c>: the highlighted value and the details.</summary>
/// <typeparam name="TValue">The type of the items' values.</typeparam>
/// <param name="Value">The highlighted item's value, or <see langword="default"/> when the highlight was cleared.</param>
/// <param name="Details">The reason, the event and the index, <c>-1</c> when the highlight was cleared.</param>
public readonly record struct AutocompleteItemHighlightedEventArgs<TValue>(TValue? Value, HighlightEventDetails Details)
{
    /// <summary>Gets a value indicating whether an item is highlighted.</summary>
    /// <value><see langword="false"/> when the highlight was cleared.</value>
    public bool Highlighted => Details.Index >= 0;
}

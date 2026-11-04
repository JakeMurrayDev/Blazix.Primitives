namespace Blazix.Primitives;

// CMB-API-37, EVT-05 (`onItemHighlighted(value | undefined, { reason, event, index })`); ADR 08 Amendments 2026-11-03 (4)
// names the args (design point 8 of M5 track 10, pending the orchestrator): a clear is unambiguous for a value type, because
// the index is -1.
/// <summary>Represents the arguments of a combobox's <c>OnItemHighlighted</c>: the highlighted value and the details.</summary>
/// <typeparam name="TValue">The type of the items' values.</typeparam>
/// <param name="Value">The highlighted item's value, or <see langword="default"/> when the highlight was cleared.</param>
/// <param name="Details">The reason, the event and the index, <c>-1</c> when the highlight was cleared.</param>
public readonly record struct ComboboxHighlightEventArgs<TValue>(TValue? Value, HighlightEventDetails Details)
{
    /// <summary>Gets a value indicating whether an item is highlighted.</summary>
    /// <value><see langword="false"/> when the highlight was cleared.</value>
    public bool Highlighted => Details.Index >= 0;
}

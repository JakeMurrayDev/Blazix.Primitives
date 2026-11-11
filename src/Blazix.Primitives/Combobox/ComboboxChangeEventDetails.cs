namespace Blazix.Primitives;

// CMB-EVT-08, §3.27 (Combobox.Root.ChangeEventDetails: the change details plus `isItemPress?`), the TabsChangeEventDetails
// precedent (ADR 08 Amendments 2026-10-26); in Combobox/, the family folder, rather than the shared Events/ (M5 track 10
// design-points note).
/// <summary>
/// Represents the details of a combobox's value or input-text change, which add whether an input clear followed an item press.
/// </summary>
public class ComboboxChangeEventDetails : ChangeEventDetails
{
    /// <summary>Initializes a new instance of the <see cref="ComboboxChangeEventDetails"/> class.</summary>
    /// <param name="reason">The reason for the request.</param>
    /// <param name="event">The snapshot of the native event that caused the request, or <see langword="null"/> when there was none.</param>
    /// <param name="trigger">The element that started the request, or <see langword="null"/> when no element did.</param>
    public ComboboxChangeEventDetails(EventReason reason, EventSnapshot? @event = null, EventTrigger? trigger = null)
        : base(reason, @event, trigger)
    {
    }

    /// <summary>Gets a value indicating whether an input clear followed an item press.</summary>
    /// <value>
    /// <see langword="true"/> for the clear that follows a selection; <see langword="false"/> for a close's clear that no item
    /// press caused; <see langword="null"/> for every other change.
    /// </value>
    public bool? IsItemPress { get; internal init; }
}

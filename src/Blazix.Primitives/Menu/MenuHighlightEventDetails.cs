namespace Blazix.Primitives;

// MNU-API-05, MNU-EVT-10/11 (menu/root/MenuRoot.tsx:580-622, 934-944; Menu.Root.HighlightEventDetails, MenuRoot.spec.tsx
// "no `index`"). ADR 08 Amendments 2026-11-03 (4) (chosen by the user 2026-11-03): Menu's OnItemHighlighted carries the
// item's id and label. Design point 3 (a) of M5 track 6 (pending the user): Menu's own details type beside its first
// consumer, with no index; Events/HighlightEventDetails, which carries one, stays Combobox's (EVD §3.7 by a dated note).
/// <summary>
/// Represents the details of a menu's <c>OnItemHighlighted</c> notification: the reason, the native event and the
/// highlighted item's label.
/// </summary>
public sealed class MenuHighlightEventDetails : GenericEventDetails
{
    /// <summary>Initializes a new instance of the <see cref="MenuHighlightEventDetails"/> class.</summary>
    /// <param name="reason">
    /// The reason for the change: <see cref="EventReason.Keyboard"/>, <see cref="EventReason.Pointer"/> or
    /// <see cref="EventReason.None"/>.
    /// </param>
    /// <param name="label">The highlighted item's label, or <see langword="null"/> when no item is highlighted.</param>
    /// <param name="event">The snapshot of the native event that caused the change, or <see langword="null"/> when there was none.</param>
    public MenuHighlightEventDetails(EventReason reason, string? label, EventSnapshot? @event = null)
        : base(reason, @event)
    {
        Label = label;
    }

    /// <summary>Gets the highlighted item's label.</summary>
    /// <value>The item's <c>Label</c>, else its text content; <see langword="null"/> when no item is highlighted.</value>
    public string? Label { get; }
}

/// <summary>Represents the arguments of a menu's <c>OnItemHighlighted</c> notification.</summary>
/// <param name="ItemId">The rendered id of the highlighted item, or <see langword="null"/> when no item is highlighted.</param>
/// <param name="Details">The details of the change.</param>
public readonly record struct MenuItemHighlightedEventArgs(string? ItemId, MenuHighlightEventDetails Details);

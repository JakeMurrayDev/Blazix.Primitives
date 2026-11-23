namespace Blazix.Primitives;

// MNU-EVT-10/11, MNU-STA-10 (menu/root/MenuRoot.tsx:580-622; menu/store/MenuStore.ts:224-240): OnItemHighlighted's reason
// bookkeeping, fed by the list handle's mirror (ListNavigationBehavior.HighlightChanged, track 1). A report is made when the
// highlighted index or item changed; its reason is the one the change's event gives (HighlightReason.From, EVD-ALG-11),
// consumed once, and `none` for a change that lands on the item already reported (a write-back).
/// <summary>Decides which highlight changes a menu reports to <c>OnItemHighlighted</c>, and with which reason.</summary>
internal sealed class MenuHighlightReporter
{
    private int? reportedIndex = -1;
    private string? reportedItemId;

    /// <summary>Takes a highlight change of the list and returns what the menu reports.</summary>
    /// <param name="index">The highlighted index, or <see langword="null"/> when nothing is highlighted.</param>
    /// <param name="itemId">The highlighted item's rendered id, or <see langword="null"/>.</param>
    /// <param name="event">The snapshot of the event that caused the change, or <see langword="null"/>.</param>
    /// <param name="label">The highlighted item's label, or <see langword="null"/>.</param>
    /// <returns>The notification's arguments, or <see langword="null"/> when nothing changed since the last report.</returns>
    public MenuItemHighlightedEventArgs? Report(int? index, string? itemId, EventSnapshot? @event, string? label)
    {
        int? itemIndex = itemId is null ? null : index;
        if (itemIndex == reportedIndex && string.Equals(itemId, reportedItemId, StringComparison.Ordinal))
        {
            return null;
        }

        // MenuStore.ts:224-240: a write back to the item already reported is tagged `none`.
        bool writeBack = itemId is not null && string.Equals(itemId, reportedItemId, StringComparison.Ordinal);
        EventReason reason = writeBack ? EventReason.None : HighlightReason.From(@event);
        reportedIndex = itemIndex;
        reportedItemId = itemId;
        return new MenuItemHighlightedEventArgs(itemId, new MenuHighlightEventDetails(reason, itemId is null ? null : label, writeBack ? null : @event));
    }
}

namespace Blazix.Primitives;

// UTL-ARIA-01 (internals/resolvePopupLabel.ts), the port of resolvePopupLabel in Menu's folder, its only user (M5 track 6's
// brief): a popup or list with no accessible name of its own is labelled by its active trigger; the trigger's rendered id is
// its registered id (issue 115), so the live id and the registered one agree. A consumer's aria-labelledby or aria-label is
// the bag's, which the merge applies.
/// <summary>Resolves the <c>aria-labelledby</c> a menu popup or list renders.</summary>
internal static class PopupLabel
{
    /// <summary>Returns the id the part labels itself by when the consumer gave it no name.</summary>
    /// <param name="attributes">The consumer's attributes.</param>
    /// <param name="activeTriggerId">The id of the active trigger, or <see langword="null"/>.</param>
    /// <returns>The active trigger's id, or <see langword="null"/> when the consumer named the part or there is no trigger.</returns>
    public static string? Resolve(IReadOnlyDictionary<string, object>? attributes, string? activeTriggerId)
    {
        if (attributes is not null)
        {
            if (attributes.TryGetValue("aria-labelledby", out object? labelledBy) && labelledBy is not null)
            {
                return null;
            }

            if (attributes.TryGetValue("aria-label", out object? label) && label is string text && text.Length > 0)
            {
                return null;
            }
        }

        return string.IsNullOrEmpty(activeTriggerId) ? null : activeTriggerId;
    }
}

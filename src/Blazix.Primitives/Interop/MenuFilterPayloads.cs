namespace Blazix.Primitives.Interop;

// MNU-EVT-16, EVT-19, KEY-31: an edit of the Input's text the user made, reported at `input` outside a composition and at
// `compositionend` (filter-dropdown/input/FilterDropdownInput.tsx:45-53, 58-103). Reason is `input-change` or `input-clear`.
/// <summary>Represents a change of the query the input reported.</summary>
/// <param name="Value">The input's text.</param>
/// <param name="Reason">The literal of the change's reason.</param>
/// <param name="Event">The event the change came from.</param>
internal sealed record MenuFilterValueChange(string Value, string Reason, EventSnapshot? Event);

// MNU-STA-17; M5 track 12's design point 2 (b′), settled by the orchestrator 2026-11-11: the List handle's report of the textContent of
// the items C# named in TextIds, at attach and after a mutation changes one, coalesced to one report per microtask.
/// <summary>Represents the rendered texts of a menu's items the list reported.</summary>
/// <param name="Ids">The items' ids.</param>
/// <param name="Texts">The items' texts, in the order of <paramref name="Ids"/>.</param>
internal sealed record MenuFilterTexts(IReadOnlyList<string> Ids, IReadOnlyList<string> Texts);

// MNU-STA-22; M5 track 12's design point 5 (a), settled by the orchestrator 2026-11-11: the module writes the Input's data-highlighted
// and reports it, which C# mirrors as MenuInputState.Highlighted.
/// <summary>Represents a change of the input's focus ring the module reported.</summary>
/// <param name="Highlighted">The new value.</param>
internal sealed record MenuFilterHighlightedChange(bool Highlighted);

// MNU-KEY-26: Shift+Tab in the Input, a `focus-out` close request; the module focuses the return element unless C# answers
// that the close was canceled.
/// <summary>Represents a focus-out close the input reported.</summary>
/// <param name="Event">The keyboard event.</param>
internal sealed record MenuFilterFocusOut(EventSnapshot? Event);

/// <summary>Represents a filterable submenu's open or close key, reported by its trigger's or popup's handle.</summary>
/// <param name="Open">The requested open state.</param>
/// <param name="Event">The keydown, or <see langword="null"/>.</param>
internal sealed record MenuFilterSubmenuKey(bool Open, EventSnapshot? Event);

// MNU-API-59, EVT-14 (M5 track 12, option (a), the orchestrator's 2026-11-12 grant): an editing key the Input kept for its
// caret, reported before Blazor dispatches it, so a filterable popup's consumer onkeydown skips it.
/// <summary>The Input's report of a keydown it kept for its caret.</summary>
/// <param name="Event">The keydown.</param>
internal sealed record MenuFilterOwnedKey(EventSnapshot? Event);

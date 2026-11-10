namespace Blazix.Primitives.Interop;

// CMP-EVT-01 (one report per change, ADR 16 rule 6)
/// <summary>The payload of the <c>composite</c> module's highlight report.</summary>
/// <param name="Index">The highlighted index: the item that holds the group's tab stop.</param>
internal sealed record CompositeHighlightChange(int Index);

// RDG-FOC-04, RDG-FOC-05
/// <summary>The payload of the <c>composite</c> module's report that focus left the root.</summary>
/// <param name="Event">The snapshot of the <c>focusout</c> whose related target is outside the root, or absent.</param>
internal sealed record CompositeFocusLeave(EventSnapshot Event);

// Option (i) of ADR 04's note of 2026-10-22, chosen by the user 2026-10-23.
/// <summary>The payload of the <c>composite</c> module's report of a key it handled and stopped without asking C# first.</summary>
/// <param name="Event">The snapshot of the <c>keydown</c>.</param>
internal sealed record CompositeKeyHandled(EventSnapshot Event);

// CMP-EVT-03 as a report of ids (M3 track 1, E2; ADR 04 Amendments 2026-10-26, chosen by the user; one self-contained
// sequence per report, design points 1 and 3, option (a), settled by the orchestrator 2026-10-26).
// Design point 5 (b) of M5 track 1, chosen by the user 2026-11-04: a long order arrives in parts of one publication.
/// <summary>The payload of the <c>composite</c> module's report of the items' order, whole or one part of it.</summary>
/// <param name="Ids">The ids of the registered items in index order, each once; for a part, the ids of that part.</param>
/// <param name="Publication">The number of the publication a part belongs to; <see langword="null"/> for a whole order.</param>
/// <param name="Part">The part's position, from 0; <see langword="null"/> for a whole order.</param>
/// <param name="Parts">The number of parts of the publication; <see langword="null"/> for a whole order.</param>
internal sealed record CompositeItemsChange(IReadOnlyList<string> Ids, int? Publication = null, int? Part = null, int? Parts = null);

// CMP-STA-31, CMP-EVT-04 (onNavigate) under ADR 04 Amendments 2026-11-03 (2), chosen by the user: the module writes the
// highlight and reports it once per change, with the C# request it had applied, so C# ignores a stale report.
/// <summary>The payload of the <c>composite</c> module's report of a list's highlight.</summary>
/// <param name="Index">The highlighted index, or <see langword="null"/> when no item is highlighted.</param>
/// <param name="ItemId">The id of the highlighted item's element, or <see langword="null"/> when no item is highlighted.</param>
/// <param name="Event">The snapshot of the event that moved the highlight, or <see langword="null"/> when no event did.</param>
/// <param name="Revision">The revision of the last highlight request from C# the module had applied; 0 before any.</param>
/// <param name="Label">
/// The highlighted item's label, its registered label else its text, or <see langword="null"/> when no item is highlighted
/// or the item has none (design point 9 (a) of M5 track 6, Menu's highlight label).
/// </param>
internal sealed record CompositeListHighlight(int? Index, string? ItemId, EventSnapshot? Event, int Revision, string? Label = null);

// CMP-EVT-06, CMP-EVT-07 (reason list-navigation) and CMP-EVT-08 (reason focus-out).
/// <summary>The payload of the <c>composite</c> module's request to open or close a list's popup.</summary>
/// <param name="Open">Whether the popup is to open.</param>
/// <param name="Reason">The reason: <c>list-navigation</c> or <c>focus-out</c>.</param>
/// <param name="Event">The snapshot of the <c>keydown</c>.</param>
/// <param name="TriggerId">The id of the trigger that opened the popup, or <see langword="null"/> for a close.</param>
internal sealed record CompositeListOpenRequest(bool Open, string Reason, EventSnapshot Event, string? TriggerId);

// CMP-EVT-10 (onMatch) on a closed list (ADR 04 Amendments 2026-11-03 (1)): the family's value request; a match while open
// moves the highlight and is reported as a highlight.
/// <summary>The payload of the <c>composite</c> module's report of a typeahead match while the list is closed.</summary>
/// <param name="Index">The index of the matching item.</param>
/// <param name="Event">The snapshot of the <c>keydown</c>.</param>
internal sealed record CompositeListMatch(int Index, EventSnapshot Event);

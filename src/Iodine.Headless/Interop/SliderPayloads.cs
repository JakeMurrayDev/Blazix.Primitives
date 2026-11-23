namespace Iodine.Headless.Interop;

// ADR 04 §3 (one message per state change, never per frame) and Amendments 2026-11-18 (the revision rule). The module
// computes the candidate (SLD §4.1), moves the thumb, writes the input's value and reports; nothing is reported for a move
// that leaves the candidate unchanged (SLD-STA-09, SLD-PTR-09). SLD-EVT-01..12.
/// <summary>The payload of a value request: the candidate the module computed and applied optimistically.</summary>
/// <param name="Reason">The reason's literal: <c>input-change</c>, <c>keyboard</c>, <c>track-press</c> or <c>drag</c>.</param>
/// <param name="ActiveThumbIndex">The index of the thumb whose value the candidate changes.</param>
/// <param name="Values">The candidate, one value per thumb: one element in single mode.</param>
/// <param name="Event">The snapshot of the event that caused the request.</param>
/// <param name="Revision">The module's revision of this act, which the answering update carries back.</param>
internal sealed record SliderValueRequest(string Reason, int ActiveThumbIndex, IReadOnlyList<double> Values, EventSnapshot Event, int Revision);

// SLD-STA-08, SLD-PTR-08/11/14/17: `data-dragging` is C#'s, rendered from this report (no mirrored field, ADR 04 §2's Slider
// row names styles only); the end of a gesture is C#'s cue to commit the last value it applied (SLD-EVT-05, SLD-EVT-12).
/// <summary>The payload of a change of the pointer or touch gesture.</summary>
/// <param name="Active">Whether a gesture is in progress.</param>
/// <param name="Dragging">Whether the gesture shows as a drag.</param>
/// <param name="Commit">Whether the gesture ended by a release, which commits the last applied value; <see langword="false"/> when it was dropped.</param>
/// <param name="Event">The snapshot of the release event, or <see langword="null"/> when the gesture did not end by one.</param>
internal sealed record SliderGestureReport(bool Active, bool Dragging, bool Commit, EventSnapshot? Event);

// SLD-STA-06/07, SLD-FOC-03/04, SLD-FRM-05(b)/06; ADR 04 Amendments 2026-10-20 (the blur classification): the module
// classifies each thumb blur by its related target once, and C# applies the focused flag, touched and the onBlur commit.
// The synthetic pair of SLD-KEY-11 is not reported.
/// <summary>The payload of a thumb input's focus or blur.</summary>
/// <param name="Index">The thumb's index.</param>
/// <param name="Focused">Whether the input received focus; <see langword="false"/> for a blur.</param>
/// <param name="Outside">For a blur, whether focus left every thumb of the slider.</param>
/// <param name="Event">The snapshot of the <c>focus</c> or <c>blur</c>.</param>
internal sealed record SliderFocusReport(int Index, bool Focused, bool Outside, EventSnapshot Event);

// SLD-API-22, SLD-EVT-09: where no C# chain ran inside the dispatch (Server), the consumer's C# `onkeydown` runs from this
// report, after the module handled the key (ADR 01's relaxation table, fallback run).
/// <summary>The payload of a key pressed on a thumb input that no C# chain saw inside the dispatch.</summary>
/// <param name="Index">The thumb's index.</param>
/// <param name="Event">The snapshot of the <c>keydown</c>.</param>
internal sealed record SliderKeyReport(int Index, EventSnapshot Event);

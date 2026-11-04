namespace Blazix.Primitives.Interop;

// ADR 04 Amendments 2026-11-03 (4): the `combobox` module's reports (names track 10's, refined at L3 by a dated line). A
// module-owned event reaches C# as a twin report carrying JavaScript's snapshot (ADR 04 Amendments 2026-10-18 item 1).
/// <summary>The payload of a change of the input text the module classified.</summary>
/// <param name="Text">The input's text after the change.</param>
/// <param name="Typed">
/// Whether the change is typed input (an <c>inputType</c> other than <c>insertReplacementText</c>, or a composition's end);
/// otherwise it is autofill-like and opens nothing.
/// </param>
/// <param name="Composing">Whether a composition is in progress, so the text is shown but not committed.</param>
/// <param name="Event">The snapshot of the <c>input</c> or <c>compositionend</c>.</param>
internal sealed record ComboboxInputChange(string Text, bool Typed, bool Composing, EventSnapshot Event);

// CMB-KEY-05, KEY-10, PTR-10, EVT-09: on Server the point's fallback ran in the dispatch, and C# runs the request now.
/// <summary>The payload of a clear or a removal the module started.</summary>
/// <param name="Index">The chip's index for a removal, or <c>-1</c> for a clear or the last value.</param>
/// <param name="Event">The snapshot of the keydown or click.</param>
internal sealed record ComboboxRequest(int Index, EventSnapshot Event);

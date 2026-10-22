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

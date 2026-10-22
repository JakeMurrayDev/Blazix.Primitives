namespace Blazix.Primitives.Interop;

// CMP-EVT-01 (one report per change, ADR 16 rule 6)
/// <summary>The payload of the <c>composite</c> module's highlight report.</summary>
/// <param name="Index">The highlighted index: the item that holds the group's tab stop.</param>
internal sealed record CompositeHighlightChange(int Index);

// RDG-FOC-04, RDG-FOC-05
/// <summary>The payload of the <c>composite</c> module's report that focus left the root.</summary>
/// <param name="Event">The snapshot of the <c>focusout</c> whose related target is outside the root, or absent.</param>
internal sealed record CompositeFocusLeave(EventSnapshot Event);

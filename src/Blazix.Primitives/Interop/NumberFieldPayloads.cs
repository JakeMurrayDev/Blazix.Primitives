namespace Blazix.Primitives.Interop;

// NUM-FOC-05, NUM-FRM-07 (NumberFieldInput.tsx:131-136)
/// <summary>The payload of the <c>number-field</c> module's focus report.</summary>
/// <param name="Event">The snapshot of the <c>focus</c> event.</param>
internal sealed record NumberFieldFocusReport(EventSnapshot Event);

// NUM-EVT-06..08 (NumberFieldInput.tsx:137-230)
/// <summary>The payload of the <c>number-field</c> module's blur report.</summary>
/// <param name="Text">The text the visible input showed when it lost focus.</param>
/// <param name="Revision">The revision of this report, which C# echoes in its next options.</param>
/// <param name="Event">The snapshot of the <c>blur</c> event.</param>
internal sealed record NumberFieldBlurReport(string Text, int Revision, EventSnapshot Event);

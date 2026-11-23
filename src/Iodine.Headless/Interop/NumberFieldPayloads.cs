namespace Iodine.Headless.Interop;

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

// NUM-KEY-01, NUM-KEY-02, NUM-KEY-12 (NumberFieldInput.tsx:351-404)
/// <summary>The payload of the <c>number-field</c> module's key step report.</summary>
/// <param name="Key">The step key: <c>ArrowUp</c>, <c>ArrowDown</c>, <c>Home</c> or <c>End</c>.</param>
/// <param name="Text">The text the input showed, which a dirty input steps from.</param>
/// <param name="Revision">The revision of this report, which C# echoes in its next options.</param>
/// <param name="Event">The snapshot of the <c>keydown</c>, whose modifiers pick the amount.</param>
internal sealed record NumberFieldKeyStepReport(string Key, string Text, int Revision, EventSnapshot Event);

// NUM-EVT-15 (NumberFieldInput.tsx:225-262)
/// <summary>The payload of the <c>number-field</c> module's change report.</summary>
/// <param name="Text">The text the input shows after an edit whose characters are all allowed.</param>
/// <param name="Revision">The revision of this report, which C# echoes in its next options.</param>
/// <param name="Event">The snapshot of the <c>input</c> event.</param>
internal sealed record NumberFieldChangeReport(string Text, int Revision, EventSnapshot Event);

// NUM-EVT-16 (NumberFieldInput.tsx:406-445)
/// <summary>The payload of the <c>number-field</c> module's paste report.</summary>
/// <param name="Text">The display text with the pasted text spliced in at the selection.</param>
/// <param name="Revision">The revision of this report, which C# echoes in its next options.</param>
/// <param name="Event">The snapshot of the <c>paste</c> event.</param>
internal sealed record NumberFieldPasteReport(string Text, int Revision, EventSnapshot Event);

// NUM-PTR-07, NUM-PTR-08
/// <summary>The payload of the <c>number-field</c> module's wheel step report.</summary>
/// <param name="Direction"><c>1</c> for a step up, <c>-1</c> for a step down.</param>
/// <param name="Event">The snapshot of the <c>wheel</c> event, whose modifiers pick the amount.</param>
internal sealed record NumberFieldWheelReport(int Direction, EventSnapshot Event);

// NUM-FRM-05
/// <summary>The payload of the <c>number-field</c> module's autofill report.</summary>
/// <param name="Value">The hidden input's <c>valueAsNumber</c>, or <see langword="null"/> when it is not a number.</param>
/// <param name="Event">The snapshot of the hidden input's <c>change</c> event.</param>
internal sealed record NumberFieldAutofillReport(double? Value, EventSnapshot Event);


// NUM-PTR-10, NUM-STR-06 (NumberFieldScrubArea.tsx:304-349)
/// <summary>The payload of the <c>number-field</c> module's scrub start report.</summary>
/// <param name="Cursor">A value indicating whether the pointer can have a virtual cursor: neither touch nor WebKit.</param>
/// <param name="Event">The snapshot of the <c>pointerdown</c>.</param>
internal sealed record NumberFieldScrubStartReport(bool Cursor, EventSnapshot Event);

// NUM-PTR-10, NUM-EDGE-09
/// <summary>The payload of the <c>number-field</c> module's pointer-lock report, sent when the outcome differs from the last one.</summary>
/// <param name="Denied">A value indicating whether the pointer-lock request was denied.</param>
internal sealed record NumberFieldScrubLockReport(bool Denied);

// NUM-PTR-11 (NumberFieldScrubArea.tsx:226-260)
/// <summary>The payload of the <c>number-field</c> module's scrub step report.</summary>
/// <param name="Delta">The crossing move's delta on the scrub axis: <c>movementX</c>, or <c>-movementY</c> when vertical.</param>
/// <param name="Revision">The revision of this report, which C# echoes in the input's next options.</param>
/// <param name="Event">The snapshot of the <c>pointermove</c>, whose modifiers pick the amount.</param>
internal sealed record NumberFieldScrubStepReport(double Delta, int Revision, EventSnapshot Event);

// NUM-PTR-13 (NumberFieldScrubArea.tsx:165-224)
/// <summary>The payload of the <c>number-field</c> module's scrub end report.</summary>
/// <param name="Event">The snapshot of the <c>pointerup</c>.</param>
internal sealed record NumberFieldScrubEndReport(EventSnapshot Event);

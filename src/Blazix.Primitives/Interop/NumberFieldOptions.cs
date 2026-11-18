namespace Blazix.Primitives.Interop;

// ADR 04 §1 (the `number-field` component module) and Amendments 2026-11-18; issue 277, design points 2 (b) and 7 (a),
// settled by the orchestrator 2026-11-18: 6a's options, the visible input's text and the echoed revision. 6b adds the gate's
// allowed set and locale symbols, the wheel and the scrub.
/// <summary>The options of the <c>number-field</c> module attached to a number field's visible input.</summary>
internal sealed record NumberFieldOptions : TwinOptions
{
    // NUM-STA-05/06: the module owns the input's `value` from attach and writes this text (C# → JS).
    /// <summary>Gets the text the visible input shows.</summary>
    /// <value>The display text C# computed.</value>
    public string Value { get; init; } = string.Empty;

    // The revision rule of ADR 04 Amendments 2026-11-18, widened to typing (design point 7 (a)).
    /// <summary>Gets the revision of the last input report C# applied, echoed so the module ignores an older update.</summary>
    /// <value>The revision; 0 before any report.</value>
    public int Revision { get; init; }
}

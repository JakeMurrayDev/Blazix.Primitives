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

    // NUM-KEY-07 (decision 7 (a)): the keydown gate tests membership in this set; C# re-validates every text.
    /// <summary>Gets the characters other than numerals that the input accepts for the locale and format.</summary>
    /// <value>The allowed characters, or <see langword="null"/> before the input role is configured.</value>
    public string? Allowed { get; init; }

    // NUM-KEY-09.
    /// <summary>Gets the locale's decimal separator, which the input accepts once.</summary>
    /// <value>The separator, or <see langword="null"/>.</value>
    public string? Decimal { get; init; }

    /// <summary>Gets the currency symbol the format writes, which the input accepts once.</summary>
    /// <value>The symbol, or <see langword="null"/> when the format writes none.</value>
    public string? Currency { get; init; }

    /// <summary>Gets the percent sign the format writes, which the input accepts once.</summary>
    /// <value>The sign, or <see langword="null"/> when the format writes none.</value>
    public string? Percent { get; init; }

    // NUM-KEY-02.
    /// <summary>Gets whether a minimum is set, so Home steps to it.</summary>
    /// <value><see langword="true"/> when the root has a <c>Min</c>; <see langword="null"/> when unset.</value>
    public bool? Min { get; init; }

    /// <summary>Gets whether a maximum is set, so End steps to it.</summary>
    /// <value><see langword="true"/> when the root has a <c>Max</c>; <see langword="null"/> when unset.</value>
    public bool? Max { get; init; }

    // NUM-PTR-07, NUM-PTR-09.
    /// <summary>Gets whether the wheel steps the focused input.</summary>
    /// <value><see langword="true"/> while <c>AllowWheelScrub</c> is set on an enabled, editable root.</value>
    public bool? Wheel { get; init; }

    // NUM-KEY-11, NUM-EVT-16.
    /// <summary>Gets whether the root is disabled, when the keydown and paste handlers do nothing.</summary>
    /// <value>The root's effective disabled state; <see langword="null"/> when unset.</value>
    public bool? Disabled { get; init; }

    /// <summary>Gets whether the root is read-only, when the keydown and paste handlers do nothing.</summary>
    /// <value>The root's read-only state; <see langword="null"/> when unset.</value>
    public bool? ReadOnly { get; init; }

    // NUM-EDGE-03.
    /// <summary>Gets whether development warnings are on.</summary>
    /// <value><see langword="true"/> to warn when the clipboard cannot be read during a paste.</value>
    public bool? DevelopmentChecks { get; init; }
}

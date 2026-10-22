namespace Blazix.Primitives.Interop;

// ADR 04 §1 (form row, Amendments 2026-10-18) and §3; FFI-ALG-02/04/08/10/16, FFI-FRM-09/10, FFI-PTR-01..04,
// FFI-FOC-05, FFI-STA-09. One options record per twin, with one sub-record per attaching role (DomOptions.Image's shape):
// a control and its validation input, a label; Form (track 2) adds the submit role.
/// <summary>
/// The options of the <c>form</c> module, sent on <c>attach</c> and on every <c>update</c>. Exactly one role is set per
/// attached element.
/// </summary>
/// <remarks>The module owns no style property, so a part leaves <see cref="TwinOptions.Style"/> unset.</remarks>
internal sealed record FormOptions : TwinOptions
{
    /// <summary>Gets the options of a control or of a control's validation input.</summary>
    /// <value>The control options, or <see langword="null"/>.</value>
    public FormControlOptions? Control { get; init; }

    /// <summary>Gets the options of a field label.</summary>
    /// <value>The label options, or <see langword="null"/>.</value>
    public FormLabelOptions? Label { get; init; }
}

/// <summary>The options of an attached control: its value, its validity and the reports it sends.</summary>
internal sealed record FormControlOptions
{
    /// <summary>Gets the id of the enclosing form's element.</summary>
    /// <value>The id the module compares the input's form owner with, or <see langword="null"/> outside a form.</value>
    public string? FormId { get; init; }

    /// <summary>Gets the custom message the field installs on the element.</summary>
    /// <value>The message, or <see langword="null"/> to withdraw the field's own message.</value>
    public string? CustomValidity { get; init; }

    /// <summary>Gets a value indicating whether the consumer owns the value.</summary>
    /// <value><see langword="true"/> when the element shows <see cref="Value"/>; the default is <see langword="false"/>.</value>
    public bool Controlled { get; init; }

    /// <summary>Gets the consumer's value.</summary>
    /// <value>The value the module shows when <see cref="Controlled"/>, re-asserted after an edit the consumer did not apply.</value>
    public string? Value { get; init; }

    /// <summary>Gets the default value.</summary>
    /// <value>The element's default value when not <see cref="Controlled"/>; the live value follows it until the user edits.</value>
    public string? DefaultValue { get; init; }

    /// <summary>Gets the refresh counter.</summary>
    /// <value>An increase asks the module to report the element's value and validity with this revision.</value>
    public int Revision { get; init; }

    /// <summary>Gets a value indicating whether the module reports every keydown.</summary>
    /// <value><see langword="true"/> while the consumer has a keydown handler; otherwise only Enter on an input is reported.</value>
    public bool ReportKeys { get; init; }

    /// <summary>Gets a value indicating whether the control is disabled.</summary>
    /// <value>When it turns <see langword="false"/>, the module reports whether the element already has focus.</value>
    public bool Disabled { get; init; }

    /// <summary>Gets a value indicating whether the module reports the element's events.</summary>
    /// <value><see langword="true"/>, the default; <see langword="false"/> for a validation input whose control reports elsewhere.</value>
    public bool ReportEvents { get; init; } = true;

    // FLD-FOC-04, INP-API-07 (DP10, pending the user): Blazor replaces a prerendered element when the page becomes
    // interactive, which loses the focus the browser's autofocus gave it.
    /// <summary>Gets a value indicating whether the module focuses the element when it attaches.</summary>
    /// <value><see langword="true"/> for an autofocus control; the module focuses it only when nothing else has focus.</value>
    public bool AutoFocus { get; init; }

    /// <summary>Gets the id of the element focus moves to when the attached element receives focus.</summary>
    /// <value>The visible control of a form-participation input, or <see langword="null"/>.</value>
    public string? FocusTarget { get; init; }
}

/// <summary>The options of an attached field label.</summary>
internal sealed record FormLabelOptions
{
    /// <summary>Gets a value indicating whether the label is a native <c>label</c>.</summary>
    /// <value><see langword="true"/>, the default, for association through <c>for</c>; <see langword="false"/> for click-to-focus.</value>
    public bool Native { get; init; } = true;

    /// <summary>Gets the id of the control a non-native label focuses.</summary>
    /// <value>The control id, or <see langword="null"/>.</value>
    public string? ControlId { get; init; }

    /// <summary>Gets a value indicating whether the consumer's C# pointer handlers run inside the dispatch first.</summary>
    /// <value><see langword="true"/> while the consumer has a mousedown, click or pointerdown handler.</value>
    public bool Decides { get; init; }

    /// <summary>Gets a value indicating whether the module reports a label element that contradicts <see cref="Native"/>.</summary>
    /// <value>The development warnings switch.</value>
    public bool DevelopmentChecks { get; init; }
}

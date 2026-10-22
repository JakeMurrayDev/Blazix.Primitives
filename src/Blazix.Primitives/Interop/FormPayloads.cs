namespace Blazix.Primitives.Interop;

// FFI-ALG-02 (the eleven ValidityState flags), FFI-FRM-06/08 (willValidate), FFI-ALG-04 (eligibility), FFI-FRM-09 (the
// field's own message is read back out). One message per event carries the value and this state (M2 plan, settled).
/// <summary>
/// The native validity of a validation input as the <c>form</c> module reads it, with the field's own custom message
/// taken out: the flags of <c>ValidityState</c>, <c>validationMessage</c>, <c>willValidate</c> and the eligibility of
/// the input for the enclosing form.
/// </summary>
internal sealed record NativeValidity
{
    /// <summary>Gets the state of an input that has not reported: valid, no message, validating, eligible.</summary>
    /// <value>The default state.</value>
    public static NativeValidity Default { get; } = new() { Valid = true };

    /// <summary>Gets a value indicating whether the browser cannot convert the input.</summary>
    /// <value>The <c>badInput</c> flag.</value>
    public bool BadInput { get; init; }

    /// <summary>Gets a value indicating whether a custom message other than the field's own is set.</summary>
    /// <value>The <c>customError</c> flag.</value>
    public bool CustomError { get; init; }

    /// <summary>Gets a value indicating whether the value does not match the pattern.</summary>
    /// <value>The <c>patternMismatch</c> flag.</value>
    public bool PatternMismatch { get; init; }

    /// <summary>Gets a value indicating whether the value is above the maximum.</summary>
    /// <value>The <c>rangeOverflow</c> flag.</value>
    public bool RangeOverflow { get; init; }

    /// <summary>Gets a value indicating whether the value is below the minimum.</summary>
    /// <value>The <c>rangeUnderflow</c> flag.</value>
    public bool RangeUnderflow { get; init; }

    /// <summary>Gets a value indicating whether the value does not fit the step.</summary>
    /// <value>The <c>stepMismatch</c> flag.</value>
    public bool StepMismatch { get; init; }

    /// <summary>Gets a value indicating whether the value is too long.</summary>
    /// <value>The <c>tooLong</c> flag.</value>
    public bool TooLong { get; init; }

    /// <summary>Gets a value indicating whether the value is too short.</summary>
    /// <value>The <c>tooShort</c> flag.</value>
    public bool TooShort { get; init; }

    /// <summary>Gets a value indicating whether the value does not have the syntax of the type.</summary>
    /// <value>The <c>typeMismatch</c> flag.</value>
    public bool TypeMismatch { get; init; }

    /// <summary>Gets a value indicating whether the value is valid.</summary>
    /// <value>The <c>valid</c> flag.</value>
    public bool Valid { get; init; }

    /// <summary>Gets a value indicating whether a required value is missing.</summary>
    /// <value>The <c>valueMissing</c> flag.</value>
    public bool ValueMissing { get; init; }

    /// <summary>Gets the browser's message for the first failing constraint.</summary>
    /// <value>The <c>validationMessage</c>, empty when valid or barred.</value>
    public string ValidationMessage { get; init; } = string.Empty;

    /// <summary>Gets a value indicating whether the input takes part in constraint validation.</summary>
    /// <value>The <c>willValidate</c> flag; <see langword="false"/> for a disabled or read-only input.</value>
    public bool WillValidate { get; init; } = true;

    /// <summary>Gets a value indicating whether the input belongs to the enclosing form and is not disabled.</summary>
    /// <value>The eligibility of the input for the enclosing form.</value>
    public bool Eligible { get; init; } = true;

    /// <summary>Gets a value indicating whether the field's own custom message is still installed on the input.</summary>
    /// <value><see langword="false"/> once someone else replaced or withdrew it, or before the field installed one.</value>
    public bool OwnMessageInstalled { get; init; }
}

// FLD-EVT-01/02/07, FLD-FOC-02, FLD-KEY-01, FFI-ALG-16, FFI-STA-09 (the active-element read), the refresh of a commit no
// event started; one message per event (ADR 04 §3, ADR 16 rule 6).
/// <summary>The payload of a control report: one event of the control, with the element's value and native validity.</summary>
internal sealed record FormControlReport
{
    /// <summary>Gets what happened.</summary>
    /// <value>
    /// <c>input</c> (the value changed), <c>blur</c>, <c>keydown</c>, <c>enter</c> (the deferred Enter commit),
    /// <c>sync</c> (the answer to a refresh), <c>focus</c> (the element was already focused when attached or enabled) or
    /// <c>label</c> (the native label the module looks for changed).
    /// </value>
    public string Type { get; init; } = string.Empty;

    // A `label` report carries the label's id here, so the payload stays one record (ADR 16 rule 7: a field more would
    // cost every report of every control).
    /// <summary>Gets the element's value, or the id of the native label a <c>label</c> report found.</summary>
    /// <value>The live value, or <see langword="null"/> for an element without one; for a <c>label</c> report, the label's id, or <see langword="null"/> when there is none.</value>
    public string? Value { get; init; }

    /// <summary>Gets the element's native validity.</summary>
    /// <value>The validity, with the field's own custom message taken out.</value>
    public NativeValidity? Validity { get; init; }

    /// <summary>Gets the snapshot of the event.</summary>
    /// <value>The snapshot, or <see langword="null"/> for a report no event caused.</value>
    public EventSnapshot? Event { get; init; }

    /// <summary>Gets a value indicating whether the element's form owner is the enclosing form.</summary>
    /// <value>For <c>keydown</c> and <c>enter</c>: whether the input belongs to the form whose id the options carry.</value>
    public bool SameForm { get; init; }

    /// <summary>Gets the refresh the report answers.</summary>
    /// <value>For <c>sync</c>: the revision of the options that asked for it.</value>
    public int Revision { get; init; }
}

// FFI-EDGE-02, FLD-EDGE-02 (the development mismatch report, once per nativeLabel value).
/// <summary>The payload of the label mismatch report: the rendered element contradicts the declared mode.</summary>
/// <param name="ExpectedNative"><see langword="true"/> when the label is declared native but its element is not a <c>label</c>.</param>
internal sealed record LabelMismatch(bool ExpectedNative);

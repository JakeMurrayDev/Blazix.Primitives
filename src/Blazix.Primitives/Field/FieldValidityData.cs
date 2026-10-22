namespace Blazix.Primitives;

// FFI-STA-03, FFI-ALG-14, FFI §3.2 (`Field.ValidityData`); field/root/FieldRoot.tsx:99-105,215-233,
// field/utils/getCombinedFieldValidityData.ts:7-18. A class, not a record: a publish creates a new instance even when its
// values equal the previous one's, and readers compare by reference (FLD-STA-08, FFI-EDGE-12).
/// <summary>The validity a field last published: its flags, its messages, the value it validated and the field's initial value.</summary>
internal sealed class FieldValidityData
{
    private static readonly string[] s_none = [];

    /// <summary>Initializes a new instance of the <see cref="FieldValidityData"/> class.</summary>
    /// <param name="state">The validity flags.</param>
    /// <param name="error">The first error message, or the empty string.</param>
    /// <param name="errors">Every error message.</param>
    /// <param name="value">The value the field validated.</param>
    /// <param name="initialValue">The value captured when the first control registered.</param>
    public FieldValidityData(FieldValidityFlags state, string error, IReadOnlyList<string> errors, object? value, object? initialValue)
    {
        State = state;
        Error = error;
        Errors = errors;
        Value = value;
        InitialValue = initialValue;
    }

    /// <summary>Gets the validity of a field that has not validated yet: every flag clear, valid unknown, no message, no values.</summary>
    /// <value>A new instance; each field starts from its own.</value>
    public static FieldValidityData Initial => new(default, string.Empty, s_none, null, null);

    /// <summary>Gets the validity flags.</summary>
    /// <value>The flags; <see cref="FieldValidityFlags.Valid"/> is the computed verdict only, without the external invalid state.</value>
    public FieldValidityFlags State { get; }

    /// <summary>Gets the first error message.</summary>
    /// <value>The message, or the empty string.</value>
    public string Error { get; }

    /// <summary>Gets every error message.</summary>
    /// <value>The messages, empty unless the field is invalid.</value>
    public IReadOnlyList<string> Errors { get; }

    /// <summary>Gets the value the field last validated.</summary>
    /// <value>The value, or <see langword="null"/> before the first validation.</value>
    public object? Value { get; }

    /// <summary>Gets the value captured when the first control registered.</summary>
    /// <value>The initial value, or <see langword="null"/> before any control registered.</value>
    public object? InitialValue { get; }

    // FFI-STA-04: the baseline is captured once, without republishing the verdict.
    /// <summary>Returns a copy with another initial value.</summary>
    /// <param name="initialValue">The initial value.</param>
    /// <returns>The copy.</returns>
    public FieldValidityData WithInitialValue(object? initialValue) => new(State, Error, Errors, Value, initialValue);

    // FFI-ALG-14 `combined(data, invalid)`: `valid = !invalid && data.state.valid`.
    /// <summary>Returns the validity with the external invalid state folded in.</summary>
    /// <param name="invalid">Whether the field is invalid for a reason outside its own validation (the invalid parameter, a form error).</param>
    /// <returns>This instance when <paramref name="invalid"/> is <see langword="false"/>; otherwise, a copy whose verdict is invalid.</returns>
    public FieldValidityData Combined(bool invalid) => invalid && State.Valid != false ? new(State with { Valid = false }, Error, Errors, Value, InitialValue) : this;
}

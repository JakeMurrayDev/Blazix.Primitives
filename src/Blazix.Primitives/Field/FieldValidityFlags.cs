namespace Blazix.Primitives;

// FFI-STA-03, FFI §3.2 (`Field.ValidityData.state`); field/root/FieldRoot.tsx:215-228,
// internals/field-constants/constants.ts:4-16. ADR 08 Amendments 2026-10-22 (the name is proposed there and refined by a
// dated note): a readonly record struct, so publishing a result allocates nothing for the flags. The default instance is
// the initial state of a field: every flag false and Valid null.
/// <summary>
/// Represents the validity of a field: the flags of the browser's <c>ValidityState</c> as the field last published them,
/// with <see cref="Valid"/> able to be unknown.
/// </summary>
/// <param name="BadInput">A value indicating whether the browser cannot convert the user's input.</param>
/// <param name="CustomError">A value indicating whether a custom message, from the field's validator or set on the element, makes the value invalid.</param>
/// <param name="PatternMismatch">A value indicating whether the value does not match the <c>pattern</c> attribute.</param>
/// <param name="RangeOverflow">A value indicating whether the value is greater than the <c>max</c> attribute.</param>
/// <param name="RangeUnderflow">A value indicating whether the value is less than the <c>min</c> attribute.</param>
/// <param name="StepMismatch">A value indicating whether the value does not fit the <c>step</c> attribute.</param>
/// <param name="TooLong">A value indicating whether the value is longer than the <c>maxlength</c> attribute.</param>
/// <param name="TooShort">A value indicating whether the value is shorter than the <c>minlength</c> attribute.</param>
/// <param name="TypeMismatch">A value indicating whether the value does not have the syntax its <c>type</c> requires.</param>
/// <param name="Valid">
/// <see langword="true"/> when the value is valid, <see langword="false"/> when it is invalid, or <see langword="null"/>
/// before the field has validated and while an asynchronous validator is running.
/// </param>
/// <param name="ValueMissing">A value indicating whether a required value is missing.</param>
public readonly record struct FieldValidityFlags(
    bool BadInput,
    bool CustomError,
    bool PatternMismatch,
    bool RangeOverflow,
    bool RangeUnderflow,
    bool StepMismatch,
    bool TooLong,
    bool TooShort,
    bool TypeMismatch,
    bool? Valid,
    bool ValueMissing);

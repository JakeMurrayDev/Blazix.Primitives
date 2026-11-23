namespace Blazix.Primitives;

// FLD-API-30, FLD §3.6 (the eleven `keyof ValidityState` literals `match` accepts); field/error/FieldError.tsx:153.
// FLD §16 item 3 (followed, PR 1c): Valid is one of the names, so a FieldError matching it renders while the field is
// valid (FLD-EDGE-07).
/// <summary>Specifies a flag of the browser's <c>ValidityState</c> that a <see cref="FieldError"/> can match.</summary>
public enum ValidityFlag
{
    /// <summary>Specifies the flag set while the browser cannot convert the user's input.</summary>
    BadInput,

    /// <summary>Specifies the flag set while a custom message, from the field's validator or set on the element, makes the value invalid.</summary>
    CustomError,

    /// <summary>Specifies the flag set while the value does not match the <c>pattern</c> attribute.</summary>
    PatternMismatch,

    /// <summary>Specifies the flag set while the value is greater than the <c>max</c> attribute.</summary>
    RangeOverflow,

    /// <summary>Specifies the flag set while the value is less than the <c>min</c> attribute.</summary>
    RangeUnderflow,

    /// <summary>Specifies the flag set while the value does not fit the <c>step</c> attribute.</summary>
    StepMismatch,

    /// <summary>Specifies the flag set while the value is longer than the <c>maxlength</c> attribute.</summary>
    TooLong,

    /// <summary>Specifies the flag set while the value is shorter than the <c>minlength</c> attribute.</summary>
    TooShort,

    /// <summary>Specifies the flag set while the value does not have the syntax its <c>type</c> requires.</summary>
    TypeMismatch,

    /// <summary>Specifies the flag set while the value is valid.</summary>
    Valid,

    /// <summary>Specifies the flag set while a required value is missing.</summary>
    ValueMissing,
}

namespace Iodine.Headless;

// OTP-DOM-17, OTP-DOM-18; otp-field/input/OTPFieldInputDataAttributes.ts, in its order, then `data-length`, which the state
// projects and Base UI's namespace omits (§16 item 1, replicated).
/// <summary>Provides the names of the data attributes a slot of a one-time code field renders.</summary>
public static class OtpFieldInputDataAttributes
{
    /// <summary>Represents the attribute present, with an empty value, while every slot holds a character: <c>data-complete</c>.</summary>
    public const string Complete = "data-complete";

    /// <summary>Represents the attribute present, with an empty value, while the field is disabled: <c>data-disabled</c>.</summary>
    public const string Disabled = "data-disabled";

    /// <summary>Represents the attribute present, with an empty value, while the field is read-only: <c>data-readonly</c>.</summary>
    public const string Readonly = "data-readonly";

    /// <summary>Represents the attribute present, with an empty value, while the field is required: <c>data-required</c>.</summary>
    public const string Required = "data-required";

    /// <summary>Represents the attribute present, with an empty value, while the field is valid inside a <see cref="FieldRoot"/>: <c>data-valid</c>.</summary>
    public const string Valid = "data-valid";

    /// <summary>Represents the attribute present, with an empty value, while the field is invalid inside a <see cref="FieldRoot"/>: <c>data-invalid</c>.</summary>
    public const string Invalid = "data-invalid";

    /// <summary>Represents the attribute present, with an empty value, once focus has left the field inside a <see cref="FieldRoot"/>: <c>data-touched</c>.</summary>
    public const string Touched = "data-touched";

    /// <summary>Represents the attribute present, with an empty value, while the value differs from the initial one inside a <see cref="FieldRoot"/>: <c>data-dirty</c>.</summary>
    public const string Dirty = "data-dirty";

    /// <summary>Represents the attribute present, with an empty value, while this slot holds a character: <c>data-filled</c>.</summary>
    public const string Filled = "data-filled";

    /// <summary>Represents the attribute present, with an empty value, while a slot of the field has focus: <c>data-focused</c>.</summary>
    public const string Focused = "data-focused";

    /// <summary>Represents the attribute that holds the number of slots, present while it is not 0: <c>data-length</c>.</summary>
    public const string Length = "data-length";
}

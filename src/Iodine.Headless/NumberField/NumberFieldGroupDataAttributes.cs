namespace Iodine.Headless;

// NUM-DOM-02..05; number-field/group/NumberFieldGroupDataAttributes.ts, in its order (the
// ten names every NumberField part shares, NUM §3.8).
/// <summary>Provides the names of the data attributes a number field group renders.</summary>
public static class NumberFieldGroupDataAttributes
{
    /// <summary>Represents the attribute present, with an empty value, while a scrub area of the field is being scrubbed: <c>data-scrubbing</c>.</summary>
    public const string Scrubbing = "data-scrubbing";

    /// <summary>Represents the attribute present, with an empty value, while the field is disabled: <c>data-disabled</c>.</summary>
    public const string Disabled = "data-disabled";

    /// <summary>Represents the attribute present, with an empty value, while the field is read-only: <c>data-readonly</c>.</summary>
    public const string Readonly = "data-readonly";

    /// <summary>Represents the attribute present, with an empty value, while the field is required: <c>data-required</c>.</summary>
    public const string Required = "data-required";

    /// <summary>Represents the attribute present, with an empty value, while the field is valid: <c>data-valid</c>.</summary>
    public const string Valid = "data-valid";

    /// <summary>Represents the attribute present, with an empty value, while the field is invalid: <c>data-invalid</c>.</summary>
    public const string Invalid = "data-invalid";

    /// <summary>Represents the attribute present, with an empty value, once the field has lost focus: <c>data-touched</c>.</summary>
    public const string Touched = "data-touched";

    /// <summary>Represents the attribute present, with an empty value, while the value differs from the field's initial one: <c>data-dirty</c>.</summary>
    public const string Dirty = "data-dirty";

    /// <summary>Represents the attribute present, with an empty value, while the field has a value: <c>data-filled</c>.</summary>
    public const string Filled = "data-filled";

    /// <summary>Represents the attribute present, with an empty value, while the field's input has focus: <c>data-focused</c>.</summary>
    public const string Focused = "data-focused";
}

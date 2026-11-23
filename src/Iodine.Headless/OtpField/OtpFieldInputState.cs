using Iodine.Headless.Rendering;

namespace Iodine.Headless;

// OTP-STA-11, OTP-DOM-17, OTP-DOM-18; otp-field/root/OTPFieldRootContext.ts:47-58 (getOTPFieldInputState: the root's state
// with the slot's value, filled and index). `Value` and `Index` produce no data attribute; `Length` does, as
// `data-length`, since the slot mapping does not suppress it (utils/stateAttributesMapping.ts:12-16; §16 item 1, replicated).
/// <summary>Represents the state of one slot of a one-time code field, which the part exposes as data attributes and passes to its class and style functions.</summary>
/// <param name="Value">The character in this slot, or <see cref="string.Empty"/> when it is empty; not projected.</param>
/// <param name="Length">The number of slots, the root's <c>Length</c>; projected as <c>data-length</c>.</param>
/// <param name="Disabled">A value indicating whether the field ignores user interaction; projected as <c>data-disabled</c>.</param>
/// <param name="Touched">A value indicating whether focus has left the field once; projected as <c>data-touched</c>.</param>
/// <param name="Dirty">A value indicating whether the value differs from the field's initial one; projected as <c>data-dirty</c>.</param>
/// <param name="Valid">
/// The field's validity: <see langword="true"/> projects <c>data-valid</c>, <see langword="false"/> projects
/// <c>data-invalid</c>, and <see langword="null"/>, before validation, projects neither.
/// </param>
/// <param name="Filled">A value indicating whether this slot holds a character; projected as <c>data-filled</c>.</param>
/// <param name="Focused">A value indicating whether a slot of the field has focus; projected as <c>data-focused</c>.</param>
/// <param name="Complete">A value indicating whether every slot holds a character; projected as <c>data-complete</c>.</param>
/// <param name="ReadOnly">A value indicating whether the user cannot change the value; projected as <c>data-readonly</c>.</param>
/// <param name="Required">A value indicating whether the field must have a value to submit its form; projected as <c>data-required</c>.</param>
/// <param name="Index">The slot's position among the field's slots, from 0; not projected.</param>
[PartState]
public sealed partial record OtpFieldInputState(
    [property: NotProjected] string Value,
    int Length,
    bool Disabled,
    bool Touched,
    bool Dirty,
    bool? Valid,
    bool Filled,
    bool Focused,
    bool Complete,
    bool ReadOnly,
    bool Required,
    [property: NotProjected] int Index);

using Blazix.Primitives.Rendering;

namespace Blazix.Primitives;

// OTP-STA-14, OTP-DOM-02..05; otp-field/root/OTPFieldRoot.tsx:311-324 (the field's state with the component's fields on
// top). ADR 08 §3: one record per part; `Value` and `Length` produce no data attribute (OTP-DOM-02,
// utils/stateAttributesMapping.ts:6-10).
/// <summary>Represents the state of a one-time code field, which the part exposes as data attributes and passes to its class and style functions.</summary>
/// <param name="Value">The field's value, every character accepted; not projected.</param>
/// <param name="Length">The number of slots, the root's <c>Length</c>; not projected.</param>
/// <param name="Disabled">A value indicating whether the field ignores user interaction, by its own parameter or its <see cref="FieldRoot"/>; projected as <c>data-disabled</c>.</param>
/// <param name="Touched">A value indicating whether focus has left the field once; projected as <c>data-touched</c>. Outside a field, always <see langword="false"/>.</param>
/// <param name="Dirty">A value indicating whether the value differs from the field's initial one; projected as <c>data-dirty</c>. Outside a field, always <see langword="false"/>.</param>
/// <param name="Valid">
/// The field's validity: <see langword="true"/> projects <c>data-valid</c>, <see langword="false"/> projects
/// <c>data-invalid</c>, and <see langword="null"/>, before validation, projects neither.
/// </param>
/// <param name="Filled">A value indicating whether the value is not empty; projected as <c>data-filled</c>.</param>
/// <param name="Focused">A value indicating whether a slot has focus; projected as <c>data-focused</c>.</param>
/// <param name="Complete">A value indicating whether every slot holds a character; projected as <c>data-complete</c>.</param>
/// <param name="ReadOnly">A value indicating whether the user cannot change the value; projected as <c>data-readonly</c>.</param>
/// <param name="Required">A value indicating whether the field must have a value to submit its form; projected as <c>data-required</c>.</param>
[PartState]
public sealed partial record OtpFieldRootState(
    [property: NotProjected] string Value,
    [property: NotProjected] int Length,
    bool Disabled,
    bool Touched,
    bool Dirty,
    bool? Valid,
    bool Filled,
    bool Focused,
    bool Complete,
    bool ReadOnly,
    bool Required);

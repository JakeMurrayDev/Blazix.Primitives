using Iodine.Headless.Rendering;

namespace Iodine.Headless;

// NUM-STA-01, NUM-STA-02; number-field/root/NumberFieldRoot.tsx:426-437. ADR 08 §3: one record per part (the
// Tabs precedent), the root's fields; `Value` and `InputValue` produce no data attribute
// (NUM-STA-01, utils/stateAttributesMapping.ts:5-9).
/// <summary>Represents the state of a number field input, which the part exposes as data attributes and passes to its class and style functions.</summary>
/// <param name="Value">The field's numeric value, or <see langword="null"/> while it is empty; not projected.</param>
/// <param name="InputValue">The text the field's input shows; not projected.</param>
/// <param name="Required">A value indicating whether the field must have a value to submit its form; projected as <c>data-required</c>.</param>
/// <param name="Disabled">A value indicating whether the field ignores user interaction, by its own parameter or its <see cref="FieldRoot"/>; projected as <c>data-disabled</c>.</param>
/// <param name="ReadOnly">A value indicating whether the user cannot change the value; projected as <c>data-readonly</c>.</param>
/// <param name="Scrubbing">A value indicating whether a scrub area of the field is being scrubbed; projected as <c>data-scrubbing</c>.</param>
/// <param name="Touched">A value indicating whether the field's input has lost focus once; projected as <c>data-touched</c>. Outside a field, always <see langword="false"/>.</param>
/// <param name="Dirty">A value indicating whether the value differs from the field's initial one; projected as <c>data-dirty</c>. Outside a field, always <see langword="false"/>.</param>
/// <param name="Valid">
/// The field's validity: <see langword="true"/> projects <c>data-valid</c>, <see langword="false"/> projects
/// <c>data-invalid</c>, and <see langword="null"/>, before validation, projects neither.
/// </param>
/// <param name="Filled">A value indicating whether the field has a value; projected as <c>data-filled</c>. Outside a field, always <see langword="false"/>.</param>
/// <param name="Focused">A value indicating whether the field's input has focus; projected as <c>data-focused</c>. Outside a field, always <see langword="false"/>.</param>
[PartState]
public sealed partial record NumberFieldInputState(
    [property: NotProjected] double? Value,
    [property: NotProjected] string InputValue,
    bool Required,
    bool Disabled,
    bool ReadOnly,
    bool Scrubbing,
    bool Touched,
    bool Dirty,
    bool? Valid,
    bool Filled,
    bool Focused);

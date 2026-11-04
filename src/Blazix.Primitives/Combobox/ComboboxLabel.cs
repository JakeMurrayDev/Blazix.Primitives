using Blazix.Primitives.Rendering;

namespace Blazix.Primitives;

// CMB §3.2, STA-01 (combobox/label/ComboboxLabel.tsx): the field state.
/// <summary>Represents the state of a combobox label, which the part exposes as data attributes and passes to its class and style functions.</summary>
/// <param name="Disabled">A value indicating whether the field is disabled; projected as <c>data-disabled</c>.</param>
/// <param name="Touched">A value indicating whether the field was touched; projected as <c>data-touched</c>.</param>
/// <param name="Dirty">A value indicating whether the value differs from the initial one; projected as <c>data-dirty</c>.</param>
/// <param name="Valid">The field's validity, or <see langword="null"/> before validation; projected as <c>data-valid</c> or <c>data-invalid</c>.</param>
/// <param name="Filled">A value indicating whether a value is present; projected as <c>data-filled</c>.</param>
/// <param name="Focused">A value indicating whether the control is focused; projected as <c>data-focused</c>.</param>
[PartState]
public sealed partial record ComboboxLabelState(
    bool Disabled,
    bool Touched,
    bool Dirty,
    bool? Valid,
    bool Filled,
    bool Focused);

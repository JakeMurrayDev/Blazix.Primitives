using Blazix.Primitives.Rendering;

namespace Blazix.Primitives;

// CMB §3.7, STA-05 (combobox/input/ComboboxInput.tsx): the field state (the default inside the popup), the popup and list flags.
/// <summary>Represents the state of a combobox input, which the part exposes as data attributes and passes to its class and style functions.</summary>
/// <param name="Disabled">A value indicating whether the input is disabled; projected as <c>data-disabled</c>.</param>
/// <param name="Touched">A value indicating whether the field was touched; projected as <c>data-touched</c>.</param>
/// <param name="Dirty">A value indicating whether the value differs from the initial one; projected as <c>data-dirty</c>.</param>
/// <param name="Valid">The field's validity, or <see langword="null"/> before validation; projected as <c>data-valid</c> or <c>data-invalid</c>.</param>
/// <param name="Filled">A value indicating whether a value is present; projected as <c>data-filled</c>.</param>
/// <param name="Focused">A value indicating whether the control is focused; projected as <c>data-focused</c>.</param>
/// <param name="Open">A value indicating whether the popup is open; projected as <c>data-popup-open</c> and <c>data-pressed</c>.</param>
/// <param name="ReadOnly">A value indicating whether the value cannot be changed; projected as <c>data-readonly</c>.</param>
/// <param name="PopupSide">The side of the anchor the mounted positioner settled on, or <see langword="null"/>; projected as <c>data-popup-side</c>.</param>
/// <param name="ListEmpty">A value indicating whether the filtered list is empty; projected as <c>data-list-empty</c>.</param>
[PartState]
public sealed partial record ComboboxInputState(
    bool Disabled,
    bool Touched,
    bool Dirty,
    bool? Valid,
    bool Filled,
    bool Focused,
    bool Open,
    bool ReadOnly,
    Side? PopupSide,
    bool ListEmpty);

// combobox/input/ComboboxInputDataAttributes.ts (CMB-DOM-02; `required` is declared at :27 and never emitted, §16 Q1: the constant is kept, M5 track 10 brief).
/// <summary>Provides the names of the data attributes a combobox input renders.</summary>
public static class ComboboxInputDataAttributes
{
    /// <summary>Represents the attribute present while the popup is open: <c>data-popup-open</c>.</summary>
    public const string PopupOpen = PopupTriggerDataAttributes.PopupOpen;

    /// <summary>Represents the attribute present while the popup is open: <c>data-pressed</c>.</summary>
    public const string Pressed = PopupTriggerDataAttributes.Pressed;

    /// <summary>Represents the attribute present while disabled: <c>data-disabled</c>.</summary>
    public const string Disabled = "data-disabled";

    /// <summary>Represents the attribute present while the value cannot be changed: <c>data-readonly</c>.</summary>
    public const string ReadOnly = "data-readonly";

    /// <summary>Represents the attribute whose value is the side of the mounted positioner: <c>data-popup-side</c>.</summary>
    public const string PopupSide = "data-popup-side";

    /// <summary>Represents the attribute declared by Base UI and never rendered; kept for parity: <c>data-required</c>.</summary>
    public const string Required = "data-required";

    /// <summary>Represents the attribute present while the filtered list is empty: <c>data-list-empty</c>.</summary>
    public const string ListEmpty = "data-list-empty";

    /// <summary>Represents the attribute present while the field is valid: <c>data-valid</c>.</summary>
    public const string Valid = "data-valid";

    /// <summary>Represents the attribute present while the field is invalid: <c>data-invalid</c>.</summary>
    public const string Invalid = "data-invalid";

    /// <summary>Represents the attribute present after the field was touched: <c>data-touched</c>.</summary>
    public const string Touched = "data-touched";

    /// <summary>Represents the attribute present while the value differs from the initial one: <c>data-dirty</c>.</summary>
    public const string Dirty = "data-dirty";

    /// <summary>Represents the attribute present while a value is present: <c>data-filled</c>.</summary>
    public const string Filled = "data-filled";

    /// <summary>Represents the attribute present while the control is focused: <c>data-focused</c>.</summary>
    public const string Focused = "data-focused";
}

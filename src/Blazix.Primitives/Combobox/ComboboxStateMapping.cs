using Blazix.Primitives.Rendering;

namespace Blazix.Primitives;

// combobox/utils/stateAttributesMapping.ts:7-19: the Input, InputGroup and Trigger take the pressable trigger mapping (open →
// data-popup-open and data-pressed), PopupSide → data-popup-side, ListEmpty → data-list-empty, and the field validity
// (data-valid / data-invalid). The generator projects the other booleans as data-<name> (readonly, disabled, placeholder,
// touched, dirty, filled, focused); Clear takes the plain trigger mapping (§16 Q8 replicated); Popup, Backdrop, Positioner
// and Arrow the popup mappings; Empty → data-empty (the default projection).
/// <summary>Builds the state mappings of the combobox-family parts.</summary>
internal static class ComboboxStateMapping
{
    private const string OpenField = "Open";

    private static readonly StateDataAttribute[] s_listEmpty = [new(ComboboxInputDataAttributes.ListEmpty, string.Empty)];

    /// <summary>Creates the input's, the input group's or the trigger's mapping.</summary>
    /// <typeparam name="TState">The part's state record, with <c>Open</c>, <c>Valid</c>, <c>PopupSide</c> and <c>ListEmpty</c>.</typeparam>
    /// <returns>The mapping.</returns>
    public static StateAttributesMapping<TState> CreateInput<TState>() =>
        FieldValidityMapping.Create<TState>()
            .Map(OpenField, PopupStateMapping.PressableTriggerOpen)
            .Map<Side?>("PopupSide", static side => side is { } value ? [new StateDataAttribute(ComboboxInputDataAttributes.PopupSide, FloatingLiterals.ToLiteral(value))] : null)
            .Map<bool>("ListEmpty", static empty => empty ? s_listEmpty : null);
}

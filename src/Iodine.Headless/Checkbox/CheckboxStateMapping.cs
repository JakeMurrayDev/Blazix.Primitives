using Blazix.Primitives.Rendering;

namespace Blazix.Primitives;

// CHK-DOM-02/03/06; checkbox/utils/getCheckboxStateAttributesMapping.ts (checked, then the field validity mapping). Base UI
// builds the mapping per state, so the `checked` projection can see `indeterminate`; a port projection sees one field, so
// there are two static mappings and a part picks the one of its current state: exactly one of data-checked and
// data-unchecked while not indeterminate, neither while indeterminate (data-indeterminate is the default projection of
// its own field). The shared static arrays make a render allocate nothing.
/// <summary>Provides the mappings from the checked and validity fields of a checkbox part's state to their data attributes.</summary>
internal static class CheckboxStateMapping
{
    private const string CheckedField = "Checked";
    private const string ValidField = "Valid";

    private static readonly StateDataAttribute[] s_checked = [new(CheckboxRootDataAttributes.Checked, string.Empty)];
    private static readonly StateDataAttribute[] s_unchecked = [new(CheckboxRootDataAttributes.Unchecked, string.Empty)];
    private static readonly StateDataAttribute[] s_valid = [new(CheckboxRootDataAttributes.Valid, string.Empty)];
    private static readonly StateDataAttribute[] s_invalid = [new(CheckboxRootDataAttributes.Invalid, string.Empty)];
    private static readonly StateFieldProjection<bool> s_checkedProjection = ProjectChecked;
    private static readonly StateFieldProjection<bool> s_mixedProjection = static _ => null;
    private static readonly StateFieldProjection<bool?> s_validProjection = ProjectValid;

    /// <summary>Creates the mapping for a checkbox part's state record.</summary>
    /// <typeparam name="TState">The type of the state record, whose <c>Checked</c> and <c>Valid</c> fields the mapping replaces.</typeparam>
    /// <param name="indeterminate">Whether the mapping is the one of an indeterminate state, which projects nothing for <c>Checked</c>.</param>
    /// <returns>The mapping.</returns>
    public static StateAttributesMapping<TState> Create<TState>(bool indeterminate) =>
        new StateAttributesMapping<TState>()
            .Map(CheckedField, indeterminate ? s_mixedProjection : s_checkedProjection)
            .Map(ValidField, s_validProjection);

    private static StateDataAttribute[] ProjectChecked(bool value) => value ? s_checked : s_unchecked;

    private static StateDataAttribute[]? ProjectValid(bool? value) => value switch
    {
        true => s_valid,
        false => s_invalid,
        null => null,
    };
}

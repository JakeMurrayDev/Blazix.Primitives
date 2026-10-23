using Blazix.Primitives.Rendering;

namespace Blazix.Primitives;

// RAD-DOM-03, RAD-DOM-06, RAD-DOM-15; radio/utils/stateAttributesMapping.ts (checked, then the field validity mapping; the
// transition status is the transitions module's to write). One hand-written mapping serves both parts (the Progress
// precedent): exactly one of data-checked and data-unchecked, data-valid or data-invalid or neither, and the other
// booleans through the generator's default projection. The shared static arrays make a render allocate nothing.
/// <summary>Provides the mapping from the checked and validity fields of a radio part's state to their data attributes.</summary>
internal static class RadioStateMapping
{
    private const string CheckedField = "Checked";
    private const string ValidField = "Valid";

    private static readonly StateDataAttribute[] s_checked = [new(RadioRootDataAttributes.Checked, string.Empty)];
    private static readonly StateDataAttribute[] s_unchecked = [new(RadioRootDataAttributes.Unchecked, string.Empty)];
    private static readonly StateDataAttribute[] s_valid = [new(RadioRootDataAttributes.Valid, string.Empty)];
    private static readonly StateDataAttribute[] s_invalid = [new(RadioRootDataAttributes.Invalid, string.Empty)];
    private static readonly StateFieldProjection<bool> s_checkedProjection = ProjectChecked;
    private static readonly StateFieldProjection<bool?> s_validProjection = ProjectValid;

    /// <summary>Creates the mapping for a radio part's state record.</summary>
    /// <typeparam name="TState">The type of the state record, whose <c>Checked</c> and <c>Valid</c> fields the mapping replaces.</typeparam>
    /// <returns>The mapping.</returns>
    public static StateAttributesMapping<TState> Create<TState>() =>
        new StateAttributesMapping<TState>()
            .Map(CheckedField, s_checkedProjection)
            .Map(ValidField, s_validProjection);

    private static StateDataAttribute[] ProjectChecked(bool value) => value ? s_checked : s_unchecked;

    private static StateDataAttribute[]? ProjectValid(bool? value) => value switch
    {
        true => s_valid,
        false => s_invalid,
        null => null,
    };
}

using Blazix.Primitives.Rendering;

namespace Blazix.Primitives;

// SWT-DOM-02, SWT-DOM-05; switch/stateAttributesMapping.ts (the field validity mapping, then checked). ADR 02 decision 4:
// a custom mapping is hand-written. Both parts' state records have the same nine fields, so one projection serves both:
// exactly one of data-checked and data-unchecked; data-valid for true, data-invalid for false, neither for null. The
// other booleans keep the default projection. The shared static arrays make a render allocate nothing.
/// <summary>Provides the mapping from the checked and validity fields of a switch part's state to their data attributes.</summary>
internal static class SwitchStateMapping
{
    private const string CheckedField = "Checked";
    private const string ValidField = "Valid";

    private static readonly StateDataAttribute[] s_checked = [new(SwitchRootDataAttributes.Checked, string.Empty)];
    private static readonly StateDataAttribute[] s_unchecked = [new(SwitchRootDataAttributes.Unchecked, string.Empty)];
    private static readonly StateDataAttribute[] s_valid = [new(SwitchRootDataAttributes.Valid, string.Empty)];
    private static readonly StateDataAttribute[] s_invalid = [new(SwitchRootDataAttributes.Invalid, string.Empty)];
    private static readonly StateFieldProjection<bool> s_checkedProjection = ProjectChecked;
    private static readonly StateFieldProjection<bool?> s_validProjection = ProjectValid;

    /// <summary>Creates the mapping for a switch part's state record.</summary>
    /// <typeparam name="TState">The type of the state record, whose <c>Checked</c> and <c>Valid</c> fields the mapping replaces.</typeparam>
    /// <returns>The mapping.</returns>
    public static StateAttributesMapping<TState> Create<TState>() =>
        new StateAttributesMapping<TState>().Map(CheckedField, s_checkedProjection).Map(ValidField, s_validProjection);

    private static StateDataAttribute[] ProjectChecked(bool value) => value ? s_checked : s_unchecked;

    private static StateDataAttribute[]? ProjectValid(bool? value) => value switch
    {
        true => s_valid,
        false => s_invalid,
        null => null,
    };
}

using Blazix.Primitives.Rendering;

namespace Blazix.Primitives;

// FFI-DOM-01, FLD-DOM-03; internals/field-constants/constants.ts:34-44 (`fieldValidityMapping`). The generated projection
// writes a bool? only when it is true; the field's verdict writes data-valid for true, data-invalid for false and nothing
// while unknown. Shared by every part whose state carries the verdict (Field, Input, and the controls of tracks 4-8).
/// <summary>Provides the mapping of a field's verdict to <c>data-valid</c> and <c>data-invalid</c>.</summary>
internal static class FieldValidityMapping
{
    private const string ValidField = "Valid";

    private static readonly StateDataAttribute[] s_valid = [new("data-valid", string.Empty)];
    private static readonly StateDataAttribute[] s_invalid = [new("data-invalid", string.Empty)];
    private static readonly StateFieldProjection<bool?> s_projection = Project;

    /// <summary>Creates the mapping for a state record with a <c>Valid</c> field of type <see langword="bool"/>?.</summary>
    /// <typeparam name="TState">The type of the state record.</typeparam>
    /// <returns>The mapping.</returns>
    public static StateAttributesMapping<TState> Create<TState>() => new StateAttributesMapping<TState>().Map(ValidField, s_projection);

    private static StateDataAttribute[]? Project(bool? valid) => valid switch
    {
        true => s_valid,
        false => s_invalid,
        null => null,
    };
}

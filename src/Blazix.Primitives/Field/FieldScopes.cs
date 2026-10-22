namespace Blazix.Primitives;

// FFI-STR-02, FFI-EDGE-01, FLD-STR-02, FLD-EDGE-01: Label, Description, Error, Validity and Item throw outside a field;
// Base UI's message, with the port's prefix and names (PRG-EDGE-01 precedent; a dated port note on FFI-EDGE-01).
/// <summary>Provides the lookup of the field scope that the field parts other than the control require.</summary>
internal static class FieldScopes
{
    /// <summary>Throws the error of a field part rendered outside a field.</summary>
    /// <param name="context">The cascaded field scope, or <see langword="null"/>.</param>
    /// <returns>The field scope.</returns>
    /// <exception cref="InvalidOperationException"><paramref name="context"/> is <see langword="null"/>.</exception>
    public static FieldRootContext RequireField(FieldRootContext? context)
    {
        return context ?? throw new InvalidOperationException(
            "Blazix: The `FieldRoot` context is missing. Field parts must be placed within `<FieldRoot>`.");
    }
}

namespace Blazix.Primitives;

// FFI-EVT-05, FLD-API-04, FLD-EVT-08; field/root/FieldRoot.tsx:285-290 (`validate(value, formValues)`). ADR 08
// Amendments 2026-10-22: a readonly record struct, as MeterValueArguments is. ADR 05 Amendments 2026-10-22: the `object?`
// slot holds the committed value once per validation run, never per render.
/// <summary>Represents what a field's validator receives: the value being validated and the values of the form.</summary>
/// <param name="Value">
/// The value the field validates: a <see langword="string"/> for <see cref="FieldControl"/> and <see cref="Input"/>, the
/// control's own value type for the other controls, or <see langword="null"/>.
/// </param>
/// <param name="Values">The values of the enclosing form's named fields, or <see cref="FormValues.Empty"/> outside a form.</param>
public readonly record struct FieldValidationArguments(object? Value, FormValues Values);

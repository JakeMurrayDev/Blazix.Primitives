namespace Blazix.Primitives;

// FFI §2 (registration record `{ controlRef, id, name?, getValue?, value }`), FFI-ALG-12, FFI §6.1
// `registerFieldControl(controlRef, id, value, getValue?, enabled, name?)`. The control handle is the id of the element a
// form focuses (FFI-FOC-01); RefreshAsync asks the browser-side behavior for the control's current value and validity
// before an imperative validation (a commit no event started).
/// <summary>The registration of a field's active control.</summary>
/// <param name="Id">The control id, which keys the field's entry in the form's registry, or <see langword="null"/>.</param>
/// <param name="Name">The control's own name, which the field uses when it has none, or <see langword="null"/>.</param>
/// <param name="Value">The control's raw value (the initial-value baseline and the validator's argument), or <see langword="null"/> when <paramref name="GetValue"/> supplies it.</param>
/// <param name="GetValue">Returns the control's value as the form's values carry it, or <see langword="null"/> to use <paramref name="Value"/>.</param>
/// <param name="ControlId">The id of the element focused when a form blocks a submission on this field, or <see langword="null"/>.</param>
/// <param name="RefreshAsync">Brings the control's value and validity up to date from the browser, or <see langword="null"/>.</param>
internal sealed record FieldControlRegistration(
    string? Id,
    string? Name,
    object? Value,
    Func<object?>? GetValue,
    string? ControlId,
    Func<Task>? RefreshAsync);

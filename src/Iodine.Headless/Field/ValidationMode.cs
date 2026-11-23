namespace Blazix.Primitives;

// FFI-API-02, FLD-API-05, FRM-API-01; form/Form.tsx:181 (`Form.ValidationMode`). ADR 08 Amendments 2026-10-22: one
// shared enum, since Field and Form share the literal set; declared beside its first consumer, FieldRoot.
/// <summary>Specifies when a field validates its value.</summary>
public enum ValidationMode
{
    /// <summary>Specifies validation when the form is submitted, and again on every change after the first submission.</summary>
    OnSubmit,

    /// <summary>Specifies validation when the control loses focus.</summary>
    OnBlur,

    /// <summary>Specifies validation on every change of the control's value.</summary>
    OnChange,
}

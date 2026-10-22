namespace Blazix.Primitives;

// FFI §2 (form scope), FFI-STR-04/05, FFI-API-04, FFI-EVT-03; internals/form-context/FormContext.ts. The field side of the
// form-scope contract: what a field and its control read from the enclosing form. Form (M2 track 2, Form/) implements it
// and cascades it fixed as this interface; outside a Form the default scope applies.
/// <summary>The form scope a field reads: the registry it writes into, the form's errors, its mode and its submissions.</summary>
internal interface IFormScope
{
    /// <summary>Gets the registry of the form's fields, keyed by control id.</summary>
    /// <value>The registry; outside a form, the registry of every field of the runtime that is outside a form.</value>
    FieldRegistry Registry { get; }

    /// <summary>Gets the form's validation mode, which a field without its own mode uses.</summary>
    /// <value>The mode; outside a form <see cref="ValidationMode.OnSubmit"/>.</value>
    ValidationMode ValidationMode { get; }

    /// <summary>Gets the number of submissions the form has started.</summary>
    /// <value>The count; outside a form 0.</value>
    int SubmitCount { get; }

    // FRM-DOM-03, design point F6 of M2 track 2 (settled by the orchestrator 2026-10-23): a token, never rendered, under
    // which the form's browser-side behavior registers its element.
    /// <summary>Gets the token that names the form to the browser-side behavior of its controls.</summary>
    /// <value>The token, or <see langword="null"/> outside a form.</value>
    string? FormToken { get; }

    /// <summary>Gets the form's error for a field name.</summary>
    /// <param name="name">The field's effective name.</param>
    /// <param name="errors">The messages when the method returns <see langword="true"/>.</param>
    /// <returns><see langword="true"/> when the form has an error with at least one message for <paramref name="name"/>; otherwise, <see langword="false"/>.</returns>
    bool TryGetErrors(string name, out IReadOnlyList<string> errors);

    /// <summary>Removes the form's error for a field name.</summary>
    /// <param name="name">The field's effective name; <see langword="null"/> or empty removes nothing.</param>
    void ClearErrors(string? name);

    /// <summary>Adds a listener that runs after the form's errors or its validation mode changed.</summary>
    /// <param name="listener">The listener.</param>
    void AddListener(Action listener);

    /// <summary>Removes a listener added with <see cref="AddListener"/>.</summary>
    /// <param name="listener">The listener.</param>
    void RemoveListener(Action listener);
}

// FFI-STR-05 (fidelity correction 2026-10-23, settled by the orchestrator 2026-10-23: ADR 15; COL-DOM-05 precedent);
// internals/form-context/FormContext.ts:33-46, internals/field-register-control/useFieldControlRegistration.ts:71,113. Base
// UI's default form context holds one `fields` map that every field outside a Form registers in, so a validator's form
// values name every such field. The port keeps one per runtime: a scoped service (per circuit on Server, per application
// on WebAssembly), never static, which would share values across users. Registered by AddBlazixPrimitives.
/// <summary>
/// The form scope outside a form: the registry of the runtime's fields outside a form, no error,
/// <see cref="ValidationMode.OnSubmit"/>, no submission.
/// </summary>
internal sealed class DefaultFormScope : IFormScope
{
    private static readonly string[] s_none = [];

    /// <inheritdoc />
    public FieldRegistry Registry { get; } = new();

    /// <inheritdoc />
    public ValidationMode ValidationMode => ValidationMode.OnSubmit;

    /// <inheritdoc />
    public int SubmitCount => 0;

    /// <inheritdoc />
    public string? FormToken => null;

    /// <inheritdoc />
    public bool TryGetErrors(string name, out IReadOnlyList<string> errors)
    {
        errors = s_none;
        return false;
    }

    /// <inheritdoc />
    public void ClearErrors(string? name)
    {
    }

    /// <inheritdoc />
    public void AddListener(Action listener)
    {
    }

    /// <inheritdoc />
    public void RemoveListener(Action listener)
    {
    }
}

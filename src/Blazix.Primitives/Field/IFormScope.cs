namespace Blazix.Primitives;

// FFI §2 (form scope), FFI-STR-04/05, FFI-API-04, FFI-EVT-03; internals/form-context/FormContext.ts. The field side of the
// form-scope contract: what a field and its control read from the enclosing form. Form (M2 track 2, Form/) implements it
// and cascades it fixed as this interface; outside a Form the default scope applies.
/// <summary>The form scope a field reads: the registry it writes into, the form's errors, its mode and its submissions.</summary>
internal interface IFormScope
{
    /// <summary>Gets the registry of the form's fields, keyed by control id.</summary>
    /// <value>The registry; outside a form it accepts no entry.</value>
    FieldRegistry Registry { get; }

    /// <summary>Gets the form's validation mode, which a field without its own mode uses.</summary>
    /// <value>The mode; outside a form <see cref="ValidationMode.OnSubmit"/>.</value>
    ValidationMode ValidationMode { get; }

    /// <summary>Gets the number of submissions the form has started.</summary>
    /// <value>The count; outside a form 0.</value>
    int SubmitCount { get; }

    /// <summary>Gets the id of the form element, which the browser-side behavior compares a control's form owner with.</summary>
    /// <value>The id, or <see langword="null"/> outside a form.</value>
    string? ElementId { get; }

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

// FFI-STR-05; internals/form-context/FormContext.ts:33-46. Stateless, so one instance serves every field outside a form: its
// registry accepts nothing (Base UI's default map is shared by every formless field; FFI-STR-05 states it empty).
/// <summary>The form scope outside a form: no registry entry, no error, <see cref="ValidationMode.OnSubmit"/>, no submission.</summary>
internal sealed class DefaultFormScope : IFormScope
{
    private static readonly string[] s_none = [];

    private DefaultFormScope()
    {
    }

    /// <summary>Gets the one instance.</summary>
    /// <value>The default scope.</value>
    public static DefaultFormScope Instance { get; } = new();

    /// <inheritdoc />
    public FieldRegistry Registry => FieldRegistry.None;

    /// <inheritdoc />
    public ValidationMode ValidationMode => ValidationMode.OnSubmit;

    /// <inheritdoc />
    public int SubmitCount => 0;

    /// <inheritdoc />
    public string? ElementId => null;

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

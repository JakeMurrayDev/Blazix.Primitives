namespace Blazix.Primitives;

// FFI §2 (form scope), FFI-STR-04, FFI-API-04, FFI-EVT-03/04, FFI-FOC-03; FRM-STR-01, FRM-STA-02..05, FRM-EVT-06;
// form/Form.tsx:34-39,71-84,143-169, internals/form-context/FormContext.ts. The form side of the contract track 1 declared in
// Field/IFormScope.cs. The consumer's FormErrors is never modified: the keys fields clear are kept beside it and dropped
// when Form receives a new instance (Base UI copies the map on each clear, so a cleared key never comes back).
/// <summary>The form scope one <c>Form</c> cascades: its registry, its errors, its mode, its submissions.</summary>
internal sealed class FormScope : IFormScope
{
    private static readonly string[] s_none = [];

    private readonly ChangeListeners listeners = new();
    private readonly HashSet<string> cleared = new(StringComparer.Ordinal);
    private FormErrors? errors;
    private ValidationMode validationMode = ValidationMode.OnSubmit;
    private bool submitted;

    /// <inheritdoc />
    public FieldRegistry Registry { get; } = new();

    /// <inheritdoc />
    public ValidationMode ValidationMode => validationMode;

    /// <inheritdoc />
    public int SubmitCount { get; private set; }

    /// <inheritdoc />
    public string? FormToken { get; set; }

    // FFI-FOC-03: Form compares it to learn that the map changed through a field's clearErrors.
    /// <summary>Gets a number that changes whenever the current error map changes.</summary>
    /// <value>The version of the error map.</value>
    public int ErrorsVersion { get; private set; }

    // FRM-STA-02, FFI-EVT-04: replaced on an identity change, in OnParametersSet (ADR 05 §1).
    /// <summary>Replaces the error map when the instance differs from the current one.</summary>
    /// <param name="next">The errors parameter; <see langword="null"/> is the empty map.</param>
    /// <returns><see langword="true"/> when the map changed; otherwise, <see langword="false"/>.</returns>
    public bool SetErrors(FormErrors? next)
    {
        if (ReferenceEquals(next, errors))
        {
            return false;
        }

        errors = next;
        cleared.Clear();
        ErrorsVersion++;
        listeners.Notify();
        return true;
    }

    // FRM-API-01, FFI-API-02.
    /// <summary>Sets the form's validation mode.</summary>
    /// <param name="mode">The mode.</param>
    public void SetValidationMode(ValidationMode mode)
    {
        if (validationMode == mode)
        {
            return;
        }

        validationMode = mode;
        listeners.Notify();
    }

    // FRM-STA-03: before any field validates, whether or not the submission is blocked.
    /// <summary>Counts a submission.</summary>
    public void BeginSubmit() => SubmitCount++;

    // FRM-STA-04.
    /// <summary>Sets the submitted latch after a submission passed validation.</summary>
    public void SetSubmitted() => submitted = true;

    // FRM-FOC-02, FFI-FOC-03: Form consumes the latch on a change of the error map.
    /// <summary>Clears the submitted latch.</summary>
    /// <returns><see langword="true"/> when the latch was set; otherwise, <see langword="false"/>.</returns>
    public bool ConsumeSubmitted()
    {
        bool was = submitted;
        submitted = false;
        return was;
    }

    /// <inheritdoc />
    public bool TryGetErrors(string name, out IReadOnlyList<string> errors)
    {
        if (this.errors is not null && !cleared.Contains(name) && this.errors.TryGetValue(name, out FormErrorMessages? messages) && messages.Count > 0)
        {
            errors = messages;
            return true;
        }

        errors = s_none;
        return false;
    }

    // FRM-EVT-06, FFI-EVT-03: removes exactly that own key; several fields clearing in one update each succeed.
    /// <inheritdoc />
    public void ClearErrors(string? name)
    {
        if (string.IsNullOrEmpty(name) || errors is null || !errors.ContainsKey(name) || !cleared.Add(name))
        {
            return;
        }

        ErrorsVersion++;
        listeners.Notify();
    }

    /// <inheritdoc />
    public void AddListener(Action listener) => listeners.Add(listener);

    /// <inheritdoc />
    public void RemoveListener(Action listener) => listeners.Remove(listener);
}

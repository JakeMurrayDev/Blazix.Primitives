namespace Blazix.Primitives;

// FFI §2 (field scope), FFI-STR-01, FFI-STA-01..11, FFI-API-02/05, FFI-ALG-12 (registration), FFI-ALG-13 (focus ownership),
// FFI-ALG-14 (publish, through FieldValidation), FFI-FRM-17..21, FFI-FRM-25..27; field/root/FieldRoot.tsx,
// internals/field-register-control/useFieldControlRegistration.ts, internals/field-root-context/useSetFieldFocused.ts.
// Plain C# with one change notification (the M2 contexts design point: listener classes, not ComponentStore), cascaded
// fixed by FieldRoot (ADR 16 rule 9). A batch makes one operation notify once, so the field's parts render once.
// ADR 05 Amendments 2026-10-22: the external-error slot also holds the messages of a cascaded EditContext.
/// <summary>The field scope of one <see cref="FieldRoot"/>: its state, its labelable scope, its registration and its validation.</summary>
internal sealed class FieldRootContext : IDisposable
{
    private static readonly string[] s_none = [];
    private static readonly Func<FieldValidationArguments, ValueTask<FieldValidationResult?>> s_noValidator = static _ => ValueTask.FromResult<FieldValidationResult?>(null);

    private readonly ChangeListeners listeners = new();
    private readonly Func<Action, Task> invoke;
    private readonly Func<Exception, Task> dispatchException;
    private readonly Func<Task> validate;
    private readonly Action formChanged;
    private IFormScope form = DefaultFormScope.Instance;
    private string? name;
    private bool disabledParameter;
    private bool fieldsetDisabled;
    private bool invalidParameter;
    private bool? dirtyParameter;
    private bool? touchedParameter;
    private Func<FieldValidationArguments, ValueTask<FieldValidationResult?>>? validator;
    private ValidationMode? validationMode;
    private bool touchedState;
    private bool dirtyState;
    private bool filled;
    private bool focused;
    private object? focusOwner;
    private FieldValidityData validityData = FieldValidityData.Initial;
    private List<KeyValuePair<object, IReadOnlyList<string>>>? externalErrors;
    private object? activeSource;
    private FieldControlRegistration? registration;
    private bool initialValueCaptured;
    private bool registeredInvalid;
    private string? registeredEntryName;
    private int batchDepth;
    private bool pendingNotification;
    private bool disposed;

    /// <summary>Initializes a new instance of the <see cref="FieldRootContext"/> class.</summary>
    /// <param name="defaultControlId">The generated control id the field's labelable scope starts with.</param>
    /// <param name="timeProvider">The clock of the debounce.</param>
    /// <param name="invoke">Runs an action on the renderer's dispatcher; a timer callback goes through it.</param>
    /// <param name="dispatchException">Routes an exception of a validation nobody awaits to the field's error boundary.</param>
    public FieldRootContext(string defaultControlId, TimeProvider timeProvider, Func<Action, Task> invoke, Func<Exception, Task> dispatchException)
    {
        TimeProvider = timeProvider;
        this.invoke = invoke;
        this.dispatchException = dispatchException;
        validate = Validate;
        formChanged = OnFormChanged;
        Labelable = new LabelableScope(defaultControlId, parent: null, NotifyChanged);
        Validation = new FieldValidation(this);
    }

    private bool HasFormError => Name is { Length: > 0 } effective && form.TryGetErrors(effective, out _);

    /// <summary>Gets the labelable scope the field opens.</summary>
    /// <value>The scope of the field's label, control and descriptions.</value>
    public LabelableScope Labelable { get; }

    /// <summary>Gets the validation service.</summary>
    /// <value>The field's commits and change path.</value>
    public FieldValidation Validation { get; }

    /// <summary>Gets the clock of the debounce.</summary>
    /// <value>The time provider.</value>
    public TimeProvider TimeProvider { get; }

    /// <summary>Gets the enclosing form's scope.</summary>
    /// <value>The form scope, or the default scope outside a form.</value>
    public IFormScope Form => form;

    // FFI-STR-06, FLD-API-02.
    /// <summary>Gets a value indicating whether the field is disabled, by its own parameter or an enclosing fieldset.</summary>
    /// <value>The effective disabled state.</value>
    public bool Disabled => fieldsetDisabled || disabledParameter;

    // FFI-STA-10, FLD-API-01: `name ?? registeredFieldName`, the control's name forgotten when it unregisters.
    /// <summary>Gets the field's effective name.</summary>
    /// <value>The field's name parameter, else the active control's name, or <see langword="null"/>.</value>
    public string? Name => name ?? registration?.Name;

    // FFI-STA-02, FFI-API-04/05: only a true parameter acts; a form error and an EditContext message make the field invalid.
    /// <summary>Gets a value indicating whether the field is invalid for a reason outside its own validation.</summary>
    /// <value>The invalid parameter, a form error for the field's name, or a message of the external-error slot.</value>
    public bool Invalid => invalidParameter || HasFormError || externalErrors is { Count: > 0 };

    /// <summary>Gets the form's error messages for the field's effective name.</summary>
    /// <value>The messages, empty when the form has none for the field.</value>
    public IReadOnlyList<string> FormErrors => Name is { Length: > 0 } effective && form.TryGetErrors(effective, out IReadOnlyList<string> errors) ? errors : s_none;

    /// <summary>Gets the messages of the external-error slot, such as a cascaded EditContext's messages for the control.</summary>
    /// <value>The messages in the order their sources reported them.</value>
    public IReadOnlyList<string> ExternalErrors
    {
        get
        {
            if (externalErrors is null || externalErrors.Count == 0)
            {
                return s_none;
            }

            if (externalErrors.Count == 1)
            {
                return externalErrors[0].Value;
            }

            List<string> all = [];
            foreach (KeyValuePair<object, IReadOnlyList<string>> entry in externalErrors)
            {
                all.AddRange(entry.Value);
            }

            return all;
        }
    }

    // FFI-API-02.
    /// <summary>Gets the field's effective validation mode.</summary>
    /// <value>The field's mode, else the form's, else <see cref="ValidationMode.OnSubmit"/>.</value>
    public ValidationMode ValidationMode => validationMode ?? form.ValidationMode;

    /// <summary>Gets the debounce of change-triggered commits.</summary>
    /// <value>The delay in milliseconds; 0 commits at once.</value>
    public double DebounceMilliseconds { get; private set; }

    /// <summary>Gets the validator.</summary>
    /// <value>The latest validator the field received, or one that always answers valid.</value>
    public Func<FieldValidationArguments, ValueTask<FieldValidationResult?>> Validator => validator ?? s_noValidator;

    /// <summary>Gets the validity the field last published.</summary>
    /// <value>The validity data.</value>
    public FieldValidityData ValidityData => validityData;

    // FFI-STA-06: the dirty latch.
    /// <summary>Gets a value indicating whether the field was ever marked dirty or validated imperatively.</summary>
    /// <value><see langword="true"/> once a required value that is missing counts as invalid.</value>
    public bool MarkedDirty { get; private set; }

    // FFI-STA-01/02, FLD-STA-09/10.
    /// <summary>Gets the field's state as its parts read it.</summary>
    /// <value>The snapshot of the six state fields.</value>
    public FieldStateSnapshot State => new(
        Disabled,
        touchedParameter ?? touchedState,
        dirtyParameter ?? dirtyState,
        Invalid ? false : Disabled ? null : validityData.State.Valid,
        filled,
        focused);

    /// <summary>Gets the active control's registration.</summary>
    /// <value>The registration, or <see langword="null"/> while no control is registered.</value>
    public FieldControlRegistration? Registration => registration;

    /// <inheritdoc />
    /// <remarks>Removes the field's registry entry, cancels a pending debounce and stops listening to the form.</remarks>
    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        Validation.Dispose();
        form.RemoveListener(formChanged);
        if (registration?.Id is { } id)
        {
            form.Registry.Remove(id);
        }

        listeners.Clear();
    }

    /// <summary>Applies the field root's parameters.</summary>
    /// <param name="fieldName">The name parameter.</param>
    /// <param name="disabled">The disabled parameter.</param>
    /// <param name="invalid">The invalid parameter.</param>
    /// <param name="dirty">The dirty parameter, or <see langword="null"/> when the field owns the flag.</param>
    /// <param name="touched">The touched parameter, or <see langword="null"/> when the field owns the flag.</param>
    /// <param name="validateParameter">The validator, or <see langword="null"/>.</param>
    /// <param name="mode">The validation mode parameter, or <see langword="null"/> for the form's.</param>
    /// <param name="debounceMilliseconds">The debounce in milliseconds.</param>
    public void SetParameters(
        string? fieldName,
        bool disabled,
        bool invalid,
        bool? dirty,
        bool? touched,
        Func<FieldValidationArguments, ValueTask<FieldValidationResult?>>? validateParameter,
        ValidationMode? mode,
        double debounceMilliseconds)
    {
        using BatchScope batch = Batch();
        FieldStateSnapshot before = State;
        string? nameBefore = Name;
        name = fieldName;
        disabledParameter = disabled;
        invalidParameter = invalid;
        dirtyParameter = dirty;
        touchedParameter = touched;
        validator = validateParameter;
        validationMode = mode;
        DebounceMilliseconds = debounceMilliseconds;

        // FFI-STA-06, FLD-STA-10: the latch follows a defined dirty parameter.
        if (dirty is bool latch)
        {
            MarkedDirty = latch;
        }

        if (!string.Equals(nameBefore, Name, StringComparison.Ordinal) || before != State)
        {
            AfterExternalChange();
        }
    }

    /// <summary>Sets the enclosing fieldset's disabled state.</summary>
    /// <param name="disabled">Whether the nearest fieldset is disabled.</param>
    public void SetFieldsetDisabled(bool disabled)
    {
        if (fieldsetDisabled == disabled)
        {
            return;
        }

        fieldsetDisabled = disabled;
        NotifyChanged();
    }

    /// <summary>Sets the enclosing form's scope.</summary>
    /// <param name="scope">The form scope, or <see langword="null"/> outside a form.</param>
    public void SetForm(IFormScope? scope)
    {
        IFormScope next = scope ?? DefaultFormScope.Instance;
        if (ReferenceEquals(next, form))
        {
            return;
        }

        form.RemoveListener(formChanged);
        form = next;
        form.AddListener(formChanged);
        AfterExternalChange();
    }

    /// <summary>Starts a batch: the change notifications inside it run once, when the outermost batch ends.</summary>
    /// <returns>The scope that ends the batch when disposed.</returns>
    public BatchScope Batch()
    {
        batchDepth++;
        return new BatchScope(this);
    }

    /// <summary>Adds a listener that runs after the field's state, validity, name or labelable scope changed.</summary>
    /// <param name="listener">The listener.</param>
    public void AddListener(Action listener) => listeners.Add(listener);

    /// <summary>Removes a listener added with <see cref="AddListener"/>.</summary>
    /// <param name="listener">The listener.</param>
    public void RemoveListener(Action listener) => listeners.Remove(listener);

    // FFI-FRM-01.
    /// <summary>Determines whether a change of the value validates in full.</summary>
    /// <returns><see langword="true"/> in <see cref="ValidationMode.OnChange"/>, and in <see cref="ValidationMode.OnSubmit"/> after a submission.</returns>
    public bool ShouldValidateOnChange() => ValidationMode == ValidationMode.OnChange || (ValidationMode == ValidationMode.OnSubmit && form.SubmitCount > 0);

    // FFI-STA-07, FFI-API-05: a no-op while the parameter is defined.
    /// <summary>Marks the field touched.</summary>
    /// <param name="value">The touched flag.</param>
    public void SetTouched(bool value)
    {
        if (touchedParameter is not null || touchedState == value)
        {
            return;
        }

        touchedState = value;
        NotifyChanged();
    }

    // FFI-STA-05/06.
    /// <summary>Sets whether the value differs from the initial value; <see langword="true"/> also sets the dirty latch.</summary>
    /// <param name="value">The dirty flag.</param>
    public void SetDirty(bool value)
    {
        if (dirtyParameter is not null)
        {
            return;
        }

        if (value)
        {
            MarkedDirty = true;
        }

        if (dirtyState == value)
        {
            return;
        }

        dirtyState = value;
        NotifyChanged();
    }

    // FFI-STA-08.
    /// <summary>Sets whether the control has a value.</summary>
    /// <param name="value">The filled flag.</param>
    public void SetFilled(bool value)
    {
        if (filled == value)
        {
            return;
        }

        filled = value;
        NotifyChanged();
    }

    // FFI-ALG-13: a control acquires the flag only while enabled, and only its owner releases it.
    /// <summary>Acquires or releases the field's focused flag for a control.</summary>
    /// <param name="owner">The control, whose identity is the owner token.</param>
    /// <param name="value"><see langword="true"/> to acquire the flag; <see langword="false"/> to release it.</param>
    /// <param name="ownerDisabled">Whether the control is disabled, which keeps it from acquiring the flag.</param>
    public void SetFocused(object owner, bool value, bool ownerDisabled)
    {
        if (value ? ownerDisabled : !ReferenceEquals(focusOwner, owner))
        {
            return;
        }

        focusOwner = value ? owner : null;
        if (focused == value)
        {
            return;
        }

        focused = value;
        NotifyChanged();
    }

    // FFI-EVT-03 through the form scope, with the field's effective name.
    /// <summary>Removes the form's error for the field's name.</summary>
    public void ClearErrors() => form.ClearErrors(Name);

    // ADR 05 Amendments 2026-10-22: a cascaded EditContext's messages for the control, raised and cleared as its validator reports.
    /// <summary>Sets the messages one source puts in the external-error slot.</summary>
    /// <param name="source">The source, such as a control's EditContext participation.</param>
    /// <param name="messages">The messages, or <see langword="null"/> or empty to remove the source's messages.</param>
    public void SetExternalErrors(object source, IReadOnlyList<string>? messages)
    {
        int index = -1;
        if (externalErrors is not null)
        {
            for (int i = 0; i < externalErrors.Count; i++)
            {
                if (ReferenceEquals(externalErrors[i].Key, source))
                {
                    index = i;
                    break;
                }
            }
        }

        bool empty = messages is null || messages.Count == 0;
        if (empty)
        {
            if (index < 0)
            {
                return;
            }

            externalErrors!.RemoveAt(index);
        }
        else if (index >= 0)
        {
            if (externalErrors![index].Value.SequenceEqual(messages!))
            {
                return;
            }

            externalErrors[index] = new KeyValuePair<object, IReadOnlyList<string>>(source, [.. messages!]);
        }
        else
        {
            (externalErrors ??= []).Add(new KeyValuePair<object, IReadOnlyList<string>>(source, [.. messages!]));
        }

        AfterExternalChange();
    }

    // FFI-ALG-12, field side; useFieldControlRegistration.ts:133-169.
    /// <summary>Registers, updates or removes a control as the field's active control.</summary>
    /// <param name="source">The control, whose identity tells a re-registration from a replacement.</param>
    /// <param name="next">The registration, or <see langword="null"/> to unregister.</param>
    public void RegisterControl(object source, FieldControlRegistration? next)
    {
        using BatchScope batch = Batch();
        if (next is null)
        {
            if (!ReferenceEquals(activeSource, source))
            {
                return;
            }

            activeSource = null;
            Validation.Change(null, cancelPending: true);
            if (registration?.Id is { } removed)
            {
                form.Registry.Remove(removed);
            }

            registration = null;
            NotifyChanged();
            return;
        }

        if (activeSource is not null && !ReferenceEquals(activeSource, source))
        {
            Validation.Change(null, cancelPending: true);
        }

        string? previousId = registration?.Id;
        string? previousName = Name;
        activeSource = source;
        registration = next;
        if (previousId is not null && !string.Equals(previousId, next.Id, StringComparison.Ordinal))
        {
            form.Registry.Remove(previousId);
        }

        CaptureInitialValue(next);
        Refresh();
        if (!string.Equals(previousName, Name, StringComparison.Ordinal))
        {
            NotifyChanged();
        }
    }

    // FFI-FRM-21, FFI-ALG-12 `validate()`; FLD-API-08 by @ref (ADR 08 Amendments 2026-10-22).
    /// <summary>Validates the field imperatively: sets the dirty latch and commits the active control's value.</summary>
    /// <returns>A task that completes when the commit, including an asynchronous validator, finished; its synchronous part has updated the registry when the method returns.</returns>
    public Task Validate()
    {
        MarkedDirty = true;
        return Validation.CommitAsync(registration is null ? validityData.Value : registration.Value ?? GetValueForForm());
    }

    /// <summary>Brings the active control up to date from the browser, then validates the field imperatively.</summary>
    /// <returns>A task that completes when the commit finished.</returns>
    public async Task ValidateAsync()
    {
        if (registration?.RefreshAsync is { } refresh)
        {
            await refresh().ConfigureAwait(true);
        }

        await Validate().ConfigureAwait(true);
    }

    /// <summary>Runs an action on the renderer's dispatcher.</summary>
    /// <param name="action">The action.</param>
    public void Invoke(Action action) => Observe(invoke(action));

    /// <summary>Routes the exception of a task nobody awaits to the field's error boundary.</summary>
    /// <param name="task">The task.</param>
    public void Observe(Task task)
    {
        if (task.IsCompletedSuccessfully)
        {
            return;
        }

        _ = ObserveAsync(task);
    }

    /// <summary>Runs the change notification, or defers it to the end of the current batch.</summary>
    public void NotifyChanged()
    {
        if (batchDepth > 0)
        {
            pendingNotification = true;
            return;
        }

        pendingNotification = false;
        listeners.Notify();
    }

    // FFI-ALG-14 `publish`: the registry first, with the external invalid state folded in, then the field's own data.
    /// <summary>Publishes a validation result.</summary>
    /// <param name="data">The validity data.</param>
    /// <param name="externalInvalid">The external invalid state the registry entry folds in for this pass.</param>
    internal void PublishValidity(FieldValidityData data, bool externalInvalid)
    {
        string? id = registration?.Id ?? Labelable.ControlId;
        if (id is not null && form.Registry.Contains(id))
        {
            form.Registry.SetValidity(id, data.Combined(externalInvalid));
        }

        validityData = data;
        NotifyChanged();
    }

    private void CaptureInitialValue(FieldControlRegistration next)
    {
        // FFI-STA-04: once per field.
        if (initialValueCaptured)
        {
            return;
        }

        initialValueCaptured = true;
        object? initialValue = next.Value ?? GetValueForForm();
        if (!Equals(initialValue, validityData.InitialValue))
        {
            validityData = validityData.WithInitialValue(initialValue);
            NotifyChanged();
        }
    }

    private object? GetValueForForm() => registration is null ? null : registration.GetValue is { } getValue ? getValue() : registration.Value;

    // FFI-ALG-12 `refresh()`: replacing the entry keeps its position in the registry.
    private void Refresh()
    {
        if (registration?.Id is not { } id)
        {
            return;
        }

        registeredInvalid = Invalid;
        registeredEntryName = name ?? registration.Name;
        form.Registry.Set(id, new FieldRegistryEntry(registeredEntryName, GetValueForForm, validityData.Combined(registeredInvalid), validate, registration.ControlId, registration.ApplyState, registration.ResolveControlId));
    }

    private void OnFormChanged() => AfterExternalChange();

    // A change of the name, the invalid state or the form's errors refreshes the registry entry and notifies the parts.
    private void AfterExternalChange()
    {
        if (registration?.Id is not null && (registeredInvalid != Invalid || !string.Equals(registeredEntryName, name ?? registration.Name, StringComparison.Ordinal)))
        {
            Refresh();
        }

        NotifyChanged();
    }

    private void EndBatch()
    {
        if (--batchDepth == 0 && pendingNotification)
        {
            NotifyChanged();
        }
    }

    private async Task ObserveAsync(Task task)
    {
        try
        {
            await task.ConfigureAwait(true);
        }
        catch (Exception exception) when (!task.IsCanceled)
        {
            await dispatchException(exception).ConfigureAwait(true);
        }
    }

    /// <summary>Ends a batch of the field's change notifications when disposed.</summary>
    internal readonly struct BatchScope(FieldRootContext owner) : IDisposable
    {
        /// <inheritdoc />
        public void Dispose() => owner.EndBatch();
    }
}

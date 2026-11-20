using Blazix.Primitives.Interop;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Blazix.Primitives;

// OTP §8, FFI §6.3 row `OTPField.Root` (field-and-form-infrastructure.md:804,822,838); ADR 05 Amendments 2026-11-18 item 2
// (decision 14 (a), chosen by the user 2026-11-18): the family's one field behaviour, the SelectField pattern, composed from
// Field/ (never edited):
// - The control id (OTP-API-18, FFI-ALG-07): slot 0's id, claimed in the field's labelable scope (useLabelableId in
//   OTPFieldRoot.tsx:124); it is also the registration's id and control (useRegisterFieldControl, OTPFieldRoot.tsx:163).
// - Registration (FFI-ALG-12, OTP-FRM-06): the normalized value, re-registered when it changes; none while disabled.
// - The hidden input (OTP-DOM-20..26, OTP-FRM-01..16): the `form` module's control role with ReportEvents = false,
//   ReportAutofill = true, Controlled = true and the value, so the module owns its `value` from attach (C# → JS) and restores
//   it after an autofill the root does not take (OTP-FRM-13); FocusTarget hands its focus to slot 0 (OTP-FOC-06); it is the
//   field's validation input (FFI §6 Table A).
// - The value reaction (OTP-FRM-07, OTP-FRM-08; useValueChanged in OTPFieldRoot.tsx:201-205): filled after every render,
//   and after the render that shows a new value the form's error cleared, dirty, the EditContext notified and the change
//   validated once the hidden input reported again.
// - Focus ownership (OTP-FOC-07, OTP-FOC-09; useSetFieldFocused per slot, FFI-ALG-13): the slot that last set the flag owns
//   it; touched and the onBlur commit when focus leaves the root (OTP-FRM-09, OTP-FRM-10).
/// <summary>The field half of a one-time code field: its control registration, its hidden input and its touched and focused flags.</summary>
internal sealed class OtpFieldField : IDisposable
{
    private const string SyncReport = "sync";

    private readonly IJSRuntime jsRuntime;
    private readonly Action changed;
    private readonly Func<Exception, Task> dispatchException;
    private readonly FieldValidationInput validationInput;
    private readonly EditContextParticipation participation;
    private readonly Func<Task> refresh;
    private readonly Action fieldChanged;
    private readonly Action<FormControlReport> reported;
    private readonly Action<FormAutofillReport> autofilled;
    private readonly IFormScope formless;
    private readonly TaskCompletionSource attachedSource = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private FieldRootContext? fieldScope;
    private IFormScope form;
    private LabelableScope? scope;
    private string? name;
    private bool ownDisabled;
    private bool claimRegistered;
    private FieldControlRegistration? sentRegistration;
    private bool registered;
    private FormTwin? twin;
    private TwinHandle<FormOptions>? inputHandle;
    private FormOptions? sentOptions;
    private string serialized = string.Empty;
    private int valueRevision;
    private int revision;
    private int refreshRevision;
    private TaskCompletionSource? refreshSource;
    private string? focusTarget;
    private bool observed;
    private int observedVersion;
    private bool disposed;

    /// <summary>Occurs when the hidden input reports an autofill.</summary>
    public event Action<FormAutofillReport>? Autofilled;

    /// <summary>Initializes a new instance of the <see cref="OtpFieldField"/> class.</summary>
    /// <param name="formless">The form scope of the runtime's fields outside a form.</param>
    /// <param name="jsRuntime">The runtime the twin imports the form module through.</param>
    /// <param name="changed">Called when the field's state that the parts render changed.</param>
    /// <param name="invoke">Runs an action on the renderer's dispatcher.</param>
    /// <param name="dispatchException">Routes an exception to the root's error boundary.</param>
    public OtpFieldField(IFormScope formless, IJSRuntime jsRuntime, Action changed, Func<Action, Task> invoke, Func<Exception, Task> dispatchException)
    {
        this.formless = formless;
        form = formless;
        this.jsRuntime = jsRuntime;
        this.changed = changed;
        this.dispatchException = dispatchException;
        validationInput = new FieldValidationInput(_ => SendOptions());
        participation = new EditContextParticipation(OnEditContextMessages);
        refresh = RefreshAsync;
        fieldChanged = OnFieldChanged;
        reported = report => _ = invoke(() => HandleReport(report));
        autofilled = report => _ = invoke(() => Autofilled?.Invoke(report));
    }

    /// <summary>Gets a value indicating whether the field is inside a <see cref="FieldRoot"/>.</summary>
    public bool InField => fieldScope is not null;

    /// <summary>Gets a value indicating whether the field is disabled, its own flag or its field's.</summary>
    public bool Disabled => (fieldScope?.Disabled ?? false) || ownDisabled;

    /// <summary>Gets the effective form name: the field's, else the root's own, else the edit context's.</summary>
    public string? Name => fieldScope?.Name ?? name ?? participation.FieldName;

    /// <summary>Gets the field's state with the root's disabled flag, or the default state outside a field.</summary>
    public FieldStateSnapshot Snapshot => (fieldScope?.State ?? FieldStateSnapshot.Default) with { Disabled = Disabled };

    // OTP-ARIA-03: the label of the labelable scope.
    /// <summary>Gets the id of the field's label, or <see langword="null"/>.</summary>
    public string? FieldLabelId => scope?.Label.Id;

    // OTP-ARIA-09, §16 item 3 (replicated): the slots' aria-invalid follows the field's app-controlled flag only.
    /// <summary>Gets a value indicating whether the slots render <c>aria-invalid</c>.</summary>
    public bool SlotsInvalid => fieldScope is not null && fieldScope.Invalid && !Disabled;

    // OTP-ARIA-11 (FFI-ARIA-03): the hidden input's aria-invalid follows the field's computed validity.
    /// <summary>Gets a value indicating whether the hidden input renders <c>aria-invalid</c>.</summary>
    public bool HiddenInvalid => fieldScope is not null && fieldScope.State.Valid == false && !fieldScope.Disabled && !Disabled;

    /// <summary>Gets the field's validation mode, or <see cref="ValidationMode.OnSubmit"/> outside a field.</summary>
    public ValidationMode ValidationMode => fieldScope?.ValidationMode ?? ValidationMode.OnSubmit;

    /// <summary>Gets a task that completes when the hidden input's handle is attached.</summary>
    public Task TwinAttached => attachedSource.Task;

    /// <inheritdoc />
    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        Unmount();
        participation.Dispose();
        fieldScope?.RemoveListener(fieldChanged);
        if (twin is not null)
        {
            twin.ControlReported -= reported;
            twin.Autofilled -= autofilled;
            _ = twin.DisposeAsync().AsTask();
            twin = null;
            inputHandle = null;
        }
    }

    /// <summary>Takes the cascaded scopes; called once, when the root initializes.</summary>
    /// <param name="fieldContext">The nearest field, or <see langword="null"/>.</param>
    public void Initialize(FieldRootContext? fieldContext)
    {
        fieldScope = fieldContext;
        scope = fieldScope?.Labelable;
        fieldScope?.AddListener(fieldChanged);
    }

    /// <summary>Applies the root's parameters; called on every parameter set.</summary>
    /// <typeparam name="TField">The type of the bound value.</typeparam>
    /// <param name="ownName">The root's <c>Name</c>.</param>
    /// <param name="disabled">The root's own <c>Disabled</c>.</param>
    /// <param name="formScope">The cascaded form scope, or <see langword="null"/>.</param>
    /// <param name="editContext">The cascaded edit context, or <see langword="null"/>.</param>
    /// <param name="expression">The live triple's expression, or <see langword="null"/>.</param>
    public void SetParameters<TField>(string? ownName, bool disabled, IFormScope? formScope, Microsoft.AspNetCore.Components.Forms.EditContext? editContext, System.Linq.Expressions.Expression<Func<TField>>? expression)
    {
        name = ownName;
        ownDisabled = disabled;
        form = fieldScope?.Form ?? formScope ?? formless;
        participation.Update(editContext, expression);
    }

    // OTP-ARIA-05, OTP-EDGE-13, FFI-ARIA-01: the consumer's ids, then the field's message ids.
    /// <summary>Composes an element's <c>aria-describedby</c>.</summary>
    /// <param name="consumer">The consumer's <c>aria-describedby</c>, or <see langword="null"/>.</param>
    /// <returns>The composed ids, or <see langword="null"/>.</returns>
    public string? DescribedBy(string? consumer) => scope is null ? consumer : scope.DescribedBy(consumer);

    // OTP-FOC-07, OTP-FOC-09 (useSetFieldFocused per slot): the slot that receives focus owns the field's flag.
    /// <summary>Acquires the field's focused flag for a slot.</summary>
    /// <param name="owner">The slot.</param>
    public void HandleFocus(object owner) => fieldScope?.SetFocused(owner, true, Disabled);

    // OTP-FOC-09: a slot that leaves, or a field that becomes disabled, releases the flag it owns.
    /// <summary>Releases the field's focused flag a slot owns.</summary>
    /// <param name="owner">The slot.</param>
    public void ReleaseFocus(object owner) => fieldScope?.SetFocused(owner, false, Disabled);

    // OTP-FOC-07, OTP-FRM-09, OTP-FRM-10 (OTPFieldRoot.tsx:286-298): focus left the root.
    /// <summary>Applies a blur that left the field: touched, the focused flag released, and the onBlur commit.</summary>
    /// <param name="owner">The slot that lost focus.</param>
    /// <param name="value">The value an onBlur commit validates.</param>
    /// <returns>A task that completes when an onBlur commit has run.</returns>
    public Task HandleLeaveAsync(object owner, string value)
    {
        if (fieldScope is null)
        {
            return Task.CompletedTask;
        }

        using (fieldScope.Batch())
        {
            fieldScope.SetTouched(true);
            fieldScope.SetFocused(owner, false, Disabled);
        }

        return fieldScope.ValidationMode == ValidationMode.OnBlur ? CommitAfterRefreshAsync(value) : Task.CompletedTask;
    }

    /// <summary>Runs the work after a render: the control-id claim, the registration, the value reaction and the hidden input.</summary>
    /// <param name="controlId">Slot 0's id.</param>
    /// <param name="input">The hidden input, or <see langword="null"/> while the length is not a positive integer.</param>
    /// <param name="value">The normalized value.</param>
    /// <param name="valueVersion">The root's value counter.</param>
    /// <returns>A task that completes when the module has the options.</returns>
    public async Task AfterRenderAsync(string controlId, ElementReference? input, string value, int valueVersion)
    {
        focusTarget = controlId;
        serialized = value;
        if (fieldScope is not null)
        {
            validationInput.ControlId = controlId;
            fieldScope.Validation.InputRef = validationInput;
            using (fieldScope.Batch())
            {
                ClaimControlId(controlId);
                Register(controlId, value);
                ObserveValue(valueVersion, value);
            }
        }
        else
        {
            ObserveValue(valueVersion, value);
        }

        await AttachOrUpdateAsync(input).ConfigureAwait(true);
    }

    // OTP-FRM-13: an autofill the root takes moves the committed value, which the module shows from this revision on.
    /// <summary>Sends the hidden input's committed value now, before the render batch.</summary>
    /// <param name="value">The committed value.</param>
    public void SendValue(string value)
    {
        if (!string.Equals(serialized, value, StringComparison.Ordinal))
        {
            serialized = value;
            valueRevision++;
            SendOptions();
        }
    }

    private async Task AttachOrUpdateAsync(ElementReference? input)
    {
        if (input is not { } element)
        {
            return;
        }

        if (twin is null)
        {
            twin = new FormTwin(jsRuntime);
            twin.ControlReported += reported;
            twin.Autofilled += autofilled;
        }

        if (inputHandle is null)
        {
            FormOptions options = Options();
            sentOptions = options;
            inputHandle = await twin.AttachAsync(element, options).ConfigureAwait(true);
            attachedSource.TrySetResult();
            if (fieldScope is not null)
            {
                Observe(RefreshAsync());
            }
        }
        else if (OptionsChanged())
        {
            FormOptions options = Options();
            sentOptions = options;
            await inputHandle.UpdateAsync(options).ConfigureAwait(true);
        }
    }

    private FormOptions Options() => new()
    {
        Control = new FormControlOptions
        {
            FormId = form.FormToken,
            CustomValidity = validationInput.OwnMessage,
            Controlled = true,
            Value = serialized,
            Revision = revision + valueRevision,
            Disabled = Disabled,
            ReportEvents = false,
            ReportAutofill = true,
            FocusTarget = focusTarget,
        },
    };

    // Compared before constructing, so an unchanged render allocates no options record.
    private bool OptionsChanged()
    {
        if (sentOptions?.Control is not { } sent)
        {
            return true;
        }

        return sent.Revision != revision + valueRevision
            || sent.Disabled != Disabled
            || !string.Equals(sent.Value, serialized, StringComparison.Ordinal)
            || !string.Equals(sent.CustomValidity, validationInput.OwnMessage, StringComparison.Ordinal)
            || !string.Equals(sent.FormId, form.FormToken, StringComparison.Ordinal)
            || !string.Equals(sent.FocusTarget, focusTarget, StringComparison.Ordinal);
    }

    private void SendOptions()
    {
        if (inputHandle is null || !OptionsChanged())
        {
            return;
        }

        FormOptions options = Options();
        sentOptions = options;
        Observe(inputHandle.UpdateAsync(options).AsTask());
    }

    // A commit reads the hidden input's native state from a fresh report (the CheckableFieldBehavior refresh).
    private Task RefreshAsync()
    {
        if (inputHandle is null)
        {
            return Task.CompletedTask;
        }

        refreshSource ??= new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        revision++;
        refreshRevision = revision + valueRevision;
        SendOptions();
        return refreshSource.Task;
    }

    private void HandleReport(FormControlReport report)
    {
        if (disposed || !string.Equals(report.Type, SyncReport, StringComparison.Ordinal))
        {
            return;
        }

        if (report.Validity is { } validity)
        {
            validationInput.Report(validity);
        }

        if (refreshSource is { } source && report.Revision >= refreshRevision)
        {
            refreshSource = null;
            source.TrySetResult();
        }
    }

    // FFI-ALG-07: slot 0's id is the scope's control id.
    private void ClaimControlId(string controlId)
    {
        if (scope is null)
        {
            return;
        }

        claimRegistered = true;
        scope.RegisterControlId(this, controlId);
    }

    private void ReleaseClaim()
    {
        if (claimRegistered)
        {
            claimRegistered = false;
            scope!.UnregisterControlId(this);
        }
    }

    private void Register(string controlId, string value)
    {
        if (Disabled)
        {
            Unregister();
            return;
        }

        FieldControlRegistration next = new(controlId, name ?? participation.FieldName, value, () => value, controlId, refresh);
        if (registered && next == sentRegistration && string.Equals(value, sentRegistration!.Value as string, StringComparison.Ordinal))
        {
            return;
        }

        registered = true;
        sentRegistration = next;
        fieldScope!.RegisterControl(this, next);
    }

    private void Unregister()
    {
        if (registered)
        {
            registered = false;
            sentRegistration = null;
            fieldScope!.RegisterControl(this, null);
        }
    }

    // OTP-FRM-08 (filled after every render, OTPFieldRoot.tsx:150-152), OTP-FRM-07 (the rest on a change only).
    private void ObserveValue(int valueVersion, string value)
    {
        fieldScope?.SetFilled(value.Length > 0);
        if (!observed)
        {
            observed = true;
            observedVersion = valueVersion;
            return;
        }

        if (observedVersion == valueVersion)
        {
            return;
        }

        observedVersion = valueVersion;
        if (fieldScope is null)
        {
            // FFI-FRM-26 through the form scope, for a field outside a FieldRoot inside a form.
            form.ClearErrors(Name);
            participation.NotifyChanged();
            return;
        }

        fieldScope.ClearErrors();
        fieldScope.SetDirty(!string.Equals(value, fieldScope.ValidityData.InitialValue as string, StringComparison.Ordinal));
        participation.NotifyChanged();
        fieldScope.Observe(ChangeAfterRefreshAsync(valueVersion, value));
    }

    private async Task ChangeAfterRefreshAsync(int valueVersion, string value)
    {
        await RefreshAsync().ConfigureAwait(true);
        if (fieldScope is not null && !disposed && observedVersion == valueVersion)
        {
            fieldScope.Validation.Change(value);
        }
    }

    private async Task CommitAfterRefreshAsync(string value)
    {
        await RefreshAsync().ConfigureAwait(true);
        if (fieldScope is not null && !disposed)
        {
            await fieldScope.Validation.CommitAsync(value).ConfigureAwait(true);
        }
    }

    private void OnEditContextMessages(IReadOnlyList<string> messages) => fieldScope?.SetExternalErrors(participation, messages);

    private void OnFieldChanged() => changed();

    private void Unmount()
    {
        if (fieldScope is null)
        {
            return;
        }

        using (fieldScope.Batch())
        {
            Unregister();
            ReleaseClaim();
            if (ReferenceEquals(fieldScope.Validation.InputRef, validationInput))
            {
                fieldScope.Validation.InputRef = null;
            }
        }
    }

    private void Observe(Task task)
    {
        if (task.IsCompletedSuccessfully)
        {
            return;
        }

        _ = ObserveAsync(task);
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
}

using Blazix.Primitives.Interop;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Blazix.Primitives;

// NUM §8, FFI §6.3 row `NumberField.Root + NumberField.Input` (field-and-form-infrastructure.md:803,821,837); ADR 05
// Amendments 2026-11-18 item 2 (decision 14 (a), chosen by the user 2026-11-18): the family's one field behaviour, the
// SelectField pattern, composed from Field/ (never edited):
// - The control id (NUM-API-18, NUM-DOM-09, FFI-ALG-07): the visible input's id, claimed in the field's labelable scope by
//   the root (useLabelableId in NumberFieldRoot.tsx:114); the input is also the registration's id and control.
// - Registration (FFI-ALG-12, NUM-FRM-06): while a NumberFieldInput is mounted and the field is enabled, the numeric value
//   boxed once per commit by the root (the RadioGroup precedent), re-registered on a change.
// - The hidden input (NUM-FRM-01, NUM-DOM-06): the `form` module's control role (design point 6, the first option) with
//   ReportEvents = false, Controlled = true and the serialized value, so the module owns its `value` from attach (C# → JS);
//   FocusTarget hands its focus to the visible input (NUM-FOC-01); it is the field's validation input. The autofill
//   `change` is the `number-field` module's (6b), so ReportAutofill stays off.
// - The value reaction (NUM-STA-13, NUM-FRM-08; useValueChanged in NumberFieldInput.tsx:103-112): filled after every
//   render, and after the render that shows a new value the form's error cleared, the EditContext notified and the change
//   validated once the input reported again, except right after a blur that changed the value (the skip flag). Dirty is set
//   by the root inside its request (NUM-STA-14), through SetDirty.
// - Touched, focused and the onBlur commit (NUM-FRM-07, NUM-FRM-09): from the visible input's focus and blur.
/// <summary>The field half of a number field: its control registration, its hidden input and its touched and focused flags.</summary>
internal sealed class NumberFieldField : IDisposable
{
    private const string SyncReport = "sync";

    
    private readonly IJSRuntime jsRuntime;
    private readonly Action changed;
    private readonly Func<Action, Task> invoke;
    private readonly Func<Exception, Task> dispatchException;
    private readonly FieldValidationInput validationInput;
    private readonly EditContextParticipation participation;
    private readonly Func<Task> refresh;
    private readonly Action fieldChanged;
    private readonly Action<FormControlReport> reported;
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
    private bool previousDisabled;
    private bool initialized;
    private FormTwin? twin;
    private TwinHandle<FormOptions>? inputHandle;
    private FormOptions? sentOptions;
    private string? serialized;
    private int revision;
    private int refreshRevision;
    private TaskCompletionSource? refreshSource;
    private string? focusTarget;
    private bool observed;
    private int observedVersion;
    private bool blockRevalidation;
    private bool disposed;

    /// <summary>Initializes a new instance of the <see cref="NumberFieldField"/> class.</summary>
    /// <param name="formless">The form scope of the runtime's fields outside a form.</param>
    /// <param name="jsRuntime">The runtime the twin imports the form module through.</param>
    /// <param name="changed">Called when the field's state that the parts render changed.</param>
    /// <param name="invoke">Runs an action on the renderer's dispatcher.</param>
    /// <param name="dispatchException">Routes an exception to the root's error boundary.</param>
    public NumberFieldField(IFormScope formless, IJSRuntime jsRuntime, Action changed, Func<Action, Task> invoke, Func<Exception, Task> dispatchException)
    {
        this.formless = formless;
        form = formless;
        this.jsRuntime = jsRuntime;
        this.changed = changed;
        this.invoke = invoke;
        this.dispatchException = dispatchException;
        validationInput = new FieldValidationInput(_ => SendOptions());
        participation = new EditContextParticipation(OnEditContextMessages);
        refresh = RefreshAsync;
        fieldChanged = OnFieldChanged;
        reported = report => _ = invoke(() => HandleReport(report));
    }

    /// <summary>Gets a value indicating whether the number field is inside a field.</summary>
    public bool InField => fieldScope is not null;

    /// <summary>Gets a value indicating whether the number field is disabled, its own flag or its field's.</summary>
    public bool Disabled => (fieldScope?.Disabled ?? false) || ownDisabled;

    /// <summary>Gets the effective form name: the field's, else the root's own, else the edit context's.</summary>
    public string? Name => fieldScope?.Name ?? name ?? participation.FieldName;

    /// <summary>Gets the field's state with the number field's disabled flag, or the default state outside a field.</summary>
    public FieldStateSnapshot Snapshot => (fieldScope?.State ?? FieldStateSnapshot.Default) with { Disabled = Disabled };

    // NUM-ARIA-03: the label of the labelable scope; no native-label fallback.
    /// <summary>Gets the id of the field's label, or <see langword="null"/>.</summary>
    public string? FieldLabelId => scope?.Label.Id;

    // NUM-ARIA-02, §16 item 11: one source for both inputs, the field's invalid state while not disabled.
    /// <summary>Gets a value indicating whether the visible and hidden inputs render <c>aria-invalid</c>.</summary>
    public bool AriaInvalid => fieldScope is not null && fieldScope.State.Valid == false && !fieldScope.Disabled && !Disabled;

    /// <summary>Gets the field's validation mode, or <see cref="ValidationMode.OnSubmit"/> outside a field.</summary>
    public ValidationMode ValidationMode => fieldScope?.ValidationMode ?? ValidationMode.OnSubmit;

    /// <summary>Gets the form the number field belongs to.</summary>
    public IFormScope Form => form;

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
        if (!initialized)
        {
            initialized = true;
            previousDisabled = Disabled;
        }
    }

    // NUM-ARIA-04, FFI-ARIA-01: the consumer's ids, then the field's message ids.
    /// <summary>Composes an element's <c>aria-describedby</c>.</summary>
    /// <param name="consumer">The consumer's <c>aria-describedby</c>, or <see langword="null"/>.</param>
    /// <returns>The composed ids, or <see langword="null"/>.</returns>
    public string? DescribedBy(string? consumer) => scope is null ? consumer : scope.DescribedBy(consumer);

    // NUM-FOC-05 (useSetFieldFocused): set on the input's focus unless disabled.
    /// <summary>Acquires the field's focused flag for the number field.</summary>
    public void HandleFocus() => fieldScope?.SetFocused(this, true, Disabled);

    // NUM-FRM-07 (NumberFieldInput.tsx:139-145): the input's blur marks the field touched and releases focused, read-only
    // included, before the commit logic.
    /// <summary>Applies a blur of the visible input to the field's flags.</summary>
    public void HandleBlur()
    {
        if (fieldScope is null)
        {
            return;
        }

        using (fieldScope.Batch())
        {
            fieldScope.SetTouched(true);
            fieldScope.SetFocused(this, false, Disabled);
        }
    }

    // NUM-FRM-09: the blur's commit in onBlur mode, with the value the blur commits.
    /// <summary>Commits the field's validation with a value, after the hidden input reported its native state again.</summary>
    /// <param name="value">The committed value, boxed.</param>
    /// <returns>A task that completes when the commit has run.</returns>
    public Task CommitAsync(object? value) => fieldScope is null ? Task.CompletedTask : CommitAfterRefreshAsync(value);

    // NUM-FRM-08: a blur that changed the value already validated; the next value reaction skips its revalidation unless
    // the field validates on change. Reset when the blur's request normalized back to the current value.
    /// <summary>Sets whether the next value reaction skips its revalidation.</summary>
    /// <param name="value"><see langword="true"/> after a blur that changes the value.</param>
    public void BlockRevalidation(bool value) => blockRevalidation = value;

    // NUM-STA-14 (NumberFieldRoot.tsx:259-260): inside each applied change, before the value is stored.
    /// <summary>Sets the field's dirty flag from a validated value and the field's initial value.</summary>
    /// <param name="validated">The validated value.</param>
    public void SetDirty(double? validated)
    {
        if (fieldScope is null)
        {
            return;
        }

        double? initial = fieldScope.ValidityData.InitialValue is double number ? number : null;
        fieldScope.SetDirty(!Store.Equality.StrictEquals(validated, initial));
    }

    // FFI-FRM-26 through the form scope (NumberFieldRoot.tsx:518, the clearErrors of the autofill path and NUM-FRM-08).
    /// <summary>Clears the form's error for the number field's name.</summary>
    public void ClearErrors()
    {
        if (fieldScope is null)
        {
            form.ClearErrors(Name);
        }
        else
        {
            fieldScope.ClearErrors();
        }
    }

    /// <summary>Runs the work after a render: the control-id claim, the registration, the value reaction and the hidden input.</summary>
    /// <param name="controlId">The visible input's id.</param>
    /// <param name="inputMounted">Whether a <see cref="NumberFieldInput"/> is mounted, which registers the control.</param>
    /// <param name="input">The hidden input.</param>
    /// <param name="value">The numeric value, boxed once per commit.</param>
    /// <param name="valueVersion">The root's value counter.</param>
    /// <param name="serializedValue">The hidden input's value.</param>
    /// <param name="filled">Whether the value is not <see langword="null"/>.</param>
    /// <returns>A task that completes when the module has the options.</returns>
    public async Task AfterRenderAsync(string controlId, bool inputMounted, ElementReference? input, object? value, int valueVersion, string serializedValue, bool filled)
    {
        focusTarget = controlId;
        serialized = serializedValue;
        if (fieldScope is not null)
        {
            validationInput.ControlId = controlId;
            fieldScope.Validation.InputRef = validationInput;
            using (fieldScope.Batch())
            {
                ClaimControlId(controlId);
                if (inputMounted)
                {
                    Register(controlId, controlId, value);
                }
                else
                {
                    Unregister();
                }

                SyncDisabled();
                ObserveValue(valueVersion, value, filled);
            }
        }
        else
        {
            ObserveValue(valueVersion, value, filled);
        }

        await AttachOrUpdateAsync(input).ConfigureAwait(true);
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
            Revision = revision,
            Disabled = Disabled,
            ReportEvents = false,
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

        return sent.Revision != revision
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
        refreshRevision = revision;
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

    // FFI-ALG-07: the visible input's id is the scope's control id.
    private void ClaimControlId(string? controlId)
    {
        if (scope is null)
        {
            return;
        }

        if (controlId is null)
        {
            ReleaseClaim();
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

    private void Register(string registrationId, string? controlId, object? value)
    {
        if (Disabled)
        {
            Unregister();
            return;
        }

        FieldControlRegistration next = new(registrationId, name ?? participation.FieldName, value, () => value, controlId, refresh);

        // The boxed value is a new instance per committed value, so the identity check re-registers every change.
        if (registered && next == sentRegistration && ReferenceEquals(value, sentRegistration!.Value))
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

    // NUM-EDGE-12, FFI-ALG-13: released when the field becomes disabled.
    private void SyncDisabled()
    {
        bool disabled = Disabled;
        if (disabled == previousDisabled)
        {
            return;
        }

        previousDisabled = disabled;
        fieldScope!.SetFocused(this, false, disabled);
    }

    // NUM-STA-13 (filled after every render, NumberFieldRoot.tsx:125-127); NUM-FRM-08 (the rest on a change only).
    private void ObserveValue(int valueVersion, object? value, bool filled)
    {
        fieldScope?.SetFilled(filled);
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
        ClearErrors();
        participation.NotifyChanged();
        if (fieldScope is null)
        {
            return;
        }

        if (blockRevalidation && !fieldScope.ShouldValidateOnChange())
        {
            blockRevalidation = false;
            return;
        }

        fieldScope.Observe(ChangeAfterRefreshAsync(valueVersion, value));
    }

    private async Task ChangeAfterRefreshAsync(int valueVersion, object? value)
    {
        await RefreshAsync().ConfigureAwait(true);
        if (fieldScope is not null && !disposed && observedVersion == valueVersion)
        {
            fieldScope.Validation.Change(value);
        }
    }

    private async Task CommitAfterRefreshAsync(object? value)
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
            fieldScope.SetFocused(this, false, Disabled);
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

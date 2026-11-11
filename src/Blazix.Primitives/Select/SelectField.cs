using Blazix.Primitives.Interop;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Blazix.Primitives;

// SEL §8, FFI §6.3 rows `Select.Root`/`Select` (field-and-form-infrastructure.md), composed from Field/ (never edited;
// the brief's Deliverable 3), the CheckableFieldBehavior pattern for a control whose form value sits on a hidden input:
// - The control id (SEL-API-10/26, SEL-FRM-14/23): the trigger's rendered id, claimed in the field's labelable scope after
//   the render; the registration id is the root's id (SEL-FRM-01).
// - Registration (FFI-ALG-12, SEL-FRM-01/02): the raw value boxed once per commit by the root, the string projection as
//   the form value, the trigger as the control, re-registered on a change; none while disabled (SEL-API-13).
// - The hidden input (SEL-DOM-47..52; ADR 04 Amendments 2026-11-04 (track 4's form additions)): the `form` module's
//   control role with ReportEvents = false, ReportAutofill = true, Controlled = true and the serialized value, so the
//   module owns `value` from attach (C# → JS) and restores it after an autofill the root does not take (SEL-FRM-19);
//   FocusTarget hands focus to the trigger (SEL-FOC-06); it is the field's validation input (FFI §6.3 Table A).
// - The value reaction (SEL-EVT-13, SEL-FRM-04..06; useValueChanged): after the render that shows a new value, filled,
//   the form's error cleared, dirty, the EditContext notified, then the change validated once the input reported again.
// - Touched, focused and the onBlur commit (SEL-FOC-10, SEL-FRM-03/07): from the trigger's focus and blur and from the
//   store's focus-out and outside-press closes.
/// <summary>The field half of a select: its control registration, its hidden input and its touched and focused flags.</summary>
internal sealed class SelectField : IDisposable
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
    private FieldRegistry? sinkRegistry;
    private string? sinkId;
    private bool previousDisabled;
    private bool initialized;
    private FormTwin? twin;
    private TwinHandle<FormOptions>? inputHandle;
    private FormOptions? sentOptions;
    private string? serialized;
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

    /// <summary>Initializes a new instance of the <see cref="SelectField"/> class.</summary>
    /// <param name="formless">The form scope of the runtime's fields outside a form.</param>
    /// <param name="jsRuntime">The runtime the twin imports the form module through.</param>
    /// <param name="changed">Called when the field's state that the trigger and the label render changed.</param>
    /// <param name="invoke">Runs an action on the renderer's dispatcher.</param>
    /// <param name="dispatchException">Routes an exception to the root's error boundary.</param>
    public SelectField(IFormScope formless, IJSRuntime jsRuntime, Action changed, Func<Action, Task> invoke, Func<Exception, Task> dispatchException)
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
        autofilled = report => _ = invoke(() => Autofilled?.Invoke(report));
    }

    /// <summary>Gets a value indicating whether the select is inside a field.</summary>
    public bool InField => fieldScope is not null;

    /// <summary>Gets a value indicating whether the select is disabled, its own flag or its field's.</summary>
    public bool Disabled => (fieldScope?.Disabled ?? false) || ownDisabled;

    /// <summary>Gets the effective form name: the field's, else the root's own, else the edit context's.</summary>
    public string? Name => fieldScope?.Name ?? name ?? participation.FieldName;

    /// <summary>Gets the field's state with the select's disabled flag, or the default state outside a field.</summary>
    public FieldStateSnapshot Snapshot => (fieldScope?.State ?? FieldStateSnapshot.Default) with { Disabled = Disabled };

    /// <summary>Gets the field state the select label renders: the field's own, unmodified.</summary>
    public FieldStateSnapshot FieldSnapshot => fieldScope?.State ?? FieldStateSnapshot.Default;

    /// <summary>Gets the id of the field's label, or <see langword="null"/>.</summary>
    public string? FieldLabelId => scope?.Label.Id;

    /// <summary>Gets a value indicating whether the trigger and the hidden input render <c>aria-invalid</c>.</summary>
    public bool AriaInvalid => fieldScope is not null && fieldScope.State.Valid == false && !fieldScope.Disabled && !Disabled;

    /// <summary>Gets the form the select belongs to.</summary>
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
        if (!initialized)
        {
            initialized = true;
            previousDisabled = Disabled;
        }
    }

    // SEL-ARIA-07, FFI-ARIA-01: the consumer's ids, then the field's message ids.
    /// <summary>Composes an element's <c>aria-describedby</c>.</summary>
    /// <param name="consumer">The consumer's <c>aria-describedby</c>, or <see langword="null"/>.</param>
    /// <returns>The composed ids, or <see langword="null"/>.</returns>
    public string? DescribedBy(string? consumer) => scope is null ? consumer : scope.DescribedBy(consumer);

    // SEL-FOC-10 (useSetFieldFocused): set on the trigger's focus unless disabled.
    /// <summary>Acquires the field's focused flag for the select.</summary>
    public void HandleFocus() => fieldScope?.SetFocused(this, true, Disabled);

    // SEL-FOC-10, SEL-FRM-03, SEL-FRM-07 (SelectTrigger.tsx:160-171): a blur whose related target is outside the
    // positioner marks the field touched, releases focused and commits in onBlur mode.
    /// <summary>Applies a blur of the trigger.</summary>
    /// <param name="intoPopup">Whether focus moved into the popup.</param>
    /// <param name="value">The raw value an onBlur commit validates, boxed.</param>
    /// <returns>A task that completes when an onBlur commit has run.</returns>
    public Task HandleBlurAsync(bool intoPopup, object? value)
    {
        if (fieldScope is null || intoPopup)
        {
            return Task.CompletedTask;
        }

        using (fieldScope.Batch())
        {
            fieldScope.SetTouched(true);
            fieldScope.SetFocused(this, false, Disabled);
        }

        return fieldScope.ValidationMode == ValidationMode.OnBlur ? CommitAfterRefreshAsync(value) : Task.CompletedTask;
    }

    // SEL-FOC-10, SEL-FRM-03, SEL-FRM-07 (SelectRoot.tsx:303-313): a focus-out or outside-press close.
    /// <summary>Applies a close of the popup by a focus-out or an outside press.</summary>
    /// <param name="value">The raw value an onBlur commit validates, boxed.</param>
    /// <returns>A task that completes when an onBlur commit has run.</returns>
    public Task HandleDismissAsync(object? value)
    {
        if (fieldScope is null)
        {
            return Task.CompletedTask;
        }

        using (fieldScope.Batch())
        {
            fieldScope.SetTouched(true);
            fieldScope.SetFocused(this, false, Disabled);
        }

        return fieldScope.ValidationMode == ValidationMode.OnBlur ? CommitAfterRefreshAsync(value) : Task.CompletedTask;
    }

    /// <summary>Runs the work after a render: the control-id claim, the registration, the value reaction and the hidden input.</summary>
    /// <param name="controlId">The trigger's rendered id, or <see langword="null"/> while no trigger is mounted.</param>
    /// <param name="registrationId">The root's id.</param>
    /// <param name="input">The hidden input.</param>
    /// <param name="inputId">The hidden input's rendered id, or <see langword="null"/>.</param>
    /// <param name="value">The raw value, boxed once per commit.</param>
    /// <param name="getValue">Returns the string projection of the value.</param>
    /// <param name="valueVersion">The store's value counter.</param>
    /// <param name="serializedValue">The hidden input's value.</param>
    /// <param name="filled">Whether a value is selected.</param>
    /// <param name="dirty">Whether the value differs from the initial one.</param>
    /// <returns>A task that completes when the module has the options.</returns>
    public async Task AfterRenderAsync(string? controlId, string registrationId, ElementReference? input, string? inputId, object? value, Func<object?> getValue, int valueVersion, string serializedValue, bool filled, Func<bool> dirty)
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
                SetSink(inputId);
                Register(registrationId, controlId, value, getValue);
                SyncDisabled();
                ObserveValue(valueVersion, value, filled, dirty);
            }
        }
        else
        {
            ObserveValue(valueVersion, value, filled, dirty);
        }

        await AttachOrUpdateAsync(input).ConfigureAwait(true);
    }

    // SEL-FRM-19: an autofill the root takes moves the committed value, which the module shows from this revision on.
    /// <summary>Sends the hidden input's committed value now, before the render batch.</summary>
    /// <param name="serializedValue">The committed value's form string.</param>
    public void SendValue(string serializedValue)
    {
        if (!string.Equals(serialized, serializedValue, StringComparison.Ordinal))
        {
            serialized = serializedValue;
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

    // FFI-ALG-07: the trigger's rendered id is the scope's control id.
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

    private void Register(string registrationId, string? controlId, object? value, Func<object?> getValue)
    {
        if (Disabled)
        {
            Unregister();
            return;
        }

        FieldControlRegistration next = new(registrationId, name ?? participation.FieldName, value, getValue, controlId, refresh);

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

    // FFI-FRM-04: the state the module reads from the hidden input at a submission reaches the validation input by its id.
    private void SetSink(string? inputId)
    {
        FieldRegistry registry = form.Registry;
        if (ReferenceEquals(registry, sinkRegistry) && string.Equals(inputId, sinkId, StringComparison.Ordinal))
        {
            return;
        }

        RemoveSink();
        if (inputId is not null)
        {
            sinkRegistry = registry;
            sinkId = inputId;
            registry.SetInput(inputId, validationInput);
        }
    }

    private void RemoveSink()
    {
        if (sinkRegistry is not null)
        {
            sinkRegistry.RemoveInput(sinkId!, validationInput);
            sinkRegistry = null;
            sinkId = null;
        }
    }

    // SEL-FRM-20, FFI-ALG-13: released when the trigger becomes disabled.
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

    // SEL-FRM-05 (filled on every render, SelectRoot.tsx:254-262), SEL-EVT-13 (the rest on a change only).
    private void ObserveValue(int valueVersion, object? value, bool filled, Func<bool> dirty)
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
        if (fieldScope is null)
        {
            // FFI-FRM-26 through the form scope, for a select outside a field inside a form.
            form.ClearErrors(Name);
            participation.NotifyChanged();
            return;
        }

        fieldScope.ClearErrors();
        fieldScope.SetDirty(dirty());
        participation.NotifyChanged();
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
            RemoveSink();
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

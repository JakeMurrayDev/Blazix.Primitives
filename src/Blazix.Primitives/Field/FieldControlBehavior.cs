using System.Linq.Expressions;
using Blazix.Primitives.Interop;
using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace Blazix.Primitives;

// Base UI's Field.Control (field/control/FieldControl.tsx), which Input renders unchanged (input/Input.tsx:13-18): one
// behavior under two sealed parts (ADR 08 Amendments 2026-10-22; ADR 16 rule 10; the ButtonBehavior pattern).
// - The control-id claim (FFI-ALG-07): the rendered id is the scope's control id, else the consumer's, else the control's
//   own generated id (FLD-API-20, FLD-DOM-08, FFI-FRM-34/36); registered after the render (FFI registration row).
// - Registration (FLD-FRM-01, FFI-ALG-12, FFI-FRM-33): after a render that changed the id, the name, the serialized value or
//   the enabled state; value String(value) when controlled, the element's live value as the form value.
// - Value ownership (ADR 05 Amendments 2026-10-18 item 1: the mode is fixed at the first parameter set): C# renders the
//   value until the form module attaches; from then on the module owns the element's value and default value (the value
//   design point, pending the user), so a controlled edit the consumer does not apply is reverted (FLD-API-15) and a
//   changed default keeps the user's edit (FFI-EDGE-06/07).
// - The module owns the input, change, blur and keydown listeners and reports value and validity in one message per event
//   (ADR 04 Amendments 2026-10-18; M2 plan, settled); the consumer's oninput, onblur and onkeydown run from the report,
//   before the part's logic, so PreventPartHandler() vetoes on both render modes (FLD-EVT-09, FLD-KEY-04). onfocus is
//   chained by the pipeline.
// - FLD-EVT-01..07: onValueChange first; uncontrolled: dirty and filled, then (not canceled, not prevented) clearErrors and
//   change; an uncontrolled native edit commits despite Cancel() (ADR 05 Amendments 2026-10-18 item 2); controlled: the
//   reactions follow String(value) after the render (FLD-EVT-06).
// - FFI-ALG-13 focus ownership; FFI-ALG-16 Enter (the 0 ms task is the module's, pending the user); FLD-FRM-17/FFI-FRM-32 the
//   blur re-commit after the render (ADR 01 decision 3 amendment).
// - ADR 05 Amendments 2026-10-22: EditContext participation.
/// <summary>The behavior of a field control: what <see cref="FieldControl"/> and <see cref="Input"/> share.</summary>
internal sealed class FieldControlBehavior : IDisposable
{
    /// <summary>Represents the event the form module reports a value change for.</summary>
    public const string InputEvent = "oninput";

    /// <summary>Represents the event the form module reports a blur for.</summary>
    public const string BlurEvent = "onblur";

    /// <summary>Represents the event the form module reports a keydown for.</summary>
    public const string KeyDownEvent = "onkeydown";

    private const string FocusEvent = "onfocus";

    private readonly Action stateHasChanged;
    private readonly Func<Action, Task> invoke;
    private readonly Func<Exception, Task> dispatchException;
    private readonly FieldConsumerHandlers consumerHandlers = new(InputEvent, BlurEvent, KeyDownEvent);
    private readonly Controlled<string?> controlled;
    private readonly FieldValidationInput validationInput;
    private readonly EditContextParticipation participation;
    private readonly Func<object?> getValue;
    private readonly Func<Task> refresh;
    private readonly Action fieldChanged;
    private readonly Action<FormControlReport> reported;
    private readonly IJSRuntime jsRuntime;
    private readonly string ownId;
    private readonly TaskCompletionSource attachedSource = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private FieldRootContext? fieldScope;
    private FieldItemContext? item;
    private LabelableScope? scope;
    private IFormScope form = DefaultFormScope.Instance;
    private FieldControlParameters parameters;
    private string? lastValue;
    private string? renderedValue;
    private bool valueFrozen;
    private bool claimRegistered;
    private bool hadExplicitId;
    private FieldControlRegistration? sentRegistration;
    private bool registered;
    private FormTwin? twin;
    private TwinHandle<FormOptions>? handle;
    private FormOptions? sentOptions;
    private int revision;
    private TaskCompletionSource? refreshSource;
    private int refreshRevision;
    private bool syncPending;
    private string? syncedSerialized;
    private bool serializedKnown;
    private string? defaultValueSynced;
    private bool previousDisabled;
    private bool enterPending;
    private int enterSubmitCount;
    private string? blurValue;
    private bool blurPending;
    private RenderKey renderedKey;
    private bool rendered;
    private bool initialized;
    private bool disposed;

    /// <summary>Initializes a new instance of the <see cref="FieldControlBehavior"/> class.</summary>
    /// <param name="componentName">The part's name, used in development warnings.</param>
    /// <param name="ownId">The id the control renders when neither its field nor the consumer gives one.</param>
    /// <param name="jsRuntime">The runtime the twin imports the form module through.</param>
    /// <param name="logger">The part's logger, which receives development warnings.</param>
    /// <param name="stateHasChanged">Renders the part again.</param>
    /// <param name="invoke">Runs an action on the renderer's dispatcher.</param>
    /// <param name="dispatchException">Routes an exception to the part's error boundary.</param>
    public FieldControlBehavior(string componentName, string ownId, IJSRuntime jsRuntime, ILogger logger, Action stateHasChanged, Func<Action, Task> invoke, Func<Exception, Task> dispatchException)
    {
        this.ownId = ownId;
        this.jsRuntime = jsRuntime;
        this.stateHasChanged = stateHasChanged;
        this.invoke = invoke;
        this.dispatchException = dispatchException;
        controlled = new Controlled<string?>(componentName, logger: logger);
        validationInput = new FieldValidationInput(_ => SendOptions());
        participation = new EditContextParticipation(OnEditContextMessages);
        getValue = () => lastValue;
        refresh = RefreshAsync;
        fieldChanged = OnFieldChanged;
        reported = report => _ = invoke(() => Observe(HandleReportAsync(report)));
    }

    private bool EffectiveDisabled => (fieldScope?.Disabled ?? false) || parameters.Disabled;

    // FLD-API-15, FLD-EDGE-10: String(value), `undefined` for null.
    private string? Serialized => controlled.IsControlled ? controlled.Value : null;

    // FLD-API-18, ADR 05 Amendments 2026-10-22: the control's own name, else the EditContext identifier's field name.
    private string? OwnName => parameters.Name ?? participation.FieldName;

    private string? IdParameter => parameters.AdditionalAttributes is not null && parameters.AdditionalAttributes.TryGetValue("id", out object? value) && value is string text ? text : null;

    /// <summary>Gets a value indicating whether the consumer owns the value.</summary>
    /// <value>Fixed at the first parameter set.</value>
    public bool IsControlled => controlled.IsControlled;

    /// <summary>Gets a task that completes when the form module is attached to the element.</summary>
    /// <value>The attach task, for the fixtures.</value>
    public Task TwinAttached => attachedSource.Task;

    // FLD-STA-03: the field's state with `disabled = fieldDisabled || disabledProp`; outside a field, the default state.
    /// <summary>Gets the control's state.</summary>
    /// <value>The field's snapshot with the control's effective disabled state.</value>
    public FieldStateSnapshot Snapshot => (fieldScope?.State ?? FieldStateSnapshot.Default) with { Disabled = EffectiveDisabled };

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
            handle = null;
        }
    }

    /// <summary>Reads the value parameter of a parameter set; called before the part's parameters are applied.</summary>
    /// <param name="parameterView">The parameters.</param>
    public void SetParameters(ParameterView parameterView)
    {
        bool supplied = parameterView.TryGetValue(nameof(FieldControl.Value), out string? value);
        controlled.Update(supplied, value, false, null);
    }

    /// <summary>Takes the cascaded scopes; called once, when the part initializes.</summary>
    /// <param name="fieldContext">The nearest field, or <see langword="null"/>.</param>
    /// <param name="itemContext">The nearest item, or <see langword="null"/>.</param>
    public void Initialize(FieldRootContext? fieldContext, FieldItemContext? itemContext)
    {
        fieldScope = fieldContext;
        item = itemContext;
        scope = fieldScope is null ? null : FieldItemContext.LabelableOf(fieldScope, item);
        fieldScope?.AddListener(fieldChanged);
    }

    /// <summary>Applies the part's parameters; called on every parameter set.</summary>
    /// <param name="next">The parameters.</param>
    /// <param name="formScope">The cascaded form scope, or <see langword="null"/>.</param>
    public void SetParameters(FieldControlParameters next, IFormScope? formScope)
    {
        bool first = !initialized;
        initialized = true;
        parameters = next;
        form = fieldScope?.Form ?? formScope ?? DefaultFormScope.Instance;
        consumerHandlers.Set(next.AdditionalAttributes);
        participation.Update(next.EditContext, next.ValueExpression);
        if (first)
        {
            // The live value before any report: what the first render shows.
            lastValue = controlled.IsControlled ? controlled.Value : next.DefaultValue ?? string.Empty;
            previousDisabled = EffectiveDisabled;
        }
    }

    /// <summary>Determines whether the consumer's handler for an event is rendered on the element.</summary>
    /// <param name="eventName">The event's attribute name in lowercase.</param>
    /// <returns><see langword="false"/> for the events whose consumer handlers run from the module's reports.</returns>
    public bool ForwardsConsumerHandler(string eventName) => !consumerHandlers.Declines(eventName);

    /// <summary>Adds the control's attributes to a render.</summary>
    /// <param name="attributes">The part-supplied attributes.</param>
    /// <param name="onFocus">The part's focus handler.</param>
    public void AddAttributes(AttributeList attributes, EventCallback onFocus)
    {
        rendered = true;
        renderedKey = CurrentKey();
        if (!valueFrozen)
        {
            renderedValue = controlled.IsControlled ? controlled.Value : parameters.DefaultValue;
        }

        if (renderedKey.Name is { } name)
        {
            attributes.Set("name", name);
        }

        if (renderedKey.Disabled)
        {
            attributes.Set("disabled", true);
        }

        if (renderedKey.LabelId is { } labelId)
        {
            attributes.Set("aria-labelledby", labelId);
        }

        if (parameters.AutoFocus)
        {
            attributes.Set("autofocus", true);
        }

        if (renderedValue is not null)
        {
            attributes.Set("value", renderedValue);
        }

        attributes.Set(FocusEvent, onFocus);
    }

    // FLD-DOM-08 (the resolved id, not the consumer's, until registration), FLD-ARIA-02 (the consumer's ids composed with the
    // scopes' description ids) and FLD-ARIA-03 (`aria-invalid="true"` wins while invalid): attributes the document makes
    // part-owned, so they win over the consumer here (ADR 02 Amendments 2026-10-21, the human check of Rendering/CONTEXT.md).
    /// <summary>Adjusts the merged attributes: the resolved id, the composed description ids and the invalid state.</summary>
    /// <param name="attributes">The merged attributes.</param>
    public void FinishAttributes(AttributeList attributes)
    {
        attributes.Set("id", renderedKey.Id);
        if (fieldScope is null)
        {
            return;
        }

        string? consumer = attributes.TryGetValue("aria-describedby", out AttributeValue current) && current.TryGetValue(out string text) ? text : null;
        string? describedBy = scope!.DescribedBy(consumer);
        if (describedBy is null)
        {
            attributes.Remove("aria-describedby");
        }
        else
        {
            attributes.Set("aria-describedby", describedBy);
        }

        if (renderedKey.AriaInvalid)
        {
            attributes.Set("aria-invalid", "true");
        }
    }

    /// <summary>Runs the work that follows a render: the id claim, the registration, the module and the controlled value.</summary>
    /// <param name="element">The rendered element, or <see langword="null"/> under a substitute callback.</param>
    /// <returns>A task that completes when the module has the options.</returns>
    public async Task AfterRenderAsync(ElementReference? element)
    {
        if (!parameters.Enabled)
        {
            Unmount();
            if (handle is { } released)
            {
                handle = null;
                sentOptions = null;
                valueFrozen = false;
                await released.DisposeAsync().ConfigureAwait(true);
            }

            return;
        }

        if (fieldScope is not null)
        {
            fieldScope.Validation.InputRef = validationInput;
            validationInput.ControlId = renderedKey.Id;
        }

        using FieldRootContext.BatchScope? batch = fieldScope?.Batch();
        ClaimControlId();
        Register();
        SyncDisabled();
        SyncFilledOnRender();
        SyncControlledValue();
        await AttachOrUpdateAsync(element).ConfigureAwait(true);
    }

    /// <summary>Acquires the field's focused flag for the control.</summary>
    /// <param name="e">The focus event.</param>
    public void HandleFocus(FocusEventArgs e) => fieldScope?.SetFocused(this, true, EffectiveDisabled);

    // DP6 (pending the user): a commit no event started reads the control's state from a fresh report.
    private Task RefreshAsync()
    {
        if (handle is null)
        {
            return Task.CompletedTask;
        }

        refreshSource ??= new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        revision++;
        refreshRevision = revision;
        SendOptions();
        return refreshSource.Task;
    }

    private RenderKey CurrentKey()
    {
        string renderedId = RenderedId();
        bool disabled = EffectiveDisabled;
        if (fieldScope is null)
        {
            return new RenderKey(Snapshot, renderedId, OwnName, disabled, null, null, false);
        }

        FieldStateSnapshot snapshot = Snapshot;
        return new RenderKey(
            snapshot,
            renderedId,
            fieldScope.Name ?? OwnName,
            disabled,
            scope!.Label.Id,
            scope.DescribedBy(null),
            fieldScope.State.Valid == false && !fieldScope.Disabled && !disabled);
    }

    // FFI-ALG-07 `rendered id = (enabled ? scope.controlId : undefined) ?? idProp ?? ownId`.
    private string RenderedId() => (scope is not null ? scope.ControlId : null) ?? IdParameter ?? ownId;

    // FFI-ALG-07, post-render.
    private void ClaimControlId()
    {
        if (scope is null)
        {
            return;
        }

        string? explicitId = IdParameter;
        string next;
        if (explicitId is not null)
        {
            hadExplicitId = true;
            next = explicitId;
        }
        else if (hadExplicitId)
        {
            next = ownId;
        }
        else
        {
            scope.ResetControlId();
            return;
        }

        claimRegistered = true;
        scope.RegisterControlId(this, next);
    }

    // FLD-FRM-01, FFI-ALG-12 control side; enabled = !disabled.
    private void Register()
    {
        if (fieldScope is null)
        {
            return;
        }

        if (EffectiveDisabled)
        {
            if (registered)
            {
                registered = false;
                sentRegistration = null;
                fieldScope.RegisterControl(this, null);
            }

            return;
        }

        FieldControlRegistration next = new(RenderedId(), OwnName, Serialized, getValue, RenderedId(), refresh);
        if (registered && next == sentRegistration)
        {
            return;
        }

        registered = true;
        sentRegistration = next;
        fieldScope.RegisterControl(this, next);
    }

    // FFI-ALG-13 `useSetFieldFocused` effect on `disabled`: release, then ask the module whether the element has focus.
    private void SyncDisabled()
    {
        bool disabled = EffectiveDisabled;
        if (disabled == previousDisabled)
        {
            return;
        }

        previousDisabled = disabled;
        fieldScope?.SetFocused(this, false, disabled);
    }

    // FLD-STA-13: after the first render, the filled flag from the value the element shows.
    private void SyncFilledOnRender()
    {
        if (fieldScope is null || serializedKnown)
        {
            return;
        }

        serializedKnown = true;
        syncedSerialized = Serialized;
        defaultValueSynced = parameters.DefaultValue;
        string? current = Serialized ?? lastValue;
        if (current is not null)
        {
            fieldScope.SetFilled(current.Length > 0);
        }
    }

    // FLD-EVT-06, FLD-STA-13/14: a change of String(value) runs clearErrors, setDirty and change once, after the render; a
    // change of the default value recomputes filled from the element. Both need the element's state after the module
    // applied the value, unless a report already carried it.
    private void SyncControlledValue()
    {
        string? serialized = Serialized;
        bool valueChanged = controlled.IsControlled && !string.Equals(serialized, syncedSerialized, StringComparison.Ordinal);
        bool defaultChanged = !controlled.IsControlled && !string.Equals(parameters.DefaultValue, defaultValueSynced, StringComparison.Ordinal);
        if (!valueChanged && !defaultChanged)
        {
            return;
        }

        syncedSerialized = serialized;
        defaultValueSynced = parameters.DefaultValue;
        if (valueChanged && (handle is null || string.Equals(serialized, lastValue, StringComparison.Ordinal)))
        {
            // The element already shows the value (a reported edit the consumer applied, or no module yet).
            RunControlledSequence(serialized);
            return;
        }

        if (handle is null)
        {
            return;
        }

        syncPending = true;
        revision++;
    }

    private void RunControlledSequence(string? serialized)
    {
        lastValue = serialized ?? lastValue;
        if (fieldScope is null)
        {
            // FLD-FRM-18: a field-less control inside a form still clears the form's error for its own name.
            if (serialized is not null)
            {
                form.ClearErrors(OwnName);
                participation.NotifyChanged();
            }

            return;
        }

        if (serialized is null)
        {
            return;
        }

        using FieldRootContext.BatchScope batch = fieldScope.Batch();
        fieldScope.SetFilled(serialized.Length > 0);
        fieldScope.ClearErrors();
        fieldScope.SetDirty(!Equals(serialized, fieldScope.ValidityData.InitialValue ?? string.Empty));
        participation.NotifyChanged();
        fieldScope.Validation.Change(serialized);
        RunBlurRecommit(serialized);
    }

    // FLD-FRM-17, FFI-FRM-32: in onBlur mode a controlled value normalised after the blur is validated once more, unless it
    // went back to the initial value (ADR 01 decision 3 amendment: the render boundary does the microtask's job).
    private void RunBlurRecommit(string? current)
    {
        if (!blurPending)
        {
            return;
        }

        blurPending = false;
        if (current is not null && !string.Equals(current, blurValue, StringComparison.Ordinal) && !Equals(current, fieldScope!.ValidityData.InitialValue ?? string.Empty))
        {
            fieldScope.Observe(fieldScope.Validation.CommitAsync(current));
        }
    }

    private async Task AttachOrUpdateAsync(ElementReference? element)
    {
        FormOptions options = Options();
        if (handle is not null)
        {
            if (options != sentOptions)
            {
                sentOptions = options;
                await handle.UpdateAsync(options).ConfigureAwait(true);
            }

            return;
        }

        if (twin is null)
        {
            twin = new FormTwin(jsRuntime);
            twin.ControlReported += reported;
        }

        sentOptions = options;
        string id = renderedKey.Id;
        handle = element is { } reference ? await twin.AttachAsync(reference, options).ConfigureAwait(true) : await twin.AttachAsync(id, options).ConfigureAwait(true);
        valueFrozen = true;
        attachedSource.TrySetResult();
    }

    private void SendOptions()
    {
        if (handle is null)
        {
            return;
        }

        FormOptions options = Options();
        if (options == sentOptions)
        {
            return;
        }

        sentOptions = options;
        Observe(handle.UpdateAsync(options).AsTask());
    }

    private FormOptions Options() => new()
    {
        Control = new FormControlOptions
        {
            FormId = form.ElementId,
            CustomValidity = validationInput.OwnMessage,
            Controlled = controlled.IsControlled,
            Value = controlled.IsControlled ? controlled.Value : null,
            DefaultValue = controlled.IsControlled ? null : parameters.DefaultValue,
            Revision = revision,
            ReportKeys = consumerHandlers.Has(KeyDownEvent),
            Disabled = EffectiveDisabled,
        },
    };

    private async Task HandleReportAsync(FormControlReport report)
    {
        if (disposed)
        {
            return;
        }

        if (report.Value is not null)
        {
            lastValue = report.Value;
        }

        if (report.Validity is { } validity)
        {
            validationInput.Report(validity);
        }

        switch (report.Type)
        {
            case "input":
                await HandleInputAsync(report).ConfigureAwait(true);
                break;
            case "blur":
                await HandleBlurAsync(report).ConfigureAwait(true);
                break;
            case "keydown":
                await HandleKeyDownAsync(report).ConfigureAwait(true);
                break;
            case "enter":
                HandleEnter(report);
                break;
            case "sync":
                HandleSync(report);
                break;
            case "focus":
                fieldScope?.SetFocused(this, true, EffectiveDisabled);
                break;
        }
    }

    // FLD-EVT-01..05/07, FLD-FRM-18, ADR 05 Amendments 2026-10-18 item 2.
    private async Task HandleInputAsync(FormControlReport report)
    {
        string value = report.Value ?? string.Empty;
        if (await consumerHandlers.RunAsync(InputEvent, new ChangeEventArgs { Value = value }).ConfigureAwait(true))
        {
            return;
        }

        ChangeEventDetails details = new(EventReason.None, report.Event);
        await parameters.OnValueChange.InvokeAsync(new ValueChangeEventArgs<string>(value, details)).ConfigureAwait(true);
        bool canceled = details.IsCanceled;
        if (controlled.IsControlled)
        {
            if (!canceled)
            {
                await parameters.ValueChanged.InvokeAsync(value).ConfigureAwait(true);
            }

            // FLD-API-15: an edit the consumer did not apply is reverted (by the next update, which carries the value).
            if (!string.Equals(controlled.Value, value, StringComparison.Ordinal))
            {
                revision++;
                SendOptions();
            }

            return;
        }

        // An uncontrolled native edit commits despite Cancel(): the browser already changed the text.
        await parameters.ValueChanged.InvokeAsync(value).ConfigureAwait(true);
        controlled.SetValueIfUncontrolled(value);
        bool prevented = report.Event?.DefaultPrevented == true;
        if (fieldScope is null)
        {
            if (!canceled && !prevented)
            {
                form.ClearErrors(OwnName);
                participation.NotifyChanged();
            }

            return;
        }

        using FieldRootContext.BatchScope batch = fieldScope.Batch();
        fieldScope.SetDirty(!Equals(value, fieldScope.ValidityData.InitialValue ?? string.Empty));
        fieldScope.SetFilled(value.Length > 0);
        if (!canceled && !prevented)
        {
            fieldScope.ClearErrors();
            participation.NotifyChanged();
            fieldScope.Validation.Change(value);
        }
    }

    // FLD-FOC-02: touched, then focused released, then the onBlur commit; FLD-FRM-17 for a controlled value.
    private async Task HandleBlurAsync(FormControlReport report)
    {
        if (await consumerHandlers.RunAsync(BlurEvent, new FocusEventArgs { Type = "blur" }).ConfigureAwait(true) || fieldScope is null)
        {
            return;
        }

        using FieldRootContext.BatchScope batch = fieldScope.Batch();
        fieldScope.SetTouched(true);
        fieldScope.SetFocused(this, false, EffectiveDisabled);
        if (fieldScope.ValidationMode != ValidationMode.OnBlur)
        {
            return;
        }

        string value = report.Value ?? lastValue ?? string.Empty;
        fieldScope.Observe(fieldScope.Validation.CommitAsync(value));
        if (controlled.IsControlled)
        {
            blurValue = value;
            blurPending = true;
            if (!string.Equals(Serialized, value, StringComparison.Ordinal))
            {
                // The consumer's handler already normalised the value: it is validated once the element shows it.
                return;
            }

            blurPending = false;
        }
    }

    // FLD-KEY-01/04, FFI-ALG-16: the consumer's handler first; Enter on an input marks touched, then commits now or after the
    // module's 0 ms task unless the form submitted meanwhile.
    private async Task HandleKeyDownAsync(FormControlReport report)
    {
        EventSnapshot snapshot = report.Event ?? EventSnapshot.CreatePlaceholder();
        if (await consumerHandlers.RunAsync(KeyDownEvent, snapshot.ToKeyboardEventArgs()).ConfigureAwait(true))
        {
            return;
        }

        if (!string.Equals(snapshot.Key, "Enter", StringComparison.Ordinal) || !string.Equals(snapshot.Target?.TagName, "INPUT", StringComparison.OrdinalIgnoreCase) || fieldScope is null)
        {
            return;
        }

        enterPending = false;
        fieldScope.SetTouched(true);
        if (report.SameForm && !snapshot.DefaultPrevented)
        {
            enterPending = true;
            enterSubmitCount = form.SubmitCount;
            return;
        }

        fieldScope.Observe(fieldScope.Validation.CommitAsync(report.Value ?? lastValue ?? string.Empty));
    }

    private void HandleEnter(FormControlReport report)
    {
        if (!enterPending || fieldScope is null)
        {
            return;
        }

        enterPending = false;
        if (form.SubmitCount == enterSubmitCount)
        {
            fieldScope.Observe(fieldScope.Validation.CommitAsync(report.Value ?? lastValue ?? string.Empty));
        }
    }

    private void HandleSync(FormControlReport report)
    {
        if (syncPending && report.Revision >= revision)
        {
            syncPending = false;
            if (controlled.IsControlled)
            {
                RunControlledSequence(Serialized);
            }
            else if (fieldScope is not null && report.Value is not null)
            {
                fieldScope.SetFilled(report.Value.Length > 0);
            }
        }

        if (refreshSource is { } source && report.Revision >= refreshRevision)
        {
            refreshSource = null;
            source.TrySetResult();
        }
    }

    // ADR 05 Amendments 2026-10-22 item 3: inside a field, the context's messages for the control are the field's external errors.
    private void OnEditContextMessages(IReadOnlyList<string> messages) => fieldScope?.SetExternalErrors(participation, messages);

    private void OnFieldChanged()
    {
        if (!rendered || CurrentKey() == renderedKey)
        {
            return;
        }

        stateHasChanged();
    }

    // Unmounting (disposal, or Enabled set to false): unregister, release the claim and the focused flag.
    private void Unmount()
    {
        if (fieldScope is null)
        {
            return;
        }

        using FieldRootContext.BatchScope batch = fieldScope.Batch();
        if (registered)
        {
            registered = false;
            sentRegistration = null;
            fieldScope.RegisterControl(this, null);
        }

        if (claimRegistered)
        {
            claimRegistered = false;
            scope!.UnregisterControlId(this);
        }

        fieldScope.SetFocused(this, false, EffectiveDisabled);
        if (ReferenceEquals(fieldScope.Validation.InputRef, validationInput))
        {
            fieldScope.Validation.InputRef = null;
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

    /// <summary>What a render of the control shows, compared to tell whether a change of the field needs another render.</summary>
    private readonly record struct RenderKey(FieldStateSnapshot State, string Id, string? Name, bool Disabled, string? LabelId, string? DescribedBy, bool AriaInvalid);
}

/// <summary>The parameters of a field control that its behavior reads.</summary>
/// <param name="Name">The control's name parameter.</param>
/// <param name="Disabled">The control's disabled parameter.</param>
/// <param name="AutoFocus">The control's autofocus parameter.</param>
/// <param name="DefaultValue">The default value.</param>
/// <param name="ValueChanged">The binding callback of the value.</param>
/// <param name="OnValueChange">The change callback.</param>
/// <param name="ValueExpression">The expression of the bound value.</param>
/// <param name="EditContext">The cascaded edit context.</param>
/// <param name="AdditionalAttributes">The unmatched attributes.</param>
/// <param name="Enabled">Whether the part renders.</param>
internal readonly record struct FieldControlParameters(
    string? Name,
    bool Disabled,
    bool AutoFocus,
    string? DefaultValue,
    EventCallback<string?> ValueChanged,
    EventCallback<ValueChangeEventArgs<string>> OnValueChange,
    Expression<Func<string?>>? ValueExpression,
    EditContext? EditContext,
    IReadOnlyDictionary<string, object>? AdditionalAttributes,
    bool Enabled);

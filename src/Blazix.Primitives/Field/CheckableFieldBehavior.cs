using System.Linq.Expressions;
using Blazix.Primitives.Interop;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;

namespace Blazix.Primitives;

// The field half of a control whose value is a hidden checkbox input's `checked` (Checkbox now; Switch's follow-up and
// Radio reuse it; design point C5 of M2 track 5, settled by the orchestrator 2026-10-23): FFI §8 as checkbox §8 states it
// for a standalone box, the FieldControlBehavior pattern with a boolean value and two form-module handles on one twin.
// - The control-id claim (FFI-ALG-07, CHK-DOM-15/21, CHK-FRM-16): the labelable control id is the scope's control id, else
//   the consumer's non-empty id, else the part's own; registered after the render; a box that shares its scope with a
//   group claims nothing.
// - Registration (CHK-FRM-06/07, FFI-FRM-17): value `checked`, element the root, id the root's generated id, name the
//   part's own; dropped while disabled, inside a group, and on unmount.
// - The value-changed reaction (CHK-STA-10/11, CHK-FRM-09/10/13; useValueChanged): after the render that shows a new
//   value, filled, then the form's error cleared, dirty, the EditContext notified, and the change validated once the
//   hidden input's native state has been read again (the refresh: a toggle changes valueMissing).
// - The module's handles (ADR 04 §1, the `form` row): the root reports its blur and whether it already has focus when it
//   attaches or becomes enabled (CHK-FRM-14, CHK-FOC-04/05; FFI-STA-09); the hidden input is the validation input,
//   reporting only on a refresh and handing focus to the root (CHK-FRM-19, CHK-FOC-03; FFI-FOC-05). While nothing else
//   names the control, the module also looks for the input's native label and reports its id (CHK-ARIA-07/08; design
//   point C4, settled by the orchestrator 2026-10-23). A group's member registers that input with the field's validation
//   inputs instead (checkbox-group §8, FFI-FRM-24); a commit it starts refreshes every member's input.
// - ADR 05 Amendments 2026-10-22: EditContext participation through the checked expression.
/// <summary>The field behavior of a control backed by a hidden checkbox input.</summary>
internal sealed class CheckableFieldBehavior : IDisposable
{
    private const string BlurReport = "blur";
    private const string FocusReport = "focus";
    private const string SyncReport = "sync";
    private const string LabelReport = "label";

    // A bool boxes into the registration and the validation arguments (FieldControlRegistration.Value and
    // FieldValidationArguments.Value are `object?`, ADR 05's note of 2026-10-22); one instance per value, boxed once, keeps
    // every render and commit allocation-free.
#pragma warning disable BLZX2005 // The two values are boxed once, at type initialization.
    private static readonly object s_true = true;
    private static readonly object s_false = false;
#pragma warning restore BLZX2005

    private readonly Action stateHasChanged;
    private readonly Func<Exception, Task> dispatchException;
    private readonly IJSRuntime jsRuntime;
    private readonly string ownId;
    private readonly FieldValidationInput validationInput;
    private readonly EditContextParticipation participation;
    private readonly Func<object?> getValue;
    private readonly Func<Task> refresh;
    private readonly Action fieldChanged;
    private readonly Action<FormControlReport> reported;
    private readonly TaskCompletionSource attachedSource = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private FieldRootContext? fieldScope;
    private FieldItemContext? item;
    private LabelableScope? scope;
    private IFormScope form = DefaultFormScope.Instance;
    private CheckableFieldParameters parameters;
    private bool? syncedChecked;
    private bool claimRegistered;
    private bool hadExplicitId;
    private FieldControlRegistration? sentRegistration;
    private bool registered;
    private bool inputRegistered;
    private FieldRegistry? sinkRegistry;
    private string? sinkId;
    private bool previousDisabled;
    private FormTwin? twin;
    private TwinHandle<FormOptions>? rootHandle;
    private TwinHandle<FormOptions>? inputHandle;
    private FormOptions? sentRootOptions;
    private FormOptions? sentInputOptions;
    private string? focusTarget;
    private string? fallbackLabelId;
    private int revision;
    private int refreshRevision;
    private TaskCompletionSource? refreshSource;
    private RenderKey renderedKey;
    private bool rendered;
    private bool initialized;
    private bool disposed;

    /// <summary>Initializes a new instance of the <see cref="CheckableFieldBehavior"/> class.</summary>
    /// <param name="ownId">The id the control's labelable element renders when neither its field nor the consumer gives one.</param>
    /// <param name="jsRuntime">The runtime the twin imports the form module through.</param>
    /// <param name="stateHasChanged">Updates the part's state and renders it again.</param>
    /// <param name="invoke">Runs an action on the renderer's dispatcher.</param>
    /// <param name="dispatchException">Routes an exception to the part's error boundary.</param>
    public CheckableFieldBehavior(string ownId, IJSRuntime jsRuntime, Action stateHasChanged, Func<Action, Task> invoke, Func<Exception, Task> dispatchException)
    {
        this.ownId = ownId;
        this.jsRuntime = jsRuntime;
        this.stateHasChanged = stateHasChanged;
        this.dispatchException = dispatchException;
        validationInput = new FieldValidationInput(_ => SendInputOptions());
        participation = new EditContextParticipation(OnEditContextMessages);
        getValue = () => Box(parameters.Checked);
        refresh = RefreshAsync;
        fieldChanged = OnFieldChanged;
        reported = report => _ = invoke(() => Observe(HandleReportAsync(report)));
    }

    private string? OwnName => parameters.Name ?? participation.FieldName;

    private bool LabelFallbackActive => parameters.LabelFallback && LabelId is null;

    private string? IdParameter => parameters.AdditionalAttributes is not null
        && parameters.AdditionalAttributes.TryGetValue("id", out object? value)
        && value is string { Length: > 0 } text ? text : null;

    // CHK-API-11: the field's, the item's and the part's own disabled state, OR'd.
    /// <summary>Gets a value indicating whether the control is disabled, its field's and item's state included.</summary>
    /// <value><see langword="true"/> when any of them is disabled.</value>
    public bool EffectiveDisabled => (fieldScope is not null && FieldItemContext.DisabledOf(fieldScope, item)) || parameters.Disabled || GroupDisabled;

    /// <summary>Gets or sets a value indicating whether the control's group disables it.</summary>
    /// <value><see langword="true"/> while an enclosing group is disabled.</value>
    public bool GroupDisabled { get; set; }

    // CHK-API-08, FLD-API-18, ADR 05 Amendments 2026-10-22: the field's name, else the control's own, else the EditContext
    // identifier's field name.
    /// <summary>Gets the name the control's value is submitted under.</summary>
    /// <value>The effective name, or <see langword="null"/>.</value>
    public string? Name => fieldScope?.Name ?? OwnName;

    // FFI-ALG-07 `rendered id = (enabled ? scope.controlId : undefined) ?? idProp ?? ownId`; CHK-API-14's empty id is absent.
    /// <summary>Gets the id of the control's labelable element: the hidden input, or a native button.</summary>
    /// <value>The id a label's <c>for</c> points at.</value>
    public string ControlId => (parameters.ClaimsControlId && scope is not null ? scope.ControlId : null) ?? IdParameter ?? ownId;

    // CHK-ARIA-06 step 3; CKG-ARIA-07: a box that shares its group's scope leaves that scope's label to the group.
    /// <summary>Gets the id of the label in the control's labelable scope.</summary>
    /// <value>
    /// The label's id, or <see langword="null"/> outside a field, without a mounted label, or when the scope is its
    /// group's.
    /// </value>
    public string? LabelId => parameters.ClaimsControlId ? scope?.Label.Id : null;

    // CHK-ARIA-06 step 4, CHK-ARIA-07/09: the fallback is in play only while nothing earlier in the precedence names the
    // control and the control is not a native button.
    /// <summary>Gets the id of the native label the form module found for the hidden input.</summary>
    /// <value>The label's id while the fallback is in play and a label was found; otherwise, <see langword="null"/>.</value>
    public string? FallbackLabelId => LabelFallbackActive ? fallbackLabelId : null;

    // CHK-ARIA-11, CHK-DOM-18.
    /// <summary>Gets a value indicating whether the control renders <c>aria-invalid</c>.</summary>
    /// <value><see langword="true"/> while the field is invalid and neither it nor the control is disabled.</value>
    public bool AriaInvalid => fieldScope is not null && fieldScope.State.Valid == false && !fieldScope.Disabled && !EffectiveDisabled;

    // CHK-STA-01: the field's state with the control's effective disabled state; outside a field, the default state.
    /// <summary>Gets the field's state as the control renders it.</summary>
    /// <value>The field's snapshot with the control's effective disabled state.</value>
    public FieldStateSnapshot Snapshot => (fieldScope?.State ?? FieldStateSnapshot.Default) with { Disabled = EffectiveDisabled };

    /// <summary>Gets a value indicating whether the control is inside a field.</summary>
    /// <value><see langword="true"/> when a <see cref="FieldRoot"/> encloses it.</value>
    public bool InField => fieldScope is not null;

    /// <summary>Gets or sets the refresh a commit the control starts runs, when it is a member of a group.</summary>
    /// <value>The group's refresh of its members' inputs, or <see langword="null"/> to refresh the control's own input.</value>
    public Func<Task>? GroupRefresh { get; set; }

    /// <summary>Gets a value indicating whether the control shows its value as ticked.</summary>
    /// <value>The value the control last took.</value>
    public bool Checked => parameters.Checked;

    /// <summary>Gets the hidden input's validation state.</summary>
    /// <value>The validation input the field reads.</value>
    public FieldValidationInput ValidationInput => validationInput;

    /// <summary>Gets a task that completes when the form module is attached to the root and the hidden input.</summary>
    /// <value>The attach task, for the fixtures.</value>
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
            rootHandle = null;
            inputHandle = null;
        }
    }

    /// <summary>Gets the boxed form of a value, one instance per value.</summary>
    /// <param name="value">The value.</param>
    /// <returns>The shared boxed value.</returns>
    public static object Box(bool value) => value ? s_true : s_false;

    /// <summary>Brings the hidden input's native state up to date from the browser.</summary>
    /// <returns>A task that completes when the input has reported, or at once before it is attached.</returns>
    public Task RefreshInputAsync() => RefreshAsync();

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
    public void SetParameters(CheckableFieldParameters next, IFormScope? formScope)
    {
        parameters = next;
        form = fieldScope?.Form ?? formScope ?? DefaultFormScope.Instance;

        // FFI-FRM-24: a group's validation inputs are in document order, as Base UI's ref callbacks add them, so the
        // representative is the first eligible member; registering after the render's interop would order them by the
        // round trips' completion on Server.
        if (fieldScope is not null && next.RegistersInput && next.Enabled)
        {
            RegisterInput();
        }
        else if (fieldScope is not null)
        {
            UnregisterInput();
        }

        participation.Update(next.EditContext, next.CheckedExpression);
        if (!initialized)
        {
            initialized = true;
            previousDisabled = EffectiveDisabled;
        }
    }

    /// <summary>Takes the value the control shows after a request or a parameter set.</summary>
    /// <param name="isChecked">The value.</param>
    /// <param name="blurValue">The value an onBlur commit validates: the control's own, boxed, or its group's.</param>
    public void SetValue(bool isChecked, object? blurValue) => parameters = parameters with { Checked = isChecked, BlurValue = blurValue };

    /// <summary>Determines whether the control's labelable scope is the given one.</summary>
    /// <param name="other">A scope, such as the one a group renders in, or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when <paramref name="other"/> is the control's scope.</returns>
    public bool SharesScope(LabelableScope? other) => other is not null && ReferenceEquals(scope, other);

    /// <summary>Records what a render of the part shows, so a change of the field renders it again only when needed.</summary>
    public void Rendered()
    {
        rendered = true;
        renderedKey = CurrentKey();
    }

    // CHK-ARIA-10, FFI-ALG-09: the consumer's ids, then the scopes' message ids; outside a field the consumer's as given.
    /// <summary>Composes an element's <c>aria-describedby</c>.</summary>
    /// <param name="consumer">The consumer's <c>aria-describedby</c>, or <see langword="null"/> for the hidden input.</param>
    /// <returns>The composed ids, or <see langword="null"/> when there is none.</returns>
    public string? DescribedBy(string? consumer) => scope is null ? consumer : scope.DescribedBy(consumer);

    // CHK-FOC-04, FFI-ALG-13.
    /// <summary>Acquires the field's focused flag for the control.</summary>
    public void HandleFocus() => fieldScope?.SetFocused(this, true, EffectiveDisabled);

    /// <summary>Runs the work that follows a render: the id claim, the registration, the value reaction and the module.</summary>
    /// <param name="root">The root element, or <see langword="null"/> under a substitute callback.</param>
    /// <param name="rootId">The root element's id, which the hidden input hands focus to.</param>
    /// <param name="registrationId">The id the control registers with its field.</param>
    /// <param name="input">The hidden input, or <see langword="null"/> before it is rendered.</param>
    /// <param name="inputId">The hidden input's id, or <see langword="null"/> when it renders none.</param>
    /// <returns>A task that completes when the module has the options.</returns>
    public async Task AfterRenderAsync(ElementReference? root, string rootId, string registrationId, ElementReference? input, string? inputId)
    {
        if (!parameters.Enabled)
        {
            Unmount();
            await ReleaseHandlesAsync().ConfigureAwait(true);
            return;
        }

        focusTarget = rootId;
        if (fieldScope is null)
        {
            ObserveValue();
        }
        else
        {
            validationInput.ControlId = rootId;
            if (parameters.OwnsValue)
            {
                fieldScope.Validation.InputRef = validationInput;
            }

            using (fieldScope.Batch())
            {
                ClaimControlId();
                SetSink(inputId);
                Register(rootId, registrationId);
                SyncDisabled();
                ObserveValue();
            }
        }

        await AttachOrUpdateAsync(root, rootId, input).ConfigureAwait(true);
    }

    // DP6 (settled by the orchestrator 2026-10-22 under ADR 04 §3's one-way contract): a commit reads the hidden input's
    // native state from a fresh report.
    private Task RefreshAsync()
    {
        if (inputHandle is null)
        {
            return Task.CompletedTask;
        }

        refreshSource ??= new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        revision++;
        refreshRevision = revision;
        SendInputOptions();
        return refreshSource.Task;
    }

    private RenderKey CurrentKey() => fieldScope is null
        ? new RenderKey(Snapshot, ControlId, Name, EffectiveDisabled, null, null, false)
        : new RenderKey(Snapshot, ControlId, Name, EffectiveDisabled, scope!.Label.Id, scope.DescribedBy(null), AriaInvalid);

    // FFI-ALG-07, post-render; only a control that owns its scope's control id claims it (CHK-DOM-15).
    private void ClaimControlId()
    {
        if (scope is null || !parameters.ClaimsControlId)
        {
            ReleaseClaim();
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

    private void ReleaseClaim()
    {
        if (claimRegistered)
        {
            claimRegistered = false;
            scope!.UnregisterControlId(this);
        }
    }

    // CHK-FRM-06: registration while enabled and owning the value; the baseline is the field's (FFI-STA-04).
    private void Register(string rootId, string registrationId)
    {
        if (EffectiveDisabled || !parameters.OwnsValue)
        {
            if (registered)
            {
                registered = false;
                sentRegistration = null;
                fieldScope!.RegisterControl(this, null);
            }

            return;
        }

        FieldControlRegistration next = new(registrationId, OwnName, Box(parameters.Checked), getValue, rootId, refresh);
        if (registered && next == sentRegistration)
        {
            return;
        }

        registered = true;
        sentRegistration = next;
        fieldScope!.RegisterControl(this, next);
    }

    // FFI-FRM-04 (M2 track 6, settled by the orchestrator 2026-10-23 (F2/DP6, ADR 16 rule 6)): the state the module reads from the hidden input when the form
    // submits reaches the validation input by the input's id. A native-button box renders no id on its input, so its
    // input's state at submit is the last one reported.
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

    // FFI-FRM-24, CHK-FRM-19: a member's hidden input is one of the field's validation inputs; a parent's is not.
    private void RegisterInput()
    {
        if (!inputRegistered)
        {
            inputRegistered = true;
            fieldScope!.Validation.RegisterInput(validationInput);
        }
    }

    private void UnregisterInput()
    {
        if (inputRegistered)
        {
            inputRegistered = false;
            fieldScope!.Validation.UnregisterInput(validationInput);
        }
    }

    // FFI-ALG-13 `useSetFieldFocused` effect on `disabled`: release, then the module reports focus when re-enabled.
    private void SyncDisabled()
    {
        bool disabled = EffectiveDisabled;
        if (disabled == previousDisabled)
        {
            return;
        }

        previousDisabled = disabled;
        fieldScope!.SetFocused(this, false, disabled);
    }

    // CHK-STA-10/11, CHK-FRM-09/10/13; checkbox/root/CheckboxRoot.tsx:178-193. Filled on mount and on every change; the rest
    // on every change only, and only for a control that owns its value (CHK-FRM-20).
    private void ObserveValue()
    {
        bool isChecked = parameters.Checked;
        if (syncedChecked is null)
        {
            syncedChecked = isChecked;
            if (parameters.OwnsValue)
            {
                fieldScope?.SetFilled(isChecked);
            }

            return;
        }

        if (syncedChecked == isChecked)
        {
            return;
        }

        syncedChecked = isChecked;
        if (!parameters.OwnsValue)
        {
            return;
        }

        if (fieldScope is null)
        {
            // FLD-FRM-18's reading for a field-less control inside a form: the form's error for its own name is cleared.
            form.ClearErrors(OwnName);
            participation.NotifyChanged();
            return;
        }

        fieldScope.SetFilled(isChecked);
        fieldScope.ClearErrors();
        fieldScope.SetDirty(!Equals(Box(isChecked), fieldScope.ValidityData.InitialValue));
        participation.NotifyChanged();
        fieldScope.Observe(ChangeAfterRefreshAsync(isChecked));
    }

    private async Task ChangeAfterRefreshAsync(bool value)
    {
        await RefreshAsync().ConfigureAwait(true);
        if (fieldScope is not null && !disposed && syncedChecked == value)
        {
            fieldScope.Validation.Change(Box(value));
        }
    }

    private async Task CommitAfterRefreshAsync(object? value)
    {
        await (GroupRefresh ?? refresh)().ConfigureAwait(true);
        if (fieldScope is not null && !disposed)
        {
            await fieldScope.Validation.CommitAsync(value).ConfigureAwait(true);
        }
    }

    private async Task AttachOrUpdateAsync(ElementReference? root, string rootId, ElementReference? input)
    {
        if (twin is null)
        {
            twin = new FormTwin(jsRuntime);
            twin.ControlReported += reported;
        }

        // Compared before constructing, so an unchanged render allocates no options record.
        bool rootChanged = sentRootOptions?.Control is not { } sentRoot
            || sentRoot.Disabled != EffectiveDisabled
            || sentRoot.EnterSubmit != parameters.EnterSubmit
            || sentRoot.ReportEvents != InField
            || !string.Equals(sentRoot.FormId, form.FormToken, StringComparison.Ordinal);
        if (!parameters.EnterSubmit && !InField)
        {
            // No root handle: nothing to report and no Enter path.
        }
        else if (rootHandle is null)
        {
            FormOptions rootOptions = RootOptions();
            sentRootOptions = rootOptions;
            if (root is { } rootElement)
            {
                rootHandle = await twin.AttachAsync(rootElement, rootOptions).ConfigureAwait(true);
            }
            else
            {
                try
                {
                    rootHandle = await twin.AttachAsync(rootId, rootOptions).ConfigureAwait(true);
                }
                catch (JSException)
                {
                    // A substitute that renders no element leaves nothing to attach to; the next render tries again.
                    sentRootOptions = null;
                }
            }
        }
        else if (rootChanged)
        {
            FormOptions rootOptions = RootOptions();
            sentRootOptions = rootOptions;
            await rootHandle.UpdateAsync(rootOptions).ConfigureAwait(true);
        }

        // The hidden input is always attached: it hands focus to the root (CHK-FOC-03), and inside a field it is the
        // validation input; it looks for its native label while the fallback is in play (CHK-ARIA-07).
        if (input is not { } inputElement)
        {
            return;
        }

        if (inputHandle is null)
        {
            FormOptions inputOptions = InputOptions();
            sentInputOptions = inputOptions;
            inputHandle = await twin.AttachAsync(inputElement, inputOptions).ConfigureAwait(true);
            attachedSource.TrySetResult();
        }
        else if (InputOptionsChanged())
        {
            FormOptions inputOptions = InputOptions();
            sentInputOptions = inputOptions;
            await inputHandle.UpdateAsync(inputOptions).ConfigureAwait(true);
        }
    }

    private async Task ReleaseHandlesAsync()
    {
        TwinHandle<FormOptions>? rootReleased = rootHandle;
        TwinHandle<FormOptions>? inputReleased = inputHandle;
        rootHandle = null;
        inputHandle = null;
        sentRootOptions = null;
        sentInputOptions = null;
        if (rootReleased is not null)
        {
            await rootReleased.DisposeAsync().ConfigureAwait(true);
        }

        if (inputReleased is not null)
        {
            await inputReleased.DisposeAsync().ConfigureAwait(true);
        }
    }

    private void SendInputOptions()
    {
        if (inputHandle is null)
        {
            return;
        }

        if (!InputOptionsChanged())
        {
            return;
        }

        FormOptions options = InputOptions();
        sentInputOptions = options;
        Observe(inputHandle.UpdateAsync(options).AsTask());
    }

    // Inside a field the root reports its blur and its focus at attach; with EnterSubmit, Enter submits the owning form.
    private FormOptions RootOptions() => new()
    {
        Control = new FormControlOptions { FormId = form.FormToken, Disabled = EffectiveDisabled, ReportEvents = InField, EnterSubmit = parameters.EnterSubmit },
    };

    private FormOptions InputOptions() => new()
    {
        Control = new FormControlOptions
        {
            FormId = form.FormToken,
            CustomValidity = validationInput.OwnMessage,
            Revision = revision,
            Disabled = EffectiveDisabled,
            ReportEvents = false,
            FocusTarget = focusTarget,
            LabelFallback = LabelFallbackActive,
        },
    };

    // Compared before constructing, so an unchanged render allocates no options record.
    private bool InputOptionsChanged()
    {
        if (sentInputOptions?.Control is not { } sent)
        {
            return true;
        }

        return sent.Revision != revision
            || sent.Disabled != EffectiveDisabled
            || !string.Equals(sent.CustomValidity, validationInput.OwnMessage, StringComparison.Ordinal)
            || !string.Equals(sent.FormId, form.FormToken, StringComparison.Ordinal)
            || !string.Equals(sent.FocusTarget, focusTarget, StringComparison.Ordinal)
            || sent.LabelFallback != LabelFallbackActive;
    }

    private async Task HandleReportAsync(FormControlReport report)
    {
        if (disposed)
        {
            return;
        }

        if (string.Equals(report.Type, LabelReport, StringComparison.Ordinal))
        {
            if (!string.Equals(report.Value, fallbackLabelId, StringComparison.Ordinal))
            {
                fallbackLabelId = report.Value;
                stateHasChanged();
            }

            return;
        }

        if (fieldScope is null)
        {
            return;
        }

        switch (report.Type)
        {
            case SyncReport:
                // Only the hidden input refreshes: its report carries the validity the field reads.
                if (report.Validity is { } validity)
                {
                    validationInput.Report(validity);
                }

                if (refreshSource is { } source && report.Revision >= refreshRevision)
                {
                    refreshSource = null;
                    source.TrySetResult();
                }

                break;
            case BlurReport:
                await HandleBlurAsync().ConfigureAwait(true);
                break;
            case FocusReport:
                fieldScope.SetFocused(this, true, EffectiveDisabled);
                break;
        }
    }

    // CHK-FRM-14, CHK-FOC-04, CHK-FRM-11: touched, then focused released, then the onBlur commit of the current value.
    private Task HandleBlurAsync()
    {
        using (fieldScope!.Batch())
        {
            fieldScope.SetTouched(true);
            fieldScope.SetFocused(this, false, EffectiveDisabled);
        }

        return fieldScope.ValidationMode == ValidationMode.OnBlur ? CommitAfterRefreshAsync(parameters.BlurValue) : Task.CompletedTask;
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

    // Unmounting (disposal, or Enabled set to false): unregister, release the claim, the focused flag and the input.
    private void Unmount()
    {
        if (fieldScope is null)
        {
            return;
        }

        using (fieldScope.Batch())
        {
            if (registered)
            {
                registered = false;
                sentRegistration = null;
                fieldScope.RegisterControl(this, null);
            }

            ReleaseClaim();
            UnregisterInput();
            RemoveSink();
            fieldScope.SetFocused(this, false, EffectiveDisabled);
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

    /// <summary>What a render of the control shows, compared to tell whether a change of the field needs another render.</summary>
    private readonly record struct RenderKey(FieldStateSnapshot State, string Id, string? Name, bool Disabled, string? LabelId, string? DescribedBy, bool AriaInvalid);
}

/// <summary>The parameters of a hidden-input control that its field behavior reads.</summary>
/// <param name="Name">The control's name parameter.</param>
/// <param name="Disabled">The control's own disabled state, a group's included.</param>
/// <param name="Checked">The value the control shows.</param>
/// <param name="BlurValue">The value an onBlur commit validates: the control's own, boxed, or its group's.</param>
/// <param name="OwnsValue">Whether the control is its field's control: it registers, and writes filled and dirty.</param>
/// <param name="ClaimsControlId">Whether the control claims its labelable scope's control id and takes the scope's label.</param>
/// <param name="RegistersInput">Whether the hidden input is one of the field's validation inputs, as a group member's is.</param>
/// <param name="EnterSubmit">Whether Enter on the root submits the form the hidden input belongs to.</param>
/// <param name="LabelFallback">Whether the hidden input's native label may name the control: it is not a native button and the consumer names it no other way.</param>
/// <param name="CheckedExpression">The expression of the bound value.</param>
/// <param name="EditContext">The cascaded edit context.</param>
/// <param name="AdditionalAttributes">The unmatched attributes.</param>
/// <param name="Enabled">Whether the part renders.</param>
internal readonly record struct CheckableFieldParameters(
    string? Name,
    bool Disabled,
    bool Checked,
    object? BlurValue,
    bool OwnsValue,
    bool ClaimsControlId,
    bool RegistersInput,
    bool EnterSubmit,
    bool LabelFallback,
    Expression<Func<bool>>? CheckedExpression,
    EditContext? EditContext,
    IReadOnlyDictionary<string, object>? AdditionalAttributes,
    bool Enabled);

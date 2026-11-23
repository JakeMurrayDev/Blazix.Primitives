using System.Linq.Expressions;
using Microsoft.AspNetCore.Components.Forms;

namespace Iodine.Headless;

// SLD §8, FFI §6 (the Slider rows), composed from Field/ (never edited; ADR 05 Amendments 2026-11-18 item 2, ADR 16 rule 10:
// the SelectField pattern, one internal behavior composed into the Root, never a nested component).
// - Registration (SLD-FRM-02, FFI-ALG-12): after every render that changed it, the field value (SLD-STA-12) boxed once per
//   commit by the Root, the Root's id, the Root's own name as the fallback, and the last-mounted thumb's input as the control
//   a blocked submission focuses (SLD-FOC-07); none while disabled.
// - The validation input: a range input has no native constraint (SLD-FRM-09), so its validity is the default, valid one,
//   and nothing reads its value from the browser; its id is the control's.
// - The value reaction (SLD-FRM-04, SLD-STA-21; slider/root/SliderRoot.tsx:178-191): after the render that shows a new field
//   value, the form's error cleared, the validation's change, then dirty, the reverse of other controls (FFI-EVT-02 allows
//   it for Slider), then the EditContext notified.
// - Touched, focused and the onBlur commit (SLD-FOC-04, SLD-FRM-05/06): from the module's blur classification.
// - `filled` is never written (SLD-STA-22; decision 14, replicated).
/// <summary>The field half of a slider: its control registration, its validation input and its touched and focused flags.</summary>
internal sealed class SliderField : IDisposable
{
    private readonly Action changed;
    private readonly FieldValidationInput validationInput;
    private readonly EditContextParticipation participation;
    private readonly Action fieldChanged;
    private readonly IFormScope formless;
    private FieldRootContext? fieldScope;
    private IFormScope form;
    private LabelableScope? scope;
    private string? name;
    private bool ownDisabled;
    private FieldControlRegistration? sentRegistration;
    private bool registered;
    private FieldRegistry? sinkRegistry;
    private string? sinkId;
    private bool initialized;
    private bool observed;
    private int observedVersion;
    private bool disposed;

    /// <summary>Initializes a new instance of the <see cref="SliderField"/> class.</summary>
    /// <param name="formless">The form scope of the runtime's fields outside a form.</param>
    /// <param name="changed">Called when the field's state that the parts render changed.</param>
    public SliderField(IFormScope formless, Action changed)
    {
        this.formless = formless;
        form = formless;
        this.changed = changed;
        validationInput = new FieldValidationInput(static _ => { });
        participation = new EditContextParticipation(OnEditContextMessages);
        fieldChanged = OnFieldChanged;
    }

    /// <summary>Gets a value indicating whether the slider is inside a field.</summary>
    /// <value><see langword="true"/> inside a <c>FieldRoot</c>.</value>
    public bool InField => fieldScope is not null;

    // SLD-API-05.
    /// <summary>Gets a value indicating whether the slider is disabled, its own flag or its field's.</summary>
    /// <value>The effective disabled flag.</value>
    public bool Disabled => (fieldScope?.Disabled ?? false) || ownDisabled;

    /// <summary>Gets the effective form name: the field's, else the root's own, else the edit context's.</summary>
    /// <value>The name, or <see langword="null"/>.</value>
    public string? Name => fieldScope?.Name ?? name ?? participation.FieldName;

    /// <summary>Gets the field's flags, or the default flags outside a field.</summary>
    /// <value>The flags; <see cref="FieldStateSnapshot.Disabled"/> is not the slider's.</value>
    public FieldStateSnapshot Snapshot => fieldScope?.State ?? FieldStateSnapshot.Default;

    /// <summary>Gets the id of the field's label, or <see langword="null"/>.</summary>
    /// <value>The label id the field's label published.</value>
    public string? FieldLabelId => scope?.Label.Id;

    /// <summary>Gets the field's control id, which the input of a single-thumb slider renders.</summary>
    /// <value>The labelable scope's control id, or <see langword="null"/> outside a field.</value>
    public string? ControlId => scope?.ControlId ?? scope?.DefaultId;

    /// <summary>Gets the form the slider belongs to.</summary>
    /// <value>The field's form, else the cascaded one, else the formless scope.</value>
    public IFormScope Form => form;

    /// <summary>Gets the field's validation mode.</summary>
    /// <value>The mode, or <see cref="ValidationMode.OnSubmit"/> outside a field.</value>
    public ValidationMode ValidationMode => fieldScope?.ValidationMode ?? ValidationMode.OnSubmit;

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
    }

    /// <summary>Takes the cascaded field; called once, when the root initializes.</summary>
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
    /// <param name="expression">The value's expression, or <see langword="null"/>.</param>
    public void SetParameters<TField>(string? ownName, bool disabled, IFormScope? formScope, EditContext? editContext, Expression<Func<TField>>? expression)
    {
        name = ownName;
        ownDisabled = disabled;
        form = fieldScope?.Form ?? formScope ?? formless;
        participation.Update(editContext, expression);
        initialized = true;
    }

    // SLD-ARIA-08/09, FFI-ARIA-01: the consumer's ids, then the field's message ids.
    /// <summary>Composes an element's <c>aria-describedby</c>.</summary>
    /// <param name="consumer">The consumer's <c>aria-describedby</c>, or <see langword="null"/>.</param>
    /// <returns>The composed ids, or <see langword="null"/>.</returns>
    public string? DescribedBy(string? consumer) => scope is null ? consumer : scope.DescribedBy(consumer);

    // SLD-ARIA-08/09: while the field is invalid and neither it nor the element is disabled.
    /// <summary>Determines whether an element renders <c>aria-invalid</c>.</summary>
    /// <param name="elementDisabled">Whether the element itself is disabled.</param>
    /// <returns><see langword="true"/> to render <c>aria-invalid="true"</c>.</returns>
    public bool AriaInvalid(bool elementDisabled) => fieldScope is not null && fieldScope.State.Valid == false && !fieldScope.Disabled && !elementDisabled;

    // SLD-FOC-03/04 (useSetFieldFocused, per thumb input): acquired on an enabled thumb's focus.
    /// <summary>Acquires or releases the field's focused flag for a thumb.</summary>
    /// <param name="owner">The thumb's owner token.</param>
    /// <param name="focused"><see langword="true"/> to acquire the flag.</param>
    /// <param name="ownerDisabled">Whether the thumb is disabled.</param>
    public void SetFocused(object owner, bool focused, bool ownerDisabled) => fieldScope?.SetFocused(owner, focused, ownerDisabled);

    // SLD-FRM-05 (a): a keyboard or native-input candidate that passed the minimum distance, applied or not.
    /// <summary>Marks the field touched.</summary>
    public void MarkTouched() => fieldScope?.SetTouched(true);

    // SLD-FOC-04, SLD-FRM-05 (b), SLD-FRM-06 (SliderThumb.tsx:351-366): a blur to a target outside every thumb.
    /// <summary>Applies a thumb's blur to a target outside every thumb of the slider.</summary>
    /// <param name="owner">The thumb's owner token.</param>
    /// <param name="ownerDisabled">Whether the thumb is disabled.</param>
    /// <param name="value">The value an onBlur commit validates, boxed.</param>
    /// <returns>A task that completes when an onBlur commit has run.</returns>
    public Task HandleBlurOutsideAsync(object owner, bool ownerDisabled, object? value)
    {
        if (fieldScope is null)
        {
            return Task.CompletedTask;
        }

        using (fieldScope.Batch())
        {
            fieldScope.SetTouched(true);
            fieldScope.SetFocused(owner, false, ownerDisabled);
        }

        return fieldScope.ValidationMode == ValidationMode.OnBlur ? fieldScope.Validation.CommitAsync(value) : Task.CompletedTask;
    }

    // A thumb that becomes disabled or unmounts while it owns the flag releases it (SLD-FOC-04; FFI-ALG-13).
    /// <summary>Releases the field's focused flag when the thumb owns it.</summary>
    /// <param name="owner">The thumb's owner token.</param>
    public void Release(object owner) => fieldScope?.SetFocused(owner, false, ownerDisabled: false);

    /// <summary>Runs the work after a render: the registration, the validation input and the value reaction.</summary>
    /// <param name="registrationId">The root's id.</param>
    /// <param name="controlId">The id of the last-mounted thumb's input, or <see langword="null"/> while no thumb is mounted.</param>
    /// <param name="value">The field value, boxed once per commit.</param>
    /// <param name="valueVersion">The store's value counter.</param>
    /// <param name="dirty">Returns whether the field value differs from the initial one.</param>
    public void AfterRender(string registrationId, string? controlId, object? value, int valueVersion, Func<object?, bool> dirty)
    {
        if (fieldScope is null)
        {
            ObserveValue(valueVersion, value, dirty);
            return;
        }

        validationInput.ControlId = controlId;
        fieldScope.Validation.InputRef = validationInput;
        using (fieldScope.Batch())
        {
            SetSink(controlId);
            Register(registrationId, controlId, value);
            ObserveValue(valueVersion, value, dirty);
        }
    }

    private void Register(string registrationId, string? controlId, object? value)
    {
        if (Disabled)
        {
            Unregister();
            return;
        }

        FieldControlRegistration next = new(registrationId, name ?? participation.FieldName, value, null, controlId, null);

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

    // FFI-FRM-04: a form submission's state reaches the validation input by its id.
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

    // SLD-FRM-04, SLD-STA-21 (useValueChanged): a change only, never the first render.
    private void ObserveValue(int valueVersion, object? value, Func<object?, bool> dirty)
    {
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
            // FFI-FRM-26 through the form scope, for a slider outside a field inside a form.
            form.ClearErrors(Name);
            participation.NotifyChanged();
            return;
        }

        fieldScope.ClearErrors();
        fieldScope.Validation.Change(value);
        fieldScope.SetDirty(dirty(fieldScope.ValidityData.InitialValue));
        participation.NotifyChanged();
    }

    private void OnEditContextMessages(IReadOnlyList<string> messages) => fieldScope?.SetExternalErrors(participation, messages);

    private void OnFieldChanged()
    {
        if (!disposed && initialized)
        {
            changed();
        }
    }

    private void Unmount()
    {
        if (fieldScope is null)
        {
            return;
        }

        using (fieldScope.Batch())
        {
            Unregister();
            RemoveSink();
            if (ReferenceEquals(fieldScope.Validation.InputRef, validationInput))
            {
                fieldScope.Validation.InputRef = null;
            }
        }
    }
}

using System.Linq.Expressions;
using Blazix.Primitives.Interop;
using Blazix.Primitives.Rendering;
using Blazix.Primitives.Store;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace Blazix.Primitives;

// RDG-STA-01; radio-group/RadioGroup.tsx:187-192,271-280 (the field's state with three group fields; the value is not in
// it). ADR 08 §3: the state record is sealed, partial and [PartState].
/// <summary>Represents the state of a radio group, which the part exposes as data attributes and passes to its class and style functions.</summary>
/// <param name="Disabled">A value indicating whether every radio button of the group is disabled; projected as <c>data-disabled</c>.</param>
/// <param name="ReadOnly">A value indicating whether the user cannot change the selection; projected as <c>data-readonly</c>.</param>
/// <param name="Required">A value indicating whether a radio button must be selected to submit the form; projected as <c>data-required</c>.</param>
/// <param name="Touched">A value indicating whether a radio button was selected or focus left the group within its field; projected as <c>data-touched</c>.</param>
/// <param name="Dirty">A value indicating whether the value differs from the field's initial one; projected as <c>data-dirty</c>.</param>
/// <param name="Valid">
/// The field's validity: <see langword="true"/> projects <c>data-valid</c>, <see langword="false"/> projects
/// <c>data-invalid</c>, and <see langword="null"/>, before validation, projects neither.
/// </param>
/// <param name="Filled">A value indicating whether a value is selected within the group's field; projected as <c>data-filled</c>.</param>
/// <param name="Focused">A value indicating whether a radio button of the group has focus within its field; projected as <c>data-focused</c>.</param>
[PartState]
public sealed partial record RadioGroupState(bool Disabled, bool ReadOnly, bool Required, bool Touched, bool Dirty, bool? Valid, bool Filled, bool Focused);

// RDG §3.1; radio-group/RadioGroupDataAttributes.ts (only `disabled`, as Base UI exports; §16 Q2).
/// <summary>Provides the names of the data attributes a radio group renders.</summary>
public static class RadioGroupDataAttributes
{
    /// <summary>Represents the attribute present, with an empty value, while the group is disabled: <c>data-disabled</c>.</summary>
    public const string Disabled = "data-disabled";
}

// Base UI's RadioGroup (radio-group/RadioGroup.tsx); the contract is docs/components/radio-group.md.
// - One `div` with role="radiogroup" and no input of its own (RDG-STR-01); the consumer's attributes win (RDG-DOM-06).
// - The value is a Controlled<TValue> fixed at the first parameter set (ADR 05 Amendments 2026-10-18; ADR 08 Amendments
//   2026-10-22: any-typed values, `null` selects nothing, comparisons by Equality.StrictEquals). An uncontrolled group
//   without a DefaultValue holds no value until its first selection (design point R1, settled by the orchestrator
//   2026-10-23), so a non-nullable TValue starts with nothing selected as Base UI's `undefined` does (RDG-API-02).
// - The radios in the subtree read the group through RadioGroupContext<TValue> and run their requests through it:
//   OnValueChange, then ValueChanged and the commit (RDG §7). TValue reaches them through [CascadingTypeParameter].
// - It is the composite root of its radios (CompositeRootBehavior; RDG §11): Shift does not block an arrow, Home and End
//   are not handled, the orientation is both, focus loops, a handled key stops, the radios' disabled state is read from
//   the document, the focus-driven selection is on and the direction comes from the nearest DirectionProvider. The
//   pending-navigation flag and every radio's tabindex are the `composite` module's (ADR 04 Amendments 2026-10-20 and
//   2026-10-22).
// - The registration id is the consumer's id, else one drawn from the generator and never rendered (RDG-API-10; design
//   point G2 of M2 track 6, settled by the orchestrator 2026-10-23).
// - Inside a field the group is the field's control (RDG §8): it registers while enabled, with the representative radio's
//   root as its focus target (RDG-FOC-07) and its eligible-only form projection (RDG-FRM-06); after the render that shows
//   a new value it clears the form's error, writes dirty and filled and validates the change once every radio's input
//   has reported again (RDG-STA-05, RDG-FRM-10/12); the composite module's report of focus leaving the group touches the
//   field, releases its focused flag and commits in onBlur mode (RDG-FOC-04/05, RDG-FRM-11). ADR05-5: EditContext
//   participation through ValueExpression.
/// <summary>
/// Represents a set of radio buttons that share one selected value: Base UI's <c>RadioGroup</c>, rendered as a
/// <c>div</c> with <c>role="radiogroup"</c>. The group is one tab stop, and the arrow keys move focus between its radio
/// buttons and select the one they focus.
/// </summary>
/// <typeparam name="TValue">The type of the value of the selected radio button.</typeparam>
/// <remarks>
/// A <see cref="RadioRoot{TValue}"/> inside the group takes <typeparamref name="TValue"/> from the group when its own
/// <c>Value</c> does not determine it. For a nullable value type such as <c>int?</c>, which lets the consumer clear the
/// selection with <see langword="null"/>, set <c>TValue</c> on each radio button: <c>Value="1"</c> alone is an
/// <see cref="int"/>. A radio button whose <typeparamref name="TValue"/> differs from the group's throws
/// <see cref="InvalidOperationException"/> when it is initialized.
/// </remarks>
[CascadingTypeParameter(nameof(TValue))]
public sealed class RadioGroup<TValue> : PartBase<RadioGroupState>
{
    private const string DefaultTag = "div";

    private static readonly IReadOnlyList<string> s_modifierKeys = ["Shift"];
    private static readonly StateAttributesMapping<RadioGroupState> s_mapping = FieldValidityMapping.Create<RadioGroupState>();

    private readonly EventCallback onKeyDown;
    private readonly Action<EventSnapshot> focusLeft;
    private Controlled<TValue>? valueState;
    private bool valueSupplied;
    private bool defaultValueSupplied;
    private bool selectedOnce;
    private CompositeRootBehavior? composite;
    private RadioGroupContext<TValue>? context;
    private RadioGroupState? state;
    private string? registrationId;
    private RenderFragment? cascade;
    private RenderFragment? cascadedContent;
    private FieldRootContext? fieldScope;
    private LabelableScope? scope;
    private FieldsetRootContext? fieldset;
    private EditContextParticipation? participation;
    private Action? fieldChanged;
    private Func<object?>? getValue;
    private Func<string?>? resolveControlId;
    private bool observed;
    private bool observedSelected;
    private TValue observedValue = default!;
    private object? boxedValue;
    private TValue boxedFor = default!;
    private bool boxed;
    private FieldControlRegistration? sentRegistration;
    private bool registered;
    private bool claimRegistered;
    private string? renderedLabelId;
    private string? renderedDescribedBy;
    private bool disposed;

    /// <summary>Initializes a new instance of the <see cref="RadioGroup{TValue}"/> class.</summary>
    public RadioGroup()
    {
        onKeyDown = CreateHandler<KeyboardEventArgs>("onkeydown", e => composite!.HandleKeyDownAsync(e));
        focusLeft = snapshot => _ = InvokeAsync(OnFocusLeft);
    }

    [Inject]
    private IJSRuntime JSRuntime { get; set; } = null!;

    [Inject]
    private ILogger<RadioGroup<TValue>> Logger { get; set; } = null!;

    [Inject]
    private PartIdGenerator IdGenerator { get; set; } = null!;

    // RDG-API-01/02, RDG-STA-04: the value parameter while it is supplied, the committed value of an uncontrolled group, and
    // the initial default when a controlled value is removed.
    private TValue CurrentValue => valueState!.Value;

    // Design point R1 (settled by the orchestrator 2026-10-23): whether the group holds a value at all.
    private bool HasSelection => (valueState!.IsControlled && valueSupplied) || defaultValueSupplied || selectedOnce;

    // RDG §4 `checkedValue != null`.
    private bool Selected => HasSelection && !RadioValues.IsNull(CurrentValue);

    // RDG-API-04, FFI-STA-11: the field's disabled state with the group's own.
    private bool EffectiveDisabled => Disabled || (fieldScope?.Disabled ?? false);

    // RDG-API-07, FFI-STA-10; ADR 05 Amendments 2026-10-22: the field's name, else Name, else the EditContext identifier's.
    private string? EffectiveName => fieldScope?.Name ?? Name ?? participation!.FieldName;

    private string? IdParameter => AdditionalAttributes is not null
        && AdditionalAttributes.TryGetValue("id", out object? value)
        && value is string { Length: > 0 } text ? text : null;

    [CascadingParameter]
    private FieldRootContext? FieldContext { get; set; }

    [CascadingParameter]
    private FieldItemContext? ItemContext { get; set; }

    [CascadingParameter]
    private FieldsetRootContext? FieldsetContext { get; set; }

    [CascadingParameter]
    private IFormScope? FormScope { get; set; }

    [CascadingParameter]
    private EditContext? CascadedEditContext { get; set; }

    [CascadingParameter]
    private DirectionProviderContext? DirectionContext { get; set; }

    // RDG-API-01, RDG-STA-03; ADR 05 §2, ADR 08 Amendments 2026-10-22
    /// <summary>Gets or sets the value of the selected radio button, when the consumer owns the value.</summary>
    /// <value>
    /// The value. Supplying it at the first render makes the value the consumer's for the lifetime of the group: a
    /// selection then only raises <see cref="OnValueChange"/> and <see cref="ValueChanged"/>. <see langword="null"/>
    /// selects no radio button.
    /// </value>
    [Parameter]
    public TValue Value { get; set; } = default!;

    // ADR 05 §2: the binding half of @bind-Value.
    /// <summary>Gets or sets the callback that receives the new value after a selection that was not canceled.</summary>
    /// <value>The callback <c>@bind-Value</c> supplies, invoked after <see cref="OnValueChange"/>.</value>
    [Parameter]
    public EventCallback<TValue> ValueChanged { get; set; }

    // RDG-API-02
    /// <summary>Gets or sets the value of the radio button initially selected, when the group owns the value.</summary>
    /// <value>
    /// The value. Read at the first render only. Without it no radio button is selected until the user selects one,
    /// whatever <typeparamref name="TValue"/> is.
    /// </value>
    [Parameter]
    public TValue DefaultValue { get; set; } = default!;

    // RDG-API-03, RDG-EVT-01..08
    /// <summary>Gets or sets the callback invoked when the user selects a radio button, before the value changes.</summary>
    /// <value>
    /// The callback, which receives the selected radio button's value and the change details: the reason
    /// <see cref="EventReason.None"/> and the click the radio button's hidden input received. Calling
    /// <see cref="ChangeEventDetails.Cancel"/> keeps the current value.
    /// </value>
    [Parameter]
    public EventCallback<ValueChangeEventArgs<TValue>> OnValueChange { get; set; }

    // RDG-API-04
    /// <summary>Gets or sets a value indicating whether every radio button of the group ignores user interaction.</summary>
    /// <value>
    /// <see langword="true"/> to disable every radio button, whatever its own setting; a disabled field disables the group
    /// too. The default is <see langword="false"/>.
    /// </value>
    [Parameter]
    public bool Disabled { get; set; }

    // RDG-API-05, RDG-EDGE-04
    /// <summary>Gets or sets a value indicating whether the user cannot change the selection.</summary>
    /// <value>
    /// <see langword="true"/> to keep the value while the radio buttons stay focusable, arrow keys still move focus and
    /// the form still submits the value; the default is <see langword="false"/>.
    /// </value>
    [Parameter]
    public bool ReadOnly { get; set; }

    // RDG-API-06; radio-group §16 Q4 (design point R5, settled by the orchestrator 2026-10-23: document only).
    /// <summary>Gets or sets a value indicating whether a radio button must be selected for the form to submit.</summary>
    /// <value><see langword="true"/> to set <c>required</c> on every radio button's hidden input; the default is <see langword="false"/>.</value>
    /// <remarks>
    /// Give a required group a name, through <see cref="Name"/> or its <see cref="FieldRoot"/>. Without one, the browser
    /// treats every hidden input as a group of its own, so each unselected radio button stays invalid after another one
    /// was selected.
    /// </remarks>
    [Parameter]
    public bool Required { get; set; }

    // RDG-API-07
    /// <summary>Gets or sets the name the selected value is submitted under.</summary>
    /// <value>
    /// The name placed on every radio button's hidden input, or <see langword="null"/>. The name of an enclosing
    /// <see cref="FieldRoot"/> takes precedence.
    /// </value>
    [Parameter]
    public string? Name { get; set; }

    // RDG-API-08; ADR 08 Amendments 2026-10-22 (the parameter keeps the name `Form`)
    /// <summary>Gets or sets the id of the form that owns the radio buttons when the group is rendered outside that form.</summary>
    /// <value>The form's id, placed as the <c>form</c> attribute of every radio button's hidden input; or <see langword="null"/>.</value>
    [Parameter]
    public string? Form { get; set; }

    // ADR 05 Amendments 2026-10-22: EditContext participation (issue 76).
    /// <summary>Gets or sets the expression that identifies the bound value in a cascaded <see cref="EditContext"/>.</summary>
    /// <value>
    /// The expression <c>@bind-Value</c> supplies, or <see langword="null"/>. Inside an <see cref="EditContext"/> the group
    /// then notifies the context of every change, takes the identifier's field name when it has no other name, and is
    /// invalid while the context holds messages for it. Without an <see cref="EditContext"/> it does nothing.
    /// </value>
    [Parameter]
    public Expression<Func<TValue>>? ValueExpression { get; set; }

    /// <inheritdoc />
    protected override string DefaultTagName => DefaultTag;

    /// <inheritdoc />
    protected override RadioGroupState State => state!;

    /// <inheritdoc />
    protected override StateAttributesMapping<RadioGroupState>? StateAttributesMapping => s_mapping;

    // RDG-DOM-01 renders no generated id; ADR 02 decision 1: under a substitute callback the composite twin finds the
    // element by id (the Button precedent).
    /// <inheritdoc />
    protected override bool UsesGeneratedId => RenderFunction is not null;

    // For the fixtures (tests/Blazix.Primitives.Fixtures.Client): completes once the composite behavior is attached, so a
    // browser test can wait for its listeners before it presses a key.
    /// <summary>Gets a task that completes when the group's composite behavior is attached to its element.</summary>
    /// <value>A task that completes after the first interactive render has attached it.</value>
    internal Task TwinAttached => composite!.TwinAttached;

    /// <inheritdoc />
    public override Task SetParametersAsync(ParameterView parameters)
    {
        // RDG-API-01/02, RDG-EDGE-01: ownership is fixed at the first parameter set (ADR 05 Amendments 2026-10-18).
        if (valueState is null)
        {
            valueState = new Controlled<TValue>("RadioGroup", "value", Logger);
            defaultValueSupplied = parameters.TryGetValue<TValue>(nameof(DefaultValue), out _);
        }

        valueSupplied = parameters.TryGetValue<TValue>(nameof(Value), out _);
        valueState.SetParameters(parameters, nameof(Value), nameof(DefaultValue));
        return base.SetParametersAsync(parameters);
    }

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        base.OnInitialized();

        // RDG-API-10: the registration id without a consumer id, drawn in render order and never rendered.
        registrationId = IdGenerator.Next();
        composite = new CompositeRootBehavior(JSRuntime, IdGenerator, StateHasChanged, DispatchExceptionAsync, onKeyDown)
        {
            ModifierKeys = s_modifierKeys,
            EnableHomeAndEndKeys = false,
            SelectOnNavigation = true,
            SelectionDecider = DecideSelection,
        };
        context = new RadioGroupContext<TValue>(RequestAsync, OnRadioRendered);
        participation = new EditContextParticipation(OnEditContextMessages);
        fieldScope = FieldContext;
        fieldset = FieldsetContext;
        fieldChanged = OnFieldChanged;
        fieldset?.AddListener(fieldChanged);
        if (fieldScope is not null)
        {
            scope = FieldItemContext.LabelableOf(fieldScope, ItemContext);
            context.Scope = scope;
            getValue = GetFormValue;
            resolveControlId = ResolveControlId;
            fieldScope.AddListener(fieldChanged);

            // RDG-FOC-04/05: the leaving blur matters only to a field; outside one the module clears its flag alone.
            composite.ReportFocusLeave = true;
            composite.FocusLeft += focusLeft;
        }
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        composite!.SetDirectionContext(DirectionContext);
        composite.SetConsumerAttributes(AdditionalAttributes);
        participation!.Update(CascadedEditContext, ValueExpression);
        UpdateState();
        PublishState();

        // The cascade wraps the consumer's content inside the group's element: the group's context, then the composite
        // registry; it is rebuilt only when the consumer supplied other content (the Meter precedent).
        if (cascade is null || (!ReferenceEquals(ChildContent, cascade) && !ReferenceEquals(cascadedContent, ChildContent)))
        {
            cascadedContent = ChildContent;
            cascade = context!.Cascade(composite.Context.Cascade(cascadedContent));
        }

        ChildContent = cascade;
    }

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (disposed)
        {
            return;
        }

        await composite!.AfterRenderAsync(Enabled, Element, IdParameter ?? GeneratedId);
        ReactToValue();
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing && !disposed)
        {
            disposed = true;
            participation?.Dispose();
            composite?.Dispose();
            if (fieldChanged is not null)
            {
                fieldset?.RemoveListener(fieldChanged);
            }

            if (fieldScope is not null)
            {
                composite!.FocusLeft -= focusLeft;
                fieldScope.RemoveListener(fieldChanged!);
                using (fieldScope.Batch())
                {
                    Unmount();
                }
            }
        }

        base.Dispose(disposing);
    }

    // RDG-ARIA-01..05: the consumer's role and aria-labelledby win at the merge; a consumer aria-label suppresses nothing
    // (RDG §16 Q7). RDG-DOM-05: no tabindex, aria-orientation, name, value or form.
    /// <inheritdoc />
    protected override void AddPartAttributes(AttributeList attributes)
    {
        composite!.AddAttributes(attributes, IdParameter ?? GeneratedId);
        attributes.Set("role", "radiogroup");
        if (Required)
        {
            attributes.Set("aria-required", "true");
        }

        if (EffectiveDisabled)
        {
            attributes.Set("aria-disabled", "true");
        }

        if (ReadOnly)
        {
            attributes.Set("aria-readonly", "true");
        }

        // RDG-ARIA-05, FFI-ARIA-08/09: the field's label, else the nearest fieldset's legend.
        renderedLabelId = scope?.Label.Id ?? fieldset?.Legend.Id;
        if (renderedLabelId is not null)
        {
            attributes.Set("aria-labelledby", renderedLabelId);
        }
    }

    // RDG-ARIA-06: the consumer's ids, then the field scope's description and error ids. RDG-ARIA-07: aria-invalid while the
    // field is invalid and neither it nor the group is disabled.
    /// <inheritdoc />
    protected override void FinishAttributes(AttributeList attributes)
    {
        if (scope is null)
        {
            return;
        }

        renderedDescribedBy = scope.DescribedBy(null);
        string? consumer = attributes.TryGetValue("aria-describedby", out AttributeValue current) && current.TryGetValue(out string text) ? text : null;
        if (scope.DescribedBy(consumer) is { } describedBy)
        {
            attributes.Set("aria-describedby", describedBy);
        }
        else
        {
            attributes.Remove("aria-describedby");
        }

        if (AriaInvalid())
        {
            attributes.Set("aria-invalid", "true");
        }
    }

    /// <inheritdoc />
    protected override bool ForwardsConsumerHandler(string eventName) => composite!.ForwardsConsumerHandler(eventName);

    // RDG §7, RDG-EVT-01..05: the group's half of a radio's request, with the radio's details. OnValueChange runs before the
    // commit, and a cancel stops it.
    private async Task<bool> RequestAsync(TValue next, ChangeEventDetails details)
    {
        if (!await valueState!.NotifyAsync(next, details, OnValueChange))
        {
            return false;
        }

        await valueState.CommitAsync(next, ValueChanged);
        if (!valueState.IsControlled)
        {
            selectedOnce = true;
        }

        // The radios whose state changed render as they are notified, and the first to finish its render runs the value
        // reaction (RadioRendered); rendering the group instead would render every radio again.
        PublishState();
        if (UpdateState())
        {
            StateHasChanged();
        }

        return true;
    }

    private void PublishState() =>
        context!.SetState(CurrentValue, HasSelection, EffectiveDisabled, ReadOnly, Required, EffectiveName, Form);

    // RAD-FOC-05: the focused radio's consumer onfocus may veto the focus-driven selection on WebAssembly.
    private DecisionPolicy DecideSelection(EventSnapshot snapshot) =>
        context!.Find(snapshot.Target?.Id) is { } radio ? radio.DecideSelection() : DecisionPolicy.Run;

    // A radio's render after a commit: the value reaction, once per new value.
    private void OnRadioRendered()
    {
        if (!observed || observedSelected != Selected || !Equality.Is(observedValue, CurrentValue))
        {
            ReactToValue();
        }
    }

    // After a render, and after a commit the radios have rendered: the registration and the value reaction.
    private void ReactToValue()
    {
        if (fieldScope is null)
        {
            ObserveValue();
            return;
        }

        using (fieldScope.Batch())
        {
            if (Enabled)
            {
                ClaimControlId();
                Register();
                ObserveValue();
            }
            else
            {
                Unmount();
            }
        }
    }

    // RDG-FRM-05, FFI-ALG-12; radio-group/RadioGroup.tsx:169 (useRegisterFieldControl). Registered while enabled, with the
    // group's own name; the handle resolves to the representative radio's root when a form blocks a submission.
    private void Register()
    {
        if (EffectiveDisabled)
        {
            Unregister();
            return;
        }

        object? value = BoxedValue();
        FieldControlRegistration next = new(
            IdParameter ?? registrationId,
            Name ?? participation!.FieldName,
            value,
            getValue,
            fieldScope!.Validation.Representative()?.ControlId,
            context!.RefreshMembersAsync,
            ResolveControlId: resolveControlId);

        // The record compares the boxed value with Equals, which is structural for a record; a new value is a new box, so
        // the identity check re-registers every change (RDG-STA-02: values are kept by identity).
        if (registered && next == sentRegistration && ReferenceEquals(value, sentRegistration!.Value))
        {
            return;
        }

        registered = true;
        sentRegistration = next;
        fieldScope.RegisterControl(this, next);
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

    // Design point R6 (settled by the orchestrator 2026-10-23), CKG-ARIA-03's precedent: post-render, the group claims
    // its scope's control id as null, so a Field.Label in the scope names the group and points at no single radio's
    // input.
    private void ClaimControlId()
    {
        if (!claimRegistered)
        {
            claimRegistered = true;
            scope!.RegisterControlId(this, null);
        }
    }

    private void Unmount()
    {
        Unregister();
        if (claimRegistered)
        {
            claimRegistered = false;
            scope!.UnregisterControlId(this);
        }
    }

    // RDG-FOC-07, FFI-FOC-02/04: the representative radio's root when the form blocks a submission; none when no radio is
    // eligible, so the form focuses the next invalid field.
    private string? ResolveControlId() => fieldScope!.Validation.Representative()?.ControlId;

    // RDG-FRM-06: outside a form the raw value; inside one only while a selected radio's input is eligible.
    private object? GetFormValue()
    {
        if (fieldScope!.Form.FormToken is null || context!.AnySubmitted())
        {
            return BoxedValue();
        }

        return null;
    }

    // The registration and the validator take the value as an object (FieldControlRegistration.Value and
    // FieldValidationArguments.Value are `object?`, ADR 05's note of 2026-10-22); the value is boxed once per committed
    // value, so a render boxes nothing.
    private object? BoxedValue()
    {
        if (!Selected)
        {
            return null;
        }

        TValue value = CurrentValue;
        if (!boxed || !Equality.Is(boxedFor, value))
        {
            boxed = true;
            boxedFor = value;
            boxedValue = value;
        }

        return boxedValue;
    }

    // RDG-STA-05, RDG-FRM-10/12; radio-group/RadioGroup.tsx:171-183 (useValueChanged). Nothing on mount (a radio mounted
    // checked fills the field, RAD-STA-05); on every change: the form's error cleared, dirty, filled, the EditContext
    // notified, and the change validated once every radio's input has reported again. Observed by Object.is and acted on
    // by strict inequality (ValueChangedObserver's rule), with "no value" a value of its own (design point R1).
    private void ObserveValue()
    {
        bool selected = Selected;
        TValue value = CurrentValue;
        if (!observed)
        {
            observed = true;
            observedSelected = selected;
            observedValue = value;
            return;
        }

        if (observedSelected == selected && (!selected || Equality.Is(observedValue, value)))
        {
            return;
        }

        bool changed = observedSelected != selected || !Equality.StrictEquals(observedValue, value);
        observedSelected = selected;
        observedValue = value;
        if (!changed)
        {
            return;
        }

        if (fieldScope is null)
        {
            // FFI-FRM-26 through the form scope, for a group outside a field inside a form.
            FormScope?.ClearErrors(EffectiveName);
            participation!.NotifyChanged();
            return;
        }

        fieldScope.ClearErrors();
        fieldScope.SetDirty(!SameAsInitial(selected, value));
        fieldScope.SetFilled(selected);
        participation!.NotifyChanged();
        fieldScope.Observe(ChangeAfterRefreshAsync(BoxedValue()));
    }

    // FFI-STA-04/05: strictly unequal to the value the field captured at its first registration.
    private bool SameAsInitial(bool selected, TValue value)
    {
        object? initial = fieldScope!.ValidityData.InitialValue;
        if (!selected)
        {
            return initial is null;
        }

        return initial is TValue captured && Equality.StrictEquals(captured, value);
    }

    private async Task ChangeAfterRefreshAsync(object? value)
    {
        await context!.RefreshMembersAsync().ConfigureAwait(true);
        if (!disposed && ReferenceEquals(value, BoxedValue()))
        {
            fieldScope!.Validation.Change(value);
        }
    }

    // RDG-FOC-04/05, FFI-FRM-03, RDG-FRM-11: focus left the group (the composite module's report): touched, the focused
    // flag released, and in onBlur mode one commit of the value once every radio's input has reported again.
    private void OnFocusLeft()
    {
        if (disposed || fieldScope is null)
        {
            return;
        }

        using (fieldScope.Batch())
        {
            fieldScope.SetTouched(true);
            context!.ReleaseFocused(fieldScope);
        }

        if (fieldScope.ValidationMode == ValidationMode.OnBlur)
        {
            fieldScope.Observe(CommitAfterRefreshAsync());
        }
    }

    private async Task CommitAfterRefreshAsync()
    {
        await context!.RefreshMembersAsync().ConfigureAwait(true);
        if (!disposed)
        {
            await fieldScope!.Validation.CommitAsync(BoxedValue()).ConfigureAwait(true);
        }
    }

    // FFI-ARIA-03.
    private bool AriaInvalid() => fieldScope is not null && fieldScope.State.Valid == false && !EffectiveDisabled;

    // ADR 05 Amendments 2026-10-22 item 3.
    private void OnEditContextMessages(IReadOnlyList<string> messages) => fieldScope?.SetExternalErrors(participation!, messages);

    // A change of the field's state, name, label, descriptions or disabled state, or of the fieldset's legend; a render
    // re-runs the registration.
    private void OnFieldChanged()
    {
        PublishState();
        if (UpdateState()
            || (fieldScope is not null && registered == EffectiveDisabled)
            || !string.Equals(scope?.Label.Id ?? fieldset?.Legend.Id, renderedLabelId, StringComparison.Ordinal)
            || !string.Equals(scope?.DescribedBy(null), renderedDescribedBy, StringComparison.Ordinal))
        {
            StateHasChanged();
        }
    }

    // Returns whether the state changed; a new record is made only then (ADR 16 rule 7). RDG-STA-01: the field's state with
    // the group's effective disabled state and its own read-only and required states; outside a field, the default state.
    private bool UpdateState()
    {
        FieldStateSnapshot snapshot = (fieldScope?.State ?? FieldStateSnapshot.Default) with { Disabled = EffectiveDisabled };
        if (state is { } current
            && current.Disabled == snapshot.Disabled
            && current.ReadOnly == ReadOnly
            && current.Required == Required
            && current.Touched == snapshot.Touched
            && current.Dirty == snapshot.Dirty
            && current.Valid == snapshot.Valid
            && current.Filled == snapshot.Filled
            && current.Focused == snapshot.Focused)
        {
            return false;
        }

        state = new RadioGroupState(snapshot.Disabled, ReadOnly, Required, snapshot.Touched, snapshot.Dirty, snapshot.Valid, snapshot.Filled, snapshot.Focused);
        return true;
    }
}

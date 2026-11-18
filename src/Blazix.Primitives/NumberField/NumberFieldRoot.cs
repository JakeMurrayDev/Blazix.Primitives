using System.Globalization;
using System.Linq.Expressions;
using Blazix.Primitives.Rendering;
using Blazix.Primitives.Store;
using Blazix.Primitives.Utilities;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace Blazix.Primitives;

// Base UI's NumberField.Root (number-field/root/NumberFieldRoot.tsx); the contract is docs/components/number-field.md.
// - The element is a div with the state attributes only (NUM §5.1); after it the part renders the visually hidden
//   `<input type="number">` that submits the raw value (BuildAfterElement, ADR 02 Amendments 2026-10-22; NUM-STR-01,
//   NUM-DOM-06/07).
// - The value is a Controlled<double?> fixed at the first parameter set (NUM-STA-03, NUM-EDGE-04). Every request passes
//   through the step pipeline (Utilities/NumberStepValidation, NUM-STA-07..12) in SetValueAsync, Base UI's setValue, with
//   the firing rule of NUM-STA-18 and the cancel semantics of ADR 05 Amendments 2026-11-18 item 1 (decision 9 (a)): a typed
//   or cleared change commits regardless of Cancel(), every other reason is not stored when canceled (NUM-EVT-04).
// - NUM §14's instance fields are this part's: the sync flag (AllowInputSync), the last changed value and the pending
//   commit; the display text follows the value while the flag is set (NUM-STA-06, re-derived in UpdateState).
// - The text is formatted by Utilities/NumberFormatter (NUM-I18N-01); the formatter is resolved once per (culture, format)
//   pair and the text once per (value, formatter), so an unchanged render formats nothing (NUM-I18N-13).
// - Inside a field the number field is the field's control (NUM §8): NumberFieldField claims the visible input's id,
//   registers the value while a NumberFieldInput is mounted, makes the hidden input the validation input through the
//   `form` module (whose `value` it owns after attach, C# → JS) and reacts to each new value; ADR 05 Amendments
//   2026-10-22 and 2026-11-18 item 2: the cascaded EditContext through ValueExpression.
/// <summary>
/// Represents a numeric text field with optional stepper buttons: Base UI's <c>NumberField.Root</c>, rendered as a
/// <c>div</c> followed by a visually hidden <c>input type="number"</c> that takes part in forms and native validation.
/// </summary>
/// <remarks>
/// <para>
/// The value is the consumer's when <see cref="Value"/> is supplied at the first render (<c>@bind-Value</c> works, and
/// <see langword="null"/> is a valid empty value), and the field's own otherwise, starting from <see cref="DefaultValue"/>.
/// Every change is validated: snapped to the step grid when <see cref="SnapOnStep"/> is set, clamped to
/// <see cref="Min"/> and <see cref="Max"/>, and rounded to the precision <see cref="Format"/> shows.
/// </para>
/// <para>
/// The text the input shows is the value formatted under <see cref="Culture"/> and <see cref="Format"/>; while the user
/// types, the typed text stays until the input loses focus or a step changes the value.
/// </para>
/// </remarks>
public sealed class NumberFieldRoot : PartBase<NumberFieldRootState>
{
    private const string DefaultTag = "div";
    private const string NumericInputMode = "numeric";
    private const string DecimalInputMode = "decimal";
    private const string TextInputMode = "text";

    private static readonly StateAttributesMapping<NumberFieldRootState> s_mapping = FieldValidityMapping.Create<NumberFieldRootState>();

    private readonly NumberFieldRootContext context;
    private readonly Action fieldChanged;
    private readonly Action<ElementReference> captureHiddenInput;
    private Controlled<double?>? value;
    private NumberFieldField? fieldHalf;
    private ElementReference? hiddenInput;
    private string? renderedHiddenValue;
    private bool hiddenValueHandedOver;
    private int valueVersion;
    private double? versionedValue;
    private object? boxedValue;
    private int mountedInputs;
    private string? inputId;
    private string inputValue = string.Empty;
    private NumberFormatter? formatter;
    private double? formattedValue;
    private NumberFormatter? formattedWith;
    private string formattedText = string.Empty;
    private bool scrubbing;
    private NumberFieldRootState? state;
    private RenderFragment? cascade;
    private RenderFragment? cascadedContent;

    /// <summary>Initializes a new instance of the <see cref="NumberFieldRoot"/> class.</summary>
    public NumberFieldRoot()
    {
        context = new NumberFieldRootContext(this);
        fieldChanged = Refresh;
        captureHiddenInput = element => hiddenInput = element;
    }

    [Inject]
    private ILogger<NumberFieldRoot> Logger { get; set; } = null!;

    [Inject]
    private PartIdGenerator IdGenerator { get; set; } = null!;

    [Inject]
    private IJSRuntime JSRuntime { get; set; } = null!;

    [Inject]
    private DefaultFormScope Formless { get; set; } = null!;

    [Inject]
    private PlatformFlags Platform { get; set; } = null!;

    [CascadingParameter]
    private FieldRootContext? FieldContext { get; set; }

    [CascadingParameter]
    private IFormScope? FormScope { get; set; }

    [CascadingParameter]
    private EditContext? CascadedEditContext { get; set; }

    // NUM-API-01; ADR 05 §2 and Amendments 2026-11-18 item 2 (decision 13).
    /// <summary>Gets or sets the value, when the consumer owns it.</summary>
    /// <value>
    /// The number, or <see langword="null"/> for an empty field. Supplying it at the first render, <see langword="null"/>
    /// included, makes the value the consumer's for the lifetime of the field: a change then only raises
    /// <see cref="OnValueChange"/> and <see cref="ValueChanged"/>.
    /// </value>
    [Parameter]
    public double? Value { get; set; }

    // ADR 05 §2: the binding half of @bind-Value.
    /// <summary>Gets or sets the callback that receives the new value after a change that was not canceled.</summary>
    /// <value>The callback <c>@bind-Value</c> supplies, invoked after <see cref="OnValueChange"/>.</value>
    [Parameter]
    public EventCallback<double?> ValueChanged { get; set; }

    // NUM-API-02
    /// <summary>Gets or sets the initial value, when the field owns its value.</summary>
    /// <value>The number, or <see langword="null"/>, the default, for an empty field. Read at the first render only.</value>
    [Parameter]
    public double? DefaultValue { get; set; }

    // NUM-API-03, NUM-EVT-01..13
    /// <summary>Gets or sets the callback invoked when the value is about to change.</summary>
    /// <value>
    /// The callback, which receives the validated next value and the change details. The details are a
    /// <see cref="NumberFieldChangeEventDetails"/>, so <c>((NumberFieldChangeEventDetails)args.Details).Direction</c> reads
    /// the direction of a step. Calling <see cref="ChangeEventDetails.Cancel"/> keeps the current value for a step, a key,
    /// a paste or a blur; typing and clearing still change the value, as the browser has already changed the text.
    /// </value>
    [Parameter]
    public EventCallback<ValueChangeEventArgs<double?>> OnValueChange { get; set; }

    // NUM-API-05
    /// <summary>Gets or sets the minimum value.</summary>
    /// <value>The minimum, or <see langword="null"/>, the default, for none (the smallest safe integer, -(2^53 - 1)).</value>
    [Parameter]
    public double? Min { get; set; }

    // NUM-API-05
    /// <summary>Gets or sets the maximum value.</summary>
    /// <value>The maximum, or <see langword="null"/>, the default, for none (the largest safe integer, 2^53 - 1).</value>
    [Parameter]
    public double? Max { get; set; }

    // NUM-API-06
    /// <summary>Gets or sets the amount a key, a stepper button, the wheel or a scrub changes the value by.</summary>
    /// <value>
    /// The step, or <see langword="null"/> for 1. <see cref="StepValue.Any"/> steps by 1 and lets the browser accept any
    /// value; a number is also the hidden input's <c>step</c>, which the browser validates the value against.
    /// </value>
    [Parameter]
    public StepValue? Step { get; set; }

    // NUM-API-07
    /// <summary>Gets or sets the step used while the Alt key is held.</summary>
    /// <value>The small step; the default is 0.1.</value>
    [Parameter]
    public double SmallStep { get; set; } = 0.1;

    // NUM-API-07
    /// <summary>Gets or sets the step used while the Shift key is held.</summary>
    /// <value>The large step; the default is 10.</value>
    [Parameter]
    public double LargeStep { get; set; } = 10;

    // NUM-API-08
    /// <summary>Gets or sets a value indicating whether a step snaps the value to the step grid.</summary>
    /// <value>
    /// <see langword="true"/> to snap step results to multiples of the step from <see cref="Min"/> (or 0); typed text is
    /// never snapped. The default is <see langword="false"/>.
    /// </value>
    [Parameter]
    public bool SnapOnStep { get; set; }

    // NUM-API-09
    /// <summary>Gets or sets a value indicating whether typed text may leave the range of <see cref="Min"/> and <see cref="Max"/>.</summary>
    /// <value>
    /// <see langword="true"/> to keep typed, pasted and cleared values unclamped, so the browser's range validation can
    /// report them, while steps still clamp; the default is <see langword="false"/>.
    /// </value>
    [Parameter]
    public bool AllowOutOfRange { get; set; }

    // NUM-API-10
    /// <summary>Gets or sets a value indicating whether the mouse wheel over the focused input steps the value.</summary>
    /// <value><see langword="true"/> to step with the wheel; the default is <see langword="false"/>.</value>
    [Parameter]
    public bool AllowWheelScrub { get; set; }

    // NUM-API-11; ADR 08 Amendments 2026-11-18 (decision 10 (a)).
    /// <summary>Gets or sets the options that format the value the input shows.</summary>
    /// <value>
    /// The options, or <see langword="null"/>, the default, for grouping and at most three fraction digits. The options also
    /// decide how typed text is parsed and the precision steps and typed values are rounded to.
    /// </value>
    [Parameter]
    public NumberFormatOptions? Format { get; set; }

    // NUM-API-12, NUM-I18N-03
    /// <summary>Gets or sets the culture the value is formatted and parsed under.</summary>
    /// <value>
    /// The culture, or <see langword="null"/>, the default, for <see cref="CultureInfo.CurrentCulture"/>; with
    /// <see langword="null"/>, typed Arabic-Indic or Persian digits are also read as Arabic and Han numerals as Chinese.
    /// </value>
    [Parameter]
    public CultureInfo? Culture { get; set; }

    // NUM-API-13
    /// <summary>Gets or sets a value indicating whether the field ignores user interaction.</summary>
    /// <value>
    /// <see langword="true"/> to disable the input, the stepper buttons and the scrub areas and to keep the value out of
    /// form submission; otherwise, <see langword="false"/>. The default is <see langword="false"/>.
    /// </value>
    [Parameter]
    public bool Disabled { get; set; }

    // NUM-API-14
    /// <summary>Gets or sets a value indicating whether the user cannot change the value.</summary>
    /// <value>
    /// <see langword="true"/> to keep the value while the input stays focusable and the value submits with its form; the
    /// default is <see langword="false"/>.
    /// </value>
    [Parameter]
    public bool ReadOnly { get; set; }

    // NUM-API-15
    /// <summary>Gets or sets a value indicating whether the field must have a value for its form to submit.</summary>
    /// <value><see langword="true"/> to require a value; the default is <see langword="false"/>.</value>
    [Parameter]
    public bool Required { get; set; }

    // NUM-API-16
    /// <summary>Gets or sets the name the value is submitted under.</summary>
    /// <value>The name placed on the hidden input, or <see langword="null"/> for none. An enclosing field's name wins.</value>
    [Parameter]
    public string? Name { get; set; }

    // NUM-API-17; ADR 08 Amendments 2026-10-22 (the parameter keeps the name `Form`).
    /// <summary>Gets or sets the id of the form that owns the field when it is rendered outside that form.</summary>
    /// <value>The form's id, placed as the hidden input's <c>form</c> attribute; or <see langword="null"/>.</value>
    [Parameter]
    public string? Form { get; set; }

    // NUM-API-18
    /// <summary>Gets or sets the id of the visible input.</summary>
    /// <value>The id rendered on <see cref="NumberFieldInput"/>, or <see langword="null"/> for a generated one.</value>
    [Parameter]
    public new string? Id { get; set; }

    // ADR 05 Amendments 2026-10-22 and 2026-11-18 item 2 (decision 14 (a)): EditContext participation.
    /// <summary>Gets or sets the expression of the bound value, which <c>@bind-Value</c> supplies.</summary>
    /// <value>
    /// The expression that identifies the bound value; <see langword="null"/> by default. Inside an <c>EditForm</c> or any
    /// other ancestor that cascades an <see cref="EditContext"/>, the field notifies the context of every committed change,
    /// takes the identifier's field name as its name when <see cref="Name"/> is <see langword="null"/>, and inside a
    /// <see cref="FieldRoot"/> shows the context's messages for the identifier as the field's errors. Without an edit
    /// context it has no effect.
    /// </value>
    [Parameter]
    public Expression<Func<double?>>? ValueExpression { get; set; }

    /// <inheritdoc />
    protected override string DefaultTagName => DefaultTag;

    /// <inheritdoc />
    protected override NumberFieldRootState State => state!;

    /// <inheritdoc />
    protected override StateAttributesMapping<NumberFieldRootState>? StateAttributesMapping => s_mapping;

    // NUM-API-18: the labelable control id, the consumer's or the one drawn at first render.
    /// <summary>Gets the id of the visible input.</summary>
    internal string InputId => Id ?? inputId!;

    /// <summary>Gets the minimum used for clamping and the boundaries: <see cref="Min"/>, else the smallest safe integer.</summary>
    internal double MinWithDefault => Min ?? NumberStepValidation.MinSafeInteger;

    /// <summary>Gets the maximum used for clamping and the boundaries: <see cref="Max"/>, else the largest safe integer.</summary>
    internal double MaxWithDefault => Max ?? NumberStepValidation.MaxSafeInteger;

    /// <summary>Gets the snapping base when no minimum is set: <see cref="Min"/>, else 0.</summary>
    internal double MinWithZeroDefault => Min ?? 0;

    // NUM-API-13: the field's disabled state OR'd with the part's.
    /// <summary>Gets the field's effective disabled state.</summary>
    internal bool EffectiveDisabled => fieldHalf!.Disabled;

    // NUM-API-16: the field's name, else the part's own, else the EditContext identifier's field name.
    /// <summary>Gets the effective form name.</summary>
    internal string? EffectiveName => fieldHalf!.Name;

    /// <summary>Gets the field half.</summary>
    internal NumberFieldField Field => fieldHalf!;

    // NUM-STA-19: `numeric`, and on iOS `decimal` when no value can be negative, else `text` (the iOS numeric keyboard has no
    // minus key). The iOS flag arrives after the first interactive render (PlatformFlags; ADR 01's row like MNU-A11Y-01's).
    /// <summary>Gets the visible input's <c>inputmode</c>.</summary>
    internal string InputMode => Platform.Ios == true ? (MinWithDefault >= 0 ? DecimalInputMode : TextInputMode) : NumericInputMode;

    /// <summary>Gets the formatter of the current culture and format.</summary>
    internal NumberFormatter Formatter => formatter ??= NumberFormatter.Get(Culture, Format);

    // NUM §14 "sync allowed": typing and pasting clear it; blur and every step set it again.
    /// <summary>Gets or sets a value indicating whether the display text follows the value.</summary>
    internal bool AllowInputSync { get; set; } = true;

    // NUM §14 "last changed value": the validated value of the last request, reported or not.
    /// <summary>Gets or sets the validated value of the last request.</summary>
    internal double? LastChangedValue { get; set; }

    // NUM §14 "pending programmatic change": a change reported since the last commit.
    /// <summary>Gets or sets a value indicating whether a change was applied since the last commit.</summary>
    internal bool HasPendingCommit { get; set; }

    // Base UI's valueRef: the value of the last render, which a stepper moves ahead within one interaction.
    /// <summary>Gets or sets the value a step starts from.</summary>
    internal double? ValueRef { get; set; }

    /// <summary>Gets the value of the last parameter set or committed request.</summary>
    internal double? CurrentValue => value!.Value;

    /// <summary>Gets the text the visible input shows.</summary>
    internal string InputValue => inputValue;

    /// <inheritdoc />
    public override Task SetParametersAsync(ParameterView parameters)
    {
        // NUM-STA-03, NUM-EDGE-04: ownership is fixed at the first parameter set (ADR 05 Amendments 2026-10-18).
        value ??= new Controlled<double?>("NumberField", "value", Logger);
        value.SetParameters(parameters, nameof(Value), nameof(DefaultValue));
        return base.SetParametersAsync(parameters);
    }

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        base.OnInitialized();

        // NUM-API-18: the input's id is drawn in render order (ADR 02 decision 5), whether or not an Input part renders.
        inputId = IdGenerator.Next();
        fieldHalf = new NumberFieldField(Formless, JSRuntime, fieldChanged, InvokeAsync, DispatchExceptionAsync);
        fieldHalf.Initialize(FieldContext);
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        formatter = NumberFormatter.Get(Culture, Format);
        fieldHalf!.SetParameters(Name, Disabled, FormScope, CascadedEditContext, ValueExpression);
        // A changed state notifies the parts; otherwise they are notified for the parameters they read beside the state
        // (the bounds of the steppers, the input's id and mode).
        if (!UpdateState())
        {
            context.Notify();
        }

        // The cascade wraps the consumer's content inside the root's element; it is rebuilt only when the consumer supplied
        // other content, and a parameter set without ChildContent keeps the cascade (the Meter precedent).
        if (cascade is null || (!ReferenceEquals(ChildContent, cascade) && !ReferenceEquals(cascadedContent, ChildContent)))
        {
            cascadedContent = ChildContent;
            cascade = context.Cascade(cascadedContent);
        }

        ChildContent = cascade;
    }

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        // NUM-STA-19: the platform flags are read once per scope from the first after-render, which a prerender never runs
        // (the MenuRootBehavior precedent); a read that fails leaves `numeric`.
        if (firstRender && Platform.Ios is null)
        {
            _ = ReadPlatformAsync();
        }

        double? current = CurrentValue;
        await fieldHalf!.AfterRenderAsync(InputId, mountedInputs > 0, hiddenInput, BoxedValue(), valueVersion, Serialize(current), current.HasValue);
        if (!hiddenValueHandedOver && fieldHalf.TwinAttached.IsCompleted)
        {
            hiddenValueHandedOver = true;
        }
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            fieldHalf?.Dispose();
        }

        base.Dispose(disposing);
    }

    // NUM-STR-01, NUM-DOM-06/07, NUM-ARIA-10; number-field/root/NumberFieldRoot.tsx:500-540.
    /// <inheritdoc />
    protected override void BuildAfterElement(RenderTreeBuilder builder)
    {
        // The handover at attach (ADR 04 Amendments 2026-10-18): the value is rendered until the `form` module owns it.
        if (!hiddenValueHandedOver)
        {
            renderedHiddenValue = Serialize(CurrentValue);
        }

        string? name = EffectiveName;
        builder.OpenElement(0, "input");
        builder.AddAttribute(1, "type", "number");
        builder.AddAttribute(2, "form", Form);
        builder.AddAttribute(3, "name", name);
        builder.AddAttribute(4, "value", renderedHiddenValue);
        builder.AddAttribute(5, "min", Min is double min ? NumberSerializer.Serialize(min) : null);
        builder.AddAttribute(6, "max", Max is double max ? NumberSerializer.Serialize(max) : null);
        builder.AddAttribute(7, "step", (Step ?? default).AttributeText);
        builder.AddAttribute(8, "disabled", EffectiveDisabled);
        builder.AddAttribute(9, "readonly", ReadOnly);
        builder.AddAttribute(10, "required", Required);
        builder.AddAttribute(11, "aria-hidden", "true");
        builder.AddAttribute(12, "tabindex", "-1");
        builder.AddAttribute(13, "style", string.IsNullOrEmpty(name) ? VisuallyHidden.Style : VisuallyHidden.InputStyle);

        // NUM-ARIA-04: the field's description ids without the consumer's, and its invalid state.
        builder.AddAttribute(14, "aria-describedby", fieldHalf!.InField ? fieldHalf.DescribedBy(null) : null);
        builder.AddAttribute(15, "aria-invalid", fieldHalf.AriaInvalid ? "true" : null);
        builder.AddElementReferenceCapture(16, captureHiddenInput);
        builder.CloseElement();
    }

    // NUM-STA-16: the event's Alt key picks the small step, else its Shift key the large step, else the base step.
    /// <summary>Gets the amount a step moves by for the modifiers of the event that triggered it.</summary>
    /// <param name="snapshot">The triggering event, or <see langword="null"/>.</param>
    /// <returns><see cref="SmallStep"/>, <see cref="LargeStep"/> or the base step.</returns>
    internal double GetStepAmount(EventSnapshot? snapshot)
    {
        if (snapshot?.AltKey == true)
        {
            return SmallStep;
        }

        if (snapshot?.ShiftKey == true)
        {
            return LargeStep;
        }

        return (Step ?? default).Amount;
    }

    // NUM-STA-07, NUM-STA-17, NUM-STA-18, NUM-EVT-01..05; number-field/root/NumberFieldRoot.tsx:219-277 (setValue).
    /// <summary>Requests a change of the value.</summary>
    /// <param name="unvalidated">The requested value, before the step pipeline.</param>
    /// <param name="details">The request's details, which carry the reason, the direction and the triggering event.</param>
    /// <returns>A task whose result is <see langword="true"/> when the change was reported and not canceled.</returns>
    internal async Task<bool> SetValueAsync(double? unvalidated, NumberFieldChangeEventDetails details)
    {
        EventReason reason = details.Reason;
        bool isInputReason = reason is EventReason.InputChange or EventReason.InputClear or EventReason.InputBlur or EventReason.InputPaste or EventReason.None;
        bool shouldClamp = !AllowOutOfRange || !isInputReason;
        EventSnapshot snapshot = details.Event;
        double? signedStep = details.Direction is int direction ? GetStepAmount(snapshot) * direction : null;

        double? validated = NumberStepValidation.ToValidatedNumber(
            unvalidated,
            signedStep,
            MinWithDefault,
            MaxWithDefault,
            MinWithZeroDefault,
            Format,
            SnapOnStep,
            snapshot.AltKey,
            shouldClamp);

        double? current = CurrentValue;
        bool shouldFireChange = !Equality.StrictEquals(validated, current)
            || (isInputReason && (!Equality.StrictEquals(unvalidated, current) || !AllowInputSync));

        if (shouldFireChange)
        {
            // ADR 05 Amendments 2026-11-18 item 1: a typed or cleared value commits even when canceled; the follow-up
            // effects (dirty, the pending commit, the text sync) stay suppressed.
            bool nativeTextEdit = reason is EventReason.InputChange or EventReason.InputClear;
            if (!await value!.RequestAsync(validated, details, OnValueChange, ValueChanged, nativeTextEdit))
            {
                if (nativeTextEdit && UpdateState())
                {
                    StateHasChanged();
                }

                return false;
            }

            fieldHalf!.SetDirty(validated);
            HasPendingCommit = true;
        }

        LastChangedValue = validated;

        // Keep the text in sync at once for steps; while the user types the text stays until blur.
        if (AllowInputSync)
        {
            SetInputValue(FormatText(validated));
        }

        // The format can change the text even when the value did not, so the parts render either way.
        UpdateState();
        StateHasChanged();
        return shouldFireChange;
    }

    // NUM-STA-17; number-field/root/NumberFieldRoot.tsx:279-298 (incrementValue).
    /// <summary>Requests a step of the value.</summary>
    /// <param name="amount">The step amount.</param>
    /// <param name="direction"><c>1</c> to step up, <c>-1</c> to step down.</param>
    /// <param name="currentValue">The value to step from, or <see langword="null"/> to step from <see cref="ValueRef"/>.</param>
    /// <param name="snapshot">The triggering event, or <see langword="null"/>.</param>
    /// <param name="reason">The reason of the step.</param>
    /// <returns>A task whose result is <see langword="true"/> when the change was reported and not canceled.</returns>
    internal Task<bool> IncrementValueAsync(double amount, int direction, double? currentValue, EventSnapshot? snapshot, EventReason reason)
    {
        double? previous = currentValue ?? ValueRef;
        if (previous is not double from)
        {
            // An empty field is seeded with 0 and no direction: the seed is clamped into range but never snapped.
            return SetValueAsync(0, new NumberFieldChangeEventDetails(reason, null, snapshot));
        }

        return SetValueAsync(from + (amount * direction), new NumberFieldChangeEventDetails(reason, direction, snapshot));
    }

    // NUM-API-04, NUM-EVT-05 (onValueCommitted, number-field/root/NumberFieldRoot.tsx:135-140). The consumer callback takes
    // track 1's ValueCommittedEventArgs<T> (ADR 08 Amendments 2026-11-18, decision 13 (a)) and is added when it lands.
    /// <summary>Reports that an interaction ended with a value.</summary>
    /// <param name="committed">The committed value.</param>
    /// <param name="reason">The reason of the interaction.</param>
    /// <param name="snapshot">The event that ended it, or <see langword="null"/>.</param>
    /// <returns>A task that completes when the consumer's callback has run.</returns>
    internal Task CommitAsync(double? committed, EventReason reason, EventSnapshot? snapshot)
    {
        _ = committed;
        _ = reason;
        _ = snapshot;
        HasPendingCommit = false;
        return Task.CompletedTask;
    }

    /// <summary>Sets the text the visible input shows.</summary>
    /// <param name="text">The text.</param>
    internal void SetInputValue(string text)
    {
        if (!string.Equals(inputValue, text, StringComparison.Ordinal))
        {
            inputValue = text;
        }
    }

    // NUM-I18N-01 with the cache of NUM-I18N-13: the same value under the same formatter is not formatted again.
    /// <summary>Formats a value under the current culture and format.</summary>
    /// <param name="number">The value, or <see langword="null"/>.</param>
    /// <returns>The formatted text, or <see cref="string.Empty"/> for <see langword="null"/>.</returns>
    internal string FormatText(double? number)
    {
        NumberFormatter current = Formatter;
        if (ReferenceEquals(current, formattedWith) && Equality.Is(formattedValue, number))
        {
            return formattedText;
        }

        formattedWith = current;
        formattedValue = number;
        formattedText = number is double n ? current.Format(n) : string.Empty;
        return formattedText;
    }

    // NUM-FRM-06: the Input registers the field control; more than one is unsupported (§16 item 8), so the count only says
    // whether one is mounted.
    /// <summary>Records that a <see cref="NumberFieldInput"/> mounted or unmounted.</summary>
    /// <param name="mounted"><see langword="true"/> when one mounted; <see langword="false"/> when one left.</param>
    internal void SetInputMounted(bool mounted)
    {
        mountedInputs += mounted ? 1 : -1;
    }

    // ADR 05 Amendments 2026-11-18 item 2: the value enters the form registry's object slots boxed once per commit, never per
    // render (the RadioGroup precedent).
    /// <summary>Gets the value boxed once per committed value.</summary>
    /// <returns>The boxed value, or <see langword="null"/> while it is <see langword="null"/>.</returns>
    internal object? BoxedValue() => boxedValue;

        // NUM-STA-15: a scrub area's start and end (the `number-field` module's scrub role).
    /// <summary>Sets whether a scrub area of the field is being scrubbed, and renders the change.</summary>
    /// <param name="value"><see langword="true"/> while a scrub is in progress.</param>
    internal void SetScrubbing(bool value)
    {
        if (scrubbing != value)
        {
            scrubbing = value;
            Refresh();
        }
    }

    /// <summary>Re-renders the root and its parts after a change of the fields a part renders.</summary>
    internal void Refresh()
    {
        UpdateState();
        StateHasChanged();
    }

    // Returns whether the state changed; a new record is made only then (ADR 16 rule 7), and the parts are told first, so
    // they render in the same batch as the root.
    private bool UpdateState()
    {
        double? current = CurrentValue;

        // Base UI's useValueAsRef: the value a step starts from is the rendered value; a stepper moves it ahead within one
        // interaction after this point (NUM-PTR-05).
        ValueRef = current;

        if (valueVersion == 0 || !Equality.StrictEquals(versionedValue, current) || versionedValue.HasValue != current.HasValue)
        {
            valueVersion++;
            versionedValue = current;

            // BLZX2005: FieldControlRegistration.Value and FieldValidationArguments.Value are object slots; the boxing
            // happens once per committed value here, the RadioGroup precedent (ADR 05 Amendments 2026-10-22, 2026-11-18).
#pragma warning disable BLZX2005
            boxedValue = current is double number ? number : null;
#pragma warning restore BLZX2005
        }

        // NUM-STA-06: while sync is allowed the text is the formatted value after every update.
        if (AllowInputSync)
        {
            SetInputValue(FormatText(current));
        }

        FieldStateSnapshot snapshot = fieldHalf!.Snapshot;
        bool disabled = snapshot.Disabled;
        if (state is { } previous
            && Equality.StrictEquals(previous.Value, current)
            && previous.Value.HasValue == current.HasValue
            && ReferenceEquals(previous.InputValue, inputValue)
            && previous.Required == Required
            && previous.Disabled == disabled
            && previous.ReadOnly == ReadOnly
            && previous.Scrubbing == scrubbing
            && previous.Touched == snapshot.Touched
            && previous.Dirty == snapshot.Dirty
            && previous.Valid == snapshot.Valid
            && previous.Filled == snapshot.Filled
            && previous.Focused == snapshot.Focused)
        {
            return false;
        }

        state = new NumberFieldRootState(current, inputValue, Required, disabled, ReadOnly, scrubbing, snapshot.Touched, snapshot.Dirty, snapshot.Valid, snapshot.Filled, snapshot.Focused);
        context.SetState(state);
        return true;
    }

    private static string Serialize(double? number) => number is double n ? NumberSerializer.Serialize(n) : string.Empty;

    private async Task ReadPlatformAsync()
    {
        try
        {
            await Platform.EnsureAsync(JSRuntime).ConfigureAwait(true);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return;
        }

        if (Platform.Ios == true)
        {
            await InvokeAsync(context.Notify).ConfigureAwait(true);
        }
    }
}

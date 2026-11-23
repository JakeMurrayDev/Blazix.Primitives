using System.Globalization;
using System.Linq.Expressions;
using Iodine.Headless.Interop;
using Iodine.Headless.Rendering;
using Iodine.Headless.Store;
using Iodine.Headless.Utilities;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace Iodine.Headless;

// Base UI's OTPField.Root (otp-field/root/OTPFieldRoot.tsx); the contract is docs/components/otp-field.md (issue 278, M6
// track 7).
// - A `div role="group"` followed by a visually hidden `<input type="text">` that carries the whole value, rendered only while
//   Length is a positive integer (BuildAfterElement; OTP-STR-01, OTP-DOM-20..26).
// - The value is a Controlled<string> fixed at the first parameter set (OTP-STA-01, OTP-EDGE-06). The rendered value is
//   always the normalization of the stored or controlled one (OTP-STA-06, OTP-EDGE-14), re-derived when the value,
//   Length, ValidationType or NormalizeValue changed, never with a callback.
// - The slots register with the root's CompositeListBehavior (registration-only, the TabsRoot precedent) and take their
//   index from its render-order counter until the module's order report (OTP-STR-03, OTP-EDGE-10/11; CMP-STA-16).
// - The store of OTP §4: the last focused index, the focused flag and its owner slot (OTP-FOC-02, OTP-FOC-07, OTP-FOC-09).
// - The `otp-field` module attaches to the root element after the composite list (design point 6 (a)); its edit reports
//   become Base UI's requests (OTPFieldRoot.tsx:228-266 `setValue`, OTPFieldInput.tsx:101-133 and :256-303), each echoed
//   by its revision so the update restores the slots (the revision rule, ADR 04 Amendments 2026-11-18, decision 6). On
//   WebAssembly the edit decision runs the slot's consumer handler, then the request, inside the dispatch (design points 1
//   and 2 (a), chosen by the user 2026-11-19).
// - Typing and clearing commit regardless of Cancel(), which suppresses the queued focus, the completion and AutoSubmit;
//   keyboard and paste follow OTP-EVT-04 (ADR 05 Amendments 2026-11-18 item 1, decision 9 (a)). A canceled edit the module
//   advanced ahead rolls its focus back (design point 4 (a)).
// - Inside a field the root is the field's control through OtpFieldField (OTP §8; ADR 05 Amendments 2026-11-18 item 2).
// - aria-labelledby and aria-describedby are read from the consumer's attributes and replaced in FinishAttributes, so they
//   are consumed rather than forwarded (OTP-ARIA-02..05, OTP-EDGE-13; ADR 08 Amendments 2026-11-18).
/// <summary>
/// Represents a one-time code field: Base UI's <c>OTPField.Root</c>, rendered as a <c>div</c> with the <c>group</c> role,
/// followed by a visually hidden <c>input</c> that holds the whole code for forms, native validation and autofill.
/// </summary>
/// <remarks>
/// <para>
/// Place one <see cref="OtpFieldInput"/> per character inside it, in any layout, optionally separated by
/// <see cref="OtpFieldSeparator"/>. The slots take their position from the order they render in.
/// </para>
/// <para>
/// The value is the consumer's when <see cref="Value"/> is supplied at the first render (<c>@bind-Value</c> works), and
/// the field's own otherwise, starting from <see cref="DefaultValue"/>. Every value is cleaned before it is used: whitespace
/// is removed, characters outside <see cref="ValidationType"/> are dropped, <see cref="NormalizeValue"/> runs, and the
/// result is cut to <see cref="Length"/> characters.
/// </para>
/// </remarks>
public sealed class OtpFieldRoot : PartBase<OtpFieldRootState>
{
    private const string DefaultTag = "div";
    private const string AriaLabelledBy = "aria-labelledby";
    private const string AriaDescribedBy = "aria-describedby";

    private static readonly StateAttributesMapping<OtpFieldRootState> s_mapping = FieldValidityMapping.Create<OtpFieldRootState>();

    private readonly OtpFieldRootContext context;
    private readonly Action fieldChanged;
    private readonly Action<ElementReference> captureHiddenInput;
    private Controlled<string>? value;
    private OtpFieldField? fieldHalf;
    private CompositeListBehavior? slots;
    private ElementReference? hiddenInput;
    private string? renderedHiddenValue;
    private bool hiddenValueHandedOver;
    private string? controlId;
    private string? normalizedFrom;
    private int normalizedLength = -1;
    private OtpFieldValidationType normalizedType;
    private Func<string, string>? normalizedWith;
    private string normalized = string.Empty;
    private int valueVersion;
    private string? versionedValue;
    private int mountedSlots;
    private bool focused;
    private int focusedIndex;
    private object? focusOwner;
    private string? labelFallbackId;
    private readonly List<OtpFieldInput> slotParts = [];
    private readonly TaskCompletionSource attachedSource = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private OtpFieldTwin? twin;
    private TwinHandle<OtpFieldOptions>? handle;
    private OtpFieldOptions? sentOptions;
    private int editRevision;
    private bool rollback;
    private int pendingFocusIndex = -1;
    private string? pendingFocusValue;
    private OtpFieldFocusRequest? focusRequest;
    private string? pendingCompleteValue;
    private GenericEventDetails? pendingCompleteDetails;
    private int submitRevision;
    private int processedVersion;
    private OtpFieldRootState? state;
    private RenderFragment? cascade;
    private RenderFragment? cascadedContent;

    /// <summary>Initializes a new instance of the <see cref="OtpFieldRoot"/> class.</summary>
    public OtpFieldRoot()
    {
        context = new OtpFieldRootContext(this);
        fieldChanged = Refresh;
        captureHiddenInput = element => hiddenInput = element;
    }

    [Inject]
    private ILogger<OtpFieldRoot> Logger { get; set; } = null!;

    [Inject]
    private PartIdGenerator IdGenerator { get; set; } = null!;

    [Inject]
    private IJSRuntime JSRuntime { get; set; } = null!;

    [Inject]
    private DefaultFormScope Formless { get; set; } = null!;

    [Inject]
    private OtpFieldDevelopmentLog DevelopmentLog { get; set; } = null!;

    // OTP-ARIA-03, OTP-ARIA-04 (useAriaLabelledBy): the field label's id, else the native label of slot 0.
    private string? ResolvedLabel => fieldHalf!.FieldLabelId ?? labelFallbackId;

    // Variant (ii) (decision 6 (a)): the optimistic advance needs built-in validation, no custom normalization and the
    // field's own value.
    private bool Optimistic => !value!.IsControlled && NormalizeValue is null && SlotPattern is not null;

    [CascadingParameter]
    private FieldRootContext? FieldContext { get; set; }

    [CascadingParameter]
    private IFormScope? FormScope { get; set; }

    [CascadingParameter]
    private EditContext? CascadedEditContext { get; set; }

    [CascadingParameter]
    private DirectionProviderContext? Direction { get; set; }

    // OTP-API-01; ADR 08 Amendments 2026-11-18: an int, so a non-integer length cannot be expressed.
    /// <summary>Gets or sets the number of slots.</summary>
    /// <value>
    /// The number of characters the code has. It cuts the value, decides when the code is complete and sizes the hidden
    /// input's constraints; render this many <see cref="OtpFieldInput"/> parts.
    /// </value>
    [Parameter]
    [EditorRequired]
    public int Length { get; set; }

    // OTP-API-02; ADR 05 §2 and Amendments 2026-11-18 item 2.
    /// <summary>Gets or sets the value, when the consumer owns it.</summary>
    /// <value>
    /// The code, cleaned before it is shown; <see langword="null"/> is shown as empty. Supplying it at the first render makes
    /// the value the consumer's for the lifetime of the field: a change then only raises <see cref="OnValueChange"/> and
    /// <see cref="ValueChanged"/>.
    /// </value>
    [Parameter]
    public string? Value { get; set; }

    // ADR 05 §2: the binding half of @bind-Value.
    /// <summary>Gets or sets the callback that receives the new value after a change that was not canceled.</summary>
    /// <value>The callback <c>@bind-Value</c> supplies, invoked after <see cref="OnValueChange"/>.</value>
    [Parameter]
    public EventCallback<string> ValueChanged { get; set; }

    // OTP-API-03
    /// <summary>Gets or sets the initial value, when the field owns its value.</summary>
    /// <value>The code, cleaned before it is shown; the default is <see cref="string.Empty"/>. Read at the first render only.</value>
    [Parameter]
    public string? DefaultValue { get; set; } = string.Empty;

    // OTP-API-04, OTP-EVT-01..05
    /// <summary>Gets or sets the callback invoked when the value is about to change.</summary>
    /// <value>
    /// The callback, which receives the cleaned next value and the change details, whose reason is
    /// <see cref="EventReason.InputChange"/>, <see cref="EventReason.InputClear"/>, <see cref="EventReason.InputPaste"/> or
    /// <see cref="EventReason.Keyboard"/>. Calling <see cref="ChangeEventDetails.Cancel"/> keeps the current value for a key
    /// or a paste; typed text still changes the value, and the cancel then only keeps focus where it is.
    /// </value>
    [Parameter]
    public EventCallback<ValueChangeEventArgs<string>> OnValueChange { get; set; }

    // OTP-API-05, OTP-EVT-07
    /// <summary>Gets or sets the callback invoked when entered text had characters the field does not accept.</summary>
    /// <value>The callback, which receives the text as entered and the details; it runs before <see cref="OnValueChange"/>.</value>
    [Parameter]
    public EventCallback<OtpFieldInvalidEventArgs> OnValueInvalid { get; set; }

    // OTP-API-06, OTP-EVT-08..12
    /// <summary>Gets or sets the callback invoked when typing, pasting or autofill completes the code.</summary>
    /// <value>
    /// The callback, which receives the complete value and the details. It runs after the new value is shown, and also
    /// when a complete code is pasted over the same complete code.
    /// </value>
    [Parameter]
    public EventCallback<OtpFieldCompleteEventArgs> OnValueComplete { get; set; }

    // OTP-API-07, OTP-STA-05
    /// <summary>Gets or sets the characters the field accepts.</summary>
    /// <value>The accepted characters, or <see langword="null"/>, the default, for <see cref="OtpFieldValidationType.Numeric"/>.</value>
    [Parameter]
    public OtpFieldValidationType? ValidationType { get; set; }

    // OTP-API-08
    /// <summary>Gets or sets the function that cleans the value after whitespace and <see cref="ValidationType"/> filtering.</summary>
    /// <value>
    /// The function, or <see langword="null"/>. Its result is filtered by <see cref="ValidationType"/> again and cut to
    /// <see cref="Length"/>. It runs for every value the field cleans, so it should return its input unchanged when called
    /// on its own output.
    /// </value>
    [Parameter]
    public Func<string, string>? NormalizeValue { get; set; }

    // OTP-API-09; ADR 08 Amendments 2026-11-18.
    /// <summary>Gets or sets the virtual keyboard the slots and the hidden input ask for.</summary>
    /// <value>
    /// The keyboard, or <see langword="null"/>, the default, for <see cref="Headless.InputMode.Numeric"/> with numeric
    /// validation, <see cref="Headless.InputMode.Text"/> with alphabetic or alphanumeric validation, and none otherwise.
    /// </value>
    [Parameter]
    public InputMode? InputMode { get; set; }

    // OTP-API-10
    /// <summary>Gets or sets a value indicating whether the slots hide the characters they show.</summary>
    /// <value><see langword="true"/> to render the slots as <c>type="password"</c>; the default is <see langword="false"/>.</value>
    [Parameter]
    public bool Mask { get; set; }

    // OTP-API-11
    /// <summary>Gets or sets the <c>autocomplete</c> hint of the first slot and the hidden input.</summary>
    /// <value>The hint; the default is <c>one-time-code</c>. The other slots always render <c>off</c>.</value>
    [Parameter]
    public string? AutoComplete { get; set; } = "one-time-code";

    // OTP-API-12, OTP-EVT-13
    /// <summary>Gets or sets a value indicating whether the field submits its form when the code becomes complete.</summary>
    /// <value><see langword="true"/> to submit right after <see cref="OnValueComplete"/>; the default is <see langword="false"/>.</value>
    [Parameter]
    public bool AutoSubmit { get; set; }

    // OTP-API-13; ADR 08 Amendments 2026-10-22 (the parameter keeps the name `Form`).
    /// <summary>Gets or sets the id of the form that owns the field when it is rendered outside that form.</summary>
    /// <value>The form's id, placed as the <c>form</c> attribute of the hidden input and every slot; or <see langword="null"/>.</value>
    [Parameter]
    public string? Form { get; set; }

    // OTP-API-14
    /// <summary>Gets or sets the name the value is submitted under.</summary>
    /// <value>The name placed on the hidden input, or <see langword="null"/> for none. An enclosing field's name wins.</value>
    [Parameter]
    public string? Name { get; set; }

    // OTP-API-15
    /// <summary>Gets or sets a value indicating whether the field ignores user interaction.</summary>
    /// <value><see langword="true"/> to disable every slot and the hidden input; the default is <see langword="false"/>.</value>
    [Parameter]
    public bool Disabled { get; set; }

    // OTP-API-16
    /// <summary>Gets or sets a value indicating whether the user cannot change the value.</summary>
    /// <value>
    /// <see langword="true"/> to keep the value while the slots stay focusable and navigable and the value submits with its
    /// form; the default is <see langword="false"/>.
    /// </value>
    [Parameter]
    public bool ReadOnly { get; set; }

    // OTP-API-17
    /// <summary>Gets or sets a value indicating whether the field must have a value for its form to submit.</summary>
    /// <value><see langword="true"/> to require a value; the default is <see langword="false"/>.</value>
    [Parameter]
    public bool Required { get; set; }

    // OTP-API-18
    /// <summary>Gets or sets the id of the first slot.</summary>
    /// <value>
    /// The first slot's id, from which the other slots' ids follow (<c>{id}-2</c>, <c>{id}-3</c> and so on); or
    /// <see langword="null"/> for a generated one. It is never rendered on the root's own element.
    /// </value>
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
    public Expression<Func<string>>? ValueExpression { get; set; }

    /// <inheritdoc />
    protected override string DefaultTagName => DefaultTag;

    /// <inheritdoc />
    protected override OtpFieldRootState State => state!;

    /// <inheritdoc />
    protected override StateAttributesMapping<OtpFieldRootState>? StateAttributesMapping => s_mapping;

    /// <inheritdoc />
    protected override bool UsesGeneratedId => RenderFunction is not null;

    // OTP-API-18: the labelable control id, the consumer's or the one drawn at first render (ADR 02 decision 5).
    /// <summary>Gets slot 0's id.</summary>
    internal string ControlId => Id ?? controlId!;

    // OTP-API-15: the field's disabled state OR'd with the part's.
    /// <summary>Gets the field's effective disabled state.</summary>
    internal bool EffectiveDisabled => fieldHalf!.Disabled;

    // OTP-API-14
    /// <summary>Gets the effective form name.</summary>
    internal string? EffectiveName => fieldHalf!.Name;

    /// <summary>Gets the field half.</summary>
    internal OtpFieldField Field => fieldHalf!;

    /// <summary>Gets the registry the slots join.</summary>
    internal CompositeListContext Slots => slots!.Context;

    /// <summary>Gets the effective validation type: <see cref="ValidationType"/>, else numeric.</summary>
    internal OtpFieldValidationType EffectiveValidationType => ValidationType ?? OtpFieldValidationType.Numeric;

    /// <summary>Gets the normalized value the field shows.</summary>
    internal string CurrentValue => normalized;

    // OTP-STA-05, OTP-DOM-16: the single-character pattern of the built-in classes; none for `none`.
    /// <summary>Gets the slot pattern, or <see langword="null"/>.</summary>
    internal string? SlotPattern => EffectiveValidationType switch
    {
        OtpFieldValidationType.Numeric => @"\d{1}",
        OtpFieldValidationType.Alpha => "[a-zA-Z]{1}",
        OtpFieldValidationType.Alphanumeric => "[a-zA-Z0-9]{1}",
        _ => null,
    };

    // OTP-API-09, OTP-DOM-10, OTP-DOM-23.
    /// <summary>Gets the effective <c>inputmode</c> literal, or <see langword="null"/>.</summary>
    internal string? InputModeLiteral => InputMode switch
    {
        Headless.InputMode.None => "none",
        Headless.InputMode.Text => "text",
        Headless.InputMode.Tel => "tel",
        Headless.InputMode.Url => "url",
        Headless.InputMode.Email => "email",
        Headless.InputMode.Numeric => "numeric",
        Headless.InputMode.Decimal => "decimal",
        Headless.InputMode.Search => "search",
        _ => EffectiveValidationType switch
        {
            OtpFieldValidationType.Numeric => "numeric",
            OtpFieldValidationType.Alpha or OtpFieldValidationType.Alphanumeric => "text",
            _ => null,
        },
    };

    // OTP-I18N-01
    /// <summary>Gets a value indicating whether the reading direction is right to left.</summary>
    internal bool Rtl => DirectionProviderContext.Read(Direction) == TextDirection.Rtl;

    // OTP-FOC-02 (OTPFieldRoot.tsx:146-148): the focused slot while a slot has focus, else the first empty slot, or the last
    // slot when the value is complete.
    /// <summary>Gets the index of the slot in the tab order.</summary>
    internal int ActiveIndex => focused
        ? Math.Min(focusedIndex, Math.Max(Length - 1, 0))
        : Math.Min(normalized.Length, Length - 1);

    // OTP-ARIA-02..04, OTP-ARIA-06: the label the slots inherit, unless the consumer gave the root its own aria-labelledby.
    /// <summary>Gets the <c>aria-labelledby</c> a slot without its own label inherits, or <see langword="null"/>.</summary>
    internal string? InheritedLabel => ConsumerAttribute(AriaLabelledBy) is null ? ResolvedLabel : null;

    // OTP-DOM-08, design point 5 (a): once the module is attached it owns the slots' `value` and `tabindex`.
    /// <summary>Gets a value indicating whether the module owns the slots' <c>value</c> and <c>tabindex</c>.</summary>
    internal bool ModuleOwnsSlots => handle is not null;

    /// <summary>Gets a task that completes once the <c>otp-field</c> module is attached (the browser fixtures' marker).</summary>
    internal Task TwinAttached => attachedSource.Task;

    /// <summary>Gets the id of the slot 0's native label the slot reported, which labels the group without a field label.</summary>
    internal string? LabelFallbackId => labelFallbackId;

    /// <inheritdoc />
    public override Task SetParametersAsync(ParameterView parameters)
    {
        // OTP-STA-01, OTP-EDGE-06: ownership is fixed at the first parameter set (ADR 05 Amendments 2026-10-18).
        value ??= new Controlled<string>("OtpField", "value", Logger);
        value.SetParameters(parameters, nameof(Value), nameof(DefaultValue));
        return base.SetParametersAsync(parameters);
    }

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        base.OnInitialized();

        // OTP-API-18: the control id is drawn in render order (ADR 02 decision 5), whether or not a slot renders.
        controlId = IdGenerator.Next();
        slots = new CompositeListBehavior(JSRuntime, IdGenerator, StateHasChanged);
        fieldHalf = new OtpFieldField(Formless, JSRuntime, fieldChanged, InvokeAsync, DispatchExceptionAsync);
        fieldHalf.Initialize(FieldContext);
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        fieldHalf!.SetParameters(Name, Disabled, FormScope, CascadedEditContext, ValueExpression);
        bool wasDisabled = state?.Disabled ?? false;

        // A changed state notifies the slots; otherwise they are notified for the parameters they read beside the state.
        if (!UpdateState())
        {
            context.Notify();
        }

        // OTP-FOC-09: a field that becomes disabled releases the focused flag its slot owned.
        if (!wasDisabled && EffectiveDisabled)
        {
            ReleaseFocus();
        }

        if (cascade is null || (!ReferenceEquals(ChildContent, cascade) && !ReferenceEquals(cascadedContent, ChildContent)))
        {
            cascadedContent = ChildContent;
            cascade = context.Cascade(slots!.Context, cascadedContent);
        }

        ChildContent = cascade;
    }

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        WarnAboutLength();
        await slots!.AfterRenderAsync(Enabled, Element, Id ?? GeneratedId);
        await fieldHalf!.AfterRenderAsync(ControlId, Length > 0 ? hiddenInput : null, normalized, valueVersion);
        if (!hiddenValueHandedOver && fieldHalf.TwinAttached.IsCompleted)
        {
            hiddenValueHandedOver = true;
        }

        await ProcessCommittedValueAsync();
        await SyncTwinAsync();
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            ReleaseFocus();
            slots?.Dispose();
            fieldHalf?.Dispose();
            if (twin is not null)
            {
                twin.Edited -= OnEdited;
                twin.SlotFocused -= OnSlotFocused;
                twin.SlotBlurred -= OnSlotBlurred;
                twin.PasteUnreadable -= OnPasteUnreadable;
                fieldHalf!.Autofilled -= OnAutofilled;
                _ = twin.DisposeAsync().AsTask();
                twin = null;
                handle = null;
            }
        }

        base.Dispose(disposing);
    }

    /// <inheritdoc />
    protected override void AddPartAttributes(AttributeList attributes)
    {
        slots!.AddAttributes(attributes, Id ?? GeneratedId);
        attributes.Set("role", "group");
    }

    // OTP-ARIA-02..05, OTP-EDGE-13 (OTPFieldRoot.tsx:125-131,384-391): the consumer's aria-labelledby and aria-describedby are
    // consumed: the group carries the consumer's labelledby, else the resolved label; and the consumer's describedby ids then
    // the field's, split on whitespace, each once.
    /// <inheritdoc />
    protected override void FinishAttributes(AttributeList attributes)
    {
        string? labelledBy = ConsumerAttribute(AriaLabelledBy) ?? ResolvedLabel;
        if (labelledBy is null)
        {
            attributes.Remove(AriaLabelledBy);
        }
        else
        {
            attributes.Set(AriaLabelledBy, labelledBy);
        }

        string? describedBy = MergeIds(ConsumerAttribute(AriaDescribedBy), fieldHalf!.InField ? fieldHalf.DescribedBy(null) : null);
        if (describedBy is null)
        {
            attributes.Remove(AriaDescribedBy);
        }
        else
        {
            attributes.Set(AriaDescribedBy, describedBy);
        }
    }

    // OTP-STR-01, OTP-DOM-20..26, OTP-ARIA-11; otp-field/root/OTPFieldRoot.tsx:404-458.
    /// <inheritdoc />
    protected override void BuildAfterElement(RenderTreeBuilder builder)
    {
        if (Length <= 0)
        {
            return;
        }

        // The handover at attach (ADR 04 Amendments 2026-10-18): the value is rendered until the `form` module owns it.
        if (!hiddenValueHandedOver)
        {
            renderedHiddenValue = normalized;
        }

        string? name = EffectiveName;
        string? pattern = EffectiveValidationType switch
        {
            OtpFieldValidationType.Numeric => $@"\d{{{Length}}}",
            OtpFieldValidationType.Alpha => $"[a-zA-Z]{{{Length}}}",
            OtpFieldValidationType.Alphanumeric => $"[a-zA-Z0-9]{{{Length}}}",
            _ => null,
        };
        string length = Length.ToString(CultureInfo.InvariantCulture);
        builder.OpenElement(0, "input");
        builder.AddAttribute(1, "type", "text");
        builder.AddAttribute(2, "id", name is null ? $"{ControlId}-hidden-input" : null);
        builder.AddAttribute(3, "form", Form);
        builder.AddAttribute(4, "name", name);
        builder.AddAttribute(5, "value", renderedHiddenValue);
        builder.AddAttribute(6, "autocomplete", AutoComplete);
        builder.AddAttribute(7, "inputmode", InputModeLiteral);
        builder.AddAttribute(8, "minlength", length);
        builder.AddAttribute(9, "maxlength", length);
        builder.AddAttribute(10, "pattern", pattern);
        builder.AddAttribute(11, "disabled", EffectiveDisabled);
        builder.AddAttribute(12, "readonly", ReadOnly);
        builder.AddAttribute(13, "required", Required);
        builder.AddAttribute(14, "aria-hidden", "true");
        builder.AddAttribute(15, "tabindex", "-1");
        builder.AddAttribute(16, "style", string.IsNullOrEmpty(name) ? VisuallyHidden.Style : VisuallyHidden.InputStyle);
        builder.AddAttribute(17, AriaDescribedBy, fieldHalf!.InField ? fieldHalf.DescribedBy(null) : null);
        builder.AddAttribute(18, "aria-invalid", fieldHalf.HiddenInvalid ? "true" : null);
        builder.AddElementReferenceCapture(19, captureHiddenInput);
        builder.CloseElement();
    }

    // OTP-DOM-07 (OTPFieldRoot.tsx:300-309).
    /// <summary>Gets a slot's id.</summary>
    /// <param name="index">The slot's index.</param>
    /// <returns>The control id for slot 0, else <c>{id}-{index + 1}</c>.</returns>
    internal string SlotId(int index) => index == 0 ? ControlId : $"{ControlId}-{(index + 1).ToString(CultureInfo.InvariantCulture)}";

    /// <summary>Records that a slot mounted or unmounted.</summary>
    /// <param name="mounted"><see langword="true"/> when one mounted; <see langword="false"/> when one left.</param>
    internal void SetSlotMounted(bool mounted) => mountedSlots += mounted ? 1 : -1;

    // OTP-ARIA-04: slot 0's form handle discovers its native label.
    /// <summary>Takes the id of slot 0's native label.</summary>
    /// <param name="labelId">The label's id, or <see langword="null"/> when none is associated.</param>
    internal void SetLabelFallback(string? labelId)
    {
        if (!string.Equals(labelFallbackId, labelId, StringComparison.Ordinal))
        {
            labelFallbackId = labelId;
            Refresh();
        }
    }

    // OTP-FOC-03, OTP-FOC-04, OTP-FOC-07 (OTPFieldRoot.tsx:274-284, OTPFieldInput.tsx:172-179): a focus that no consumer vetoed.
    /// <summary>Applies a slot's focus: the last focused index and the focused flag, owned by the slot.</summary>
    /// <param name="slot">The slot.</param>
    /// <param name="index">The slot's index.</param>
    internal void HandleSlotFocus(object slot, int index)
    {
        if (EffectiveDisabled)
        {
            return;
        }

        focusOwner = slot;
        fieldHalf!.HandleFocus(slot);
        if (index <= normalized.Length)
        {
            focusedIndex = index;
        }

        // OTP-FOC-09: a slot-to-slot move only hands the owner over; the module owns the tab stop, so nothing renders.
        if (!focused)
        {
            focused = true;
            Refresh();
        }
    }

    // OTP-FOC-07, OTP-FRM-09, OTP-FRM-10 (OTPFieldRoot.tsx:286-298): a blur whose related target is outside the root.
    /// <summary>Applies a blur that left the field.</summary>
    /// <param name="slot">The slot that lost focus.</param>
    /// <returns>A task that completes when an onBlur commit has run.</returns>
    internal async Task HandleLeaveAsync(object slot)
    {
        focused = false;
        focusOwner = null;
        Refresh();
        await fieldHalf!.HandleLeaveAsync(slot, normalized).ConfigureAwait(true);
    }

    // OTP-FOC-09 (useSetFieldFocused): the slot that owns the focused flag left or became disabled.
    /// <summary>Releases the focused flag when a slot owns it.</summary>
    /// <param name="slot">The slot that left.</param>
    internal void ReleaseSlot(object slot)
    {
        if (ReferenceEquals(focusOwner, slot))
        {
            ReleaseFocus();
            Refresh();
        }
    }

    /// <summary>Adds a slot to the parts the root's decisions find by index.</summary>
    /// <param name="slot">The slot.</param>
    internal void RegisterSlot(OtpFieldInput slot) => slotParts.Add(slot);

    /// <summary>Removes a slot from the parts the root's decisions find by index.</summary>
    /// <param name="slot">The slot.</param>
    internal void UnregisterSlot(OtpFieldInput slot) => slotParts.Remove(slot);

    /// <summary>Re-renders the root and its slots after a change of the fields a slot renders.</summary>
    internal void Refresh()
    {
        if (!UpdateState())
        {
            context.Notify();
        }

        StateHasChanged();
    }

    // OTPFieldInput.tsx:101-133 (`commitValue`) and :256-303 (the keyboard edits): one edit report, one request.
    /// <summary>Turns an edit the module reported into a request, then echoes its revision.</summary>
    /// <param name="edit">The edit.</param>
    /// <returns>A task that completes when the request and its callbacks have run.</returns>
    internal async Task HandleEditAsync(OtpFieldEditReport edit)
    {
        string current = normalized;
        int index = edit.Index;
        EventSnapshot @event = edit.Event;
        bool canEdit = !EffectiveDisabled && !ReadOnly && index >= 0;
        if (canEdit)
        {
            switch (edit.Kind)
            {
                case "input":
                case "paste":
                    await CommitTextAsync(current, edit, edit.Kind == "paste" ? EventReason.InputPaste : EventReason.InputChange).ConfigureAwait(true);
                    break;
                case "clear":
                    await SetKeyboardValueAsync(string.Empty, 0, @event).ConfigureAwait(true);
                    break;
                case "delete":
                    await SetKeyboardValueAsync(OtpValue.Remove(current, index), index, @event).ConfigureAwait(true);
                    break;
                case "backspace":
                    int target = Math.Max(0, index - 1);
                    int deleteIndex = index < current.Length ? index : target;
                    await SetKeyboardValueAsync(OtpValue.Remove(current, deleteIndex), target, @event).ConfigureAwait(true);
                    break;
            }
        }

        // The revision rule: the update that follows echoes the report, so the module applies it (and restores the slots).
        editRevision = Math.Max(editRevision, edit.Revision);
        Refresh();
    }

    private async Task CommitTextAsync(string current, OtpFieldEditReport edit, EventReason reason)
    {
        int index = edit.Index;
        (string digits, bool rejected) = OtpValue.NormalizeWithDetails(edit.Raw, Length, EffectiveValidationType, NormalizeValue);
        if (rejected)
        {
            await OnValueInvalid.InvokeAsync(new OtpFieldInvalidEventArgs(edit.Raw, new GenericEventDetails(reason, edit.Event))).ConfigureAwait(true);
        }

        if (digits.Length == 0)
        {
            // Typed input edits the slot in place: an emptied slot clears; a fully rejected character is restored by the
            // update. An empty or fully rejected paste changes nothing.
            if (reason == EventReason.InputChange && edit.Raw.Length == 0)
            {
                await SetValueAsync(OtpValue.Remove(current, index), EventReason.InputClear, edit.Event, -1).ConfigureAwait(true);
            }

            return;
        }

        string? committed = await SetValueAsync(
            OtpValue.Replace(current, index, digits, Length, EffectiveValidationType, NormalizeValue),
            reason,
            edit.Event,
            edit.Advanced ? -1 : Math.Min(index + digits.Length, Length - 1)).ConfigureAwait(true);
        if (committed is null && edit.Advanced)
        {
            // Design point 4 (a): the module moved focus ahead of a request that was canceled; focus returns to the slot.
            rollback = true;
            focusRequest = new OtpFieldFocusRequest(index, (focusRequest?.Revision ?? 0) + 1);
        }
    }

    private async Task SetKeyboardValueAsync(string next, int target, EventSnapshot @event)
    {
        await SetValueAsync(next, EventReason.Keyboard, @event, target).ConfigureAwait(true);
    }

    // OTPFieldRoot.tsx:228-266 `setValue`, with decision 9 (a): typing and clearing commit regardless of Cancel(), which
    // still suppresses the queued focus and the completion. The focus and the completion are queued before the commit, whose
    // render may run inside it, and dropped again when it is canceled. Returns the committed value, or null.
    private async Task<string?> SetValueAsync(string next, EventReason reason, EventSnapshot @event, int focusIndex)
    {
        string current = normalized;
        string value = OtpValue.Normalize(next, Length, EffectiveValidationType, NormalizeValue);
        bool canComplete = reason is EventReason.InputChange or EventReason.InputPaste;
        GenericEventDetails? complete = canComplete && value.Length == Length && (current.Length != Length || reason == EventReason.InputPaste)
            ? new GenericEventDetails(reason, @event)
            : null;
        if (Equality.StrictEquals(value, current))
        {
            if (complete is not null)
            {
                await CompleteAsync(value, complete).ConfigureAwait(true);
            }

            return null;
        }

        ChangeEventDetails details = new(reason, @event);
        bool typing = reason is EventReason.InputChange or EventReason.InputClear;
        string? previousComplete = pendingCompleteValue;
        GenericEventDetails? previousDetails = pendingCompleteDetails;
        if (focusIndex >= 0)
        {
            pendingFocusIndex = focusIndex;
            pendingFocusValue = value;
        }

        if (complete is not null)
        {
            pendingCompleteValue = value;
            pendingCompleteDetails = complete;
        }
        else if (value.Length != Length)
        {
            pendingCompleteValue = null;
            pendingCompleteDetails = null;
        }

        bool accepted = await this.value!.RequestAsync(value, details, OnValueChange, ValueChanged, nativeTextEdit: typing).ConfigureAwait(true);
        if (!accepted)
        {
            if (focusIndex >= 0 && ReferenceEquals(pendingFocusValue, value))
            {
                pendingFocusValue = null;
            }

            pendingCompleteValue = previousComplete;
            pendingCompleteDetails = previousDetails;
            return null;
        }

        return value;
    }

    // OTPFieldRoot.tsx:201-226 (useValueChanged): after a committed value renders, the queued focus and completion run when
    // they were queued for that value.
    private async Task ProcessCommittedValueAsync()
    {
        if (processedVersion == valueVersion)
        {
            return;
        }

        processedVersion = valueVersion;
        if (pendingFocusValue is not null)
        {
            string queued = pendingFocusValue;
            pendingFocusValue = null;
            if (string.Equals(queued, normalized, StringComparison.Ordinal))
            {
                focusRequest = new OtpFieldFocusRequest(pendingFocusIndex, (focusRequest?.Revision ?? 0) + 1);
            }
        }

        if (pendingCompleteValue is not null)
        {
            string queued = pendingCompleteValue;
            GenericEventDetails details = pendingCompleteDetails!;
            pendingCompleteValue = null;
            pendingCompleteDetails = null;
            if (string.Equals(queued, normalized, StringComparison.Ordinal))
            {
                await CompleteAsync(queued, details).ConfigureAwait(true);
            }
        }
    }

    // OTP-EVT-08..15 (OTPFieldRoot.tsx:192-199): the completion, then AutoSubmit's revision (design point 7 (a)).
    private async Task CompleteAsync(string completed, GenericEventDetails details)
    {
        await OnValueComplete.InvokeAsync(new OtpFieldCompleteEventArgs(completed, details)).ConfigureAwait(true);
        if (AutoSubmit)
        {
            submitRevision++;
            StateHasChanged();
        }
    }

    // OTP-FRM-11..13 (OTPFieldRoot.tsx:409-431): an autofill of the hidden input commits like typing, then focuses the last
    // filled slot.
    private void OnAutofilled(FormAutofillReport report) => _ = InvokeAsync(() => HandleAutofillAsync(report));

    private async Task HandleAutofillAsync(FormAutofillReport report)
    {
        if (report.DefaultPrevented || EffectiveDisabled || ReadOnly)
        {
            return;
        }

        (string next, bool rejected) = OtpValue.NormalizeWithDetails(report.Text, Length, EffectiveValidationType, NormalizeValue);
        if (rejected)
        {
            await OnValueInvalid.InvokeAsync(new OtpFieldInvalidEventArgs(report.Text, new GenericEventDetails(EventReason.InputChange, report.Event))).ConfigureAwait(true);
        }

        await SetValueAsync(next, EventReason.InputChange, report.Event, next.Length - 1).ConfigureAwait(true);
        Refresh();
    }

    private void OnEdited(OtpFieldEditReport edit) => _ = InvokeAsync(() => HandleEditAsync(edit));

    private void OnSlotFocused(OtpFieldFocusReport report) => _ = InvokeAsync(() => HandleSlotFocus(FindSlot(report.Index) ?? (object)this, report.Index));

    private void OnSlotBlurred(OtpFieldBlurReport report) => _ = InvokeAsync(() => HandleLeaveAsync(focusOwner ?? FindSlot(report.Index) ?? (object)this));

    // OTP-EDGE-05 (OTPFieldInput.tsx:312-321).
    private void OnPasteUnreadable(OtpFieldPasteUnreadableReport report) =>
        _ = InvokeAsync(() => Warn("Iodine: `<OtpFieldInput>` could not read clipboard text during paste handling."));

    private OtpFieldInput? FindSlot(int index)
    {
        foreach (OtpFieldInput slot in slotParts)
        {
            if (slot.Index == index)
            {
                return slot;
            }
        }

        return null;
    }

    private OtpFieldInput? FindSlot(EventSnapshot snapshot)
    {
        string? id = snapshot.Target?.Id;
        foreach (OtpFieldInput slot in slotParts)
        {
            if (string.Equals(SlotId(slot.Index), id, StringComparison.Ordinal))
            {
                return slot;
            }
        }

        return null;
    }

    // Design points 1 and 2 (a): on WebAssembly the slot's consumer handler runs first and may veto; the request then runs
    // inside the dispatch, and its update may reach the module before the handler returns.
    private DecisionPolicy DecideEdit(EventSnapshot snapshot)
    {
        OtpFieldEditReport? edit = twin!.PendingEdit;
        OtpFieldInput? slot = edit is null ? FindSlot(snapshot) : FindSlot(edit.Index);
        if (slot is not null && slot.DecideConsumer(snapshot) != DecisionPolicy.Run)
        {
            return DecisionPolicy.Stop;
        }

        if (edit is not null)
        {
            Task task = HandleEditAsync(edit);
            if (task.IsFaulted)
            {
                _ = DispatchExceptionAsync(task.Exception!.GetBaseException());
            }
        }

        return DecisionPolicy.Run;
    }

    private DecisionPolicy DecideSlot(EventSnapshot snapshot) => FindSlot(snapshot)?.DecideConsumer(snapshot) ?? DecisionPolicy.Run;

    private int PointsMask()
    {
        int mask = 0;
        foreach (OtpFieldInput slot in slotParts)
        {
            mask |= (slot.HasConsumer(OtpFieldTwin.KeyPoint) ? 1 : 0)
                | (slot.HasConsumer(OtpFieldTwin.PressPoint) ? 2 : 0)
                | (slot.HasConsumer(OtpFieldTwin.FocusPoint) ? 4 : 0)
                | (slot.HasConsumer(OtpFieldTwin.BlurPoint) ? 8 : 0);
        }

        return mask;
    }

    private static List<string>? Points(int mask)
    {
        if (mask == 0)
        {
            return null;
        }

        List<string> points = [];
        if ((mask & 1) != 0)
        {
            points.Add(OtpFieldTwin.KeyPoint);
        }

        if ((mask & 2) != 0)
        {
            points.Add(OtpFieldTwin.PressPoint);
        }

        if ((mask & 4) != 0)
        {
            points.Add(OtpFieldTwin.FocusPoint);
        }

        if ((mask & 8) != 0)
        {
            points.Add(OtpFieldTwin.BlurPoint);
        }

        return points;
    }

    private int SentPointsMask()
    {
        int mask = 0;
        if (sentOptions?.Points is { } points)
        {
            foreach (string point in points)
            {
                mask |= point switch
                {
                    OtpFieldTwin.KeyPoint => 1,
                    OtpFieldTwin.PressPoint => 2,
                    OtpFieldTwin.FocusPoint => 4,
                    _ => 8,
                };
            }
        }

        return mask;
    }

    private OtpFieldOptions BuildOptions(int mask)
    {
        int count = Math.Max(mountedSlots, 0);
        string[] ids = new string[count];
        for (int i = 0; i < count; i++)
        {
            ids[i] = SlotId(i);
        }

        return new OtpFieldOptions
        {
            Slots = ids,
            Length = Length,
            Value = normalized,
            Revision = editRevision,
            Rollback = rollback ? true : null,
            ActiveIndex = ActiveIndex,
            Focus = focusRequest,
            Disabled = EffectiveDisabled ? true : null,
            ReadOnly = ReadOnly ? true : null,
            Rtl = Rtl ? true : null,
            Optimistic = Optimistic ? true : null,
            SlotPattern = SlotPattern,
            SubmitRevision = submitRevision,
            Form = Form,
            Points = Points(mask),
            DevelopmentChecks = DevelopmentWarnings.IsEnabled ? true : null,
        };
    }

    // Compares before constructing, so an unchanged render allocates no options record (ADR 16 rule 7).
    private bool OptionsChanged(int mask) =>
        sentOptions is not { } sent
        || !ReferenceEquals(sent.Value, normalized)
        || sent.Length != Length
        || sent.Revision != editRevision
        || sent.Rollback is not null || rollback
        || sent.ActiveIndex != ActiveIndex
        || !ReferenceEquals(sent.Focus, focusRequest)
        || (sent.Disabled == true) != EffectiveDisabled
        || (sent.ReadOnly == true) != ReadOnly
        || (sent.Rtl == true) != Rtl
        || (sent.Optimistic == true) != Optimistic
        || !string.Equals(sent.SlotPattern, SlotPattern, StringComparison.Ordinal)
        || sent.SubmitRevision != submitRevision
        || !string.Equals(sent.Form, Form, StringComparison.Ordinal)
        || sent.Slots.Count != Math.Max(mountedSlots, 0)
        || (sent.Slots.Count > 0 && !string.Equals(sent.Slots[0], ControlId, StringComparison.Ordinal))
        || SentPointsMask() != mask;

    // Design point 6 (a): one handle on the root element, attached after the composite list's (the attach order is pinned at
    // L4).
    private async Task SyncTwinAsync()
    {
        if (Element is not { } element)
        {
            return;
        }

        int mask = PointsMask();
        if (handle is not null)
        {
            if (OptionsChanged(mask))
            {
                OtpFieldOptions next = BuildOptions(mask);
                sentOptions = next;
                rollback = false;
                await handle.UpdateAsync(next);
            }

            return;
        }

        if (twin is null)
        {
            twin = new OtpFieldTwin(JSRuntime)
            {
                EditDecider = DecideEdit,
                KeyDecider = DecideSlot,
                PressDecider = DecideSlot,
                FocusDecider = DecideSlot,
                BlurDecider = DecideSlot,
            };
            twin.Edited += OnEdited;
            twin.SlotFocused += OnSlotFocused;
            twin.SlotBlurred += OnSlotBlurred;
            twin.PasteUnreadable += OnPasteUnreadable;
            fieldHalf!.Autofilled += OnAutofilled;
        }

        OtpFieldOptions options = BuildOptions(mask);
        sentOptions = options;
        rollback = false;
        handle = await twin.AttachAsync(element, options);
        attachedSource.TrySetResult();
    }

    // OTP-ARIA-05, OTP-EDGE-13 (mergeAriaIds, OTPFieldRoot.tsx:639-642): split on whitespace, first occurrence kept.
    private static string? MergeIds(string? first, string? second)
    {
        if (string.IsNullOrWhiteSpace(first) && string.IsNullOrWhiteSpace(second))
        {
            return null;
        }

        List<string> ids = [];
        foreach (string? source in (ReadOnlySpan<string?>)[first, second])
        {
            if (source is null)
            {
                continue;
            }

            foreach (string id in source.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
            {
                if (!ids.Contains(id))
                {
                    ids.Add(id);
                }
            }
        }

        return ids.Count > 0 ? string.Join(' ', ids) : null;
    }

    private string? ConsumerAttribute(string name) =>
        AdditionalAttributes is not null && AdditionalAttributes.TryGetValue(name, out object? supplied) && supplied is string text ? text : null;

    private void ReleaseFocus()
    {
        if (focusOwner is { } owner)
        {
            fieldHalf?.ReleaseFocus(owner);
        }

        focusOwner = null;
        focused = false;
    }

    // OTP-EDGE-02, OTP-EDGE-03 (useOTPFieldRootDevWarnings, OTPFieldRoot.tsx:649-676), after paint.
    private void WarnAboutLength()
    {
        if (!DevelopmentWarnings.IsEnabled)
        {
            return;
        }

        if (Length <= 0)
        {
            Warn($"Iodine: `<OtpFieldRoot>` `Length` must be a positive integer. Received `Length={Length.ToString(CultureInfo.InvariantCulture)}`.");
            return;
        }

        if (mountedSlots == 0 || mountedSlots == Length)
        {
            return;
        }

        string count = mountedSlots.ToString(CultureInfo.InvariantCulture);
        Warn(
            $"Iodine: `<OtpFieldRoot>` `Length` must match the number of rendered `<OtpFieldInput />` parts. Received `Length={Length.ToString(CultureInfo.InvariantCulture)}` but rendered {count} input{(mountedSlots == 1 ? string.Empty : "s")}.");
    }

    // OTP-EDGE-02, OTP-EDGE-03: once per message text per session (UTL-EDGE-11/12; the scoped log, granted by the
    // orchestrator 2026-11-19).
    private void Warn(string message) => DevelopmentLog.Report(Logger, message);

    // Returns whether the state changed; a new record is made only then (ADR 16 rule 7), and the slots are told first, so they
    // render in the same batch as the root.
    private bool UpdateState()
    {
        Normalize();
        FieldStateSnapshot snapshot = fieldHalf!.Snapshot;
        bool disabled = snapshot.Disabled;
        bool filled = normalized.Length > 0;
        bool complete = normalized.Length == Length;
        if (state is { } previous
            && ReferenceEquals(previous.Value, normalized)
            && previous.Length == Length
            && previous.Disabled == disabled
            && previous.Touched == snapshot.Touched
            && previous.Dirty == snapshot.Dirty
            && previous.Valid == snapshot.Valid
            && previous.Filled == filled
            && previous.Focused == focused
            && previous.Complete == complete
            && previous.ReadOnly == ReadOnly
            && previous.Required == Required)
        {
            return false;
        }

        state = new OtpFieldRootState(normalized, Length, disabled, snapshot.Touched, snapshot.Dirty, snapshot.Valid, filled, focused, complete, ReadOnly, Required);
        context.SetState(state);
        return true;
    }

    // OTP-STA-06, OTP-EDGE-14: the shown value is re-derived only when what it derives from changed, without callbacks.
    private void Normalize()
    {
        string raw = value!.Value ?? string.Empty;
        OtpFieldValidationType type = EffectiveValidationType;
        if (ReferenceEquals(raw, normalizedFrom) && Length == normalizedLength && type == normalizedType && ReferenceEquals(NormalizeValue, normalizedWith))
        {
            return;
        }

        normalizedFrom = raw;
        normalizedLength = Length;
        normalizedType = type;
        normalizedWith = NormalizeValue;
        string next = OtpValue.Normalize(raw, Length, type, NormalizeValue);
        if (!string.Equals(next, normalized, StringComparison.Ordinal) || valueVersion == 0)
        {
            normalized = next;
            if (valueVersion == 0 || !string.Equals(versionedValue, next, StringComparison.Ordinal))
            {
                valueVersion++;
                versionedValue = next;
            }
        }
    }
}

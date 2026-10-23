using Blazix.Primitives.Interop;
using Blazix.Primitives.Rendering;
using Blazix.Primitives.Store;
using Blazix.Primitives.Utilities;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace Blazix.Primitives;

// RAD-STA-03; radio/root/RadioRoot.tsx:226-235,283-320 (the field's state with four radio fields). ADR 08 §3: the state
// record is sealed, partial and [PartState]; one record per part.
/// <summary>Represents the state of a radio button, which the part exposes as data attributes and passes to its class and style functions.</summary>
/// <param name="Checked">A value indicating whether the radio button is selected; projected as <c>data-checked</c> or <c>data-unchecked</c>.</param>
/// <param name="Disabled">A value indicating whether the radio button ignores user interaction; projected as <c>data-disabled</c>.</param>
/// <param name="ReadOnly">A value indicating whether the user cannot select the radio button; projected as <c>data-readonly</c>.</param>
/// <param name="Required">A value indicating whether a radio button of the group must be selected to submit its form; projected as <c>data-required</c>.</param>
/// <param name="Touched">A value indicating whether a radio button of the field was selected or focus left the group; projected as <c>data-touched</c>.</param>
/// <param name="Dirty">A value indicating whether the group's value differs from the field's initial one; projected as <c>data-dirty</c>.</param>
/// <param name="Valid">
/// The field's validity: <see langword="true"/> projects <c>data-valid</c>, <see langword="false"/> projects
/// <c>data-invalid</c>, and <see langword="null"/>, before validation, projects neither.
/// </param>
/// <param name="Filled">A value indicating whether the field has a selected value; projected as <c>data-filled</c>.</param>
/// <param name="Focused">A value indicating whether a radio button of the field has focus; projected as <c>data-focused</c>.</param>
[PartState]
public sealed partial record RadioRootState(bool Checked, bool Disabled, bool ReadOnly, bool Required, bool Touched, bool Dirty, bool? Valid, bool Filled, bool Focused);

// RAD §3.3; radio/root/RadioRootDataAttributes.ts, in its order. No data-composite-item-active: it is a root attribute, not a
// state field (radio §16 Q3).
/// <summary>Provides the names of the data attributes a radio button renders.</summary>
public static class RadioRootDataAttributes
{
    /// <summary>Represents the attribute present, with an empty value, while the radio button is selected: <c>data-checked</c>.</summary>
    public const string Checked = "data-checked";

    /// <summary>Represents the attribute present, with an empty value, while the radio button is not selected: <c>data-unchecked</c>.</summary>
    public const string Unchecked = "data-unchecked";

    /// <summary>Represents the attribute present, with an empty value, while the radio button is disabled: <c>data-disabled</c>.</summary>
    public const string Disabled = "data-disabled";

    /// <summary>Represents the attribute present, with an empty value, while the radio button is read-only: <c>data-readonly</c>.</summary>
    public const string Readonly = "data-readonly";

    /// <summary>Represents the attribute present, with an empty value, while a selection is required: <c>data-required</c>.</summary>
    public const string Required = "data-required";

    /// <summary>Represents the attribute present, with an empty value, while the radio button's field is valid: <c>data-valid</c>.</summary>
    public const string Valid = "data-valid";

    /// <summary>Represents the attribute present, with an empty value, while the radio button's field is invalid: <c>data-invalid</c>.</summary>
    public const string Invalid = "data-invalid";

    /// <summary>Represents the attribute present, with an empty value, once the radio button's field was touched: <c>data-touched</c>.</summary>
    public const string Touched = "data-touched";

    /// <summary>Represents the attribute present, with an empty value, while the value differs from the field's initial one: <c>data-dirty</c>.</summary>
    public const string Dirty = "data-dirty";

    /// <summary>Represents the attribute present, with an empty value, while the radio button's field has a value: <c>data-filled</c>.</summary>
    public const string Filled = "data-filled";

    /// <summary>Represents the attribute present, with an empty value, while a radio button of the field has focus: <c>data-focused</c>.</summary>
    public const string Focused = "data-focused";
}

// Base UI's Radio.Root (radio/root/RadioRoot.tsx); the contract is docs/components/radio.md.
// - The element is a button-like span (ButtonBehavior with composite: false, RAD §11; non-native by default, RAD-API-05)
//   whose role the part replaces with `radio` (the consumer's role still wins, RAD-ARIA-01). Enter never selects it
//   (SuppressEnter, RAD-KEY-02); Space selects on keyup (RAD-KEY-01).
// - After it the part renders the visually hidden `<input type="radio">` (BuildAfterElement, ADR 02 Amendments
//   2026-10-22; RAD-STR-01, RAD-DOM-08..13): every selection is a click on that input, which the `button` module forwards
//   from the root (the hidden-input bridge) and hands to RequestToggleAsync, inside the click on WebAssembly or as a
//   report on Server. The input's `checked` is the `dom` primitive's from attach (ADR 04 Amendments 2026-10-22): C#
//   renders it once and then sends it in DomOptions, raising CheckedRestores when a request is refused.
// - The value belongs to the enclosing RadioGroup<TValue>, read through RadioGroupContext<TValue>; the radio asks the group
//   to adopt its Value (RAD-EVT-01). Outside a group the radio is static: checked iff its Value is the empty string
//   (RAD-STA-01/02, radio §16 Q1), and an activation only marks its field touched (RAD-EVT-02).
// - Inside a group the element is also a composite item (CompositeItemBehavior; RAD-STR-03, RAD-FOC-01/06): it renders
//   tabindex="-1" after the button writer's value, so the composite's wins, and the `composite` module owns the value from
//   attach. The module's focus-driven selection clicks the hidden input (RAD-FOC-05).
// - id: in non-native mode the root keeps its generated id and the labelable control id goes to the input; in native mode
//   the root takes the labelable control id and the input has none (RAD-API-07, RAD-DOM-01/10).
// - Inside a field the radio is never the field's control (the group is, RDG §8): CheckableFieldBehavior claims the
//   labelable scope's control id, keeps the focused flag, makes the hidden input one of the field's validation inputs in
//   a group (RAD-FRM-03) and composes the label and description ids; the radio itself writes filled on a checked mount
//   (RAD-STA-05) and touched after a selection (RAD-FRM-05).
/// <summary>
/// Represents one option of a radio group: Base UI's <c>Radio.Root</c>, rendered as a <c>span</c> with
/// <c>role="radio"</c> followed by the hidden radio input that takes part in forms, native validation and label
/// activation.
/// </summary>
/// <typeparam name="TValue">The type of the value that identifies the radio button within its group.</typeparam>
/// <remarks>
/// Within a <see cref="RadioGroup{TValue}"/> the radio button is selected when the group's value is its
/// <see cref="Value"/>, and selecting it asks the group to adopt that value. Outside a group it is selected only when
/// <see cref="Value"/> is the empty string, and user interaction never changes it.
/// </remarks>
public sealed class RadioRoot<TValue> : PartBase<RadioRootState>
{
    private const string DefaultTag = "span";
    private const string RadioType = "radio";
    private const string ArrowKeyPrefix = "Arrow";

    private static readonly StateAttributesMapping<RadioRootState> s_mapping = RadioStateMapping.Create<RadioRootState>();

    private readonly RadioRootContext context = new();
    private readonly EventCallback onKeyDown;
    private readonly EventCallback onKeyUp;
    private readonly EventCallback onClick;
    private readonly EventCallback onFocus;
    private readonly Action fieldStateChanged;
    private readonly Action groupChanged;
    private readonly Action<ElementReference> captureInput;
    private readonly TaskCompletionSource inputAttached = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private ButtonBehavior? behavior;
    private CheckableFieldBehavior? fieldBehavior;
    private CompositeItemBehavior? item;
    private RadioGroupContext<TValue>? group;
    private DomTwin? domTwin;
    private TwinHandle<DomOptions>? domHandle;
    private DomOptions? sentDomOptions;
    private ElementReference? input;
    private string? controlId;
    private bool? renderedChecked;
    private string? renderedName;
    private string? renderedForm;
    private int checkedRestores;
    private RadioRootState? state;
    private RenderFragment? cascade;
    private RenderFragment? cascadedContent;
    private AttributeValue consumerFocus;
    private bool focusVetoed;
    private bool serialized;
    private TValue serializedValue = default!;
    private string? formValue;
    private bool disposed;

    /// <summary>Initializes a new instance of the <see cref="RadioRoot{TValue}"/> class.</summary>
    public RadioRoot()
    {
        onKeyDown = CreateHandler<KeyboardEventArgs>("onkeydown", HandleKeyDownAsync);
        onKeyUp = CreateHandler<KeyboardEventArgs>("onkeyup", e => behavior!.HandleKeyUpAsync(e));
        onClick = CreateHandler<MouseEventArgs>("onclick", e => behavior!.HandleClickAsync(e));
        onFocus = CreateHandler<FocusEventArgs>("onfocus", HandleFocusAsync);
        fieldStateChanged = OnFieldStateChanged;
        groupChanged = OnGroupChanged;
        captureInput = element => input = element;
    }

    [Inject]
    private IJSRuntime JSRuntime { get; set; } = null!;

    [Inject]
    private ILogger<RadioRoot<TValue>> Logger { get; set; } = null!;

    [Inject]
    private ButtonDevelopmentLog DevelopmentLog { get; set; } = null!;

    [Inject]
    private PartIdGenerator IdGenerator { get; set; } = null!;

    [Inject]
    private DefaultFormScope Formless { get; set; } = null!;

    // RAD-STA-01: in a group, the group's value strictly equals Value (ADR 08 Amendments 2026-10-22: null selects nothing);
    // standalone, Value is the empty string.
    private bool IsChecked => group is not null
        ? group.IsSelected(Value)
        : typeof(TValue) == typeof(string) && (object?)Value is string { Length: 0 };

    // RAD-API-02: the field's, the item's, the group's and the radio's own disabled state, OR'd.
    private bool EffectiveDisabled => fieldBehavior!.EffectiveDisabled;

    // RAD-API-03, RAD-API-04: the group's state OR'd with the radio's own.
    private bool EffectiveReadOnly => ReadOnly || (group?.ReadOnly ?? false);

    private bool EffectiveRequired => Required || (group?.Required ?? false);

    // RAD-DOM-09: the group's effective name and form; a radio has neither of its own.
    private string? EffectiveName => group?.Name;

    private string? EffectiveForm => group?.Form;

    private string RootId => NativeButton ? fieldBehavior!.ControlId : GeneratedId!;

    private string? InputId => NativeButton ? null : fieldBehavior!.ControlId;

    // RAD-ARIA-03 step 2, RAD-EDGE-05: a non-blank consumer aria-label names the radio, even with a label in its scope.
    private bool ConsumerNamesRadio => AdditionalAttributes is not null
        && AdditionalAttributes.TryGetValue("aria-label", out object? label)
        && label is string text
        && !string.IsNullOrWhiteSpace(text);

    // RAD-ARIA-03 step 1: the consumer's aria-labelledby wins at the merge.
    private bool ConsumerLabelsRadio => AdditionalAttributes is not null && AdditionalAttributes.ContainsKey("aria-labelledby");

    [CascadingParameter]
    private RadioGroupContext<TValue>? GroupContext { get; set; }

    [CascadingParameter]
    private RadioGroupContext? AnyGroupContext { get; set; }

    [CascadingParameter]
    private CompositeRootContext? CompositeContext { get; set; }

    [CascadingParameter]
    private FieldRootContext? FieldContext { get; set; }

    [CascadingParameter]
    private FieldItemContext? ItemContext { get; set; }

    [CascadingParameter]
    private IFormScope? FormScope { get; set; }

    // RAD-API-01, RAD-DOM-11
    /// <summary>Gets or sets the value that identifies the radio button within its group.</summary>
    /// <value>
    /// The value, compared with the group's value by identity for a reference type, by ordinal value for a
    /// <see cref="string"/> and by value for a value type. The hidden input submits it as text. A
    /// <see langword="null"/> value is never selected and renders no <c>value</c> attribute.
    /// </value>
    [Parameter, EditorRequired]
    public TValue Value { get; set; } = default!;

    // RAD-API-02
    /// <summary>Gets or sets a value indicating whether the radio button ignores user interaction.</summary>
    /// <value>
    /// <see langword="true"/> to ignore clicks and keys, which also withholds the consumer's press and click handlers and
    /// keeps the radio button out of form submission; otherwise, <see langword="false"/>. A disabled group, field or field
    /// item disables it too. The default is <see langword="false"/>.
    /// </value>
    [Parameter]
    public bool Disabled { get; set; }

    // RAD-API-03, RAD-EDGE-04
    /// <summary>Gets or sets a value indicating whether the user cannot select the radio button.</summary>
    /// <value>
    /// <see langword="true"/> to keep the selection while the radio button stays focusable and submits with its form;
    /// a read-only group makes it read-only too. The default is <see langword="false"/>.
    /// </value>
    [Parameter]
    public bool ReadOnly { get; set; }

    // RAD-API-04
    /// <summary>Gets or sets a value indicating whether a radio button of the group must be selected for the form to submit.</summary>
    /// <value>
    /// <see langword="true"/> to set <c>required</c> on the hidden input; a required group sets it too. The default is
    /// <see langword="false"/>.
    /// </value>
    [Parameter]
    public bool Required { get; set; }

    // RAD-API-05, UBT-API-03
    /// <summary>Gets or sets a value indicating whether the rendered element is a native <c>button</c>.</summary>
    /// <value>
    /// <see langword="true"/> when <see cref="PartBase{TState}.Render"/> supplies a <c>button</c>, which then also takes the
    /// consumer's <c>id</c>; <see langword="false"/>, the default, for the <c>span</c>, which gets a <c>tabindex</c> and
    /// clicks from Space.
    /// </value>
    [Parameter]
    public bool NativeButton { get; set; }

    /// <inheritdoc />
    protected override string DefaultTagName => DefaultTag;

    /// <inheritdoc />
    protected override RadioRootState State => state!;

    /// <inheritdoc />
    protected override StateAttributesMapping<RadioRootState>? StateAttributesMapping => s_mapping;

    // RAD-DOM-01: the root always has an id (the generated one, or the control id in native mode).
    /// <inheritdoc />
    protected override bool UsesGeneratedId => true;

    // For the group: the id the composite item registers and the focus-driven selection names.
    /// <summary>Gets the id of the radio button's element.</summary>
    /// <value>The generated id, or the labelable control id of a native button.</value>
    internal string ElementId => RootId;

    /// <summary>Gets a value indicating whether the radio button is selected.</summary>
    /// <value>The value the hidden input's <c>checked</c> property mirrors.</value>
    internal bool Checked => IsChecked;

    /// <summary>Gets the field behavior of the radio button's hidden input.</summary>
    /// <value>The behavior, which the group refreshes and reads for its form value.</value>
    internal CheckableFieldBehavior FieldBehavior => fieldBehavior!;

    // For the fixtures (tests/Blazix.Primitives.Fixtures.Client): completes once the twins are attached, so a browser test
    // can wait for the modules' listeners before it clicks.
    /// <summary>Gets a task that completes when the radio button's behaviors are attached to its element and its hidden input.</summary>
    /// <value>A task that completes after the first interactive render has attached them.</value>
    internal Task TwinAttached => Task.WhenAll(behavior!.TwinAttached, inputAttached.Task, fieldBehavior!.TwinAttached);

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">The radio button is inside a <see cref="RadioGroup{TValue}"/> whose value type differs from <typeparamref name="TValue"/>.</exception>
    protected override void OnInitialized()
    {
        // Design point R7 (pending the user): a type mismatch would leave the radio outside its group, static and never
        // selectable; it is reported in every build, as a missing context is.
        if (GroupContext is null && AnyGroupContext is not null)
        {
            throw new InvalidOperationException(
                "Blazix: A `RadioRoot` must have the `TValue` of its `RadioGroup`. Set `TValue` on the radio button, or give `Value` the group's type, for example `Value=\"(int?)1\"` in a `RadioGroup` of `int?`.");
        }

        behavior = new ButtonBehavior(JSRuntime, Logger, DevelopmentLog, IdGenerator, StateHasChanged, DispatchExceptionAsync, onKeyDown, onKeyUp, onClick)
        {
            HiddenInput = true,
            SuppressEnter = true,
            ToggleRequest = RequestToggleAsync,
            RestoreRequested = RestoreInput,
        };
        base.OnInitialized();

        // RAD-DOM-10: the labelable control id, drawn after the root's own, in render order (ADR 02 decision 5).
        controlId = IdGenerator.Next();
        fieldBehavior = new CheckableFieldBehavior(controlId, Formless, JSRuntime, fieldStateChanged, InvokeAsync, DispatchExceptionAsync);
        fieldBehavior.Initialize(FieldContext, ItemContext);
        group = GroupContext;
        if (group is not null)
        {
            // RAD-STR-03: a composite item only inside a group. The consumer's onkeydown runs through the item's gate for
            // the keys the group handles, under option (i) of ADR 04's note of 2026-10-22 (HandleKeyDownAsync).
            item = new CompositeItemBehavior(CompositeContext, "RadioRoot", onKeyDown);
            group.AddListener(groupChanged);
            group.Register(this);
            fieldBehavior.GroupRefresh = group.RefreshMembersAsync;
        }
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        SerializeValue();

        // RDG §8: the group is the field's control; a grouped radio's hidden input is one of the field's validation inputs
        // (RAD-FRM-03); a radio claims its labelable scope's control id (useLabelableId, FFI-ALG-07) and takes its label
        // (RAD-ARIA-03 step 3), unless it shares the scope with its group (design point R6, pending the user: the inputs
        // keep distinct ids, CheckboxGroup's CKG-ARIA-07 precedent). A radio has no name and no expression of its own. Its
        // blur: inside a group the group owns it (RDG-FOC-04/05); standalone it only releases the focused flag (RAD-FOC-03;
        // design point R4, pending the user).
        fieldBehavior!.GroupDisabled = group?.Disabled ?? false;
        fieldBehavior.SetParameters(
            new CheckableFieldParameters(
                Name: null,
                Disabled,
                IsChecked,
                BlurValue: null,
                OwnsValue: false,
                ClaimsControlId: !fieldBehavior.SharesScope(group?.Scope),
                RegistersInput: group is not null,
                EnterSubmit: false,
                LabelFallback: !NativeButton && !ConsumerNamesRadio && !ConsumerLabelsRadio,
                CheckedExpression: null,
                EditContext: null,
                AdditionalAttributes,
                Enabled,
                BlurTouches: false,
                BlurReleasesFocus: group is null),
            FormScope);
        behavior!.Disabled = EffectiveDisabled;
        behavior.Native = NativeButton;
        behavior.ReadOnly = EffectiveReadOnly;
        behavior.SetConsumerAttributes(AdditionalAttributes);
        if (item is not null)
        {
            item.SetRegistration(RootId, AdditionalAttributes);
            // RAD-KEY-06: a disabled radio's key handlers are not invoked, so the group's key handling runs none either.
            item.SetConsumerAttributes(EffectiveDisabled ? null : AdditionalAttributes);
            consumerFocus = FindConsumerFocus();
        }

        UpdateState();

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
        if (disposed)
        {
            return;
        }

        await behavior!.AfterRenderAsync(Enabled, Element, RootId, composite: false);
        if (item is not null)
        {
            await item.AfterRenderAsync(Element);
        }

        await AttachInputAsync();
        await fieldBehavior!.AfterRenderAsync(Element, RootId, GeneratedId!, input, InputId);

        // RAD-STA-05; radio/root/RadioRoot.tsx:98-102: a radio mounted checked fills its field, once; only the group
        // clears it (RDG-STA-05).
        if (firstRender && Enabled && IsChecked)
        {
            FieldContext?.SetFilled(true);
        }

        group?.RadioRendered();
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing && !disposed)
        {
            disposed = true;
            behavior?.Dispose();
            fieldBehavior?.Dispose();
            item?.Dispose();
            if (group is not null)
            {
                group.RemoveListener(groupChanged);
                group.Unregister(this);
            }

            if (domTwin is not null)
            {
                _ = domTwin.DisposeAsync().AsTask();
                domTwin = null;
                domHandle = null;
            }
        }

        base.Dispose(disposing);
    }

    /// <inheritdoc />
    protected override void AddPartAttributes(AttributeList attributes)
    {
        behavior!.AddAttributes(attributes, RootId, composite: false);

        // RAD-FOC-01, RAD §14 "Explicit composite: false": inside a group the composite item's tabindex replaces the button
        // writer's, and the consumer's still wins at the merge (RAD-DOM-16).
        item?.AddAttributes(attributes);

        // RAD-ARIA-01, RAD-DOM-07: the writer's role is replaced, and the consumer's role still wins at the merge.
        bool isChecked = IsChecked;
        attributes.Set("role", "radio");
        attributes.Set("aria-checked", isChecked ? "true" : "false");

        // RAD-DOM-02: the marker the composite reads for the initial tab stop, inside and outside a group.
        if (isChecked)
        {
            attributes.Set(CompositeAttributes.ActiveItem, string.Empty);
        }

        if (NativeButton)
        {
            // RAD-DOM-01: the labelable control id.
            attributes.Set("id", RootId);
        }

        if (fieldBehavior!.InField)
        {
            fieldBehavior.Rendered();
        }

        // RAD-FOC-03, RAD-FOC-05: the field's focused flag, and in a group the consumer's onfocus, whose veto the group's
        // select decision reads.
        if (fieldBehavior.InField || consumerFocus.IsHandler)
        {
            attributes.Set("onfocus", onFocus);
        }

        // RAD-ARIA-03: the consumer's aria-labelledby replaces this at the merge; a non-blank aria-label leaves it out;
        // then the label of the radio's labelable scope, then the input's native label (RAD-ARIA-04, the form module).
        if (!ConsumerNamesRadio && (fieldBehavior.LabelId ?? fieldBehavior.FallbackLabelId) is { } labelId)
        {
            attributes.Set("aria-labelledby", labelId);
        }
    }

    // ADR 02 Amendments 2026-10-21 (FinishAttributes); part-owned by the document: the root's id (RAD-DOM-01: in non-native
    // mode the consumer's `id` is the hidden input's, RAD-API-07; in native mode an empty one counts as absent; a substitute
    // element's own id wins, RND-DOM-32), the composed aria-describedby (RAD-ARIA-07) and aria-invalid in a group while the
    // field is invalid (RAD-ARIA-08).
    /// <inheritdoc />
    protected override void FinishAttributes(AttributeList attributes)
    {
        string? merged = attributes.TryGetValue("id", out AttributeValue mergedId) && mergedId.TryGetValue(out string idText) ? idText : null;
        string? consumerId = AdditionalAttributes is not null && AdditionalAttributes.TryGetValue("id", out object? supplied) ? supplied as string : null;
        string partId = NativeButton ? RootId : GeneratedId!;
        if (merged is null || string.Equals(merged, consumerId, StringComparison.Ordinal) || string.Equals(merged, partId, StringComparison.Ordinal))
        {
            attributes.Set("id", RootId);
        }

        if (!fieldBehavior!.InField)
        {
            return;
        }

        string? consumer = attributes.TryGetValue("aria-describedby", out AttributeValue current) && current.TryGetValue(out string text) ? text : null;
        if (fieldBehavior.DescribedBy(consumer) is { } describedBy)
        {
            attributes.Set("aria-describedby", describedBy);
        }
        else
        {
            attributes.Remove("aria-describedby");
        }

        // RAD-ARIA-08: a standalone radio has no access to the group's validation.
        if (group is not null && fieldBehavior.AriaInvalid)
        {
            attributes.Set("aria-invalid", "true");
        }
    }

    /// <inheritdoc />
    protected override bool ForwardsConsumerHandler(string eventName)
    {
        if (group is not null && string.Equals(eventName, "onfocus", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return behavior!.ForwardsConsumerHandler(eventName) && (item?.ForwardsConsumerHandler(eventName) ?? true);
    }

    // RAD-STR-01, RAD-DOM-08..13; radio/root/RadioRoot.tsx:182-224,276.
    /// <inheritdoc />
    protected override void BuildAfterElement(RenderTreeBuilder builder)
    {
        string? name = EffectiveName;
        renderedName = name;
        renderedForm = EffectiveForm;

        // The handover at attach (ADR 04 Amendments 2026-10-18 and 2026-10-22): `checked` is rendered from the first render's
        // value and then frozen, so Blazor never writes the property the `dom` primitive owns.
        renderedChecked ??= IsChecked;
        builder.OpenElement(0, "input");
        builder.AddAttribute(1, "type", RadioType);
        builder.AddAttribute(2, "form", renderedForm);
        builder.AddAttribute(3, "id", InputId);
        builder.AddAttribute(4, "name", name);
        builder.AddAttribute(5, "tabindex", "-1");
        builder.AddAttribute(6, "style", string.IsNullOrEmpty(name) ? VisuallyHidden.Style : VisuallyHidden.InputStyle);
        builder.AddAttribute(7, "aria-hidden", "true");
        builder.AddAttribute(8, "value", formValue);
        builder.AddAttribute(9, "disabled", EffectiveDisabled);
        builder.AddAttribute(10, "checked", renderedChecked.Value);
        builder.AddAttribute(11, "required", EffectiveRequired);

        // Radio §16 Q5: emitted although a radio input ignores it.
        builder.AddAttribute(12, "readonly", EffectiveReadOnly);
        builder.AddElementReferenceCapture(13, captureInput);
        builder.CloseElement();
    }

    // For the group's `select` decision (RAD-FOC-05, ADR 04 Amendments 2026-10-20): a consumer onfocus that called
    // PreventPartHandler() vetoes the focus-driven selection on WebAssembly; on Server the decision falls back to run.
    /// <summary>Answers the focus-driven selection of this radio button.</summary>
    /// <returns><see cref="DecisionPolicy.Stop"/> when the consumer's focus handler vetoed the focus that asks it.</returns>
    internal DecisionPolicy DecideSelection()
    {
        bool vetoed = focusVetoed;
        focusVetoed = false;
        return vetoed ? DecisionPolicy.Stop : DecisionPolicy.Run;
    }

    // RDG-EVT-04 on Server (design point R2 (c), pending the user): the browser unchecked this radio's input natively when a
    // same-name sibling was clicked, and the request was refused, so the input is restored without a render.
    /// <summary>Restores the hidden input's <c>checked</c> property from the radio button's state.</summary>
    internal void RestoreCheckedInput()
    {
        checkedRestores++;
        _ = SendRestoreAsync();
    }

    // RAD-EVT-01..03, RDG-EVT-01..07: the request of one click on the hidden input. A radio input only ever turns on, so
    // the next value is "selected"; the result says whether the radio now holds what the input shows. When it does not,
    // the module reverts the click (WebAssembly) or the part restores the input (RestoreInput).
    private async Task<bool> RequestToggleAsync(bool? reported, EventSnapshot snapshot)
    {
        // RDG-EVT-06: a click on the radio that is already selected is not a change (the native input fires no `change`);
        // C# holds the value, so this also covers a click that races a commit on Server.
        if (IsChecked)
        {
            return true;
        }

        // RAD-EVT-03, RDG-EVT-07: ignored entirely, and the input re-synchronized.
        if (EffectiveDisabled || EffectiveReadOnly || RadioValues.IsNull(Value))
        {
            return false;
        }

        // RAD-EVT-01, RDG-EVT-02/03: reason none, the input's click as the event, no trigger.
        ChangeEventDetails details = new(EventReason.None, snapshot);

        // RAD-EVT-02: outside a group the request is a no-op that is never canceled.
        bool accepted = group is null || await group.Request(Value, details);
        if (accepted)
        {
            // RAD-FRM-05: every selection that was not canceled touches the field.
            FieldContext?.SetTouched(true);
        }

        if (UpdateState())
        {
            StateHasChanged();
        }

        return IsChecked;
    }

    // RAD-KEY-06 and option (i) of ADR 04's note of 2026-10-22: the button mechanism and the composite item both decline the
    // consumer's onkeydown, and a key reaches at most one decision point (Space the `button` module's `activate`, an arrow
    // the composite's `navigate`), so the part's one handler runs it through the gate whose one-shot marker that key may
    // have left.
    private Task HandleKeyDownAsync(KeyboardEventArgs e)
    {
        return item is not null && e.Key is { } key && key.StartsWith(ArrowKeyPrefix, StringComparison.Ordinal)
            ? item.HandleKeyDownAsync(e)
            : behavior!.HandleKeyDownAsync(e);
    }

    // RAD-FOC-03: the field's focused flag, unless disabled. RAD-FOC-05: in a group the consumer's onfocus runs first, as
    // Base UI merges it, and its PreventPartHandler() stops the radio's own focus handling, the selection included.
    private Task HandleFocusAsync(FocusEventArgs e)
    {
        Task consumerTask = Task.CompletedTask;
        if (consumerFocus.IsHandler)
        {
            consumerTask = consumerFocus.InvokeAsync(e);
            if (e.PartHandlerPrevented())
            {
                focusVetoed = true;
                return consumerTask;
            }
        }

        focusVetoed = false;
        fieldBehavior!.HandleFocus();
        return consumerTask;
    }

    private AttributeValue FindConsumerFocus()
    {
        if (AdditionalAttributes is null || !AdditionalAttributes.TryGetValue("onfocus", out object? value))
        {
            return default;
        }

        AttributeValue handler = AttributeValue.FromObject(value);
        return MergeProps.IsHandler("onfocus", handler) ? handler : default;
    }

    // RAD-DOM-11, FFI-ALG-11: computed when Value changes, not per render.
    private void SerializeValue()
    {
        if (serialized && Equality.Is(serializedValue, Value))
        {
            return;
        }

        serialized = true;
        serializedValue = Value;
        formValue = RadioValues.Serialize(Value);
    }

    // A change of the group's value, name, form or flags.
    private void OnGroupChanged()
    {
        fieldBehavior!.GroupDisabled = group!.Disabled;
        bool wasDisabled = behavior!.Disabled;
        behavior.Disabled = EffectiveDisabled;
        behavior.ReadOnly = EffectiveReadOnly;
        if (item is not null && wasDisabled != behavior.Disabled)
        {
            item.SetConsumerAttributes(EffectiveDisabled ? null : AdditionalAttributes);
        }

        if (UpdateState()
            || !string.Equals(EffectiveName, renderedName, StringComparison.Ordinal)
            || !string.Equals(EffectiveForm, renderedForm, StringComparison.Ordinal))
        {
            StateHasChanged();
        }
    }

    // A change of the field's state that the radio shows (its flags, label, descriptions, validity or disabled state).
    private void OnFieldStateChanged()
    {
        behavior!.Disabled = EffectiveDisabled;
        UpdateState();
        StateHasChanged();
    }

    // RAD-STA-02, RAD-EVT-03, RDG-EVT-04 (restore clause): the ADR 01 row "C#-origin consumer vetoes" of 2026-10-22. The
    // radio's own state did not change, so the input is restored without a render; the browser may also have unchecked
    // the selected same-name radio, which the group restores (design point R2 (c), pending the user).
    private void RestoreInput()
    {
        RestoreCheckedInput();
        group?.RestoreSelected(this);
    }

    private async Task SendRestoreAsync()
    {
        try
        {
            await AttachInputAsync();
        }
        catch (Exception exception) when (!disposed)
        {
            await DispatchExceptionAsync(exception);
        }
    }

    private async Task AttachInputAsync()
    {
        if (!Enabled || input is not { } element)
        {
            if (domHandle is { } released)
            {
                domHandle = null;
                sentDomOptions = null;
                await released.DisposeAsync();
            }

            return;
        }

        bool isChecked = IsChecked;
        if (sentDomOptions is { } sent && sent.Checked == isChecked && sent.CheckedRestores == checkedRestores)
        {
            return;
        }

        DomOptions options = new() { Checked = isChecked, CheckedRestores = checkedRestores };
        sentDomOptions = options;
        if (domHandle is { } handle && !handle.IsDisposed)
        {
            await handle.UpdateAsync(options);
            return;
        }

        domTwin ??= new DomTwin(JSRuntime);
        domHandle = await domTwin.AttachAsync(element, options);
        inputAttached.TrySetResult();
    }

    // Returns whether the state changed; a new record is made only then (ADR 16 rule 7), and the indicators are told first,
    // so they render in the same batch as the root (RAD-STA-04).
    private bool UpdateState()
    {
        bool isChecked = IsChecked;
        fieldBehavior!.SetValue(isChecked, null);
        FieldStateSnapshot snapshot = fieldBehavior.Snapshot;
        bool readOnly = EffectiveReadOnly;
        bool required = EffectiveRequired;
        if (state is { } current
            && current.Checked == isChecked
            && current.Disabled == snapshot.Disabled
            && current.ReadOnly == readOnly
            && current.Required == required
            && current.Touched == snapshot.Touched
            && current.Dirty == snapshot.Dirty
            && current.Valid == snapshot.Valid
            && current.Filled == snapshot.Filled
            && current.Focused == snapshot.Focused)
        {
            return false;
        }

        state = new RadioRootState(isChecked, snapshot.Disabled, readOnly, required, snapshot.Touched, snapshot.Dirty, snapshot.Valid, snapshot.Filled, snapshot.Focused);
        context.SetState(state);
        return true;
    }
}

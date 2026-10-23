using System.Linq.Expressions;
using Blazix.Primitives.Interop;
using Blazix.Primitives.Rendering;
using Blazix.Primitives.Utilities;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace Blazix.Primitives;

// CHK-STA-01; checkbox/root/CheckboxRoot.tsx:278-288,408-429 (the field's state with five checkbox fields). ADR 08 §3: the
// state record is sealed, partial and [PartState]; one record per part.
/// <summary>Represents the state of a checkbox, which the part exposes as data attributes and passes to its class and style functions.</summary>
/// <param name="Checked">
/// A value indicating whether the box is ticked; projected as <c>data-checked</c> or <c>data-unchecked</c>, and as neither
/// while <paramref name="Indeterminate"/> is <see langword="true"/>.
/// </param>
/// <param name="Disabled">A value indicating whether the box ignores user interaction; projected as <c>data-disabled</c>.</param>
/// <param name="ReadOnly">A value indicating whether the user cannot toggle the box; projected as <c>data-readonly</c>.</param>
/// <param name="Required">A value indicating whether the box must be ticked to submit its form; projected as <c>data-required</c>.</param>
/// <param name="Indeterminate">A value indicating whether the box is shown as mixed; projected as <c>data-indeterminate</c>.</param>
/// <param name="Touched">A value indicating whether the box's field has lost focus once; projected as <c>data-touched</c>.</param>
/// <param name="Dirty">A value indicating whether the value differs from the field's initial one; projected as <c>data-dirty</c>.</param>
/// <param name="Valid">
/// The field's validity: <see langword="true"/> projects <c>data-valid</c>, <see langword="false"/> projects
/// <c>data-invalid</c>, and <see langword="null"/>, before validation, projects neither.
/// </param>
/// <param name="Filled">A value indicating whether the field has a value; projected as <c>data-filled</c>.</param>
/// <param name="Focused">A value indicating whether the box has focus within its field; projected as <c>data-focused</c>.</param>
[PartState]
public sealed partial record CheckboxRootState(bool Checked, bool Disabled, bool ReadOnly, bool Required, bool Indeterminate, bool Touched, bool Dirty, bool? Valid, bool Filled, bool Focused);

// CHK §3.3; checkbox/root/CheckboxRootDataAttributes.ts, in its order. No data-parent, as Base UI (checkbox §16 Q1).
/// <summary>Provides the names of the data attributes a checkbox renders.</summary>
public static class CheckboxRootDataAttributes
{
    /// <summary>Represents the attribute present, with an empty value, while the box is ticked and not mixed: <c>data-checked</c>.</summary>
    public const string Checked = "data-checked";

    /// <summary>Represents the attribute present, with an empty value, while the box is not ticked and not mixed: <c>data-unchecked</c>.</summary>
    public const string Unchecked = "data-unchecked";

    /// <summary>Represents the attribute present, with an empty value, while the box is shown as mixed: <c>data-indeterminate</c>.</summary>
    public const string Indeterminate = "data-indeterminate";

    /// <summary>Represents the attribute present, with an empty value, while the box is disabled: <c>data-disabled</c>.</summary>
    public const string Disabled = "data-disabled";

    /// <summary>Represents the attribute present, with an empty value, while the box is read-only: <c>data-readonly</c>.</summary>
    public const string Readonly = "data-readonly";

    /// <summary>Represents the attribute present, with an empty value, while the box is required: <c>data-required</c>.</summary>
    public const string Required = "data-required";

    /// <summary>Represents the attribute present, with an empty value, while the box's field is valid: <c>data-valid</c>.</summary>
    public const string Valid = "data-valid";

    /// <summary>Represents the attribute present, with an empty value, while the box's field is invalid: <c>data-invalid</c>.</summary>
    public const string Invalid = "data-invalid";

    /// <summary>Represents the attribute present, with an empty value, once the box's field has lost focus: <c>data-touched</c>.</summary>
    public const string Touched = "data-touched";

    /// <summary>Represents the attribute present, with an empty value, while the value differs from the field's initial one: <c>data-dirty</c>.</summary>
    public const string Dirty = "data-dirty";

    /// <summary>Represents the attribute present, with an empty value, while the box's field has a value: <c>data-filled</c>.</summary>
    public const string Filled = "data-filled";

    /// <summary>Represents the attribute present, with an empty value, while the box has focus within a field: <c>data-focused</c>.</summary>
    public const string Focused = "data-focused";
}

// Base UI's Checkbox.Root (checkbox/root/CheckboxRoot.tsx); the contract is docs/components/checkbox.md.
// - The element is a button-like span (ButtonBehavior, UBT; non-native by default, UBT-API-03) whose role the part replaces
//   with `checkbox` (the consumer's role still wins, CHK-DOM-09). Enter never activates it (SuppressEnter, CHK-KEY-03);
//   the `form` module submits the owning form instead (EnterSubmit, CHK-KEY-04..07; design point C2, settled by the orchestrator 2026-10-23).
// - After it the part renders its form-participation inputs (BuildAfterElement, ADR 02 Amendments 2026-10-22): the
//   unchecked-value `<input type="hidden">` and the visually hidden `<input type="checkbox">` (CHK-STR-01, CHK-DOM-11..19).
// - Every toggle is a click on that input: the `button` module forwards the root's click to it (the hidden-input bridge,
//   ADR 12 Amendments 2026-10-22 item 4) and hands the toggle to RequestToggleAsync, inside the click on WebAssembly or as
//   a report on Server. The input's `checked` and `indeterminate` properties are the `dom` primitive's after attach (ADR 04
//   Amendments 2026-10-20 and 2026-10-22): C# renders the initial `checked` once and then sends both in DomOptions,
//   raising CheckedRestores when a request leaves the value different from the box the browser toggled.
// - id: in non-native mode the root keeps its generated id and the labelable control id goes to the input; in native mode
//   the root takes the labelable control id and the input has none (CHK-API-14, CHK-DOM-01/15). The labelable control id
//   is the field's control id, else the consumer's non-empty `id`, else the part's own (CheckableFieldBehavior).
// - Inside a field the box is the field's control (checkbox §8): CheckableFieldBehavior registers it, writes the field's
//   flags, validates through the hidden input and composes the label and description ids.
/// <summary>
/// Represents a control that the user ticks or clears, and that can be shown as mixed: Base UI's <c>Checkbox.Root</c>,
/// rendered as a <c>span</c> with <c>role="checkbox"</c> followed by the hidden inputs that take part in forms, native
/// validation and label activation.
/// </summary>
public sealed class CheckboxRoot : PartBase<CheckboxRootState>
{
    private const string DefaultTag = "span";
    private const string CheckboxType = "checkbox";

    private static readonly StateAttributesMapping<CheckboxRootState> s_mapping = CheckboxStateMapping.Create<CheckboxRootState>(indeterminate: false);
    private static readonly StateAttributesMapping<CheckboxRootState> s_mixedMapping = CheckboxStateMapping.Create<CheckboxRootState>(indeterminate: true);

    private readonly CheckboxRootContext context = new();
    private readonly EventCallback onKeyDown;
    private readonly EventCallback onKeyUp;
    private readonly EventCallback onClick;
    private readonly EventCallback onFocus;
    private readonly Action fieldStateChanged;
    private readonly Action groupChanged;
    private readonly Action<ElementReference> captureInput;
    private readonly TaskCompletionSource inputAttached = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private Controlled<bool>? checkedState;
    private ButtonBehavior? behavior;
    private CheckableFieldBehavior? fieldBehavior;
    private CheckboxGroupContext? group;
    private string? renderedRootId;
    private string? renderedAriaControls;
    private string? registeredChildIdentity;
    private string? registeredChildId;
    private string? registeredDisabledIdentity;
    private bool memberRegistered;
    private DomTwin? domTwin;
    private TwinHandle<DomOptions>? domHandle;
    private DomOptions? sentDomOptions;
    private ElementReference? input;
    private string? controlId;
    private bool? renderedChecked;
    private int checkedRestores;
    private CheckboxRootState? state;
    private RenderFragment? cascade;
    private RenderFragment? cascadedContent;

    /// <summary>Initializes a new instance of the <see cref="CheckboxRoot"/> class.</summary>
    public CheckboxRoot()
    {
        onKeyDown = CreateHandler<KeyboardEventArgs>("onkeydown", e => behavior!.HandleKeyDownAsync(e));
        onKeyUp = CreateHandler<KeyboardEventArgs>("onkeyup", e => behavior!.HandleKeyUpAsync(e));
        onClick = CreateHandler<MouseEventArgs>("onclick", e => behavior!.HandleClickAsync(e));
        onFocus = CreateHandler<FocusEventArgs>("onfocus", _ => fieldBehavior!.HandleFocus());
        fieldStateChanged = OnFieldStateChanged;
        groupChanged = OnGroupChanged;
        captureInput = element => input = element;
    }

    [Inject]
    private IJSRuntime JSRuntime { get; set; } = null!;

    [Inject]
    private ILogger<CheckboxRoot> Logger { get; set; } = null!;

    [Inject]
    private ButtonDevelopmentLog DevelopmentLog { get; set; } = null!;

    [Inject]
    private PartIdGenerator IdGenerator { get; set; } = null!;

    [Inject]
    private DefaultFormScope Formless { get; set; } = null!;

    // CHK-STR-04, CHK-API-06: the identity is the value, an empty string included, else the effective name.
    private string? Identity => Value ?? fieldBehavior!.Name;

    // CHK-STR-04: a member reads and writes the group's value; a parent derives from it when the group lists all values;
    // a detached box and a parent without that list keep their own state (CHK-STA-12, CHK-EDGE-11). Design point C3/G5
    // (settled by the orchestrator 2026-10-23): the group's path runs beside Controlled<bool>, which keeps the box's own pair.
    private bool IsMember => group is not null && !Parent && Identity is not null;

    private bool IsDerivedParent => group is not null && Parent && group.AllValues is not null;

    // CHK-STA-04/07/08: the value the hidden input's `checked` property mirrors.
    private bool IsChecked => IsMember
        ? ContainsOrdinal(group!.Value, Identity!)
        : IsDerivedParent ? CheckboxGroupParent.IsChecked(group!.Value, group.AllValues!) : checkedState!.Value;

    // CHK-STA-08/09: the indeterminate rendering never changes the underlying value; a parent adds the group's mixed state.
    private bool EffectiveIndeterminate => Indeterminate || (IsDerivedParent && CheckboxGroupParent.IsIndeterminate(group!.Value, group.AllValues!));

    // CHK-API-11
    private bool EffectiveDisabled => fieldBehavior!.EffectiveDisabled;

    // CHK-API-08, CHK-DOM-12: a parent box has no name, so it never submits.
    private string? EffectiveName => Parent ? null : fieldBehavior!.Name;

    private string RootId => NativeButton ? fieldBehavior!.ControlId : GeneratedId!;

    private string? InputId => NativeButton ? null : fieldBehavior!.ControlId;

    // CHK-ARIA-06 step 2, CHK-EDGE-07: a non-blank consumer aria-label names the box, even with a label in its scope.
    private bool ConsumerNamesBox => AdditionalAttributes is not null
        && AdditionalAttributes.TryGetValue("aria-label", out object? label)
        && label is string text
        && !string.IsNullOrWhiteSpace(text);

    // CHK-ARIA-06 step 1: the consumer's aria-labelledby wins at the merge.
    private bool ConsumerLabelsBox => AdditionalAttributes is not null && AdditionalAttributes.ContainsKey("aria-labelledby");

    // CHK-ARIA-12: only a parent that derives from the group carries the members' ids.
    private string? AriaControls => IsDerivedParent ? group!.Parent.AriaControls(group.AllValues!) : null;

    [CascadingParameter]
    private CheckboxGroupContext? GroupContext { get; set; }

    [CascadingParameter]
    private FieldRootContext? FieldContext { get; set; }

    [CascadingParameter]
    private FieldItemContext? ItemContext { get; set; }

    [CascadingParameter]
    private IFormScope? FormScope { get; set; }

    [CascadingParameter]
    private EditContext? CascadedEditContext { get; set; }

    // CHK-API-01, CHK-STA-03/05; ADR 05 §2
    /// <summary>Gets or sets a value indicating whether the box is ticked, when the consumer owns the value.</summary>
    /// <value>
    /// <see langword="true"/> when the box is ticked; otherwise, <see langword="false"/>. Supplying it at the first render
    /// makes the value the consumer's for the lifetime of the box: a toggle then only raises
    /// <see cref="OnCheckedChange"/> and <see cref="CheckedChanged"/>.
    /// </value>
    [Parameter]
    public bool Checked { get; set; }

    // ADR 05 §2: the binding half of @bind-Checked.
    /// <summary>Gets or sets the callback that receives the new value after a toggle that was not canceled.</summary>
    /// <value>The callback <c>@bind-Checked</c> supplies, invoked after <see cref="OnCheckedChange"/>.</value>
    [Parameter]
    public EventCallback<bool> CheckedChanged { get; set; }

    // CHK-API-02
    /// <summary>Gets or sets a value indicating whether the box is initially ticked, when the box owns the value.</summary>
    /// <value><see langword="true"/> to start ticked; the default is <see langword="false"/>. Read at the first render only.</value>
    [Parameter]
    public bool DefaultChecked { get; set; }

    // CHK-API-03, CHK-EVT-01..08
    /// <summary>Gets or sets the callback invoked when the user toggles the box, before the value changes.</summary>
    /// <value>
    /// The callback, which receives the next value and the change details: the reason <see cref="EventReason.None"/> and the
    /// click the hidden input received. Calling <see cref="ChangeEventDetails.Cancel"/> keeps the current value.
    /// </value>
    [Parameter]
    public EventCallback<ValueChangeEventArgs<bool>> OnCheckedChange { get; set; }

    // CHK-API-04, CHK-STA-09
    /// <summary>Gets or sets a value indicating whether the box is shown as mixed, neither ticked nor clear.</summary>
    /// <value>
    /// <see langword="true"/> to render <c>aria-checked="mixed"</c>, <c>data-indeterminate</c> and the indicator, and to set
    /// the hidden input's <c>indeterminate</c> property; the default is <see langword="false"/>. It never changes the
    /// value: a toggle still flips it, and the box stays mixed until this is <see langword="false"/> again.
    /// </value>
    [Parameter]
    public bool Indeterminate { get; set; }

    // CHK-API-05, CHK-EDGE-11
    /// <summary>Gets or sets a value indicating whether the box is the parent of the enclosing checkbox group.</summary>
    /// <value>
    /// <see langword="true"/> to render <c>data-parent</c> and leave the hidden input without a name, so the box is never
    /// submitted; the default is <see langword="false"/>. Within a checkbox group that lists all of its values, the box
    /// then shows and sets whether all of the group's boxes are ticked.
    /// </value>
    [Parameter]
    public bool Parent { get; set; }

    // CHK-API-06, CHK-DOM-16
    /// <summary>Gets or sets the value submitted while the box is ticked, which also identifies the box within a group.</summary>
    /// <value>The value, an empty string included; or <see langword="null"/>, the default, to submit the browser's <c>on</c>.</value>
    [Parameter]
    public string? Value { get; set; }

    // CHK-API-07, CHK-DOM-19
    /// <summary>Gets or sets the value submitted while the box is clear.</summary>
    /// <value>The value, or <see langword="null"/>, the default, to submit nothing while clear.</value>
    [Parameter]
    public string? UncheckedValue { get; set; }

    // CHK-API-08
    /// <summary>Gets or sets the name the box's value is submitted under.</summary>
    /// <value>The name placed on the hidden inputs, or <see langword="null"/> for none.</value>
    [Parameter]
    public string? Name { get; set; }

    // CHK-API-09; ADR 08 Amendments 2026-10-22 (the parameter keeps the name `Form`)
    /// <summary>Gets or sets the id of the form that owns the box when it is rendered outside that form.</summary>
    /// <value>The form's id, placed as the <c>form</c> attribute of the hidden inputs; or <see langword="null"/>.</value>
    [Parameter]
    public string? Form { get; set; }

    // CHK-API-10
    /// <summary>Gets or sets a value indicating whether the box must be ticked for its form to submit.</summary>
    /// <value><see langword="true"/> to require it; the default is <see langword="false"/>.</value>
    [Parameter]
    public bool Required { get; set; }

    // CHK-API-11
    /// <summary>Gets or sets a value indicating whether the box ignores user interaction.</summary>
    /// <value>
    /// <see langword="true"/> to ignore clicks and keys, which also withholds the consumer's press and click handlers and
    /// keeps the box out of form submission; otherwise, <see langword="false"/>. The default is <see langword="false"/>.
    /// </value>
    [Parameter]
    public bool Disabled { get; set; }

    // CHK-API-12, CHK-EDGE-06
    /// <summary>Gets or sets a value indicating whether the user cannot toggle the box.</summary>
    /// <value>
    /// <see langword="true"/> to keep the value while the box stays focusable, submits with its form and lets Enter submit
    /// the form; the default is <see langword="false"/>.
    /// </value>
    [Parameter]
    public bool ReadOnly { get; set; }

    // CHK-API-15, UBT-API-03
    /// <summary>Gets or sets a value indicating whether the rendered element is a native <c>button</c>.</summary>
    /// <value>
    /// <see langword="true"/> when <see cref="PartBase{TState}.Render"/> supplies a <c>button</c>, which then also takes the
    /// consumer's <c>id</c>; <see langword="false"/>, the default, for the <c>span</c>, which gets a <c>tabindex</c> and
    /// clicks from Space.
    /// </value>
    [Parameter]
    public bool NativeButton { get; set; }

    // ADR 05 Amendments 2026-10-22: EditContext participation (issue 76).
    /// <summary>Gets or sets the expression that identifies the bound value in a cascaded <see cref="EditContext"/>.</summary>
    /// <value>
    /// The expression <c>@bind-Checked</c> supplies, or <see langword="null"/>. Inside an <see cref="EditContext"/> the box
    /// then notifies the context of every change, takes the identifier's field name when it has no
    /// <see cref="Name"/>, and is invalid while the context holds messages for it. Without an <see cref="EditContext"/>
    /// it does nothing.
    /// </value>
    [Parameter]
    public Expression<Func<bool>>? CheckedExpression { get; set; }

    /// <inheritdoc />
    protected override string DefaultTagName => DefaultTag;

    /// <inheritdoc />
    protected override CheckboxRootState State => state!;

    /// <inheritdoc />
    protected override StateAttributesMapping<CheckboxRootState>? StateAttributesMapping => state!.Indeterminate ? s_mixedMapping : s_mapping;

    // CHK-DOM-01: the root always has an id (the generated one, or the control id in native mode).
    /// <inheritdoc />
    protected override bool UsesGeneratedId => true;

    // For the tests: the underlying value of a box shown as mixed.
    /// <summary>Gets a value indicating whether the box's hidden input is checked, which a mixed box does not show.</summary>
    /// <value>The underlying value.</value>
    internal bool InputChecked => IsChecked;

    // For the fixtures (tests/Blazix.Primitives.Fixtures.Client): completes once both twins are attached, so a browser test
    // can wait for the modules' listeners before it clicks.
    /// <summary>Gets a task that completes when the box's behaviors are attached to its element and its hidden input.</summary>
    /// <value>A task that completes after the first interactive render has attached both.</value>
    internal Task TwinAttached => Task.WhenAll(behavior!.TwinAttached, inputAttached.Task, fieldBehavior!.TwinAttached);

    /// <inheritdoc />
    public override Task SetParametersAsync(ParameterView parameters)
    {
        // CHK-STA-03/06, CHK-EDGE-02/03: ownership is fixed at the first parameter set (ADR 05 Amendments 2026-10-18).
        checkedState ??= new Controlled<bool>("Checkbox", "checked", Logger);
        checkedState.SetParameters(parameters, nameof(Checked), nameof(DefaultChecked));
        return base.SetParametersAsync(parameters);
    }

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        behavior = new ButtonBehavior(JSRuntime, Logger, DevelopmentLog, IdGenerator, StateHasChanged, DispatchExceptionAsync, onKeyDown, onKeyUp, onClick)
        {
            HiddenInput = true,
            SuppressEnter = true,
            ToggleRequest = RequestToggleAsync,
            RestoreRequested = RestoreInput,
        };
        base.OnInitialized();

        // CHK-DOM-15: the labelable control id, drawn after the root's own, in render order (ADR 02 decision 5).
        controlId = IdGenerator.Next();
        fieldBehavior = new CheckableFieldBehavior(controlId, Formless, JSRuntime, fieldStateChanged, InvokeAsync, DispatchExceptionAsync);
        fieldBehavior.Initialize(FieldContext, ItemContext);
        group = GroupContext;
        if (group is not null)
        {
            group.AddListener(groupChanged);
            fieldBehavior.GroupRefresh = group.RefreshMembersAsync;
        }
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        // CHK-FRM-20: inside a group the group is the field's control, a member's hidden input is one of the field's
        // validation inputs, and the group's ValueExpression is the EditContext's (ADR05-5); CHK-DOM-15, CKG-ARIA-07: a box
        // that shares its labelable scope with the group claims no control id and takes no label from it; CHK-API-11: the
        // group's disabled state is OR'd in.
        fieldBehavior!.GroupDisabled = group?.Disabled ?? false;
        fieldBehavior.SetParameters(
            new CheckableFieldParameters(
                Name,
                Disabled,
                IsChecked,
                BlurValue(),
                OwnsValue: group is null,
                ClaimsControlId: !fieldBehavior.SharesScope(group?.Scope),
                RegistersInput: group is not null && !Parent,
                EnterSubmit: true,
                LabelFallback: !NativeButton && !ConsumerNamesBox && !ConsumerLabelsBox,
                group is null ? CheckedExpression : null,
                CascadedEditContext,
                AdditionalAttributes,
                Enabled),
            FormScope);
        behavior!.Disabled = EffectiveDisabled;
        behavior.Native = NativeButton;
        behavior.ReadOnly = ReadOnly;
        behavior.SetConsumerAttributes(AdditionalAttributes);
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
        await behavior!.AfterRenderAsync(Enabled, Element, RootId, composite: false);
        await AttachInputAsync();
        await fieldBehavior!.AfterRenderAsync(Element, RootId, GeneratedId!, input, InputId);
        RegisterChildId();
        RegisterMember();
        group?.BoxRendered();
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            behavior?.Dispose();
            fieldBehavior?.Dispose();
            if (group is not null)
            {
                group.RemoveListener(groupChanged);
                if (registeredChildIdentity is not null)
                {
                    group.Parent.UnregisterChild(registeredChildIdentity, registeredChildId!);
                }

                if (registeredDisabledIdentity is not null)
                {
                    group.Parent.RemoveDisabled(registeredDisabledIdentity);
                }

                if (memberRegistered)
                {
                    group.UnregisterMember(fieldBehavior!);
                }
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

        // CHK-ARIA-01..04: the writer's role is replaced, and the consumer's role still wins at the merge.
        attributes.Set("role", "checkbox");
        attributes.Set("aria-checked", EffectiveIndeterminate ? "mixed" : IsChecked ? "true" : "false");
        if (ReadOnly)
        {
            attributes.Set("aria-readonly", "true");
        }

        if (Required)
        {
            attributes.Set("aria-required", "true");
        }

        // CHK-DOM-07: a part attribute, not a state field, and never on the indicator.
        if (Parent)
        {
            attributes.Set("data-parent", string.Empty);
        }

        if (NativeButton)
        {
            // CHK-DOM-01: the labelable control id.
            attributes.Set("id", RootId);
        }

        if (fieldBehavior!.InField)
        {
            fieldBehavior.Rendered();
            attributes.Set("onfocus", onFocus);
        }

        // CHK-ARIA-06: the consumer's aria-labelledby replaces this at the merge; a non-blank aria-label leaves it out;
        // then the label of the box's labelable scope, then the input's native label (CHK-ARIA-07, the form module).
        if (!ConsumerNamesBox && (fieldBehavior.LabelId ?? fieldBehavior.FallbackLabelId) is { } labelId)
        {
            attributes.Set("aria-labelledby", labelId);
        }
    }

    // ADR 02 Amendments 2026-10-21 (FinishAttributes); part-owned by the document: the root's id (CHK-DOM-01: in non-native
    // mode the consumer's `id` is the hidden input's, CHK-API-14; in native mode an empty one counts as absent; a
    // substitute element's own id wins, RND-DOM-32), the parent's group-derived aria-controls (CHK-ARIA-12, CHK-DOM-09),
    // the composed aria-describedby (CHK-ARIA-10) and aria-invalid while the field is invalid (CHK-ARIA-11). The pipeline
    // calls this after a substitute element's attributes merged, so the id kept here is the one on the element: a member
    // registers it for the parent's aria-controls (design point G3, settled by the orchestrator 2026-10-23).
    /// <inheritdoc />
    protected override void FinishAttributes(AttributeList attributes)
    {
        string? merged = attributes.TryGetValue("id", out AttributeValue mergedId) && mergedId.TryGetValue(out string idText) ? idText : null;
        string? consumerId = AdditionalAttributes is not null && AdditionalAttributes.TryGetValue("id", out object? supplied) ? supplied as string : null;
        string partId = NativeButton ? RootId : GeneratedId!;
        if (merged is null || string.Equals(merged, consumerId, StringComparison.Ordinal) || string.Equals(merged, partId, StringComparison.Ordinal))
        {
            attributes.Set("id", RootId);
            renderedRootId = RootId;
        }
        else
        {
            renderedRootId = merged;
        }

        renderedAriaControls = AriaControls;
        if (renderedAriaControls is not null)
        {
            attributes.Set("aria-controls", renderedAriaControls);
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

        if (fieldBehavior.AriaInvalid)
        {
            attributes.Set("aria-invalid", "true");
        }
    }

    /// <inheritdoc />
    protected override bool ForwardsConsumerHandler(string eventName)
    {
        return behavior!.ForwardsConsumerHandler(eventName);
    }

    // CHK-STR-01, CHK-DOM-11..19; checkbox/root/CheckboxRoot.tsx:195-263,397-405.
    /// <inheritdoc />
    protected override void BuildAfterElement(RenderTreeBuilder builder)
    {
        bool isChecked = IsChecked;
        string? name = EffectiveName;
        // CHK-DOM-19, CHK-API-07: never inside a group.
        if (!isChecked && group is null && !string.IsNullOrEmpty(name) && UncheckedValue is not null)
        {
            builder.OpenElement(0, "input");
            builder.AddAttribute(1, "type", "hidden");
            builder.AddAttribute(2, "form", Form);
            builder.AddAttribute(3, "name", name);
            builder.AddAttribute(4, "value", UncheckedValue);
            builder.AddAttribute(5, "disabled", EffectiveDisabled);
            builder.CloseElement();
        }

        // The handover at attach (ADR 04 Amendments 2026-10-18 and 2026-10-22): `checked` is rendered from the first render's
        // value and then frozen, so Blazor never writes the property the `dom` primitive owns.
        renderedChecked ??= isChecked;
        builder.OpenElement(10, "input");
        builder.AddAttribute(11, "type", CheckboxType);
        builder.AddAttribute(12, "aria-hidden", "true");
        builder.AddAttribute(13, "tabindex", "-1");
        builder.AddAttribute(14, "checked", renderedChecked.Value);
        builder.AddAttribute(15, "disabled", EffectiveDisabled);
        builder.AddAttribute(16, "required", Required);
        builder.AddAttribute(17, "name", name);
        builder.AddAttribute(18, "form", Form);
        builder.AddAttribute(19, "id", InputId);
        // CHK-DOM-16: inside a group the attribute is the value while checked and empty while clear.
        builder.AddAttribute(20, "value", Value is null || group is null ? Value : isChecked ? Value : string.Empty);
        builder.AddAttribute(21, "style", string.IsNullOrEmpty(name) ? VisuallyHidden.Style : VisuallyHidden.InputStyle);

        // CHK-DOM-18: the field's description ids without the consumer's, and its invalid state.
        builder.AddAttribute(22, "aria-describedby", fieldBehavior!.InField ? fieldBehavior.DescribedBy(null) : null);
        builder.AddAttribute(23, "aria-invalid", fieldBehavior.AriaInvalid ? "true" : null);
        builder.AddElementReferenceCapture(24, captureInput);
        builder.CloseElement();
    }

    // CHK-STA-04/05/09, CHK-EVT-01..07: the request of one toggle of the hidden input. `reported` is the input's new value
    // from the module's report; on WebAssembly the toggle is decided inside the click and the next value is the opposite of
    // the box's, which the `dom` primitive keeps on the input. The result says whether the box now holds what the input
    // shows; when it does not, the module reverts the input (WebAssembly) or the part restores it (RestoreInput).
    private async Task<bool> RequestToggleAsync(bool? reported, EventSnapshot snapshot)
    {
        bool next = reported ?? !IsChecked;
        if (ReadOnly || EffectiveDisabled)
        {
            return IsChecked == next;
        }

        ChangeEventDetails details = new(EventReason.None, snapshot);
        if (IsMember || IsDerivedParent)
        {
            // CHK-EVT-09, CKG-EVT-01..05: the box's own callback, then the group's with the same details, then the commits.
            // The box commits nothing of its own: the group's value is its state (CHK-STA-07/08).
            if (await checkedState!.NotifyAsync(next, details, OnCheckedChange))
            {
                await RequestGroupAsync(next, details);
            }
        }
        else
        {
            await checkedState!.RequestAsync(next, details, OnCheckedChange, CheckedChanged);
        }

        if (UpdateState())
        {
            StateHasChanged();
        }

        return IsChecked == next;
    }

    private async Task RequestGroupAsync(bool next, ChangeEventDetails details)
    {
        CheckboxGroupContext current = group!;
        if (IsMember)
        {
            // CKG-STA-03, CKG-STA-08: a committed member toggle also replaces the parent's snapshot.
            IReadOnlyList<string> proposed = CheckboxGroupParent.ProposeMemberToggle(current.Value, Identity!, next);
            if (await current.Request(proposed, details) && current.AllValues is not null)
            {
                current.Parent.CommitMemberToggle(proposed);
            }

            return;
        }

        // CKG-STA-07: the parent's proposal; the cycle advances only when it was not canceled.
        ParentProposal proposal = current.Parent.ProposeParentToggle(current.Value, current.AllValues!);
        if (await current.Request(proposal.Value, details))
        {
            current.Parent.CommitParentToggle(proposal);
        }
    }

    // CKG-ARIA-06, CHK-ARIA-12: a member registers the id on its root after the render, and withdraws it on change.
    private void RegisterChildId()
    {
        string? identity = group is not null && !Parent && Enabled ? Identity : null;
        string? id = identity is null ? null : renderedRootId;
        if (string.Equals(identity, registeredChildIdentity, StringComparison.Ordinal) && string.Equals(id, registeredChildId, StringComparison.Ordinal))
        {
            return;
        }

        if (registeredChildIdentity is not null)
        {
            group!.Parent.UnregisterChild(registeredChildIdentity, registeredChildId!);
        }

        registeredChildIdentity = id is null ? null : identity;
        registeredChildId = id;
        if (registeredChildIdentity is not null)
        {
            group!.Parent.RegisterChild(registeredChildIdentity, id!);
        }
    }

    // CKG-FRM-03, FFI-FRM-24: inside a field a member's input joins the group's refresh and its projection, by identity.
    private void RegisterMember()
    {
        if (group is null)
        {
            return;
        }

        if (!Parent && Enabled && fieldBehavior!.InField)
        {
            memberRegistered = true;
            group.RegisterMember(fieldBehavior, Identity);
        }
        else if (memberRegistered)
        {
            memberRegistered = false;
            group.UnregisterMember(fieldBehavior!);
        }
    }

    // A change of the group's value, list, disabled state or members' ids.
    private void OnGroupChanged()
    {
        fieldBehavior!.GroupDisabled = group!.Disabled;
        behavior!.Disabled = EffectiveDisabled;
        if (UpdateState() || !string.Equals(AriaControls, renderedAriaControls, StringComparison.Ordinal))
        {
            StateHasChanged();
        }
    }

    private object? BlurValue() => group is null ? CheckableFieldBehavior.Box(IsChecked) : group.Value;

    private static bool ContainsOrdinal(IReadOnlyList<string> values, string identity)
    {
        for (int i = 0; i < values.Count; i++)
        {
            if (string.Equals(values[i], identity, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    // A change of the field's state that the box shows (its flags, label, descriptions, validity or disabled state).
    private void OnFieldStateChanged()
    {
        behavior!.Disabled = EffectiveDisabled;
        UpdateState();
        StateHasChanged();
    }

    // CHK-EVT-04, CHK-STA-05 (restore clause): the ADR 01 row of 2026-10-22.
    private void RestoreInput()
    {
        checkedRestores++;
        StateHasChanged();
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

        // CHK-DOM-12/13: the underlying value and the effective indeterminate state; the primitive re-asserts
        // `indeterminate` inside every native click, which clears it.
        bool isChecked = IsChecked;
        bool indeterminate = EffectiveIndeterminate;
        if (sentDomOptions is { } sent && sent.Checked == isChecked && sent.Indeterminate == indeterminate && sent.CheckedRestores == checkedRestores)
        {
            return;
        }

        DomOptions options = new() { Indeterminate = indeterminate, Checked = isChecked, CheckedRestores = checkedRestores };
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
    // so they render in the same batch as the root (CHK-DOM-08).
    private bool UpdateState()
    {
        bool isChecked = IsChecked;
        bool indeterminate = EffectiveIndeterminate;
        fieldBehavior!.SetValue(isChecked, BlurValue());
        SyncDisabledEntry();
        FieldStateSnapshot snapshot = fieldBehavior.Snapshot;
        if (state is { } current
            && current.Checked == isChecked
            && current.Disabled == snapshot.Disabled
            && current.ReadOnly == ReadOnly
            && current.Required == Required
            && current.Indeterminate == indeterminate
            && current.Touched == snapshot.Touched
            && current.Dirty == snapshot.Dirty
            && current.Valid == snapshot.Valid
            && current.Filled == snapshot.Filled
            && current.Focused == snapshot.Focused)
        {
            return false;
        }

        state = new CheckboxRootState(isChecked, snapshot.Disabled, ReadOnly, Required, indeterminate, snapshot.Touched, snapshot.Dirty, snapshot.Valid, snapshot.Filled, snapshot.Focused);
        context.SetState(state);
        return true;
    }

    // CKG-STA-09 (design point G4, settled by the orchestrator 2026-10-23): a member's disabled state is the parent's to read at once, so the
    // entry is updated with the box's state rather than after paint.
    private void SyncDisabledEntry()
    {
        string? identity = group is not null && !Parent ? Identity : null;
        if (registeredDisabledIdentity is not null && !string.Equals(registeredDisabledIdentity, identity, StringComparison.Ordinal))
        {
            group!.Parent.RemoveDisabled(registeredDisabledIdentity);
        }

        registeredDisabledIdentity = identity;
        if (identity is not null)
        {
            group!.Parent.SetDisabled(identity, EffectiveDisabled);
        }
    }
}

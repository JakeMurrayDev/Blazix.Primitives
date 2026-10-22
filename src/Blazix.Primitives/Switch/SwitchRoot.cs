using Blazix.Primitives.Interop;
using Blazix.Primitives.Rendering;
using Blazix.Primitives.Utilities;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace Blazix.Primitives;

// SWT-STA-04; switch/root/SwitchRoot.tsx:207-216 (the field's state with four switch fields). ADR 08 §3: the state record is
// sealed, partial and [PartState]; one record per part (Progress precedent).
/// <summary>Represents the state of a switch, which the part exposes as data attributes and passes to its class and style functions.</summary>
/// <param name="Checked">A value indicating whether the switch is on; projected as <c>data-checked</c> or <c>data-unchecked</c>.</param>
/// <param name="Disabled">A value indicating whether the switch ignores user interaction; projected as <c>data-disabled</c>.</param>
/// <param name="ReadOnly">A value indicating whether the user cannot toggle the switch; projected as <c>data-readonly</c>.</param>
/// <param name="Required">A value indicating whether the switch must be on to submit its form; projected as <c>data-required</c>.</param>
/// <param name="Touched">A value indicating whether the switch's field has lost focus once; projected as <c>data-touched</c>.</param>
/// <param name="Dirty">A value indicating whether the value differs from the field's initial one; projected as <c>data-dirty</c>.</param>
/// <param name="Valid">
/// The field's validity: <see langword="true"/> projects <c>data-valid</c>, <see langword="false"/> projects
/// <c>data-invalid</c>, and <see langword="null"/>, before validation, projects neither.
/// </param>
/// <param name="Filled">A value indicating whether the field has a value, which for a switch is whether it is on; projected as <c>data-filled</c>.</param>
/// <param name="Focused">A value indicating whether the switch has focus within its field; projected as <c>data-focused</c>.</param>
[PartState]
public sealed partial record SwitchRootState(bool Checked, bool Disabled, bool ReadOnly, bool Required, bool Touched, bool Dirty, bool? Valid, bool Filled, bool Focused);

// SWT-DOM-02..05; switch/root/SwitchRootDataAttributes.ts, in its order.
/// <summary>Provides the names of the data attributes a switch renders.</summary>
public static class SwitchRootDataAttributes
{
    /// <summary>Represents the attribute present, with an empty value, while the switch is on: <c>data-checked</c>.</summary>
    public const string Checked = "data-checked";

    /// <summary>Represents the attribute present, with an empty value, while the switch is off: <c>data-unchecked</c>.</summary>
    public const string Unchecked = "data-unchecked";

    /// <summary>Represents the attribute present, with an empty value, while the switch is disabled: <c>data-disabled</c>.</summary>
    public const string Disabled = "data-disabled";

    /// <summary>Represents the attribute present, with an empty value, while the switch is read-only: <c>data-readonly</c>.</summary>
    public const string Readonly = "data-readonly";

    /// <summary>Represents the attribute present, with an empty value, while the switch is required: <c>data-required</c>.</summary>
    public const string Required = "data-required";

    /// <summary>Represents the attribute present, with an empty value, while the switch's field is valid: <c>data-valid</c>.</summary>
    public const string Valid = "data-valid";

    /// <summary>Represents the attribute present, with an empty value, while the switch's field is invalid: <c>data-invalid</c>.</summary>
    public const string Invalid = "data-invalid";

    /// <summary>Represents the attribute present, with an empty value, once the switch's field has lost focus: <c>data-touched</c>.</summary>
    public const string Touched = "data-touched";

    /// <summary>Represents the attribute present, with an empty value, while the value differs from the field's initial one: <c>data-dirty</c>.</summary>
    public const string Dirty = "data-dirty";

    /// <summary>Represents the attribute present, with an empty value, while the switch is on within a field: <c>data-filled</c>.</summary>
    public const string Filled = "data-filled";

    /// <summary>Represents the attribute present, with an empty value, while the switch has focus within a field: <c>data-focused</c>.</summary>
    public const string Focused = "data-focused";
}

// Base UI's Switch.Root (switch/root/SwitchRoot.tsx); the contract is docs/components/switch.md.
// - The element is a button-like span (ButtonBehavior, UBT; non-native by default, UBT-API-03) whose role the part replaces
//   with `switch` (the consumer's role still wins, SWT-DOM-14).
// - After it the part renders its form-participation inputs (BuildAfterElement, ADR 02 Amendments 2026-10-22): the
//   unchecked-value `<input type="hidden">` and the visually hidden `<input type="checkbox">` (SWT-STR-01, SWT-DOM-07..13).
// - Every toggle is a click on that input: the `button` module forwards the root's click to it (the hidden-input bridge,
//   ADR 12 Amendments 2026-10-22 item 4) and hands the toggle to RequestToggleAsync, inside the click on WebAssembly or as
//   a report on Server. The input's `checked` property is the `dom` primitive's after attach (ADR 04 Amendments
//   2026-10-22): C# renders the initial value once and then sends it in DomOptions, raising CheckedRestores when a request
//   leaves the switch's value different from the box the browser toggled.
// - id: in non-native mode the root keeps its generated id and the consumer's `id` goes to the input; in native mode the
//   root takes the consumer's id, else the control id, and the input has none (SWT-API-11, SWT-DOM-01/10).
/// <summary>
/// Represents a control that turns a setting on or off: Base UI's <c>Switch.Root</c>, rendered as a <c>span</c> with
/// <c>role="switch"</c> followed by the hidden inputs that take part in forms, native validation and label activation.
/// </summary>
public sealed class SwitchRoot : PartBase<SwitchRootState>
{
    private const string DefaultTag = "span";
    private const string CheckboxType = "checkbox";

    private static readonly StateAttributesMapping<SwitchRootState> s_mapping = SwitchStateMapping.Create<SwitchRootState>();

    private readonly SwitchRootContext context = new();
    private readonly EventCallback onKeyDown;
    private readonly EventCallback onKeyUp;
    private readonly EventCallback onClick;
    private readonly Action<ElementReference> captureInput;
    private readonly TaskCompletionSource inputAttached = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private Controlled<bool>? checkedState;
    private ButtonBehavior? behavior;
    private DomTwin? domTwin;
    private TwinHandle<DomOptions>? domHandle;
    private DomOptions? sentDomOptions;
    private ElementReference? input;
    private string? controlId;
    private bool? renderedChecked;
    private int checkedRestores;
    private SwitchRootState? state;
    private RenderFragment? cascade;
    private RenderFragment? cascadedContent;

    /// <summary>Initializes a new instance of the <see cref="SwitchRoot"/> class.</summary>
    public SwitchRoot()
    {
        onKeyDown = CreateHandler<KeyboardEventArgs>("onkeydown", e => behavior!.HandleKeyDownAsync(e));
        onKeyUp = CreateHandler<KeyboardEventArgs>("onkeyup", e => behavior!.HandleKeyUpAsync(e));
        onClick = CreateHandler<MouseEventArgs>("onclick", e => behavior!.HandleClickAsync(e));
        captureInput = element => input = element;
    }

    [Inject]
    private IJSRuntime JSRuntime { get; set; } = null!;

    [Inject]
    private ILogger<SwitchRoot> Logger { get; set; } = null!;

    [Inject]
    private ButtonDevelopmentLog DevelopmentLog { get; set; } = null!;

    [Inject]
    private PartIdGenerator IdGenerator { get; set; } = null!;

    private bool IsChecked => checkedState!.Value;

    private bool EffectiveDisabled => Disabled;

    private string? EffectiveName => Name;

    private string? ConsumerId => AdditionalAttributes is not null && AdditionalAttributes.TryGetValue("id", out object? supplied) ? supplied as string : null;

    private string RootId => NativeButton ? ConsumerId ?? controlId! : GeneratedId!;

    private string? InputId => NativeButton ? null : ConsumerId ?? controlId;

    // SWT-API-01; ADR 05 §2
    /// <summary>Gets or sets a value indicating whether the switch is on, when the consumer owns the value.</summary>
    /// <value>
    /// <see langword="true"/> when the switch is on; otherwise, <see langword="false"/>. Supplying it at the first render
    /// makes the value the consumer's for the lifetime of the switch: a toggle then only raises
    /// <see cref="OnCheckedChange"/> and <see cref="CheckedChanged"/>.
    /// </value>
    [Parameter]
    public bool Checked { get; set; }

    // ADR 05 §2: the binding half of @bind-Checked.
    /// <summary>Gets or sets the callback that receives the new value after a toggle that was not canceled.</summary>
    /// <value>The callback <c>@bind-Checked</c> supplies, invoked after <see cref="OnCheckedChange"/>.</value>
    [Parameter]
    public EventCallback<bool> CheckedChanged { get; set; }

    // SWT-API-02
    /// <summary>Gets or sets a value indicating whether the switch is initially on, when the switch owns the value.</summary>
    /// <value><see langword="true"/> to start on; the default is <see langword="false"/>. Read at the first render only.</value>
    [Parameter]
    public bool DefaultChecked { get; set; }

    // SWT-API-03, SWT-EVT-01..08
    /// <summary>Gets or sets the callback invoked when the user toggles the switch, before the value changes.</summary>
    /// <value>
    /// The callback, which receives the next value and the change details: the reason <see cref="EventReason.None"/> and the
    /// click the hidden input received. Calling <see cref="ChangeEventDetails.Cancel"/> keeps the current value.
    /// </value>
    [Parameter]
    public EventCallback<ValueChangeEventArgs<bool>> OnCheckedChange { get; set; }

    // SWT-API-04
    /// <summary>Gets or sets a value indicating whether the switch ignores user interaction.</summary>
    /// <value>
    /// <see langword="true"/> to ignore clicks and keys, which also withholds the consumer's press and click handlers and
    /// keeps the switch out of form submission; otherwise, <see langword="false"/>. The default is <see langword="false"/>.
    /// </value>
    [Parameter]
    public bool Disabled { get; set; }

    // SWT-API-05, SWT-EDGE-06
    /// <summary>Gets or sets a value indicating whether the user cannot toggle the switch.</summary>
    /// <value>
    /// <see langword="true"/> to keep the value while the switch stays focusable and submits with its form; the default is
    /// <see langword="false"/>.
    /// </value>
    [Parameter]
    public bool ReadOnly { get; set; }

    // SWT-API-06
    /// <summary>Gets or sets a value indicating whether the switch must be on for its form to submit.</summary>
    /// <value><see langword="true"/> to require it; the default is <see langword="false"/>.</value>
    [Parameter]
    public bool Required { get; set; }

    // SWT-API-07
    /// <summary>Gets or sets the name the switch's value is submitted under.</summary>
    /// <value>The name placed on the hidden inputs, or <see langword="null"/> for none.</value>
    [Parameter]
    public string? Name { get; set; }

    // SWT-API-10; ADR 08 Amendments 2026-10-22 (the parameter keeps the name `Form`)
    /// <summary>Gets or sets the id of the form that owns the switch when it is rendered outside that form.</summary>
    /// <value>The form's id, placed as the <c>form</c> attribute of the hidden inputs; or <see langword="null"/>.</value>
    [Parameter]
    public string? Form { get; set; }

    // SWT-API-08, SWT-DOM-11
    /// <summary>Gets or sets the value submitted while the switch is on.</summary>
    /// <value>The value, or <see langword="null"/>, the default, to submit the browser's <c>on</c>.</value>
    [Parameter]
    public string? Value { get; set; }

    // SWT-API-09, SWT-DOM-13
    /// <summary>Gets or sets the value submitted while the switch is off.</summary>
    /// <value>The value, or <see langword="null"/>, the default, to submit nothing while off.</value>
    [Parameter]
    public string? UncheckedValue { get; set; }

    // SWT-API-13, UBT-API-03
    /// <summary>Gets or sets a value indicating whether the rendered element is a native <c>button</c>.</summary>
    /// <value>
    /// <see langword="true"/> when <see cref="PartBase{TState}.Render"/> supplies a <c>button</c>, which then also takes the
    /// consumer's <c>id</c>; <see langword="false"/>, the default, for the <c>span</c>, which gets a <c>tabindex</c> and
    /// clicks from Enter and Space.
    /// </value>
    [Parameter]
    public bool NativeButton { get; set; }

    /// <inheritdoc />
    protected override string DefaultTagName => DefaultTag;

    /// <inheritdoc />
    protected override SwitchRootState State => state!;

    /// <inheritdoc />
    protected override StateAttributesMapping<SwitchRootState>? StateAttributesMapping => s_mapping;

    // SWT-DOM-01: the root always has an id (the generated one, or the control id in native mode).
    /// <inheritdoc />
    protected override bool UsesGeneratedId => true;

    // For the fixtures (tests/Blazix.Primitives.Fixtures.Client): completes once both twins are attached, so a browser test
    // can wait for the modules' listeners before it clicks.
    /// <summary>Gets a task that completes when the switch's behaviors are attached to its element and its hidden input.</summary>
    /// <value>A task that completes after the first interactive render has attached both.</value>
    internal Task TwinAttached => Task.WhenAll(behavior!.TwinAttached, inputAttached.Task);

    /// <inheritdoc />
    public override Task SetParametersAsync(ParameterView parameters)
    {
        // SWT-STA-01/05, SWT-EDGE-02/03: ownership is fixed at the first parameter set (ADR 05 Amendments 2026-10-18).
        checkedState ??= new Controlled<bool>("Switch", "checked", Logger);
        checkedState.SetParameters(parameters, nameof(Checked), nameof(DefaultChecked));
        return base.SetParametersAsync(parameters);
    }

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        behavior = new ButtonBehavior(JSRuntime, Logger, DevelopmentLog, IdGenerator, StateHasChanged, DispatchExceptionAsync, onKeyDown, onKeyUp, onClick)
        {
            HiddenInput = true,
            ToggleRequest = RequestToggleAsync,
            RestoreRequested = RestoreInput,
        };
        base.OnInitialized();

        // SWT-DOM-10: the labelable control id, drawn after the root's own, in render order (ADR 02 decision 5).
        controlId = IdGenerator.Next();
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
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
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            behavior?.Dispose();
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

        // SWT-ARIA-01..04: the writer's role is replaced, and the consumer's role still wins at the merge.
        attributes.Set("role", "switch");
        attributes.Set("aria-checked", IsChecked ? "true" : "false");
        if (ReadOnly)
        {
            attributes.Set("aria-readonly", "true");
        }

        if (Required)
        {
            attributes.Set("aria-required", "true");
        }

        if (NativeButton)
        {
            // SWT-DOM-01: the labelable control id; the consumer's id replaces it at the merge.
            attributes.Set("id", controlId!);
        }
    }

    // ADR 02 Amendments 2026-10-21 (FinishAttributes); SWT-DOM-01 names the root's id as part-owned: in non-native mode the
    // consumer's `id` is the hidden input's (SWT-API-11), so the root keeps its generated id.
    /// <inheritdoc />
    protected override void FinishAttributes(AttributeList attributes)
    {
        if (!NativeButton)
        {
            attributes.Set("id", GeneratedId!);
        }
    }

    /// <inheritdoc />
    protected override bool ForwardsConsumerHandler(string eventName)
    {
        return behavior!.ForwardsConsumerHandler(eventName);
    }

    // SWT-STR-01, SWT-DOM-07..13; switch/root/SwitchRoot.tsx:158-205,230-238.
    /// <inheritdoc />
    protected override void BuildAfterElement(RenderTreeBuilder builder)
    {
        bool isChecked = IsChecked;
        string? name = EffectiveName;
        if (!isChecked && !string.IsNullOrEmpty(name) && UncheckedValue is not null)
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
        builder.AddAttribute(20, "value", Value);
        builder.AddAttribute(21, "style", string.IsNullOrEmpty(name) ? VisuallyHidden.Style : VisuallyHidden.InputStyle);
        builder.AddElementReferenceCapture(22, captureInput);
        builder.CloseElement();
    }

    // SWT-STA-02/03, SWT-EVT-01..07: the request of one toggle of the hidden input. `reported` is the input's new value from
    // the module's report; on WebAssembly the toggle is decided inside the click and the next value is the opposite of the
    // switch's, which the `dom` primitive keeps on the input. The result says whether the switch now shows what the box
    // shows; when it does not, the module reverts the box (WebAssembly) or the part restores it (RestoreInput).
    private async Task<bool> RequestToggleAsync(bool? reported, EventSnapshot snapshot)
    {
        bool next = reported ?? !IsChecked;
        if (ReadOnly || EffectiveDisabled)
        {
            return IsChecked == next;
        }

        ChangeEventDetails details = new(EventReason.None, snapshot);
        await checkedState!.RequestAsync(next, details, OnCheckedChange, CheckedChanged);
        if (UpdateState())
        {
            StateHasChanged();
        }

        return IsChecked == next;
    }

    // SWT-EVT-04, SWT-STA-03 (restore clause): the ADR 01 row of 2026-10-22.
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

    // Returns whether the state changed; a new record is made only then (ADR 16 rule 7), and the thumbs are told first, so
    // they render in the same batch as the root (SWT-DOM-06).
    private bool UpdateState()
    {
        bool isChecked = IsChecked;
        bool disabled = EffectiveDisabled;
        if (state is { } current
            && current.Checked == isChecked
            && current.Disabled == disabled
            && current.ReadOnly == ReadOnly
            && current.Required == Required)
        {
            return false;
        }

        state = new SwitchRootState(isChecked, disabled, ReadOnly, Required, Touched: false, Dirty: false, Valid: null, Filled: false, Focused: false);
        context.SetState(state);
        return true;
    }
}

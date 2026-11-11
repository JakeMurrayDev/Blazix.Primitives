using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace Blazix.Primitives;

// CMB §3.2, STA-01 (combobox/label/ComboboxLabel.tsx): the field state.
/// <summary>Represents the state of a combobox label, which the part exposes as data attributes and passes to its class and style functions.</summary>
/// <param name="Disabled">A value indicating whether the field is disabled; projected as <c>data-disabled</c>.</param>
/// <param name="Touched">A value indicating whether the field was touched; projected as <c>data-touched</c>.</param>
/// <param name="Dirty">A value indicating whether the value differs from the initial one; projected as <c>data-dirty</c>.</param>
/// <param name="Valid">The field's validity, or <see langword="null"/> before validation; projected as <c>data-valid</c> or <c>data-invalid</c>.</param>
/// <param name="Filled">A value indicating whether a value is present; projected as <c>data-filled</c>.</param>
/// <param name="Focused">A value indicating whether the control is focused; projected as <c>data-focused</c>.</param>
[PartState]
public sealed partial record ComboboxLabelState(
    bool Disabled,
    bool Touched,
    bool Dirty,
    bool? Valid,
    bool Filled,
    bool Focused);

// Base UI's Combobox.Label (combobox/label/ComboboxLabel.tsx; CMB §3.2). Over ComboboxLabelBehavior; the state is the field's
// (CMB-STA-01), projected with the field validity mapping (CMB-DOM-17).
/// <summary>Represents the label of a combobox's trigger, Base UI's <c>Combobox.Label</c>, rendered as a <c>div</c>.</summary>
/// <remarks>
/// The label's id is always the root's id followed by <c>-label</c>. It labels the trigger; when the input outside the popup
/// is the form control, use a native <c>label</c> or a <see cref="FieldLabel"/> instead.
/// </remarks>
public sealed class ComboboxLabel : PartBase<ComboboxLabelState>
{
    private const string MouseDownEvent = "onmousedown";

    private const string ClickEvent = "onclick";

    private const string PointerDownEvent = "onpointerdown";

    private static readonly StateAttributesMapping<ComboboxLabelState> s_mapping = FieldValidityMapping.Create<ComboboxLabelState>();

    private readonly EventCallback onMouseDown;

    private readonly EventCallback onClick;

    private readonly EventCallback onPointerDown;

    private ComboboxLabelBehavior? behavior;

    private ComboboxLabelState state = new(false, false, false, null, false, false);

    /// <summary>Initializes a new instance of the <see cref="ComboboxLabel"/> class.</summary>
    public ComboboxLabel()
    {
        onMouseDown = CreateHandler<MouseEventArgs>(MouseDownEvent, e => behavior!.RunConsumerAsync(MouseDownEvent, e));
        onClick = CreateHandler<MouseEventArgs>(ClickEvent, e => behavior!.RunConsumerAsync(ClickEvent, e));
        onPointerDown = CreateHandler<PointerEventArgs>(PointerDownEvent, e => behavior!.RunConsumerAsync(PointerDownEvent, e));
    }

    [Inject]
    private IJSRuntime JSRuntime { get; set; } = null!;

    [Inject]
    private ILogger<ComboboxLabel> Logger { get; set; } = null!;

    [Inject]
    private PartIdGenerator IdGenerator { get; set; } = null!;

    [CascadingParameter]
    private ComboboxStore? Root { get; set; }

    [CascadingParameter]
    private FieldRootContext? Field { get; set; }

    /// <inheritdoc />
    protected override string DefaultTagName => "div";

    /// <inheritdoc />
    protected override ComboboxLabelState State
    {
        get
        {
            FieldStateSnapshot fieldState = behavior!.Field;
            if (state.Disabled != fieldState.Disabled || state.Touched != fieldState.Touched || state.Dirty != fieldState.Dirty || state.Valid != fieldState.Valid
                || state.Filled != fieldState.Filled || state.Focused != fieldState.Focused)
            {
                state = new ComboboxLabelState(fieldState.Disabled, fieldState.Touched, fieldState.Dirty, fieldState.Valid, fieldState.Filled, fieldState.Focused);
            }

            return state;
        }
    }

    /// <inheritdoc />
    protected override StateAttributesMapping<ComboboxLabelState>? StateAttributesMapping => s_mapping;

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        behavior = new ComboboxLabelBehavior(new ComboboxPartServices("Combobox", JSRuntime, Logger, IdGenerator, StateHasChanged, InvokeAsync, DispatchExceptionAsync));
        base.OnInitialized();
    }

    /// <inheritdoc />
    protected override void OnParametersSet() => behavior!.Apply(AdditionalAttributes);

    /// <inheritdoc />
    protected override Task OnAfterRenderAsync(bool firstRender) => behavior!.AfterRenderAsync(Element);

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing && behavior is not null)
        {
            _ = behavior.DisposeAsync().AsTask();
        }

        base.Dispose(disposing);
    }

    /// <inheritdoc />
    protected override void SubscribeSelectors() => behavior!.Subscribe(Root, Field, SubscribeTo);

    /// <inheritdoc />
    protected override bool ForwardsConsumerHandler(string eventName) => behavior!.ForwardsConsumerHandler(eventName);

    /// <inheritdoc />
    protected override void AddPartAttributes(AttributeList attributes) => behavior!.AddPartAttributes(attributes, onMouseDown, onClick, onPointerDown);

    /// <inheritdoc />
    protected override void FinishAttributes(AttributeList attributes) => behavior!.FinishAttributes(attributes);
}

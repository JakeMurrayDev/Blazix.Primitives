using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace Blazix.Primitives;

// CMB §3.9, STA-07 (combobox/trigger/ComboboxTrigger.tsx): as the input group's.
/// <summary>Represents the state of a combobox trigger, which the part exposes as data attributes and passes to its class and style functions.</summary>
/// <param name="Disabled">A value indicating whether the trigger is disabled; projected as <c>data-disabled</c>.</param>
/// <param name="Touched">A value indicating whether the field was touched; projected as <c>data-touched</c>.</param>
/// <param name="Dirty">A value indicating whether the value differs from the initial one; projected as <c>data-dirty</c>.</param>
/// <param name="Valid">The field's validity, or <see langword="null"/> before validation; projected as <c>data-valid</c> or <c>data-invalid</c>.</param>
/// <param name="Filled">A value indicating whether a value is present; projected as <c>data-filled</c>.</param>
/// <param name="Focused">A value indicating whether the control is focused; projected as <c>data-focused</c>.</param>
/// <param name="Open">A value indicating whether the popup is open; projected as <c>data-popup-open</c> and <c>data-pressed</c>.</param>
/// <param name="ReadOnly">A value indicating whether the value cannot be changed; projected as <c>data-readonly</c>.</param>
/// <param name="PopupSide">The side of the anchor the mounted positioner settled on, or <see langword="null"/>; projected as <c>data-popup-side</c>.</param>
/// <param name="ListEmpty">A value indicating whether the filtered list is empty; projected as <c>data-list-empty</c>.</param>
/// <param name="Placeholder">A value indicating whether no value is selected; projected as <c>data-placeholder</c>.</param>
[PartState]
public sealed partial record ComboboxTriggerState(
    bool Disabled,
    bool Touched,
    bool Dirty,
    bool? Valid,
    bool Filled,
    bool Focused,
    bool Open,
    bool ReadOnly,
    Side? PopupSide,
    bool ListEmpty,
    bool Placeholder);

// combobox/trigger/ComboboxTriggerDataAttributes.ts (CMB-DOM-04; `required` declared at :27, never emitted).
/// <summary>Provides the names of the data attributes a combobox trigger renders.</summary>
public static class ComboboxTriggerDataAttributes
{
    /// <summary>Represents the attribute present while the popup is open: <c>data-popup-open</c>.</summary>
    public const string PopupOpen = PopupTriggerDataAttributes.PopupOpen;

    /// <summary>Represents the attribute present while the popup is open: <c>data-pressed</c>.</summary>
    public const string Pressed = PopupTriggerDataAttributes.Pressed;

    /// <summary>Represents the attribute present while disabled: <c>data-disabled</c>.</summary>
    public const string Disabled = "data-disabled";

    /// <summary>Represents the attribute present while the value cannot be changed: <c>data-readonly</c>.</summary>
    public const string ReadOnly = "data-readonly";

    /// <summary>Represents the attribute whose value is the side of the mounted positioner: <c>data-popup-side</c>.</summary>
    public const string PopupSide = "data-popup-side";

    /// <summary>Represents the attribute declared by Base UI and never rendered; kept for parity: <c>data-required</c>.</summary>
    public const string Required = "data-required";

    /// <summary>Represents the attribute present while the filtered list is empty: <c>data-list-empty</c>.</summary>
    public const string ListEmpty = "data-list-empty";

    /// <summary>Represents the attribute present while no value is selected: <c>data-placeholder</c>.</summary>
    public const string Placeholder = "data-placeholder";

    /// <summary>Represents the attribute present while the field is valid: <c>data-valid</c>.</summary>
    public const string Valid = "data-valid";

    /// <summary>Represents the attribute present while the field is invalid: <c>data-invalid</c>.</summary>
    public const string Invalid = "data-invalid";

    /// <summary>Represents the attribute present after the field was touched: <c>data-touched</c>.</summary>
    public const string Touched = "data-touched";

    /// <summary>Represents the attribute present while the value differs from the initial one: <c>data-dirty</c>.</summary>
    public const string Dirty = "data-dirty";

    /// <summary>Represents the attribute present while a value is present: <c>data-filled</c>.</summary>
    public const string Filled = "data-filled";

    /// <summary>Represents the attribute present while the control is focused: <c>data-focused</c>.</summary>
    public const string Focused = "data-focused";
}

// Base UI's Combobox.Trigger (combobox/trigger/ComboboxTrigger.tsx; CMB §3.9). Over ComboboxTriggerBehavior; the state is the
// field's with the trigger's own flags (CMB-STA-07), projected by the input's mapping (CMB-DOM-04).
/// <summary>Represents the button that opens a combobox's popup, Base UI's <c>Combobox.Trigger</c>, rendered as a <c>button</c>.</summary>
/// <remarks>
/// With the input inside the popup the trigger is the combobox: it carries the combobox's ARIA and the root's id. With the
/// input outside, it is a button beside the input that is not a tab stop.
/// </remarks>
public sealed class ComboboxTrigger : PartBase<ComboboxTriggerState>
{
    private static readonly StateAttributesMapping<ComboboxTriggerState> s_mapping = ComboboxStateMapping.CreateInput<ComboboxTriggerState>();

    private readonly EventCallback onKeyDown;

    private readonly EventCallback onKeyUp;

    private readonly EventCallback onClick;

    private readonly EventCallback onMouseDown;

    private readonly EventCallback onFocus;

    private ComboboxTriggerBehavior? behavior;

    private ComboboxTriggerState state = new(false, false, false, null, false, false, false, false, null, false, true);

    /// <summary>Initializes a new instance of the <see cref="ComboboxTrigger"/> class.</summary>
    public ComboboxTrigger()
    {
        onKeyDown = CreateHandler<KeyboardEventArgs>("onkeydown", e => behavior!.HandleKeyDownAsync(e));
        onKeyUp = CreateHandler<KeyboardEventArgs>("onkeyup", e => behavior!.HandleKeyUpAsync(e));
        onClick = CreateHandler<MouseEventArgs>("onclick", e => behavior!.HandleClickAsync(e));
        onMouseDown = CreateHandler<MouseEventArgs>("onmousedown", e => behavior!.HandleMouseDownAsync(e));
        onFocus = CreateHandler<FocusEventArgs>("onfocus", _ => behavior!.OnFocusChange(true));
    }

    [Inject]
    private IJSRuntime JSRuntime { get; set; } = null!;

    [Inject]
    private ILogger<ComboboxTrigger> Logger { get; set; } = null!;

    [Inject]
    private PartIdGenerator IdGenerator { get; set; } = null!;

    [Inject]
    private ButtonDevelopmentLog DevelopmentLog { get; set; } = null!;

    [CascadingParameter]
    private ComboboxStore? Root { get; set; }

    [CascadingParameter]
    private FieldRootContext? Field { get; set; }

    // CMB-API-43
    /// <summary>Gets or sets a value indicating whether the rendered element is a native <c>button</c>; the default is <see langword="true"/>.</summary>
    [Parameter]
    public bool NativeButton { get; set; } = true;

    // CMB-API-44
    /// <summary>Gets or sets a value indicating whether the trigger is disabled; the combobox's and the field's disabled state also apply.</summary>
    [Parameter]
    public bool Disabled { get; set; }

    /// <inheritdoc />
    protected override string DefaultTagName => "button";

    /// <inheritdoc />
    protected override ComboboxTriggerState State
    {
        get
        {
            ComboboxTriggerBehavior trigger = behavior!;
            ComboboxStoreState root = trigger.Store.State;
            FieldStateSnapshot fieldState = trigger.Field;
            bool open = root.Lifecycle.EffectiveOpen;
            Side? side = root.Lifecycle.Mounted ? root.PopupSide : null;
            bool listEmpty = root.FilteredCount == 0;
            bool placeholder = root.SelectionMode != ComboboxSelectionMode.None && !root.HasSelectedValue;
            if (state.Disabled != fieldState.Disabled || state.Touched != fieldState.Touched || state.Dirty != fieldState.Dirty || state.Valid != fieldState.Valid
                || state.Filled != fieldState.Filled || state.Focused != fieldState.Focused || state.Open != open || state.ReadOnly != root.ReadOnly
                || state.PopupSide != side || state.ListEmpty != listEmpty || state.Placeholder != placeholder)
            {
                state = new ComboboxTriggerState(fieldState.Disabled, fieldState.Touched, fieldState.Dirty, fieldState.Valid, fieldState.Filled, fieldState.Focused, open, root.ReadOnly, side, listEmpty, placeholder);
            }

            return state;
        }
    }

    /// <inheritdoc />
    protected override StateAttributesMapping<ComboboxTriggerState>? StateAttributesMapping => s_mapping;

    /// <inheritdoc />
    protected override bool UsesGeneratedId => true;

    // For the fixtures: completes once the trigger's handles are attached.
    internal Task TwinAttached => behavior?.TwinAttached ?? Task.CompletedTask;

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        behavior = new ComboboxTriggerBehavior(new ComboboxPartServices("Combobox", JSRuntime, Logger, IdGenerator, StateHasChanged, InvokeAsync, DispatchExceptionAsync), DevelopmentLog, onKeyDown, onKeyUp, onClick, onMouseDown);
        base.OnInitialized();
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        behavior!.Apply(NativeButton, Disabled, GeneratedId, AdditionalAttributes);
    }

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
    protected override void AddPartAttributes(AttributeList attributes)
    {
        behavior!.AddPartAttributes(attributes);
        attributes.Set("onfocus", onFocus);
    }

    /// <inheritdoc />
    protected override void FinishAttributes(AttributeList attributes) => behavior!.FinishAttributes(attributes);
}

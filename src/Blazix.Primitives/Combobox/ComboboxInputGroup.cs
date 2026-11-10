using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace Blazix.Primitives;

// CMB §3.3, STA-02 (combobox/input-group/ComboboxInputGroup.tsx:27-49): the field state with the Root's disabled, the popup and list flags.
/// <summary>Represents the state of a combobox input group, which the part exposes as data attributes and passes to its class and style functions.</summary>
/// <param name="Disabled">A value indicating whether the combobox is disabled; projected as <c>data-disabled</c>.</param>
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
public sealed partial record ComboboxInputGroupState(
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

// combobox/input-group/ComboboxInputGroupDataAttributes.ts (CMB-DOM-03).
/// <summary>Provides the names of the data attributes a combobox input group renders.</summary>
public static class ComboboxInputGroupDataAttributes
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

// Base UI's Combobox.InputGroup (combobox/input-group/ComboboxInputGroup.tsx; CMB §3.3). Over ComboboxInputGroupBehavior; the
// state is the field's with the root's disabled state and the popup's flags (CMB-STA-02), projected by the input's mapping.
/// <summary>Represents the group around a combobox's input and its buttons, Base UI's <c>Combobox.InputGroup</c>, rendered as a <c>div</c>.</summary>
/// <remarks>A press on the group outside its buttons focuses the input and opens the popup; the popup is positioned against the group.</remarks>
public sealed class ComboboxInputGroup : PartBase<ComboboxInputGroupState>
{
    private static readonly StateAttributesMapping<ComboboxInputGroupState> s_mapping = ComboboxStateMapping.CreateInput<ComboboxInputGroupState>();

    private ComboboxInputGroupBehavior? behavior;

    private ComboboxInputGroupState state = new(false, false, false, null, false, false, false, false, null, false, true);

    [Inject]
    private IJSRuntime JSRuntime { get; set; } = null!;

    [Inject]
    private ILogger<ComboboxInputGroup> Logger { get; set; } = null!;

    [Inject]
    private PartIdGenerator IdGenerator { get; set; } = null!;

    [CascadingParameter]
    private ComboboxStore? Root { get; set; }

    [CascadingParameter]
    private FieldRootContext? Field { get; set; }

    /// <inheritdoc />
    protected override string DefaultTagName => "div";

    /// <inheritdoc />
    protected override ComboboxInputGroupState State
    {
        get
        {
            ComboboxInputGroupBehavior group = behavior!;
            ComboboxStoreState root = group.Store.State;
            FieldStateSnapshot fieldState = group.Field;
            bool open = root.Lifecycle.EffectiveOpen;
            Side? side = root.Lifecycle.Mounted ? root.PopupSide : null;
            bool listEmpty = root.FilteredCount == 0;
            bool placeholder = root.SelectionMode != ComboboxSelectionMode.None && !root.HasSelectedValue;
            if (state.Disabled != fieldState.Disabled || state.Touched != fieldState.Touched || state.Dirty != fieldState.Dirty || state.Valid != fieldState.Valid
                || state.Filled != fieldState.Filled || state.Focused != fieldState.Focused || state.Open != open || state.ReadOnly != root.ReadOnly
                || state.PopupSide != side || state.ListEmpty != listEmpty || state.Placeholder != placeholder)
            {
                state = new ComboboxInputGroupState(fieldState.Disabled, fieldState.Touched, fieldState.Dirty, fieldState.Valid, fieldState.Filled, fieldState.Focused, open, root.ReadOnly, side, listEmpty, placeholder);
            }

            return state;
        }
    }

    /// <inheritdoc />
    protected override StateAttributesMapping<ComboboxInputGroupState>? StateAttributesMapping => s_mapping;

    /// <inheritdoc />
    protected override bool UsesGeneratedId => true;

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        behavior = new ComboboxInputGroupBehavior(new ComboboxPartServices("Combobox", JSRuntime, Logger, IdGenerator, StateHasChanged, InvokeAsync, DispatchExceptionAsync));
        base.OnInitialized();
    }

    /// <inheritdoc />
    protected override void OnParametersSet() => behavior!.Apply(Id);

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
    protected override void AddPartAttributes(AttributeList attributes) => behavior!.AddPartAttributes(attributes);
}

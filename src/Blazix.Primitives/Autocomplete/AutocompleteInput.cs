using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace Blazix.Primitives;

// AUT-STR-02, STR-05 (M5 track 11; decision 12, ADR 08 Amendments 2026-11-03 (3)): the Autocomplete parts of this file
// are Combobox's, as sealed shells over the same internal behaviours with the family `Autocomplete`; the comments below
// cite the Combobox rows the Autocomplete document adopts (autocomplete.md §2.1).

// CMB §3.7, STA-05 (combobox/input/AutocompleteInput.tsx): the field state (the default inside the popup), the popup and list flags.
/// <summary>Represents the state of an autocomplete input, which the part exposes as data attributes and passes to its class and style functions.</summary>
/// <param name="Disabled">A value indicating whether the input is disabled; projected as <c>data-disabled</c>.</param>
/// <param name="Touched">A value indicating whether the field was touched; projected as <c>data-touched</c>.</param>
/// <param name="Dirty">A value indicating whether the value differs from the initial one; projected as <c>data-dirty</c>.</param>
/// <param name="Valid">The field's validity, or <see langword="null"/> before validation; projected as <c>data-valid</c> or <c>data-invalid</c>.</param>
/// <param name="Filled">A value indicating whether a value is present; projected as <c>data-filled</c>.</param>
/// <param name="Focused">A value indicating whether the control is focused; projected as <c>data-focused</c>.</param>
/// <param name="Open">A value indicating whether the popup is open; projected as <c>data-popup-open</c> and <c>data-pressed</c>.</param>
/// <param name="ReadOnly">A value indicating whether the value cannot be changed; projected as <c>data-readonly</c>.</param>
/// <param name="PopupSide">The side of the anchor the mounted positioner settled on, or <see langword="null"/>; projected as <c>data-popup-side</c>.</param>
/// <param name="ListEmpty">A value indicating whether the filtered list is empty; projected as <c>data-list-empty</c>.</param>
[PartState]
public sealed partial record AutocompleteInputState(
    bool Disabled,
    bool Touched,
    bool Dirty,
    bool? Valid,
    bool Filled,
    bool Focused,
    bool Open,
    bool ReadOnly,
    Side? PopupSide,
    bool ListEmpty);

// combobox/input/AutocompleteInputDataAttributes.ts (CMB-DOM-02; `required` is declared at :27 and never emitted, §16 Q1: the constant is kept, M5 track 10 brief).
/// <summary>Provides the names of the data attributes an autocomplete input renders.</summary>
public static class AutocompleteInputDataAttributes
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

// Base UI's Combobox.Input (combobox/input/AutocompleteInput.tsx; CMB §3.7): a sealed thin part over ComboboxInputBehavior,
// which Autocomplete's Input shells (ADR 08 Amendments 2026-11-03 (3)). The input mapping is the pressable trigger mapping
// with data-popup-side, data-list-empty and the field validity (combobox/utils/stateAttributesMapping.ts).
/// <summary>Represents the text input of an autocomplete, Base UI's <c>Autocomplete.Input</c>, rendered as an <c>input</c>.</summary>
/// <remarks>
/// Outside the popup it is the autocomplete: it carries the autocomplete's ARIA and opens the list as the user types or presses it.
/// Inside the popup it filters the list while the trigger carries the autocomplete's ARIA.
/// </remarks>
public sealed class AutocompleteInput : PartBase<AutocompleteInputState>
{
    private static readonly StateAttributesMapping<AutocompleteInputState> s_mapping = ComboboxStateMapping.CreateInput<AutocompleteInputState>();

    private readonly EventCallback onKeyDown;

    private readonly EventCallback onClick;

    private readonly EventCallback onMouseDown;

    private readonly EventCallback onFocus;

    private readonly EventCallback onBlur;

    private readonly RenderFragment element;

    private ComboboxInputBehavior? behavior;

    private AutocompleteInputState state = new(false, false, false, null, false, false, false, false, null, false);

    /// <summary>Initializes a new instance of the <see cref="AutocompleteInput"/> class.</summary>
    public AutocompleteInput()
    {
        onKeyDown = CreateHandler<KeyboardEventArgs>("onkeydown", e => behavior!.HandleKeyDownAsync(e));
        onClick = CreateHandler<MouseEventArgs>("onclick", e => behavior!.HandleClickAsync(e));
        onMouseDown = CreateHandler<MouseEventArgs>("onmousedown", e => behavior!.HandleMouseDownAsync(e));
        onFocus = CreateHandler<FocusEventArgs>("onfocus", _ => behavior!.OnFocusChangeAsync(true));
        onBlur = CreateHandler<FocusEventArgs>("onblur", _ => behavior!.OnFocusChangeAsync(false));
        element = base.BuildRenderTree;
    }

    [Inject]
    private IJSRuntime JSRuntime { get; set; } = null!;

    [Inject]
    private ILogger<AutocompleteInput> Logger { get; set; } = null!;

    [Inject]
    private PartIdGenerator IdGenerator { get; set; } = null!;

    [CascadingParameter]
    private ComboboxStore? Root { get; set; }

    [CascadingParameter]
    private ComboboxPositionerContext? AutocompletePositioner { get; set; }

    [CascadingParameter]
    private FieldRootContext? Field { get; set; }

    [CascadingParameter]
    private DirectionProviderContext? Direction { get; set; }

    // CMB-API-38
    /// <summary>Gets or sets a value indicating whether the input is disabled; the autocomplete's and the field's disabled state also apply.</summary>
    [Parameter]
    public bool Disabled { get; set; }

    /// <inheritdoc />
    protected override string DefaultTagName => "input";

    /// <inheritdoc />
    protected override AutocompleteInputState State
    {
        get
        {
            ComboboxInputBehavior input = behavior!;
            ComboboxStoreState root = input.Store.State;
            FieldStateSnapshot fieldState = input.Field;
            bool open = root.Lifecycle.EffectiveOpen;
            Side? side = root.Lifecycle.Mounted ? root.PopupSide : null;
            bool listEmpty = root.FilteredCount == 0;
            if (state.Disabled != fieldState.Disabled || state.Touched != fieldState.Touched || state.Dirty != fieldState.Dirty || state.Valid != fieldState.Valid
                || state.Filled != fieldState.Filled || state.Focused != fieldState.Focused || state.Open != open || state.ReadOnly != root.ReadOnly
                || state.PopupSide != side || state.ListEmpty != listEmpty)
            {
                state = new AutocompleteInputState(fieldState.Disabled, fieldState.Touched, fieldState.Dirty, fieldState.Valid, fieldState.Filled, fieldState.Focused, open, root.ReadOnly, side, listEmpty);
            }

            return state;
        }
    }

    /// <inheritdoc />
    protected override StateAttributesMapping<AutocompleteInputState>? StateAttributesMapping => s_mapping;

    /// <inheritdoc />
    protected override bool UsesGeneratedId => true;

    // For the fixtures: completes once the input's handles are attached.
    internal Task TwinAttached => behavior?.TwinAttached ?? Task.CompletedTask;

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        behavior = new ComboboxInputBehavior(new ComboboxPartServices("Autocomplete", JSRuntime, Logger, IdGenerator, StateHasChanged, InvokeAsync, DispatchExceptionAsync), onKeyDown, onClick, onMouseDown);
        base.OnInitialized();
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        behavior!.Apply(Disabled, GeneratedId, AdditionalAttributes, Direction, Render);
    }

    /// <inheritdoc />
    protected override Task OnAfterRenderAsync(bool firstRender) => behavior!.AfterRenderAsync(Element);

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder) => behavior!.BuildElement(builder, element);

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
    protected override void SubscribeSelectors() => behavior!.Subscribe(Root, AutocompletePositioner, Field, SubscribeTo);

    /// <inheritdoc />
    protected override bool ForwardsConsumerHandler(string eventName) => behavior!.ForwardsConsumerHandler(eventName);

    /// <inheritdoc />
    protected override void AddPartAttributes(AttributeList attributes)
    {
        behavior!.AddPartAttributes(attributes);
        attributes.Set("onfocus", onFocus);
        attributes.Set("onblur", onBlur);
    }

    /// <inheritdoc />
    protected override void FinishAttributes(AttributeList attributes) => behavior!.FinishAttributes(attributes);

    /// <inheritdoc />
    protected override void BuildBeforeElement(RenderTreeBuilder builder) => behavior!.BuildBeforeElement(builder);
}

using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace Blazix.Primitives;

// TGL-STA-01; toggle/Toggle.tsx:73-76,145-154. ADR 08 §3: the state record is sealed, partial and [PartState]; the default
// projection gives data-pressed and data-disabled (TGL §11: no custom mapping).
/// <summary>Represents the state of a toggle, which the part exposes as data attributes and passes to its class and style functions.</summary>
/// <param name="Pressed">A value indicating whether the toggle is pressed; projected as <c>data-pressed</c>.</param>
/// <param name="Disabled">A value indicating whether the toggle ignores user interaction; projected as <c>data-disabled</c>.</param>
[PartState]
public sealed partial record ToggleState(bool Pressed, bool Disabled);

// TGL §3.1; toggle/ToggleDataAttributes.ts:4,8.
/// <summary>Provides the names of the data attributes a toggle renders.</summary>
public static class ToggleDataAttributes
{
    /// <summary>Represents the attribute present, with an empty value, while the toggle is pressed: <c>data-pressed</c>.</summary>
    public const string Pressed = "data-pressed";

    /// <summary>Represents the attribute present, with an empty value, while the toggle is disabled: <c>data-disabled</c>.</summary>
    public const string Disabled = "data-disabled";
}

// Base UI's Toggle (toggle/Toggle.tsx); the contract is docs/components/toggle.md.
// - One `button` with aria-pressed always "true" or "false" (TGL-STR-01, TGL-ARIA-01). The button mechanism is
//   ButtonBehavior (UBT; button-behavior.md §6.2: native = NativeButton, disabled = effective, focusableWhenDisabled
//   unset), one ButtonTwin. Composite button mode is inferred from the optional CompositeRootContext cascade (UBT-STA-01;
//   useButton.ts:25-26; ADR 04 Amendments 2026-10-26, chosen by the user): under any composite root Space activates on
//   keydown, the writer emits no tabindex and the composite rows of TGL-ARIA-02 apply, whether or not the toggle is an
//   item (TGL-EDGE-09, toggle §16 Q1).
// - The consumer's form and type are dropped (TGL-API-08, TGL-DOM-04; FinishAttributes, the COL-API-10 precedent, ADR 02
//   Amendments 2026-10-21); every other consumer attribute wins (TGL-DOM-06). No hidden input (TGL-FRM-01).
// - The part's own onclick runs the request of TGL-PTR-01 with the click's snapshot (ADR 04 Amendments 2026-10-21). The
//   consumer's onclick is not declined (ButtonBehavior declines it only with the hidden-input bridge), so it runs first in
//   the pipeline's chain and its PreventPartHandler() stops the toggle on both render modes (TGL-PTR-03). Standalone the
//   pressed state is a Controlled<bool> fixed at the first parameter set (TGL-API-01/02, TGL-STA-02).
/// <summary>
/// Represents a two-state button that is pressed or not: Base UI's <c>Toggle</c>, rendered as a <c>button</c> whose
/// <c>aria-pressed</c> reports the state.
/// </summary>
/// <remarks>
/// A toggle is never a form control: it renders no input, and the <c>form</c> and <c>type</c> attributes are dropped so
/// a native button never submits a form.
/// </remarks>
public sealed class Toggle : PartBase<ToggleState>
{
    private const string DefaultTag = "button";

    private static readonly ToggleState s_unpressed = new(Pressed: false, Disabled: false);
    private static readonly ToggleState s_pressed = new(Pressed: true, Disabled: false);
    private static readonly ToggleState s_unpressedDisabled = new(Pressed: false, Disabled: true);
    private static readonly ToggleState s_pressedDisabled = new(Pressed: true, Disabled: true);

    private readonly EventCallback onClick;
    private readonly EventCallback onKeyDown;
    private readonly EventCallback onKeyUp;
    private ButtonBehavior? behavior;
    private Controlled<bool>? pressedState;
    private bool pressedSupplied;
    private bool defaultPressedSupplied;
    private ToggleState state = s_unpressed;

    /// <summary>Initializes a new instance of the <see cref="Toggle"/> class.</summary>
    public Toggle()
    {
        onClick = CreateHandler<MouseEventArgs>("onclick", HandleClickAsync);
        onKeyDown = CreateHandler<KeyboardEventArgs>("onkeydown", e => behavior!.HandleKeyDownAsync(e));
        onKeyUp = CreateHandler<KeyboardEventArgs>("onkeyup", e => behavior!.HandleKeyUpAsync(e));
    }

    [Inject]
    private IJSRuntime JSRuntime { get; set; } = null!;

    [Inject]
    private ILogger<Toggle> Logger { get; set; } = null!;

    [Inject]
    private ButtonDevelopmentLog DevelopmentLog { get; set; } = null!;

    [Inject]
    private PartIdGenerator IdGenerator { get; set; } = null!;

    // UBT-STA-01: composite button mode under any composite root.
    private bool Composite => CompositeContext is not null;

    // TGL-API-04
    private bool EffectiveDisabled => Disabled;

    [CascadingParameter]
    private CompositeRootContext? CompositeContext { get; set; }

    // TGL-API-01, TGL-STA-05; ADR 05 §2
    /// <summary>Gets or sets a value indicating whether the toggle is pressed, when the consumer owns the state.</summary>
    /// <value>
    /// The pressed state. Supplying it at the first render makes the state the consumer's for the lifetime of the toggle:
    /// a click then only raises <see cref="OnPressedChange"/> and <see cref="PressedChanged"/>.
    /// </value>
    [Parameter]
    public bool Pressed { get; set; }

    // ADR 05 §2: the binding half of @bind-Pressed.
    /// <summary>Gets or sets the callback that receives the new pressed state after a click that was not canceled.</summary>
    /// <value>The callback <c>@bind-Pressed</c> supplies, invoked after <see cref="OnPressedChange"/>.</value>
    [Parameter]
    public EventCallback<bool> PressedChanged { get; set; }

    // TGL-API-02
    /// <summary>Gets or sets a value indicating whether the toggle is initially pressed, when the toggle owns the state.</summary>
    /// <value><see langword="true"/> to start pressed; the default is <see langword="false"/>. Read at the first render only.</value>
    [Parameter]
    public bool DefaultPressed { get; set; }

    // TGL-API-03, TGL-EVT-01..09
    /// <summary>Gets or sets the callback invoked when the user activates the toggle, before the pressed state changes.</summary>
    /// <value>
    /// The callback, which receives the next pressed state and the change details: the reason
    /// <see cref="EventReason.None"/> and the click. Calling <see cref="ChangeEventDetails.Cancel"/> keeps the current state.
    /// </value>
    [Parameter]
    public EventCallback<ValueChangeEventArgs<bool>> OnPressedChange { get; set; }

    // TGL-API-04
    /// <summary>Gets or sets a value indicating whether the toggle ignores user interaction.</summary>
    /// <value>
    /// <see langword="true"/> to ignore clicks and keys, which also withholds the consumer's <c>onclick</c>,
    /// <c>onmousedown</c>, <c>onpointerdown</c>, <c>onkeydown</c> and <c>onkeyup</c> handlers; otherwise,
    /// <see langword="false"/>. The default is <see langword="false"/>.
    /// </value>
    [Parameter]
    public bool Disabled { get; set; }

    // TGL-API-05, TGL-FRM-01
    /// <summary>Gets or sets the string that identifies the toggle within a toggle group.</summary>
    /// <value>The identity, never rendered; or <see langword="null"/>, the default.</value>
    [Parameter]
    public string? Value { get; set; }

    // TGL-API-06, UBT-API-03
    /// <summary>Gets or sets a value indicating whether the rendered element is a native <c>button</c>.</summary>
    /// <value>
    /// <see langword="true"/>, the default, for a <c>button</c> element; <see langword="false"/> when
    /// <see cref="PartBase{TState}.Render"/> supplies another element, which then gets <c>role="button"</c>, a
    /// <c>tabindex</c>, and clicks from Enter and Space.
    /// </value>
    [Parameter]
    public bool NativeButton { get; set; } = true;

    /// <inheritdoc />
    protected override string DefaultTagName => DefaultTag;

    /// <inheritdoc />
    protected override ToggleState State => state;

    // ADR 02 decision 1: under a substitute callback the twin finds the element by id (the Button precedent).
    /// <inheritdoc />
    protected override bool UsesGeneratedId => RenderFunction is not null;

    // For the fixtures (tests/Blazix.Primitives.Fixtures.Client): completes once the twin is attached, so a browser test
    // can wait for the module's listeners before it clicks or presses keys.
    /// <summary>Gets a task that completes when the toggle's behavior is attached to its element.</summary>
    /// <value>A task that completes after the first interactive render has attached the behavior.</value>
    internal Task TwinAttached => behavior!.TwinAttached;

    /// <inheritdoc />
    public override Task SetParametersAsync(ParameterView parameters)
    {
        // TGL-STA-02: whether each parameter was supplied in this parameter set; Controlled<bool> takes them raw.
        pressedSupplied = parameters.TryGetValue<bool>(nameof(Pressed), out _);
        defaultPressedSupplied = parameters.TryGetValue<bool>(nameof(DefaultPressed), out _);
        return base.SetParametersAsync(parameters);
    }

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        behavior = new ButtonBehavior(JSRuntime, Logger, DevelopmentLog, IdGenerator, StateHasChanged, DispatchExceptionAsync, onKeyDown, onKeyUp);
        pressedState = new Controlled<bool>("Toggle", "pressed", Logger);
        base.OnInitialized();
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        // TGL-API-01/02, TGL-STA-02, TGL-EDGE-02/03: ownership is fixed at the first parameter set (ADR 05 Amendments
        // 2026-10-18).
        pressedState!.Update(pressedSupplied, Pressed, defaultPressedSupplied, DefaultPressed);
        behavior!.Disabled = EffectiveDisabled;
        behavior.Native = NativeButton;
        behavior.FocusableWhenDisabled = null;
        behavior.SetConsumerAttributes(AdditionalAttributes);
        UpdateState();
    }

    /// <inheritdoc />
    protected override Task OnAfterRenderAsync(bool firstRender)
    {
        return behavior!.AfterRenderAsync(Enabled, Element, Id, Composite);
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            behavior?.Dispose();
        }

        base.Dispose(disposing);
    }

    // TGL-ARIA-01, TGL-PTR-01; the button writer's type or role, tabindex, disabled and aria-disabled (TGL-ARIA-02).
    /// <inheritdoc />
    protected override void AddPartAttributes(AttributeList attributes)
    {
        attributes.Set("aria-pressed", state.Pressed ? "true" : "false");
        attributes.Set("onclick", onClick);
        behavior!.AddAttributes(attributes, Id, Composite);
    }

    // ADR 02 Amendments 2026-10-21 (FinishAttributes); part-owned by the document: TGL-API-08 discards the consumer's
    // `form` and `type`, and TGL-DOM-04 makes a native toggle always type="button" (toggle §16 Q5: accepted and ignored).
    /// <inheritdoc />
    protected override void FinishAttributes(AttributeList attributes)
    {
        attributes.Remove("form");
        if (NativeButton)
        {
            attributes.Set("type", "button");
        }
        else
        {
            attributes.Remove("type");
        }
    }

    /// <inheritdoc />
    protected override bool ForwardsConsumerHandler(string eventName)
    {
        return behavior!.ForwardsConsumerHandler(eventName);
    }

    // TGL-PTR-01's seven steps (toggle/Toggle.tsx:82-103): the next state, the details with reason none and the click
    // (TGL-EVT-02/03), OnPressedChange, stop when canceled, then the commit when the toggle owns the state.
    private async Task HandleClickAsync(MouseEventArgs e)
    {
        // TGL-PTR-02, TGL-EVT-08: the module prevents a disabled click; a dispatch that still arrives changes nothing.
        if (EffectiveDisabled)
        {
            return;
        }

        bool next = !state.Pressed;
        ChangeEventDetails details = new(EventReason.None, EventSnapshot.FromEventArgs(e));
        await pressedState!.RequestAsync(next, details, OnPressedChange, PressedChanged);

        // ADR 16 rule 8: a canceled or consumer-owned request renders nothing.
        if (UpdateState())
        {
            StateHasChanged();
        }
    }

    // Returns whether the state changed; the four states are shared instances, so a render allocates none (ADR 16 rule 7).
    private bool UpdateState()
    {
        bool pressed = pressedState!.Value;
        bool disabled = EffectiveDisabled;
        if (state.Pressed == pressed && state.Disabled == disabled)
        {
            return false;
        }

        state = (pressed, disabled) switch
        {
            (false, false) => s_unpressed,
            (true, false) => s_pressed,
            (false, true) => s_unpressedDisabled,
            (true, true) => s_pressedDisabled,
        };
        return true;
    }
}

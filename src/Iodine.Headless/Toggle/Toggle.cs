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
// - Inside a ToggleGroup, at any depth, the toggle is "always controlled" (TGL §14): pressed is "the group's value holds my
//   identity" on every render, Pressed and DefaultPressed are ignored silently (TGL-STA-04, TGL-EDGE-06), and a click runs
//   OnPressedChange, then, unless canceled, the group's request with the same details (TGL-EVT-04/05; the CheckboxGroup
//   member path). The toggle commits nothing of its own, so toggle §16 Q4's second cancel check has nothing to guard. The
//   identity is Value when non-empty, else the part's generated id, drawn once (ADR 08 Amendments 2026-10-26, chosen by
//   the user); a grouped toggle draws it and renders it as its id (design point 12.1 (a), settled by the orchestrator
//   2026-10-26).
// - A grouped toggle is a composite item of the nearest root (CompositeItemBehavior; TGL-STR-02; the RadioRoot wiring):
//   the item's tabindex="-1" follows the button writer's attributes, so it is rendered (CMP-STA-15), and the `composite`
//   module owns it from attach (ADR 04 Amendments 2026-10-22). A toggle under a root without a group registers nothing.
// - One keydown gate (ADR 01 and ADR 04 Amendments 2026-10-26, chosen by the user; the Radio precedent): both behaviours
//   decline the consumer's onkeydown and the part's one handler runs it through the gate whose one-shot marker the key may
//   have left: in a group, the keys the composite navigates by take the item's (`navigate`), every other key the button's
//   (`activate`); standalone, every key the button's. The navigation set is the arrows, Home and End (design point 12.3
//   (a), settled by the orchestrator 2026-10-26): a key the root does not handle leaves no marker in either gate, so a
//   root without Home and End (Toolbar) and one with them (ToggleGroup, Tabs) follow the same rule.
// - TGL-EDGE-01 is checked after render (ADR 01 decision 4, "after paint"), through the scoped ToggleDevelopmentLog.
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
    private const string ArrowKeyPrefix = "Arrow";

    private static readonly ToggleState s_unpressed = new(Pressed: false, Disabled: false);
    private static readonly ToggleState s_pressed = new(Pressed: true, Disabled: false);
    private static readonly ToggleState s_unpressedDisabled = new(Pressed: false, Disabled: true);
    private static readonly ToggleState s_pressedDisabled = new(Pressed: true, Disabled: true);

    private readonly EventCallback onClick;
    private readonly EventCallback onKeyDown;
    private readonly EventCallback onKeyUp;
    private readonly Action groupChanged;
    private ButtonBehavior? behavior;
    private CompositeItemBehavior? item;
    private ToggleGroupContext? group;
    private Controlled<bool>? pressedState;
    private bool pressedSupplied;
    private bool defaultPressedSupplied;
    private ToggleState state = s_unpressed;

    /// <summary>Initializes a new instance of the <see cref="Toggle"/> class.</summary>
    public Toggle()
    {
        onClick = CreateHandler<MouseEventArgs>("onclick", HandleClickAsync);
        onKeyDown = CreateHandler<KeyboardEventArgs>("onkeydown", HandleKeyDownAsync);
        onKeyUp = CreateHandler<KeyboardEventArgs>("onkeyup", e => behavior!.HandleKeyUpAsync(e));
        groupChanged = OnGroupChanged;
    }

    [Inject]
    private IJSRuntime JSRuntime { get; set; } = null!;

    [Inject]
    private ILogger<Toggle> Logger { get; set; } = null!;

    [Inject]
    private ButtonDevelopmentLog DevelopmentLog { get; set; } = null!;

    [Inject]
    private PartIdGenerator IdGenerator { get; set; } = null!;

    [Inject]
    private ToggleDevelopmentLog ToggleLog { get; set; } = null!;

    // UBT-STA-01: composite button mode under any composite root.
    private bool Composite => CompositeContext is not null;

    // TGL-API-04
    private bool EffectiveDisabled => Disabled || (group?.Disabled ?? false);

    // TGL-API-05: Value when non-empty, else the generated id (`""` is replaced too, toggle/Toggle.tsx:41-42).
    private string Identity => string.IsNullOrEmpty(Value) ? GeneratedId! : Value;

    // TGL-STA-04: in a group, membership in the group's value; standalone, the controlled state.
    private bool IsPressed => group is not null ? group.Contains(Identity) : pressedState!.Value;

    [CascadingParameter]
    private CompositeRootContext? CompositeContext { get; set; }

    [CascadingParameter]
    private ToggleGroupContext? GroupContext { get; set; }

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

    // Design point 12.1 (a), settled by the orchestrator 2026-10-26: a grouped toggle draws the id it registers and identifies
    // itself by (ADR 08 and ADR 04 Amendments 2026-10-26 and 2026-10-22, the RadioRoot precedent); ADR 02
    // decision 1: under a substitute callback the twin finds the element by id (the Button precedent). A standalone toggle
    // draws none and renders none, as Base UI renders none.
    /// <inheritdoc />
    protected override bool UsesGeneratedId => GroupContext is not null || RenderFunction is not null;

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
        group = GroupContext;
        if (group is not null)
        {
            // TGL-STR-02: a composite item only inside a group, of the nearest root.
            item = new CompositeItemBehavior(CompositeContext, "Toggle", onKeyDown);
            group.AddListener(groupChanged);
        }
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        // TGL-API-01/02, TGL-STA-02, TGL-EDGE-02/03: ownership is fixed at the first parameter set (ADR 05 Amendments
        // 2026-10-18). TGL-EDGE-06: in a group the parameters are ignored, and so are their diagnostics.
        if (group is null)
        {
            pressedState!.Update(pressedSupplied, Pressed, defaultPressedSupplied, DefaultPressed);
        }

        behavior!.Disabled = EffectiveDisabled;
        behavior.Native = NativeButton;
        behavior.FocusableWhenDisabled = null;
        behavior.SetConsumerAttributes(AdditionalAttributes);
        if (item is not null)
        {
            // TLB §4, M3 track 1's E3: a toggle's flags are its effective disabled state and never focusable when disabled; a
            // toolbar's root derives its disabled indices from them, and a standalone group's root reads the document.
            item.SetRegistration(Id, AdditionalAttributes, EffectiveDisabled, focusableWhenDisabled: false);
            // TGL-KEY-05: a disabled toggle's key handlers are not invoked, so the root's key handling runs none either.
            item.SetConsumerAttributes(EffectiveDisabled ? null : AdditionalAttributes);
        }

        UpdateState();
    }

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        await behavior!.AfterRenderAsync(Enabled, Element, Id, Composite);
        if (item is not null)
        {
            await item.AfterRenderAsync(Element);
        }

        // TGL-EDGE-01: only a null Value counts (`""` does not, toggle/Toggle.tsx:51), in a group whose value the consumer
        // initialized (TGG-STA-07).
        if (group is not null && Value is null && group.IsValueInitialized)
        {
            ToggleLog.ReportMissingValue(Logger);
        }
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            behavior?.Dispose();
            item?.Dispose();
            group?.RemoveListener(groupChanged);
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

        // CMP-STA-15: the item's tabindex replaces nothing the button writer rendered in composite mode (UBT-DOM-05), and
        // the consumer's still wins at the merge.
        item?.AddAttributes(attributes);
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
        return behavior!.ForwardsConsumerHandler(eventName) && (item?.ForwardsConsumerHandler(eventName) ?? true);
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

        bool next = !IsPressed;
        ChangeEventDetails details = new(EventReason.None, EventSnapshot.FromEventArgs(e));
        if (group is not null)
        {
            // TGL-EVT-04/05: the group is asked only when the toggle's callback did not cancel, with the same details; the
            // group notifies the toggles whose state changed.
            if (await pressedState!.NotifyAsync(next, details, OnPressedChange))
            {
                await group.RequestAsync(Identity, next, details);
            }

            return;
        }

        await pressedState!.RequestAsync(next, details, OnPressedChange, PressedChanged);

        // ADR 16 rule 8: a canceled or consumer-owned request renders nothing.
        if (UpdateState())
        {
            StateHasChanged();
        }
    }

    // The one keydown gate: the key reaches the gate whose decision point may have run the consumer's handler for it.
    private Task HandleKeyDownAsync(KeyboardEventArgs e)
    {
        return item is not null && IsItemKey(e.Key) ? item.HandleKeyDownAsync(e) : behavior!.HandleKeyDownAsync(e);
    }

    // A change of the group's value, disabled state or initialized flag: render only when this toggle's state changed, or
    // when the missing-value check now applies, so it runs after that render.
    private void OnGroupChanged()
    {
        bool wasDisabled = behavior!.Disabled;
        behavior.Disabled = EffectiveDisabled;
        if (item is not null && wasDisabled != behavior.Disabled)
        {
            item.SetRegistration(Id, AdditionalAttributes, EffectiveDisabled, focusableWhenDisabled: false);
            item.SetConsumerAttributes(EffectiveDisabled ? null : AdditionalAttributes);
        }

        if (UpdateState() || (Value is null && group!.IsValueInitialized && !ToggleLog.MissingValueWritten))
        {
            StateHasChanged();
        }
    }

    // Returns whether the state changed; the four states are shared instances, so a render allocates none (ADR 16 rule 7).
    private bool UpdateState()
    {
        bool pressed = IsPressed;
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

    // The composite's navigation keys: arrows (TGL-KEY-03), Home and End (TGG-KEY-04; design point 12.3 (a), settled by the
    // orchestrator 2026-10-26), never activation keys.
    private static bool IsItemKey(string? key)
    {
        return key is not null
            && (key.StartsWith(ArrowKeyPrefix, StringComparison.Ordinal)
                || string.Equals(key, "Home", StringComparison.Ordinal)
                || string.Equals(key, "End", StringComparison.Ordinal));
    }
}

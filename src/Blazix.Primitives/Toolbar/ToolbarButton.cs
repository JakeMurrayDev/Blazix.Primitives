using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace Blazix.Primitives;

// TLB-STA-01; toolbar/button/ToolbarButton.tsx:48-52,78-87. ADR 08 §3: the state record is sealed, partial and [PartState];
// the default projection gives data-disabled, data-orientation and data-focusable (TLB-DOM-03).
/// <summary>Represents the state of a toolbar button, which the part exposes as data attributes and passes to its class and style functions.</summary>
/// <param name="Disabled">A value indicating whether the button ignores user interaction; projected as <c>data-disabled</c>.</param>
/// <param name="Orientation">The toolbar's orientation; projected as <c>data-orientation</c>.</param>
/// <param name="Focusable">A value indicating whether the button stays focusable while disabled; projected as <c>data-focusable</c>.</param>
[PartState]
public sealed partial record ToolbarButtonState(bool Disabled, Orientation Orientation, bool Focusable);

// TLB §3.2; toolbar/button/ToolbarButtonDataAttributes.ts:4,9,13, in its order.
/// <summary>Provides the names of the data attributes a toolbar button renders.</summary>
public static class ToolbarButtonDataAttributes
{
    /// <summary>Represents the attribute present, with an empty value, while the button is disabled: <c>data-disabled</c>.</summary>
    public const string Disabled = "data-disabled";

    /// <summary>Represents the attribute that holds the toolbar's orientation, <c>horizontal</c> or <c>vertical</c>: <c>data-orientation</c>.</summary>
    public const string Orientation = "data-orientation";

    /// <summary>Represents the attribute present, with an empty value, while the button stays focusable when disabled: <c>data-focusable</c>.</summary>
    public const string Focusable = "data-focusable";
}

// Base UI's Toolbar.Button (toolbar/button/ToolbarButton.tsx); the contract is docs/components/toolbar.md.
// - One `button` over the button mechanism (ButtonBehavior: native = NativeButton, disabled = effective, focusableWhenDisabled
//   = FocusableWhenDisabled) in composite mode always (ADR 04 Amendments 2026-10-26, chosen by the user), so the writer gives
//   TLB-DOM-04's rows and no tabindex (TLB-DOM-05). One ButtonTwin; TLB-EDGE-02 runs through ButtonDevelopmentLog.
// - Its effective disabled state is the toolbar's, its group's or its own (TLB-API-06, TLB-STA-05), read from the two
//   contexts (design point 14.4 (a), pending the user). While disabled the button mechanism declines the consumer's press
//   and key handlers (TLB-API-10), and the item runs none either.
// - It is a composite item of the toolbar (CompositeItemBehavior; TLB-STR-01): the item's tabindex="-1" is rendered
//   (CMP-STA-15) and the `composite` module owns it from attach (ADR 04 Amendments 2026-10-22). The button draws the id it
//   registers (the RadioRoot precedent).
// - `disabled` reaches a substitute only (TLB-API-09, TLB-EDGE-04; design point 14.2 (a), pending the user): while Render or
//   RenderFunction is set the part adds the effective disabled state to its own attributes, so an element substitute
//   receives the attribute and a component substitute (Toggle, SwitchRoot) the parameter. The default button carries
//   `disabled` only from the writer (FocusableWhenDisabled false), and stays hoverable otherwise (TLB-PTR-01).
// - One keydown gate (ADR 01 and ADR 04 Amendments 2026-10-26, chosen by the user; the Radio and Toggle precedent): the
//   composite's navigation keys take the item's gate (`navigate`), every other key the button's (`activate`).
/// <summary>
/// Represents a button in a toolbar: Base UI's <c>Toolbar.Button</c>, rendered as a <c>button</c> element that the
/// toolbar's arrow keys move focus to. Substitute another part, such as a <see cref="Toggle"/>, to make it a toolbar item.
/// </summary>
/// <remarks>
/// A disabled button stays focusable by default, with <c>aria-disabled="true"</c>, so it can still show a tooltip; set
/// <see cref="FocusableWhenDisabled"/> to <see langword="false"/> to disable it natively and remove it from the toolbar's
/// arrow-key order.
/// </remarks>
public sealed class ToolbarButton : PartBase<ToolbarButtonState>
{
    private const string DefaultTag = "button";
    private const string ArrowKeyPrefix = "Arrow";

    private readonly EventCallback onKeyDown;
    private readonly EventCallback onKeyUp;
    private readonly Action contextChanged;
    private ButtonBehavior? behavior;
    private CompositeItemBehavior? item;
    private ToolbarRootContext? toolbar;
    private ToolbarGroupContext? group;
    private ToolbarButtonState? state;

    /// <summary>Initializes a new instance of the <see cref="ToolbarButton"/> class.</summary>
    public ToolbarButton()
    {
        onKeyDown = CreateHandler<KeyboardEventArgs>("onkeydown", HandleKeyDownAsync);
        onKeyUp = CreateHandler<KeyboardEventArgs>("onkeyup", e => behavior!.HandleKeyUpAsync(e));
        contextChanged = OnContextChanged;
    }

    [Inject]
    private IJSRuntime JSRuntime { get; set; } = null!;

    [Inject]
    private ILogger<ToolbarButton> Logger { get; set; } = null!;

    [Inject]
    private ButtonDevelopmentLog DevelopmentLog { get; set; } = null!;

    [Inject]
    private PartIdGenerator IdGenerator { get; set; } = null!;

    // TLB-API-06
    private bool EffectiveDisabled => toolbar!.Disabled || (group?.Disabled ?? false) || Disabled;

    [CascadingParameter]
    private ToolbarRootContext? ToolbarContext { get; set; }

    [CascadingParameter]
    private ToolbarGroupContext? GroupContext { get; set; }

    [CascadingParameter]
    private CompositeRootContext? CompositeContext { get; set; }

    // TLB-API-06
    /// <summary>Gets or sets a value indicating whether the button ignores user interaction.</summary>
    /// <value>
    /// <see langword="true"/> to disable the button; the default is <see langword="false"/>. A disabled toolbar or group
    /// disables the button either way.
    /// </value>
    [Parameter]
    public bool Disabled { get; set; }

    // TLB-API-07
    /// <summary>Gets or sets a value indicating whether the button stays focusable, and a stop of the toolbar's arrow keys, while disabled.</summary>
    /// <value>
    /// <see langword="true"/>, the default, to keep a disabled button focusable with <c>aria-disabled="true"</c>;
    /// <see langword="false"/> to render a disabled native button with the <c>disabled</c> attribute and skip it.
    /// </value>
    [Parameter]
    public bool FocusableWhenDisabled { get; set; } = true;

    // TLB-API-08, UBT-API-03
    /// <summary>Gets or sets a value indicating whether the rendered element is a native <c>button</c>.</summary>
    /// <value>
    /// <see langword="true"/>, the default, for a <c>button</c> element, which gets <c>type="button"</c>;
    /// <see langword="false"/> when a substitute renders another element, which gets <c>role="button"</c>.
    /// </value>
    [Parameter]
    public bool NativeButton { get; set; } = true;

    /// <inheritdoc />
    protected override string DefaultTagName => DefaultTag;

    /// <inheritdoc />
    protected override ToolbarButtonState State => state!;

    /// <inheritdoc />
    protected override bool UsesGeneratedId => true;

    // For the fixtures (tests/Blazix.Primitives.Fixtures.Client): completes once the twin is attached, so a browser test
    // can wait for the module's listeners before it clicks or presses keys.
    /// <summary>Gets a task that completes when the button's behavior is attached to its element.</summary>
    /// <value>A task that completes after the first interactive render has attached the behavior.</value>
    internal Task TwinAttached => behavior!.TwinAttached;

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        // TLB-EDGE-01: the toolbar's error comes before the composite item's.
        toolbar = ToolbarRootContext.Require(ToolbarContext);
        behavior = new ButtonBehavior(JSRuntime, Logger, DevelopmentLog, IdGenerator, StateHasChanged, DispatchExceptionAsync, onKeyDown, onKeyUp);
        item = new CompositeItemBehavior(CompositeContext, "ToolbarButton", onKeyDown);
        base.OnInitialized();
        group = GroupContext;
        toolbar.AddListener(contextChanged);
        group?.AddListener(contextChanged);
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        behavior!.Disabled = EffectiveDisabled;
        behavior.Native = NativeButton;
        behavior.FocusableWhenDisabled = FocusableWhenDisabled;
        behavior.SetConsumerAttributes(AdditionalAttributes);
        // TLB-STA-03, M3 track 1's E3: the flags are the effective disabled state and FocusableWhenDisabled; this
        // registration is the element's when a substituted part registers it too (E4, the outer one wins).
        item!.SetRegistration(Id, AdditionalAttributes, EffectiveDisabled, FocusableWhenDisabled);

        // TLB-API-10: a disabled button's key handlers are not invoked, so the root's key handling runs none either.
        item.SetConsumerAttributes(EffectiveDisabled ? null : AdditionalAttributes);
        UpdateState();
    }

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        await behavior!.AfterRenderAsync(Enabled, Element, Id, composite: true);
        await item!.AfterRenderAsync(Element);
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            behavior?.Dispose();
            item?.Dispose();
            toolbar?.RemoveListener(contextChanged);
            group?.RemoveListener(contextChanged);
        }

        base.Dispose(disposing);
    }

    // TLB-API-09; the button writer's type or role, disabled and aria-disabled (TLB-DOM-04), then the item's tabindex.
    /// <inheritdoc />
    protected override void AddPartAttributes(AttributeList attributes)
    {
        if (Render is not null || RenderFunction is not null)
        {
            attributes.Set("disabled", state!.Disabled);
        }

        behavior!.AddAttributes(attributes, Id, composite: true);

        // CMP-STA-15: the item's tabindex replaces nothing the button writer rendered in composite mode (UBT-DOM-05), and
        // the consumer's still wins at the merge.
        item!.AddAttributes(attributes);
    }

    /// <inheritdoc />
    protected override bool ForwardsConsumerHandler(string eventName)
    {
        return behavior!.ForwardsConsumerHandler(eventName) && item!.ForwardsConsumerHandler(eventName);
    }

    // The one keydown gate: the key reaches the gate whose decision point may have run the consumer's handler for it.
    private Task HandleKeyDownAsync(KeyboardEventArgs e)
    {
        return IsItemKey(e.Key) ? item!.HandleKeyDownAsync(e) : behavior!.HandleKeyDownAsync(e);
    }

    // A change of the toolbar's or the group's state: render only when this button's state changed.
    private void OnContextChanged()
    {
        bool disabled = EffectiveDisabled;
        if (behavior!.Disabled != disabled)
        {
            // TLB-STA-06: the new flags are sent after the render this change causes.
            behavior.Disabled = disabled;
            item!.SetRegistration(Id, AdditionalAttributes, disabled, FocusableWhenDisabled);
            item.SetConsumerAttributes(disabled ? null : AdditionalAttributes);
        }

        if (UpdateState())
        {
            StateHasChanged();
        }
    }

    // Returns whether the state changed; a new record is made only then (ADR 16 rule 7).
    private bool UpdateState()
    {
        bool disabled = EffectiveDisabled;
        Orientation orientation = toolbar!.Orientation;
        if (state is { } current && current.Disabled == disabled && current.Orientation == orientation && current.Focusable == FocusableWhenDisabled)
        {
            return false;
        }

        state = new ToolbarButtonState(disabled, orientation, FocusableWhenDisabled);
        return true;
    }

    // The composite's navigation keys: the arrows, Home and End (design point 12.3 (a) of M3 tracks 2-3, settled by the
    // orchestrator 2026-10-26); the toolbar handles no Home or End, so they leave no marker in either gate.
    private static bool IsItemKey(string? key)
    {
        return key is not null
            && (key.StartsWith(ArrowKeyPrefix, StringComparison.Ordinal)
                || string.Equals(key, "Home", StringComparison.Ordinal)
                || string.Equals(key, "End", StringComparison.Ordinal));
    }
}

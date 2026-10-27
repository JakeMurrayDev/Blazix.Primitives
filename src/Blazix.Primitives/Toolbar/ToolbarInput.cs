using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace Blazix.Primitives;

// TLB-STA-01; toolbar/input/ToolbarInput.tsx:47-51,78-87. ADR 08 §3: the state record is sealed, partial and [PartState];
// the default projection gives data-disabled, data-orientation and data-focusable (TLB-DOM-07).
/// <summary>Represents the state of a toolbar input, which the part exposes as data attributes and passes to its class and style functions.</summary>
/// <param name="Disabled">A value indicating whether the input ignores user interaction; projected as <c>data-disabled</c>.</param>
/// <param name="Orientation">The toolbar's orientation; projected as <c>data-orientation</c>.</param>
/// <param name="Focusable">A value indicating whether the input stays focusable while disabled; projected as <c>data-focusable</c>.</param>
[PartState]
public sealed partial record ToolbarInputState(bool Disabled, Orientation Orientation, bool Focusable);

// TLB §3.4; toolbar/input/ToolbarInputDataAttributes.ts:4,9,13, in its order.
/// <summary>Provides the names of the data attributes a toolbar input renders.</summary>
public static class ToolbarInputDataAttributes
{
    /// <summary>Represents the attribute present, with an empty value, while the input is disabled: <c>data-disabled</c>.</summary>
    public const string Disabled = "data-disabled";

    /// <summary>Represents the attribute that holds the toolbar's orientation, <c>horizontal</c> or <c>vertical</c>: <c>data-orientation</c>.</summary>
    public const string Orientation = "data-orientation";

    /// <summary>Represents the attribute present, with an empty value, while the input stays focusable when disabled: <c>data-focusable</c>.</summary>
    public const string Focusable = "data-focusable";
}

// Base UI's Toolbar.Input (toolbar/input/ToolbarInput.tsx); the contract is docs/components/toolbar.md.
// - One `input`, disabled through ARIA only: aria-disabled="true" while disabled and never the `disabled` attribute, so a
//   disabled input stays focusable, navigable and a live form control (TLB-API-12, TLB-DOM-07, TLB-ARIA-04, TLB-EDGE-10).
//   The attribute is ButtonAttributeWriter's input item variant, written over the consumer's in FinishAttributes, as Base
//   UI merges it after the consumer's props (TLB-DOM-11; §16 Q5, chosen by the user 2026-10-26). No type or role is added.
// - Its effective disabled state is the toolbar's, its group's or its own (TLB-API-12, TLB-STA-05).
// - The button mechanism in its input item mode (UBT-DOM-12; ADR 04 Amendments 2026-10-26, chosen by the user): the
//   `button` module's inputItem option keeps a disabled, focusable input's non-Tab key prevention and its click and
//   pointerdown prevention (TLB-KEY-13, TLB-PTR-02) and never activates it, and the part declines none of the consumer's
//   handlers, which run after the module's (TLB-API-14; design point 14.5 (a), pending the user).
// - It is a composite item of the toolbar (CompositeItemBehavior; TLB-STR-01) with the item gate for the consumer's
//   onkeydown; the item's tabindex="-1" is rendered (CMP-STA-15) and the `composite` module owns it from attach (ADR 04
//   Amendments 2026-10-22). The caret and select-on-focus rules are the module's (TLB-KEY-09/10/11, TLB-FOC-05).
// - DefaultValue is rendered once, as the `value` attribute of the first render (plan of 2026-10-26, chosen by the user;
//   ADR 04 Amendments 2026-10-22, the field half's evidence): Blazor never writes the element's value afterwards.
// - A substituted Input (the port's) is supported for its attributes only: its own key gate and the item's would both own
//   one onkeydown (design point 14.3 (a), pending the user).
/// <summary>
/// Represents a native text input in a toolbar: Base UI's <c>Toolbar.Input</c>, rendered as an <c>input</c> element that
/// the toolbar's arrow keys move focus to once the caret is at either end of its text.
/// </summary>
/// <remarks>
/// A disabled input renders <c>aria-disabled="true"</c> instead of the <c>disabled</c> attribute: it stays focusable,
/// ignores pointer presses and keys other than Tab, and is still submitted with its form. Substituting an
/// <see cref="Input"/> is supported for its attributes; its key handling is the toolbar's.
/// </remarks>
public sealed class ToolbarInput : PartBase<ToolbarInputState>
{
    private const string DefaultTag = "input";

    private readonly EventCallback onKeyDown;
    private readonly Action contextChanged;
    private ButtonBehavior? behavior;
    private CompositeItemBehavior? item;
    private ToolbarRootContext? toolbar;
    private ToolbarGroupContext? group;
    private ToolbarInputState? state;
    private string? initialValue;
    private bool initialValueRead;

    /// <summary>Initializes a new instance of the <see cref="ToolbarInput"/> class.</summary>
    public ToolbarInput()
    {
        onKeyDown = CreateHandler<KeyboardEventArgs>("onkeydown", e => item!.HandleKeyDownAsync(e));
        contextChanged = OnContextChanged;
    }

    [Inject]
    private IJSRuntime JSRuntime { get; set; } = null!;

    [Inject]
    private ILogger<ToolbarInput> Logger { get; set; } = null!;

    [Inject]
    private ButtonDevelopmentLog DevelopmentLog { get; set; } = null!;

    [Inject]
    private PartIdGenerator IdGenerator { get; set; } = null!;

    // TLB-API-12
    private bool EffectiveDisabled => toolbar!.Disabled || (group?.Disabled ?? false) || Disabled;

    [CascadingParameter]
    private ToolbarRootContext? ToolbarContext { get; set; }

    [CascadingParameter]
    private ToolbarGroupContext? GroupContext { get; set; }

    [CascadingParameter]
    private CompositeRootContext? CompositeContext { get; set; }

    // TLB-API-12
    /// <summary>Gets or sets a value indicating whether the input ignores user interaction.</summary>
    /// <value>
    /// <see langword="true"/> to disable the input through <c>aria-disabled</c>; the default is <see langword="false"/>. A
    /// disabled toolbar or group disables the input either way.
    /// </value>
    [Parameter]
    public bool Disabled { get; set; }

    // TLB-API-13
    /// <summary>Gets or sets a value indicating whether the input stays a stop of the toolbar's arrow keys while disabled.</summary>
    /// <value>
    /// <see langword="true"/>, the default, to keep a disabled input focusable and in the arrow-key order;
    /// <see langword="false"/> to skip it.
    /// </value>
    [Parameter]
    public bool FocusableWhenDisabled { get; set; } = true;

    // TLB-API-14
    /// <summary>Gets or sets the input's initial value.</summary>
    /// <value>The text the input starts with, read at the first render only; <see langword="null"/>, the default, for none.</value>
    [Parameter]
    public string? DefaultValue { get; set; }

    /// <inheritdoc />
    protected override string DefaultTagName => DefaultTag;

    /// <inheritdoc />
    protected override ToolbarInputState State => state!;

    /// <inheritdoc />
    protected override bool UsesGeneratedId => true;

    // For the fixtures (tests/Blazix.Primitives.Fixtures.Client): completes once the twin is attached, so a browser test
    // can wait for the module's listeners before it clicks or presses keys.
    /// <summary>Gets a task that completes when the input's behavior is attached to its element.</summary>
    /// <value>A task that completes after the first interactive render has attached the behavior.</value>
    internal Task TwinAttached => behavior!.TwinAttached;

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        // TLB-EDGE-01: the toolbar's error comes before the composite item's.
        toolbar = ToolbarRootContext.Require(ToolbarContext);

        // The input item mode runs no decision and no consumer key handler of its own, so its key handlers are unset.
        behavior = new ButtonBehavior(JSRuntime, Logger, DevelopmentLog, IdGenerator, StateHasChanged, DispatchExceptionAsync, default, default)
        {
            InputItem = true,
            Native = false,
        };
        item = new CompositeItemBehavior(CompositeContext, "ToolbarInput", onKeyDown);
        base.OnInitialized();
        group = GroupContext;
        toolbar.AddListener(contextChanged);
        group?.AddListener(contextChanged);
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        if (!initialValueRead)
        {
            initialValue = DefaultValue;
            initialValueRead = true;
        }

        behavior!.Disabled = EffectiveDisabled;
        behavior.FocusableWhenDisabled = FocusableWhenDisabled;
        item!.SetRegistration(Id, AdditionalAttributes);
        item.SetConsumerAttributes(AdditionalAttributes);
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

    // CMP-STA-15: the item's tabindex="-1" and its key handler; the first render's value, so Blazor never writes it again.
    /// <inheritdoc />
    protected override void AddPartAttributes(AttributeList attributes)
    {
        if (initialValue is not null)
        {
            attributes.Set("value", initialValue);
        }

        item!.AddAttributes(attributes);
    }

    // TLB-DOM-11: Base UI merges aria-disabled after the consumer's props, so it wins while the input is disabled.
    /// <inheritdoc />
    protected override void FinishAttributes(AttributeList attributes) => ButtonAttributeWriter.WriteInputItem(attributes, state!.Disabled);

    /// <inheritdoc />
    protected override bool ForwardsConsumerHandler(string eventName)
    {
        return behavior!.ForwardsConsumerHandler(eventName) && item!.ForwardsConsumerHandler(eventName);
    }

    // A change of the toolbar's or the group's state: render only when this input's state changed.
    private void OnContextChanged()
    {
        behavior!.Disabled = EffectiveDisabled;
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

        state = new ToolbarInputState(disabled, orientation, FocusableWhenDisabled);
        return true;
    }
}

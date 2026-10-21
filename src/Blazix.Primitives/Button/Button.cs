using Blazix.Primitives.Interop;
using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace Blazix.Primitives;

// BTN-STA-01; button/Button.tsx:44-49. ADR 08 §3: the state record is sealed, partial and [PartState].
/// <summary>Represents the state of a button, which the part exposes as data attributes and passes to its class and style functions.</summary>
/// <param name="Disabled">A value indicating whether the button ignores user interaction; projected as <c>data-disabled</c>.</param>
[PartState]
public sealed partial record ButtonState(bool Disabled);

// BTN-DOM-01; button/ButtonDataAttributes.ts.
/// <summary>Provides the names of the data attributes a button renders.</summary>
public static class ButtonDataAttributes
{
    /// <summary>Represents the attribute present, with an empty value, while the button is disabled: <c>data-disabled</c>.</summary>
    public const string Disabled = "data-disabled";
}

// Base UI's Button (button/Button.tsx); the contract is docs/components/button.md, the mechanism
// docs/architecture/button-behavior.md (UBT). The mechanism as a part uses it (the attributes of UBT-ALG-03 through
// ButtonAttributeWriter, the gate of the consumer's handlers, the activation decision point and one ButtonTwin per part,
// ADR 04 §1 and §3) is ButtonBehavior, shared with CollapsibleTrigger.
/// <summary>
/// Represents a button that runs an action: Base UI's <c>Button</c>, rendered as a <c>button</c>. It can be replaced by
/// another element through <see cref="PartBase{TState}.Render"/> while keeping button semantics, and can stay focusable
/// while disabled.
/// </summary>
public sealed class Button : PartBase<ButtonState>
{
    private const string DefaultTag = "button";

    private static readonly ButtonState s_enabled = new(Disabled: false);
    private static readonly ButtonState s_disabled = new(Disabled: true);

    private readonly EventCallback onKeyDown;
    private readonly EventCallback onKeyUp;
    private ButtonBehavior? behavior;

    /// <summary>Initializes a new instance of the <see cref="Button"/> class.</summary>
    public Button()
    {
        onKeyDown = CreateHandler<KeyboardEventArgs>("onkeydown", e => behavior!.HandleKeyDownAsync(e));
        onKeyUp = CreateHandler<KeyboardEventArgs>("onkeyup", e => behavior!.HandleKeyUpAsync(e));
    }

    [Inject]
    private IJSRuntime JSRuntime { get; set; } = null!;

    [Inject]
    private ILogger<Button> Logger { get; set; } = null!;

    [Inject]
    private ButtonDevelopmentLog DevelopmentLog { get; set; } = null!;

    [Inject]
    private PartIdGenerator IdGenerator { get; set; } = null!;

    // BTN-API-01
    /// <summary>Gets or sets a value indicating whether the button ignores user interaction.</summary>
    /// <value>
    /// <see langword="true"/> to ignore clicks, presses and keyboard activation, which also withholds the consumer's
    /// <c>onclick</c>, <c>onmousedown</c>, <c>onpointerdown</c>, <c>onkeydown</c> and <c>onkeyup</c> handlers; otherwise,
    /// <see langword="false"/>. The default is <see langword="false"/>.
    /// </value>
    [Parameter]
    public bool Disabled { get; set; }

    // BTN-API-02
    /// <summary>Gets or sets a value indicating whether the button stays focusable and in the tab order while disabled.</summary>
    /// <value>
    /// <see langword="true"/> to keep a disabled button focusable, announced with <c>aria-disabled</c> instead of the
    /// <c>disabled</c> attribute; otherwise, <see langword="false"/>. The default is <see langword="false"/>.
    /// </value>
    [Parameter]
    public bool FocusableWhenDisabled { get; set; }

    // BTN-API-03
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
    protected override ButtonState State => Disabled ? s_disabled : s_enabled;

    // ADR 02 decision 1: under a substitute callback the twin finds the element by id.
    /// <inheritdoc />
    protected override bool UsesGeneratedId => RenderFunction is not null;

    // For the fixtures (tests/Blazix.Primitives.Fixtures.Client): completes once the twin is attached, so a browser test
    // can wait for the module's listeners before it presses keys.
    /// <summary>Gets a task that completes when the button's behavior is attached to its element.</summary>
    /// <value>A task that completes after the first interactive render has attached the behavior.</value>
    internal Task TwinAttached => behavior!.TwinAttached;

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        behavior = new ButtonBehavior(JSRuntime, Logger, DevelopmentLog, IdGenerator, StateHasChanged, DispatchExceptionAsync, onKeyDown, onKeyUp);
        base.OnInitialized();
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        // BTN-API-02: focusableWhenDisabled always explicit; BTN-EDGE-02: composite inference is M2's composite module.
        behavior!.Disabled = Disabled;
        behavior.FocusableWhenDisabled = FocusableWhenDisabled;
        behavior.Native = NativeButton;
        behavior.SetConsumerAttributes(AdditionalAttributes);
    }

    /// <inheritdoc />
    protected override Task OnAfterRenderAsync(bool firstRender)
    {
        return behavior!.AfterRenderAsync(Enabled, Element, Id, composite: false);
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

    /// <inheritdoc />
    protected override void AddPartAttributes(AttributeList attributes)
    {
        behavior!.AddAttributes(attributes, Id, composite: false);
    }

    /// <inheritdoc />
    protected override bool ForwardsConsumerHandler(string eventName)
    {
        return behavior!.ForwardsConsumerHandler(eventName);
    }
}

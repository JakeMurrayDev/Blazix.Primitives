using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace Blazix.Primitives;

// MNU §3.19 state object (menu/submenu-trigger/MenuSubmenuTrigger.tsx:226-230). Highlighted is the parent list's mirror
// (ADR 04 Amendments 2026-11-03 (2)).
/// <summary>Represents the state of a menu submenu trigger, which the part exposes as data attributes and passes to its class and style functions.</summary>
/// <param name="Disabled">A value indicating whether the trigger ignores user interaction; projected as <c>data-disabled</c>.</param>
/// <param name="Highlighted">
/// A value indicating whether the trigger is highlighted in its parent menu; its <c>data-highlighted</c> attribute is
/// written by the browser-side behavior.
/// </param>
/// <param name="Open">A value indicating whether the submenu is open; projected as <c>data-popup-open</c>.</param>
[PartState]
public sealed partial record MenuSubmenuTriggerState(bool Disabled, [property: MirroredField] bool Highlighted, bool Open);

// menu/submenu-trigger/MenuSubmenuTriggerDataAttributes.ts (MNU-DOM-18).
/// <summary>Provides the names of the data attributes a menu submenu trigger renders.</summary>
public static class MenuSubmenuTriggerDataAttributes
{
    /// <summary>Represents the attribute present while the submenu is open: <c>data-popup-open</c>.</summary>
    public const string PopupOpen = PopupTriggerDataAttributes.PopupOpen;

    /// <summary>Represents the attribute present while the trigger is highlighted: <c>data-highlighted</c>.</summary>
    public const string Highlighted = "data-highlighted";

    /// <summary>Represents the attribute present while the trigger is disabled: <c>data-disabled</c>.</summary>
    public const string Disabled = "data-disabled";
}

// Base UI's Menu.SubmenuTrigger (menu/submenu-trigger/MenuSubmenuTrigger.tsx); the contract is docs/components/menu.md §3.19
// (MNU-API-50..53, DOM-18, ARIA-13, A11Y-01, A11Y-03). A thin shell over MenuSubmenuTriggerBehavior.
/// <summary>
/// Represents a menu item that opens a submenu, Base UI's <c>Menu.SubmenuTrigger</c>, rendered as a <c>div</c>.
/// </summary>
/// <remarks>Place it inside a <see cref="MenuSubmenuRoot"/>, beside the submenu's portal.</remarks>
public sealed class MenuSubmenuTrigger : PartBase<MenuSubmenuTriggerState>
{
    private static readonly StateAttributesMapping<MenuSubmenuTriggerState> s_mapping = PopupStateMapping.CreateTrigger<MenuSubmenuTriggerState>();

    private readonly EventCallback onClick;
    private readonly EventCallback onMouseDown;
    private readonly EventCallback onKeyDown;
    private readonly EventCallback onKeyUp;
    private MenuSubmenuTriggerBehavior? behavior;
    private MenuSubmenuTriggerState state = new(false, false, false);

    /// <summary>Initializes a new instance of the <see cref="MenuSubmenuTrigger"/> class.</summary>
    public MenuSubmenuTrigger()
    {
        onClick = CreateHandler<MouseEventArgs>("onclick", e => behavior!.HandleClickAsync(e));
        onMouseDown = CreateHandler<MouseEventArgs>("onmousedown", e => behavior!.HandleMouseDownAsync(e));
        onKeyDown = CreateHandler<KeyboardEventArgs>("onkeydown", e => behavior!.HandleKeyDownAsync(e));
        onKeyUp = CreateHandler<KeyboardEventArgs>("onkeyup", e => behavior!.HandleKeyUpAsync(e));
    }

    [Inject]
    private IJSRuntime JSRuntime { get; set; } = null!;

    [Inject]
    private ILogger<MenuSubmenuTrigger> Logger { get; set; } = null!;

    [Inject]
    private ButtonDevelopmentLog DevelopmentLog { get; set; } = null!;

    [Inject]
    private PartIdGenerator IdGenerator { get; set; } = null!;

    [Inject]
    private PlatformFlags Platform { get; set; } = null!;

    [CascadingParameter]
    private MenuRootContext? RootContext { get; set; }

    [CascadingParameter]
    private CompositeRootContext? ParentList { get; set; }

    // MNU-API-50
    /// <summary>Gets or sets a value indicating whether hovering the trigger opens the submenu.</summary>
    /// <value><see langword="true"/> by default.</value>
    [Parameter]
    public bool OpenOnHover { get; set; } = true;

    // MNU-API-51
    /// <summary>Gets or sets how long to wait before a hover opens the submenu.</summary>
    /// <value>A duration; <see langword="null"/>, the default, means 100 milliseconds. Needs <see cref="OpenOnHover"/>.</value>
    [Parameter]
    public Duration? Delay { get; set; }

    // MNU-API-51
    /// <summary>Gets or sets how long to wait before closing a submenu that a hover opened.</summary>
    /// <value>A duration; <see langword="null"/>, the default, means no delay. Needs <see cref="OpenOnHover"/>.</value>
    [Parameter]
    public Duration? CloseDelay { get; set; }

    // MNU-API-52
    /// <summary>Gets or sets a value indicating whether the trigger ignores user interaction.</summary>
    /// <value><see langword="false"/> by default; a disabled trigger stays focusable.</value>
    [Parameter]
    public bool Disabled { get; set; }

    /// <summary>Gets or sets the text the parent menu's typeahead matches the trigger by.</summary>
    /// <value>The label, or <see langword="null"/>, the default, for the trigger's text content.</value>
    [Parameter]
    public string? Label { get; set; }

    /// <summary>Gets or sets a value indicating whether the rendered element is a native <c>button</c>.</summary>
    /// <value><see langword="false"/> by default; set it when <see cref="PartBase{TState}.Render"/> supplies a <c>button</c>.</value>
    [Parameter]
    public bool NativeButton { get; set; }

    /// <inheritdoc />
    protected override string DefaultTagName => "div";

    /// <inheritdoc />
    protected override MenuSubmenuTriggerState State
    {
        get
        {
            MenuSubmenuTriggerBehavior current = behavior!;
            if (state.Disabled != current.Disabled || state.Highlighted != current.Highlighted || state.Open != current.Open)
            {
                state = new MenuSubmenuTriggerState(current.Disabled, current.Highlighted, current.Open);
            }

            return state;
        }
    }

    /// <inheritdoc />
    protected override StateAttributesMapping<MenuSubmenuTriggerState>? StateAttributesMapping => s_mapping;

    /// <inheritdoc />
    protected override bool UsesGeneratedId => true;

    /// <summary>Gets a task that completes when the trigger's handles are attached.</summary>
    internal Task TwinsAttached => behavior?.TwinsAttached ?? Task.CompletedTask;

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        behavior = new MenuSubmenuTriggerBehavior(this, "Menu", JSRuntime, Logger, DevelopmentLog, IdGenerator, Platform, StateHasChanged, InvokeAsync, DispatchExceptionAsync, onClick, onMouseDown, onKeyDown, onKeyUp);
        behavior.Subscribe(RootContext, ParentList);
        base.OnInitialized();
    }

    /// <inheritdoc />
    protected override void OnParametersSet() =>
        behavior!.Apply(Id, Element, new MenuSubmenuTriggerParameters(OpenOnHover, Delay, CloseDelay, new MenuItemParameters(Disabled, Label, NativeButton, CloseOnClick: false)), AdditionalAttributes);

    /// <inheritdoc />
    protected override Task OnAfterRenderAsync(bool firstRender) => behavior!.OnAfterRenderAsync(Enabled, Element, Id);

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
    protected override void AddPartAttributes(AttributeList attributes) => behavior!.AddAttributes(attributes, Id);

    /// <inheritdoc />
    protected override bool ForwardsConsumerHandler(string eventName) => behavior!.ForwardsConsumerHandler(eventName);
}

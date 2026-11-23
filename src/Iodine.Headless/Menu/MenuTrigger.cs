using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace Blazix.Primitives;

// MNU §3.2 state object (menu/trigger/MenuTrigger.tsx:296-299).
/// <summary>Represents the state of a menu trigger, which the part exposes as data attributes and passes to its class and style functions.</summary>
/// <param name="Disabled">A value indicating whether the trigger ignores user interaction; projected as <c>data-disabled</c>.</param>
/// <param name="Open">
/// A value indicating whether the menu is open and this trigger owns it; projected as <c>data-popup-open</c> and
/// <c>data-pressed</c>.
/// </param>
[PartState]
public sealed partial record MenuTriggerState(bool Disabled, bool Open);

// menu/trigger/MenuTriggerDataAttributes.ts (MNU-DOM-01).
/// <summary>Provides the names of the data attributes a menu trigger renders.</summary>
public static class MenuTriggerDataAttributes
{
    /// <summary>Represents the attribute present while the menu is open and this trigger owns it: <c>data-popup-open</c>.</summary>
    public const string PopupOpen = PopupTriggerDataAttributes.PopupOpen;

    /// <summary>Represents the attribute present while the menu is open and this trigger owns it: <c>data-pressed</c>.</summary>
    public const string Pressed = PopupTriggerDataAttributes.Pressed;

    /// <summary>Represents the attribute present while the trigger is disabled: <c>data-disabled</c>.</summary>
    public const string Disabled = "data-disabled";
}

// Base UI's Menu.Trigger (menu/trigger/MenuTrigger.tsx); the contract is docs/components/menu.md §3.2, §5.1, §6.1, §6.2. A
// thin shell over MenuTriggerBehavior (decision 12 of the M5 plan), generic with the Root (ADR 08 Amendments 2026-10-27).
/// <summary>Represents a button that opens a menu, Base UI's <c>Menu.Trigger</c>, rendered as a <c>button</c>.</summary>
/// <typeparam name="TPayload">The type of the payload this trigger carries; inferred from its root or <see cref="Handle"/>.</typeparam>
/// <remarks>
/// A trigger inside a <see cref="MenuRoot{TPayload}"/> belongs to it; a trigger with a <see cref="Handle"/> belongs to the
/// root that uses the same handle, wherever it is rendered. The trigger's <c>id</c>, given or generated, is the id
/// <see cref="MenuRoot{TPayload}.TriggerId"/> and <see cref="PopupHandle{TPayload}.OpenAsync"/> name.
/// </remarks>
[CascadingTypeParameter(nameof(TPayload))]
public sealed class MenuTrigger<TPayload> : PartBase<MenuTriggerState>
{
    private static readonly StateAttributesMapping<MenuTriggerState> s_mapping = PopupStateMapping.CreatePressableTrigger<MenuTriggerState>();

    private readonly EventCallback onClick;
    private readonly EventCallback onMouseDown;
    private readonly EventCallback onKeyDown;
    private readonly EventCallback onKeyUp;
    private MenuTriggerBehavior<TPayload>? behavior;
    private MenuTriggerState state = new(false, false);
    private bool payloadSupplied;

    /// <summary>Initializes a new instance of the <see cref="MenuTrigger{TPayload}"/> class.</summary>
    public MenuTrigger()
    {
        onClick = CreateHandler<MouseEventArgs>("onclick", e => behavior!.HandleClickAsync(e));
        onMouseDown = CreateHandler<MouseEventArgs>("onmousedown", e => behavior!.HandleMouseDownAsync(e));
        onKeyDown = CreateHandler<KeyboardEventArgs>("onkeydown", e => behavior!.HandleKeyDownAsync(e));
        onKeyUp = CreateHandler<KeyboardEventArgs>("onkeyup", e => behavior!.HandleKeyUpAsync(e));
    }

    [Inject]
    private IJSRuntime JSRuntime { get; set; } = null!;

    [Inject]
    private ILogger<MenuTrigger<TPayload>> Logger { get; set; } = null!;

    [Inject]
    private ButtonDevelopmentLog DevelopmentLog { get; set; } = null!;

    [Inject]
    private PartIdGenerator IdGenerator { get; set; } = null!;

    [CascadingParameter]
    private MenuStore<TPayload>? RootStore { get; set; }

    [CascadingParameter]
    private IMenuParentContext? ParentContext { get; set; }

    [CascadingParameter]
    private CompositeRootContext? BarRoot { get; set; }

    // MNU-API-17
    /// <summary>Gets or sets a value indicating whether the rendered element is a native <c>button</c>.</summary>
    /// <value>
    /// <see langword="true"/>, the default, for a <c>button</c> element; <see langword="false"/> when
    /// <see cref="PartBase{TState}.Render"/> supplies another element, which then gets <c>role="button"</c>, a
    /// <c>tabindex</c>, and clicks from Enter and Space.
    /// </value>
    [Parameter]
    public bool NativeButton { get; set; } = true;

    // MNU-API-16
    /// <summary>Gets or sets a value indicating whether the trigger ignores user interaction.</summary>
    /// <value><see langword="false"/> by default.</value>
    [Parameter]
    public bool Disabled { get; set; }

    // MNU-API-22
    /// <summary>Gets or sets the handle that associates this trigger with the root that uses the same handle.</summary>
    /// <value>A <see cref="MenuHandle{TPayload}"/>, or <see langword="null"/> for a trigger inside its root.</value>
    [Parameter]
    public MenuHandle<TPayload>? Handle { get; set; }

    // MNU-API-23
    /// <summary>Gets or sets the payload the menu receives while this trigger is active.</summary>
    /// <value>The payload, which the root's <c>PayloadContent</c> receives; when the parameter is not supplied, none.</value>
    [Parameter]
    public TPayload? Payload { get; set; }

    // MNU-API-19
    /// <summary>Gets or sets a value indicating whether hovering the trigger opens the menu.</summary>
    /// <value>
    /// <see langword="null"/>, the default, for <see langword="false"/>, or inside a menubar for whether the menubar has a
    /// menu open; <see langword="true"/> or <see langword="false"/> to choose.
    /// </value>
    [Parameter]
    public bool? OpenOnHover { get; set; }

    // MNU-API-20
    /// <summary>Gets or sets how long the pointer must rest on the trigger before a hover opens the menu.</summary>
    /// <value>A duration; <see langword="null"/>, the default, means 100 milliseconds. Needs <see cref="OpenOnHover"/>.</value>
    [Parameter]
    public Duration? Delay { get; set; }

    // MNU-API-21
    /// <summary>Gets or sets how long to wait before closing a menu that a hover opened.</summary>
    /// <value>A duration; <see langword="null"/>, the default, means no delay. Needs <see cref="OpenOnHover"/>.</value>
    [Parameter]
    public Duration? CloseDelay { get; set; }

    /// <inheritdoc />
    protected override string DefaultTagName => "button";

    // The pipeline reads the state before the part's attributes, so the record follows the store here.
    /// <inheritdoc />
    protected override MenuTriggerState State
    {
        get
        {
            bool disabled = behavior!.Disabled;
            bool open = behavior.Open;
            if (state.Open != open || state.Disabled != disabled)
            {
                state = new MenuTriggerState(disabled, open);
            }

            return state;
        }
    }

    /// <inheritdoc />
    protected override StateAttributesMapping<MenuTriggerState>? StateAttributesMapping => s_mapping;

    /// <inheritdoc />
    protected override bool UsesGeneratedId => true;

    /// <summary>Gets a task that completes when the trigger's behaviors are attached.</summary>
    internal Task TwinsAttached => behavior?.TwinsAttached ?? Task.CompletedTask;

    /// <summary>Gets a task that completes when a filterable menu's trigger has its <c>menu-filter</c> handle.</summary>
    internal Task FilterAttached => behavior?.FilterAttached ?? Task.CompletedTask;

    /// <inheritdoc />
    public override Task SetParametersAsync(ParameterView parameters)
    {
        payloadSupplied = parameters.TryGetValue<TPayload>(nameof(Payload), out _);
        return base.SetParametersAsync(parameters);
    }

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        behavior = new MenuTriggerBehavior<TPayload>(this, "Menu", JSRuntime, Logger, DevelopmentLog, IdGenerator, StateHasChanged, InvokeAsync, DispatchExceptionAsync, onClick, onMouseDown, onKeyDown, onKeyUp);
        base.OnInitialized();
    }

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">The trigger is neither inside a <see cref="MenuRoot{TPayload}"/> nor given a <see cref="Handle"/>.</exception>
    protected override void OnParametersSet() =>
        behavior!.Apply(RootStore, Handle, ParentContext, BarRoot, Id, Element, payloadSupplied, Payload, new MenuTriggerParameters(NativeButton, Disabled, OpenOnHover, Delay, CloseDelay), AdditionalAttributes);

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

    /// <inheritdoc />
    protected override void BuildBeforeElement(RenderTreeBuilder builder) => behavior!.BuildBeforeElement(builder);

    /// <inheritdoc />
    protected override void BuildAfterElement(RenderTreeBuilder builder) => behavior!.BuildAfterElement(builder);
}

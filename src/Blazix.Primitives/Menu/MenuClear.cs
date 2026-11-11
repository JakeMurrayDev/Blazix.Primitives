using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace Blazix.Primitives;

// MNU §3.23 state object (filter-dropdown/clear/FilterDropdownClear.tsx; MNU-API-60).
/// <summary>Represents the state of a menu's clear button, which the part exposes as data attributes and passes to its class and style functions.</summary>
/// <param name="Disabled">A value indicating whether the button ignores user interaction; projected as <c>data-disabled</c>.</param>
[PartState]
public sealed partial record MenuClearState(bool Disabled);

// menu/clear/MenuClearDataAttributes.ts (MNU-DOM-22).
/// <summary>Provides the names of the data attributes a menu's clear button renders.</summary>
public static class MenuClearDataAttributes
{
    /// <summary>Represents the attribute present while the button is disabled: <c>data-disabled</c>.</summary>
    public const string Disabled = "data-disabled";
}

// Base UI's Menu.Clear (menu/clear/MenuClear.tsx, filter-dropdown/clear/FilterDropdownClear.tsx); the contract is
// docs/components/menu.md §3.23 (MNU-API-60, DOM-22, ARIA-16, EVT-17, PTR-20, PTR-21; UBT-PTR-01).
// - ButtonBehavior { Native = NativeButton, Disabled = root's || part's, FocusableWhenDisabled = false } (useButton).
// - It renders only while the query is not empty (FilterDropdownClear.tsx:31-33, enabled: visible), with tabindex -1 and
//   aria-hidden; the state carries no visible flag, as in Base UI.
// - A C# onclick requests '' with clear-press and the click's snapshot through the provider (FilterDropdownClear.tsx:53-58).
//   The mousedown default and the input's refocus are the browser's half (L4).
/// <summary>
/// Represents the button that clears a menu's filter query, Base UI's <c>Menu.Clear</c>, rendered as a <c>button</c> only
/// while the query is not empty.
/// </summary>
/// <remarks>It must be placed in a menu wrapped in <see cref="MenuFilterProvider"/>.</remarks>
public sealed class MenuClear : PartBase<MenuClearState>
{
    private readonly EventCallback onClick;
    private readonly EventCallback onKeyDown;
    private readonly EventCallback onKeyUp;
    private ButtonBehavior? button;
    private MenuFilterRootContext? filter;
    private IDisposable? filterSubscription;
    private MenuClearState state = new(false);

    /// <summary>Initializes a new instance of the <see cref="MenuClear"/> class.</summary>
    public MenuClear()
    {
        onClick = CreateHandler<MouseEventArgs>("onclick", HandleClickAsync);
        onKeyDown = CreateHandler<KeyboardEventArgs>("onkeydown", e => button!.HandleKeyDownAsync(e));
        onKeyUp = CreateHandler<KeyboardEventArgs>("onkeyup", e => button!.HandleKeyUpAsync(e));
    }

    [Inject]
    private IJSRuntime JSRuntime { get; set; } = null!;

    [Inject]
    private ILogger<MenuClear> Logger { get; set; } = null!;

    [Inject]
    private ButtonDevelopmentLog DevelopmentLog { get; set; } = null!;

    [Inject]
    private PartIdGenerator IdGenerator { get; set; } = null!;

    private bool IsDisabled => Disabled || RootContext!.State.Disabled;

    [CascadingParameter]
    private MenuRootContext? RootContext { get; set; }

    /// <summary>Gets or sets a value indicating whether the button ignores user interaction.</summary>
    /// <value>The default is <see langword="false"/>. The button is also disabled while its menu is.</value>
    [Parameter]
    public bool Disabled { get; set; }

    /// <summary>Gets or sets a value indicating whether the rendered element is a native <c>button</c>.</summary>
    /// <value>The default is <see langword="true"/>. Set it to <see langword="false"/> when the element rendered in its place is not a button.</value>
    [Parameter]
    public bool NativeButton { get; set; } = true;

    /// <inheritdoc />
    protected override string DefaultTagName => "button";

    /// <inheritdoc />
    protected override MenuClearState State => state;

    /// <inheritdoc />
    protected override bool UsesGeneratedId => RenderFunction is not null;

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        // MNU-EDGE-04, STR-15.
        filter = RootContext?.Filter ?? throw MenuParts.MissingFilter("Clear");
        filterSubscription = filter.Subscribe(() => _ = InvokeAsync(StateHasChanged));
        button = new ButtonBehavior(JSRuntime, Logger, DevelopmentLog, IdGenerator, StateHasChanged, DispatchExceptionAsync, onKeyDown, onKeyUp)
        {
            FocusableWhenDisabled = false,
        };
        base.OnInitialized();
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        button!.Native = NativeButton;
        button.SetConsumerAttributes(AdditionalAttributes);
    }

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        bool disabled = IsDisabled;
        if (state.Disabled != disabled)
        {
            state = new MenuClearState(disabled);
        }

        Enabled = filter!.ShowsClear;
        base.BuildRenderTree(builder);
    }

    /// <inheritdoc />
    protected override Task OnAfterRenderAsync(bool firstRender) => button!.AfterRenderAsync(Enabled, Element, Id, composite: false);

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            filterSubscription?.Dispose();
            button?.Dispose();
        }

        base.Dispose(disposing);
    }

    // MNU-DOM-22, ARIA-16: tabindex -1 after the button's own, so the button stays out of the tab order.
    /// <inheritdoc />
    protected override void AddPartAttributes(AttributeList attributes)
    {
        button!.Disabled = state.Disabled;
        attributes.Set("aria-hidden", "true");
        if (state.Disabled)
        {
            attributes.Set(MenuClearDataAttributes.Disabled, true);
        }

        attributes.Set("onclick", onClick);
        button.AddAttributes(attributes, Id, composite: false);
        attributes.Set("tabindex", "-1");
    }

    /// <inheritdoc />
    protected override bool ForwardsConsumerHandler(string eventName) => button!.ForwardsConsumerHandler(eventName);

    // MNU-EVT-17, PTR-21: a disabled button requests nothing.
    private Task HandleClickAsync(MouseEventArgs e) =>
        IsDisabled ? Task.CompletedTask : filter!.RequestQueryAsync("", EventReason.ClearPress, EventSnapshot.FromEventArgs(e));
}

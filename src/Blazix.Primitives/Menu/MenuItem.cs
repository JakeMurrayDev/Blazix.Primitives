using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace Blazix.Primitives;

// MNU §3.9 state object (menu/item/MenuItem.tsx:59-62). Highlighted is the list handle's (ADR 04 Amendments 2026-11-03
// (2), chosen by the user): mirrored, so C# never projects data-highlighted.
/// <summary>Represents the state of a menu item, which the part exposes as data attributes and passes to its class and style functions.</summary>
/// <param name="Disabled">A value indicating whether the item ignores user interaction; projected as <c>data-disabled</c>.</param>
/// <param name="Highlighted">
/// A value indicating whether the item is highlighted; its <c>data-highlighted</c> attribute is written by the
/// browser-side behavior.
/// </param>
[PartState]
public sealed partial record MenuItemState(bool Disabled, [property: MirroredField] bool Highlighted);

// menu/item/MenuItemDataAttributes.ts (MNU-DOM-13).
/// <summary>Provides the names of the data attributes a menu item renders.</summary>
public static class MenuItemDataAttributes
{
    /// <summary>Represents the attribute present while the item is highlighted: <c>data-highlighted</c>.</summary>
    public const string Highlighted = "data-highlighted";

    /// <summary>Represents the attribute present while the item is disabled: <c>data-disabled</c>.</summary>
    public const string Disabled = "data-disabled";
}

// Base UI's Menu.Item (menu/item/MenuItem.tsx); the contract is docs/components/menu.md §3.9 (MNU-STR-08, API-34..39,
// DOM-13/14, ARIA-07). A thin shell over MenuItemBehavior.
/// <summary>Represents an individual interactive item in a menu, Base UI's <c>Menu.Item</c>, rendered as a <c>div</c>.</summary>
public sealed class MenuItem : PartBase<MenuItemState>
{
    private readonly EventCallback onClick;
    private readonly EventCallback onKeyDown;
    private readonly EventCallback onKeyUp;
    private MenuItemBehavior? behavior;
    private MenuItemState state = new(false, false);

    /// <summary>Initializes a new instance of the <see cref="MenuItem"/> class.</summary>
    public MenuItem()
    {
        onClick = CreateHandler<MouseEventArgs>("onclick", e => behavior!.HandleClickAsync(e));
        onKeyDown = CreateHandler<KeyboardEventArgs>("onkeydown", e => behavior!.HandleKeyDownAsync(e));
        onKeyUp = CreateHandler<KeyboardEventArgs>("onkeyup", e => behavior!.HandleKeyUpAsync(e));
    }

    [Inject]
    private IJSRuntime JSRuntime { get; set; } = null!;

    [Inject]
    private ILogger<MenuItem> Logger { get; set; } = null!;

    [Inject]
    private ButtonDevelopmentLog DevelopmentLog { get; set; } = null!;

    [Inject]
    private PartIdGenerator IdGenerator { get; set; } = null!;

    [CascadingParameter]
    private MenuRootContext? RootContext { get; set; }

    [CascadingParameter]
    private CompositeRootContext? ListRoot { get; set; }

    // MNU-API-34
    /// <summary>Gets or sets a value indicating whether a click on the item closes the menu.</summary>
    /// <value><see langword="true"/> by default.</value>
    [Parameter]
    public bool CloseOnClick { get; set; } = true;

    // MNU-API-35
    /// <summary>Gets or sets a value indicating whether the item ignores user interaction.</summary>
    /// <value><see langword="false"/> by default; a disabled item stays focusable.</value>
    [Parameter]
    public bool Disabled { get; set; }

    // MNU-API-36
    /// <summary>Gets or sets the text the typeahead matches the item by.</summary>
    /// <value>The label, or <see langword="null"/>, the default, for the item's text content.</value>
    [Parameter]
    public string? Label { get; set; }

    // MNU-API-37
    /// <summary>Gets or sets a value indicating whether the rendered element is a native <c>button</c>.</summary>
    /// <value><see langword="false"/> by default; set it when <see cref="PartBase{TState}.Render"/> supplies a <c>button</c>.</value>
    [Parameter]
    public bool NativeButton { get; set; }

    /// <inheritdoc />
    protected override string DefaultTagName => "div";

    /// <inheritdoc />
    protected override MenuItemState State
    {
        get
        {
            bool highlighted = behavior!.Highlighted;
            bool disabled = behavior.Disabled;
            if (state.Disabled != disabled || state.Highlighted != highlighted)
            {
                state = new MenuItemState(disabled, highlighted);
            }

            return state;
        }
    }

    /// <inheritdoc />
    protected override bool UsesGeneratedId => true;

    /// <summary>Gets a task that completes when the item's handles are attached.</summary>
    internal Task TwinsAttached => behavior?.TwinsAttached ?? Task.CompletedTask;

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        behavior = new MenuItemBehavior("Menu", MenuItemKind.Item, JSRuntime, Logger, DevelopmentLog, IdGenerator, StateHasChanged, DispatchExceptionAsync, onClick, onKeyDown, onKeyUp);
        behavior.Subscribe(RootContext, ListRoot);
        base.OnInitialized();
    }

    /// <inheritdoc />
    protected override void OnParametersSet() => behavior!.Apply(Id, new MenuItemParameters(Disabled, Label, NativeButton, CloseOnClick), AdditionalAttributes);

    /// <inheritdoc />
    protected override Task OnAfterRenderAsync(bool firstRender) => behavior!.OnAfterRenderAsync(Enabled, Element, Id);

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
    protected override void AddPartAttributes(AttributeList attributes) => behavior!.AddAttributes(attributes, Id);

    /// <inheritdoc />
    protected override bool ForwardsConsumerHandler(string eventName) => behavior!.ForwardsConsumerHandler(eventName);
}

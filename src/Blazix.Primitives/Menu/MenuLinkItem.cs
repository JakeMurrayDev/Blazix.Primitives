using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace Blazix.Primitives;

// MNU §3.10 state object (menu/link-item/MenuLinkItem.tsx). Highlighted is mirrored (ADR 04 Amendments 2026-11-03 (2)).
/// <summary>Represents the state of a menu link item, which the part exposes as data attributes and passes to its class and style functions.</summary>
/// <param name="Highlighted">
/// A value indicating whether the item is highlighted; its <c>data-highlighted</c> attribute is written by the
/// browser-side behavior.
/// </param>
[PartState]
public sealed partial record MenuLinkItemState([property: MirroredField] bool Highlighted);

// menu/link-item/MenuLinkItemDataAttributes.ts (MNU-DOM-15).
/// <summary>Provides the names of the data attributes a menu link item renders.</summary>
public static class MenuLinkItemDataAttributes
{
    /// <summary>Represents the attribute present while the item is highlighted: <c>data-highlighted</c>.</summary>
    public const string Highlighted = "data-highlighted";
}

// Base UI's Menu.LinkItem (menu/link-item/MenuLinkItem.tsx); the contract is docs/components/menu.md §3.10 (MNU-API-40,
// DOM-15). A thin shell over MenuItemBehavior (kind LinkItem: an anchor, no button behavior, no Disabled).
/// <summary>Represents a link in a menu, Base UI's <c>Menu.LinkItem</c>, rendered as an <c>a</c> element.</summary>
public sealed class MenuLinkItem : PartBase<MenuLinkItemState>
{
    private readonly EventCallback onClick;
    private readonly EventCallback onKeyDown;
    private readonly EventCallback onKeyUp;
    private MenuItemBehavior? behavior;
    private MenuLinkItemState state = new(false);

    /// <summary>Initializes a new instance of the <see cref="MenuLinkItem"/> class.</summary>
    public MenuLinkItem()
    {
        onClick = CreateHandler<MouseEventArgs>("onclick", e => behavior!.HandleClickAsync(e));
        onKeyDown = CreateHandler<KeyboardEventArgs>("onkeydown", e => behavior!.HandleKeyDownAsync(e));
        onKeyUp = CreateHandler<KeyboardEventArgs>("onkeyup", e => behavior!.HandleKeyUpAsync(e));
    }

    [Inject]
    private IJSRuntime JSRuntime { get; set; } = null!;

    [Inject]
    private ILogger<MenuLinkItem> Logger { get; set; } = null!;

    [Inject]
    private ButtonDevelopmentLog DevelopmentLog { get; set; } = null!;

    [Inject]
    private PartIdGenerator IdGenerator { get; set; } = null!;

    [CascadingParameter]
    private MenuRootContext? RootContext { get; set; }

    [CascadingParameter]
    private CompositeRootContext? ListRoot { get; set; }

    // MNU-API-40
    /// <summary>Gets or sets a value indicating whether a click on the link closes the menu.</summary>
    /// <value><see langword="false"/> by default.</value>
    [Parameter]
    public bool CloseOnClick { get; set; }

    /// <summary>Gets or sets the text the typeahead matches the link by.</summary>
    /// <value>The label, or <see langword="null"/>, the default, for the link's text content.</value>
    [Parameter]
    public string? Label { get; set; }

    /// <inheritdoc />
    protected override string DefaultTagName => "a";

    /// <inheritdoc />
    protected override MenuLinkItemState State
    {
        get
        {
            bool highlighted = behavior!.Highlighted;
            if (state.Highlighted != highlighted)
            {
                state = new MenuLinkItemState(highlighted);
            }

            return state;
        }
    }

    /// <inheritdoc />
    protected override bool UsesGeneratedId => true;

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        behavior = new MenuItemBehavior("Menu", MenuItemKind.LinkItem, JSRuntime, Logger, DevelopmentLog, IdGenerator, StateHasChanged, DispatchExceptionAsync, onClick, onKeyDown, onKeyUp);
        behavior.Subscribe(RootContext, ListRoot);
        base.OnInitialized();
    }

    /// <inheritdoc />
    protected override void OnParametersSet() => behavior!.Apply(Id, new MenuItemParameters(Disabled: false, Label, NativeButton: false, CloseOnClick), AdditionalAttributes);

    // MNU-STA-15 (M5 track 12): an item the menu's filter hides renders nothing.
    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        Enabled = !behavior!.FilteredOut;
        base.BuildRenderTree(builder);
    }

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

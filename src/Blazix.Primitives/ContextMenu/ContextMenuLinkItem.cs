using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace Blazix.Primitives;

// MNU §3.10 state object (menu/link-item/MenuLinkItem.tsx). Highlighted is mirrored (ADR 04 Amendments 2026-11-03 (2)).
/// <summary>Represents the state of a context menu link item, which the part exposes as data attributes and passes to its class and style functions.</summary>
/// <param name="Highlighted">
/// A value indicating whether the item is highlighted; its <c>data-highlighted</c> attribute is written by the
/// browser-side behavior.
/// </param>
[PartState]
public sealed partial record ContextMenuLinkItemState([property: MirroredField] bool Highlighted);

// menu/link-item/MenuLinkItemDataAttributes.ts (MNU-DOM-15).
/// <summary>Provides the names of the data attributes a context menu link item renders.</summary>
public static class ContextMenuLinkItemDataAttributes
{
    /// <summary>Represents the attribute present while the item is highlighted: <c>data-highlighted</c>.</summary>
    public const string Highlighted = "data-highlighted";
}

// Base UI's ContextMenu.LinkItem is Menu.LinkItem (context-menu/index.parts.ts:4-19; CTX-STR-01, STR-02: the MNU rows below are
// adopted). Decision 12 of the M5 plan and ADR 08 Amendments 2026-11-03 (3) (chosen by the user 2026-11-03): a sealed shell
// over Menu's behaviour with the family name `ContextMenu`, which its diagnostics name (CTX-EDGE-02); M5 track 7.
// Base UI's Menu.LinkItem (menu/link-item/MenuLinkItem.tsx); the contract is docs/components/menu.md §3.10 (MNU-API-40,
// DOM-15). A thin shell over MenuItemBehavior (kind LinkItem: an anchor, no button behavior, no Disabled).
/// <summary>Represents a link in a context menu, Base UI's <c>ContextMenu.LinkItem</c>, rendered as an <c>a</c> element.</summary>
public sealed class ContextMenuLinkItem : PartBase<ContextMenuLinkItemState>
{
    private readonly EventCallback onClick;
    private readonly EventCallback onKeyDown;
    private readonly EventCallback onKeyUp;
    private MenuItemBehavior? behavior;
    private ContextMenuLinkItemState state = new(false);

    /// <summary>Initializes a new instance of the <see cref="ContextMenuLinkItem"/> class.</summary>
    public ContextMenuLinkItem()
    {
        onClick = CreateHandler<MouseEventArgs>("onclick", e => behavior!.HandleClickAsync(e));
        onKeyDown = CreateHandler<KeyboardEventArgs>("onkeydown", e => behavior!.HandleKeyDownAsync(e));
        onKeyUp = CreateHandler<KeyboardEventArgs>("onkeyup", e => behavior!.HandleKeyUpAsync(e));
    }

    [Inject]
    private IJSRuntime JSRuntime { get; set; } = null!;

    [Inject]
    private ILogger<ContextMenuLinkItem> Logger { get; set; } = null!;

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
    protected override ContextMenuLinkItemState State
    {
        get
        {
            bool highlighted = behavior!.Highlighted;
            if (state.Highlighted != highlighted)
            {
                state = new ContextMenuLinkItemState(highlighted);
            }

            return state;
        }
    }

    /// <inheritdoc />
    protected override bool UsesGeneratedId => true;

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        behavior = new MenuItemBehavior("ContextMenu", MenuItemKind.LinkItem, JSRuntime, Logger, DevelopmentLog, IdGenerator, StateHasChanged, DispatchExceptionAsync, onClick, onKeyDown, onKeyUp);
        behavior.Subscribe(RootContext, ListRoot);
        base.OnInitialized();
    }

    /// <inheritdoc />
    protected override void OnParametersSet() => behavior!.Apply(Id, new MenuItemParameters(Disabled: false, Label, NativeButton: false, CloseOnClick), AdditionalAttributes);

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

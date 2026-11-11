using Blazix.Primitives.Interop;
using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Blazix.Primitives;

// Base UI's plain Menu.List (menu/list/MenuList.tsx); the contract is docs/components/menu.md §3.27 (MNU-STR-13, API-62,
// DOM-24, ARIA-05). A thin shell over MenuListBehavior. In a filterable menu it also attaches the `menu-filter` List handle,
// which reports the rendered texts of the items without a label (MNU-STA-15, STA-17; M5 track 12's design point 2 (b′)).
/// <summary>
/// Groups a menu's items so other content can share the popup, Base UI's <c>Menu.List</c>, rendered as a <c>div</c> with the
/// <c>menu</c> role.
/// </summary>
public sealed class MenuList : PartBase<EmptyState>
{
    private MenuListBehavior? behavior;
    private MenuFilterModuleAttachment? filterModule;
    private IDisposable? textIdsSubscription;

    [Inject]
    private IJSRuntime JSRuntime { get; set; } = null!;

    [CascadingParameter]
    private MenuRootContext? RootContext { get; set; }

    [CascadingParameter]
    private PopupPortalContext? Portal { get; set; }

    /// <inheritdoc />
    protected override string DefaultTagName => "div";

    /// <inheritdoc />
    protected override EmptyState State => EmptyState.Instance;

    /// <inheritdoc />
    protected override bool UsesGeneratedId => true;

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        behavior = new MenuListBehavior("Menu", StateHasChanged);
        behavior.Subscribe(RootContext);
        if (RootContext!.Filter is { } filter)
        {
            filterModule = new MenuFilterModuleAttachment(JSRuntime);
            filterModule.Twin.TextsChanged += texts => _ = InvokeAsync(() => filter.SetTexts(texts.Ids, texts.Texts));
            textIdsSubscription = filter.SubscribeTextIds(() => _ = InvokeAsync(StateHasChanged));
        }

        base.OnInitialized();
    }

    /// <inheritdoc />
    protected override void OnParametersSet() => behavior!.Apply(Id, AdditionalAttributes);

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (filterModule is null || Element is not { } element)
        {
            return;
        }

        await PopupPortalContext.WhenMoved(Portal);
        await filterModule.SyncAsync(element, new MenuFilterOptions
        {
            Part = MenuFilterTwin.ListPart,
            MenuKey = RootContext!.SessionToken,
            TextIds = RootContext.Filter!.TextIds,
        });
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            behavior?.Dispose();
            textIdsSubscription?.Dispose();
            if (filterModule is not null)
            {
                _ = filterModule.DisposeAsync().AsTask();
            }
        }

        base.Dispose(disposing);
    }

    /// <inheritdoc />
    protected override void AddPartAttributes(AttributeList attributes) => behavior!.AddAttributes(attributes);
}

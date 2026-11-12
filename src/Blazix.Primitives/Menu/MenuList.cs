using Blazix.Primitives.Interop;
using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
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

    /// <summary>Gets a task that completes when a filterable menu's list has attached its <c>menu-filter</c> handle.</summary>
    internal Task FilterAttached => filterModule?.Attached ?? Task.CompletedTask;

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
    public override Task SetParametersAsync(ParameterView parameters)
    {
        // The part replaces ChildContent with its own cascade in a filterable menu, so a parameter set without content must not keep it.
        ChildContent = null;
        return base.SetParametersAsync(parameters);
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        behavior!.Apply(Id, AdditionalAttributes);

        // MNU-STA-24 (FilterDropdownList.tsx:58-66): the list's groups and items belong to this root's query, so a filterable
        // submenu's list inside an enclosing group of its parent does not count them as that group's members.
        if (RootContext!.Filter is not null)
        {
            RenderFragment? content = ChildContent;
            ChildContent = builder =>
            {
                builder.OpenComponent<CascadingValue<MenuFilterGroupContext?>>(0);
                builder.AddComponentParameter(1, nameof(CascadingValue<MenuFilterGroupContext?>.Value), null);
                builder.AddComponentParameter(2, nameof(CascadingValue<MenuFilterGroupContext?>.IsFixed), true);
                builder.AddComponentParameter(3, nameof(CascadingValue<MenuFilterGroupContext?>.ChildContent), content);
                builder.CloseComponent();
            };
        }
    }

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

            // FilterDropdownRoot.tsx:89-90: an item-set change highlights the first item when the automatic highlight applies,
            // which the module decides from the input's current text, since a query change reaches C# a round trip later.
            AutoHighlight = RootContext.Filter.AutoHighlight.IsAlways ? "always" : RootContext.Filter.AutoHighlight.IsEnabled ? "true" : null,
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

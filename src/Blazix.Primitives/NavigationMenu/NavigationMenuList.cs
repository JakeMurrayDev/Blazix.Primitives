using Blazix.Primitives.Interop;
using Blazix.Primitives.Rendering;
using Blazix.Primitives.Store;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace Blazix.Primitives;

// NAV-STA-16, §3.2 state object (navigation-menu/list/NavigationMenuList.tsx:68-70, 136-142): the default mapping only.
/// <summary>Represents the state of a navigation menu list, which the part exposes as data attributes and passes to its class and style functions.</summary>
/// <param name="Open">Whether the menu is open; projected as <c>data-open</c>.</param>
[PartState]
public sealed partial record NavigationMenuListState(bool Open);

// Base UI's NavigationMenu.List (navigation-menu/list/NavigationMenuList.tsx); the contract is
// docs/components/navigation-menu.md §3.2, §5.2, §6.1.
// - Top level: a `ul` that is a composite root (the Root's orientation, loopFocus false, Home and End off, the direction
//   from DirectionProviderContext; NAV-KEY-05, NAV-KEY-07), with the `navigation-menu` module's propagation guard for its
//   axis' arrows (NAV-KEY-06). Nested: a plain `ul` whose triggers join the enclosing Content's composite (NAV-STR-02,
//   NAV-KEY-08). No aria-orientation, no role (NAV-ARIA-07).
// - Base UI marks the List as inside for the dismissal (contradiction 15); the port's session marks its floating element.
/// <summary>
/// Represents the list of a navigation menu's items: Base UI's <c>NavigationMenu.List</c>, rendered as a <c>ul</c>. The
/// arrow keys of the root's orientation move between its triggers and links.
/// </summary>
public sealed class NavigationMenuList : PartBase<NavigationMenuListState>
{
    private readonly EventCallback onKeyDown;
    private NavigationMenuRootContext? root;
    private CompositeRootBehavior? composite;
    private NavigationMenuModule? module;
    private StoreSelection<NavigationMenuStoreState, bool>? open;
    private NavigationMenuListState? state;
    private RenderFragment? cascadedContent;
    private RenderFragment? cascade;

    /// <summary>Initializes a new instance of the <see cref="NavigationMenuList"/> class.</summary>
    public NavigationMenuList()
    {
        onKeyDown = CreateHandler<KeyboardEventArgs>("onkeydown", e => composite?.HandleKeyDownAsync(e) ?? Task.CompletedTask);
    }

    [Inject]
    private IJSRuntime JSRuntime { get; set; } = null!;

    [Inject]
    private PartIdGenerator IdGenerator { get; set; } = null!;

    [CascadingParameter]
    private NavigationMenuRootContext? RootContext { get; set; }

    [CascadingParameter]
    private DirectionProviderContext? DirectionContext { get; set; }

    /// <inheritdoc />
    protected override string DefaultTagName => "ul";

    /// <inheritdoc />
    protected override NavigationMenuListState State
    {
        get
        {
            bool isOpen = open!.Value;
            if (state is null || state.Open != isOpen)
            {
                state = new NavigationMenuListState(isOpen);
            }

            return state;
        }
    }

    /// <summary>Gets a task that completes once the list's handles have attached.</summary>
    internal Task TwinsAttached => Task.WhenAll(composite?.TwinAttached ?? Task.CompletedTask, module?.Attached ?? Task.CompletedTask);

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        root = NavigationMenuRootContext.Require(RootContext);
        if (!root.Nested)
        {
            composite = new CompositeRootBehavior(JSRuntime, IdGenerator, StateHasChanged, DispatchExceptionAsync, onKeyDown)
            {
                LoopFocus = false,
                EnableHomeAndEndKeys = false,
            };
            module = new NavigationMenuModule(JSRuntime);
        }

        base.OnInitialized();
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        if (composite is null)
        {
            return;
        }

        composite.Orientation = CompositeLiterals.ToLiteral(root!.Orientation);
        composite.SetDirectionContext(DirectionContext);
        composite.SetConsumerAttributes(AdditionalAttributes);
        if (cascade is null || (!ReferenceEquals(ChildContent, cascade) && !ReferenceEquals(cascadedContent, ChildContent)))
        {
            cascadedContent = ChildContent;
            cascade = composite.Context.Cascade(cascadedContent);
        }

        ChildContent = cascade;
    }

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (composite is null)
        {
            return;
        }

        Task moduleSync = Element is { } element ? module!.SyncAsync(element, new NavigationMenuOptions { Part = NavigationMenuTwin.ListPart }, root!.RootElement) : Task.CompletedTask;
        await composite.AfterRenderAsync(Enabled, Element, Id);
        await moduleSync;
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            composite?.Dispose();
            if (module is not null)
            {
                _ = module.DisposeAsync().AsTask();
            }
        }

        base.Dispose(disposing);
    }

    /// <inheritdoc />
    protected override void SubscribeSelectors()
    {
        open = SubscribeTo(RootContext!.Store).Select(NavigationMenuSelectors.Open);
    }

    /// <inheritdoc />
    protected override void AddPartAttributes(AttributeList attributes) => composite?.AddAttributes(attributes, Id);

    /// <inheritdoc />
    protected override bool ForwardsConsumerHandler(string eventName) => composite?.ForwardsConsumerHandler(eventName) ?? true;
}

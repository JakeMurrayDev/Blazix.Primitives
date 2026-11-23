using Blazix.Primitives.Interop;
using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace Blazix.Primitives;

// NAV-STA-16, §3.7 state object (navigation-menu/link/NavigationMenuLink.tsx:37-39).
/// <summary>Represents the state of a navigation menu link, which the part exposes as data attributes and passes to its class and style functions.</summary>
/// <param name="Active">Whether the link is the current page; projected as <c>data-active</c>.</param>
[PartState]
public sealed partial record NavigationMenuLinkState(bool Active);

// navigation-menu/link/NavigationMenuLinkDataAttributes.ts (NAV-DOM-20).
/// <summary>Provides the names of the data attributes a navigation menu link renders.</summary>
public static class NavigationMenuLinkDataAttributes
{
    /// <summary>Represents the attribute present while the link is the current page: <c>data-active</c>.</summary>
    public const string Active = "data-active";
}

// Base UI's NavigationMenu.Link (navigation-menu/link/NavigationMenuLink.tsx); the contract is
// docs/components/navigation-menu.md §3.7, §5.7.
// - An `a`, a composite item of the enclosing List or Content; aria-current="page" while Active (NAV-ARIA-04, the
//   consumer's wins). NAV-DOM-21's "no tabindex" needs track 1's registration form (open to track 1 until it lands; the
//   item renders the module's roving tabindex meanwhile).
// - CloseOnClick: a C# onclick, after the consumer's, requests a close with `link-press` (NAV-API-14, NAV-DIS-09, the
//   Close precedent); a C# handler does not run inside a shadow-root container (issue 146, a documented deviation).
// - A Link outside its Root's Content attaches the module's link handle for its blur check (NAV-FOC-11); a Link inside
//   one does not, whose check never passes (§16 Q7 replicated).
/// <summary>
/// Represents a link of a navigation menu: Base UI's <c>NavigationMenu.Link</c>, rendered as an <c>a</c>.
/// </summary>
public sealed class NavigationMenuLink : PartBase<NavigationMenuLinkState>
{
    private const string ArrowKeyPrefix = "Arrow";

    private readonly EventCallback onKeyDown;
    private readonly EventCallback onClick;
    private NavigationMenuRootContext? root;
    private CompositeItemBehavior? composite;
    private NavigationMenuModule? module;
    private NavigationMenuLinkState? state;

    /// <summary>Initializes a new instance of the <see cref="NavigationMenuLink"/> class.</summary>
    public NavigationMenuLink()
    {
        onKeyDown = CreateHandler<KeyboardEventArgs>("onkeydown", e => e.Key is { } key && key.StartsWith(ArrowKeyPrefix, StringComparison.Ordinal) ? composite!.HandleKeyDownAsync(e) : Task.CompletedTask);
        onClick = CreateHandler<MouseEventArgs>("onclick", HandleClickAsync);
    }

    [Inject]
    private IJSRuntime JSRuntime { get; set; } = null!;

    // NAV-FOC-11: only a Link outside its Root's Content checks its blur.
    private bool ChecksBlur => !ReferenceEquals(ContentScope?.Root, root);

    [CascadingParameter]
    private NavigationMenuRootContext? RootContext { get; set; }

    [CascadingParameter]
    private CompositeRootContext? CompositeContext { get; set; }

    [CascadingParameter]
    private NavigationMenuContentScope? ContentScope { get; set; }

    // NAV-API-13
    /// <summary>Gets or sets a value indicating whether the link is the current page.</summary>
    /// <value><see langword="false"/> by default; <see langword="true"/> renders <c>aria-current="page"</c>.</value>
    [Parameter]
    public bool Active { get; set; }

    // NAV-API-14
    /// <summary>Gets or sets a value indicating whether a click on the link closes the menu.</summary>
    /// <value><see langword="false"/> by default.</value>
    [Parameter]
    public bool CloseOnClick { get; set; }

    /// <inheritdoc />
    protected override string DefaultTagName => "a";

    /// <inheritdoc />
    protected override NavigationMenuLinkState State
    {
        get
        {
            if (state is null || state.Active != Active)
            {
                state = new NavigationMenuLinkState(Active);
            }

            return state;
        }
    }

    /// <inheritdoc />
    protected override bool UsesGeneratedId => true;

    /// <summary>Gets the link's <c>navigation-menu</c> twin, or <see langword="null"/> inside its Root's content.</summary>
    internal NavigationMenuTwin? ModuleTwin => module?.Twin;

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        root = NavigationMenuRootContext.Require(RootContext);
        composite = new CompositeItemBehavior(CompositeContext, "NavigationMenuLink", onKeyDown);
        if (ChecksBlur)
        {
            module = new NavigationMenuModule(JSRuntime);
            module.Twin.FocusedOut += focusOut => _ = InvokeAsync(() => root.FocusOutAsync(focusOut.Event));
        }

        base.OnInitialized();
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        // NAV-DOM-21, NAV-FOC-03: no tabindex, so a Link is tabbable by native rules and stays a composite item for the arrows.
        composite!.SetRegistration(Id, AdditionalAttributes, omitTabIndex: true);
        composite.SetConsumerAttributes(AdditionalAttributes);
    }

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        Task moduleSync = module is not null && Element is { } element
            ? module.SyncAsync(element, new NavigationMenuOptions { Part = NavigationMenuTwin.LinkPart }, root!.RootElement)
            : Task.CompletedTask;
        await composite!.AfterRenderAsync(Element);
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
    protected override void AddPartAttributes(AttributeList attributes)
    {
        if (Active)
        {
            attributes.Set("aria-current", "page");
        }

        attributes.Set("onclick", onClick);
        composite!.AddAttributes(attributes);
    }

    /// <inheritdoc />
    protected override bool ForwardsConsumerHandler(string eventName) => composite!.ForwardsConsumerHandler(eventName);

    // NAV-API-14, NAV-DIS-09, NAV-EVT-10; NavigationMenuLink.tsx:44-48.
    private Task HandleClickAsync(MouseEventArgs e) =>
        CloseOnClick ? root!.RequestCloseAsync(new OpenChangeEventDetails(EventReason.LinkPress, EventSnapshot.FromEventArgs(e))) : Task.CompletedTask;
}

// The marker a Content cascades to its subtree, so a Link knows whether it is inside its own Root's Content (NAV-FOC-11).
/// <summary>Identifies the root whose content a part is rendered inside.</summary>
/// <param name="root">The root's context.</param>
internal sealed class NavigationMenuContentScope(NavigationMenuRootContext root)
{
    /// <summary>Gets the root whose content this is.</summary>
    public NavigationMenuRootContext Root { get; } = root;
}

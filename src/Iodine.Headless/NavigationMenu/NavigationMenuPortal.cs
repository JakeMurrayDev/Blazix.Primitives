using Blazix.Primitives.Rendering;
using Blazix.Primitives.Store;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.JSInterop;

namespace Blazix.Primitives;

// NAV-EDGE-03 (navigation-menu/portal/NavigationMenuPortalContext.ts): what the Positioner reads from its Portal.
internal sealed class NavigationMenuPortalContext
{
    public bool KeepMounted { get; internal set; }

    public static NavigationMenuPortalContext Require(NavigationMenuPortalContext? context)
    {
        // NAV-EDGE-03: thrown in every build.
        return context ?? throw new InvalidOperationException("Blazix: `<NavigationMenuPortal>` is missing.");
    }
}

// Base UI's NavigationMenu.Portal (navigation-menu/portal/NavigationMenuPortal.tsx); the contract is
// docs/components/navigation-menu.md §3.8, §5.8. The portal node over PopupPortalBehavior and PortalTwin (ADR 04 and 08
// Amendments 2026-10-27): the pipeline's display: contents host wrapper, the node `<div id data-base-ui-portal>` rendered
// display: none until the `portal` module moves it; no guards and no owner, as NavigationMenu publishes no focus manager
// (NAV-DOM-22). It renders while mounted or KeepMounted (NAV-ANI-01) and selects the mounted state, so it registers as a
// boundary part (ADR 16 Amendments 2026-10-27). An initially open menu prerenders its node display: none (ADR 04 §5).
/// <summary>
/// Moves the navigation menu's popup into another part of the document, the body by default: Base UI's
/// <c>NavigationMenu.Portal</c>, rendered as a <c>div</c>.
/// </summary>
public sealed class NavigationMenuPortal : PartBase<EmptyState>
{
    private readonly NavigationMenuPortalContext portalContext = new();
    private readonly RenderFragment cascadedContent;
    private NavigationMenuRootContext? root;
    private PopupPortalBehavior? portal;
    private StoreSelection<NavigationMenuStoreState, bool>? mounted;
    private IDisposable? boundaryPart;
    private RenderFragment? consumerContent;

    /// <summary>Initializes a new instance of the <see cref="NavigationMenuPortal"/> class.</summary>
    public NavigationMenuPortal()
    {
        cascadedContent = RenderCascade;
    }

    [Inject]
    private IJSRuntime JSRuntime { get; set; } = null!;

    [CascadingParameter]
    private NavigationMenuRootContext? RootContext { get; set; }

    [CascadingParameter]
    private PopupPortalContext? EnclosingPortal { get; set; }

    // NAV-API-15
    /// <summary>Gets or sets a value indicating whether the portal stays in the page while the menu is closed.</summary>
    /// <value><see langword="false"/> by default.</value>
    [Parameter]
    public bool KeepMounted { get; set; }

    // NAV-API-16
    /// <summary>Gets or sets the element the portal moves its node into.</summary>
    /// <value>An element, a script-held node or a selector; <see langword="null"/>, the default, is the enclosing portal's node, else the body.</value>
    [Parameter]
    public PortalContainerValue? Container { get; set; }

    /// <inheritdoc />
    protected override string DefaultTagName => "div";

    /// <inheritdoc />
    protected override EmptyState State => EmptyState.Instance;

    /// <inheritdoc />
    protected override bool UsesGeneratedId => true;

    /// <inheritdoc />
    protected override string? HostWrapperTagName => "div";

    /// <inheritdoc />
    public override Task SetParametersAsync(ParameterView parameters)
    {
        // The part replaces ChildContent with its own cascade, so a parameter set without content must not keep it.
        ChildContent = null;
        return base.SetParametersAsync(parameters);
    }

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        root = NavigationMenuRootContext.Require(RootContext);
        portal = new PopupPortalBehavior(JSRuntime, StateHasChanged);
        boundaryPart = root.Store.RegisterBoundaryPart();
        base.OnInitialized();
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        portal!.Enclosing = EnclosingPortal;
        portal.NodeId = Id;
        portalContext.KeepMounted = KeepMounted;
        consumerContent = ChildContent;
        ChildContent = portal.CascadeContent(cascadedContent);
    }

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        Enabled = (KeepMounted || mounted!.Value) && PopupPortalBehavior.RendersNode(Container);
        base.BuildRenderTree(builder);
    }

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        await portal!.OnAfterRenderAsync(Enabled ? Element : null, Container);
        await root!.Store.AtBatchBoundaryAsync();
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            boundaryPart?.Dispose();
            boundaryPart = null;
            if (portal is not null)
            {
                _ = portal.DisposeAsync().AsTask();
                portal = null;
            }
        }

        base.Dispose(disposing);
    }

    /// <inheritdoc />
    protected override void SubscribeSelectors()
    {
        mounted = SubscribeTo(RootContext!.Store).Select(NavigationMenuSelectors.Mounted);
    }

    /// <inheritdoc />
    protected override void AddPartAttributes(AttributeList attributes) => attributes.Set(PopupMarkerAttributes.Portal, string.Empty);

    /// <inheritdoc />
    protected override void FinishAttributes(AttributeList attributes) => portal!.FinishAttributes(attributes);

    /// <inheritdoc />
    protected override void AddHostWrapperAttributes(AttributeList attributes) => PopupPortalBehavior.AddWrapperAttributes(attributes);

    private void RenderCascade(RenderTreeBuilder builder) => NavigationMenuCascades.Render(builder, portalContext, consumerContent);
}

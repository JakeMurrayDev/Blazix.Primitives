using Blazix.Primitives.Rendering;
using Blazix.Primitives.Store;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.JSInterop;

namespace Blazix.Primitives;

// Base UI's Select.Portal (select/portal/SelectPortal.tsx); SEL-STR-06, SEL-API-33, SEL-DOM-16: the shared portal node
// (track 1 of M4's PopupPortalBehavior over PortalTwin), rendered once the popup is mounted or force-mounted and kept from
// then on, the positioner hidden while closed (SEL-DOM-18). No KeepMounted (SEL-API-33, §16 Q1). An initially open
// select prerenders the node `display: none` until the module moves it (ADR 04 §5; the M4 rows). It selects the mounted
// state, so it registers as a boundary part (ADR 16 Amendments 2026-10-27).
/// <summary>
/// Represents the node a select's positioner and backdrop are moved into, Base UI's <c>Select.Portal</c>, rendered as a
/// <c>div</c> appended to the document body by default.
/// </summary>
/// <remarks>Its content renders from the trigger's first focus or the first open, and stays rendered, hidden while closed.</remarks>
public sealed class SelectPortal : PartBase<EmptyState>
{
    private PopupPortalBehavior? portal;
    private SelectStore? store;
    private StoreSelection<SelectStoreState, bool>? rendered;
    private IDisposable? boundaryPart;
    private RenderFragment? consumerContent;
    private readonly RenderFragment passedContent;

    /// <summary>Initializes a new instance of the <see cref="SelectPortal"/> class.</summary>
    public SelectPortal()
    {
        passedContent = builder => builder.AddContent(0, consumerContent);
    }

    [Inject]
    private IJSRuntime JSRuntime { get; set; } = null!;

    [CascadingParameter]
    private SelectStore? RootStore { get; set; }

    [CascadingParameter]
    private PopupPortalContext? EnclosingPortal { get; set; }

    /// <summary>Gets or sets the element the portal node is appended to.</summary>
    /// <value>
    /// An element, a script-held node, a selector, or <see cref="PortalContainerValue.Pending"/> to render nothing yet;
    /// <see langword="null"/>, the default, for the enclosing portal's node, else the document body.
    /// </value>
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
        ChildContent = null;
        return base.SetParametersAsync(parameters);
    }

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">The portal is not inside a <see cref="SelectRoot{TValue}"/>.</exception>
    protected override void OnInitialized()
    {
        store = SelectStore.Require(RootStore);
        portal = new PopupPortalBehavior(JSRuntime, StateHasChanged);
        boundaryPart = store.RegisterBoundaryPart();
        base.OnInitialized();
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        portal!.Enclosing = EnclosingPortal;
        portal.NodeId = Id;
        consumerContent = ChildContent;
        ChildContent = portal.CascadeContent(passedContent);
    }

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        Enabled = rendered!.Value && PopupPortalBehavior.RendersNode(Container);
        base.BuildRenderTree(builder);
    }

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        await portal!.OnAfterRenderAsync(Enabled ? Element : null, Container);
        await store!.AtBatchBoundaryAsync();
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
    protected override void SubscribeSelectors() => rendered = SubscribeTo<SelectStoreState>(store!).Select(SelectStoreState.Rendered);

    /// <inheritdoc />
    protected override void AddPartAttributes(AttributeList attributes) => attributes.Set(PopupMarkerAttributes.Portal, string.Empty);

    /// <inheritdoc />
    protected override void FinishAttributes(AttributeList attributes) => portal!.FinishAttributes(attributes);

    /// <inheritdoc />
    protected override void AddHostWrapperAttributes(AttributeList attributes) => PopupPortalBehavior.AddWrapperAttributes(attributes);

    /// <inheritdoc />
    protected override void BuildBeforeElement(RenderTreeBuilder builder) => portal!.BuildBeforeElement(builder);

    /// <inheritdoc />
    protected override void BuildAfterElement(RenderTreeBuilder builder) => portal!.BuildAfterElement(builder);
}

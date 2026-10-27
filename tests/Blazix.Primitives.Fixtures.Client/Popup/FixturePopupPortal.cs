using Blazix.Primitives.Rendering;
using Blazix.Primitives.Store;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.JSInterop;

namespace Blazix.Primitives.Fixtures.Client.PopupFixtures;

/// <summary>
/// The test-only popup portal at L4: a family Portal's shape over <see cref="PopupPortalBehavior"/>, rendered while the
/// popup is mounted (or kept mounted). It selects the mounted state, so it registers as the store's boundary part.
/// </summary>
/// <typeparam name="TPayload">The type of the payload the popup's triggers carry.</typeparam>
[CascadingTypeParameter(nameof(TPayload))]
public sealed class FixturePopupPortal<TPayload> : PartBase<EmptyState>
{
    private PopupPortalBehavior? portal;
    private StoreSelection<FixturePopupStoreState<TPayload>, bool>? mounted;
    private IDisposable? boundaryPart;

    [Inject]
    private IJSRuntime JSRuntime { get; set; } = null!;

    [CascadingParameter]
    private FixturePopupStore<TPayload>? Store { get; set; }

    [CascadingParameter]
    private PopupPortalContext? EnclosingPortal { get; set; }

    [Parameter]
    public PortalContainerValue? Container { get; set; }

    [Parameter]
    public bool KeepMounted { get; set; }

    protected override string DefaultTagName => "div";

    protected override EmptyState State => EmptyState.Instance;

    protected override bool UsesGeneratedId => true;

    protected override string? HostWrapperTagName => "div";

    protected override void OnInitialized()
    {
        portal = new PopupPortalBehavior(JSRuntime, StateHasChanged);
        base.OnInitialized();
    }

    protected override void SubscribeSelectors()
    {
        SelectorSubscription<FixturePopupStoreState<TPayload>> subscription = SubscribeTo(Store!);
        mounted = subscription.Select(PopupSelectors<FixturePopupStoreState<TPayload>, TPayload>.Mounted);
        boundaryPart = Store!.RegisterBoundaryPart();
    }

    protected override void OnParametersSet()
    {
        portal!.Enclosing = EnclosingPortal;
        portal.NodeId = Id;
        ChildContent = portal.CascadeContent(ChildContent);
    }

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        Enabled = (KeepMounted || mounted!.Value) && PopupPortalBehavior.RendersNode(Container);
        base.BuildRenderTree(builder);
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        await portal!.OnAfterRenderAsync(Enabled ? Element : null, Container);
        await Store!.AtBatchBoundaryAsync();
    }

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

    protected override void AddPartAttributes(AttributeList attributes) => attributes.Set("data-base-ui-portal", string.Empty);

    protected override void FinishAttributes(AttributeList attributes) => portal!.FinishAttributes(attributes);

    protected override void AddHostWrapperAttributes(AttributeList attributes) => PopupPortalBehavior.AddWrapperAttributes(attributes);

    protected override void BuildBeforeElement(RenderTreeBuilder builder) => portal!.BuildBeforeElement(builder);

    protected override void BuildAfterElement(RenderTreeBuilder builder) => portal!.BuildAfterElement(builder);
}

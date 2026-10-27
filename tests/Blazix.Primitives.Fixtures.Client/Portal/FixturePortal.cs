using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.JSInterop;

namespace Blazix.Primitives.Fixtures.Client.PortalFixtures;

/// <summary>
/// The test-only portal at L4 (FloatingPortal and FloatingPortalLite over <see cref="PopupPortalBehavior"/>; the counterpart
/// of the L2 project's <c>TestPopupPortal</c>), for the <c>portal-*</c> fixtures and the CheckboxGroup and RadioGroup
/// portal titles: the node <c>&lt;div id data-base-ui-portal&gt;</c> inside the pipeline's <c>display: contents</c> wrapper,
/// with the outside guards and the owner beside it while a non-modal, open publication exists.
/// </summary>
public sealed class FixturePortal : PartBase<EmptyState>
{
    private readonly TaskCompletionSource attached = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private PopupPortalBehavior? portal;

    [Inject]
    private IJSRuntime JSRuntime { get; set; } = null!;

    [CascadingParameter]
    private PopupPortalContext? EnclosingPortal { get; set; }

    [Parameter]
    public PortalContainerValue? Container { get; set; }

    [Parameter]
    public bool Lite { get; set; }

    [Parameter]
    public string? PortalOwnerRole { get; set; }

    /// <summary>Gets a task that completes once the module has moved the node the first time.</summary>
    public Task TwinAttached => attached.Task;

    protected override string DefaultTagName => "div";

    protected override EmptyState State => EmptyState.Instance;

    protected override bool UsesGeneratedId => true;

    protected override string? HostWrapperTagName => "div";

    protected override void OnInitialized()
    {
        portal = new PopupPortalBehavior(JSRuntime, StateHasChanged);
        base.OnInitialized();
    }

    protected override void OnParametersSet()
    {
        portal!.Enclosing = EnclosingPortal;
        portal.Lite = Lite;
        portal.OwnerRole = PortalOwnerRole;
        portal.NodeId = Id;
        Enabled = PopupPortalBehavior.RendersNode(Container);
        ChildContent = portal.CascadeContent(ChildContent);
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        await portal!.OnAfterRenderAsync(Enabled ? Element : null, Container);
        if (portal.Context.Moved.IsCompletedSuccessfully && portal.Context.Node is not null)
        {
            attached.TrySetResult();
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && portal is not null)
        {
            _ = portal.DisposeAsync().AsTask();
            portal = null;
        }

        base.Dispose(disposing);
    }

    protected override void AddPartAttributes(AttributeList attributes) => attributes.Set("data-base-ui-portal", string.Empty);

    protected override void FinishAttributes(AttributeList attributes) => portal!.FinishAttributes(attributes);

    protected override void AddHostWrapperAttributes(AttributeList attributes) => PopupPortalBehavior.AddWrapperAttributes(attributes);

    protected override void BuildBeforeElement(RenderTreeBuilder builder) => portal!.BuildBeforeElement(builder);

    protected override void BuildAfterElement(RenderTreeBuilder builder) => portal!.BuildAfterElement(builder);
}

/// <summary>
/// A stand-in for the focus manager's publication to its portal (FLT-FOC-18; track 4 owns the real one): while rendered,
/// publishes <see cref="Modal"/> and <see cref="Open"/> after each render, and withdraws on dispose.
/// </summary>
public sealed class FixturePortalFocusPublisher : ComponentBase, IDisposable
{
    [CascadingParameter]
    private PopupPortalContext? Portal { get; set; }

    [Parameter]
    public bool Modal { get; set; }

    [Parameter]
    public bool Open { get; set; } = true;

    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    public void Dispose() => Portal?.Publish(null);

    protected override void BuildRenderTree(RenderTreeBuilder builder) => builder.AddContent(0, ChildContent);

    protected override void OnAfterRender(bool firstRender) => Portal?.Publish(new PortalFocusPublication(Modal, Open, CloseOnFocusOut: true));
}

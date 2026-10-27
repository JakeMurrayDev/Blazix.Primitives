using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.JSInterop;

namespace Blazix.Primitives.Tests.Components;

/// <summary>The state of the test-only portal: whether it is the lite portal, not projected.</summary>
[PartState]
internal sealed partial record TestPopupPortalState([property: NotProjected] bool Lite);

/// <summary>
/// A test-only portal: FloatingPortal and FloatingPortalLite over <see cref="PopupPortalBehavior"/> (ADR 16 rule 10), the
/// node <c>&lt;div id data-base-ui-portal&gt;</c> inside the pipeline's <c>display: contents</c> wrapper, with the outside
/// guards and the owner beside it.
/// </summary>
internal sealed class TestPopupPortal : PartBase<TestPopupPortalState>
{
    private PopupPortalBehavior? portal;
    private TestPopupPortalState state = new(false);

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

    public PopupPortalContext PortalContext => portal!.Context;

    protected override string DefaultTagName => "div";

    protected override TestPopupPortalState State => state;

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
        if (state.Lite != Lite)
        {
            state = new TestPopupPortalState(Lite);
        }

        Enabled = PopupPortalBehavior.RendersNode(Container);
        ChildContent = portal.CascadeContent(ChildContent);
    }

    protected override Task OnAfterRenderAsync(bool firstRender) => portal!.OnAfterRenderAsync(Enabled ? Element : null, Container);

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
/// publishes <see cref="Modal"/> and <see cref="Open"/> to the enclosing portal after each render, and withdraws on dispose.
/// </summary>
internal sealed class TestPortalFocusPublisher : ComponentBase, IDisposable
{
    [CascadingParameter]
    private PopupPortalContext? Portal { get; set; }

    [Parameter]
    public bool Modal { get; set; }

    [Parameter]
    public bool Open { get; set; } = true;

    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    protected override void BuildRenderTree(RenderTreeBuilder builder) => builder.AddContent(0, ChildContent);

    protected override void OnAfterRender(bool firstRender) => Portal?.Publish(new PortalFocusPublication(Modal, Open, CloseOnFocusOut: true));

    public void Dispose() => Portal?.Publish(null);
}

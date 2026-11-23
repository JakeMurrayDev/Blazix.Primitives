using Iodine.Headless.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Iodine.Headless;

// Base UI's Toast.Portal (toast/portal/ToastPortal.tsx:13-28); TST-STR-09, TST-API-17, TST-DOM-20. The lite portal of
// PSH-STR-11 (PopupPortalBehavior with Lite: no outside guards, no aria-owns owner, FLT-DOM-11): the node
// <div id data-base-ui-portal> inside the pipeline's display: contents wrapper. It needs no Provider and carries no toast
// behavior (STR-09, STR-13), and it has no KeepMounted (TST-ANI-13), so the node is rendered for the portal's whole
// life. M6 track 9b design point 4 (a) gives Toast a portal context of its own; this part uses only the shared one
// (PopupPortalContext) that nested portals read. The container is a PortalContainerValue, `null` meaning not supplied.
/// <summary>
/// Moves the toast viewport into a container elsewhere in the document, by default the document's body. Base UI's
/// <c>Toast.Portal</c>; it renders a <c>div</c>.
/// </summary>
public sealed class ToastPortal : PartBase<EmptyState>
{
    private PopupPortalBehavior? portal;
    private RenderFragment? cascadedContent;
    private RenderFragment? cascade;

    [Inject]
    private IJSRuntime JSRuntime { get; set; } = null!;

    [CascadingParameter]
    private PopupPortalContext? EnclosingPortal { get; set; }

    /// <summary>Gets or sets the element the portal's node is moved into.</summary>
    /// <value>
    /// An element, a selector or a JavaScript element reference; <see cref="PortalContainerValue.Pending"/> to render
    /// nothing until a container is given; or <see langword="null"/> (the default) for the enclosing portal's node, else
    /// the document's body.
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
    protected override void OnInitialized()
    {
        portal = new PopupPortalBehavior(JSRuntime, StateHasChanged) { Lite = true };
        base.OnInitialized();
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        portal!.Enclosing = EnclosingPortal;
        portal.NodeId = Id;

        // A parameter set that does not supply ChildContent leaves this part's own cascade in it.
        RenderFragment? supplied = cascade is not null && ReferenceEquals(ChildContent, cascade) ? cascadedContent : ChildContent;
        if (cascade is null || !ReferenceEquals(cascadedContent, supplied))
        {
            cascadedContent = supplied;
            cascade = portal.CascadeContent(cascadedContent);
        }

        ChildContent = cascade;
        Enabled = PopupPortalBehavior.RendersNode(Container);
    }

    /// <inheritdoc />
    protected override Task OnAfterRenderAsync(bool firstRender) => portal!.OnAfterRenderAsync(Enabled ? Element : null, Container);

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing && portal is not null)
        {
            _ = portal.DisposeAsync().AsTask();
            portal = null;
        }

        base.Dispose(disposing);
    }

    /// <inheritdoc />
    protected override void AddPartAttributes(AttributeList attributes) => attributes.Set(PopupMarkerAttributes.Portal, string.Empty);

    /// <inheritdoc />
    protected override void FinishAttributes(AttributeList attributes) => portal!.FinishAttributes(attributes);

    /// <inheritdoc />
    protected override void AddHostWrapperAttributes(AttributeList attributes) => PopupPortalBehavior.AddWrapperAttributes(attributes);
}

using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.JSInterop;

namespace Blazix.Primitives;

// The portal every dialog family composes (dialog/portal/DialogPortal.tsx; ADR 04 Amendments 2026-10-27, decision 5, chosen
// by the user 2026-10-27), over track 1's PopupPortalBehavior: the node `<div id data-base-ui-portal>` inside the
// `display: contents` wrapper, moved by the `portal` module; the outside guards and the `aria-owns` owner while a non-modal
// open focus manager has published (DLG-STR-03/04, API-17/18, DOM-05). It renders while `mounted || keepMounted` (DLG-API-17)
// and holds the node's style from attach (ADR 04 Amendments 2026-10-18).
// The internal backdrop (DLG-STR-10, DOM-06, PTR-18; PSH-DOM-14) is the node's first child while `mounted && modal === true`,
// with `inert` from C# while not open (UTL-DOM-16's inertValue), so it stops intercepting presses as the close is requested.
/// <summary>The behavior of a dialog-family portal: its node, its move, its internal backdrop and its registration.</summary>
internal sealed class DialogPortalBehavior : IAsyncDisposable
{
    private readonly PopupPortalBehavior portal;
    private readonly RenderFragment content;
    private readonly Action requestRender;
    private IDialogRootContext? context;
    private IDisposable? subscription;
    private IDisposable? boundaryPart;
    private RenderFragment? childContent;
    private RenderFragment? cascade;
    private bool disposed;

    /// <summary>Initializes a new instance of the <see cref="DialogPortalBehavior"/> class.</summary>
    /// <param name="jsRuntime">The runtime the portal's twin imports its module through.</param>
    /// <param name="requestRender">Asks the part to render, its <c>StateHasChanged</c>.</param>
    public DialogPortalBehavior(IJSRuntime jsRuntime, Action requestRender)
    {
        portal = new PopupPortalBehavior(jsRuntime, requestRender);
        content = RenderContent;
        this.requestRender = requestRender;
    }

    /// <summary>Attaches the module to the node after the render that created it, follows its container, or detaches.</summary>
    /// <param name="enabled">Whether the part rendered its node.</param>
    /// <param name="node">The node.</param>
    /// <param name="container">The part's <c>Container</c>.</param>
    /// <returns>A task that completes when the module has moved the node and the batch boundary has run.</returns>
    public async Task OnAfterRenderAsync(bool enabled, ElementReference? node, PortalContainerValue? container)
    {
        await portal.OnAfterRenderAsync(enabled ? node : null, container);
        await context!.AtBatchBoundaryAsync();
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        subscription?.Dispose();
        boundaryPart?.Dispose();
        if (context is not null && ReferenceEquals(context.Portal, portal.Context))
        {
            context.RegisterPortal(null, keepMounted: false);
        }

        await portal.DisposeAsync();
    }

    /// <summary>Subscribes to the root's mounted, modal and open state, once.</summary>
    /// <param name="rootContext">The enclosing root's context.</param>
    public void Subscribe(IDialogRootContext rootContext)
    {
        context = rootContext;
        subscription = rootContext.Subscribe(DialogFields.Mounted | DialogFields.Modality | DialogFields.Open, requestRender);
        boundaryPart = rootContext.RegisterBoundaryPart();
    }

    /// <summary>Applies the part's parameters and returns the content its node renders.</summary>
    /// <param name="enclosing">The context of the portal this one is rendered inside, or <see langword="null"/>.</param>
    /// <param name="nodeId">The id the node renders.</param>
    /// <param name="keepMounted">The part's <c>KeepMounted</c>.</param>
    /// <param name="consumerContent">The part's <c>ChildContent</c> as the consumer supplied it.</param>
    /// <returns>The fragment the part renders as its <c>ChildContent</c>.</returns>
    public RenderFragment Apply(PopupPortalContext? enclosing, string? nodeId, bool keepMounted, RenderFragment? consumerContent)
    {
        portal.Enclosing = enclosing;
        portal.NodeId = nodeId;
        // A parameter set that does not supply ChildContent leaves the part's property at the fragment returned last time.
        if (!ReferenceEquals(consumerContent, cascade))
        {
            childContent = consumerContent;
        }

        context!.RegisterPortal(portal.Context, keepMounted);
        cascade = portal.CascadeContent(content);
        return cascade;
    }

    /// <summary>Determines whether the portal renders its node in this render.</summary>
    /// <param name="keepMounted">The part's <c>KeepMounted</c>.</param>
    /// <param name="container">The part's <c>Container</c>.</param>
    /// <returns><see langword="true"/> while mounted or kept mounted, unless the container is pending.</returns>
    public bool Renders(bool keepMounted, PortalContainerValue? container) => (keepMounted || context!.Mounted) && PopupPortalBehavior.RendersNode(container);

    /// <summary>Hides the node until it moves and freezes its style.</summary>
    /// <param name="attributes">The node's merged attributes.</param>
    public void FinishAttributes(AttributeList attributes) => portal.FinishAttributes(attributes);

    /// <summary>Adds the wrapper's attributes.</summary>
    /// <param name="attributes">The wrapper's attributes.</param>
    public static void AddWrapperAttributes(AttributeList attributes) => PopupPortalBehavior.AddWrapperAttributes(attributes);

    /// <summary>Renders the before-outside guard and the owner.</summary>
    /// <param name="builder">The part's render tree builder.</param>
    public void BuildBeforeElement(RenderTreeBuilder builder) => portal.BuildBeforeElement(builder);

    /// <summary>Renders the after-outside guard.</summary>
    /// <param name="builder">The part's render tree builder.</param>
    public void BuildAfterElement(RenderTreeBuilder builder) => portal.BuildAfterElement(builder);

    private void RenderContent(RenderTreeBuilder builder)
    {
        IDialogRootContext root = context!;
        if (root.Mounted && root.Modality == DialogModality.Modal)
        {
            PopupInternalBackdrop.Render(builder, root.InternalBackdropId, inert: !root.Open);
        }

        builder.AddContent(1, childContent);
    }
}

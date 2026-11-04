using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.JSInterop;

namespace Blazix.Primitives;

// MNU-EDGE-03 (menu/portal/MenuPortalContext.ts:9): what the Positioner reads from its Portal (MNU-API-24: KeepMounted is
// published to the Positioner).
/// <summary>The state a menu portal shares with its positioner.</summary>
internal sealed class MenuPortalContext
{
    /// <summary>Gets a value indicating whether the portal keeps the popup mounted while closed.</summary>
    public bool KeepMounted { get; internal set; }
}

// Base UI's Menu.Portal (menu/portal/MenuPortal.tsx) over PopupPortalBehavior and PortalTwin, the PopoverPortal shape; the
// aria-owns owner is a `<span role="group">` for a submenu's or a menubar's menu (MNU-STR-18, ARIA-18; MenuPortal.tsx:32).
/// <summary>The behavior of a menu portal: the portal node, its move and the owner of its contents.</summary>
internal sealed class MenuPortalBehavior : IAsyncDisposable
{
    /// <summary>Initializes a new instance of the <see cref="MenuPortalBehavior"/> class.</summary>
    /// <param name="familyName">The portal's family, such as <c>Menu</c>.</param>
    /// <param name="jsRuntime">The runtime the twin imports its module through.</param>
    /// <param name="requestRender">Asks the part to render.</param>
    public MenuPortalBehavior(string familyName, IJSRuntime jsRuntime, Action requestRender) => throw new NotImplementedException();

    /// <summary>Gets the context the portal cascades to its positioner.</summary>
    public MenuPortalContext PortalContext => throw new NotImplementedException();

    /// <summary>Gets a value indicating whether the portal renders its node: mounted or kept mounted, and a container that renders one.</summary>
    public bool RendersNode => throw new NotImplementedException();

    /// <summary>Moves the node after the render and runs the batch boundary.</summary>
    /// <param name="element">The node, or <see langword="null"/> while it is not rendered.</param>
    /// <returns>A task that completes when the move has run.</returns>
    public Task OnAfterRenderAsync(ElementReference? element) => throw new NotImplementedException();

    /// <inheritdoc />
    public ValueTask DisposeAsync() => throw new NotImplementedException();

    /// <summary>Checks the part's placement and subscribes to the root's state, once.</summary>
    /// <param name="root">The enclosing root's context, or <see langword="null"/>.</param>
    /// <exception cref="InvalidOperationException">The part is outside a root.</exception>
    public void Subscribe(MenuRootContext? root) => throw new NotImplementedException();

    /// <summary>Applies the portal's parameters and wraps its content in the portal's cascades.</summary>
    /// <param name="nodeId">The portal node's id.</param>
    /// <param name="keepMounted">The portal's <c>KeepMounted</c>.</param>
    /// <param name="container">The portal's <c>Container</c>.</param>
    /// <param name="enclosing">The enclosing portal's context, or <see langword="null"/>.</param>
    /// <param name="content">The consumer's content.</param>
    /// <returns>The content to render inside the node.</returns>
    public RenderFragment? Apply(string? nodeId, bool keepMounted, PortalContainerValue? container, PopupPortalContext? enclosing, RenderFragment? content) =>
        throw new NotImplementedException();

    /// <summary>Adds the node's attributes.</summary>
    /// <param name="attributes">The part's attributes.</param>
    public void AddAttributes(AttributeList attributes) => throw new NotImplementedException();

    /// <summary>Finishes the node's attributes after the consumer's.</summary>
    /// <param name="attributes">The part's attributes.</param>
    public void FinishAttributes(AttributeList attributes) => throw new NotImplementedException();

    /// <summary>Adds the host wrapper's attributes.</summary>
    /// <param name="attributes">The wrapper's attributes.</param>
    public void AddWrapperAttributes(AttributeList attributes) => throw new NotImplementedException();

    /// <summary>Renders the outside guard and the owner before the node.</summary>
    /// <param name="builder">The part's render tree builder.</param>
    public void BuildBeforeElement(RenderTreeBuilder builder) => throw new NotImplementedException();

    /// <summary>Renders the outside guard after the node.</summary>
    /// <param name="builder">The part's render tree builder.</param>
    public void BuildAfterElement(RenderTreeBuilder builder) => throw new NotImplementedException();
}

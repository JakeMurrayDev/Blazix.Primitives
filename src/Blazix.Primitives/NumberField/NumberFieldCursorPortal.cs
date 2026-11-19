using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.JSInterop;

namespace Blazix.Primitives;

// NUM-STR-06, NUM-DOM-14 (NumberFieldScrubAreaCursor.tsx:52, ReactDOM.createPortal to the owning document's body): the
// cursor's body portal, the lite FloatingPortal over PopupPortalBehavior (composed, never edited; the brief's "no second
// portal mechanism"). The node `<div id data-base-ui-portal>` inside the pipeline's `display: contents` wrapper is appended
// to the body by the `portal` module; the cursor element is its only child (a wrapper Base UI's createPortal does not
// have, recorded on NUM-DOM-14).
/// <summary>The body portal of a number field scrub area cursor.</summary>
internal sealed class NumberFieldCursorPortal : PartBase<EmptyState>
{
    private PopupPortalBehavior? portal;

    [Inject]
    private IJSRuntime JSRuntime { get; set; } = null!;

    [CascadingParameter]
    private PopupPortalContext? EnclosingPortal { get; set; }

    /// <inheritdoc />
    protected override string DefaultTagName => "div";

    /// <inheritdoc />
    protected override EmptyState State => EmptyState.Instance;

    /// <inheritdoc />
    protected override bool UsesGeneratedId => true;

    /// <inheritdoc />
    protected override string? HostWrapperTagName => "div";

    /// <summary>Gets a task that completes once the <c>portal</c> module moved the node into the body.</summary>
    internal Task Moved => portal?.Context.Moved ?? Task.CompletedTask;

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
        ChildContent = portal.CascadeContent(ChildContent);
    }

    /// <inheritdoc />
    protected override Task OnAfterRenderAsync(bool firstRender) => portal!.OnAfterRenderAsync(Element, null);

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

    /// <inheritdoc />
    protected override void BuildBeforeElement(RenderTreeBuilder builder) => portal!.BuildBeforeElement(builder);

    /// <inheritdoc />
    protected override void BuildAfterElement(RenderTreeBuilder builder) => portal!.BuildAfterElement(builder);
}

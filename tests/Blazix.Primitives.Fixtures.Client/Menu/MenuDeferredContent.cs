using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Blazix.Primitives.Fixtures.Client.MenuFixtures;

/// <summary>
/// Renders nothing at first and its content in the render after it mounted: the port of MenuRoot.test.tsx's
/// <c>RenderNextMicrotask</c>, the items of a keyboard-opened menu arriving after the popup (MNU-EDGE-09).
/// </summary>
public sealed class MenuDeferredContent : ComponentBase
{
    private bool ready;

    /// <summary>Gets or sets the content rendered after the mount.</summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        if (ready)
        {
            builder.AddContent(0, ChildContent);
        }
    }

    /// <inheritdoc />
    protected override void OnAfterRender(bool firstRender)
    {
        if (firstRender)
        {
            ready = true;
            StateHasChanged();
        }
    }
}

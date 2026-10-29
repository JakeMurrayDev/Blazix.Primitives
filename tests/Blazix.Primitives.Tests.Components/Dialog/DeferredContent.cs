using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Blazix.Primitives.Tests.Components;

/// <summary>
/// DLG-EDGE-10 restated (design point 7 of M4 track 8): Base UI's lazily loaded child, as a component that renders its
/// fallback until an awaited <c>OnInitializedAsync</c> completes, then its content.
/// </summary>
public sealed class DeferredContent : ComponentBase
{
    private bool loaded;

    [Parameter]
    public Task? Loading { get; set; }

    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    protected override async Task OnInitializedAsync()
    {
        if (Loading is not null)
        {
            await Loading;
        }

        loaded = true;
    }

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        if (loaded)
        {
            builder.AddContent(0, ChildContent);
        }
        else
        {
            builder.AddContent(1, "Loading…");
        }
    }
}

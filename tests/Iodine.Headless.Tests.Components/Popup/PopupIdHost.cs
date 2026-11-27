using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Iodine.Headless.Tests.Components;

/// <summary>A test-only host that renders a popup tree for the popup id its parameter names (issue 344).</summary>
internal sealed class PopupIdHost : ComponentBase
{
    [Parameter]
    public RenderFragment<string?>? Tree { get; set; }

    [Parameter]
    public string? PopupId { get; set; }

    protected override void BuildRenderTree(RenderTreeBuilder builder) => builder.AddContent(0, Tree?.Invoke(PopupId));
}

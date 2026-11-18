using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Probe.Parts;

// ScrollAreaRoot.OverflowEdgeThreshold (ADR 08 Amendments 2026-11-18, the ScrollArea line). Elementless.
public sealed class ScrollAreaRoot : ComponentBase
{
    [Parameter]
    public OverflowEdgeThresholdValue? OverflowEdgeThreshold { get; set; }

    protected override void BuildRenderTree(RenderTreeBuilder builder) =>
        builder.AddContent(0, $"[ScrollAreaRoot OverflowEdgeThreshold={OverflowEdgeThreshold?.ToString() ?? "null"}]");
}

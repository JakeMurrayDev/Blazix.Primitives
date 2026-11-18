using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Probe.Parts;

// DrawerRoot's snap-point parameters (ADR 08 Amendments 2026-11-18, the Drawer line). Elementless; when SnapPointChanged
// is bound it invokes it once with the string "200px".
public sealed class DrawerRoot : ComponentBase
{
    private string delivered = "none";

    [Parameter]
    public IReadOnlyList<SnapPointValue>? SnapPoints { get; set; }

    [Parameter]
    public SnapPointValue? SnapPoint { get; set; }

    [Parameter]
    public EventCallback<SnapPointValue?> SnapPointChanged { get; set; }

    [Parameter]
    public SnapPointValue? DefaultSnapPoint { get; set; }

    protected override async Task OnInitializedAsync()
    {
        if (SnapPointChanged.HasDelegate)
        {
            await SnapPointChanged.InvokeAsync("200px");
            delivered = "string:200px";
        }
    }

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        string points = SnapPoints is null ? "null" : $"{SnapPoints.GetType().Name}[" + string.Join(", ", SnapPoints) + "]";
        builder.AddContent(0, $"[DrawerRoot SnapPoints={points} SnapPoint={SnapPoint?.ToString() ?? "null"} DefaultSnapPoint={DefaultSnapPoint?.ToString() ?? "null"} SnapPointChanged={(SnapPointChanged.HasDelegate ? "bound" : "none")} delivered={delivered}]");
    }
}

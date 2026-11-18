using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Blazix.Primitives;

// Base UI's Drawer.VirtualKeyboardProvider (drawer/virtual-keyboard-provider/DrawerVirtualKeyboardProvider.tsx); the
// contract is docs/components/drawer.md (§3.7, §6.3.1; DRW-STR-09, API-11). It renders no element and must be under a
// drawer root (DLG-EDGE-02's message under the Drawer name). Its behaviour is the `drawer` module's keyboard role on the
// Viewport handle (M6 track 8 design point 1 (a), settled by the orchestrator 2026-11-18), which 8c turns on; it has no twin.
/// <summary>
/// Keeps a focused field of a drawer visible above the on-screen keyboard: Base UI's <c>Drawer.VirtualKeyboardProvider</c>.
/// It renders no element.
/// </summary>
/// <remarks>
/// Place it anywhere inside a <see cref="DrawerRoot{TPayload}"/> whose popup is inside a <see cref="DrawerViewport"/>; the
/// viewport is where it measures the keyboard and sets <c>--drawer-keyboard-inset</c>.
/// </remarks>
public sealed class DrawerVirtualKeyboardProvider : ComponentBase
{
    [CascadingParameter]
    private DrawerRootContext? Root { get; set; }

    /// <summary>Gets or sets the content.</summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        if (Root is null)
        {
            throw DialogParts.MissingRoot("Drawer");
        }
    }

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.AddContent(0, ChildContent);
    }
}

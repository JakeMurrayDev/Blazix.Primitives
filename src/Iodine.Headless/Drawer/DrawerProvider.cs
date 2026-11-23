using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Blazix.Primitives;

// Base UI's Drawer.Provider (drawer/provider/DrawerProvider.tsx); the contract is docs/components/drawer.md (§3.1, §4.3;
// DRW-API-14, DRW-STA-14, DRW-STR-08). It renders no element, only the fixed cascade of its context (the DirectionProvider
// precedent, ADR 16 rule 9). No state record: Base UI's Drawer.Provider.State is empty.
/// <summary>
/// Tracks whether any drawer inside it is open, for the indent parts: Base UI's <c>Drawer.Provider</c>. It renders no
/// element.
/// </summary>
/// <remarks>
/// Place <see cref="DrawerIndent"/> and <see cref="DrawerIndentBackground"/> inside it to style the page behind an open
/// drawer.
/// </remarks>
public sealed class DrawerProvider : ComponentBase
{
    private readonly DrawerProviderContext context = new();
    private RenderFragment? cascadedContent;
    private RenderFragment? cascade;

    /// <summary>Gets or sets the content, which includes the drawers and the indent parts.</summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        if (cascade is null || !ReferenceEquals(cascadedContent, ChildContent))
        {
            cascadedContent = ChildContent;
            cascade = context.Cascade(cascadedContent);
        }
    }

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.AddContent(0, cascade);
    }
}

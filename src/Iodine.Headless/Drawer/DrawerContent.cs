using Iodine.Headless.Rendering;
using Microsoft.AspNetCore.Components;

namespace Iodine.Headless;

// Base UI's Drawer.Content (drawer/content/DrawerContent.tsx:14-26); the contract is docs/components/drawer.md (§3.12;
// DRW-STR-06, DOM-24, API-13). It requires a drawer root (DLG-EDGE-02's message under the Drawer name) and marks its subtree
// with data-drawer-content, which the `drawer` module's gesture reads (DRW-PTR-04).
/// <summary>
/// Represents a container for the drawer's content, Base UI's <c>Drawer.Content</c>, rendered as a <c>div</c>. Text inside
/// it stays selectable with a mouse or a pen while the drawer can be swiped.
/// </summary>
public sealed class DrawerContent : PartBase<EmptyState>
{
    /// <summary>Represents the marker attribute the content renders: <c>data-drawer-content</c>.</summary>
    internal const string ContentAttribute = "data-drawer-content";

    [CascadingParameter]
    private DrawerRootContext? Root { get; set; }

    /// <inheritdoc />
    protected override string DefaultTagName => "div";

    /// <inheritdoc />
    protected override EmptyState State => EmptyState.Instance;

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        if (Root is null)
        {
            throw DialogParts.MissingRoot("Drawer");
        }

        base.OnInitialized();
    }

    /// <inheritdoc />
    protected override void AddPartAttributes(AttributeList attributes) => attributes.Set(ContentAttribute, string.Empty);
}

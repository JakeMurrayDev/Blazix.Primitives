using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;

namespace Blazix.Primitives;

// Base UI's plain Menu.List (menu/list/MenuList.tsx); the contract is docs/components/menu.md §3.27 (MNU-STR-13, API-62,
// DOM-24, ARIA-05). A thin shell over MenuListBehavior.
/// <summary>
/// Groups a menu's items so other content can share the popup, Base UI's <c>Menu.List</c>, rendered as a <c>div</c> with the
/// <c>menu</c> role.
/// </summary>
public sealed class MenuList : PartBase<EmptyState>
{
    private MenuListBehavior? behavior;

    [CascadingParameter]
    private MenuRootContext? RootContext { get; set; }

    /// <inheritdoc />
    protected override string DefaultTagName => "div";

    /// <inheritdoc />
    protected override EmptyState State => EmptyState.Instance;

    /// <inheritdoc />
    protected override bool UsesGeneratedId => true;

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        behavior = new MenuListBehavior("Menu", StateHasChanged);
        behavior.Subscribe(RootContext);
        base.OnInitialized();
    }

    /// <inheritdoc />
    protected override void OnParametersSet() => behavior!.Apply(Id, AdditionalAttributes);

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            behavior?.Dispose();
        }

        base.Dispose(disposing);
    }

    /// <inheritdoc />
    protected override void AddPartAttributes(AttributeList attributes) => behavior!.AddAttributes(attributes);
}

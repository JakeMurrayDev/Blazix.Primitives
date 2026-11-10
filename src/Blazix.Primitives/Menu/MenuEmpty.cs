using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;

namespace Blazix.Primitives;

// Base UI's Menu.Empty (menu/empty/MenuEmpty.tsx, filter-dropdown/empty/FilterDropdownEmpty.tsx); the contract is
// docs/components/menu.md §3.24 (MNU-API-61, DOM-23, ARIA-16, STA-23; UTL-A11Y-01..04 through a DomTwin with LiveRegion).
// Interface commit: the acting bodies throw until their red-first commits.
/// <summary>
/// Represents the message a filterable menu shows when no item matches, Base UI's <c>Menu.Empty</c>, rendered as a
/// <c>div</c> live region only while nothing is visible.
/// </summary>
/// <remarks>It must be placed in a menu wrapped in <see cref="MenuFilterProvider"/>.</remarks>
public sealed class MenuEmpty : PartBase<EmptyState>
{
    [CascadingParameter]
    private MenuRootContext? RootContext { get; set; }

    /// <inheritdoc />
    protected override string DefaultTagName => "div";

    /// <inheritdoc />
    protected override EmptyState State => EmptyState.Instance;

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        _ = RootContext?.Filter ?? throw MenuParts.MissingFilter("Empty");
        throw new NotImplementedException();
    }
}

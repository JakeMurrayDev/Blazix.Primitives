using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Blazix.Primitives;

// Base UI's Menu.Empty (menu/empty/MenuEmpty.tsx, filter-dropdown/empty/FilterDropdownEmpty.tsx); the contract is
// docs/components/menu.md §3.24 (MNU-API-61, DOM-23, ARIA-16, STA-23). It decides only after its first render, once the
// items of the same batch have registered (FilterDropdownEmpty.tsx:25-31's ready flag), so its children never mount for
// a render while items match and it is absent from prerendered markup. The initial live-region text mutation
// (UTL-A11Y-01..04 through a DomTwin with LiveRegion) is still open.
/// <summary>
/// Represents the message a filterable menu shows when no item matches, Base UI's <c>Menu.Empty</c>, rendered as a
/// <c>div</c> live region only while nothing is visible.
/// </summary>
/// <remarks>It must be placed in a menu wrapped in <see cref="MenuFilterProvider"/>.</remarks>
public sealed class MenuEmpty : PartBase<EmptyState>
{
    private MenuFilterRootContext? filter;
    private IDisposable? subscription;
    private bool ready;

    [CascadingParameter]
    private MenuRootContext? RootContext { get; set; }

    /// <inheritdoc />
    protected override string DefaultTagName => "div";

    /// <inheritdoc />
    protected override EmptyState State => EmptyState.Instance;

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        // MNU-EDGE-04, STR-15.
        filter = RootContext?.Filter ?? throw MenuParts.MissingFilter("Empty");
        subscription = filter.Subscribe(() => _ = InvokeAsync(StateHasChanged));
        base.OnInitialized();
    }

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        Enabled = ready && filter!.Engine.IsEmpty;
        base.BuildRenderTree(builder);
    }

    /// <inheritdoc />
    protected override Task OnAfterRenderAsync(bool firstRender)
    {
        filter!.Flush();
        if (!ready)
        {
            ready = true;
            if (filter.Engine.IsEmpty)
            {
                StateHasChanged();
            }
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            subscription?.Dispose();
        }

        base.Dispose(disposing);
    }

    // MNU-DOM-23, ARIA-16.
    /// <inheritdoc />
    protected override void AddPartAttributes(AttributeList attributes)
    {
        attributes.Set("role", "status");
        attributes.Set("aria-live", "polite");
        attributes.Set("aria-atomic", "true");
    }
}

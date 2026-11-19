using Microsoft.AspNetCore.Components;

// Family tests use the project's root namespace.
namespace Blazix.Primitives.Tests.Components;

// store.test.tsx's `EarliestCommandProbe` (M6 track 11, issue 257): a descendant of the root that calls the store's open
// command from its first initialization, the earliest moment a Blazor descendant exists (Base UI's probe dispatches an
// ArrowDown from a ref callback during the first commit).
/// <summary>A descendant that requests an open from its first initialization.</summary>
internal sealed class AutocompleteEarliestCommandProbe : ComponentBase
{
    /// <summary>Gets a value indicating whether the probe has requested the open.</summary>
    public bool Dispatched { get; private set; }

    [CascadingParameter]
    private ComboboxStore? Root { get; set; }

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        if (Root is not null)
        {
            Dispatched = true;
            _ = Root.RequestOpenAsync(true, new OpenChangeEventDetails(EventReason.ListNavigation));
        }
    }
}

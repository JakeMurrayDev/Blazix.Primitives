using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;

namespace Blazix.Primitives;

// Base UI's Popover.Description (popover/description/PopoverDescription.tsx); the contract is docs/components/popover.md
// §3.10, POP-STA-09, POP-DOM-42, POP-ARIA-06; the PopoverTitle shape.
/// <summary>Represents a paragraph that describes a popover, Base UI's <c>Popover.Description</c>, rendered as a <c>p</c>.</summary>
public sealed class PopoverDescription : PartBase<EmptyState>
{
    private PopoverIdSync? sync;

    [CascadingParameter]
    private PopoverRootContext? RootContext { get; set; }

    /// <inheritdoc />
    protected override string DefaultTagName => "p";

    /// <inheritdoc />
    protected override EmptyState State => EmptyState.Instance;

    /// <inheritdoc />
    protected override bool UsesGeneratedId => true;

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">The description is not inside a <see cref="PopoverRoot{TPayload}"/>.</exception>
    protected override void OnInitialized()
    {
        sync = PopoverRootContext.Require(RootContext).CreateDescriptionSync();
        base.OnInitialized();
    }

    /// <inheritdoc />
    protected override void OnParametersSet() => sync!.Sync(Id);

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            sync?.Dispose();
        }

        base.Dispose(disposing);
    }
}

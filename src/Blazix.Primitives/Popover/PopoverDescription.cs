using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;

namespace Blazix.Primitives;

// Base UI's Popover.Description (popover/description/PopoverDescription.tsx); the contract is docs/components/popover.md
// §3.10, POP-STA-09, POP-DOM-42, POP-ARIA-06; the PopoverTitle shape.
/// <summary>Represents a paragraph that describes a popover, Base UI's <c>Popover.Description</c>, rendered as a <c>p</c>.</summary>
public sealed class PopoverDescription : PartBase<EmptyState>
{
    private string? published;

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
        PopoverRootContext.Require(RootContext);
        base.OnInitialized();
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        string? id = Id;
        if (!string.Equals(published, id, StringComparison.Ordinal))
        {
            published = id;
            RootContext!.SetDescriptionElementId(id);
        }
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing && published is not null && string.Equals(RootContext!.State.DescriptionElementId, published, StringComparison.Ordinal))
        {
            RootContext.SetDescriptionElementId(null);
        }

        base.Dispose(disposing);
    }
}

using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;

namespace Blazix.Primitives;

// Base UI's Popover.Title (popover/title/PopoverTitle.tsx); the contract is docs/components/popover.md §3.9, POP-STA-09,
// POP-DOM-42, POP-ARIA-05. The id it renders (the consumer's, else the generated one) is published in OnParametersSet
// (ADR 01 Amendments 2026-10-22: a parameter-set publication reaches the Popup in the same batch); the latest mount wins,
// and a disposed Title clears only its own id (§16 Q14 mirrored).
/// <summary>Represents a heading that labels a popover, Base UI's <c>Popover.Title</c>, rendered as an <c>h2</c>.</summary>
public sealed class PopoverTitle : PartBase<EmptyState>
{
    private string? published;

    [CascadingParameter]
    private PopoverRootContext? RootContext { get; set; }

    /// <inheritdoc />
    protected override string DefaultTagName => "h2";

    /// <inheritdoc />
    protected override EmptyState State => EmptyState.Instance;

    /// <inheritdoc />
    protected override bool UsesGeneratedId => true;

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">The title is not inside a <see cref="PopoverRoot{TPayload}"/>.</exception>
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
            RootContext!.SetTitleElementId(id);
        }
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing && published is not null && string.Equals(RootContext!.State.TitleElementId, published, StringComparison.Ordinal))
        {
            RootContext.SetTitleElementId(null);
        }

        base.Dispose(disposing);
    }
}

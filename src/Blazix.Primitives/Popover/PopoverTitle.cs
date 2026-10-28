using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;

namespace Blazix.Primitives;

// Base UI's Popover.Title (popover/title/PopoverTitle.tsx); the contract is docs/components/popover.md §3.9, POP-STA-09,
// POP-DOM-42, POP-ARIA-05. The id it renders (the consumer's, else the generated one) is published in OnParametersSet
// (ADR 01 Amendments 2026-10-22: a parameter-set publication reaches the Popup in the same batch); the latest mount wins,
// and a disposed Title clears only its own id (§16 Q14 mirrored), through SyncedValueWithCleanup (UTL-STA-20's
// fix of 2026-10-28 on main).
/// <summary>Represents a heading that labels a popover, Base UI's <c>Popover.Title</c>, rendered as an <c>h2</c>.</summary>
public sealed class PopoverTitle : PartBase<EmptyState>
{
    private PopoverIdSync? sync;

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
        sync = PopoverRootContext.Require(RootContext).CreateTitleSync();
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

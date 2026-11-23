using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;

namespace Blazix.Primitives;

// Base UI's Drawer.Title, the Dialog.Title component itself (drawer/title/DrawerTitle.tsx:12); the contract is
// docs/components/drawer.md (§3.13/§3.14; DRW-STR-03 over dialog.md §3.7, §5.6, §5.8). A sealed part over
// DialogLabelBehavior, which DialogTitle shares (ADR 08 Amendments 2026-10-27; decision 20 (a) of the M6 plan, chosen by the user 2026-11-18). Its id, given or generated, is the popup's
// aria-labelledby.
/// <summary>
/// Represents a heading that labels the drawer, Base UI's <c>Drawer.Title</c>, rendered as an <c>h2</c>. The
/// drawer's popup names it in <c>aria-labelledby</c>.
/// </summary>
public sealed class DrawerTitle : PartBase<EmptyState>
{
    private readonly DialogLabelBehavior behavior = new(description: false);

    [CascadingParameter]
    private IDialogRootContext? Context { get; set; }

    /// <inheritdoc />
    protected override string DefaultTagName => "h2";

    /// <inheritdoc />
    protected override EmptyState State => EmptyState.Instance;

    /// <inheritdoc />
    protected override bool UsesGeneratedId => true;

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        if (Context is null)
        {
            throw DialogParts.MissingRoot("Drawer");
        }

        base.OnInitialized();
    }

    /// <inheritdoc />
    protected override void OnParametersSet() => behavior.Apply(Context!, Id);

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            behavior.Dispose();
        }

        base.Dispose(disposing);
    }
}

using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;

namespace Blazix.Primitives;

// Base UI's Drawer.Description, the Dialog.Description component itself (drawer/description/DrawerDescription.tsx:12); the contract
// is docs/components/drawer.md (§3.13/§3.14; DRW-STR-03 over dialog.md §3.8, §5.6, §5.8). A sealed part over
// DialogLabelBehavior, which DialogDescription shares (ADR 08 Amendments 2026-10-27; decision 20 (a) of the M6 plan, chosen by the user 2026-11-18). Its id, given or generated, is the
// popup's aria-describedby.
/// <summary>
/// Represents a paragraph that describes the drawer, Base UI's <c>Drawer.Description</c>, rendered as a
/// <c>p</c>. The drawer's popup names it in <c>aria-describedby</c>.
/// </summary>
public sealed class DrawerDescription : PartBase<EmptyState>
{
    private readonly DialogLabelBehavior behavior = new(description: true);

    [CascadingParameter]
    private IDialogRootContext? Context { get; set; }

    /// <inheritdoc />
    protected override string DefaultTagName => "p";

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

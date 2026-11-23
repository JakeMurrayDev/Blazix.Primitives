using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;

namespace Blazix.Primitives;

// Base UI's AlertDialog.Description, the Dialog.Description component itself (alert-dialog/index.parts.ts:8); the contract
// is docs/components/alert-dialog.md (§3.3, ADL-STR-02 over dialog.md §3.8, §5.6, §5.8). A sealed part over
// DialogLabelBehavior, which DialogDescription shares (ADR 08 Amendments 2026-10-27). Its id, given or generated, is the
// popup's aria-describedby.
/// <summary>
/// Represents a paragraph that describes the alert dialog, Base UI's <c>AlertDialog.Description</c>, rendered as a
/// <c>p</c>. The alert dialog's popup names it in <c>aria-describedby</c>.
/// </summary>
public sealed class AlertDialogDescription : PartBase<EmptyState>
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
            throw DialogParts.MissingRoot("AlertDialog");
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

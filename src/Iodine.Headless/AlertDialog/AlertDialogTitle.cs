using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;

namespace Blazix.Primitives;

// Base UI's AlertDialog.Title, the Dialog.Title component itself (alert-dialog/index.parts.ts:7); the contract is
// docs/components/alert-dialog.md (§3.3, ADL-STR-02 over dialog.md §3.7, §5.6, §5.8). A sealed part over
// DialogLabelBehavior, which DialogTitle shares (ADR 08 Amendments 2026-10-27). Its id, given or generated, is the popup's
// aria-labelledby.
/// <summary>
/// Represents a heading that labels the alert dialog, Base UI's <c>AlertDialog.Title</c>, rendered as an <c>h2</c>. The
/// alert dialog's popup names it in <c>aria-labelledby</c>.
/// </summary>
public sealed class AlertDialogTitle : PartBase<EmptyState>
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

using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;

namespace Blazix.Primitives;

// Base UI's Dialog.Title (DLG §3.7 (dialog/title/DialogTitle.tsx)); the contract is docs/components/dialog.md (§5.6, §5.8). A thin part over
// DialogLabelBehavior, which the AlertDialog family shares (ADR 08 Amendments 2026-10-27). Its id, given or generated, is
// the popup's aria-labelledby.
/// <summary>
/// Represents a heading that labels the dialog, Base UI's <c>Dialog.Title</c>, rendered as an <c>h2</c>. The dialog's popup names it in
/// <c>aria-labelledby</c>.
/// </summary>
public sealed class DialogTitle : PartBase<EmptyState>
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
            throw DialogParts.MissingRoot("Dialog");
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

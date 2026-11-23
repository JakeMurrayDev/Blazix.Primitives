using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;

namespace Blazix.Primitives;

// Base UI's Dialog.Description (DLG §3.8 (dialog/description/DialogDescription.tsx)); the contract is docs/components/dialog.md (§5.6, §5.8). A thin part over
// DialogLabelBehavior, which the AlertDialog family shares (ADR 08 Amendments 2026-10-27). Its id, given or generated, is
// the popup's aria-describedby.
/// <summary>
/// Represents a paragraph that describes the dialog, Base UI's <c>Dialog.Description</c>, rendered as an <c>p</c>. The dialog's popup names it in
/// <c>aria-describedby</c>.
/// </summary>
public sealed class DialogDescription : PartBase<EmptyState>
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

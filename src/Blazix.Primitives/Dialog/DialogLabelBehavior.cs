namespace Blazix.Primitives;

// DLG-STA-09, DOM-23, ARIA-05/06 (dialog/title/DialogTitle.tsx:20-24, dialog/description/DialogDescription.tsx:20-24): a title
// or a description publishes its rendered id with SyncedValueWithCleanup (UTL-STA-20) in OnParametersSet, before the
// popup renders (ADR 01 Amendments 2026-10-22); the last one mounted wins, and unmounting clears it. Shared by every
// dialog family's Title and Description (ADR 08 Amendments 2026-10-27).
/// <summary>The behavior of a dialog-family title or description: the publication of its id.</summary>
internal sealed class DialogLabelBehavior : IDisposable
{
    private readonly bool description;
    private IDialogRootContext? context;
    private IDialogLabelSync? sync;

    /// <summary>Initializes a new instance of the <see cref="DialogLabelBehavior"/> class.</summary>
    /// <param name="description"><see langword="true"/> for a description; <see langword="false"/> for a title.</param>
    public DialogLabelBehavior(bool description)
    {
        this.description = description;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        sync?.Dispose();
        sync = null;
    }

    /// <summary>Publishes the id the part renders, at each parameter set.</summary>
    /// <param name="rootContext">The enclosing root's context.</param>
    /// <param name="id">The id the part renders.</param>
    public void Apply(IDialogRootContext rootContext, string? id)
    {
        if (!ReferenceEquals(context, rootContext))
        {
            sync?.Dispose();
            context = rootContext;
            sync = rootContext.CreateLabelSync(description);
        }

        sync!.Sync(id);
    }
}

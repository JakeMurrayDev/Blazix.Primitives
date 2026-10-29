namespace Blazix.Primitives;

// ADL-API-06 (alert-dialog/handle.ts:11-22), ADR 08 Amendments 2026-10-27 (chosen by the user 2026-10-27): the AlertDialog
// family's handle derives from DialogHandle, is created with `new` and adds no member. Base UI brands it with a type-only
// private field; C#'s nominal types separate the families one way (ADL-EDGE-02, ADL §14): an AlertDialog part rejects a
// DialogHandle, and a Dialog part accepts an AlertDialogHandle. Its fallback store runs in alert mode; its warnings name
// DialogHandle (PSH-EDGE-02).
/// <summary>
/// Connects an alert dialog's root with triggers rendered anywhere on the page, and opens and closes the alert dialog from
/// code.
/// </summary>
/// <typeparam name="TPayload">The type of the payload the alert dialog's triggers carry; <see cref="NoPayload"/> for none.</typeparam>
/// <remarks>
/// Create it with <see langword="new"/>, such as <c>new AlertDialogHandle&lt;NoPayload&gt;()</c>, and pass it to the alert
/// dialog's root and to each trigger through their <c>Handle</c> parameters. Its members are those of
/// <see cref="DialogHandle{TPayload}"/>.
/// </remarks>
public sealed class AlertDialogHandle<TPayload> : DialogHandle<TPayload>
{
    /// <summary>Initializes a new instance of the <see cref="AlertDialogHandle{TPayload}"/> class.</summary>
    public AlertDialogHandle()
        : base(DialogMode.AlertDialog)
    {
    }
}

namespace Blazix.Primitives;

// ADR 08 Amendments 2026-10-27 (AlertDialog as sealed parts over Dialog's behaviours, chosen by the user 2026-10-27): the
// family a dialog root belongs to, applied in the store sync (DLG §1's "alert mode"; ADL §3.5). Drawer joins in M6.
/// <summary>Specifies the family of a dialog root, which decides the values the family forces.</summary>
internal enum DialogMode
{
    /// <summary>A dialog: the root's parameters apply as given.</summary>
    Dialog,

    /// <summary>An alert dialog: always modal, never dismissed by a pointer, with the <c>alertdialog</c> role.</summary>
    AlertDialog,
}

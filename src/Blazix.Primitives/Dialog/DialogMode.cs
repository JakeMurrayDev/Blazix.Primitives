namespace Blazix.Primitives;

// ADR 08 Amendments 2026-10-27 (AlertDialog as sealed parts over Dialog's behaviors, chosen by the user 2026-10-27): the
// family a dialog root belongs to, applied in the store sync (DLG §1's "alert mode"; ADL §3.5). M6 track 8a (issue 279;
// decision 20 (a) of the M6 plan, chosen by the user 2026-11-18): Drawer, which forces nothing and is reported in the
// parent's drawer count (DLG-STA-05, DLG-DIS-10).
/// <summary>Specifies the family of a dialog root, which decides the values the family forces.</summary>
internal enum DialogMode
{
    /// <summary>A dialog: the root's parameters apply as given.</summary>
    Dialog,

    /// <summary>An alert dialog: always modal, never dismissed by a pointer, with the <c>alertdialog</c> role.</summary>
    AlertDialog,

    /// <summary>A drawer: the root's parameters apply as given, and an open drawer counts as a nested drawer of its parent.</summary>
    Drawer,
}

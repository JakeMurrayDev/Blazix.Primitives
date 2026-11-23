namespace Blazix.Primitives;

// DRW-API-12 (drawer/handle.ts:11-22), decision 20 (a) of the M6 plan (chosen by the user 2026-11-18; ADR 08 Amendments
// 2026-11-18): the Drawer family's handle derives from DialogHandle, is created with `new` and adds no member, the
// AlertDialogHandle precedent. Base UI brands it with a type-only private field; C#'s nominal types separate the families one
// way (ADL-EDGE-02's form): a Drawer part rejects a DialogHandle, and a Dialog part accepts a DrawerHandle. Its fallback
// store runs in drawer mode; its warnings name DialogHandle (PSH-EDGE-02).
/// <summary>
/// Connects a drawer's root with triggers rendered anywhere on the page, and opens and closes the drawer from code.
/// </summary>
/// <typeparam name="TPayload">The type of the payload the drawer's triggers carry; <see cref="NoPayload"/> for none.</typeparam>
/// <remarks>
/// Create it with <see langword="new"/>, such as <c>new DrawerHandle&lt;NoPayload&gt;()</c>, and pass it to the drawer's root
/// and to each trigger through their <c>Handle</c> parameters. Its members are those of <see cref="DialogHandle{TPayload}"/>.
/// </remarks>
public sealed class DrawerHandle<TPayload> : DialogHandle<TPayload>
{
    /// <summary>Initializes a new instance of the <see cref="DrawerHandle{TPayload}"/> class.</summary>
    public DrawerHandle()
        : base(DialogMode.Drawer)
    {
    }
}

namespace Blazix.Primitives;

// DLG §3.10 (dialog/store/DialogHandle.ts), ADR 08 Amendments 2026-10-27 (chosen by the user 2026-10-27: a per-family handle
// created with `new`): the shared handle with the Dialog family's OpenWithPayloadAsync (PSH-STA-39, DLG-API-25). Dialog
// opens unassociated, with a development warning, for an id no trigger has (PSH-EDGE-04, DLG-EDGE-06). AlertDialogHandle
// derives from it (track 9), so the family name in its diagnostics is a constructor argument.
/// <summary>
/// Connects a dialog's root with triggers rendered anywhere on the page, and opens and closes the dialog from code.
/// </summary>
/// <typeparam name="TPayload">The type of the payload the dialog's triggers carry; <see cref="NoPayload"/> for none.</typeparam>
/// <remarks>
/// <para>
/// Create it with <see langword="new"/>, such as <c>new DialogHandle&lt;NoPayload&gt;()</c>, and pass it to the dialog's
/// root and to each trigger through their <c>Handle</c> parameters.
/// </para>
/// <para>
/// <see cref="PopupHandle{TPayload}.OpenAsync"/> with an id that no trigger using this handle has opens the dialog without a
/// trigger. <see cref="OpenWithPayloadAsync"/> sets the payload the root renders, then opens the dialog without a trigger.
/// </para>
/// </remarks>
public class DialogHandle<TPayload> : PopupHandle<TPayload>
{
    private readonly DialogMode mode;

    /// <summary>Initializes a new instance of the <see cref="DialogHandle{TPayload}"/> class.</summary>
    public DialogHandle()
        : this(DialogMode.Dialog)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="DialogHandle{TPayload}"/> class for a dialog family.</summary>
    /// <param name="mode">The family of the roots that use the handle.</param>
    private protected DialogHandle(DialogMode mode)
        : base(DialogStore<TPayload>.NameOf(mode), throwOnMissingTrigger: false)
    {
        this.mode = mode;
    }

    /// <summary>Sets the payload the dialog renders, then opens the dialog without a trigger.</summary>
    /// <param name="payload">The payload the root's <c>PayloadContent</c> receives.</param>
    /// <returns>A task that completes when the open request has run.</returns>
    /// <remarks>
    /// The request carries the reason <see cref="EventReason.ImperativeAction"/> and no trigger, so no trigger is marked as
    /// open. While no root uses this handle, the call and its payload are ignored.
    /// </remarks>
    public Task OpenWithPayloadAsync(TPayload payload) => OpenWithPayloadCoreAsync(payload);

    /// <inheritdoc />
    private protected override IPopupStore<TPayload> CreateFallbackStore() =>
        new DialogStore<TPayload>(mode, floatingId: null, sessionToken: null, internalBackdropId: string.Empty, inert: true);
}

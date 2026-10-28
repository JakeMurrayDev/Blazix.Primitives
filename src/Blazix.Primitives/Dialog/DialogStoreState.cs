using Blazix.Primitives.Interop;
using Blazix.Primitives.Store;

namespace Blazix.Primitives;

// DLG §4.2 (dialog/store/DialogStore.ts:14-68), ADR 05 Amendments 2026-10-27 (the composed store record, chosen by the user
// 2026-10-27): Dialog's fields beside the shared popup state. Left out: the drawer count and the viewport element (M6's
// Drawer reads them), the three interaction prop bags and floatingRootContext (the `interactions` session owns them), and
// openMethod (PopupStoreState.OpenMethod). Added: BackdropElementId, the consumer Backdrop's rendered id, which the
// outside-press options name (Base UI holds the element in backdropRef).
/// <summary>Represents the state of a dialog's store: the shared popup state and the dialog's own fields.</summary>
/// <typeparam name="TPayload">The type of the payload the dialog's triggers carry.</typeparam>
/// <param name="Popup">The shared popup state.</param>
/// <param name="Modality">The effective modality regime.</param>
/// <param name="DisablePointerDismissal">Whether outside presses and focus leaving the popup never close it.</param>
/// <param name="Role">The popup's role: <c>dialog</c>, or <c>alertdialog</c> for an alert dialog.</param>
/// <param name="Nested">Whether the root is rendered inside another dialog-family root.</param>
/// <param name="NestedOpenDialogCount">The depth of the chain of open dialogs below this one, as last reported.</param>
/// <param name="TitleElementId">The rendered id of the mounted title, or <see langword="null"/> for none.</param>
/// <param name="DescriptionElementId">The rendered id of the mounted description, or <see langword="null"/> for none.</param>
/// <param name="BackdropElementId">The rendered id of the consumer backdrop, or <see langword="null"/> while none renders.</param>
[StoreState]
internal sealed partial record DialogStoreState<TPayload>(
    PopupStoreState<TPayload> Popup,
    DialogModality Modality,
    bool DisablePointerDismissal,
    string Role,
    bool Nested,
    int NestedOpenDialogCount,
    string? TitleElementId,
    string? DescriptionElementId,
    string? BackdropElementId) : IStoreState<DialogStoreState<TPayload>>, IPopupStoreState<DialogStoreState<TPayload>, TPayload>
{
    /// <summary>Represents the role of a dialog's popup.</summary>
    public const string DialogRole = "dialog";

    /// <summary>Represents the role of an alert dialog's popup.</summary>
    public const string AlertDialogRole = "alertdialog";

    /// <inheritdoc />
    public PopupStoreState<TPayload> Popup { get; init; } = Popup;

    // DLG-STA-09, the fields SyncedValueWithCleanup writes (UTL-STA-20).
    /// <summary>Gets the field of the title's id.</summary>
    public static StoreField<DialogStoreState<TPayload>, string?> TitleElementIdField { get; } =
        new(nameof(TitleElementId), static state => state.TitleElementId, static (state, value) => state with { TitleElementId = value });

    /// <summary>Gets the field of the description's id.</summary>
    public static StoreField<DialogStoreState<TPayload>, string?> DescriptionElementIdField { get; } =
        new(nameof(DescriptionElementId), static state => state.DescriptionElementId, static (state, value) => state with { DescriptionElementId = value });

    // DLG-STA-13.
    /// <summary>Gets a value indicating whether a nested dialog is open.</summary>
    public bool NestedDialogOpen => NestedOpenDialogCount > 0;

    // DLG-STA-07.
    /// <summary>Gets a value indicating whether no nested dialog is open, so this dialog handles Escape and outside presses.</summary>
    public bool Topmost => NestedOpenDialogCount == 0;

    // DLG-STA-01 (DialogStore.ts:113-135): the shared initial state with Dialog's defaults.
    /// <summary>Creates the initial state of a dialog's store.</summary>
    /// <param name="floatingId">The generated id of the popup element.</param>
    /// <returns>A closed, unmounted, modal, top-level dialog with no labels.</returns>
    public static DialogStoreState<TPayload> Initial(string? floatingId) => new(
        PopupStoreState<TPayload>.Initial(floatingId),
        Modality: DialogModality.Modal,
        DisablePointerDismissal: false,
        Role: DialogRole,
        Nested: false,
        NestedOpenDialogCount: 0,
        TitleElementId: null,
        DescriptionElementId: null,
        BackdropElementId: null);

    /// <inheritdoc />
    public static DialogStoreState<TPayload> WithPopup(DialogStoreState<TPayload> state, PopupStoreState<TPayload> popup) => state with { Popup = popup };
}

// ADR 05 §1: Dialog's named selectors over its record, beside the shared ones of PopupSelectors.
/// <summary>Provides the named selectors of a dialog's own store fields.</summary>
/// <typeparam name="TPayload">The type of the payload the dialog's triggers carry.</typeparam>
internal static class DialogSelectors<TPayload>
{
    /// <summary>Selects the modality regime.</summary>
    public static readonly Func<DialogStoreState<TPayload>, DialogModality> Modality = static state => state.Modality;

    /// <summary>Selects whether pointer dismissal is disabled.</summary>
    public static readonly Func<DialogStoreState<TPayload>, bool> DisablePointerDismissal = static state => state.DisablePointerDismissal;

    /// <summary>Selects the popup's role.</summary>
    public static readonly Func<DialogStoreState<TPayload>, string> Role = static state => state.Role;

    /// <summary>Selects whether the root is nested.</summary>
    public static readonly Func<DialogStoreState<TPayload>, bool> Nested = static state => state.Nested;

    /// <summary>Selects the nested open dialog count.</summary>
    public static readonly Func<DialogStoreState<TPayload>, int> NestedOpenDialogCount = static state => state.NestedOpenDialogCount;

    /// <summary>Selects the title's id.</summary>
    public static readonly Func<DialogStoreState<TPayload>, string?> TitleElementId = static state => state.TitleElementId;

    /// <summary>Selects the description's id.</summary>
    public static readonly Func<DialogStoreState<TPayload>, string?> DescriptionElementId = static state => state.DescriptionElementId;

    /// <summary>Selects the consumer backdrop's id.</summary>
    public static readonly Func<DialogStoreState<TPayload>, string?> BackdropElementId = static state => state.BackdropElementId;

    /// <summary>Selects the relay of the last committed open change.</summary>
    public static readonly Func<DialogStoreState<TPayload>, OpenChangeRelay?> OpenChange = static state => state.Popup.OpenChange;
}

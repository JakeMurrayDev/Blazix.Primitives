using Blazix.Primitives.Interop;
using Microsoft.AspNetCore.Components;

namespace Blazix.Primitives;

// DLG-STR-12, DLG-STA-03 (dialog/root/DialogRootContext.ts): what every dialog-family root provides to its parts and to a
// nested root, whatever its payload type (ADR 08 Amendments 2026-10-27: only the Root and the Trigger take TPayload, so the
// other parts are not generic). The root's store implements it and is the root's one cascade (ADR 16 rule 10; the store's
// cascade matches a parameter of this interface type, since a cascading value supplies any parameter its type is
// assignable to). A nested root reads its parent's as an optional cascade, so nesting crosses Dialog and AlertDialog.
/// <summary>Provides a dialog root's state and actions to its non-generic parts and to a nested dialog root.</summary>
internal interface IDialogRootContext
{
    /// <summary>Gets the family of the root.</summary>
    DialogMode Mode { get; }

    /// <summary>Gets the family's name in diagnostics, such as <c>Dialog</c>.</summary>
    string ComponentName { get; }

    /// <summary>Gets the session token of the dialog, never rendered.</summary>
    string? SessionToken { get; }

    /// <summary>Gets the id the internal backdrop renders.</summary>
    string InternalBackdropId { get; }

    /// <summary>Gets a value indicating whether the dialog is effectively open.</summary>
    bool Open { get; }

    /// <summary>Gets a value indicating whether the dialog's subtree is mounted.</summary>
    bool Mounted { get; }

    /// <summary>Gets the derived transition status.</summary>
    TransitionStatus? TransitionStatus { get; }

    /// <summary>Gets the number of times the unmount action ended a close.</summary>
    int UnmountRequests { get; }

    /// <summary>Gets a value indicating whether the root is nested in another dialog-family root.</summary>
    bool Nested { get; }

    /// <summary>Gets the depth of the chain of open dialogs below this one.</summary>
    int NestedOpenDialogCount { get; }

    /// <summary>Gets the effective modality regime.</summary>
    DialogModality Modality { get; }

    /// <summary>Gets a value indicating whether pointer dismissal is disabled.</summary>
    bool DisablePointerDismissal { get; }

    /// <summary>Gets the popup's role.</summary>
    string Role { get; }

    /// <summary>Gets the rendered id of the mounted title.</summary>
    string? TitleElementId { get; }

    /// <summary>Gets the rendered id of the mounted description.</summary>
    string? DescriptionElementId { get; }

    /// <summary>Gets the generated id of the popup element.</summary>
    string? FloatingId { get; }

    /// <summary>Gets the id of the active trigger while mounted.</summary>
    string? ActiveTriggerId { get; }

    /// <summary>Gets how the user opened the dialog, or <see langword="null"/> for a programmatic open.</summary>
    InteractionType? OpenMethod { get; }

    /// <summary>Gets the relay of the last committed open change.</summary>
    OpenChangeRelay? OpenChange { get; }

    /// <summary>Gets the snapshot of the event of the last committed close, or <see langword="null"/>.</summary>
    EventSnapshot? LastCloseEvent { get; }

    /// <summary>Gets the rendered id of the consumer backdrop, or <see langword="null"/> while none renders.</summary>
    string? BackdropElementId { get; }

    /// <summary>Gets the context of the dialog's portal while one is rendered.</summary>
    PopupPortalContext? Portal { get; }

    /// <summary>Gets a value indicating whether the dialog's portal keeps its subtree mounted while closed.</summary>
    bool PortalKeepMounted { get; }

    /// <summary>Subscribes a part to the fields it renders.</summary>
    /// <param name="fields">The fields.</param>
    /// <param name="changed">Called when at least one of <paramref name="fields"/> changed, such as the part's <c>StateHasChanged</c>.</param>
    /// <returns>The subscription; disposing it ends it.</returns>
    IDisposable Subscribe(DialogFields fields, Action changed);

    /// <summary>Registers a part that selects the open or mounted state and runs the render-batch boundary after its renders.</summary>
    /// <returns>The registration; disposing it unregisters the part.</returns>
    IDisposable RegisterBoundaryPart();

    /// <summary>Runs the work that waits for the end of a render batch.</summary>
    /// <returns>A task that completes when that work has run.</returns>
    Task AtBatchBoundaryAsync();

    /// <summary>Runs the open-change sequence for a requested open state.</summary>
    /// <param name="open">The requested open state.</param>
    /// <param name="details">The request's details.</param>
    /// <returns><see langword="true"/> if the change was applied.</returns>
    Task<bool> RequestOpenAsync(bool open, OpenChangeEventDetails details);

    /// <summary>Records the popup element and the id it renders.</summary>
    /// <param name="element">The popup element.</param>
    /// <param name="id">The id it renders.</param>
    void SetPopupElement(ElementReference? element, string? id);

    /// <summary>Forgets the popup element.</summary>
    void ClearPopupElement();

    /// <summary>Records the consumer backdrop's rendered id, or forgets it with <see langword="null"/>.</summary>
    /// <param name="id">The id.</param>
    void SetBackdropElementId(string? id);

    /// <summary>Mirrors the transition status the popup's transitions handle reported.</summary>
    /// <param name="status">The status.</param>
    void MirrorStatus(TransitionStatus? status);

    /// <summary>Applies a finished enter or exit transition of the popup element.</summary>
    /// <param name="opened">Whether the enter finished.</param>
    /// <returns>A task that completes when the callbacks have run.</returns>
    Task CompleteTransitionAsync(bool opened);

    /// <summary>Creates the synchronizer a title or a description publishes its id with.</summary>
    /// <param name="description"><see langword="true"/> for the description; <see langword="false"/> for the title.</param>
    /// <returns>The synchronizer; disposing it clears the id it published.</returns>
    IDialogLabelSync CreateLabelSync(bool description);

    /// <summary>Records the dialog's portal, or forgets it with <see langword="null"/>.</summary>
    /// <param name="portal">The portal's context.</param>
    /// <param name="keepMounted">The portal's <c>KeepMounted</c>.</param>
    void RegisterPortal(PopupPortalContext? portal, bool keepMounted);

    /// <summary>Applies a nested root's report of its open chain.</summary>
    /// <param name="dialogs">The depth the nested root reports: its own count plus one while open, 0 once closed.</param>
    /// <param name="drawers">The nested drawer count it reports; 0 until Drawer is ported.</param>
    void ReportNestedOpen(int dialogs, int drawers);
}

/// <summary>Publishes the rendered id of a dialog's title or description, and clears it on dispose.</summary>
internal interface IDialogLabelSync : IDisposable
{
    /// <summary>Publishes the id.</summary>
    /// <param name="id">The rendered id.</param>
    void Sync(string? id);
}

// ADR 05 §1: a part selects only what it renders, so it renders only when one of its fields changed.
/// <summary>Specifies the store fields a dialog part renders.</summary>
[Flags]
internal enum DialogFields
{
    /// <summary>No field.</summary>
    None = 0,

    /// <summary>The effective open state.</summary>
    Open = 1 << 0,

    /// <summary>Whether the subtree is mounted.</summary>
    Mounted = 1 << 1,

    /// <summary>The derived transition status.</summary>
    TransitionStatus = 1 << 2,

    /// <summary>The unmount requests.</summary>
    UnmountRequests = 1 << 3,

    /// <summary>Whether the root is nested.</summary>
    Nested = 1 << 4,

    /// <summary>The nested open dialog count.</summary>
    NestedOpenDialogCount = 1 << 5,

    /// <summary>The modality regime.</summary>
    Modality = 1 << 6,

    /// <summary>Whether pointer dismissal is disabled.</summary>
    DisablePointerDismissal = 1 << 7,

    /// <summary>The popup's role.</summary>
    Role = 1 << 8,

    /// <summary>The title's and the description's ids.</summary>
    Labels = 1 << 9,

    /// <summary>The popup id.</summary>
    PopupId = 1 << 10,

    /// <summary>The active trigger's id.</summary>
    ActiveTriggerId = 1 << 11,

    /// <summary>The open interaction type.</summary>
    OpenMethod = 1 << 12,

    /// <summary>The relay of the last committed open change.</summary>
    OpenChange = 1 << 13,

    /// <summary>The consumer backdrop's id.</summary>
    BackdropElementId = 1 << 14,
}

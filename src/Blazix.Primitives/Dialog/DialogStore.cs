using Blazix.Primitives.Interop;
using Blazix.Primitives.Store;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace Blazix.Primitives;

// DLG §4.2 (dialog/store/DialogStore.ts), ADR 05 Amendments 2026-10-27 (chosen by the user 2026-10-27): the store every
// dialog-family root owns, over the shared popup store, and the context its non-generic parts and a nested root read.
// - DLG-STA-15: the shared sequence (PopupStore.RequestOpenAsync) with Dialog's back-fill of a close's trigger (step 2,
//   EVD-EVT-16) in PrepareOpenChange; Dialog writes no instant phase.
// - DLG-EVT-05 (ADR 05 Amendments 2026-10-27, DLG §16 Q1 resolved as observed): PreventUnmountOnClose() is honored even
//   when the same callback cancels, and acts on the next close that is not canceled (UnmountPreventionAtCall).
// - DLG-STA-02: the root's props are synced in its OnParametersSet (ApplyRootSettings), with the mode's forced values
//   (alert mode: modal, no pointer dismissal, the alertdialog role; ADL §3.5).
// - DLG-STA-04..06: a nested root's report (ReportNestedOpen) is dropped while this dialog is neither open nor mounted;
//   the count resets once it is closed and unmounted (ResetNestedIfClosed, called from the root's commit path).
/// <summary>Holds the state of one dialog and runs its open-change sequence.</summary>
/// <typeparam name="TPayload">The type of the payload the dialog's triggers carry.</typeparam>
internal sealed class DialogStore<TPayload> : PopupStore<DialogStoreState<TPayload>, TPayload>, IDialogRootContext
{
    private EventSnapshot? lastCloseEvent;

    /// <summary>Initializes a new instance of the <see cref="DialogStore{TPayload}"/> class.</summary>
    /// <param name="mode">The family of the root.</param>
    /// <param name="floatingId">The generated id of the popup element.</param>
    /// <param name="sessionToken">The session token the root drew.</param>
    /// <param name="internalBackdropId">The id the internal backdrop renders.</param>
    /// <param name="inert">Whether this is a handle's fallback store.</param>
    /// <param name="logger">The root's logger, or <see langword="null"/>.</param>
    public DialogStore(DialogMode mode, string? floatingId, string? sessionToken, string internalBackdropId, bool inert = false, ILogger? logger = null)
        : base(Forced(mode, DialogStoreState<TPayload>.Initial(floatingId)), NameOf(mode), sessionToken, inert, logger)
    {
        Mode = mode;
        InternalBackdropId = internalBackdropId;
        UnmountPreventionAtCall = true;
    }

    /// <inheritdoc />
    public DialogMode Mode { get; }

    /// <inheritdoc />
    public string InternalBackdropId { get; }

    /// <inheritdoc />
    public bool Open => State.Popup.EffectiveOpen;

    /// <inheritdoc />
    public bool Mounted => State.Popup.Mounted;

    /// <inheritdoc />
    public TransitionStatus? TransitionStatus => State.Popup.DerivedTransitionStatus;

    /// <inheritdoc />
    public int UnmountRequests => State.Popup.UnmountRequests;

    /// <inheritdoc />
    public bool PreventUnmountingOnClose => State.Popup.PreventUnmountingOnClose;

    /// <inheritdoc />
    public bool Nested => State.Nested;

    /// <inheritdoc />
    public int NestedOpenDialogCount => State.NestedOpenDialogCount;

    /// <inheritdoc />
    public DialogModality Modality => State.Modality;

    /// <inheritdoc />
    public bool DisablePointerDismissal => State.DisablePointerDismissal;

    /// <inheritdoc />
    public string Role => State.Role;

    /// <inheritdoc />
    public string? TitleElementId => State.TitleElementId;

    /// <inheritdoc />
    public string? DescriptionElementId => State.DescriptionElementId;

    /// <inheritdoc />
    public string? FloatingId => State.Popup.FloatingId;

    /// <inheritdoc />
    public string? ActiveTriggerId => State.Popup.Mounted ? State.Popup.EffectiveActiveTriggerId : null;

    /// <inheritdoc />
    public InteractionType? OpenMethod => State.Popup.OpenMethod;

    /// <inheritdoc />
    public OpenChangeRelay? OpenChange => State.Popup.OpenChange;

    /// <inheritdoc />
    public EventSnapshot? LastCloseEvent => lastCloseEvent;

    /// <inheritdoc />
    public string? BackdropElementId => State.BackdropElementId;

    /// <inheritdoc />
    public PopupPortalContext? Portal { get; private set; }

    /// <inheritdoc />
    public bool PortalKeepMounted { get; private set; }

    /// <summary>Gets the family's name in diagnostics for a mode.</summary>
    /// <param name="mode">The mode.</param>
    /// <returns><c>Dialog</c> or <c>AlertDialog</c>.</returns>
    public static string NameOf(DialogMode mode) => mode == DialogMode.AlertDialog ? "AlertDialog" : "Dialog";

    // DLG-STA-02, DLG-API-03/06, ADL §3.5: the prop-derived fields, after the mode's overrides.
    /// <summary>Applies the root's modality, pointer dismissal and nesting, after the mode's forced values.</summary>
    /// <param name="modality">The root's modality regime.</param>
    /// <param name="disablePointerDismissal">The root's <c>DisablePointerDismissal</c>.</param>
    /// <param name="nested">Whether the root has a dialog-family ancestor.</param>
    public void ApplyRootSettings(DialogModality modality, bool disablePointerDismissal, bool nested)
    {
        DialogStoreState<TPayload> current = State;
        DialogStoreState<TPayload> next = Forced(Mode, current with { Modality = modality, DisablePointerDismissal = disablePointerDismissal, Nested = nested });
        if (next.Modality != current.Modality || next.DisablePointerDismissal != current.DisablePointerDismissal || next.Nested != current.Nested)
        {
            Write(next);
        }
    }

    /// <inheritdoc />
    public IDisposable Subscribe(DialogFields fields, Action changed)
    {
        ArgumentNullException.ThrowIfNull(changed);
        SelectorSubscription<DialogStoreState<TPayload>> subscription = new(this, changed);
        if ((fields & DialogFields.Open) != 0)
        {
            subscription.Select(PopupSelectors<DialogStoreState<TPayload>, TPayload>.Open);
        }

        if ((fields & DialogFields.Mounted) != 0)
        {
            subscription.Select(PopupSelectors<DialogStoreState<TPayload>, TPayload>.Mounted);
        }

        if ((fields & DialogFields.TransitionStatus) != 0)
        {
            subscription.Select(PopupSelectors<DialogStoreState<TPayload>, TPayload>.TransitionStatus);
        }

        if ((fields & DialogFields.UnmountRequests) != 0)
        {
            subscription.Select(PopupSelectors<DialogStoreState<TPayload>, TPayload>.UnmountRequests);
        }

        if ((fields & DialogFields.Nested) != 0)
        {
            subscription.Select(DialogSelectors<TPayload>.Nested);
        }

        if ((fields & DialogFields.NestedOpenDialogCount) != 0)
        {
            subscription.Select(DialogSelectors<TPayload>.NestedOpenDialogCount);
        }

        if ((fields & DialogFields.Modality) != 0)
        {
            subscription.Select(DialogSelectors<TPayload>.Modality);
        }

        if ((fields & DialogFields.DisablePointerDismissal) != 0)
        {
            subscription.Select(DialogSelectors<TPayload>.DisablePointerDismissal);
        }

        if ((fields & DialogFields.Role) != 0)
        {
            subscription.Select(DialogSelectors<TPayload>.Role);
        }

        if ((fields & DialogFields.Labels) != 0)
        {
            subscription.Select(DialogSelectors<TPayload>.TitleElementId);
            subscription.Select(DialogSelectors<TPayload>.DescriptionElementId);
        }

        if ((fields & DialogFields.PopupId) != 0)
        {
            subscription.Select(PopupSelectors<DialogStoreState<TPayload>, TPayload>.PopupId);
        }

        if ((fields & DialogFields.ActiveTriggerId) != 0)
        {
            subscription.Select(PopupSelectors<DialogStoreState<TPayload>, TPayload>.ActiveTriggerId);
        }

        if ((fields & DialogFields.OpenMethod) != 0)
        {
            subscription.Select(PopupSelectors<DialogStoreState<TPayload>, TPayload>.OpenMethod);
        }

        if ((fields & DialogFields.OpenChange) != 0)
        {
            subscription.Select(PopupSelectors<DialogStoreState<TPayload>, TPayload>.OpenChange);
        }

        if ((fields & DialogFields.BackdropElementId) != 0)
        {
            subscription.Select(DialogSelectors<TPayload>.BackdropElementId);
        }

        return subscription;
    }

    /// <inheritdoc />
    public void SetBackdropElementId(string? id)
    {
        if (!string.Equals(State.BackdropElementId, id, StringComparison.Ordinal))
        {
            Write(State with { BackdropElementId = id });
        }
    }

    // DLG-STR-03, DLG-EDGE-03 (dialog/portal/DialogPortalContext.ts): the portal the Viewport and the Popup must render in,
    // and its KeepMounted, held in the store's non-reactive context; the portal's parameter set precedes its subtree's.
    /// <inheritdoc />
    public void RegisterPortal(PopupPortalContext? portal, bool keepMounted)
    {
        Portal = portal;
        PortalKeepMounted = portal is not null && keepMounted;
    }

    /// <inheritdoc />
    public IDialogLabelSync CreateLabelSync(bool description) =>
        new LabelSync(this, description ? DialogStoreState<TPayload>.DescriptionElementIdField : DialogStoreState<TPayload>.TitleElementIdField);

    // DLG-STA-04/06 (useDialogRoot.ts:91-99): the last report wins, and a report arriving while this dialog is neither open
    // nor mounted is dropped, so a handle-backed root closed meanwhile carries no count into its next open.
    /// <inheritdoc />
    // The drawer count is Drawer's (M6): reported through the same call, not stored yet.
    public void ReportNestedOpen(int dialogs, int drawers)
    {
        PopupStoreState<TPayload> popup = State.Popup;
        if ((!popup.EffectiveOpen && !popup.Mounted) || State.NestedOpenDialogCount == dialogs)
        {
            return;
        }

        Write(State with { NestedOpenDialogCount = dialogs });
    }

    // DLG-STA-06 (useDialogRoot.ts:101-106).
    /// <summary>Resets the nested count once the dialog is closed and unmounted.</summary>
    public void ResetNestedIfClosed()
    {
        PopupStoreState<TPayload> popup = State.Popup;
        if (!popup.EffectiveOpen && !popup.Mounted && State.NestedOpenDialogCount != 0)
        {
            Write(State with { NestedOpenDialogCount = 0 });
        }
    }

    // DLG-STA-15 step 2 (DialogStore.ts:80-86), EVD-EVT-16: a close with no trigger names the active trigger's element, or no
    // trigger when that element is unknown.
    /// <inheritdoc />
    protected override void PrepareOpenChange(bool open, OpenChangeEventDetails details)
    {
        PopupStoreState<TPayload> popup = State.Popup;
        if (!open && details.Trigger is null && popup.EffectiveActiveTriggerId is { } activeId && popup.ActiveTriggerElement is { } active)
        {
            details.Trigger = new EventTrigger(activeId, active.Element);
        }
    }

    // FLT-FOC-10: the close type is classified from the close's event, which the focus manager reads from the commit path.
    /// <inheritdoc />
    protected override DialogStoreState<TPayload> ApplyOpenChange(DialogStoreState<TPayload> state, bool open, OpenChangeEventDetails details)
    {
        lastCloseEvent = open ? null : details.Event;
        return state;
    }

    // ADL §3.5 (useRenderDialogRoot.tsx:35-39): alert mode forces modal, no pointer dismissal and the alertdialog role.
    private static DialogStoreState<TPayload> Forced(DialogMode mode, DialogStoreState<TPayload> state)
    {
        if (mode != DialogMode.AlertDialog)
        {
            return state;
        }

        return state.Modality == DialogModality.Modal && state.DisablePointerDismissal && string.Equals(state.Role, DialogStoreState<TPayload>.AlertDialogRole, StringComparison.Ordinal)
            ? state
            : state with { Modality = DialogModality.Modal, DisablePointerDismissal = true, Role = DialogStoreState<TPayload>.AlertDialogRole };
    }

    // DLG-STA-09: a Title's or Description's id, reset when the part goes away (UTL-STA-20).
    private sealed class LabelSync(DialogStore<TPayload> store, StoreField<DialogStoreState<TPayload>, string?> field) : IDialogLabelSync
    {
        private readonly SyncedValueWithCleanup<DialogStoreState<TPayload>, string> synced = new(field);

        /// <inheritdoc />
        public void Dispose() => synced.Dispose();

        /// <inheritdoc />
        public void Sync(string? id) => synced.Sync(store, id);
    }
}

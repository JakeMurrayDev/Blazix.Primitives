using Blazix.Primitives.Interop;
using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace Blazix.Primitives;

// The root half every dialog-family root composes (useRenderDialogRoot, DialogInteractions; ADR 08 Amendments 2026-10-27,
// the Input/FieldControl precedent: DialogRoot and AlertDialogRoot are thin parts over it). It owns:
// - the popup root (PopupRootBehavior: the store at the first parameter set, the handle, the batch boundary, the fixed
//   cascade, which is also the IDialogRootContext the parts and a nested root read);
// - the `interactions` session on the store's token (ADR 04 Amendments 2026-10-27): Escape with the Prevent verdict and
//   the outside press with Dialog's predicates as data (DLG §11; design point 2 of M4 track 3, settled 2026-10-27);
// - the commit path (ADR 01 Amendments 2026-10-27): a subscription copies every committed change to the session outside a
//   render, resets the nested count once closed and unmounted, and reports this dialog's open chain to the parent root
//   (DLG-STA-05, design point 1 of M4 track 8, settled by the orchestrator 2026-10-28), so the parent's parts render in the
//   same batch.
// The topmost gate (DLG-STA-07, KEY-04, PTR-12) is the nested count, sent as EscapeKey and OutsidePress (design point 8 of
// M4 track 8, settled by the orchestrator 2026-10-28; ADR 04 Amendments 2026-10-28).
/// <summary>The behavior of a dialog-family root: its store, handle, session, dismissal and nested report.</summary>
/// <typeparam name="TPayload">The type of the payload the dialog's triggers carry.</typeparam>
internal sealed class DialogRootBehavior<TPayload> : IAsyncDisposable
{
    private const string Intentional = "intentional";
    private const string Sloppy = "sloppy";

    private static readonly InteractionsPressEvents s_intentional = new(Intentional, Intentional);
    private static readonly InteractionsPressEvents s_trapFocus = new(Sloppy, Sloppy);
    private static readonly InteractionsPressEvents s_nonModal = new(Intentional, Sloppy);

    private readonly IJSRuntime jsRuntime;
    private readonly Func<Exception, Task> dispatchException;
    private readonly PopupRootBehavior<DialogStore<TPayload>, DialogStoreState<TPayload>, TPayload> root;
    private readonly Action committedChanged;
    private PartIdGenerator? ids;
    private InteractionsSessionBehavior? session;
    private IDisposable? committed;
    private IDialogRootContext? parent;
    private int reported;
    private bool rendered;
    private bool disposed;
    private InteractionsDismissOptions? dismiss;
    private DismissKey dismissKey;

    /// <summary>Initializes a new instance of the <see cref="DialogRootBehavior{TPayload}"/> class.</summary>
    /// <param name="mode">The family of the root.</param>
    /// <param name="jsRuntime">The runtime the session's twin imports its module through.</param>
    /// <param name="requestRender">Asks the root to render, its <c>StateHasChanged</c>.</param>
    /// <param name="dispatchException">Routes an exception of a request to the root.</param>
    public DialogRootBehavior(DialogMode mode, IJSRuntime jsRuntime, Action requestRender, Func<Exception, Task> dispatchException)
    {
        this.jsRuntime = jsRuntime;
        this.dispatchException = dispatchException;
        root = new((popupId, token, logger) => new DialogStore<TPayload>(mode, popupId, token, ids!.Next(), logger: logger), requestRender);
        committedChanged = OnCommitted;
    }

    /// <summary>Gets the root's store; available from the first parameter set.</summary>
    public DialogStore<TPayload> Store => root.Store;

    /// <summary>Gets a task that completes when the session handle is attached.</summary>
    public Task SessionAttached => session?.TwinAttached ?? Task.CompletedTask;

    /// <summary>Renders the store's fixed cascade around the root's child content and payload template.</summary>
    /// <param name="builder">The root's render tree builder.</param>
    public void BuildRenderTree(RenderTreeBuilder builder) => root.BuildRenderTree(builder);

    /// <summary>Attaches the handle and the session, sends the session's changed options and runs the batch boundary.</summary>
    /// <returns>A task that completes when that work has run.</returns>
    /// <remarks>Call it from the root's <c>OnAfterRenderAsync</c>.</remarks>
    public async Task OnAfterRenderAsync()
    {
        rendered = true;
        CopySessionInputs();
        Task sync = session!.AfterRenderAsync();
        await root.OnAfterRenderAsync();
        await sync;
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;

        // DLG-STA-05: unmounting while open reports a closed chain; a closed root never reported.
        if (reported != 0)
        {
            reported = 0;
            parent?.ReportNestedOpen(0, 0);
        }

        committed?.Dispose();
        root.Dispose();
        if (session is not null)
        {
            await session.DisposeAsync();
        }
    }

    /// <summary>Creates the store at the first parameter set and applies the controlled open and trigger id.</summary>
    /// <param name="parameters">The root's parameters.</param>
    /// <param name="idGenerator">The scoped id generator.</param>
    /// <param name="logger">The root's logger.</param>
    /// <remarks>Call it from the root's <c>SetParametersAsync</c>, before the parameters are assigned.</remarks>
    public void SetParameters(ParameterView parameters, PartIdGenerator idGenerator, ILogger? logger)
    {
        ids = idGenerator;
        root.SetParameters(parameters, idGenerator, logger);
        if (session is null)
        {
            DialogStore<TPayload> store = root.Store;
            session = new InteractionsSessionBehavior(jsRuntime, store.SessionToken!, RouteAsync, dispatchException);
            committed = store.Subscribe(
                DialogFields.Open | DialogFields.Mounted | DialogFields.TransitionStatus | DialogFields.ActiveTriggerId | DialogFields.OpenChange
                    | DialogFields.PopupId | DialogFields.NestedOpenDialogCount | DialogFields.Modality | DialogFields.DisablePointerDismissal
                    | DialogFields.BackdropElementId,
                committedChanged);
        }
    }

    /// <summary>Applies the root's parameters to its store and its session.</summary>
    /// <param name="parameters">The shared popup root parameters.</param>
    /// <param name="modality">The root's modality regime, before the mode's overrides.</param>
    /// <param name="disablePointerDismissal">The root's <c>DisablePointerDismissal</c>, before the mode's overrides.</param>
    /// <param name="parentContext">The enclosing dialog-family root's context, or <see langword="null"/> at the top level.</param>
    /// <remarks>Call it from the root's <c>OnParametersSet</c>.</remarks>
    public void ApplyParameters(in PopupRootParameters<TPayload> parameters, DialogModality modality, bool disablePointerDismissal, IDialogRootContext? parentContext)
    {
        root.ApplyParameters(parameters);
        if (!ReferenceEquals(parent, parentContext))
        {
            if (reported != 0)
            {
                parent?.ReportNestedOpen(0, 0);
                reported = 0;
            }

            parent = parentContext;
        }

        // DLG-STA-02: earlier than Base UI's post-commit sync, so this render already reads the new values.
        root.Store.ApplyRootSettings(modality, disablePointerDismissal, nested: parentContext is not null);
        CopySessionInputs();
        ReportToParent();
    }

    // DLG-API-07, DIS-09: actionsRef.close().
    /// <summary>Closes the dialog with the reason <c>imperative-action</c> and no trigger.</summary>
    /// <returns>A task that completes when the request has run.</returns>
    public Task CloseAsync() => root.Store.CloseAsync();

    // DLG-API-07, ANI-05, TRN-ANI-19..22: actionsRef.unmount().
    /// <summary>Ends the closing phase now.</summary>
    /// <returns>A task that completes when the unmount cleanup has run.</returns>
    public Task UnmountAsync() => root.Store.UnmountAsync();

    private Task RouteAsync(OpenChangeRequest request) => root.Store.RequestOpenAsync(request.Open, request.Details);

    private void OnCommitted()
    {
        DialogStore<TPayload> store = root.Store;
        store.ResetNestedIfClosed();
        ReportToParent();
        if (rendered && !disposed)
        {
            CopySessionInputs();
            _ = session!.AfterRenderAsync();
        }
    }

    // DLG-STA-05 (useDialogRoot.ts:109-123): while open, the own count plus one; once closed, 0; never while closed before.
    private void ReportToParent()
    {
        if (parent is null || disposed)
        {
            return;
        }

        DialogStore<TPayload> store = root.Store;
        int next = store.Open ? store.NestedOpenDialogCount + 1 : 0;
        if (next != reported)
        {
            reported = next;
            parent.ReportNestedOpen(next, 0);
        }
    }

    private void CopySessionInputs()
    {
        DialogStore<TPayload> store = root.Store;
        PopupStoreState<TPayload> popup = store.Popup;
        session!.Open = popup.EffectiveOpen;
        session.ActiveTriggerId = popup.EffectiveActiveTriggerId;
        session.FloatingId = popup.PopupId;
        session.Ending = popup.DerivedTransitionStatus == TransitionStatus.Ending;
        session.Relay = popup.OpenChange;
        session.ParentToken = parent?.SessionToken;
        session.Dismiss = Dismiss(store);
    }

    // DLG-PTR-05..12, KEY-04 (useDialogRoot.ts:30-85): the event mode, the predicates as data, and the topmost gate.
    private InteractionsDismissOptions Dismiss(DialogStore<TPayload> store)
    {
        DialogStoreState<TPayload> state = store.State;
        bool internalBackdrop = state.Popup.Mounted && state.Modality == DialogModality.Modal;
        DismissKey key = new(state.Topmost, state.DisablePointerDismissal, state.Modality, internalBackdrop, state.BackdropElementId);
        if (dismiss is not null && key == dismissKey)
        {
            return dismiss;
        }

        dismissKey = key;
        bool hasBackdrop = internalBackdrop || state.BackdropElementId is not null;
        IReadOnlyList<string>? ownBackdrops = null;
        if (hasBackdrop && state.Modality != DialogModality.NonModal)
        {
            // DLG-PTR-10: a press on the internal backdrop, the consumer backdrop, or an ancestor of the popup that is
            // not a portal node (the module checks the last clause).
            ownBackdrops = internalBackdrop && state.BackdropElementId is { } both
                ? [store.InternalBackdropId, both]
                : [internalBackdrop ? store.InternalBackdropId : state.BackdropElementId!];
        }

        dismiss = new InteractionsDismissOptions
        {
            EscapeKey = state.Topmost ? null : false,
            OutsidePress = state.Topmost && !state.DisablePointerDismissal ? null : false,
            OutsidePressEvent = hasBackdrop ? s_intentional : state.Modality == DialogModality.TrapFocus ? s_trapFocus : s_nonModal,
            PrimaryButtonOnly = true,
            SingleTouch = true,
            OwnBackdropIds = ownBackdrops,
        };
        return dismiss;
    }

    private readonly record struct DismissKey(bool Topmost, bool DisablePointerDismissal, DialogModality Modality, bool InternalBackdrop, string? BackdropElementId);
}

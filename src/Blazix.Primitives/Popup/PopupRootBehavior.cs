using Blazix.Primitives.Rendering;
using Blazix.Primitives.Store;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.Logging;

namespace Blazix.Primitives;

// The root half of Base UI's popup layer (usePopupRootStore, PopupHandleAttachment, useImplicitActiveTrigger,
// useOpenStateTransitions, usePopupRootSync; PSH-STR-01/03/04, PSH-STA-01/16/18..25/40/41/44, PSH-API-09), composed by
// every popup family's Root and by the test-only hosts (ADR 16 rule 10: an internal class, not a component). The Root
// renders no element (the DirectionProvider precedent): it cascades its store, fixed (ADR 16 rule 9), around its child
// content and its payload template (ADR 08 Amendments 2026-10-27, issue 116's finding).
// - The store is created once, at the first parameter set, with the popup id and the session token drawn from
//   PartIdGenerator (PSH-STR-01; ADR 04 Amendments 2026-10-27: the token is never rendered). The store's creation, its fixed
//   cascade, the root's renders on BoundaryRequested and the boundary at OnAfterRender are PopupLifecycleRootBehavior's,
//   composed here (M5 track 2; ADR 05 Amendments 2026-11-04, settled by the orchestrator 2026-11-04).
// - The handle attaches at the Root's first OnAfterRender, before any descendant's (Blazor calls a batch's OnAfterRender
//   in render order, parent first: PSH-STR-04), and follows a change of the Handle parameter (PSH-STR-03).
// - The render-batch boundary (ADR 01 decision 3's amendment) runs the store's reconciliation and a pending unmount
//   action. The Root renders when the store asks for a boundary no part's render brings (BoundaryRequested): a registered
//   trigger disposed while registered, and every write that changes the reconciliation's inputs while no popup part that
//   selects the open or mounted state is registered (ADR 16 Amendments 2026-10-27, design point 8).
/// <summary>Owns a popup root's store, its handle attachment and its render-batch boundary.</summary>
/// <typeparam name="TStore">The type of the family's store.</typeparam>
/// <typeparam name="TState">The type of the family's store record.</typeparam>
/// <typeparam name="TPayload">The type of the payload the popup's triggers carry.</typeparam>
internal sealed class PopupRootBehavior<TStore, TState, TPayload> : IDisposable
    where TStore : PopupStore<TState, TPayload>
    where TState : class, IStoreState<TState>, IPopupStoreState<TState, TPayload>
{
    private readonly PopupLifecycleRootBehavior<TStore, TState> root;
    private readonly Action requestRender;
    private PopupHandle<TPayload>? handle;
    private PopupHandle<TPayload>? attachedHandle;
    private IDisposable? attachment;
    private RenderFragment? childContent;
    private RenderFragment<TPayload?>? payloadContent;
    private bool overlapCheckScheduled;

    /// <summary>Initializes a new instance of the <see cref="PopupRootBehavior{TStore, TState, TPayload}"/> class.</summary>
    /// <param name="createStore">Creates the family's store from the popup id, the session token and the root's logger.</param>
    /// <param name="requestRender">Asks the root to render, its <c>StateHasChanged</c>.</param>
    public PopupRootBehavior(Func<string, string, ILogger?, TStore> createStore, Action requestRender)
    {
        root = new PopupLifecycleRootBehavior<TStore, TState>(createStore, requestRender) { ChildContent = RenderContent };
        this.requestRender = requestRender;
    }

    /// <summary>Gets the root's store; available from the first parameter set.</summary>
    /// <exception cref="InvalidOperationException">The store is read before the root's first parameter set.</exception>
    public TStore Store => root.Store;

    /// <summary>Renders the store's fixed cascade around the root's child content and payload template.</summary>
    /// <param name="builder">The root's render tree builder.</param>
    public void BuildRenderTree(RenderTreeBuilder builder) => root.BuildRenderTree(builder);

    /// <summary>Attaches the handle, runs the render-batch boundary and the overlap check of the handle.</summary>
    /// <returns>A task that completes when the boundary's work has run.</returns>
    /// <remarks>Call it from the root's <c>OnAfterRenderAsync</c>, on every render, interactive or not.</remarks>
    public Task OnAfterRenderAsync()
    {
        TStore current = Store;
        if (overlapCheckScheduled)
        {
            overlapCheckScheduled = false;
            attachedHandle?.CheckOverlap();
        }

        if (!ReferenceEquals(attachedHandle, handle))
        {
            // PSH-STR-03: detach from the previous handle and attach to the new one in the same step.
            attachment?.Dispose();
            attachment = null;
            attachedHandle = handle;
            if (handle is not null)
            {
                attachment = handle.AttachStore(current);
                if (handle.OverlapCheckPending && DevelopmentWarnings.IsEnabled && current.Logger is not null)
                {
                    // PSH-EDGE-06 at the next render-batch boundary: this root's next render brings it.
                    overlapCheckScheduled = true;
                    requestRender();
                }
            }
        }

        return root.OnAfterRenderAsync();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        attachment?.Dispose();
        attachment = null;
        root.Dispose();
    }

    /// <summary>Creates the store at the root's first parameter set and applies the controlled open and trigger id.</summary>
    /// <param name="parameters">The root's parameters.</param>
    /// <param name="ids">The scoped id generator.</param>
    /// <param name="logger">The root's logger, or <see langword="null"/>.</param>
    /// <remarks>Call it from the root's <c>SetParametersAsync</c>, before the parameters are assigned.</remarks>
    public void SetParameters(ParameterView parameters, PartIdGenerator ids, ILogger? logger)
    {
        root.EnsureStore(ids, logger);
        TStore store = root.Store;
        store.OpenState.SetParameters(parameters, "Open", "DefaultOpen");
        store.TriggerIdState.SetParameters(parameters, "TriggerId", "DefaultTriggerId");
    }

    /// <summary>Applies the root's parameters to its store.</summary>
    /// <param name="parameters">The root's shared parameters.</param>
    /// <remarks>Call it from the root's <c>OnParametersSet</c>, after the parameters are assigned.</remarks>
    public void ApplyParameters(in PopupRootParameters<TPayload> parameters)
    {
        TStore current = Store;
        current.OnOpenChange = parameters.OnOpenChange;
        current.OpenChanged = parameters.OpenChanged;
        current.OnOpenChangeComplete = parameters.OnOpenChangeComplete;
        current.CloseOnActiveTriggerUnmount = parameters.CloseOnActiveTriggerUnmount;
        current.ApplyRootParameters(parameters.DefaultOpen, parameters.DefaultTriggerId);
        handle = parameters.Handle;
        childContent = parameters.ChildContent;
        if (!ReferenceEquals(payloadContent, parameters.PayloadContent))
        {
            if (payloadContent is null)
            {
                // The payload template renders the payload, so the root renders when it changes (PSH-API-07).
                root.Subscription.Select(PopupSelectors<TState, TPayload>.PayloadVersion);
            }

            payloadContent = parameters.PayloadContent;
        }
    }

    private void RenderContent(RenderTreeBuilder builder)
    {
        builder.AddContent(0, childContent);
        if (payloadContent is not null)
        {
            builder.AddContent(1, payloadContent, Store.Payload);
        }
    }
}

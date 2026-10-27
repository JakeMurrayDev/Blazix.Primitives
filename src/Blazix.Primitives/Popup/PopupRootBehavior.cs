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
//   PartIdGenerator (PSH-STR-01; ADR 04 Amendments 2026-10-27: the token is never rendered).
// - The handle attaches at the Root's first OnAfterRender, before any descendant's (Blazor calls a batch's OnAfterRender
//   in render order, parent first: PSH-STR-04), and follows a change of the Handle parameter (PSH-STR-03).
// - The render-batch boundary (ADR 01 decision 3's amendment) runs the store's reconciliation and a pending unmount
//   action. Which renders reach it is design point 8 of M4 track 1 (pending the user): the Root renders when the store asks
//   for a boundary no part's render brings (BoundaryRequested), and RendersOnReconciliationInputs makes it render on every
//   write that changes the reconciliation's inputs, the brief's wiring.
/// <summary>Owns a popup root's store, its handle attachment and its render-batch boundary.</summary>
/// <typeparam name="TStore">The type of the family's store.</typeparam>
/// <typeparam name="TState">The type of the family's store record.</typeparam>
/// <typeparam name="TPayload">The type of the payload the popup's triggers carry.</typeparam>
internal sealed class PopupRootBehavior<TStore, TState, TPayload> : IDisposable
    where TStore : PopupStore<TState, TPayload>
    where TState : class, IStoreState<TState>, IPopupStoreState<TState, TPayload>
{
    private readonly Func<string, string, ILogger?, TStore> createStore;
    private readonly Action requestRender;
    private readonly Action boundaryRequested;
    private readonly RenderFragment content;
    private TStore? store;
    private PopupHandle<TPayload>? handle;
    private PopupHandle<TPayload>? attachedHandle;
    private IDisposable? attachment;
    private SelectorSubscription<TState>? subscription;
    private RenderFragment? childContent;
    private RenderFragment<TPayload?>? payloadContent;
    private bool overlapCheckScheduled;
    private bool inputsSelected;

    /// <summary>Initializes a new instance of the <see cref="PopupRootBehavior{TStore, TState, TPayload}"/> class.</summary>
    /// <param name="createStore">Creates the family's store from the popup id, the session token and the root's logger.</param>
    /// <param name="requestRender">Asks the root to render, its <c>StateHasChanged</c>.</param>
    public PopupRootBehavior(Func<string, string, ILogger?, TStore> createStore, Action requestRender)
    {
        ArgumentNullException.ThrowIfNull(createStore);
        ArgumentNullException.ThrowIfNull(requestRender);
        this.createStore = createStore;
        this.requestRender = requestRender;
        boundaryRequested = requestRender;
        content = RenderContent;
    }

    /// <summary>Gets the root's store; available from the first parameter set.</summary>
    /// <exception cref="InvalidOperationException">The store is read before the root's first parameter set.</exception>
    public TStore Store => store ?? throw new InvalidOperationException("Blazix: the popup root's store is created at its first parameter set.");

    /// <summary>Gets or sets a value indicating whether the root renders on every write that changes the reconciliation's inputs.</summary>
    public bool RendersOnReconciliationInputs { get; set; }

    /// <summary>Renders the store's fixed cascade around the root's child content and payload template.</summary>
    /// <param name="builder">The root's render tree builder.</param>
    public void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.OpenComponent<CascadingValue<TStore>>(0);
        builder.AddComponentParameter(1, nameof(CascadingValue<TStore>.Value), Store);
        builder.AddComponentParameter(2, nameof(CascadingValue<TStore>.IsFixed), true);
        builder.AddComponentParameter(3, nameof(CascadingValue<TStore>.ChildContent), content);
        builder.CloseComponent();
    }

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

        return current.AtBatchBoundaryAsync();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        attachment?.Dispose();
        attachment = null;
        subscription?.Dispose();
        subscription = null;
        if (store is not null)
        {
            store.BoundaryRequested -= boundaryRequested;
        }
    }

    /// <summary>Creates the store at the root's first parameter set and applies the controlled open and trigger id.</summary>
    /// <param name="parameters">The root's parameters.</param>
    /// <param name="ids">The scoped id generator.</param>
    /// <param name="logger">The root's logger, or <see langword="null"/>.</param>
    /// <remarks>Call it from the root's <c>SetParametersAsync</c>, before the parameters are assigned.</remarks>
    public void SetParameters(ParameterView parameters, PartIdGenerator ids, ILogger? logger)
    {
        ArgumentNullException.ThrowIfNull(ids);
        if (store is null)
        {
            // PSH-STA-01's floatingId (useId) first, then the session token, both from the scoped counter.
            string popupId = ids.Next();
            string sessionToken = ids.Next();
            store = createStore(popupId, sessionToken, logger);
            store.BoundaryRequested += boundaryRequested;
            subscription = new SelectorSubscription<TState>(store, requestRender);
        }

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
            if (payloadContent is null && subscription is not null)
            {
                // The payload template renders the payload, so the root renders when it changes (PSH-API-07).
                subscription.Select(PopupSelectors<TState, TPayload>.PayloadVersion);
            }

            payloadContent = parameters.PayloadContent;
        }

        if (RendersOnReconciliationInputs && subscription is not null && !inputsSelected)
        {
            inputsSelected = true;
            subscription.Select(PopupSelectors<TState, TPayload>.Open);
            subscription.Select(PopupSelectors<TState, TPayload>.TriggerCount);
            subscription.Select(PopupSelectors<TState, TPayload>.ActiveTriggerId);
            subscription.Select(static state => state.Popup.ActiveTriggerElement);
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

using Blazix.Primitives.Rendering;
using Blazix.Primitives.Store;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.Logging;

namespace Blazix.Primitives;

// The root wiring of a popup built on the open lifecycle alone (ADR 05 Amendments 2026-11-03; M5 track 2 brief deliverable
// 1; ADR 16 rule 10: an internal class, not a component): Select's, Combobox's and NavigationMenu's Roots and the standalone
// test hosts compose it; PopupRootBehavior<TStore, TState, TPayload> composes it too and adds the handle attachment, the
// trigger id and the payload template, so the wiring exists once.
// - The store is created once, at the first parameter set, with the popup id and the session token drawn from
//   PartIdGenerator (PSH-STR-01; ADR 04 Amendments 2026-10-27: the token is never rendered).
// - The render-batch boundary (ADR 01 decision 3's amendment) runs at the root's OnAfterRender, and the root renders when
//   the store asks for a boundary no part's render brings (BoundaryRequested; ADR 16 Amendments 2026-10-27, design point 8).
/// <summary>Owns a popup root's lifecycle store, its fixed cascade and its render-batch boundary.</summary>
/// <typeparam name="TStore">The type of the family's store.</typeparam>
/// <typeparam name="TState">The type of the family's store record.</typeparam>
internal sealed class PopupLifecycleRootBehavior<TStore, TState> : IDisposable
    where TStore : PopupLifecycleStore<TState>
    where TState : class, IStoreState<TState>, IPopupLifecycleState<TState>
{
    private readonly Func<string, string, ILogger?, TStore> createStore;
    private readonly Action requestRender;
    private readonly RenderFragment content;
    private TStore? store;
    private SelectorSubscription<TState>? subscription;

    /// <summary>Initializes a new instance of the <see cref="PopupLifecycleRootBehavior{TStore, TState}"/> class.</summary>
    /// <param name="createStore">Creates the family's store from the popup id, the session token and the root's logger.</param>
    /// <param name="requestRender">Asks the root to render, its <c>StateHasChanged</c>.</param>
    public PopupLifecycleRootBehavior(Func<string, string, ILogger?, TStore> createStore, Action requestRender)
    {
        ArgumentNullException.ThrowIfNull(createStore);
        ArgumentNullException.ThrowIfNull(requestRender);
        this.createStore = createStore;
        this.requestRender = requestRender;
        content = RenderContent;
    }

    /// <summary>Gets the root's store; available from the first parameter set.</summary>
    /// <exception cref="InvalidOperationException">The store is read before the root's first parameter set.</exception>
    public TStore Store => store ?? throw new InvalidOperationException("Blazix: the popup root's store is created at its first parameter set.");

    /// <summary>Gets the subscription through which the root renders on the selections it makes.</summary>
    /// <exception cref="InvalidOperationException">The subscription is read before the root's first parameter set.</exception>
    public SelectorSubscription<TState> Subscription => subscription ?? throw new InvalidOperationException("Blazix: the popup root's store is created at its first parameter set.");

    /// <summary>Gets or sets the content the root renders inside its store's cascade.</summary>
    public RenderFragment? ChildContent { get; set; }

    /// <summary>Renders the store's fixed cascade around <see cref="ChildContent"/>.</summary>
    /// <param name="builder">The root's render tree builder.</param>
    public void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.OpenComponent<CascadingValue<TStore>>(0);
        builder.AddComponentParameter(1, nameof(CascadingValue<TStore>.Value), Store);
        builder.AddComponentParameter(2, nameof(CascadingValue<TStore>.IsFixed), true);
        builder.AddComponentParameter(3, nameof(CascadingValue<TStore>.ChildContent), content);
        builder.CloseComponent();
    }

    /// <summary>Runs the render-batch boundary.</summary>
    /// <returns>A task that completes when the boundary's work has run.</returns>
    /// <remarks>Call it from the root's <c>OnAfterRenderAsync</c>, on every render, interactive or not.</remarks>
    public Task OnAfterRenderAsync() => Store.AtBatchBoundaryAsync();

    /// <inheritdoc />
    public void Dispose()
    {
        subscription?.Dispose();
        subscription = null;
        if (store is not null)
        {
            store.BoundaryRequested -= requestRender;
        }
    }

    /// <summary>Creates the store at the root's first parameter set; later calls do nothing.</summary>
    /// <param name="ids">The scoped id generator.</param>
    /// <param name="logger">The root's logger, or <see langword="null"/>.</param>
    /// <remarks>Call it from the root's <c>SetParametersAsync</c>, before the parameters are assigned.</remarks>
    public void EnsureStore(PartIdGenerator ids, ILogger? logger)
    {
        ArgumentNullException.ThrowIfNull(ids);
        if (store is not null)
        {
            return;
        }

        // PSH-STA-01's floatingId (useId) first, then the session token, both from the scoped counter.
        string popupId = ids.Next();
        string sessionToken = ids.Next();
        store = createStore(popupId, sessionToken, logger);
        store.BoundaryRequested += requestRender;
        subscription = new SelectorSubscription<TState>(store, requestRender);
    }

    private void RenderContent(RenderTreeBuilder builder) => builder.AddContent(0, ChildContent);
}

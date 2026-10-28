using Blazix.Primitives.Rendering;
using Blazix.Primitives.Store;
using Microsoft.AspNetCore.Components;

namespace Blazix.Primitives;

// The trigger half of Base UI's popup layer (usePopupHandleStore, useTriggerRegistration, useTriggerDataForwarding;
// PSH-STR-02/05/06, PSH-API-08, PSH-STA-26..30, PSH-ALG-04/05, PSH-ARIA-01..04, PSH-EDGE-01), composed by every popup
// family's Trigger and by the test-only hosts (ADR 16 rule 10).
// - The store: the handle's exposed store when a Handle is given, else the cascaded one (PSH-STR-02); a store-pointer
//   change migrates the registration and the part's one subscription (PSH-STA-26/36; ADR 05 §1: one subscription at a
//   time, so the behavior owns it instead of PartBase.SubscribeTo).
// - Registration by id at parameter set (settled by the orchestrator 2026-10-27 under ADR 01 Amendments 2026-10-22), so a
//   batch's registrations precede its reconciliation; the identity is the part plus its element once rendered
//   (PopupTriggerElement), re-registered after the render that gives it an element.
// - The id is the one the part renders on its own element (the consumer's id, else the generated one): the lesson of
//   issue 115.
/// <summary>Resolves a popup trigger's store, registers the trigger in it, forwards its payload and renders its ARIA attributes.</summary>
/// <typeparam name="TStore">The type of the family's store.</typeparam>
/// <typeparam name="TState">The type of the family's store record.</typeparam>
/// <typeparam name="TPayload">The type of the payload the popup's triggers carry.</typeparam>
internal sealed class PopupTriggerBehavior<TStore, TState, TPayload> : IDisposable
    where TStore : PopupStore<TState, TPayload>
    where TState : class, IStoreState<TState>, IPopupStoreState<TState, TPayload>
{
    private readonly object owner;
    private readonly string partName;
    private readonly Action requestRender;
    private readonly Action<SelectorSubscription<TState>>? addSelections;
    private readonly Action storeChanged;
    private TStore? store;
    private PopupHandle<TPayload>? handle;
    private IDisposable? handleSubscription;
    private SelectorSubscription<TState>? subscription;
    private StoreSelection<TState, string?, bool>? openedByTrigger;
    private StoreSelection<TState, string?, bool>? mountedByTrigger;
    private StoreSelection<TState, string?, string?>? popupId;
    private TStore? registeredStore;
    private string? registeredId;
    private PopupTriggerElement registeredElement;
    private string? id;
    private ElementReference? element;
    private bool payloadSupplied;
    private TPayload? payload;
    private bool forwardedSupplied;
    private TPayload? forwardedPayload;
    private bool forwarded;

    /// <summary>Initializes a new instance of the <see cref="PopupTriggerBehavior{TStore, TState, TPayload}"/> class.</summary>
    /// <param name="owner">The trigger part, whose identity registers in the store.</param>
    /// <param name="partName">The part's name as a consumer writes it, such as <c>PopoverTrigger</c>, for its messages.</param>
    /// <param name="requestRender">Asks the part to render, its <c>StateHasChanged</c>.</param>
    /// <param name="addSelections">Adds the family's own selections to each subscription the behavior creates, or <see langword="null"/>.</param>
    public PopupTriggerBehavior(object owner, string partName, Action requestRender, Action<SelectorSubscription<TState>>? addSelections = null)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentException.ThrowIfNullOrEmpty(partName);
        this.partName = partName;
        ArgumentNullException.ThrowIfNull(requestRender);
        this.owner = owner;
        this.requestRender = requestRender;
        this.addSelections = addSelections;
        storeChanged = OnStoreChanged;
    }

    /// <summary>Gets the store the trigger reads: the handle's, else its root's.</summary>
    /// <exception cref="InvalidOperationException">The store is read before the trigger's first parameter set.</exception>
    public TStore Store => store ?? throw new InvalidOperationException("Blazix: a popup trigger's store is resolved at its first parameter set.");

    /// <summary>Gets the trigger id, the id the trigger renders.</summary>
    public string? TriggerId => id;

    // PSH-STA-08, PSH-DOM-01, PSH-ARIA-01.
    /// <summary>Gets a value indicating whether the popup is open and this trigger owns it.</summary>
    public bool IsOpenedByTrigger => openedByTrigger?.Value ?? false;

    // PSH-STA-09.
    /// <summary>Gets a value indicating whether the popup is mounted and this trigger owns it.</summary>
    public bool IsMountedByTrigger => mountedByTrigger?.Value ?? false;

    // PSH-STA-10, PSH-ARIA-02/03.
    /// <summary>Gets the popup id this trigger names in <c>aria-controls</c>, or <see langword="null"/>.</summary>
    public string? ControlsId => popupId?.Value;

    /// <summary>Gets the selector subscription of the current store, which the family adds selections to.</summary>
    public SelectorSubscription<TState>? Subscription => subscription;

    /// <summary>Re-registers the trigger once its element is known, forwards a changed payload and runs the batch boundary.</summary>
    /// <param name="element">The trigger's element after this render, or <see langword="null"/>.</param>
    /// <returns>A task that completes when the boundary's work has run.</returns>
    /// <remarks>Call it from the part's <c>OnAfterRenderAsync</c>.</remarks>
    public Task OnAfterRenderAsync(ElementReference? element)
    {
        TStore current = Store;
        if (!SameElement(this.element, element))
        {
            this.element = element;
            Register();
        }

        // PSH-STA-28/30: when the trigger comes to own the mounted popup, and while it owns it whenever a forwarded value
        // changed, the values are written post-commit; a trigger that lost the popup writes them again when it takes it
        // back (useTriggerDataForwarding re-runs on isMountedByThisTrigger; design point 13.8 of M4 track 6).
        if (!IsMountedByTrigger)
        {
            forwarded = false;
        }
        else if (registeredId is not null && (!forwarded || forwardedSupplied != payloadSupplied || !Equality.Is(forwardedPayload, payload)))
        {
            Forwarded();
            current.ForwardTriggerData(registeredElement, payloadSupplied, payload);
        }

        return current.AtBatchBoundaryAsync();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Unregister(disposing: true);
        handleSubscription?.Dispose();
        handleSubscription = null;
        subscription?.Dispose();
        subscription = null;
    }

    /// <summary>Resolves the store and registers the trigger, at each parameter set.</summary>
    /// <param name="cascadedStore">The store of the enclosing root, or <see langword="null"/>.</param>
    /// <param name="handle">The trigger's <c>Handle</c>, or <see langword="null"/>.</param>
    /// <param name="triggerId">The id the trigger renders.</param>
    /// <param name="element">The trigger's element from its last render, or <see langword="null"/>.</param>
    /// <param name="payloadSupplied">Whether the trigger's <c>Payload</c> was supplied.</param>
    /// <param name="triggerPayload">The trigger's <c>Payload</c>.</param>
    /// <param name="missingRootMessage">The family's message when there is neither a root nor a handle.</param>
    /// <exception cref="InvalidOperationException">There is neither an enclosing root nor a handle, or the handle is another family's.</exception>
    public void Apply(TStore? cascadedStore, PopupHandle<TPayload>? handle, string? triggerId, ElementReference? element, bool payloadSupplied, TPayload? triggerPayload, string missingRootMessage)
    {
        if (handle is null && cascadedStore is null)
        {
            // PSH-EDGE-01: thrown in every environment.
            throw new InvalidOperationException(missingRootMessage);
        }

        if (!ReferenceEquals(this.handle, handle))
        {
            handleSubscription?.Dispose();
            handleSubscription = handle?.SubscribeStore(storeChanged);
            this.handle = handle;
        }

        id = triggerId;
        this.element = element;
        this.payloadSupplied = payloadSupplied;
        payload = triggerPayload;
        UseStore(Resolve(cascadedStore));
        Register();
    }

    // PSH-ARIA-01..04.
    /// <summary>Adds the trigger's <c>aria-expanded</c>, <c>aria-controls</c> and <c>aria-haspopup</c>.</summary>
    /// <param name="attributes">The part's attributes.</param>
    /// <param name="ariaExpanded">Whether the family's trigger renders <c>aria-expanded</c> (Tooltip and PreviewCard do not).</param>
    /// <param name="ariaHasPopup">The family's fixed <c>aria-haspopup</c> value, or <see langword="null"/> for none.</param>
    public void AddAttributes(AttributeList attributes, bool ariaExpanded, string? ariaHasPopup)
    {
        ArgumentNullException.ThrowIfNull(attributes);
        if (ariaHasPopup is not null)
        {
            attributes.Set("aria-haspopup", ariaHasPopup);
        }

        if (ariaExpanded)
        {
            attributes.Set("aria-expanded", IsOpenedByTrigger ? "true" : "false");
        }

        if (ControlsId is { } controls)
        {
            attributes.Set("aria-controls", controls);
        }
    }

    private static bool SameElement(ElementReference? left, ElementReference? right)
    {
        return left is { } l
            ? right is { } r && string.Equals(l.Id, r.Id, StringComparison.Ordinal) && ReferenceEquals(l.Context, r.Context)
            : right is null;
    }

    private TStore Resolve(TStore? cascadedStore)
    {
        if (handle is null)
        {
            return cascadedStore!;
        }

        return handle.Store as TStore
            ?? throw new InvalidOperationException($"Blazix: the `Handle` of `{partName}` belongs to another popup family.");
    }

    private void UseStore(TStore next)
    {
        if (ReferenceEquals(store, next))
        {
            if (openedByTrigger is not null && !string.Equals(openedByTrigger.Arg, id, StringComparison.Ordinal))
            {
                openedByTrigger.Arg = id;
                mountedByTrigger!.Arg = id;
                popupId!.Arg = id;
            }

            return;
        }

        subscription?.Dispose();
        store = next;
        subscription = new SelectorSubscription<TState>(next, requestRender);
        openedByTrigger = subscription.Select(PopupSelectors<TState, TPayload>.IsOpenedByTrigger, id);
        mountedByTrigger = subscription.Select(PopupSelectors<TState, TPayload>.IsMountedByTrigger, id);
        popupId = subscription.Select(PopupSelectors<TState, TPayload>.TriggerPopupId, id);
        addSelections?.Invoke(subscription);
    }

    // PSH-ALG-04/05: a registration is the triple (store, id, element); a change of any of them unregisters from where it
    // was, then registers where it belongs, with the data a registering trigger applies.
    private void Register()
    {
        TStore current = Store;
        PopupTriggerElement next = new(owner, element);
        if (registeredId is not null && ReferenceEquals(registeredStore, current) && string.Equals(registeredId, id, StringComparison.Ordinal) && registeredElement == next)
        {
            return;
        }

        Unregister(disposing: false);
        if (id is null)
        {
            return;
        }

        registeredStore = current;
        registeredId = id;
        registeredElement = next;
        current.RegisterTrigger(id, next);
        current.ApplyTriggerData(id, next, payloadSupplied, payload);
        if (current.Popup.IsTriggerActive(id) && current.Popup.EffectiveOpen)
        {
            Forwarded();
        }
    }

    private void Unregister(bool disposing)
    {
        if (registeredStore is null || registeredId is null)
        {
            return;
        }

        TStore previous = registeredStore;
        string previousId = registeredId;
        registeredStore = null;
        registeredId = null;
        previous.UnregisterTrigger(previousId, registeredElement, disposing);
    }

    private void Forwarded()
    {
        forwarded = true;
        forwardedSupplied = payloadSupplied;
        forwardedPayload = payload;
    }

    // PSH-STA-36: the handle's exposed store changed; follow it and migrate the registration.
    private void OnStoreChanged()
    {
        if (handle is null)
        {
            return;
        }

        UseStore(Resolve(cascadedStore: null));
        Register();
        requestRender();
    }
}

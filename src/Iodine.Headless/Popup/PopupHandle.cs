using Microsoft.Extensions.Logging;

namespace Blazix.Primitives;

// PSH §3.1, PSH-ALG-06 (utils/popups/popupHandle.ts, BasePopupHandle); ADR 08 Amendments 2026-10-27 (chosen by the user
// 2026-10-27: PopupHandle<TPayload> with OpenAsync, CloseAsync and IsOpen; a <Family>Handle<TPayload> per family, created
// with `new`). The handle is a thin pointer holder (PSH §8): the attached-store stack, the store-pointer listeners
// (PSH-STA-34..38) and the inert fallback store, which the family creates. Diagnostics (design point 6 of M4 track 1,
// settled by the orchestrator 2026-10-27; ADR 02 Amendments 2026-10-27): a handle has no logger of its own and writes
// through the logger of the root it last attached to; a warning raised before any root ever attached is dropped (a dated
// note on PSH-EDGE-02). PSH-EDGE-06's "a frame later" check runs at the attaching root's next render-batch boundary (ADR 01
// Amendments 2026-10-27). The constructor is private protected: only the library's families derive from it.
/// <summary>
/// Connects a popup's root with triggers rendered anywhere on the page, and opens and closes the popup from code: Base UI's
/// popup handle.
/// </summary>
/// <typeparam name="TPayload">The type of the payload the popup's triggers carry.</typeparam>
/// <remarks>
/// <para>
/// Create a family's handle with <see langword="new"/>, such as <c>new PopoverHandle&lt;NoPayload&gt;()</c>, and pass it to
/// the popup's root and to each trigger through their <c>Handle</c> parameters. A trigger with a handle may be rendered
/// before or after the root, inside it or outside it.
/// </para>
/// <para>
/// <see cref="OpenAsync"/> and <see cref="CloseAsync"/> act on the root that uses this handle. While no root using it is
/// rendered, they are ignored; a root rendered again later starts closed, and a call made meanwhile is not replayed. Call
/// them from an event handler or after a render, not while rendering.
/// </para>
/// </remarks>
public abstract class PopupHandle<TPayload>
{
    private readonly List<IPopupStore<TPayload>> attachedStores = [];
    private readonly List<Action> storeListeners = [];
    private readonly string componentName;
    private readonly bool throwOnMissingTrigger;
    private IPopupStore<TPayload>? attachedStore;
    private IPopupStore<TPayload>? fallbackStore;
    private ILogger? logger;
    private bool overlapCheckPending;

    /// <summary>Initializes a new instance of the <see cref="PopupHandle{TPayload}"/> class.</summary>
    /// <param name="componentName">The family's name in diagnostics, such as <c>Popover</c>.</param>
    /// <param name="throwOnMissingTrigger">
    /// Whether <see cref="OpenAsync"/> throws when no trigger has the given id; an anchored popup throws, Dialog opens
    /// without a trigger instead.
    /// </param>
    private protected PopupHandle(string componentName, bool throwOnMissingTrigger)
    {
        ArgumentNullException.ThrowIfNull(componentName);
        this.componentName = componentName;
        this.throwOnMissingTrigger = throwOnMissingTrigger;
    }

    // PSH-API-05.
    /// <summary>Gets a value indicating whether the popup of the root that uses this handle is open.</summary>
    /// <value><see langword="true"/> if that popup is open; <see langword="false"/> while it is closed or no root uses this handle.</value>
    public bool IsOpen => attachedStore?.IsOpen ?? false;

    // PSH-STA-34: the store detached triggers read and register in.
    /// <summary>Gets the store the handle exposes: the most recently attached root's, or the inert fallback store.</summary>
    internal IPopupStore<TPayload> Store => attachedStore ?? FallbackStore;

    /// <summary>Gets the store a prerendered trigger reads, always the inert fallback store.</summary>
    internal IPopupStore<TPayload> ServerStore => FallbackStore;

    /// <summary>Gets the inert fallback store, created on first use.</summary>
    internal IPopupStore<TPayload> FallbackStore => fallbackStore ??= CreateFallbackStore();

    /// <summary>Gets a value indicating whether more than one root attached since the last overlap check.</summary>
    internal bool OverlapCheckPending => overlapCheckPending;

    // PSH-ALG-06 openByTrigger, PSH-STA-38, PSH-EVT-06, PSH-EDGE-02..05.
    /// <summary>Opens the popup of the root that uses this handle.</summary>
    /// <param name="triggerId">
    /// The id of a trigger that uses this handle, which the popup is anchored to and associated with; or
    /// <see langword="null"/> or the empty string to open without a trigger.
    /// </param>
    /// <returns>A task that completes when the open request has run.</returns>
    /// <exception cref="InvalidOperationException">
    /// <paramref name="triggerId"/> names no registered trigger and the popup is anchored to its trigger.
    /// </exception>
    /// <remarks>
    /// The request carries the reason <see cref="EventReason.ImperativeAction"/> and the trigger, so the root's
    /// <c>OnOpenChange</c> can cancel it. While no root uses this handle, the call is ignored.
    /// </remarks>
    public Task OpenAsync(string? triggerId = null)
    {
        IPopupStore<TPayload>? store = attachedStore;
        if (store is null)
        {
            Warn($"Blazix: `{componentName}Handle.OpenAsync()` was called while no root using this handle is mounted. The call was ignored; mount a root with this handle before opening it imperatively.");
            return Task.CompletedTask;
        }

        // During the batch in which a root attaches, a detached trigger may still be registered where it lived before:
        // the fallback store, or a previously attached root's during a transient overlap. Search them all, newest first.
        PopupTriggerElement? element = null;
        if (!string.IsNullOrEmpty(triggerId))
        {
            for (int i = attachedStores.Count - 1; i >= 0 && element is null; i--)
            {
                element = attachedStores[i].Triggers.GetById(triggerId);
            }

            element ??= FallbackStore.Triggers.GetById(triggerId);
            if (element is null)
            {
                if (throwOnMissingTrigger)
                {
                    throw new InvalidOperationException(
                        $"Blazix: `{componentName}Handle.OpenAsync()` was called with the trigger id \"{triggerId}\", but no matching trigger is registered with this handle. An anchored popup cannot open without a trigger to anchor to. Pass the id of a mounted `{componentName}Trigger` that has this handle set on its `Handle` parameter.");
                }

                Warn($"Blazix: `{componentName}Handle.OpenAsync()`: No trigger found with id \"{triggerId}\". The popup will open, but the trigger will not be associated with it.");
            }
        }

        EventTrigger? trigger = element is { } registered ? new EventTrigger(triggerId, registered.Element) : null;
        return store.RequestOpenAsync(true, new OpenChangeEventDetails(EventReason.ImperativeAction, trigger: trigger));
    }

    // PSH-ALG-06 closePopup, PSH-DIS-03.
    /// <summary>Closes the popup of the root that uses this handle.</summary>
    /// <returns>A task that completes when the close request has run.</returns>
    /// <remarks>
    /// The request carries the reason <see cref="EventReason.ImperativeAction"/> and no trigger. While no root uses this
    /// handle, the call is ignored.
    /// </remarks>
    public Task CloseAsync()
    {
        IPopupStore<TPayload>? store = attachedStore;
        if (store is null)
        {
            Warn($"Blazix: `{componentName}Handle.CloseAsync()` was called while no root using this handle is mounted. The call was ignored.");
            return Task.CompletedTask;
        }

        return store.RequestOpenAsync(false, new OpenChangeEventDetails(EventReason.ImperativeAction));
    }

    // PSH-STA-35/36: the stack of attached roots; detaching the newest restores the previous still-attached one.
    /// <summary>Points the handle at a root's store until the returned attachment is disposed.</summary>
    /// <param name="store">The root's store.</param>
    /// <returns>The attachment; disposing it detaches the store.</returns>
    internal IDisposable AttachStore(IPopupStore<TPayload> store)
    {
        ArgumentNullException.ThrowIfNull(store);
        attachedStores.Add(store);
        logger = store.Logger ?? logger;
        SetAttachedStore(store);
        if (attachedStores.Count > 1)
        {
            overlapCheckPending = true;
        }

        return new Attachment(this, store);
    }

    /// <summary>Adds a listener called synchronously whenever the exposed store changes.</summary>
    /// <param name="listener">The listener.</param>
    /// <returns>The subscription; disposing it removes the listener.</returns>
    internal IDisposable SubscribeStore(Action listener)
    {
        ArgumentNullException.ThrowIfNull(listener);
        storeListeners.Add(listener);
        return new Subscription(this, listener);
    }

    // PSH-EDGE-06 at the render-batch boundary (ADR 01 Amendments 2026-10-27): a transient overlap that resolved meanwhile
    // does not warn.
    /// <summary>Warns when more than one root is still attached, once per overlap.</summary>
    internal void CheckOverlap()
    {
        if (!overlapCheckPending)
        {
            return;
        }

        overlapCheckPending = false;
        if (attachedStores.Count > 1)
        {
            Warn("Blazix: A handle is attached to more than one mounted root at the same time. The most recently mounted root takes over and the previous one stops being controlled by the handle. A handle should be used by a single root that stays mounted for the lifetime of the handle.");
        }
    }

    // PSH-STA-39 (DialogHandle.ts:42-57): the payload, then a trigger-less open; the Dialog family's
    // OpenWithPayloadAsync(TPayload) calls it (track 8).
    /// <summary>Writes the payload to the attached root's store, then opens the popup without a trigger.</summary>
    /// <param name="payload">The payload.</param>
    /// <returns>A task that completes when the open request has run.</returns>
    internal Task OpenWithPayloadCoreAsync(TPayload payload)
    {
        IPopupStore<TPayload>? store = attachedStore;
        if (store is null)
        {
            Warn($"Blazix: `{componentName}Handle.OpenWithPayloadAsync()` was called while no root using this handle is mounted. The call and its payload were ignored; mount a root with this handle before opening it imperatively.");
            return Task.CompletedTask;
        }

        store.SetPayload(payload);
        return store.RequestOpenAsync(true, new OpenChangeEventDetails(EventReason.ImperativeAction));
    }

    /// <summary>Creates the inert fallback store detached triggers read while no root is attached.</summary>
    /// <returns>A store of the family, flagged inert.</returns>
    private protected abstract IPopupStore<TPayload> CreateFallbackStore();

    private void Detach(IPopupStore<TPayload> store)
    {
        int index = attachedStores.LastIndexOf(store);
        if (index >= 0)
        {
            attachedStores.RemoveAt(index);
        }

        SetAttachedStore(attachedStores.Count > 0 ? attachedStores[^1] : null);
    }

    private void SetAttachedStore(IPopupStore<TPayload>? store)
    {
        if (ReferenceEquals(attachedStore, store))
        {
            return;
        }

        attachedStore = store;
        // A listener may unsubscribe itself while being notified; iterate a snapshot.
        foreach (Action listener in storeListeners.ToArray())
        {
            listener();
        }
    }

    private void Warn(string message)
    {
        if (logger is not null && DevelopmentWarnings.IsEnabled)
        {
            DevelopmentWarnings.Write(logger, message);
        }
    }

    private sealed class Attachment(PopupHandle<TPayload> handle, IPopupStore<TPayload> store) : IDisposable
    {
        private bool disposed;

        /// <inheritdoc />
        public void Dispose()
        {
            if (!disposed)
            {
                disposed = true;
                handle.Detach(store);
            }
        }
    }

    private sealed class Subscription(PopupHandle<TPayload> handle, Action listener) : IDisposable
    {
        /// <inheritdoc />
        public void Dispose() => handle.storeListeners.Remove(listener);
    }
}

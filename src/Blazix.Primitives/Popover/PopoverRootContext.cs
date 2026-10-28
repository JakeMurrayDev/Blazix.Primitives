using Blazix.Primitives.Store;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace Blazix.Primitives;

// POP §11 (PopoverRootContext); popover/root/PopoverRootContext.ts. The Root cascades it fixed (ADR 16 rule 9) beside its
// store. Only the Root and the Trigger are generic (ADR 08 Amendments 2026-10-27, issue 116's finding), so every other part
// reads this non-generic base (the Tabs precedent, ADR 08 Amendments 2026-10-26): the state through IPopoverState, a
// selector subscription whose selections are the store's own (a selector over IPopoverState is a selector over the
// generic record by delegate variance), and the writes the parts make.
/// <summary>The state a popover root shares with the parts in its subtree, independent of its payload type.</summary>
internal abstract class PopoverRootContext
{
    /// <summary>Gets the popover's current state.</summary>
    public abstract IPopoverState State { get; }

    /// <summary>Gets the session token of the popover, which its browser-side behaviors join; never rendered.</summary>
    public abstract string? SessionToken { get; }

    /// <summary>Gets the root's logger, or <see langword="null"/>.</summary>
    public abstract ILogger? Logger { get; }

    /// <summary>Gets a value indicating whether the root was given a handle.</summary>
    public abstract bool HasHandle { get; }

    /// <summary>Throws the error of a popover part rendered outside a popover root.</summary>
    /// <param name="context">The cascaded context, or <see langword="null"/>.</param>
    /// <returns>The context.</returns>
    /// <exception cref="InvalidOperationException"><paramref name="context"/> is <see langword="null"/>.</exception>
    public static PopoverRootContext Require(PopoverRootContext? context)
    {
        // POP-EDGE-02: thrown in every build (ADR 02 Amendments 2026-10-20).
        return context ?? throw new InvalidOperationException(
            "Blazix: The `PopoverRoot` context is missing. Popover parts must be placed within `<PopoverRoot>`.");
    }

    /// <summary>Subscribes a part to the popover's state.</summary>
    /// <param name="changed">Called when one of the subscription's selections changed, the part's <c>StateHasChanged</c>.</param>
    /// <returns>The subscription; the part disposes it with itself.</returns>
    public abstract PopoverSubscription Subscribe(Action changed);

    /// <summary>Registers a part that selects the open or mounted state and brings the render-batch boundary.</summary>
    /// <returns>The registration; disposing it unregisters the part.</returns>
    public abstract IDisposable RegisterBoundaryPart();

    /// <summary>Runs the store's render-batch boundary work.</summary>
    /// <returns>A task that completes when it has run.</returns>
    public abstract Task AtBatchBoundaryAsync();

    /// <summary>Runs the popover's open-change sequence.</summary>
    /// <param name="open">The requested open state.</param>
    /// <param name="details">The request's details.</param>
    /// <returns><see langword="true"/> if the change was applied.</returns>
    public abstract Task<bool> RequestOpenAsync(bool open, OpenChangeEventDetails details);

    /// <summary>Records the popup element and the id it renders.</summary>
    /// <param name="element">The popup element.</param>
    /// <param name="id">The id it renders.</param>
    public abstract void SetPopupElement(ElementReference? element, string? id);

    /// <summary>Forgets the popup element.</summary>
    public abstract void ClearPopupElement();

    /// <summary>Records the positioner element.</summary>
    /// <param name="element">The positioner element, or <see langword="null"/>.</param>
    public abstract void SetPositionerElement(ElementReference? element);

    /// <summary>Mirrors the popup's reported transition status.</summary>
    /// <param name="status">The status.</param>
    public abstract void MirrorStatus(TransitionStatus? status);

    /// <summary>Applies a finished enter or exit transition of the popup.</summary>
    /// <param name="opened">Whether the enter finished.</param>
    /// <returns>A task that completes when the callbacks have run.</returns>
    public abstract Task CompleteTransitionAsync(bool opened);

    /// <summary>Writes whether focus is trapped in the popup.</summary>
    /// <param name="focusManagerModal">Whether focus is trapped.</param>
    public abstract void SetFocusManagerModal(bool focusManagerModal);

    /// <summary>Writes the title's id.</summary>
    /// <param name="id">The id, or <see langword="null"/>.</param>
    public abstract void SetTitleElementId(string? id);

    /// <summary>Writes the description's id.</summary>
    /// <param name="id">The id, or <see langword="null"/>.</param>
    public abstract void SetDescriptionElementId(string? id);

    /// <summary>Writes whether a viewport is mounted.</summary>
    /// <param name="adaptiveOrigin">Whether a viewport is mounted.</param>
    public abstract void SetAdaptiveOrigin(bool adaptiveOrigin);

    /// <summary>Clears the instant type as the popover starts moving between triggers.</summary>
    public abstract void BeginTriggerChange();

    /// <summary>Writes the trigger-change instant type once the move has finished, if still open.</summary>
    public abstract void CompleteTriggerChange();

    /// <summary>Drops a stored trigger-change instant type once closed.</summary>
    public abstract void ClearTriggerChangeWhenClosed();

    /// <summary>Gets the element of a registered trigger.</summary>
    /// <param name="triggerId">The trigger's id.</param>
    /// <returns>The trigger's element, or <see langword="null"/> when no rendered trigger has that id.</returns>
    public abstract ElementReference? TriggerElement(string? triggerId);
}

/// <summary>A part's subscription to a popover's state, independent of its payload type.</summary>
internal abstract class PopoverSubscription : IDisposable
{
    /// <inheritdoc />
    public abstract void Dispose();

    /// <summary>Adds a selection.</summary>
    /// <typeparam name="TValue">The type of the selected value.</typeparam>
    /// <param name="selector">The selector.</param>
    /// <returns>The selection, whose value follows the state.</returns>
    public abstract PopoverSelection<TValue> Select<TValue>(Func<IPopoverState, TValue> selector);

    /// <summary>Adds a selection with an argument.</summary>
    /// <typeparam name="TArg">The type of the argument.</typeparam>
    /// <typeparam name="TValue">The type of the selected value.</typeparam>
    /// <param name="selector">The selector.</param>
    /// <param name="arg">The argument.</param>
    /// <returns>The selection.</returns>
    public abstract PopoverSelection<TValue> Select<TArg, TValue>(Func<IPopoverState, TArg, TValue> selector, TArg arg);
}

/// <summary>A selected value of a popover's state.</summary>
/// <typeparam name="TValue">The type of the value.</typeparam>
internal abstract class PopoverSelection<TValue>
{
    /// <summary>Gets the value for the current state.</summary>
    public abstract TValue Value { get; }
}

/// <summary>The context of a popover root whose triggers carry <typeparamref name="TPayload"/>.</summary>
/// <typeparam name="TPayload">The type of the payload.</typeparam>
/// <param name="store">The root's store.</param>
internal sealed class PopoverRootContext<TPayload>(PopoverStore<TPayload> store) : PopoverRootContext
{
    /// <summary>Gets the root's store.</summary>
    public PopoverStore<TPayload> Store => store;

    /// <inheritdoc />
    public override IPopoverState State => store.State;

    /// <inheritdoc />
    public override string? SessionToken => store.SessionToken;

    /// <inheritdoc />
    public override ILogger? Logger => store.Logger;

    /// <summary>Gets or sets a value indicating whether the root was given a handle.</summary>
    public bool Handled { get; set; }

    /// <inheritdoc />
    public override bool HasHandle => Handled;

    /// <inheritdoc />
    public override PopoverSubscription Subscribe(Action changed) => new Subscription(store, changed);

    /// <inheritdoc />
    public override IDisposable RegisterBoundaryPart() => store.RegisterBoundaryPart();

    /// <inheritdoc />
    public override Task AtBatchBoundaryAsync() => store.AtBatchBoundaryAsync();

    /// <inheritdoc />
    public override Task<bool> RequestOpenAsync(bool open, OpenChangeEventDetails details) => store.RequestOpenAsync(open, details);

    /// <inheritdoc />
    public override void SetPopupElement(ElementReference? element, string? id) => store.SetPopupElement(element, id);

    /// <inheritdoc />
    public override void ClearPopupElement() => store.ClearPopupElement();

    /// <inheritdoc />
    public override void SetPositionerElement(ElementReference? element) => store.SetPositionerElement(element);

    /// <inheritdoc />
    public override void MirrorStatus(TransitionStatus? status) => store.MirrorStatus(status);

    /// <inheritdoc />
    public override Task CompleteTransitionAsync(bool opened) => store.CompleteTransitionAsync(opened);

    /// <inheritdoc />
    public override void SetFocusManagerModal(bool focusManagerModal) => store.SetFocusManagerModal(focusManagerModal);

    /// <inheritdoc />
    public override void SetTitleElementId(string? id) => store.SetTitleElementId(id);

    /// <inheritdoc />
    public override void SetDescriptionElementId(string? id) => store.SetDescriptionElementId(id);

    /// <inheritdoc />
    public override void SetAdaptiveOrigin(bool adaptiveOrigin) => store.SetAdaptiveOrigin(adaptiveOrigin);

    /// <inheritdoc />
    public override void BeginTriggerChange() => store.BeginTriggerChange();

    /// <inheritdoc />
    public override void CompleteTriggerChange() => store.CompleteTriggerChange();

    /// <inheritdoc />
    public override void ClearTriggerChangeWhenClosed() => store.ClearTriggerChangeWhenClosed();

    /// <inheritdoc />
    public override ElementReference? TriggerElement(string? triggerId) => store.Triggers.GetById(triggerId)?.Element;

    private sealed class Subscription(PopoverStore<TPayload> store, Action changed) : PopoverSubscription
    {
        private readonly SelectorSubscription<PopoverStoreState<TPayload>> inner = new(store, changed);

        /// <inheritdoc />
        public override void Dispose() => inner.Dispose();

        /// <inheritdoc />
        public override PopoverSelection<TValue> Select<TValue>(Func<IPopoverState, TValue> selector) => new Selection<TValue>(inner.Select<TValue>(selector));

        /// <inheritdoc />
        public override PopoverSelection<TValue> Select<TArg, TValue>(Func<IPopoverState, TArg, TValue> selector, TArg arg) => new ArgSelection<TArg, TValue>(inner.Select<TArg, TValue>(selector, arg));
    }

    private sealed class Selection<TValue>(StoreSelection<PopoverStoreState<TPayload>, TValue> inner) : PopoverSelection<TValue>
    {
        /// <inheritdoc />
        public override TValue Value => inner.Value;
    }

    private sealed class ArgSelection<TArg, TValue>(StoreSelection<PopoverStoreState<TPayload>, TArg, TValue> inner) : PopoverSelection<TValue>
    {
        /// <inheritdoc />
        public override TValue Value => inner.Value;
    }
}

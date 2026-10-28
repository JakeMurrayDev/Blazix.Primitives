using Blazix.Primitives.Interop;
using Blazix.Primitives.Store;
using Microsoft.AspNetCore.Components;

namespace Blazix.Primitives;

// tooltip/root/TooltipRootContext.ts:9-20 (TIP-STR-11, TIP-EDGE-02). The Root and the Trigger are generic over the payload
// (ADR 08 Amendments 2026-10-27, issue 116's finding); the Portal, Positioner, Popup, Arrow and Viewport are not, so the Root
// cascades this non-generic view of its store, fixed (ADR 16 rule 9). A part takes one view, the selections it reads, and
// is rendered when one of them changed (ADR 05 §1's one subscription per part).
/// <summary>The non-generic view of a tooltip root's store that the parts below the root read.</summary>
internal abstract class TooltipRootContext
{
    // TIP-EDGE-02 (tooltip/root/TooltipRootContext.ts:11-19), with the port's prefix and code quoting (issue 121).
    /// <summary>The message thrown when a tooltip part is placed outside a <c>TooltipRoot</c>.</summary>
    internal const string MissingMessage = "Blazix: `TooltipRootContext` is missing. Tooltip parts must be placed within `<TooltipRoot>`.";

    /// <summary>Gets the session token the tooltip's browser-side behaviors share; it is never rendered.</summary>
    public abstract string SessionToken { get; }

    /// <summary>Gets the popup's generated id.</summary>
    public abstract string? FloatingId { get; }

    /// <summary>Returns <paramref name="context"/>, or throws when a part is placed outside a tooltip root.</summary>
    /// <param name="context">The cascaded context.</param>
    /// <returns>The context.</returns>
    /// <exception cref="InvalidOperationException"><paramref name="context"/> is <see langword="null"/>.</exception>
    public static TooltipRootContext Require(TooltipRootContext? context) => context ?? throw new InvalidOperationException(MissingMessage);

    /// <summary>Runs the store's open-change sequence for a request of a part's browser-side behavior.</summary>
    /// <param name="request">The request.</param>
    /// <returns>A task that completes when the sequence has run.</returns>
    public abstract Task RequestAsync(OpenChangeRequest request);

    /// <summary>Subscribes a part to the selections it reads.</summary>
    /// <param name="changed">Called when one of the selections changed, the part's <c>StateHasChanged</c>.</param>
    /// <param name="selection">The selections.</param>
    /// <returns>The view; disposing it ends the subscription and a boundary registration.</returns>
    public abstract TooltipStoreView CreateView(Action changed, TooltipSelection selection);

    /// <summary>Writes whether a viewport is mounted (TIP-POS-05).</summary>
    /// <param name="adaptiveOrigin">Whether a viewport is mounted.</param>
    public abstract void SetAdaptiveOrigin(bool adaptiveOrigin);
}

/// <summary>The store selections a tooltip part reads.</summary>
[Flags]
internal enum TooltipSelection
{
    /// <summary>None.</summary>
    None = 0,

    /// <summary>The effective open state.</summary>
    Open = 1,

    /// <summary>Whether the popup is mounted; the part also brings the render-batch boundary.</summary>
    Mounted = 2,

    /// <summary>The transition status.</summary>
    TransitionStatus = 4,

    /// <summary>The instant type.</summary>
    InstantType = 8,

    /// <summary>The positioner's inputs: the instant type it renders, the cursor axis, hoverability, the root's disabled state, the active trigger, the close delay and whether a viewport is mounted.</summary>
    Positioner = 16,

    /// <summary>The count of unmount requests the popup's transitions handle reads.</summary>
    UnmountRequests = 32,

    /// <summary>The viewport's inputs: the active trigger and the payload's version.</summary>
    Viewport = 64,
}

/// <summary>A part's subscription to a tooltip store: the values it selected and the store calls it makes.</summary>
internal abstract class TooltipStoreView : IDisposable
{
    /// <summary>Gets a value indicating whether the tooltip is effectively open.</summary>
    public abstract bool Open { get; }

    /// <summary>Gets a value indicating whether the tooltip's popup is mounted.</summary>
    public abstract bool Mounted { get; }

    /// <summary>Gets the derived transition status.</summary>
    public abstract TransitionStatus? TransitionStatus { get; }

    /// <summary>Gets the instant type the popup, arrow and viewport render.</summary>
    public abstract TooltipInstantType? InstantType { get; }

    /// <summary>Gets the instant type the positioner renders.</summary>
    public abstract TooltipInstantType? PositionerInstantType { get; }

    /// <summary>Gets the axes along which the tooltip follows the pointer.</summary>
    public abstract TooltipTrackCursorAxis TrackCursorAxis { get; }

    /// <summary>Gets a value indicating whether the popup cannot be hovered.</summary>
    public abstract bool DisableHoverablePopup { get; }

    /// <summary>Gets a value indicating whether the root is disabled.</summary>
    public abstract bool Disabled { get; }

    /// <summary>Gets the id of the trigger the tooltip is anchored to.</summary>
    public abstract string? ActiveTriggerId { get; }

    /// <summary>Gets the active trigger's close delay in milliseconds.</summary>
    public abstract int CloseDelay { get; }

    /// <summary>Gets the count of unmount requests.</summary>
    public abstract int UnmountRequests { get; }

    /// <summary>Gets the counter that moves whenever the payload is written.</summary>
    public abstract int PayloadVersion { get; }

    /// <summary>Gets a value indicating whether a viewport is mounted.</summary>
    public abstract bool AdaptiveOrigin { get; }

    /// <inheritdoc />
    public abstract void Dispose();

    /// <summary>Runs the store's render-batch boundary.</summary>
    /// <returns>A task that completes when the boundary's work has run.</returns>
    public abstract Task AtBatchBoundaryAsync();

    /// <summary>Records the popup element.</summary>
    /// <param name="element">The popup element.</param>
    public abstract void SetPopupElement(ElementReference? element);

    /// <summary>Forgets the popup element.</summary>
    public abstract void ClearPopupElement();

    /// <summary>Records the positioner element.</summary>
    /// <param name="element">The positioner element, or <see langword="null"/>.</param>
    public abstract void SetPositionerElement(ElementReference? element);

    /// <summary>Mirrors the transition status the popup's transitions handle reported.</summary>
    /// <param name="status">The status.</param>
    public abstract void MirrorStatus(TransitionStatus? status);

    /// <summary>Applies a finished enter or exit transition of the popup element.</summary>
    /// <param name="opened">Whether the enter transition finished.</param>
    /// <returns>A task that completes when the callbacks have run.</returns>
    public abstract Task CompleteTransitionAsync(bool opened);

}

/// <summary>The view of a <see cref="TooltipStore{TPayload}"/> for the non-generic parts.</summary>
/// <typeparam name="TPayload">The type of the payload the tooltip's triggers carry.</typeparam>
internal sealed class TooltipRootContext<TPayload>(TooltipStore<TPayload> store) : TooltipRootContext
{
    /// <summary>Gets the store.</summary>
    public TooltipStore<TPayload> Store => store;

    /// <inheritdoc />
    public override string SessionToken => store.SessionToken!;

    /// <inheritdoc />
    public override string? FloatingId => store.Popup.FloatingId;

    /// <inheritdoc />
    public override Task RequestAsync(OpenChangeRequest request) => store.RequestAsync(request);

    /// <inheritdoc />
    public override TooltipStoreView CreateView(Action changed, TooltipSelection selection) => new View(store, changed, selection);

    /// <inheritdoc />
    public override void SetAdaptiveOrigin(bool adaptiveOrigin) => store.SetAdaptiveOrigin(adaptiveOrigin);

    private sealed class View : TooltipStoreView
    {
        private readonly TooltipStore<TPayload> store;
        private readonly SelectorSubscription<TooltipStoreState<TPayload>> subscription;
        private readonly IDisposable? boundaryPart;

        public View(TooltipStore<TPayload> store, Action changed, TooltipSelection selection)
        {
            this.store = store;
            subscription = new SelectorSubscription<TooltipStoreState<TPayload>>(store, changed);
            if ((selection & TooltipSelection.Open) != 0)
            {
                subscription.Select(PopupSelectors<TooltipStoreState<TPayload>, TPayload>.Open);
            }

            if ((selection & TooltipSelection.Mounted) != 0)
            {
                subscription.Select(PopupSelectors<TooltipStoreState<TPayload>, TPayload>.Mounted);

                // A part that selects the mounted state brings the render-batch boundary (ADR 16 Amendments 2026-10-27).
                boundaryPart = store.RegisterBoundaryPart();
            }

            if ((selection & TooltipSelection.TransitionStatus) != 0)
            {
                subscription.Select(PopupSelectors<TooltipStoreState<TPayload>, TPayload>.TransitionStatus);
            }

            if ((selection & TooltipSelection.InstantType) != 0)
            {
                subscription.Select(TooltipSelectors<TPayload>.InstantType);
            }

            if ((selection & TooltipSelection.Positioner) != 0)
            {
                subscription.Select(TooltipSelectors<TPayload>.PositionerInstantType);
                subscription.Select(TooltipSelectors<TPayload>.TrackCursorAxis);
                subscription.Select(TooltipSelectors<TPayload>.DisableHoverablePopup);
                subscription.Select(TooltipSelectors<TPayload>.Disabled);
                subscription.Select(PopupSelectors<TooltipStoreState<TPayload>, TPayload>.ActiveTriggerId);
                subscription.Select(TooltipSelectors<TPayload>.CloseDelay);
                subscription.Select(TooltipSelectors<TPayload>.AdaptiveOrigin);
            }

            if ((selection & TooltipSelection.UnmountRequests) != 0)
            {
                subscription.Select(PopupSelectors<TooltipStoreState<TPayload>, TPayload>.UnmountRequests);
            }

            if ((selection & TooltipSelection.Viewport) != 0)
            {
                subscription.Select(PopupSelectors<TooltipStoreState<TPayload>, TPayload>.ActiveTriggerId);
                subscription.Select(PopupSelectors<TooltipStoreState<TPayload>, TPayload>.PayloadVersion);
            }
        }

        /// <inheritdoc />
        public override bool Open => store.Popup.EffectiveOpen;

        /// <inheritdoc />
        public override bool Mounted => store.Popup.Mounted;

        /// <inheritdoc />
        public override TransitionStatus? TransitionStatus => store.Popup.DerivedTransitionStatus;

        /// <inheritdoc />
        public override TooltipInstantType? InstantType => store.State.InstantType;

        /// <inheritdoc />
        public override TooltipInstantType? PositionerInstantType => store.State.PositionerInstantType;

        /// <inheritdoc />
        public override TooltipTrackCursorAxis TrackCursorAxis => store.State.TrackCursorAxis;

        /// <inheritdoc />
        public override bool DisableHoverablePopup => store.State.DisableHoverablePopup;

        /// <inheritdoc />
        public override bool Disabled => store.Popup.Disabled;

        /// <inheritdoc />
        public override string? ActiveTriggerId => store.Popup.EffectiveActiveTriggerId;

        /// <inheritdoc />
        public override int CloseDelay => store.State.CloseDelay;

        /// <inheritdoc />
        public override int UnmountRequests => store.Popup.UnmountRequests;

        /// <inheritdoc />
        public override int PayloadVersion => store.Popup.PayloadVersion;

        /// <inheritdoc />
        public override bool AdaptiveOrigin => store.State.AdaptiveOrigin;

        /// <inheritdoc />
        public override void Dispose()
        {
            subscription.Dispose();
            boundaryPart?.Dispose();
        }

        /// <inheritdoc />
        public override Task AtBatchBoundaryAsync() => store.AtBatchBoundaryAsync();

        // The popup renders no id of its own (tooltip/popup/TooltipPopup.tsx: no `id` prop), so the popup id stays the
        // floating id unless the consumer gives one (PSH-STA-04).
        /// <inheritdoc />
        public override void SetPopupElement(ElementReference? element) => store.SetPopupElement(element, null);

        /// <inheritdoc />
        public override void ClearPopupElement() => store.ClearPopupElement();

        /// <inheritdoc />
        public override void SetPositionerElement(ElementReference? element) => store.SetPositionerElement(element);

        /// <inheritdoc />
        public override void MirrorStatus(TransitionStatus? status) => store.MirrorStatus(status);

        /// <inheritdoc />
        public override Task CompleteTransitionAsync(bool opened) => store.CompleteTransitionAsync(opened);

    }
}

// TooltipStore.ts:33-50: the tooltip's own selectors.
/// <summary>The named selectors of a tooltip store's own fields.</summary>
/// <typeparam name="TPayload">The type of the payload the tooltip's triggers carry.</typeparam>
internal static class TooltipSelectors<TPayload>
{
    /// <summary>Selects the instant type.</summary>
    public static readonly Func<TooltipStoreState<TPayload>, TooltipInstantType?> InstantType = static state => state.InstantType;

    /// <summary>Selects the positioner's instant type.</summary>
    public static readonly Func<TooltipStoreState<TPayload>, TooltipInstantType?> PositionerInstantType = static state => state.PositionerInstantType;

    /// <summary>Selects the cursor axis.</summary>
    public static readonly Func<TooltipStoreState<TPayload>, TooltipTrackCursorAxis> TrackCursorAxis = static state => state.TrackCursorAxis;

    /// <summary>Selects whether the popup cannot be hovered.</summary>
    public static readonly Func<TooltipStoreState<TPayload>, bool> DisableHoverablePopup = static state => state.DisableHoverablePopup;

    /// <summary>Selects whether the root is disabled.</summary>
    public static readonly Func<TooltipStoreState<TPayload>, bool> Disabled = static state => state.Popup.Disabled;

    /// <summary>Selects the active trigger's close delay.</summary>
    public static readonly Func<TooltipStoreState<TPayload>, int> CloseDelay = static state => state.CloseDelay;

    /// <summary>Selects whether a viewport is mounted.</summary>
    public static readonly Func<TooltipStoreState<TPayload>, bool> AdaptiveOrigin = static state => state.AdaptiveOrigin;
}

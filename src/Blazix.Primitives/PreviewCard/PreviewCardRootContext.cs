using Blazix.Primitives.Interop;
using Blazix.Primitives.Store;
using Microsoft.AspNetCore.Components;

namespace Blazix.Primitives;

// preview-card/root/PreviewCardContext.ts (PRV-STR-09, PRV-EDGE-01), the shape of TooltipRootContext (M4 track 6). The Root
// and the Trigger are generic over the payload (ADR 08 Amendments 2026-10-27, issue 116's finding); the Portal, Backdrop,
// Positioner, Popup, Arrow and Viewport are not, so the Root cascades this non-generic view of its store, fixed (ADR 16
// rule 9). A part takes one view, the selections it reads, and is rendered when one of them changed (ADR 05 §1's one
// subscription per part).
/// <summary>The non-generic view of a preview card root's store that the parts below the root read.</summary>
internal abstract class PreviewCardRootContext
{
    // PRV-EDGE-01 (preview-card/root/PreviewCardContext.ts), with the port's prefix and code quoting (issue 122).
    /// <summary>The message thrown when a preview card part is placed outside a <c>PreviewCardRoot</c>.</summary>
    internal const string MissingMessage = "Blazix: `PreviewCardRootContext` is missing. PreviewCard parts must be placed within `<PreviewCardRoot>`.";

    /// <summary>Gets the session token the preview card's browser-side behaviors share; it is never rendered.</summary>
    public abstract string SessionToken { get; }

    /// <summary>Gets the popup's generated id.</summary>
    public abstract string? FloatingId { get; }

    /// <summary>Returns <paramref name="context"/>, or throws when a part is placed outside a preview card root.</summary>
    /// <param name="context">The cascaded context.</param>
    /// <returns>The context.</returns>
    /// <exception cref="InvalidOperationException"><paramref name="context"/> is <see langword="null"/>.</exception>
    public static PreviewCardRootContext Require(PreviewCardRootContext? context) => context ?? throw new InvalidOperationException(MissingMessage);

    /// <summary>Runs the store's open-change sequence for a request of a part's browser-side behavior.</summary>
    /// <param name="request">The request.</param>
    /// <returns>A task that completes when the sequence has run.</returns>
    public abstract Task RequestAsync(OpenChangeRequest request);

    /// <summary>Subscribes a part to the selections it reads.</summary>
    /// <param name="changed">Called when one of the selections changed, the part's <c>StateHasChanged</c>.</param>
    /// <param name="selection">The selections.</param>
    /// <returns>The view; disposing it ends the subscription and a boundary registration.</returns>
    public abstract PreviewCardStoreView CreateView(Action changed, PreviewCardSelection selection);

    /// <summary>Registers a listener for a change of whether the current close keeps the popup mounted, called before the write is published.</summary>
    /// <param name="listener">The listener, called with the new value.</param>
    /// <returns>The registration; disposing it unregisters the listener.</returns>
    public abstract IDisposable OnUnmountPreventionChanging(Action<bool> listener);

    // PRV-STA-15.
    /// <summary>Writes whether a viewport is mounted.</summary>
    /// <param name="adaptiveOrigin">Whether a viewport is mounted.</param>
    public abstract void SetAdaptiveOrigin(bool adaptiveOrigin);
}

/// <summary>The store selections a preview card part reads.</summary>
[Flags]
internal enum PreviewCardSelection
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

    /// <summary>The positioner's inputs: the active trigger, the close delay and whether a viewport is mounted.</summary>
    Positioner = 16,

    /// <summary>The count of unmount requests the popup's transitions handle reads.</summary>
    UnmountRequests = 32,

    /// <summary>The viewport's inputs: the active trigger and the payload's version.</summary>
    Viewport = 64,
}

/// <summary>A part's subscription to a preview card store: the values it selected and the store calls it makes.</summary>
internal abstract class PreviewCardStoreView : IDisposable
{
    /// <summary>Gets a value indicating whether the preview card is effectively open.</summary>
    public abstract bool Open { get; }

    /// <summary>Gets a value indicating whether the preview card's popup is mounted.</summary>
    public abstract bool Mounted { get; }

    /// <summary>Gets the derived transition status.</summary>
    public abstract TransitionStatus? TransitionStatus { get; }

    /// <summary>Gets the instant type the popup, arrow and viewport render.</summary>
    public abstract PreviewCardInstantType? InstantType { get; }

    /// <summary>Gets the id of the trigger the preview card is anchored to.</summary>
    public abstract string? ActiveTriggerId { get; }

    /// <summary>Gets the active trigger's close delay in milliseconds.</summary>
    public abstract int CloseDelay { get; }

    /// <summary>Gets the count of unmount requests.</summary>
    public abstract int UnmountRequests { get; }

    /// <summary>Gets the counter that moves whenever the payload is written.</summary>
    public abstract int PayloadVersion { get; }

    /// <summary>Gets a value indicating whether a viewport is mounted.</summary>
    public abstract bool AdaptiveOrigin { get; }

    /// <summary>Gets a value indicating whether the current close keeps the popup mounted until the unmount action runs.</summary>
    public abstract bool PreventUnmountingOnClose { get; }

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

/// <summary>The view of a <see cref="PreviewCardStore{TPayload}"/> for the non-generic parts.</summary>
/// <typeparam name="TPayload">The type of the payload the preview card's triggers carry.</typeparam>
internal sealed class PreviewCardRootContext<TPayload>(PreviewCardStore<TPayload> store) : PreviewCardRootContext
{
    /// <summary>Gets the store.</summary>
    public PreviewCardStore<TPayload> Store => store;

    /// <inheritdoc />
    public override string SessionToken => store.SessionToken!;

    /// <inheritdoc />
    public override string? FloatingId => store.Popup.FloatingId;

    /// <inheritdoc />
    public override Task RequestAsync(OpenChangeRequest request) => store.RequestOpenAsync(request.Open, request.Details);

    /// <inheritdoc />
    public override PreviewCardStoreView CreateView(Action changed, PreviewCardSelection selection) => new View(store, changed, selection);

    /// <inheritdoc />
    public override void SetAdaptiveOrigin(bool adaptiveOrigin) => store.SetAdaptiveOrigin(adaptiveOrigin);

    /// <inheritdoc />
    public override IDisposable OnUnmountPreventionChanging(Action<bool> listener) => store.OnUnmountPreventionChanging(listener);

    private sealed class View : PreviewCardStoreView
    {
        private readonly PreviewCardStore<TPayload> store;
        private readonly SelectorSubscription<PreviewCardStoreState<TPayload>> subscription;
        private readonly IDisposable? boundaryPart;

        public View(PreviewCardStore<TPayload> store, Action changed, PreviewCardSelection selection)
        {
            this.store = store;
            subscription = new SelectorSubscription<PreviewCardStoreState<TPayload>>(store, changed);
            if ((selection & PreviewCardSelection.Open) != 0)
            {
                subscription.Select(PopupSelectors<PreviewCardStoreState<TPayload>, TPayload>.Open);
            }

            if ((selection & PreviewCardSelection.Mounted) != 0)
            {
                subscription.Select(PopupSelectors<PreviewCardStoreState<TPayload>, TPayload>.Mounted);

                // A part that selects the mounted state brings the render-batch boundary (ADR 16 Amendments 2026-10-27).
                boundaryPart = store.RegisterBoundaryPart();
            }

            if ((selection & PreviewCardSelection.TransitionStatus) != 0)
            {
                subscription.Select(PopupSelectors<PreviewCardStoreState<TPayload>, TPayload>.TransitionStatus);
            }

            if ((selection & PreviewCardSelection.InstantType) != 0)
            {
                subscription.Select(PreviewCardSelectors<TPayload>.InstantType);
            }

            if ((selection & PreviewCardSelection.Positioner) != 0)
            {
                subscription.Select(PopupSelectors<PreviewCardStoreState<TPayload>, TPayload>.ActiveTriggerId);
                subscription.Select(PreviewCardSelectors<TPayload>.CloseDelay);
                subscription.Select(PreviewCardSelectors<TPayload>.AdaptiveOrigin);
            }

            if ((selection & PreviewCardSelection.UnmountRequests) != 0)
            {
                subscription.Select(PopupSelectors<PreviewCardStoreState<TPayload>, TPayload>.UnmountRequests);
            }

            if ((selection & PreviewCardSelection.Viewport) != 0)
            {
                subscription.Select(PopupSelectors<PreviewCardStoreState<TPayload>, TPayload>.ActiveTriggerId);
                subscription.Select(PopupSelectors<PreviewCardStoreState<TPayload>, TPayload>.PayloadVersion);
            }
        }

        /// <inheritdoc />
        public override bool Open => store.Popup.EffectiveOpen;

        /// <inheritdoc />
        public override bool Mounted => store.Popup.Mounted;

        /// <inheritdoc />
        public override TransitionStatus? TransitionStatus => store.Popup.DerivedTransitionStatus;

        /// <inheritdoc />
        public override PreviewCardInstantType? InstantType => store.State.InstantType;

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
        public override bool PreventUnmountingOnClose => store.Popup.PreventUnmountingOnClose;

        /// <inheritdoc />
        public override void Dispose()
        {
            subscription.Dispose();
            boundaryPart?.Dispose();
        }

        /// <inheritdoc />
        public override Task AtBatchBoundaryAsync() => store.AtBatchBoundaryAsync();

        // The popup renders no id of its own (preview-card/popup/PreviewCardPopup.tsx: no `id` prop), so the popup id stays the
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

// preview-card/store/PreviewCardStore.ts: the preview card's own selectors.
/// <summary>The named selectors of a preview card store's own fields.</summary>
/// <typeparam name="TPayload">The type of the payload the preview card's triggers carry.</typeparam>
internal static class PreviewCardSelectors<TPayload>
{
    /// <summary>Selects the instant type.</summary>
    public static readonly Func<PreviewCardStoreState<TPayload>, PreviewCardInstantType?> InstantType = static state => state.InstantType;

    /// <summary>Selects the active trigger's close delay.</summary>
    public static readonly Func<PreviewCardStoreState<TPayload>, int> CloseDelay = static state => state.CloseDelay;

    /// <summary>Selects whether a viewport is mounted.</summary>
    public static readonly Func<PreviewCardStoreState<TPayload>, bool> AdaptiveOrigin = static state => state.AdaptiveOrigin;
}

using Blazix.Primitives.Store;
using Microsoft.Extensions.Logging;

namespace Blazix.Primitives;

// TIP §4.1 (tooltip/store/TooltipStore.ts:17-27, 120-141); ADR 05 Amendments 2026-10-27 (the composed store record, chosen
// by the user 2026-10-27). The root's `disabled` is the shared record's Disabled (ADR 05 Amendments 2026-10-28, design
// point 13.7 of M4 track 6, settled by the orchestrator 2026-10-28). Left out of C# state (issue 121): isInstantPhase, which the open request
// carries from the `interactions` module's delay group (ADR 04 Amendments 2026-10-27), and adaptiveOrigin, the Viewport's.
/// <summary>Holds the state of one tooltip: the shared popup state and the tooltip's own fields.</summary>
/// <typeparam name="TPayload">The type of the payload the tooltip's triggers carry.</typeparam>
/// <param name="Popup">The shared popup state.</param>
/// <param name="InstantType">Why the parts skip their transitions, or <see langword="null"/> when they play them.</param>
/// <param name="TrackCursorAxis">The axes along which the tooltip follows the pointer.</param>
/// <param name="DisableHoverablePopup">Whether the popup cannot be hovered.</param>
/// <param name="LastOpenChangeReason">The reason of the last applied open change, or <see langword="null"/> before the first.</param>
/// <param name="CloseOnClick">Whether a press of the active trigger closes the tooltip.</param>
/// <param name="CloseDelay">The active trigger's close delay in milliseconds, which the popup's own leave reads.</param>
[StoreState]
internal sealed partial record TooltipStoreState<TPayload>(
    PopupStoreState<TPayload> Popup,
    TooltipInstantType? InstantType,
    TooltipTrackCursorAxis TrackCursorAxis,
    bool DisableHoverablePopup,
    EventReason? LastOpenChangeReason,
    bool CloseOnClick,
    int CloseDelay)
    : IStoreState<TooltipStoreState<TPayload>>, IPopupStoreState<TooltipStoreState<TPayload>, TPayload>
{
    /// <inheritdoc />
    public PopupStoreState<TPayload> Popup { get; init; } = Popup;

    // TIP-STA-12: the positioner's instant value.
    /// <summary>Gets the instant type the positioner renders: <see cref="TooltipInstantType.TrackingCursor"/> while tracking the pointer.</summary>
    public TooltipInstantType? PositionerInstantType => TrackCursorAxis != TooltipTrackCursorAxis.None ? TooltipInstantType.TrackingCursor : InstantType;

    // TIP-STA-01: createInitialState (TooltipStore.ts:120-141).
    /// <summary>Creates the initial state of a tooltip store.</summary>
    /// <param name="floatingId">The popup's generated id, or <see langword="null"/> for a fallback store.</param>
    /// <returns>The initial state.</returns>
    public static TooltipStoreState<TPayload> Initial(string? floatingId) =>
        new(PopupStoreState<TPayload>.Initial(floatingId), null, TooltipTrackCursorAxis.None, false, null, true, 0);

    /// <inheritdoc />
    public static TooltipStoreState<TPayload> WithPopup(TooltipStoreState<TPayload> state, PopupStoreState<TPayload> popup) => state with { Popup = popup };

}

// tooltip/store/TooltipStore.ts; TIP-STA-04..07, TIP-STA-09, TIP-STA-16; PSH-STA-37 (the inert fallback store).
// - The instant type (TIP-STA-05..07): the base change of EVD-EVT-17 is applied with every committed change, then the
//   delay-group override of TooltipRoot.tsx:92-118 in the same write. The group's phase arrives on the request
//   (OpenChangeRequest.Instant), so data-instant="delay" renders in the batch of data-open on both modes (chosen by the
//   user 2026-10-27, ADR 04 Amendments 2026-10-27; TIP §16 Q8 no longer arises). The value the override replaces is
//   remembered and restored by the first change the override no longer covers, stale values included (TIP §16 Q13,
//   replicated).
// - The last reason (TIP-STA-04): every applied change records it.
/// <summary>Runs one tooltip's open-change sequence and holds its instant type and forwarded trigger state.</summary>
/// <typeparam name="TPayload">The type of the payload the tooltip's triggers carry.</typeparam>
internal sealed class TooltipStore<TPayload> : PopupStore<TooltipStoreState<TPayload>, TPayload>
{
    private OpenChangeEventDetails? instantRequest;
    private bool instantPhase;
    private bool hasRememberedInstantType;
    private TooltipInstantType? rememberedInstantType;

    /// <summary>Initializes a new instance of the <see cref="TooltipStore{TPayload}"/> class.</summary>
    /// <param name="floatingId">The popup's generated id, or <see langword="null"/> for a fallback store.</param>
    /// <param name="sessionToken">The session token, or <see langword="null"/> for a fallback store.</param>
    /// <param name="inert">Whether this is a handle's fallback store.</param>
    /// <param name="logger">The root's logger, or <see langword="null"/>.</param>
    public TooltipStore(string? floatingId, string? sessionToken, bool inert = false, ILogger? logger = null)
        : base(TooltipStoreState<TPayload>.Initial(floatingId), "Tooltip", sessionToken, inert, logger)
    {
        CloseOnActiveTriggerUnmount = true;
    }

    /// <summary>Runs the open-change sequence for a request of the browser-side behaviors, with the delay group's phase it carries.</summary>
    /// <param name="request">The request.</param>
    /// <returns>A task that completes when the sequence has run.</returns>
    public Task<bool> RequestAsync(OpenChangeRequest request)
    {
        instantRequest = request.Instant ? request.Details : null;
        return RequestOpenAsync(request.Open, request.Details);
    }

    // TIP-STA-16: the root's props, mirrored at its parameter set.
    /// <summary>Records the root's cursor-tracking axis and hoverability.</summary>
    /// <param name="trackCursorAxis">The axes along which the tooltip follows the pointer.</param>
    /// <param name="disableHoverablePopup">Whether the popup cannot be hovered.</param>
    public void SetRootInputs(TooltipTrackCursorAxis trackCursorAxis, bool disableHoverablePopup)
    {
        TooltipStoreState<TPayload> state = State;
        if (state.TrackCursorAxis != trackCursorAxis || state.DisableHoverablePopup != disableHoverablePopup)
        {
            Write(state with { TrackCursorAxis = trackCursorAxis, DisableHoverablePopup = disableHoverablePopup });
        }
    }

    // TIP-STA-09: the active trigger's forwarded closeOnClick and closeDelay (useTriggerDataForwarding,
    // TooltipTrigger.tsx:80-89).
    /// <summary>Records the forwarded state of the trigger that owns the mounted tooltip.</summary>
    /// <param name="closeOnClick">Whether a press of that trigger closes the tooltip.</param>
    /// <param name="closeDelay">That trigger's close delay in milliseconds, without the provider's fallback.</param>
    public void SetForwardedState(bool closeOnClick, int closeDelay)
    {
        TooltipStoreState<TPayload> state = State;
        if (state.CloseOnClick != closeOnClick || state.CloseDelay != closeDelay)
        {
            Write(state with { CloseOnClick = closeOnClick, CloseDelay = closeDelay });
        }
    }

    /// <inheritdoc />
    protected override TooltipStoreState<TPayload> ApplyOpenChange(TooltipStoreState<TPayload> state, bool open, OpenChangeEventDetails details)
    {
        // The phase belongs to the request that carried it, whatever ran since it was sent.
        bool phase = ReferenceEquals(instantRequest, details);
        instantRequest = null;
        instantPhase = phase;

        TooltipInstantType? instantType = PopupStore<TooltipStoreState<TPayload>, TPayload>.InstantChange(open, details.Reason) switch
        {
            PopupInstantChange.Focus => TooltipInstantType.Focus,
            PopupInstantChange.Dismiss => TooltipInstantType.Dismiss,
            PopupInstantChange.Clear => null,
            _ => state.InstantType,
        };

        // TooltipRoot.tsx:103-118: `ending` is the status this write leaves, read from the effective open state it commits.
        PopupStoreState<TPayload> popup = state.Popup;
        bool effectiveOpen = !popup.Disabled && (popup.OpenProp ?? open);
        bool ending = !effectiveOpen && popup.Mounted;
        if ((ending && details.Reason == EventReason.None) || (!ending && instantPhase))
        {
            if (instantType != TooltipInstantType.Delay)
            {
                hasRememberedInstantType = true;
                rememberedInstantType = instantType;
            }

            instantType = TooltipInstantType.Delay;
        }
        else if (hasRememberedInstantType)
        {
            instantType = rememberedInstantType;
            hasRememberedInstantType = false;
            rememberedInstantType = null;
        }

        return state with { InstantType = instantType, LastOpenChangeReason = details.Reason };
    }
}

using Blazix.Primitives.Store;
using Microsoft.Extensions.Logging;

namespace Blazix.Primitives;

// PRV §4.1 (preview-card/store/PreviewCardStore.ts:22-30, 107-142); ADR 05 Amendments 2026-10-27 (the composed store
// record, chosen by the user 2026-10-27). Left out of C# state (issue 122): the hovered-line record, the `interactions`
// session's (ADR 04 Amendments 2026-10-18, JavaScript's end to end). adaptiveOrigin is a flag here, as Popover's and
// Tooltip's (PSH-POS-02): the positioning module holds the middleware.
/// <summary>Holds the state of one preview card: the shared popup state and the card's own fields.</summary>
/// <typeparam name="TPayload">The type of the payload the card's triggers carry.</typeparam>
/// <param name="Popup">The shared popup state.</param>
/// <param name="InstantType">Why the parts skip their transitions, or <see langword="null"/> when they play them.</param>
/// <param name="CloseDelay">The active trigger's close delay in milliseconds, which the card's own leave reads.</param>
/// <param name="AdaptiveOrigin">Whether a viewport is mounted, so the positioner is placed by its far edge.</param>
[StoreState]
internal sealed partial record PreviewCardStoreState<TPayload>(
    PopupStoreState<TPayload> Popup,
    PreviewCardInstantType? InstantType,
    int CloseDelay,
    bool AdaptiveOrigin)
    : IStoreState<PreviewCardStoreState<TPayload>>, IPopupStoreState<PreviewCardStoreState<TPayload>, TPayload>
{
    /// <inheritdoc />
    public PopupStoreState<TPayload> Popup { get; init; } = Popup;

    // PRV-STA-01: createInitialState (PreviewCardStore.ts:107-142).
    /// <summary>Creates the initial state of a preview card store.</summary>
    /// <param name="floatingId">The popup's generated id, or <see langword="null"/> for a fallback store.</param>
    /// <returns>The initial state.</returns>
    public static PreviewCardStoreState<TPayload> Initial(string? floatingId) =>
        new(PopupStoreState<TPayload>.Initial(floatingId), null, PreviewCardDelays.CloseDelay, false);

    /// <inheritdoc />
    public static PreviewCardStoreState<TPayload> WithPopup(PreviewCardStoreState<TPayload> state, PopupStoreState<TPayload> popup) => state with { Popup = popup };
}

// preview-card/store/PreviewCardStore.ts; PRV-STA-01, PRV-STA-10, PRV-STA-11, PRV-STA-14; PSH-STA-37 (the inert fallback
// store). The instant type is EVD-EVT-17's base change applied with every committed change; a change with an unmapped
// reason keeps the previous value (PRV §16 Q4, replicated; chosen by the user 2026-10-27, ADR 04 Amendments 2026-10-27).
/// <summary>Runs one preview card's open-change sequence and holds its instant type and forwarded close delay.</summary>
/// <typeparam name="TPayload">The type of the payload the card's triggers carry.</typeparam>
internal sealed class PreviewCardStore<TPayload> : PopupStore<PreviewCardStoreState<TPayload>, TPayload>
{
    /// <summary>Initializes a new instance of the <see cref="PreviewCardStore{TPayload}"/> class.</summary>
    /// <param name="floatingId">The popup's generated id, or <see langword="null"/> for a fallback store.</param>
    /// <param name="sessionToken">The session token, or <see langword="null"/> for a fallback store.</param>
    /// <param name="inert">Whether this is a handle's fallback store.</param>
    /// <param name="logger">The root's logger, or <see langword="null"/>.</param>
    public PreviewCardStore(string? floatingId, string? sessionToken, bool inert = false, ILogger? logger = null)
        : base(PreviewCardStoreState<TPayload>.Initial(floatingId), "PreviewCard", sessionToken, inert, logger)
    {
        CloseOnActiveTriggerUnmount = true;
    }

    // PRV-STA-11 (PreviewCardTrigger.tsx:58-68): the active trigger's close delay, never reset on close (PRV §16 Q6).
    /// <summary>Records the close delay of the trigger that owns the mounted card.</summary>
    /// <param name="closeDelay">That trigger's close delay in milliseconds, after its fallback.</param>
    public void SetCloseDelay(int closeDelay)
    {
        if (State.CloseDelay != closeDelay)
        {
            Write(State with { CloseDelay = closeDelay });
        }
    }

    // PRV-STA-15, PSH-POS-02: a mounted Viewport positions the card by its far edge.
    /// <summary>Writes whether a viewport is mounted.</summary>
    /// <param name="adaptiveOrigin">Whether a viewport is mounted.</param>
    public void SetAdaptiveOrigin(bool adaptiveOrigin)
    {
        if (State.AdaptiveOrigin != adaptiveOrigin)
        {
            Write(State with { AdaptiveOrigin = adaptiveOrigin });
        }
    }

    /// <inheritdoc />
    protected override PreviewCardStoreState<TPayload> ApplyOpenChange(PreviewCardStoreState<TPayload> state, bool open, OpenChangeEventDetails details)
    {
        PreviewCardInstantType? instantType = PopupStore<PreviewCardStoreState<TPayload>, TPayload>.InstantChange(open, details.Reason) switch
        {
            PopupInstantChange.Focus => PreviewCardInstantType.Focus,
            PopupInstantChange.Dismiss => PreviewCardInstantType.Dismiss,
            PopupInstantChange.Clear => null,
            _ => state.InstantType,
        };

        return instantType == state.InstantType ? state : state with { InstantType = instantType };
    }
}

// PRV-API-10/11 (preview-card/utils/constants.ts: OPEN_DELAY 600, CLOSE_DELAY 300); ADR 08 Amendments 2026-10-27
// (`null` meaning not supplied). Resolved in C# before they cross.
/// <summary>The millisecond fallbacks of a preview card trigger.</summary>
internal static class PreviewCardDelays
{
    /// <summary>The open delay of a trigger with no delay, in milliseconds.</summary>
    public const int OpenDelay = 600;

    /// <summary>The close delay of a trigger with no close delay, in milliseconds.</summary>
    public const int CloseDelay = 300;

    /// <summary>Resolves a trigger's open delay, for hover and focus: its own, else 600 ms.</summary>
    /// <param name="delay">The trigger's delay.</param>
    /// <returns>The delay in milliseconds.</returns>
    public static int Open(Duration? delay) => TooltipDelays.Milliseconds(delay) ?? OpenDelay;

    /// <summary>Resolves a trigger's close delay: its own, else 300 ms.</summary>
    /// <param name="closeDelay">The trigger's close delay.</param>
    /// <returns>The delay in milliseconds.</returns>
    public static int Close(Duration? closeDelay) => TooltipDelays.Milliseconds(closeDelay) ?? CloseDelay;
}

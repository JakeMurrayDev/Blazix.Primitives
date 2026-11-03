using Blazix.Primitives.Interop;
using Blazix.Primitives.Store;
using Microsoft.Extensions.Logging;

namespace Blazix.Primitives;

// NAV §4 (navigation-menu/root/NavigationMenuRoot.tsx:83-243); ADR 05 Amendments 2026-11-03 (chosen by the user 2026-11-03):
// the Root's store composes the open lifecycle track 2 extracts from PopupStore (PopupLifecycleStore, FLT-STR-11's port)
// and holds NavigationMenu's own fields beside it. The value itself is the Root's Controlled<TValue?> (ADR 08 Amendments
// 2026-11-03 (2): `null` is closed), so the record is not generic and every part below the Root subscribes to it without
// naming TValue; the Items compare the value through their own context (NAV-STA-10, NAV-STA-11).
// - ActivationDirection: the direction the last activation reported (design point 4, sent 2026-11-04, the user's; Writer
//   C#), null while closed (NAV-STA-06).
// - ViewportInert (NAV-STA-09), the active Trigger's id (the session's ActiveTriggerId; design point 2, settled by the
//   orchestrator 2026-11-04), the Popup's and the Viewport's rendered ids (aria-controls, aria-owns), the element registry
//   that enables the dismissal and the hover roles (NAV-STA-08, NAV-DIS-08), the latest close reason (NAV-STA-15) and the
//   count of the module's focus-out reports answered (ADR 04 Amendments 2026-10-28, widened 2026-10-29).
/// <summary>The navigation menu store's record: the open lifecycle and the menu's own fields.</summary>
/// <param name="Lifecycle">The open lifecycle.</param>
/// <param name="ActivationDirection">The literal of the activation direction, or <see langword="null"/>.</param>
/// <param name="ViewportInert">Whether focus left the viewport for an element outside it and the active trigger.</param>
/// <param name="ActiveTriggerId">The id of the first active trigger, or <see langword="null"/>.</param>
/// <param name="PopupId">The id the popup renders, or <see langword="null"/> while none is registered.</param>
/// <param name="ViewportId">The id the viewport renders, or <see langword="null"/> while none is registered.</param>
/// <param name="HasPositioner">Whether a positioner is registered.</param>
/// <param name="HasViewportTarget">Whether a viewport target, the element the contents move into, is registered.</param>
/// <param name="CloseReason">The reason of the latest close request, or <see langword="null"/>.</param>
/// <param name="Answered">The number of the module's focus-out reports answered.</param>
[StoreState]
internal sealed partial record NavigationMenuStoreState(
    PopupLifecycleState Lifecycle,
    string? ActivationDirection,
    bool ViewportInert,
    string? ActiveTriggerId,
    string? PopupId,
    string? ViewportId,
    bool HasPositioner,
    bool HasViewportTarget,
    EventReason? CloseReason,
    int Answered)
    : IStoreState<NavigationMenuStoreState>, IPopupLifecycleState<NavigationMenuStoreState>
{
    /// <inheritdoc />
    public PopupLifecycleState Lifecycle { get; init; } = Lifecycle;

    /// <summary>Gets a value indicating whether the menu is open: its value is not <see langword="null"/>.</summary>
    public bool Open => Lifecycle.EffectiveOpen;

    /// <summary>Gets a value indicating whether the popup is mounted.</summary>
    public bool Mounted => Lifecycle.Mounted;

    /// <summary>Gets the popup's transition status.</summary>
    public TransitionStatus? TransitionStatus => Lifecycle.DerivedTransitionStatus;

    /// <summary>Gets the record a store starts with.</summary>
    /// <param name="floatingId">The popup id the root drew.</param>
    /// <returns>The initial record: closed, nothing registered.</returns>
    public static NavigationMenuStoreState Initial(string? floatingId) => new(
        PopupLifecycleState.Initial(floatingId),
        ActivationDirection: null,
        ViewportInert: false,
        ActiveTriggerId: null,
        PopupId: null,
        ViewportId: null,
        HasPositioner: false,
        HasViewportTarget: false,
        CloseReason: null,
        Answered: 0);

    /// <inheritdoc />
    public static NavigationMenuStoreState WithLifecycle(NavigationMenuStoreState state, in PopupLifecycleState lifecycle) => state with { Lifecycle = lifecycle };
}

// The selections the parts make: named, static, so a render allocates none (ADR 05 §1; ADR 16 rule 7).
internal static class NavigationMenuSelectors
{
    public static readonly Func<NavigationMenuStoreState, bool> Open = static state => state.Open;

    public static readonly Func<NavigationMenuStoreState, bool> Mounted = static state => state.Mounted;

    public static readonly Func<NavigationMenuStoreState, TransitionStatus?> TransitionStatus = static state => state.TransitionStatus;

    public static readonly Func<NavigationMenuStoreState, int> UnmountRequests = static state => state.Lifecycle.UnmountRequests;

    public static readonly Func<NavigationMenuStoreState, bool> PreventUnmountingOnClose = static state => state.Lifecycle.PreventUnmountingOnClose;

    public static readonly Func<NavigationMenuStoreState, OpenChangeRelay?> OpenChange = static state => state.Lifecycle.OpenChange;

    public static readonly Func<NavigationMenuStoreState, string?> FloatingId = static state => state.Lifecycle.FloatingId;

    // NAV-STA-06: the direction Content exposes is null whenever the menu is closed, even while it exits.
    public static readonly Func<NavigationMenuStoreState, string?> ActivationDirection = static state => state.Open ? state.ActivationDirection : null;

    public static readonly Func<NavigationMenuStoreState, bool> ViewportInert = static state => state.ViewportInert;

    public static readonly Func<NavigationMenuStoreState, string?> ActiveTriggerId = static state => state.ActiveTriggerId;

    public static readonly Func<NavigationMenuStoreState, string?> PopupId = static state => state.PopupId;

    public static readonly Func<NavigationMenuStoreState, string?> ViewportId = static state => state.ViewportId;

    public static readonly Func<NavigationMenuStoreState, bool> HasPositioner = static state => state.HasPositioner;

    public static readonly Func<NavigationMenuStoreState, bool> HasViewportTarget = static state => state.HasViewportTarget;

    public static readonly Func<NavigationMenuStoreState, EventReason?> CloseReason = static state => state.CloseReason;

    public static readonly Func<NavigationMenuStoreState, int> Answered = static state => state.Answered;
}

// The store (ADR 05 §1): the lifecycle's sequence, with the Root's OnValueChange step as its consumer step and the Root's
// unmount bookkeeping as its unmount hook (NavigationMenuRoot.tsx:133-158).
/// <summary>Holds a navigation menu's open lifecycle and its own fields.</summary>
internal sealed class NavigationMenuStore : PopupLifecycleStore<NavigationMenuStoreState>
{
    /// <summary>Occurs when the popup unmounted, after the lifecycle's reset and before the completion callback.</summary>
    public event Action? Unmounted;

    /// <summary>Initializes a new instance of the <see cref="NavigationMenuStore"/> class.</summary>
    /// <param name="popupId">The popup id the root drew.</param>
    /// <param name="sessionToken">The session token the root drew.</param>
    /// <param name="logger">The root's logger, or <see langword="null"/>.</param>
    public NavigationMenuStore(string popupId, string sessionToken, ILogger? logger)
        : base(NavigationMenuStoreState.Initial(popupId), "NavigationMenu", sessionToken, logger: logger)
    {
    }

    /// <summary>Gets or sets the Root's consumer step, which the lifecycle's own requests run.</summary>
    public Func<bool, OpenChangeEventDetails, Task<bool>>? ConsumerStep { get; set; }

    /// <summary>Writes the activation direction.</summary>
    /// <param name="direction">The direction's literal, or <see langword="null"/>.</param>
    public void SetActivationDirection(string? direction)
    {
        if (!string.Equals(State.ActivationDirection, direction, StringComparison.Ordinal))
        {
            Write(State with { ActivationDirection = direction });
        }
    }

    /// <summary>Writes the viewport's inert flag.</summary>
    /// <param name="inert">The new value.</param>
    public void SetViewportInert(bool inert)
    {
        if (State.ViewportInert != inert)
        {
            Write(State with { ViewportInert = inert });
        }
    }

    /// <summary>Writes the id of the first active trigger.</summary>
    /// <param name="triggerId">The id, or <see langword="null"/>.</param>
    public void SetActiveTriggerId(string? triggerId)
    {
        if (!string.Equals(State.ActiveTriggerId, triggerId, StringComparison.Ordinal))
        {
            Write(State with { ActiveTriggerId = triggerId });
        }
    }

    /// <summary>Writes the popup's rendered id.</summary>
    /// <param name="popupId">The id, or <see langword="null"/> when no popup is registered.</param>
    public void SetPopupId(string? popupId)
    {
        if (!string.Equals(State.PopupId, popupId, StringComparison.Ordinal))
        {
            Write(State with { PopupId = popupId });
        }
    }

    /// <summary>Writes the viewport's rendered id.</summary>
    /// <param name="viewportId">The id, or <see langword="null"/> when no viewport is registered.</param>
    public void SetViewportId(string? viewportId)
    {
        if (!string.Equals(State.ViewportId, viewportId, StringComparison.Ordinal))
        {
            Write(State with { ViewportId = viewportId });
        }
    }

    /// <summary>Writes whether a positioner is registered.</summary>
    /// <param name="registered">The new value.</param>
    public void SetPositioner(bool registered)
    {
        if (State.HasPositioner != registered)
        {
            Write(State with { HasPositioner = registered });
        }
    }

    /// <summary>Writes whether a viewport target is registered.</summary>
    /// <param name="registered">The new value.</param>
    public void SetViewportTarget(bool registered)
    {
        if (State.HasViewportTarget != registered)
        {
            Write(State with { HasViewportTarget = registered });
        }
    }

    /// <summary>Records the reason of a close request.</summary>
    /// <param name="reason">The reason, or <see langword="null"/> to clear it.</param>
    public void SetCloseReason(EventReason? reason)
    {
        if (State.CloseReason != reason)
        {
            Write(State with { CloseReason = reason });
        }
    }

    /// <summary>Counts one more of the module's focus-out reports answered.</summary>
    public void Answer() => Write(State with { Answered = State.Answered + 1 });

    /// <inheritdoc />
    /// <remarks>The open state is derived from the root's value, so the root's value step runs instead of a controlled open.</remarks>
    protected override Task<bool> RunConsumerStepAsync(bool open, OpenChangeEventDetails details) =>
        ConsumerStep is { } step ? step(open, details) : Task.FromResult(true);

    /// <inheritdoc />
    protected override void PrepareOpenChange(bool open, OpenChangeEventDetails details)
    {
    }

    /// <inheritdoc />
    protected override void BeforeDispatch(bool open, OpenChangeEventDetails details)
    {
    }

    /// <inheritdoc />
    protected override void OnOpenChangeDispatched(OpenChangeRelay relay)
    {
    }

    /// <inheritdoc />
    protected override NavigationMenuStoreState ApplyOpenChange(NavigationMenuStoreState state, bool open, OpenChangeEventDetails details) => state;

    /// <inheritdoc />
    protected override void OnUnmount() => Unmounted?.Invoke();
}

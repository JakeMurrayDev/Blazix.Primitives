using Blazix.Primitives.Interop;
using Blazix.Primitives.Store;
using Microsoft.AspNetCore.Components;

namespace Blazix.Primitives;

// PSH §4.3.1 (utils/popups/store.ts:12-125), ADR 05 Amendments 2026-10-27 (chosen by the user 2026-10-27): the shared
// fields of every popup store, held as one member of each family's record (PopoverStoreState<TPayload>(PopupStoreState<
// TPayload> Popup, …)). Left out: the three interaction prop bags and floatingRootContext, which the `interactions` session
// owns (ADR 04 Amendments 2026-10-27; PSH-STA-42/46 restated). Added: PayloadVersion (the payload lives in the store's
// non-reactive context, so a value-type payload never boxes in this record's comparison, ADR 16 Amendments 2026-10-27),
// UnmountRequests (the transitions module's unmount input, TRN-ANI-19..23) and OpenChange, the relay of the last committed
// open change (ADR 04 Amendments 2026-10-27). TPayload has no member: it ties the record to its family's payload type.
// Element identity is the registering trigger part plus its element (PopupTriggerElement; settled by the orchestrator
// 2026-10-27 under ADR 01 Amendments 2026-10-22). popupElement is its id string here (PSH-STA-04 reads only its id).
// M5 track 2 (ADR 05 Amendments 2026-11-03 and 2026-11-04, design point 1 (a2), settled by the orchestrator 2026-11-04):
// the open lifecycle's ten fields (open and its controlled prop, mounted, the stored status, the floating id, the unmount
// opt-out and count, the relay, the open method, Disabled) are one member, Lifecycle, behind forwarding properties, so the
// families' reads and `with` expressions are unchanged and the generated ShallowEquals compares the struct member by member.
/// <summary>Represents the state every popup store shares, whatever its family.</summary>
/// <typeparam name="TPayload">The type of the payload the popup's triggers carry.</typeparam>
/// <param name="Lifecycle">The open lifecycle: the open state, mounting, the transition and the last committed change.</param>
/// <param name="TriggerCount">The number of registered triggers while open; 0 while closed.</param>
/// <param name="PayloadVersion">A counter that moves whenever the payload in the store's context is written.</param>
/// <param name="ActiveTriggerId">The internal id of the active trigger.</param>
/// <param name="ActiveTriggerElement">The active trigger's registration.</param>
/// <param name="OpenedWithoutTrigger">Whether the popup opened through a request that carried no trigger.</param>
/// <param name="TriggerIdControlled">Whether the root's <c>TriggerId</c> parameter is controlled.</param>
/// <param name="TriggerIdProp">The controlled <c>TriggerId</c>; <see langword="null"/> means no active trigger.</param>
/// <param name="PopupElementKnown">Whether a popup part has registered its element.</param>
/// <param name="PopupElementId">The <c>id</c> the registered popup element renders, or <see langword="null"/> for none.</param>
/// <param name="PopupElement">The popup element, once rendered.</param>
/// <param name="PositionerElement">The positioner element, once rendered.</param>
[StoreState]
internal sealed partial record PopupStoreState<TPayload>(
    PopupLifecycleState Lifecycle,
    int TriggerCount,
    int PayloadVersion,
    string? ActiveTriggerId,
    PopupTriggerElement? ActiveTriggerElement,
    bool OpenedWithoutTrigger,
    bool TriggerIdControlled,
    string? TriggerIdProp,
    bool PopupElementKnown,
    string? PopupElementId,
    ElementReference? PopupElement,
    ElementReference? PositionerElement) : IStoreState<PopupStoreState<TPayload>>
{
    // PSH-ALG-22; store.ts:146-213. The selectors of the shared state; PopupSelectors<TState, TPayload> lifts them to a
    // family's record.

    /// <summary>Gets the internal open state, written by every committed open change.</summary>
    public bool Open
    {
        get => Lifecycle.Open;
        init => Lifecycle = Lifecycle with { Open = value };
    }

    /// <summary>Gets the controlled <c>Open</c> parameter, or <see langword="null"/> while uncontrolled.</summary>
    public bool? OpenProp
    {
        get => Lifecycle.OpenProp;
        init => Lifecycle = Lifecycle with { OpenProp = value };
    }

    /// <summary>Gets a value indicating whether the popup's subtree is mounted; it lags <see cref="Open"/> through the exit.</summary>
    public bool Mounted
    {
        get => Lifecycle.Mounted;
        init => Lifecycle = Lifecycle with { Mounted = value };
    }

    /// <summary>Gets the stored transition phase.</summary>
    public TransitionStatus? TransitionStatus
    {
        get => Lifecycle.TransitionStatus;
        init => Lifecycle = Lifecycle with { TransitionStatus = value };
    }

    /// <summary>Gets the generated id of the popup element.</summary>
    public string? FloatingId
    {
        get => Lifecycle.FloatingId;
        init => Lifecycle = Lifecycle with { FloatingId = value };
    }

    /// <summary>Gets a value indicating whether the current close cycle stays mounted until the unmount action.</summary>
    public bool PreventUnmountingOnClose
    {
        get => Lifecycle.PreventUnmountingOnClose;
        init => Lifecycle = Lifecycle with { PreventUnmountingOnClose = value };
    }

    /// <summary>Gets how many times the unmount action ended a close.</summary>
    public int UnmountRequests
    {
        get => Lifecycle.UnmountRequests;
        init => Lifecycle = Lifecycle with { UnmountRequests = value };
    }

    /// <summary>Gets the relay of the last committed open change, or <see langword="null"/> before the first.</summary>
    public OpenChangeRelay? OpenChange
    {
        get => Lifecycle.OpenChange;
        init => Lifecycle = Lifecycle with { OpenChange = value };
    }

    /// <summary>Gets how the user opened the popup, or <see langword="null"/> while closed.</summary>
    public InteractionType? OpenMethod
    {
        get => Lifecycle.OpenMethod;
        init => Lifecycle = Lifecycle with { OpenMethod = value };
    }

    // TIP-STA-02 (tooltip/root/TooltipRoot.tsx:68-82: the transitions run on `!disabled && openState`); design point 13.7 of
    // M4 track 6, ADR 05 Amendments 2026-10-28 (settled by the orchestrator 2026-10-28). Tooltip sets it through
    // PopupStore.SetDisabled (Menu does not: its `disabled` only switches the interactions off); the stored open state is
    // left to the family's disabled close.
    /// <summary>Gets a value indicating whether the popup's root is disabled, which keeps it effectively closed.</summary>
    public bool Disabled
    {
        get => Lifecycle.Disabled;
        init => Lifecycle = Lifecycle with { Disabled = value };
    }

    // PSH-STA-02, TIP-STA-02.
    /// <summary>Gets the effective open state: the controlled value when controlled, else the internal one, while the root is not disabled.</summary>
    public bool EffectiveOpen => Lifecycle.EffectiveOpen;

    // PSH-STA-03.
    /// <summary>Gets the effective active trigger id: the controlled value when controlled, else the internal one.</summary>
    public string? EffectiveActiveTriggerId => TriggerIdControlled ? TriggerIdProp : ActiveTriggerId;

    // PSH-STA-04: the popup element's id whenever a popup element is known, even empty; the floating id only when none is
    // known; an empty result resolves to no id.
    /// <summary>Gets the id a trigger's <c>aria-controls</c> names: the popup element's id, else the generated one.</summary>
    public string? PopupId
    {
        get
        {
            string? id = PopupElementKnown ? PopupElementId ?? string.Empty : FloatingId;
            return string.IsNullOrEmpty(id) ? null : id;
        }
    }

    // PSH-STA-05, TRN-STA-14.
    /// <summary>Gets the derived transition status: starting while effectively open and not yet mounted, else the stored one.</summary>
    public TransitionStatus? DerivedTransitionStatus => Lifecycle.DerivedTransitionStatus;

    // PSH-STA-06.
    /// <summary>Gets the active trigger's registration while mounted, and <see langword="null"/> otherwise.</summary>
    public PopupTriggerElement? DerivedActiveTriggerElement => Mounted ? ActiveTriggerElement : null;

    // PSH-STA-02, TIP-STA-02: the one definition (the lifecycle's), also read for a state a write is about to commit.
    /// <summary>Determines the effective open state of a set of inputs.</summary>
    /// <param name="disabled">Whether the root is disabled.</param>
    /// <param name="openProp">The controlled <c>Open</c> parameter, or <see langword="null"/> while uncontrolled.</param>
    /// <param name="open">The internal open state.</param>
    /// <returns><see langword="true"/> if the root is not disabled and the controlled value, else the internal one, is open.</returns>
    public static bool IsEffectivelyOpen(bool disabled, bool? openProp, bool open) => PopupLifecycleState.IsEffectivelyOpen(disabled, openProp, open);

    // store.ts:90-125, createInitialPopupStoreState.
    /// <summary>Creates the initial state of a store.</summary>
    /// <param name="floatingId">The generated id of the popup element.</param>
    /// <returns>A closed, unmounted state with no trigger.</returns>
    public static PopupStoreState<TPayload> Initial(string? floatingId) => new(
        Lifecycle: PopupLifecycleState.Initial(floatingId),
        TriggerCount: 0,
        PayloadVersion: 0,
        ActiveTriggerId: null,
        ActiveTriggerElement: null,
        OpenedWithoutTrigger: false,
        TriggerIdControlled: false,
        TriggerIdProp: null,
        PopupElementKnown: false,
        PopupElementId: null,
        PopupElement: null,
        PositionerElement: null);

    // PSH-STA-07.
    /// <summary>Determines whether the trigger with <paramref name="triggerId"/> is the effective active trigger.</summary>
    /// <param name="triggerId">The trigger's id, or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> if <paramref name="triggerId"/> is defined and equals the effective active trigger id.</returns>
    public bool IsTriggerActive(string? triggerId) => triggerId is not null && string.Equals(EffectiveActiveTriggerId, triggerId, StringComparison.Ordinal);

    // PSH-STA-08.
    /// <summary>Determines whether the popup is open and the trigger with <paramref name="triggerId"/> owns it.</summary>
    /// <param name="triggerId">The trigger's id, or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> if the trigger is active and the popup is effectively open.</returns>
    public bool IsOpenedByTrigger(string? triggerId) => IsTriggerActive(triggerId) && EffectiveOpen;

    // PSH-STA-09.
    /// <summary>Determines whether the popup is mounted and the trigger with <paramref name="triggerId"/> owns it.</summary>
    /// <param name="triggerId">The trigger's id, or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> if the trigger is active and the popup is mounted.</returns>
    public bool IsMountedByTrigger(string? triggerId) => IsTriggerActive(triggerId) && Mounted;

    // PSH-STA-10.
    /// <summary>Gets the popup id the trigger with <paramref name="triggerId"/> names in <c>aria-controls</c>.</summary>
    /// <param name="triggerId">The trigger's id, or <see langword="null"/>.</param>
    /// <returns>The popup id when the trigger owns the open popup or is its lone trigger; otherwise, <see langword="null"/>.</returns>
    public string? TriggerPopupId(string? triggerId)
    {
        bool ownsOrLone = IsOpenedByTrigger(triggerId)
            || (triggerId is not null && EffectiveOpen && EffectiveActiveTriggerId is null && !OpenedWithoutTrigger && TriggerCount == 1);
        return ownsOrLone ? PopupId : null;
    }
}

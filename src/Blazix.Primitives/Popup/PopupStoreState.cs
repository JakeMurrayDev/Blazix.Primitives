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
/// <summary>Represents the state every popup store shares, whatever its family.</summary>
/// <typeparam name="TPayload">The type of the payload the popup's triggers carry.</typeparam>
/// <param name="Open">The internal open state, written by every committed open change.</param>
/// <param name="OpenProp">The controlled <c>Open</c> parameter, or <see langword="null"/> while uncontrolled.</param>
/// <param name="Mounted">Whether the popup's subtree is mounted; it lags <paramref name="Open"/> through the exit.</param>
/// <param name="TransitionStatus">The stored transition phase.</param>
/// <param name="FloatingId">The generated id of the popup element.</param>
/// <param name="TriggerCount">The number of registered triggers while open; 0 while closed.</param>
/// <param name="PreventUnmountingOnClose">Whether the current close cycle stays mounted until the unmount action.</param>
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
/// <param name="UnmountRequests">How many times the unmount action ended a close.</param>
/// <param name="OpenChange">The relay of the last committed open change, or <see langword="null"/> before the first.</param>
/// <param name="OpenMethod">How the user opened the popup, or <see langword="null"/> while closed.</param>
[StoreState]
internal sealed partial record PopupStoreState<TPayload>(
    bool Open,
    bool? OpenProp,
    bool Mounted,
    TransitionStatus? TransitionStatus,
    string? FloatingId,
    int TriggerCount,
    bool PreventUnmountingOnClose,
    int PayloadVersion,
    string? ActiveTriggerId,
    PopupTriggerElement? ActiveTriggerElement,
    bool OpenedWithoutTrigger,
    bool TriggerIdControlled,
    string? TriggerIdProp,
    bool PopupElementKnown,
    string? PopupElementId,
    ElementReference? PopupElement,
    ElementReference? PositionerElement,
    int UnmountRequests,
    OpenChangeRelay? OpenChange,
    InteractionType? OpenMethod) : IStoreState<PopupStoreState<TPayload>>
{
    // PSH-ALG-22; store.ts:146-213. The selectors of the shared state; PopupSelectors<TState, TPayload> lifts them to a
    // family's record.

    // PSH-STA-02.
    /// <summary>Gets the effective open state: the controlled value when controlled, else the internal one.</summary>
    public bool EffectiveOpen => OpenProp ?? Open;

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
    public TransitionStatus? DerivedTransitionStatus => EffectiveOpen && !Mounted ? Primitives.TransitionStatus.Starting : TransitionStatus;

    // PSH-STA-06.
    /// <summary>Gets the active trigger's registration while mounted, and <see langword="null"/> otherwise.</summary>
    public PopupTriggerElement? DerivedActiveTriggerElement => Mounted ? ActiveTriggerElement : null;

    // store.ts:90-125, createInitialPopupStoreState.
    /// <summary>Creates the initial state of a store.</summary>
    /// <param name="floatingId">The generated id of the popup element.</param>
    /// <returns>A closed, unmounted state with no trigger.</returns>
    public static PopupStoreState<TPayload> Initial(string? floatingId) => new(
        Open: false,
        OpenProp: null,
        Mounted: false,
        TransitionStatus: null,
        FloatingId: floatingId,
        TriggerCount: 0,
        PreventUnmountingOnClose: false,
        PayloadVersion: 0,
        ActiveTriggerId: null,
        ActiveTriggerElement: null,
        OpenedWithoutTrigger: false,
        TriggerIdControlled: false,
        TriggerIdProp: null,
        PopupElementKnown: false,
        PopupElementId: null,
        PopupElement: null,
        PositionerElement: null,
        UnmountRequests: 0,
        OpenChange: null,
        OpenMethod: null);

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

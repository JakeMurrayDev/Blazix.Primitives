using Blazix.Primitives.Interop;
using Blazix.Primitives.Store;

namespace Blazix.Primitives;

// PSH-ALG-22 (popupStoreSelectors, store.ts:146-213) as named selectors over a family's record (ADR 05 §1: a selector is a
// static readonly Func field): a part subscribes to its family's store and selects through these, so each part renders
// only when its own selection changed (ADR 16 rules 8 and 9).
/// <summary>Provides the named selectors of the shared popup state over a family's store record.</summary>
/// <typeparam name="TState">The type of the family's store record.</typeparam>
/// <typeparam name="TPayload">The type of the payload the popup's triggers carry.</typeparam>
internal static class PopupSelectors<TState, TPayload>
    where TState : class, IStoreState<TState>, IPopupStoreState<TState, TPayload>
{
    // PSH-STA-02.
    /// <summary>Selects the effective open state.</summary>
    public static readonly Func<TState, bool> Open = static state => state.Popup.EffectiveOpen;

    /// <summary>Selects whether the popup's subtree is mounted.</summary>
    public static readonly Func<TState, bool> Mounted = static state => state.Popup.Mounted;

    // PSH-STA-05.
    /// <summary>Selects the derived transition status.</summary>
    public static readonly Func<TState, TransitionStatus?> TransitionStatus = static state => state.Popup.DerivedTransitionStatus;

    /// <summary>Selects whether the current close cycle stays mounted until the unmount action.</summary>
    public static readonly Func<TState, bool> PreventUnmountingOnClose = static state => state.Popup.PreventUnmountingOnClose;

    /// <summary>Selects how many times the unmount action ended a close.</summary>
    public static readonly Func<TState, int> UnmountRequests = static state => state.Popup.UnmountRequests;

    /// <summary>Selects the number of registered triggers while open.</summary>
    public static readonly Func<TState, int> TriggerCount = static state => state.Popup.TriggerCount;

    /// <summary>Selects the payload's version, which moves whenever the payload is written.</summary>
    public static readonly Func<TState, int> PayloadVersion = static state => state.Popup.PayloadVersion;

    // PSH-STA-03.
    /// <summary>Selects the effective active trigger id.</summary>
    public static readonly Func<TState, string?> ActiveTriggerId = static state => state.Popup.EffectiveActiveTriggerId;

    // PSH-STA-06.
    /// <summary>Selects the active trigger while mounted.</summary>
    public static readonly Func<TState, PopupTriggerElement?> ActiveTriggerElement = static state => state.Popup.DerivedActiveTriggerElement;

    // PSH-STA-04.
    /// <summary>Selects the popup id.</summary>
    public static readonly Func<TState, string?> PopupId = static state => state.Popup.PopupId;

    /// <summary>Selects whether the popup opened through a request that carried no trigger.</summary>
    public static readonly Func<TState, bool> OpenedWithoutTrigger = static state => state.Popup.OpenedWithoutTrigger;

    /// <summary>Selects how the user opened the popup.</summary>
    public static readonly Func<TState, InteractionType?> OpenMethod = static state => state.Popup.OpenMethod;

    /// <summary>Selects the relay of the last committed open change.</summary>
    public static readonly Func<TState, OpenChangeRelay?> OpenChange = static state => state.Popup.OpenChange;

    // PSH-STA-07.
    /// <summary>Selects whether the trigger with the given id is active.</summary>
    public static readonly Func<TState, string?, bool> IsTriggerActive = static (state, triggerId) => state.Popup.IsTriggerActive(triggerId);

    // PSH-STA-08.
    /// <summary>Selects whether the popup is open and owned by the trigger with the given id.</summary>
    public static readonly Func<TState, string?, bool> IsOpenedByTrigger = static (state, triggerId) => state.Popup.IsOpenedByTrigger(triggerId);

    // PSH-STA-09.
    /// <summary>Selects whether the popup is mounted and owned by the trigger with the given id.</summary>
    public static readonly Func<TState, string?, bool> IsMountedByTrigger = static (state, triggerId) => state.Popup.IsMountedByTrigger(triggerId);

    // PSH-STA-10.
    /// <summary>Selects the popup id the trigger with the given id names in <c>aria-controls</c>.</summary>
    public static readonly Func<TState, string?, string?> TriggerPopupId = static (state, triggerId) => state.Popup.TriggerPopupId(triggerId);
}

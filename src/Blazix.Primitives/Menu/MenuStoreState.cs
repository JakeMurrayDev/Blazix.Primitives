using Blazix.Primitives.Interop;
using Blazix.Primitives.Store;

namespace Blazix.Primitives;

// MNU §4.1 (menu/store/MenuStore.ts:18-52, 76-149, 297-326); ADR 05 Amendments 2026-10-27 and 2026-11-03 (chosen by the
// user): the shared popup state is one member, the Menu fields beside it (the PopoverStoreState precedent, plan §Settled).
// Of Base UI's 24 fields, these are C#'s (sheet B §4.1): `disabled` (the Root's own, not PopupStoreState.Disabled, ADR 05
// Amendments 2026-10-28), `modal` as supplied, `keyboardOpen`, `parent`, `rootId`, the List's id (`listElement`),
// `instantType`, `openChangeReason`, `closeDelay`, `adaptiveOrigin` (a mounted Viewport), `highlightItemOnHover`. Left out:
// `openMethod` is the shared record's OpenMethod; `activeIndex` and `highlightedItem` are the list handle's mirror
// (ListNavigationBehavior, track 1); `allowMouseEnter` and `hoverEnabled` are the `interactions` module's (ADR 04
// Amendments 2026-11-03 (4)); the tree identity is the session token and its ParentToken; the prop bags have no
// counterpart; `keyboardEventRelay` is the list handle's RelayTarget. The filter fields are track 12's.
/// <summary>Represents the state of one menu: the shared popup state and the fields only Menu has.</summary>
/// <typeparam name="TPayload">The type of the payload the menu's triggers carry.</typeparam>
/// <param name="Popup">The shared popup state.</param>
/// <param name="Disabled">The root's own <c>Disabled</c>; a menubar parent's is combined by <see cref="IMenuState.Disabled"/>.</param>
/// <param name="Modal">The root's <c>Modal</c> as supplied, or <see langword="null"/>.</param>
/// <param name="KeyboardOpen">Whether the menu last opened from the keyboard or an assistive-technology press.</param>
/// <param name="Parent">The menu's resolved parent.</param>
/// <param name="RootId">The root's own generated id; <see cref="IMenuState.RootId"/> reads the parent chain.</param>
/// <param name="ListElementId">The id of a mounted <c>MenuList</c>, or <see langword="null"/>.</param>
/// <param name="InstantType">The stored instant type, before the public value hides <see cref="MenuInstantType.TriggerChange"/> while closed.</param>
/// <param name="OpenChangeReason">The reason of the last applied open change, or <see langword="null"/> after the menu unmounted.</param>
/// <param name="CloseDelay">The active trigger's close delay in milliseconds.</param>
/// <param name="AdaptiveOrigin">Whether a viewport is mounted, which positions the menu by its edges instead of a transform.</param>
/// <param name="HighlightItemOnHover">The root's <c>HighlightItemOnHover</c>.</param>
/// <param name="CloseEvent">The snapshot of the event that caused the last committed close, or <see langword="null"/> before any close.</param>
[StoreState]
internal sealed partial record MenuStoreState<TPayload>(
    PopupStoreState<TPayload> Popup,
    bool Disabled,
    bool? Modal,
    bool KeyboardOpen,
    MenuParent Parent,
    string? RootId,
    string? ListElementId,
    MenuInstantType? InstantType,
    EventReason? OpenChangeReason,
    int CloseDelay,
    bool AdaptiveOrigin,
    bool HighlightItemOnHover,
    EventSnapshot? CloseEvent)
    : IStoreState<MenuStoreState<TPayload>>, IPopupStoreState<MenuStoreState<TPayload>, TPayload>, IMenuState
{
    /// <inheritdoc />
    public PopupStoreState<TPayload> Popup { get; init; } = Popup;

    /// <inheritdoc />
    bool IMenuState.Open => Popup.EffectiveOpen;

    /// <inheritdoc />
    bool IMenuState.Mounted => Popup.Mounted;

    /// <inheritdoc />
    TransitionStatus? IMenuState.TransitionStatus => Popup.DerivedTransitionStatus;

    /// <inheritdoc />
    string? IMenuState.FloatingId => Popup.FloatingId;

    /// <inheritdoc />
    string? IMenuState.PopupId => Popup.PopupId;

    /// <inheritdoc />
    string? IMenuState.ActiveTriggerId => Popup.EffectiveActiveTriggerId;

    /// <inheritdoc />
    PopupTriggerElement? IMenuState.ActiveTriggerElement => Popup.DerivedActiveTriggerElement;

    /// <inheritdoc />
    InteractionType? IMenuState.OpenMethod => Popup.OpenMethod;

    /// <inheritdoc />
    int IMenuState.UnmountRequests => Popup.UnmountRequests;

    /// <inheritdoc />
    bool IMenuState.PreventUnmountingOnClose => Popup.PreventUnmountingOnClose;

    /// <inheritdoc />
    int IMenuState.PayloadVersion => Popup.PayloadVersion;

    /// <inheritdoc />
    OpenChangeRelay? IMenuState.OpenChange => Popup.OpenChange;

    // MNU-STA-07 (MenuStore.ts:77-80).
    /// <inheritdoc />
    bool IMenuState.Disabled => Disabled || (Parent.Kind == MenuParentKind.Menubar && Parent.Context!.Disabled);

    // MNU-STA-06 (MenuStore.ts:81-83).
    /// <inheritdoc />
    bool IMenuState.Modal => Parent.Kind is MenuParentKind.None or MenuParentKind.ContextMenu && Modal != false;

    /// <inheritdoc />
    bool IMenuState.ModalSupplied => Modal is not null;

    /// <inheritdoc />
    bool IMenuState.KeyboardOpen => KeyboardOpen;

    /// <inheritdoc />
    MenuParent IMenuState.Parent => Parent;

    // MenuStore.ts:91-97.
    /// <inheritdoc />
    string? IMenuState.RootId => Parent.Kind switch
    {
        MenuParentKind.Menu => Parent.Menu!.State.RootId,
        MenuParentKind.None => RootId,
        _ => Parent.Context!.RootId,
    };

    /// <inheritdoc />
    string? IMenuState.ListElementId => ListElementId;

    // MNU-STA-04, TRN-ANI-29 (MenuStore.ts:122-130).
    /// <inheritdoc />
    MenuInstantType? IMenuState.PublicInstantType => InstantType == MenuInstantType.TriggerChange && !Popup.EffectiveOpen ? null : InstantType;

    /// <inheritdoc />
    MenuInstantType? IMenuState.InstantType => InstantType;

    /// <inheritdoc />
    EventReason? IMenuState.OpenChangeReason => OpenChangeReason;

    /// <inheritdoc />
    bool IMenuState.HoverOpened => OpenChangeReason == EventReason.TriggerHover;

    /// <inheritdoc />
    int IMenuState.CloseDelay => CloseDelay;

    /// <inheritdoc />
    bool IMenuState.AdaptiveOrigin => AdaptiveOrigin;

    /// <inheritdoc />
    bool IMenuState.HighlightItemOnHover => HighlightItemOnHover;

    /// <inheritdoc />
    EventSnapshot? IMenuState.CloseEvent => CloseEvent;

    /// <inheritdoc />
    string? IMenuState.TriggerPopupId(string? triggerId) => Popup.TriggerPopupId(triggerId);

    /// <summary>Creates the initial state of a store.</summary>
    /// <param name="floatingId">The generated id of the popup element.</param>
    /// <returns>A closed top-level menu with no trigger, modal by default.</returns>
    public static MenuStoreState<TPayload> Initial(string? floatingId) => new(
        PopupStoreState<TPayload>.Initial(floatingId),
        Disabled: false,
        Modal: null,
        KeyboardOpen: false,
        Parent: MenuParent.None,
        RootId: null,
        ListElementId: null,
        InstantType: null,
        OpenChangeReason: null,
        CloseDelay: 0,
        AdaptiveOrigin: false,
        HighlightItemOnHover: true,
        CloseEvent: null);

    /// <inheritdoc />
    public static MenuStoreState<TPayload> WithPopup(MenuStoreState<TPayload> state, PopupStoreState<TPayload> popup) => state with { Popup = popup };
}

// The view the non-generic parts read (the IPopoverState precedent): a selector over this interface converts to a selector
// over the generic record by delegate variance, so the parts below the Root select without naming TPayload.
/// <summary>Defines the state of a menu as the parts that do not know its payload type read it.</summary>
internal interface IMenuState
{
    /// <summary>Gets a value indicating whether the menu is effectively open.</summary>
    bool Open { get; }

    /// <summary>Gets a value indicating whether the menu's subtree is mounted.</summary>
    bool Mounted { get; }

    /// <summary>Gets the derived transition status.</summary>
    TransitionStatus? TransitionStatus { get; }

    /// <summary>Gets the generated id of the popup element.</summary>
    string? FloatingId { get; }

    /// <summary>Gets the id of the rendered popup element.</summary>
    string? PopupId { get; }

    /// <summary>Gets the effective active trigger id.</summary>
    string? ActiveTriggerId { get; }

    /// <summary>Gets the active trigger's registration while mounted.</summary>
    PopupTriggerElement? ActiveTriggerElement { get; }

    /// <summary>Gets how the user opened the menu.</summary>
    InteractionType? OpenMethod { get; }

    /// <summary>Gets how many times the unmount action ended a close.</summary>
    int UnmountRequests { get; }

    /// <summary>Gets a value indicating whether the current close keeps the popup mounted until the unmount action runs.</summary>
    bool PreventUnmountingOnClose { get; }

    /// <summary>Gets the payload's version.</summary>
    int PayloadVersion { get; }

    /// <summary>Gets the relay of the last committed open change.</summary>
    OpenChangeRelay? OpenChange { get; }

    /// <summary>Gets a value indicating whether the menu ignores user interaction: its root's <c>Disabled</c>, or its menubar's.</summary>
    bool Disabled { get; }

    /// <summary>Gets a value indicating whether the menu is modal: a top-level or context menu whose <c>Modal</c> is not <see langword="false"/>.</summary>
    bool Modal { get; }

    /// <summary>Gets a value indicating whether the root was given a <c>Modal</c> value.</summary>
    bool ModalSupplied { get; }

    /// <summary>Gets a value indicating whether the menu last opened from the keyboard or an assistive-technology press.</summary>
    bool KeyboardOpen { get; }

    /// <summary>Gets the menu's resolved parent.</summary>
    MenuParent Parent { get; }

    /// <summary>Gets the id of the menu tree's root: the parent's, else the root's own.</summary>
    string? RootId { get; }

    /// <summary>Gets the id of a mounted list, or <see langword="null"/>.</summary>
    string? ListElementId { get; }

    /// <summary>Gets the stored instant type.</summary>
    MenuInstantType? InstantType { get; }

    /// <summary>Gets the instant type the parts render: the stored one, without <see cref="MenuInstantType.TriggerChange"/> while closed.</summary>
    MenuInstantType? PublicInstantType { get; }

    /// <summary>Gets the reason of the last applied open change.</summary>
    EventReason? OpenChangeReason { get; }

    /// <summary>Gets a value indicating whether the last applied open change was a hover.</summary>
    bool HoverOpened { get; }

    /// <summary>Gets the active trigger's close delay in milliseconds.</summary>
    int CloseDelay { get; }

    /// <summary>Gets a value indicating whether a viewport is mounted.</summary>
    bool AdaptiveOrigin { get; }

    /// <summary>Gets a value indicating whether moving the pointer over items highlights them.</summary>
    bool HighlightItemOnHover { get; }

    /// <summary>Gets the snapshot of the event that caused the last committed close.</summary>
    EventSnapshot? CloseEvent { get; }

    // PSH-STA-10: the popup id a trigger names while it owns the open popup, or is the lone trigger of a trigger-less one.
    /// <summary>Gets the popup id the trigger with the given id names in its <c>aria-controls</c>.</summary>
    /// <param name="triggerId">The trigger's id.</param>
    /// <returns>The popup id, or <see langword="null"/>.</returns>
    string? TriggerPopupId(string? triggerId);
}

// The parts' selections (MenuStore.ts:76-149) as named selectors over the non-generic view (ADR 05 §1).
/// <summary>Provides the named selectors of a menu's state.</summary>
internal static class MenuSelectors
{
    /// <summary>Selects the effective open state.</summary>
    public static readonly Func<IMenuState, bool> Open = static state => state.Open;

    /// <summary>Selects whether the subtree is mounted.</summary>
    public static readonly Func<IMenuState, bool> Mounted = static state => state.Mounted;

    /// <summary>Selects the derived transition status.</summary>
    public static readonly Func<IMenuState, TransitionStatus?> TransitionStatus = static state => state.TransitionStatus;

    /// <summary>Selects the public instant type.</summary>
    public static readonly Func<IMenuState, MenuInstantType?> InstantType = static state => state.PublicInstantType;

    /// <summary>Selects whether the menu is modal.</summary>
    public static readonly Func<IMenuState, bool> Modal = static state => state.Modal;

    /// <summary>Selects whether the menu is disabled.</summary>
    public static readonly Func<IMenuState, bool> Disabled = static state => state.Disabled;

    /// <summary>Selects whether the last applied open change was a hover.</summary>
    public static readonly Func<IMenuState, bool> HoverOpened = static state => state.HoverOpened;

    /// <summary>Selects the reason of the last applied open change.</summary>
    public static readonly Func<IMenuState, EventReason?> OpenChangeReason = static state => state.OpenChangeReason;

    /// <summary>Selects how the user opened the menu.</summary>
    public static readonly Func<IMenuState, InteractionType?> OpenMethod = static state => state.OpenMethod;

    /// <summary>Selects whether the menu last opened from the keyboard.</summary>
    public static readonly Func<IMenuState, bool> KeyboardOpen = static state => state.KeyboardOpen;

    /// <summary>Selects the effective active trigger id.</summary>
    public static readonly Func<IMenuState, string?> ActiveTriggerId = static state => state.ActiveTriggerId;

    /// <summary>Selects the active trigger's registration while mounted.</summary>
    public static readonly Func<IMenuState, PopupTriggerElement?> ActiveTriggerElement = static state => state.ActiveTriggerElement;

    /// <summary>Selects the popup id.</summary>
    public static readonly Func<IMenuState, string?> PopupId = static state => state.PopupId;

    /// <summary>Selects the list's id.</summary>
    public static readonly Func<IMenuState, string?> ListElementId = static state => state.ListElementId;

    /// <summary>Selects the tree's root id.</summary>
    public static readonly Func<IMenuState, string?> RootId = static state => state.RootId;

    /// <summary>Selects how many times the unmount action ended a close.</summary>
    public static readonly Func<IMenuState, int> UnmountRequests = static state => state.UnmountRequests;

    /// <summary>Selects whether the current close keeps the popup mounted until the unmount action runs.</summary>
    public static readonly Func<IMenuState, bool> PreventUnmountingOnClose = static state => state.PreventUnmountingOnClose;

    /// <summary>Selects the active trigger's close delay.</summary>
    public static readonly Func<IMenuState, int> CloseDelay = static state => state.CloseDelay;

    /// <summary>Selects the payload's version.</summary>
    public static readonly Func<IMenuState, int> PayloadVersion = static state => state.PayloadVersion;

    /// <summary>Selects whether a viewport is mounted.</summary>
    public static readonly Func<IMenuState, bool> AdaptiveOrigin = static state => state.AdaptiveOrigin;

    /// <summary>Selects whether items are highlighted on hover.</summary>
    public static readonly Func<IMenuState, bool> HighlightItemOnHover = static state => state.HighlightItemOnHover;

    /// <summary>Selects the relay of the last committed open change.</summary>
    public static readonly Func<IMenuState, OpenChangeRelay?> OpenChange = static state => state.OpenChange;

    /// <summary>Selects the snapshot of the event that caused the last committed close.</summary>
    public static readonly Func<IMenuState, EventSnapshot?> CloseEvent = static state => state.CloseEvent;

    // MenuStore.ts:100-103 (MNU-ARIA-03): the list when one holds the `menu` role, else the popup.
    /// <summary>Selects the id a trigger with the given id names in its <c>aria-controls</c>.</summary>
    public static readonly Func<IMenuState, string?, string?> TriggerControlsId = static (state, triggerId) =>
        state.TriggerPopupId(triggerId) is { } popupId ? (string.IsNullOrEmpty(state.ListElementId) ? popupId : state.ListElementId) : null;

    // MNU-DOM-01 (pressableTriggerOpenStateMapping): data-popup-open and data-pressed while the trigger owns the open menu.
    /// <summary>Selects whether the menu is open and owned by the trigger with the given id.</summary>
    public static readonly Func<IMenuState, string?, bool> IsOpenedByTrigger = static (state, triggerId) => IsOpenedBy(state, triggerId);

    // MNU-STR-16: the guards around the trigger that owns the open popup (never in a menubar).
    /// <summary>Selects whether the trigger with the given id renders focus guards.</summary>
    public static readonly Func<IMenuState, string?, bool> HasTriggerFocusGuards = static (state, triggerId) =>
        IsOpenedBy(state, triggerId) && state.Parent.Kind != MenuParentKind.Menubar;

    private static bool IsOpenedBy(IMenuState state, string? triggerId) =>
        triggerId is not null && state.Open && string.Equals(state.ActiveTriggerId, triggerId, StringComparison.Ordinal);
}

using Blazix.Primitives.Interop;
using Blazix.Primitives.Store;

namespace Blazix.Primitives;

// POP §4.2 (popover/store/PopoverStore.ts:22-44, 204-245); ADR 05 Amendments 2026-10-27 (chosen by the user
// 2026-10-27): the shared popup state is one member, the Popover fields beside it, named after Base UI's. Left out:
// `adaptiveOrigin` is the positioning module's middleware, so the record holds whether a Viewport asks for it
// (PSH-POS-02); `openMethod` is the shared record's OpenMethod (PSH-PTR-01/02); `stickIfOpen` and its timer are the
// `interactions` module's (ADR 04 Amendments 2026-10-27; POP-STA-11 at L4). `modal` is held as PopoverModalKind, since
// ModalValue has no value equality.
/// <summary>Represents the state of one popover: the shared popup state and the fields only Popover has.</summary>
/// <typeparam name="TPayload">The type of the payload the popover's triggers carry.</typeparam>
/// <param name="Popup">The shared popup state.</param>
/// <param name="Disabled">The active trigger's <c>Disabled</c>, not the Root's gate that <c>PopupStoreState.Disabled</c> holds.</param>
/// <param name="InstantType">The stored instant type, before the public value hides <see cref="PopoverInstantType.TriggerChange"/> while closed.</param>
/// <param name="Modal">The root's <c>Modal</c>.</param>
/// <param name="FocusManagerModal">Whether focus is trapped: <c>Modal</c> is not off and a close part is mounted inside the popup.</param>
/// <param name="OpenChangeReason">The reason of the last applied open change, or <see langword="null"/> after the popover unmounted.</param>
/// <param name="TitleElementId">The id of the mounted title, or <see langword="null"/>.</param>
/// <param name="DescriptionElementId">The id of the mounted description, or <see langword="null"/>.</param>
/// <param name="OpenOnHover">The active trigger's <c>OpenOnHover</c>.</param>
/// <param name="CloseDelay">The active trigger's close delay in milliseconds.</param>
/// <param name="AdaptiveOrigin">Whether a viewport is mounted, which positions the popover by its edges instead of a transform.</param>
/// <param name="CloseEvent">The snapshot of the event that caused the last committed close, or <see langword="null"/> before any close.</param>
[StoreState]
internal sealed partial record PopoverStoreState<TPayload>(
    PopupStoreState<TPayload> Popup,
    bool Disabled,
    PopoverInstantType? InstantType,
    PopoverModalKind Modal,
    bool FocusManagerModal,
    EventReason? OpenChangeReason,
    string? TitleElementId,
    string? DescriptionElementId,
    bool OpenOnHover,
    int CloseDelay,
    bool AdaptiveOrigin,
    EventSnapshot? CloseEvent)
    : IStoreState<PopoverStoreState<TPayload>>, IPopupStoreState<PopoverStoreState<TPayload>, TPayload>, IPopoverState
{
    // POP-STA-09 (useSyncedValueWithCleanup): the Title's and Description's ids, synced by SyncedValueWithCleanup.
    /// <summary>The key of the title's id.</summary>
    public static readonly StoreField<PopoverStoreState<TPayload>, string?> TitleElementIdField =
        new(nameof(TitleElementId), static state => state.TitleElementId, static (state, value) => state with { TitleElementId = value });

    /// <summary>The key of the description's id.</summary>
    public static readonly StoreField<PopoverStoreState<TPayload>, string?> DescriptionElementIdField =
        new(nameof(DescriptionElementId), static state => state.DescriptionElementId, static (state, value) => state with { DescriptionElementId = value });

    /// <inheritdoc />
    public PopupStoreState<TPayload> Popup { get; init; } = Popup;

    /// <inheritdoc />
    bool IPopoverState.Open => Popup.EffectiveOpen;

    /// <inheritdoc />
    bool IPopoverState.Mounted => Popup.Mounted;

    /// <inheritdoc />
    TransitionStatus? IPopoverState.TransitionStatus => Popup.DerivedTransitionStatus;

    /// <inheritdoc />
    string? IPopoverState.FloatingId => Popup.FloatingId;

    /// <inheritdoc />
    string? IPopoverState.PopupId => Popup.PopupId;

    /// <inheritdoc />
    string? IPopoverState.ActiveTriggerId => Popup.EffectiveActiveTriggerId;

    /// <inheritdoc />
    PopupTriggerElement? IPopoverState.ActiveTriggerElement => Popup.DerivedActiveTriggerElement;

    /// <inheritdoc />
    InteractionType? IPopoverState.OpenMethod => Popup.OpenMethod;

    /// <inheritdoc />
    int IPopoverState.UnmountRequests => Popup.UnmountRequests;

    /// <inheritdoc />
    int IPopoverState.PayloadVersion => Popup.PayloadVersion;

    /// <inheritdoc />
    OpenChangeRelay? IPopoverState.OpenChange => Popup.OpenChange;

    // POP-STA-07, TRN-ANI-29 (PopoverStore.ts:54-57): `trigger-change` describes a move between triggers, which only has
    // meaning while open.
    /// <inheritdoc />
    PopoverInstantType? IPopoverState.PublicInstantType => InstantType == PopoverInstantType.TriggerChange && !Popup.EffectiveOpen ? null : InstantType;

    /// <inheritdoc />
    bool IPopoverState.HoverOpened => OpenChangeReason == EventReason.TriggerHover;

    /// <inheritdoc />
    bool IPopoverState.Disabled => Disabled;

    /// <inheritdoc />
    PopoverInstantType? IPopoverState.InstantType => InstantType;

    /// <inheritdoc />
    PopoverModalKind IPopoverState.Modal => Modal;

    /// <inheritdoc />
    bool IPopoverState.FocusManagerModal => FocusManagerModal;

    /// <inheritdoc />
    EventReason? IPopoverState.OpenChangeReason => OpenChangeReason;

    /// <inheritdoc />
    string? IPopoverState.TitleElementId => TitleElementId;

    /// <inheritdoc />
    string? IPopoverState.DescriptionElementId => DescriptionElementId;

    /// <inheritdoc />
    bool IPopoverState.OpenOnHover => OpenOnHover;

    /// <inheritdoc />
    int IPopoverState.CloseDelay => CloseDelay;

    /// <inheritdoc />
    bool IPopoverState.AdaptiveOrigin => AdaptiveOrigin;

    /// <inheritdoc />
    EventSnapshot? IPopoverState.CloseEvent => CloseEvent;

    /// <summary>Creates the initial state of a store.</summary>
    /// <param name="floatingId">The generated id of the popup element.</param>
    /// <returns>A closed popover with no trigger, non-modal, with no title or description.</returns>
    public static PopoverStoreState<TPayload> Initial(string? floatingId) => new(
        PopupStoreState<TPayload>.Initial(floatingId),
        Disabled: false,
        InstantType: null,
        Modal: PopoverModalKind.NonModal,
        FocusManagerModal: false,
        OpenChangeReason: null,
        TitleElementId: null,
        DescriptionElementId: null,
        OpenOnHover: false,
        CloseDelay: 0,
        AdaptiveOrigin: false,
        CloseEvent: null);

    /// <inheritdoc />
    public static PopoverStoreState<TPayload> WithPopup(PopoverStoreState<TPayload> state, PopupStoreState<TPayload> popup) => state with { Popup = popup };
}

/// <summary>Specifies the root's <c>Modal</c>, as the popover's state holds it.</summary>
internal enum PopoverModalKind
{
    /// <summary>The popover is not modal (<c>false</c>).</summary>
    NonModal,

    /// <summary>The popover is modal (<c>true</c>): an internal backdrop, a scroll lock and, with a close part inside, a focus trap.</summary>
    Modal,

    /// <summary>The popover traps focus only (<c>trap-focus</c>).</summary>
    TrapFocus,
}

// The view the non-generic parts read (the Tabs precedent of ADR 08 Amendments 2026-10-26: a non-generic context base): a
// selector over this interface converts to a selector over the generic record by delegate variance, so the parts below the
// Root select without naming TPayload and without an allocation per evaluation.
/// <summary>Defines the state of a popover as the parts that do not know its payload type read it.</summary>
internal interface IPopoverState
{
    /// <summary>Gets a value indicating whether the popover is effectively open.</summary>
    bool Open { get; }

    /// <summary>Gets a value indicating whether the popover's subtree is mounted.</summary>
    bool Mounted { get; }

    /// <summary>Gets the derived transition status.</summary>
    TransitionStatus? TransitionStatus { get; }

    /// <summary>Gets the generated id of the popup element.</summary>
    string? FloatingId { get; }

    /// <summary>Gets the id a trigger's <c>aria-controls</c> names.</summary>
    string? PopupId { get; }

    /// <summary>Gets the effective active trigger id.</summary>
    string? ActiveTriggerId { get; }

    /// <summary>Gets the active trigger's registration while mounted.</summary>
    PopupTriggerElement? ActiveTriggerElement { get; }

    /// <summary>Gets how the user opened the popover.</summary>
    InteractionType? OpenMethod { get; }

    /// <summary>Gets how many times the unmount action ended a close.</summary>
    int UnmountRequests { get; }

    /// <summary>Gets the payload's version.</summary>
    int PayloadVersion { get; }

    /// <summary>Gets the relay of the last committed open change.</summary>
    OpenChangeRelay? OpenChange { get; }

    /// <summary>Gets the active trigger's <c>Disabled</c>, not the Root's gate that <c>PopupStoreState.Disabled</c> holds.</summary>
    bool Disabled { get; }

    /// <summary>Gets the stored instant type.</summary>
    PopoverInstantType? InstantType { get; }

    /// <summary>Gets the instant type the parts render: the stored one, without <see cref="PopoverInstantType.TriggerChange"/> while closed.</summary>
    PopoverInstantType? PublicInstantType { get; }

    /// <summary>Gets the root's <c>Modal</c>.</summary>
    PopoverModalKind Modal { get; }

    /// <summary>Gets a value indicating whether focus is trapped in the popup.</summary>
    bool FocusManagerModal { get; }

    /// <summary>Gets the reason of the last applied open change.</summary>
    EventReason? OpenChangeReason { get; }

    /// <summary>Gets a value indicating whether the last applied open change was a hover.</summary>
    bool HoverOpened { get; }

    /// <summary>Gets the id of the mounted title.</summary>
    string? TitleElementId { get; }

    /// <summary>Gets the id of the mounted description.</summary>
    string? DescriptionElementId { get; }

    /// <summary>Gets the active trigger's <c>OpenOnHover</c>.</summary>
    bool OpenOnHover { get; }

    /// <summary>Gets the active trigger's close delay in milliseconds.</summary>
    int CloseDelay { get; }

    /// <summary>Gets a value indicating whether a viewport is mounted.</summary>
    bool AdaptiveOrigin { get; }

    /// <summary>Gets the snapshot of the event that caused the last committed close.</summary>
    EventSnapshot? CloseEvent { get; }
}

// POP-STA-12 and the parts' selections (PopoverStore.ts:46-82) as named selectors over the non-generic view (ADR 05 §1).
/// <summary>Provides the named selectors of a popover's state.</summary>
internal static class PopoverSelectors
{
    /// <summary>Selects the effective open state.</summary>
    public static readonly Func<IPopoverState, bool> Open = static state => state.Open;

    /// <summary>Selects whether the subtree is mounted.</summary>
    public static readonly Func<IPopoverState, bool> Mounted = static state => state.Mounted;

    /// <summary>Selects the derived transition status.</summary>
    public static readonly Func<IPopoverState, TransitionStatus?> TransitionStatus = static state => state.TransitionStatus;

    /// <summary>Selects the public instant type.</summary>
    public static readonly Func<IPopoverState, PopoverInstantType?> InstantType = static state => state.PublicInstantType;

    /// <summary>Selects the root's modal mode.</summary>
    public static readonly Func<IPopoverState, PopoverModalKind> Modal = static state => state.Modal;

    /// <summary>Selects whether focus is trapped.</summary>
    public static readonly Func<IPopoverState, bool> FocusManagerModal = static state => state.FocusManagerModal;

    /// <summary>Selects whether the last applied open change was a hover.</summary>
    public static readonly Func<IPopoverState, bool> HoverOpened = static state => state.HoverOpened;

    /// <summary>Selects the reason of the last applied open change.</summary>
    public static readonly Func<IPopoverState, EventReason?> OpenChangeReason = static state => state.OpenChangeReason;

    /// <summary>Selects how the user opened the popover.</summary>
    public static readonly Func<IPopoverState, InteractionType?> OpenMethod = static state => state.OpenMethod;

    /// <summary>Selects the title's id.</summary>
    public static readonly Func<IPopoverState, string?> TitleElementId = static state => state.TitleElementId;

    /// <summary>Selects the description's id.</summary>
    public static readonly Func<IPopoverState, string?> DescriptionElementId = static state => state.DescriptionElementId;

    /// <summary>Selects the effective active trigger id.</summary>
    public static readonly Func<IPopoverState, string?> ActiveTriggerId = static state => state.ActiveTriggerId;

    /// <summary>Selects the active trigger's registration while mounted.</summary>
    public static readonly Func<IPopoverState, PopupTriggerElement?> ActiveTriggerElement = static state => state.ActiveTriggerElement;

    /// <summary>Selects the popup id.</summary>
    public static readonly Func<IPopoverState, string?> PopupId = static state => state.PopupId;

    /// <summary>Selects how many times the unmount action ended a close.</summary>
    public static readonly Func<IPopoverState, int> UnmountRequests = static state => state.UnmountRequests;

    /// <summary>Selects the active trigger's <c>Disabled</c>.</summary>
    public static readonly Func<IPopoverState, bool> Disabled = static state => state.Disabled;

    /// <summary>Selects the active trigger's <c>OpenOnHover</c>.</summary>
    public static readonly Func<IPopoverState, bool> OpenOnHover = static state => state.OpenOnHover;

    /// <summary>Selects the active trigger's close delay.</summary>
    public static readonly Func<IPopoverState, int> CloseDelay = static state => state.CloseDelay;

    /// <summary>Selects the payload's version.</summary>
    public static readonly Func<IPopoverState, int> PayloadVersion = static state => state.PayloadVersion;

    /// <summary>Selects whether a viewport is mounted.</summary>
    public static readonly Func<IPopoverState, bool> AdaptiveOrigin = static state => state.AdaptiveOrigin;

    /// <summary>Selects the relay of the last committed open change.</summary>
    public static readonly Func<IPopoverState, OpenChangeRelay?> OpenChange = static state => state.OpenChange;

    /// <summary>Selects the snapshot of the event that caused the last committed close.</summary>
    public static readonly Func<IPopoverState, EventSnapshot?> CloseEvent = static state => state.CloseEvent;

    // POP-STA-12.
    /// <summary>Selects whether the popover is open, owned by the trigger with the given id, and was opened by a press.</summary>
    public static readonly Func<IPopoverState, string?, bool> IsPressOpenedByTrigger = static (state, triggerId) =>
        state.OpenChangeReason == EventReason.TriggerPress && IsOpenedBy(state, triggerId);

    // POP-STA-12: a constant for a trigger without OpenOnHover, so it never re-renders on its account.
    /// <summary>Selects whether a hover-enabled trigger's popover was opened by a touch press.</summary>
    public static readonly Func<IPopoverState, bool, bool> IsTouchPressOpen = static (state, openOnHover) =>
        openOnHover && state.OpenMethod == InteractionType.Touch && state.OpenChangeReason == EventReason.TriggerPress;

    // POP-STA-12, POP-STR-14.
    /// <summary>Selects whether the trigger with the given id renders focus guards: it owns the open popover and focus is not trapped.</summary>
    public static readonly Func<IPopoverState, string?, bool> HasTriggerFocusGuards = static (state, triggerId) =>
        IsOpenedBy(state, triggerId) && !state.FocusManagerModal;

    private static bool IsOpenedBy(IPopoverState state, string? triggerId) =>
        triggerId is not null && state.Open && string.Equals(state.ActiveTriggerId, triggerId, StringComparison.Ordinal);
}

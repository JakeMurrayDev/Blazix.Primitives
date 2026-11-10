using Blazix.Primitives.Interop;
using Blazix.Primitives.Store;

namespace Blazix.Primitives;

// SEL §4 (select/store.ts:11-60); ADR 05 Amendments 2026-11-03, 2026-11-04 (the open lifecycle as one member, track 2) and
// 2026-10-27's payload rule: the value is not a member, it lives in SelectStore<TValue>'s non-reactive context with
// ValueVersion here, so a value-type TValue never boxes in the generated ShallowEquals and the record stays non-generic for
// the non-generic parts. Left out of the record: the active index (list navigation's mirror, ADR 04 Amendments
// 2026-11-03 (2)), the item, label and value registries (CMP; the store's context), the prop bags (no React props), the
// element handles (the parts' own), the typing flag and the press gates (the `select` and `composite` modules').
/// <summary>Represents the state of one select: its open lifecycle and the fields its parts observe.</summary>
/// <param name="Lifecycle">The open lifecycle.</param>
/// <param name="RootId">The root's effective id: its <c>Id</c>, else a generated one.</param>
/// <param name="TriggerId">The id the trigger renders, or <see langword="null"/> while no trigger is mounted.</param>
/// <param name="LabelId">The id of the mounted select label, or <see langword="null"/>.</param>
/// <param name="HasList">Whether a list part is mounted, which then carries the listbox role.</param>
/// <param name="PopupElementId">The id the consumer gave the popup, or <see langword="null"/>.</param>
/// <param name="ListElementId">The id the consumer gave the mounted list, or <see langword="null"/>.</param>
/// <param name="ForceMount">Whether the popup subtree renders while closed, set once and never cleared.</param>
/// <param name="SelectedIndex">The index of the selected item the popup opens on, or <see langword="null"/>.</param>
/// <param name="PopupSide">The positioner's resolved side, or <see langword="null"/> before it reports one.</param>
/// <param name="AlignedActive">Whether the popup is placed over the trigger, as the browser-side behavior last reported.</param>
/// <param name="ScrollUpArrowVisible">Whether the scroll-up arrow is visible.</param>
/// <param name="ScrollDownArrowVisible">Whether the scroll-down arrow is visible.</param>
/// <param name="ScrollArrowCount">The number of mounted scroll arrows.</param>
/// <param name="ValueVersion">A counter moved by every committed value, which the parts that read the value select.</param>
/// <param name="LabelsVersion">A counter moved when the items' label lookup or the label function changed.</param>
/// <param name="HasSelectedValue">Whether a value is selected.</param>
/// <param name="Disabled">Whether the root is disabled, its own or its field's.</param>
/// <param name="ReadOnly">Whether the value is locked.</param>
/// <param name="Required">Whether a value must be chosen.</param>
/// <param name="Modal">Whether the popup is modal.</param>
/// <param name="Multiple">Whether several values may be selected.</param>
/// <param name="HighlightItemOnHover">Whether pointer movement highlights items.</param>
/// <param name="AlignItemWithTrigger">The positioner's latched request to place the popup over the trigger.</param>
/// <param name="TextVersion">A counter moved when an item text that aligned placement measures changed.</param>
/// <param name="RenderedOpenMethod">The open interaction type, kept through the close: the current one, else the last one.</param>
/// <param name="CloseEvent">The snapshot of the event that caused the last committed close, or <see langword="null"/> before any close.</param>
/// <param name="CloseByItemPress">Whether the last committed close was an item press, whose close type the popup's return-focus resolver does not receive.</param>
[StoreState]
internal sealed partial record SelectStoreState(
    PopupLifecycleState Lifecycle,
    string? RootId,
    string? TriggerId,
    string? LabelId,
    bool HasList,
    string? PopupElementId,
    string? ListElementId,
    bool ForceMount,
    int? SelectedIndex,
    Side? PopupSide,
    bool AlignedActive,
    bool ScrollUpArrowVisible,
    bool ScrollDownArrowVisible,
    int ScrollArrowCount,
    int ValueVersion,
    int LabelsVersion,
    bool HasSelectedValue,
    bool Disabled,
    bool ReadOnly,
    bool Required,
    bool Modal,
    bool Multiple,
    bool HighlightItemOnHover,
    bool AlignItemWithTrigger,
    int TextVersion,
    InteractionType? RenderedOpenMethod,
    EventSnapshot? CloseEvent,
    bool CloseByItemPress)
    : IStoreState<SelectStoreState>, IPopupLifecycleState<SelectStoreState>
{
    /// <summary>Selects the effective open state.</summary>
    public static readonly Func<SelectStoreState, bool> Open = static state => state.Lifecycle.EffectiveOpen;

    /// <summary>Selects whether the popup is mounted.</summary>
    public static readonly Func<SelectStoreState, bool> Mounted = static state => state.Lifecycle.Mounted;

    /// <summary>Selects whether the portal renders: mounted or force-mounted.</summary>
    public static readonly Func<SelectStoreState, bool> Rendered = static state => state.Lifecycle.Mounted || state.ForceMount;

    /// <summary>Selects the derived transition status.</summary>
    public static readonly Func<SelectStoreState, TransitionStatus?> TransitionStatus = static state => state.Lifecycle.DerivedTransitionStatus;

    /// <summary>Selects the open interaction type.</summary>
    public static readonly Func<SelectStoreState, InteractionType?> OpenMethod = static state => state.Lifecycle.OpenMethod;

    /// <summary>Selects the relay of the last committed open change.</summary>
    public static readonly Func<SelectStoreState, OpenChangeRelay?> OpenChange = static state => state.Lifecycle.OpenChange;

    /// <summary>Selects the unmount counter.</summary>
    public static readonly Func<SelectStoreState, int> UnmountRequests = static state => state.Lifecycle.UnmountRequests;

    /// <summary>Selects whether the current close keeps the popup mounted.</summary>
    public static readonly Func<SelectStoreState, bool> PreventUnmountingOnClose = static state => state.Lifecycle.PreventUnmountingOnClose;

    /// <summary>Selects the floating id.</summary>
    public static readonly Func<SelectStoreState, string?> FloatingId = static state => state.Lifecycle.FloatingId;

    /// <summary>Selects the root's effective id.</summary>
    public static readonly Func<SelectStoreState, string?> RootIdSelector = static state => state.RootId;

    /// <summary>Selects the trigger's id.</summary>
    public static readonly Func<SelectStoreState, string?> TriggerIdSelector = static state => state.TriggerId;

    /// <summary>Selects the select label's id.</summary>
    public static readonly Func<SelectStoreState, string?> LabelIdSelector = static state => state.LabelId;

    // SEL-ARIA-04 (SelectTrigger.tsx:138-139): the listbox's rendered id is the list's while one is mounted, else the popup's.
    /// <summary>Selects the id the consumer gave the element that carries the listbox role, or <see langword="null"/> for the default.</summary>
    public static readonly Func<SelectStoreState, string?> ListboxConsumerIdSelector = static state => state.HasList ? state.ListElementId : state.PopupElementId;

    /// <summary>Selects whether a list part is mounted.</summary>
    public static readonly Func<SelectStoreState, bool> HasListSelector = static state => state.HasList;

    /// <summary>Selects the selected index.</summary>
    public static readonly Func<SelectStoreState, int?> SelectedIndexSelector = static state => state.SelectedIndex;

    /// <summary>Selects the trigger's popup side: the resolved side while mounted, else <see langword="null"/>.</summary>
    public static readonly Func<SelectStoreState, Side?> TriggerPopupSide = static state => state.Lifecycle.Mounted ? state.PopupSide : null;

    /// <summary>Selects whether aligned placement is active.</summary>
    public static readonly Func<SelectStoreState, bool> AlignedActiveSelector = static state => state.AlignedActive;

    /// <summary>Selects the scroll-up arrow's visibility.</summary>
    public static readonly Func<SelectStoreState, bool> ScrollUpVisible = static state => state.ScrollUpArrowVisible;

    /// <summary>Selects the scroll-down arrow's visibility.</summary>
    public static readonly Func<SelectStoreState, bool> ScrollDownVisible = static state => state.ScrollDownArrowVisible;

    /// <summary>Selects whether a scroll arrow is mounted.</summary>
    public static readonly Func<SelectStoreState, bool> HasScrollArrows = static state => state.ScrollArrowCount > 0;

    /// <summary>Selects the value counter.</summary>
    public static readonly Func<SelectStoreState, int> ValueVersionSelector = static state => state.ValueVersion;

    /// <summary>Selects the label counter.</summary>
    public static readonly Func<SelectStoreState, int> LabelsVersionSelector = static state => state.LabelsVersion;

    /// <summary>Selects whether a value is selected.</summary>
    public static readonly Func<SelectStoreState, bool> HasSelectedValueSelector = static state => state.HasSelectedValue;

    /// <summary>Selects whether the root is disabled.</summary>
    public static readonly Func<SelectStoreState, bool> DisabledSelector = static state => state.Disabled;

    /// <summary>Selects whether the value is locked.</summary>
    public static readonly Func<SelectStoreState, bool> ReadOnlySelector = static state => state.ReadOnly;

    /// <summary>Selects whether a value is required.</summary>
    public static readonly Func<SelectStoreState, bool> RequiredSelector = static state => state.Required;

    /// <summary>Selects whether the popup is modal.</summary>
    public static readonly Func<SelectStoreState, bool> ModalSelector = static state => state.Modal;

    /// <summary>Selects whether several values may be selected.</summary>
    public static readonly Func<SelectStoreState, bool> MultipleSelector = static state => state.Multiple;

    /// <summary>Selects whether pointer movement highlights items.</summary>
    public static readonly Func<SelectStoreState, bool> HighlightItemOnHoverSelector = static state => state.HighlightItemOnHover;

    /// <summary>Selects the rendered open interaction type.</summary>
    public static readonly Func<SelectStoreState, InteractionType?> RenderedOpenMethodSelector = static state => state.RenderedOpenMethod;

    // SEL-STA-08.
    /// <summary>Selects whether the popup was opened by touch, kept through the close.</summary>
    public static readonly Func<SelectStoreState, bool> TouchOpened = static state => state.RenderedOpenMethod == InteractionType.Touch;

    /// <summary>Selects the snapshot of the event of the last committed close.</summary>
    public static readonly Func<SelectStoreState, EventSnapshot?> CloseEventSelector = static state => state.CloseEvent;

    /// <summary>Selects the text counter.</summary>
    public static readonly Func<SelectStoreState, int> TextVersionSelector = static state => state.TextVersion;

    /// <inheritdoc />
    public PopupLifecycleState Lifecycle { get; init; } = Lifecycle;

    /// <summary>Creates the initial record of a select.</summary>
    /// <param name="floatingId">The generated id of the popup.</param>
    /// <returns>A closed, unmounted record with nothing selected.</returns>
    public static SelectStoreState Initial(string? floatingId) => new(
        PopupLifecycleState.Initial(floatingId),
        RootId: null,
        TriggerId: null,
        LabelId: null,
        HasList: false,
        PopupElementId: null,
        ListElementId: null,
        ForceMount: false,
        SelectedIndex: null,
        PopupSide: null,
        AlignedActive: false,
        ScrollUpArrowVisible: false,
        ScrollDownArrowVisible: false,
        ScrollArrowCount: 0,
        ValueVersion: 0,
        LabelsVersion: 0,
        HasSelectedValue: false,
        Disabled: false,
        ReadOnly: false,
        Required: false,
        Modal: true,
        Multiple: false,
        HighlightItemOnHover: true,
        AlignItemWithTrigger: true,
        TextVersion: 0,
        RenderedOpenMethod: null,
        CloseEvent: null,
        CloseByItemPress: false);

    /// <inheritdoc />
    public static SelectStoreState WithLifecycle(SelectStoreState state, in PopupLifecycleState lifecycle) => state with { Lifecycle = lifecycle };
}

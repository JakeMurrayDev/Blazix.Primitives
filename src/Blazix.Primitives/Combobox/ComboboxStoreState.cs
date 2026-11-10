using Blazix.Primitives.Store;

namespace Blazix.Primitives;

// CMB §4.1 (combobox/store.ts:10-71): the engine's state that is not a value, so every non-generic part reads it (ADR 05
// Amendments 2026-11-03: the open lifecycle as one positional member, the record composing it). The selected value or
// values, the items and the filtered view stay on ComboboxStore<TValue>; the revisions here tell a selector over this record
// that they changed (SelectionRevision, MembershipRevision). Left out: the five prop bags (the modules' options replace
// them), the element refs (the parts' rendered ids replace them, the composite precedent: values never cross), openMethod
// (Lifecycle.OpenMethod), and the item callbacks (on the store).
/// <summary>Represents the state of a combobox-family store that does not depend on the type of its values.</summary>
/// <param name="Lifecycle">The open lifecycle.</param>
/// <param name="Id">The root's id: the given one, else generated.</param>
/// <param name="SelectionMode">What the engine remembers of an item press.</param>
/// <param name="Inline">Whether the list renders without a popup.</param>
/// <param name="InputInsidePopup">Whether the input is inside the popup, or no input has registered yet.</param>
/// <param name="Disabled">Whether the combobox is disabled, by the root or its field.</param>
/// <param name="ReadOnly">Whether the value cannot be changed.</param>
/// <param name="Required">Whether a value is required.</param>
/// <param name="Grid">Whether the list is a grid.</param>
/// <param name="Virtualized">Whether the list renders a window of its items.</param>
/// <param name="Modal">Whether the popup is modal.</param>
/// <param name="ActiveIndex">The highlighted item's index, or <see langword="null"/> for none.</param>
/// <param name="SelectedIndex">The index of the (first) selected item in the rendered list, or <see langword="null"/>.</param>
/// <param name="PopupSide">The side the positioner settled on, or <see langword="null"/> while no positioner reported.</param>
/// <param name="ForceMounted">Whether the list is mounted while closed for typeahead or autofill.</param>
/// <param name="InputValue">The committed input text.</param>
/// <param name="InputRevision">A counter raised by every committed input write, the input <c>value</c> writer's revision.</param>
/// <param name="HasSelectedValue">Whether a value is selected (single) or at least one is (multiple).</param>
/// <param name="HasItems">Whether the root was given items.</param>
/// <param name="FilteredCount">The number of filtered items when the root was given items.</param>
/// <param name="MembershipRevision">A counter raised when the filtered items' membership changes.</param>
/// <param name="SelectionRevision">A counter raised when the selected value or values change.</param>
/// <param name="SubmitRevision">A counter raised when an item press asks for the form's submission.</param>
/// <param name="EmptyMounted">Whether an empty-state part is mounted.</param>
/// <param name="ElementIds">The rendered ids of the parts the modules and the dismissal read.</param>
[StoreState]
internal sealed partial record ComboboxStoreState(
    PopupLifecycleState Lifecycle,
    string Id,
    ComboboxSelectionMode SelectionMode,
    bool Inline,
    bool InputInsidePopup,
    bool Disabled,
    bool ReadOnly,
    bool Required,
    bool Grid,
    bool Virtualized,
    bool Modal,
    int? ActiveIndex,
    int? SelectedIndex,
    Side? PopupSide,
    bool ForceMounted,
    string InputValue,
    int InputRevision,
    bool HasSelectedValue,
    bool HasItems,
    int FilteredCount,
    int MembershipRevision,
    int SelectionRevision,
    int SubmitRevision,
    bool EmptyMounted,
    ComboboxElementIds ElementIds) : IStoreState<ComboboxStoreState>, IPopupLifecycleState<ComboboxStoreState>
{
    /// <summary>Selects the effective open state.</summary>
    public static readonly Func<ComboboxStoreState, bool> Open = static state => state.Lifecycle.EffectiveOpen;

    /// <summary>Selects whether the popup is mounted.</summary>
    public static readonly Func<ComboboxStoreState, bool> Mounted = static state => state.Lifecycle.Mounted;

    /// <summary>Selects the derived transition status.</summary>
    public static readonly Func<ComboboxStoreState, TransitionStatus?> Status = static state => state.Lifecycle.DerivedTransitionStatus;

    /// <summary>Selects the side the positioner settled on.</summary>
    public static readonly Func<ComboboxStoreState, Side?> PopupSideSelector = static state => state.PopupSide;

    /// <summary>Selects whether the filtered list of a root with items is empty.</summary>
    public static readonly Func<ComboboxStoreState, bool> ListEmpty = static state => state.FilteredCount == 0;

    /// <summary>Selects the committed input text's revision.</summary>
    public static readonly Func<ComboboxStoreState, int> InputRevisionSelector = static state => state.InputRevision;

    /// <summary>Selects the membership revision.</summary>
    public static readonly Func<ComboboxStoreState, int> Membership = static state => state.MembershipRevision;

    /// <summary>Selects the selection revision.</summary>
    public static readonly Func<ComboboxStoreState, int> Selection = static state => state.SelectionRevision;

    /// <summary>Selects whether the input is inside the popup.</summary>
    public static readonly Func<ComboboxStoreState, bool> InsidePopup = static state => state.InputInsidePopup;

    /// <inheritdoc />
    public PopupLifecycleState Lifecycle { get; init; } = Lifecycle;

    /// <summary>Gets a value indicating whether the list is exposed: open, or inline.</summary>
    public bool Expanded => Lifecycle.EffectiveOpen || Inline;

    /// <inheritdoc />
    public static ComboboxStoreState WithLifecycle(ComboboxStoreState state, in PopupLifecycleState lifecycle) => state with { Lifecycle = lifecycle };

    /// <summary>Creates the initial state of a root.</summary>
    /// <param name="floatingId">The popup's floating id, the list's id.</param>
    /// <param name="id">The root's id.</param>
    /// <param name="mode">The selection mode.</param>
    /// <returns>The state before the root's first parameter set is applied.</returns>
    public static ComboboxStoreState Initial(string floatingId, string id, ComboboxSelectionMode mode) => new(
        PopupLifecycleState.Initial(floatingId),
        id,
        mode,
        Inline: false,
        InputInsidePopup: true,
        Disabled: false,
        ReadOnly: false,
        Required: false,
        Grid: false,
        Virtualized: false,
        Modal: false,
        ActiveIndex: null,
        SelectedIndex: null,
        PopupSide: null,
        ForceMounted: false,
        InputValue: string.Empty,
        InputRevision: 0,
        HasSelectedValue: false,
        HasItems: false,
        FilteredCount: 0,
        MembershipRevision: 0,
        SelectionRevision: 0,
        SubmitRevision: 0,
        EmptyMounted: false,
        ElementIds: default);
}

/// <summary>The rendered ids of the parts the modules, the dismissal and the ARIA read.</summary>
/// <param name="Input">The input's id, or <see langword="null"/> while none renders.</param>
/// <param name="Trigger">The trigger's id, or <see langword="null"/>.</param>
/// <param name="Clear">The clear button's id, or <see langword="null"/>.</param>
/// <param name="Chips">The chips container's id, or <see langword="null"/>.</param>
/// <param name="InputGroup">The input group's id, or <see langword="null"/>.</param>
/// <param name="Label">The label's id, or <see langword="null"/>.</param>
/// <param name="Popup">The popup's id, or <see langword="null"/>.</param>
/// <param name="List">The list's id, or <see langword="null"/> while no list renders.</param>
/// <param name="StartDismiss">The input's dismiss button's id, or <see langword="null"/>.</param>
/// <param name="EndDismiss">The popup's dismiss button's id, or <see langword="null"/>.</param>
internal readonly record struct ComboboxElementIds(
    string? Input,
    string? Trigger,
    string? Clear,
    string? Chips,
    string? InputGroup,
    string? Label,
    string? Popup,
    string? List,
    string? StartDismiss,
    string? EndDismiss);

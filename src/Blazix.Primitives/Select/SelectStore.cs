using Blazix.Primitives.Interop;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace Blazix.Primitives;

// SEL §4 (select/store.ts, SelectRoot.tsx:136-195); ADR 05 Amendments 2026-11-03 and 2026-11-04 (the open lifecycle,
// track 2: PopupLifecycleStore over a record holding PopupLifecycleState). The non-generic base is what the non-generic
// parts read (the Tabs and Popover precedent, ADR 08 Amendments 2026-10-26, 2026-10-27): the record is non-generic (the
// value lives in SelectStore<TValue>, ADR 05 Amendments 2026-10-27's payload rule), so a part subscribes to it directly.
// The store is also the cascade's floating node (IFloatingNodeContext, track 2's design point 5): a popup nested inside
// the select's popup joins its tree.
// - The record's `Disabled` is the root's disabled flag for its parts; the lifecycle's Disabled stays false (SEL-API-13,
//   SEL-EDGE-06: a disabled select still opens when forced and is dismissible).
/// <summary>Holds the state of one select that its non-generic parts read and write.</summary>
internal abstract class SelectStore : PopupLifecycleStore<SelectStoreState>, IFloatingNodeContext
{
    private bool alignItemWithTriggerProp = true;

    /// <summary>Occurs when a close by a focus-out or an outside press is committed, which marks the field touched.</summary>
    public event Action? Dismissed;

    /// <summary>Occurs when the field's state that the trigger, the label and the hidden input render changed.</summary>
    public event Action? FieldStateChanged;

    /// <summary>Initializes a new instance of the <see cref="SelectStore"/> class.</summary>
    /// <param name="floatingId">The generated id of the popup.</param>
    /// <param name="sessionToken">The session token the root drew.</param>
    /// <param name="logger">The root's logger, or <see langword="null"/>.</param>
    protected SelectStore(string floatingId, string sessionToken, ILogger? logger)
        : base(SelectStoreState.Initial(floatingId), "Select", sessionToken, inert: false, logger)
    {
    }

    /// <summary>Gets or sets the root's list navigation, which the list's handles join.</summary>
    public ListNavigationBehavior? List { get; set; }

    /// <summary>Gets or sets the root's field half.</summary>
    public SelectField? Field { get; set; }

    /// <summary>Gets or sets the function that returns the raw value an onBlur commit validates, boxed once per value.</summary>
    public Func<object?>? BoxedValue { get; set; }

    /// <summary>Gets or sets the positioner's element, which the trigger's release check reads.</summary>
    public ElementReference? PositionerElement { get; set; }

    /// <summary>Gets or sets the trigger's element, which aligned placement measures.</summary>
    public ElementReference? TriggerElement { get; set; }

    /// <summary>Gets or sets the action the positioner runs after each render, once its element shows the open state.</summary>
    /// <value>The root's deferred session update; <see langword="null"/> before the root set it.</value>
    public Action? PositionerRendered { get; set; }

    /// <summary>Gets the counter of changed item orders, which tells aligned placement to place again from cleared heights.</summary>
    public int OrderRevision { get; protected set; }

    /// <summary>Gets or sets the element of the value part that aligned placement aligns the selected item's text with.</summary>
    /// <value>The last value part to render; <see langword="null"/> while none is mounted.</value>
    public ElementReference? ValueElement { get; set; }

    /// <summary>Gets the id of the text element of the item that holds the selected index, or <see langword="null"/>.</summary>
    public abstract string? SelectedTextId { get; }

    /// <summary>Gets the id of the text element of the first item, or <see langword="null"/>.</summary>
    public abstract string? FirstTextId { get; }

    /// <summary>Gets the number of items in rendered order, as the list last reported them.</summary>
    public abstract int ItemCount { get; }

    /// <summary>Raises <see cref="FieldStateChanged"/>; the field half calls it.</summary>
    public void FieldChanged() => FieldStateChanged?.Invoke();

    /// <summary>Throws the error of a select part rendered outside a select root.</summary>
    /// <param name="store">The cascaded store, or <see langword="null"/>.</param>
    /// <returns>The store.</returns>
    /// <exception cref="InvalidOperationException"><paramref name="store"/> is <see langword="null"/>.</exception>
    public static SelectStore Require(SelectStore? store)
    {
        // SEL-EDGE-01, SEL-STR-02: thrown in every build (ADR 02 Amendments 2026-10-20), Base UI's wording with the
        // `Blazix: ` prefix and backticks (the M5 plan §Settled).
        return store ?? throw new InvalidOperationException(
            "Blazix: `SelectRootContext` is missing. Select parts must be placed within `<SelectRoot>`.");
    }

    // SEL-STA-09, SEL-FOC-11, SEL-FRM-18: set once, never cleared.
    /// <summary>Renders the popup subtree while closed from now on, so the items exist for typeahead and autofill.</summary>
    public void ForceMount()
    {
        if (!State.ForceMount)
        {
            Write(State with { ForceMount = true });
        }
    }

    /// <summary>Records the id the trigger renders.</summary>
    /// <param name="id">The id, or <see langword="null"/> when the trigger goes away.</param>
    public void SetTriggerId(string? id)
    {
        if (!string.Equals(State.TriggerId, id, StringComparison.Ordinal))
        {
            Write(State with { TriggerId = id });
        }
    }

    /// <summary>Records the id of the mounted select label.</summary>
    /// <param name="id">The id, or <see langword="null"/> when no label is mounted.</param>
    public void SetLabelId(string? id)
    {
        if (!string.Equals(State.LabelId, id, StringComparison.Ordinal))
        {
            Write(State with { LabelId = id });
        }
    }

    /// <summary>Records the id the consumer gave the popup or the list.</summary>
    /// <param name="list"><see langword="true"/> for the list's id, <see langword="false"/> for the popup's.</param>
    /// <param name="id">The id, or <see langword="null"/> when none was given or the part left.</param>
    public void SetListboxConsumerId(bool list, string? id)
    {
        SelectStoreState state = State;
        if (!string.Equals(list ? state.ListElementId : state.PopupElementId, id, StringComparison.Ordinal))
        {
            Write(list ? state with { ListElementId = id } : state with { PopupElementId = id });
        }
    }

    // SEL-DOM-23/24, SEL-ARIA-04: the listbox role moves to the list part while one is mounted.
    /// <summary>Records whether a list part is mounted.</summary>
    /// <param name="hasList">Whether a list part is mounted.</param>
    public void SetHasList(bool hasList)
    {
        if (State.HasList != hasList)
        {
            Write(State with { HasList = hasList });
        }
    }

    // SEL-STA-28: the resolved side, never `none`.
    /// <summary>Records the positioner's resolved side.</summary>
    /// <param name="side">The side the positioning behavior reported.</param>
    public void SetPopupSide(Side? side)
    {
        if (State.PopupSide != side)
        {
            Write(State with { PopupSide = side });
        }
    }

    // SEL-POS-04/05 (SelectPositioner.tsx:75-82): the placement's fallback latches the request off while mounted; the
    // `select` module reports it, once per change (ADR 01's RND-STA-01 row).
    /// <summary>Latches aligned placement off until the popup unmounts, as the browser-side behavior's fallback asks.</summary>
    public void LatchFallback()
    {
        if (State.AlignItemWithTrigger && State.Lifecycle.Mounted)
        {
            Write(State with { AlignItemWithTrigger = false });
        }
    }

    // SEL-POS-05: the positioner's request, re-synchronized only while unmounted. Base UI keeps the latch in the
    // positioner's own state, initialized from its prop (SelectPositioner.tsx:70-71), so a positioner's first request is
    // taken even when it first renders with the popup already mounted (a default or controlled open).
    /// <summary>Records the positioner's request to place the popup over the trigger; after the first, it takes effect while unmounted.</summary>
    /// <param name="align">The positioner's <c>AlignItemWithTrigger</c>.</param>
    /// <param name="first"><see langword="true"/> for the first request of a positioner.</param>
    public void SetAlignItemWithTrigger(bool align, bool first)
    {
        alignItemWithTriggerProp = align;
        if (State.AlignItemWithTrigger != align && (first || !State.Lifecycle.Mounted))
        {
            Write(State with { AlignItemWithTrigger = align });
        }
    }

    // SEL-STA-29 (handleScrollArrowVisibility): the `select` module's report for each arrow.
    /// <summary>Records the visibility of a scroll arrow.</summary>
    /// <param name="up">Whether the arrow is the scroll-up arrow.</param>
    /// <param name="visible">Whether it is visible.</param>
    public void SetScrollArrowVisible(bool up, bool visible)
    {
        SelectStoreState state = State;
        if (up ? state.ScrollUpArrowVisible != visible : state.ScrollDownArrowVisible != visible)
        {
            Write(up ? state with { ScrollUpArrowVisible = visible } : state with { ScrollDownArrowVisible = visible });
        }
    }

    // SEL-STA-30: a mount counter, never below zero.
    /// <summary>Counts a scroll arrow mounting or unmounting.</summary>
    /// <param name="mounted">Whether the arrow mounted.</param>
    public void CountScrollArrow(bool mounted)
    {
        int count = Math.Max(0, State.ScrollArrowCount + (mounted ? 1 : -1));
        if (count != State.ScrollArrowCount)
        {
            Write(State with { ScrollArrowCount = count });
        }
    }

    // SEL-POS-10 (select/popup/SelectPopup.tsx:396-403): after an aligned placement, the first item when nothing is
    // selected or highlighted and hover highlighting is on; asked of list navigation (ADR 04 Amendments 2026-11-03 (2)).
    /// <summary>Highlights the first item after an aligned placement, when nothing is selected or highlighted.</summary>
    /// <returns>A task that completes when list navigation has the request.</returns>
    public Task HighlightFirstItemAfterPlacementAsync()
    {
        SelectStoreState state = State;
        if (List is { } list && state.HighlightItemOnHover && state.SelectedIndex is null && list.HighlightedIndex is null && ItemCount > 0)
        {
            return list.RequestHighlightAsync(0);
        }

        return Task.CompletedTask;
    }

    // SEL-PTR-15/16: a pointer movement over a scroll arrow clears the highlight.
    /// <summary>Clears the highlight, as a pointer movement over a scroll arrow does.</summary>
    /// <returns>A task that completes when list navigation has the request.</returns>
    public Task ClearHighlightAsync() => List?.RequestHighlightAsync(null) ?? Task.CompletedTask;

    /// <summary>Takes the items' order the list's floating handle reported.</summary>
    /// <param name="ids">The items' ids, in rendered order.</param>
    /// <returns>A task that completes when a reconciliation request it started has run.</returns>
    public abstract Task PublishOrderAsync(IReadOnlyList<string> ids);

    /// <summary>Activates the item with an id, as a click or a key on it does: its consumer <c>onclick</c>, then the commit.</summary>
    /// <param name="itemId">The item's id.</param>
    /// <param name="snapshot">The activation's event.</param>
    /// <returns>A task whose result is whether the consumer vetoed the activation.</returns>
    public abstract Task<bool> ActivateItemAsync(string itemId, EventSnapshot snapshot);

    /// <summary>Selects the item a typeahead on the closed trigger matched.</summary>
    /// <param name="index">The matched item's index.</param>
    /// <param name="snapshot">The key's event.</param>
    /// <returns>A task that completes when the value request has run.</returns>
    public abstract Task MatchItemAsync(int index, EventSnapshot snapshot);

    /// <summary>Gets a value indicating whether the item with an id is disabled.</summary>
    /// <param name="itemId">The item's id.</param>
    /// <returns><see langword="true"/> when the item or the root is disabled; <see langword="false"/> for an unknown id.</returns>
    public abstract bool IsItemDisabled(string itemId);

    /// <summary>Records an item text's id for aligned placement.</summary>
    /// <param name="itemId">The item's id.</param>
    /// <param name="textId">The text element's id, or <see langword="null"/> when the text goes away.</param>
    public abstract void SetItemText(string itemId, string? textId);

    // SEL-API-39, SEL-EVT-17: the close's event, which the popup's return-focus resolver classifies (the Popover precedent).
    /// <inheritdoc />
    protected override SelectStoreState ApplyOpenChange(SelectStoreState state, bool open, OpenChangeEventDetails details) =>
        open ? state : state with { CloseEvent = details.Event };

    // SEL-FOC-10, SEL-FRM-03 (SelectRoot.tsx:303-313): a committed focus-out or outside-press close marks the field.
    /// <inheritdoc />
    protected override void BeforeDispatch(bool open, OpenChangeEventDetails details)
    {
        if (!open && details.Reason is EventReason.FocusOut or EventReason.OutsidePress)
        {
            Dismissed?.Invoke();
        }
    }

    /// <inheritdoc />
    protected override void OnUnmount()
    {
        if (List is { } list)
        {
            _ = list.RequestHighlightAsync(null);
        }
    }

    /// <summary>Moves the text counter, after a text element that aligned placement measures changed.</summary>
    protected void TouchText() => Write(State with { TextVersion = State.TextVersion + 1 });

    // SEL-STA-08 (SelectRoot.tsx:222-223, usePreviousValue): the open method a closing transition and a quick reopen
    // keep. SEL-POS-03/05 (SelectPositioner.tsx:75-82): aligned placement is active while mounted, requested (the latch,
    // re-synchronized to the positioner's request while unmounted) and not opened by touch.
    /// <inheritdoc />
    private protected override void PublishWrite(SelectStoreState current, SelectStoreState next)
    {
        if (next.Lifecycle.OpenMethod is { } method && next.RenderedOpenMethod != method)
        {
            next = next with { RenderedOpenMethod = method };
        }

        if (!next.Lifecycle.Mounted && next.AlignItemWithTrigger != alignItemWithTriggerProp)
        {
            next = next with { AlignItemWithTrigger = alignItemWithTriggerProp };
        }

        bool aligned = next.Lifecycle.Mounted && next.AlignItemWithTrigger && next.RenderedOpenMethod != InteractionType.Touch;
        if (next.AlignedActive != aligned)
        {
            next = next with { AlignedActive = aligned };
        }

        base.PublishWrite(current, next);
    }

    // SEL-ANI-07 (SelectRoot.tsx:206-214): the unmount cleanup's highlight reset, open method and arrows in one write.
    /// <inheritdoc />
    private protected override SelectStoreState WithUnmounted(SelectStoreState state, in PopupLifecycleState lifecycle) =>
        state with
        {
            Lifecycle = lifecycle with { OpenMethod = null },
            ScrollUpArrowVisible = false,
            ScrollDownArrowVisible = false,
        };
}

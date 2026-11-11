using Blazix.Primitives.Interop;
using Blazix.Primitives.Store;
using Blazix.Primitives.Utilities;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace Blazix.Primitives;

// SEL §4, §7 for a select whose values are TValue. The value is the store's non-reactive context (ADR 05 Amendments
// 2026-10-27's payload rule): two Controlled<T> (the single triple and the multiple triple, ADR 08 Amendments 2026-11-03 (1)),
// with ValueVersion in the record so a part that reads the value selects the counter, never the value.
// - Equality (ADR 08 Amendments 2026-11-03 (2); track 4's ItemEquality): Equality.Is by default, IsItemEqualToValue for two
//   non-null operands, item first; the item values in rendered order as ItemSlot<TValue>, joined by id to the floating
//   handle's order report (ADR 04 Amendments 2026-11-03 (1)); no HashSet<TValue>, no LINQ.
// - "No value" (Base UI's `null` default, SEL-API-02) for a value type: an uncontrolled root without DefaultValue holds
//   none until its first commit (the RadioGroup R1 precedent); a nullable TValue holds null.
// - The selected index (SEL-STA-04..06): recomputed from the value while closed (syncSelectedIndex, SelectRoot.tsx:264-279,
//   which re-runs when the popup closes); while open only the items' claims move it, which the port runs when an item's
//   index or value changed (an order report or a registration), in rendered order (SelectItem.tsx:80-115).
// - Reconciliation (SEL-STA-12..14; SelectPositioner.tsx:141-207) on an order report whose item set differs from the last,
//   never on the first one nor while no item has a value.
/// <summary>Holds the state of one select whose values are <typeparamref name="TValue"/>.</summary>
/// <typeparam name="TValue">The type of the select's values.</typeparam>
internal sealed class SelectStore<TValue> : SelectStore
{
    private static readonly IReadOnlyList<TValue> s_empty = [];
    private static readonly bool s_nullableValue = ValueNull.IsNull(default(TValue));

    private readonly Dictionary<string, SelectItemRegistration<TValue>> items = new(StringComparer.Ordinal);
    private readonly List<SelectItemRegistration<TValue>> registrationOrder = [];
    private string[] orderedIds = [];
    private ItemSlot<TValue>[] slots = [];
    private int publishedCount;
    private bool defaultSupplied;
    private bool committedOnce;
    private bool initialCaptured;
    private TValue initialValue = default!;
    private bool initialPresent;
    private IReadOnlyList<TValue> initialValues = s_empty;
    private string? selectedTextId;
    private string? firstTextId;

    /// <summary>Initializes a new instance of the <see cref="SelectStore{TValue}"/> class.</summary>
    /// <param name="floatingId">The generated id of the popup.</param>
    /// <param name="sessionToken">The session token the root drew.</param>
    /// <param name="logger">The root's logger, or <see langword="null"/>.</param>
    public SelectStore(string floatingId, string sessionToken, ILogger? logger)
        : base(floatingId, sessionToken, logger)
    {
        ValueState = new Controlled<TValue>("Select", "value", logger);
        ValuesState = new Controlled<IReadOnlyList<TValue>>("Select", "value", logger);
    }

    /// <summary>Gets the single-mode value's controlled-or-uncontrolled state.</summary>
    public Controlled<TValue> ValueState { get; }

    /// <summary>Gets the multiple-mode value's controlled-or-uncontrolled state.</summary>
    public Controlled<IReadOnlyList<TValue>> ValuesState { get; }

    /// <summary>Gets or sets the consumer's single-mode change callback.</summary>
    public EventCallback<ValueChangeEventArgs<TValue>> OnValueChange { get; set; }

    /// <summary>Gets or sets the <c>@bind-Value</c> callback.</summary>
    public EventCallback<TValue> ValueChanged { get; set; }

    /// <summary>Gets or sets the consumer's multiple-mode change callback.</summary>
    public EventCallback<ValueChangeEventArgs<IReadOnlyList<TValue>>> OnValuesChange { get; set; }

    /// <summary>Gets or sets the <c>@bind-Values</c> callback.</summary>
    public EventCallback<IReadOnlyList<TValue>> ValuesChanged { get; set; }

    /// <summary>Gets or sets the item comparer, or <see langword="null"/> for the default.</summary>
    public Func<TValue, TValue, bool>? Comparer { get; set; }

    /// <summary>Gets or sets the function that converts a value to its label.</summary>
    public Func<TValue, string>? ItemToStringLabel { get; set; }

    /// <summary>Gets or sets the function that converts a value to its form string.</summary>
    public Func<TValue, string>? ItemToStringValue { get; set; }

    /// <summary>Gets or sets the label lookup of the root's items.</summary>
    public LabeledItems<TValue> Labels { get; set; }

    /// <summary>Gets the single-mode value.</summary>
    public TValue Value => ValueState.Value;

    /// <summary>Gets the multiple-mode values; the shared empty list when nothing is selected.</summary>
    public IReadOnlyList<TValue> Values => ValuesState.Value ?? s_empty;

    /// <summary>Gets a value indicating whether the single-mode value is a value, rather than Base UI's <c>null</c>.</summary>
    public bool HasValue => ValueState.IsControlled || defaultSupplied || committedOnce;

    /// <summary>Gets the value present on the root's first render.</summary>
    public TValue InitialValue => initialValue;

    /// <summary>Gets the values present on the root's first render.</summary>
    public IReadOnlyList<TValue> InitialValues => initialValues;

    /// <inheritdoc />
    public override string? SelectedTextId => selectedTextId;

    /// <inheritdoc />
    public override string? FirstTextId => firstTextId;

    /// <inheritdoc />
    public override int ItemCount => orderedIds.Length;

    /// <summary>Gets the item values in rendered order.</summary>
    public ReadOnlySpan<ItemSlot<TValue>> Slots => slots;

    /// <summary>Records whether the root's default value was supplied at its first parameter set.</summary>
    /// <param name="supplied">Whether <c>DefaultValue</c> was supplied.</param>
    public void SetDefaultSupplied(bool supplied)
    {
        if (!initialCaptured)
        {
            defaultSupplied = supplied;
        }
    }

    // SEL-STA-31: the value of the first render, never changed afterwards.
    /// <summary>Captures the initial value once, at the root's first parameter set.</summary>
    public void CaptureInitial()
    {
        if (initialCaptured)
        {
            return;
        }

        initialCaptured = true;
        initialValue = Value;
        initialPresent = HasValue && !ValueNull.IsNull(initialValue);
        initialValues = Values;
    }

    // SEL-STA-02 (store.ts:109-119, SelectRoot.tsx:256-258).
    /// <summary>Gets a value indicating whether a value is selected.</summary>
    /// <returns><see langword="false"/> for no value, an empty list in multiple mode, or a value whose form string is empty.</returns>
    public bool ComputeHasSelectedValue()
    {
        if (State.Multiple)
        {
            return Values.Count > 0;
        }

        if (!HasValue || ValueNull.IsNull(Value))
        {
            return false;
        }

        return ValueLabels.StringifyAsValue(Value, ItemToStringValue).Length > 0;
    }

    // SEL-STA-03 (store.ts:135-150).
    /// <summary>Gets a value indicating whether an item's value is the selected value, or one of them.</summary>
    /// <param name="item">The item's value slot.</param>
    /// <returns><see langword="true"/> if the item is selected.</returns>
    public bool IsSelected(in ItemSlot<TValue> item)
    {
        if (!item.Present)
        {
            return false;
        }

        if (State.Multiple)
        {
            return ItemEquality.Includes(Values, item.Value, Comparer);
        }

        if (!HasValue && !s_nullableValue)
        {
            return false;
        }

        return ItemEquality.Compare(item.Value, Value, Comparer);
    }

    /// <summary>Registers an item.</summary>
    /// <param name="registration">The item's registration.</param>
    public void Register(SelectItemRegistration<TValue> registration)
    {
        items[registration.Id] = registration;
        if (!registrationOrder.Contains(registration))
        {
            registrationOrder.Add(registration);
        }
    }

    /// <summary>Unregisters an item, unless another registration has since taken its id.</summary>
    /// <param name="registration">The item's registration.</param>
    public void Unregister(SelectItemRegistration<TValue> registration)
    {
        if (items.TryGetValue(registration.Id, out SelectItemRegistration<TValue>? current) && ReferenceEquals(current, registration))
        {
            items.Remove(registration.Id);
        }

        registrationOrder.Remove(registration);
    }

    /// <summary>Gets an item's registration by id.</summary>
    /// <param name="itemId">The item's id.</param>
    /// <returns>The registration, or <see langword="null"/>.</returns>
    public SelectItemRegistration<TValue>? Find(string? itemId) =>
        itemId is not null && items.TryGetValue(itemId, out SelectItemRegistration<TValue>? registration) ? registration : null;

    /// <inheritdoc />
    public override bool IsItemDisabled(string itemId) => State.Disabled || Find(itemId) is { Disabled: true };

    /// <inheritdoc />
    public override void SetItemText(string itemId, string? textId)
    {
        if (Find(itemId) is { } registration && !string.Equals(registration.TextId, textId, StringComparison.Ordinal))
        {
            registration.TextId = textId;
            RefreshTexts();
        }
    }

    // An item's value changed in place (SelectItem.tsx:80-115's claim on a value change): its slot, then the claim.
    /// <summary>Applies an item's changed value to the rendered-order values and runs its claim.</summary>
    /// <param name="registration">The item's registration.</param>
    public void ItemValueChanged(SelectItemRegistration<TValue> registration)
    {
        int index = IndexOf(registration.Id);
        if (index < 0)
        {
            return;
        }

        slots[index] = registration.Slot;
        Claim(index, registration.Slot);
    }

    /// <inheritdoc />
    public override Task PublishOrderAsync(IReadOnlyList<string> ids)
    {
        string[] previousIds = orderedIds;
        bool changed = !SameIds(previousIds, ids);
        string[] nextIds = new string[ids.Count];
        ItemSlot<TValue>[] nextSlots = new ItemSlot<TValue>[ids.Count];
        for (int i = 0; i < ids.Count; i++)
        {
            nextIds[i] = ids[i];
            nextSlots[i] = Find(ids[i]) is { } registration ? registration.Slot : default;
            if (Find(ids[i]) is { } indexed)
            {
                indexed.Index = i;
            }
        }

        ItemSlot<TValue>[] previousSlots = slots;
        orderedIds = nextIds;
        slots = nextSlots;
        int previousCount = publishedCount;
        publishedCount = nextIds.Length;

        // The items whose index or value changed claim the selected index, in rendered order.
        for (int i = 0; i < nextSlots.Length; i++)
        {
            bool moved = i >= previousIds.Length || !string.Equals(previousIds[i], nextIds[i], StringComparison.Ordinal);
            if (moved || i >= previousSlots.Length || !Equality.Is(previousSlots[i].Value, nextSlots[i].Value))
            {
                Claim(i, nextSlots[i]);
            }
        }

        SyncSelectedIndex();
        RefreshTexts();
        if (changed)
        {
            OrderRevision++;
            TouchText();
        }

        if (!changed || previousCount == 0 || nextSlots.Length == 0)
        {
            return Task.CompletedTask;
        }

        return ReconcileAsync();
    }

    /// <inheritdoc />
    public override Task<bool> ActivateItemAsync(string itemId, EventSnapshot snapshot)
    {
        if (Find(itemId) is not { } registration)
        {
            return Task.FromResult(false);
        }

        return registration.Activate is { } activate ? activate(snapshot) : CommitAndRunAsync(registration, snapshot);
    }

    // SEL-FRM-18 (SelectRoot.tsx:536-563): the first item, in rendered order (registration order before the list's first
    // report), whose value text or label text equals the filled text case-insensitively, else whose explicit label does;
    // a null value never matches.
    /// <summary>Finds the item value a browser autofill of the hidden input names.</summary>
    /// <param name="text">The text the browser filled in.</param>
    /// <returns>The matching value, or <see langword="null"/> when no item matches.</returns>
    public ItemSlot<TValue>? MatchAutofill(string text)
    {
        ItemSlot<TValue>? byText = null;
        ItemSlot<TValue>? byLabel = null;
        foreach (SelectItemRegistration<TValue> registration in RenderedOrder())
        {
            ItemSlot<TValue> slot = registration.Slot;
            if (!slot.Present || ValueNull.IsNull(slot.Value))
            {
                continue;
            }

            if (byText is null
                && (string.Equals(ValueLabels.StringifyAsValue(slot.Value, ItemToStringValue), text, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(ValueLabels.StringifyAsLabel(slot.Value, ItemToStringLabel), text, StringComparison.OrdinalIgnoreCase)))
            {
                byText = slot;
            }

            if (byLabel is null && registration.Label is { } label && string.Equals(label, text, StringComparison.OrdinalIgnoreCase))
            {
                byLabel = slot;
            }
        }

        return byText ?? byLabel;
    }

    // SEL-KEY-04 (SelectRoot.tsx:392-406): the closed trigger's match is a value request with reason `none`.
    /// <inheritdoc />
    public override Task MatchItemAsync(int index, EventSnapshot snapshot)
    {
        if (index < 0 || index >= slots.Length || !slots[index].Present)
        {
            return Task.CompletedTask;
        }

        return RequestValueAsync(slots[index].Value, new ChangeEventDetails(EventReason.None, snapshot));
    }

    // SEL-KEY-08, SEL-PTR-08, SEL-EVT-05, SEL-EVT-12, SEL-EDGE-06 (SelectItem.tsx:133-151): the commit re-checks the
    // root's disabled and read-only flags; single mode requests the value, then the close with its own details, whether or
    // not the value was canceled.
    /// <summary>Commits an item's activation.</summary>
    /// <param name="registration">The item's registration.</param>
    /// <param name="snapshot">The activation's event.</param>
    /// <returns>A task that completes when the requests have run.</returns>
    public async Task CommitSelectionAsync(SelectItemRegistration<TValue> registration, EventSnapshot? snapshot)
    {
        SelectStoreState state = State;
        if (state.Disabled || state.ReadOnly || registration.Disabled)
        {
            return;
        }

        if (state.Multiple)
        {
            IReadOnlyList<TValue> current = Values;
            TValue itemValue = registration.Slot.Value;
            IReadOnlyList<TValue> next = IsSelected(registration.Slot) ? ItemEquality.RemoveItem(current, itemValue, Comparer) : Append(current, itemValue);
            await RequestValuesAsync(next, new ChangeEventDetails(EventReason.ItemPress, snapshot));
            return;
        }

        await RequestValueAsync(registration.Slot.Value, new ChangeEventDetails(EventReason.ItemPress, snapshot));
        await RequestOpenAsync(false, new OpenChangeEventDetails(EventReason.ItemPress, snapshot));
    }

    // SEL-EVT-01..03 (SelectRoot.tsx:330-340): the consumer's callback first; a cancel stops the commit.
    /// <summary>Runs the single-mode value request.</summary>
    /// <param name="next">The requested value.</param>
    /// <param name="details">The request's details.</param>
    /// <returns><see langword="true"/> if the request was not canceled.</returns>
    public async Task<bool> RequestValueAsync(TValue next, ChangeEventDetails details)
    {
        if (!await ValueState.NotifyAsync(next, details, OnValueChange))
        {
            return false;
        }

        bool had = HasValue;
        TValue previous = Value;
        await ValueState.CommitAsync(next, ValueChanged);
        if (!ValueState.IsControlled)
        {
            committedOnce = true;
        }

        if (had != HasValue || !Equality.Is(previous, Value))
        {
            ValueApplied();
        }

        return true;
    }

    /// <summary>Runs the multiple-mode value request.</summary>
    /// <param name="next">The requested values.</param>
    /// <param name="details">The request's details.</param>
    /// <returns><see langword="true"/> if the request was not canceled.</returns>
    public async Task<bool> RequestValuesAsync(IReadOnlyList<TValue> next, ChangeEventDetails details)
    {
        if (!await ValuesState.NotifyAsync(next, details, OnValuesChange))
        {
            return false;
        }

        IReadOnlyList<TValue>? previous = ValuesState.Value;
        await ValuesState.CommitAsync(next, ValuesChanged);
        if (!ReferenceEquals(previous, ValuesState.Value))
        {
            ValueApplied();
        }

        return true;
    }

    // A controlled value the root received, or a committed one: one write with the new counter, the selected flag and,
    // while closed, the selected index (SEL-EVT-06, SEL-STA-04).
    /// <summary>Publishes a value that changed, its counter and what depends on it.</summary>
    public void ValueApplied()
    {
        SelectStoreState state = State;
        int? index = state.Lifecycle.EffectiveOpen ? state.SelectedIndex : FindSelectionIndex();
        Write(state with { ValueVersion = state.ValueVersion + 1, HasSelectedValue = ComputeHasSelectedValue(), SelectedIndex = index });
        RefreshTexts();
    }

    // The root's configuration that changes item equality or the mode re-runs the closed sync (its deps: multiple, value,
    // isItemEqualToValue, open).
    /// <summary>Recomputes the selected index after the comparer, the mode or the open state changed.</summary>
    public void SyncSelectedIndex()
    {
        SelectStoreState state = State;
        if (state.Lifecycle.EffectiveOpen)
        {
            return;
        }

        int? index = FindSelectionIndex();
        if (state.SelectedIndex != index)
        {
            Write(state with { SelectedIndex = index });
            RefreshTexts();
        }
    }

    // SEL-STA-04: the closed sync runs again once the popup is closed.
    /// <inheritdoc />
    private protected override SelectStoreState WithCommittedOpen(SelectStoreState state, SelectStoreState applied, in PopupLifecycleState lifecycle, bool open, OpenChangeEventDetails details)
    {
        SelectStoreState next = applied with { Lifecycle = lifecycle };
        return open ? next : next with { SelectedIndex = FindSelectionIndex() };
    }

    private async Task<bool> CommitAndRunAsync(SelectItemRegistration<TValue> registration, EventSnapshot snapshot)
    {
        await CommitSelectionAsync(registration, snapshot);
        return false;
    }

    private static IReadOnlyList<TValue> Append(IReadOnlyList<TValue> current, TValue value)
    {
        TValue[] next = new TValue[current.Count + 1];
        for (int i = 0; i < current.Count; i++)
        {
            next[i] = current[i];
        }

        next[current.Count] = value;
        return next;
    }

    private static bool SameIds(string[] previous, IReadOnlyList<string> next)
    {
        if (previous.Length != next.Count)
        {
            return false;
        }

        for (int i = 0; i < previous.Length; i++)
        {
            if (!string.Equals(previous[i], next[i], StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    private int? FindSelectionIndex()
    {
        if (State.Multiple)
        {
            return ItemEquality.FindSelectionIndex(slots, Values, Comparer);
        }

        if (!HasValue && !s_nullableValue)
        {
            return null;
        }

        return ItemEquality.FindSelectionIndex(slots, Value, Comparer);
    }

    // SEL-STA-05/06 (SelectItem.tsx:80-115): single mode claims for a matching item; multiple mode resolves the anchor.
    private void Claim(int index, in ItemSlot<TValue> slot)
    {
        SelectStoreState state = State;
        int? next = state.SelectedIndex;
        if (state.Multiple)
        {
            next = slot.Present ? ItemEquality.ResolveSelectedIndex(index, slot.Value, slots, Values, Comparer, state.SelectedIndex) : state.SelectedIndex;
        }
        else if (IsSelected(slot))
        {
            next = index;
        }

        if (next != state.SelectedIndex)
        {
            Write(state with { SelectedIndex = next });
        }
    }

    // SEL-STA-12/13 (SelectPositioner.tsx:141-194).
    private async Task ReconcileAsync()
    {
        if (!AnyPresent())
        {
            return;
        }

        ChangeEventDetails details = new(EventReason.None);
        SelectStoreState state = State;
        if (!state.Multiple)
        {
            if (!HasValue || ValueNull.IsNull(Value) || ItemEquality.FindItemIndex(slots, Value, Comparer) != -1)
            {
                return;
            }

            bool hasInitial = initialPresent && ItemEquality.FindItemIndex(slots, initialValue, Comparer) != -1;
            TValue next = hasInitial ? initialValue : default!;
            await RequestValueAsync(next, details);
            if (!hasInitial)
            {
                ClearSelectedIndex();
            }

            return;
        }

        IReadOnlyList<TValue>? values = ValuesState.Value;
        if (values is null)
        {
            return;
        }

        int kept = 0;
        for (int i = 0; i < values.Count; i++)
        {
            if (ItemEquality.FindItemIndex(slots, values[i], Comparer) != -1)
            {
                kept++;
            }
        }

        if (kept == values.Count)
        {
            return;
        }

        TValue[] filtered = kept == 0 ? [] : new TValue[kept];
        int at = 0;
        for (int i = 0; i < values.Count; i++)
        {
            if (ItemEquality.FindItemIndex(slots, values[i], Comparer) != -1)
            {
                filtered[at++] = values[i];
            }
        }

        await RequestValuesAsync(filtered, details);
        if (kept == 0)
        {
            ClearSelectedIndex();
        }
    }

    private void ClearSelectedIndex()
    {
        if (State.SelectedIndex is not null)
        {
            Write(State with { SelectedIndex = null });
            RefreshTexts();
        }
    }

    private bool AnyPresent()
    {
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i].Present)
            {
                return true;
            }
        }

        return false;
    }

    private IEnumerable<SelectItemRegistration<TValue>> RenderedOrder()
    {
        if (orderedIds.Length > 0)
        {
            for (int i = 0; i < orderedIds.Length; i++)
            {
                if (Find(orderedIds[i]) is { } registration)
                {
                    yield return registration;
                }
            }

            yield break;
        }

        for (int i = 0; i < registrationOrder.Count; i++)
        {
            yield return registrationOrder[i];
        }
    }

    private int IndexOf(string id)
    {
        for (int i = 0; i < orderedIds.Length; i++)
        {
            if (string.Equals(orderedIds[i], id, StringComparison.Ordinal))
            {
                return i;
            }
        }

        return -1;
    }

    // SEL-POS-07 step 2's references: the selected item's text, else the first item's; the counter tells the
    // positioner to send them again.
    private void RefreshTexts()
    {
        int? index = State.SelectedIndex;
        string? selected = index is int at && at >= 0 && at < orderedIds.Length ? Find(orderedIds[at])?.TextId : null;
        string? first = orderedIds.Length > 0 ? Find(orderedIds[0])?.TextId : null;
        if (!string.Equals(selected, selectedTextId, StringComparison.Ordinal) || !string.Equals(first, firstTextId, StringComparison.Ordinal))
        {
            selectedTextId = selected;
            firstTextId = first;
            TouchText();
        }
    }
}

/// <summary>What a select item registers with its store: its id, its value and what activation and placement read.</summary>
/// <typeparam name="TValue">The type of the select's values.</typeparam>
/// <param name="id">The id the item renders.</param>
internal sealed class SelectItemRegistration<TValue>(string id)
{
    /// <summary>Gets the id the item renders.</summary>
    public string Id { get; } = id;

    /// <summary>Gets or sets the item's value slot: absent for an omitted value of a non-nullable type.</summary>
    public ItemSlot<TValue> Slot { get; set; }

    /// <summary>Gets or sets the item's explicit label, or <see langword="null"/>.</summary>
    public string? Label { get; set; }

    /// <summary>Gets or sets a value indicating whether the item is disabled, its own flag or the root's.</summary>
    public bool Disabled { get; set; }

    /// <summary>Gets or sets the id of the item's text element, or <see langword="null"/>.</summary>
    public string? TextId { get; set; }

    /// <summary>Gets or sets the item's index in rendered order, or -1 before the list reported it.</summary>
    public int Index { get; set; } = -1;

    /// <summary>Gets or sets the item's activation: its consumer <c>onclick</c>, then the commit; the result is whether the consumer vetoed it.</summary>
    public Func<EventSnapshot, Task<bool>>? Activate { get; set; }
}

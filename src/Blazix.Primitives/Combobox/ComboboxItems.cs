using System.Collections;
using Blazix.Primitives.Store;
using Blazix.Primitives.Utilities;

namespace Blazix.Primitives;

// Base UI's createItems (combobox/items/createItems.ts, itemCollection.ts; CMB-API-32, EDGE-12), in design point 2's shape
// (settled by the orchestrator 2026-11-04): the collection is a list of the derived values, so it passes as the root's
// Items (or, grouped, as ItemGroups) with TValue inferred through the interface; the root resolves a value's label from the
// collection first (then from a collection given as the filtered items, then ItemToStringLabel, then the invariant text), and
// Get(value) returns the source item. The accessors never run at creation; the value index is built on the first lookup, the
// first item of a duplicated value winning.
/// <summary>Creates item collections for a combobox whose items carry their own values and labels.</summary>
public static class ComboboxItems
{
    /// <summary>Creates a flat collection of items, each with a value and a label.</summary>
    /// <typeparam name="TItem">The type of the source items.</typeparam>
    /// <typeparam name="TValue">The type of the values the combobox selects.</typeparam>
    /// <param name="data">The source items.</param>
    /// <param name="getValue">Returns an item's value.</param>
    /// <param name="getLabel">Returns an item's label, or <see langword="null"/> to label values by the root's converter.</param>
    /// <returns>The collection, which passes as the root's <c>Items</c>.</returns>
    public static ComboboxItemCollection<TItem, TValue> Create<TItem, TValue>(IReadOnlyList<TItem> data, Func<TItem, TValue> getValue, Func<TItem, string>? getLabel = null)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(getValue);
        return new ComboboxItemCollection<TItem, TValue>(data, getValue, getLabel);
    }

    /// <summary>Creates a grouped collection of items, each with a value and a label.</summary>
    /// <typeparam name="TItem">The type of the source items.</typeparam>
    /// <typeparam name="TValue">The type of the values the combobox selects.</typeparam>
    /// <param name="groups">The groups of source items.</param>
    /// <param name="getValue">Returns an item's value.</param>
    /// <param name="getLabel">Returns an item's label, or <see langword="null"/> to label values by the root's converter.</param>
    /// <returns>The collection, which passes as the root's <c>ItemGroups</c>.</returns>
    public static ComboboxItemGroupCollection<TItem, TValue> CreateGrouped<TItem, TValue>(IReadOnlyList<ComboboxItemGroup<TItem>> groups, Func<TItem, TValue> getValue, Func<TItem, string>? getLabel = null)
    {
        ArgumentNullException.ThrowIfNull(groups);
        ArgumentNullException.ThrowIfNull(getValue);
        return new ComboboxItemGroupCollection<TItem, TValue>(groups, getValue, getLabel);
    }
}

/// <summary>A flat combobox item collection: the derived values of its source items, with their labels and the items themselves.</summary>
/// <typeparam name="TItem">The type of the source items.</typeparam>
/// <typeparam name="TValue">The type of the values the combobox selects.</typeparam>
public sealed class ComboboxItemCollection<TItem, TValue> : IReadOnlyList<TValue>, IComboboxItemLabels<TValue>
{
    private readonly ComboboxItemIndex<TItem, TValue> index;

    internal ComboboxItemCollection(IReadOnlyList<TItem> data, Func<TItem, TValue> getValue, Func<TItem, string>? getLabel)
    {
        Data = data;
        index = new ComboboxItemIndex<TItem, TValue>(getValue, getLabel, () => data);
    }

    /// <summary>Gets the source items.</summary>
    public IReadOnlyList<TItem> Data { get; }

    /// <inheritdoc />
    public int Count => Data.Count;

    /// <inheritdoc />
    public TValue this[int index] => this.index.ValueOf(Data[index]);

    /// <inheritdoc />
    IReadOnlyList<TValue> IComboboxItemLabels<TValue>.DuplicateValues => index.DuplicateValues;

    /// <summary>Gets the source item whose value is <paramref name="value"/>.</summary>
    /// <param name="value">The value.</param>
    /// <returns>The first source item with that value.</returns>
    /// <exception cref="KeyNotFoundException">No source item has that value.</exception>
    public TItem Get(TValue value) =>
        index.TryFind(value, null, out TItem? item) ? item! : throw new KeyNotFoundException($"Blazix: no item of the collection has the value `{value}`.");

    /// <summary>Gets the source item whose value is <paramref name="value"/>, if any.</summary>
    /// <param name="value">The value.</param>
    /// <param name="item">The first source item with that value, or the default value.</param>
    /// <returns><see langword="true"/> when an item has that value.</returns>
    public bool TryGet(TValue value, out TItem? item) => index.TryFind(value, null, out item);

    /// <inheritdoc />
    public IEnumerator<TValue> GetEnumerator()
    {
        for (int i = 0; i < Data.Count; i++)
        {
            yield return index.ValueOf(Data[i]);
        }
    }

    /// <inheritdoc />
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <inheritdoc />
    bool IComboboxItemLabels<TValue>.TryGetLabel(TValue value, Func<TValue, TValue, bool>? isItemEqualToValue, out string label) =>
        index.TryGetLabel(value, isItemEqualToValue, out label);

    /// <inheritdoc />
    bool IComboboxItemLabels<TValue>.HasValue(TValue value, Func<TValue, TValue, bool>? isItemEqualToValue) =>
        index.TryFind(value, isItemEqualToValue, out _);
}

/// <summary>A grouped combobox item collection: its groups with their items' derived values, with the items' labels and the items themselves.</summary>
/// <typeparam name="TItem">The type of the source items.</typeparam>
/// <typeparam name="TValue">The type of the values the combobox selects.</typeparam>
public sealed class ComboboxItemGroupCollection<TItem, TValue> : IReadOnlyList<ComboboxItemGroup<TValue>>, IComboboxItemLabels<TValue>
{
    private readonly ComboboxItemIndex<TItem, TValue> index;

    private readonly ComboboxItemGroup<TValue>[] projected;

    internal ComboboxItemGroupCollection(IReadOnlyList<ComboboxItemGroup<TItem>> groups, Func<TItem, TValue> getValue, Func<TItem, string>? getLabel)
    {
        Groups = groups;
        index = new ComboboxItemIndex<TItem, TValue>(getValue, getLabel, () => groups.SelectMany(static group => group.Items));
        projected = new ComboboxItemGroup<TValue>[groups.Count];
        for (int i = 0; i < projected.Length; i++)
        {
            ComboboxItemGroup<TItem> group = groups[i];
            projected[i] = new ComboboxItemGroup<TValue>(group.Value, new ProjectedValues(group.Items, index)) { Label = group.Label };
        }
    }

    /// <summary>Gets the groups of source items.</summary>
    public IReadOnlyList<ComboboxItemGroup<TItem>> Groups { get; }

    /// <inheritdoc />
    public int Count => projected.Length;

    /// <inheritdoc />
    public ComboboxItemGroup<TValue> this[int index] => projected[index];

    /// <inheritdoc />
    IReadOnlyList<TValue> IComboboxItemLabels<TValue>.DuplicateValues => index.DuplicateValues;

    /// <summary>Gets the source item whose value is <paramref name="value"/>.</summary>
    /// <param name="value">The value.</param>
    /// <returns>The first source item with that value.</returns>
    /// <exception cref="KeyNotFoundException">No source item has that value.</exception>
    public TItem Get(TValue value) =>
        index.TryFind(value, null, out TItem? item) ? item! : throw new KeyNotFoundException($"Blazix: no item of the collection has the value `{value}`.");

    /// <summary>Gets the source item whose value is <paramref name="value"/>, if any.</summary>
    /// <param name="value">The value.</param>
    /// <param name="item">The first source item with that value, or the default value.</param>
    /// <returns><see langword="true"/> when an item has that value.</returns>
    public bool TryGet(TValue value, out TItem? item) => index.TryFind(value, null, out item);

    /// <inheritdoc />
    public IEnumerator<ComboboxItemGroup<TValue>> GetEnumerator() => ((IEnumerable<ComboboxItemGroup<TValue>>)projected).GetEnumerator();

    /// <inheritdoc />
    IEnumerator IEnumerable.GetEnumerator() => projected.GetEnumerator();

    /// <inheritdoc />
    bool IComboboxItemLabels<TValue>.TryGetLabel(TValue value, Func<TValue, TValue, bool>? isItemEqualToValue, out string label) =>
        index.TryGetLabel(value, isItemEqualToValue, out label);

    /// <inheritdoc />
    bool IComboboxItemLabels<TValue>.HasValue(TValue value, Func<TValue, TValue, bool>? isItemEqualToValue) =>
        index.TryFind(value, isItemEqualToValue, out _);

    // A group's items as their values, projected on read so the accessors never run at creation.
    private sealed class ProjectedValues(IReadOnlyList<TItem> items, ComboboxItemIndex<TItem, TValue> index) : IReadOnlyList<TValue>
    {
        /// <inheritdoc />
        public int Count => items.Count;

        /// <inheritdoc />
        public TValue this[int position] => index.ValueOf(items[position]);

        /// <inheritdoc />
        public IEnumerator<TValue> GetEnumerator()
        {
            for (int i = 0; i < items.Count; i++)
            {
                yield return index.ValueOf(items[i]);
            }
        }

        /// <inheritdoc />
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}

/// <summary>Resolves the label of a value from the items that carry it.</summary>
/// <typeparam name="TValue">The type of the values.</typeparam>
internal interface IComboboxItemLabels<TValue>
{
    /// <summary>Gets the derived values two or more items share, each once, in order (createItems.ts:118-125).</summary>
    IReadOnlyList<TValue> DuplicateValues { get; }

    /// <summary>Gets the label of the first item whose value is <paramref name="value"/>.</summary>
    /// <param name="value">The value.</param>
    /// <param name="isItemEqualToValue">The root's comparer, or <see langword="null"/> for the default equality.</param>
    /// <param name="label">The item's label, or the empty string.</param>
    /// <returns><see langword="true"/> when an item with a label has that value.</returns>
    bool TryGetLabel(TValue value, Func<TValue, TValue, bool>? isItemEqualToValue, out string label);

    /// <summary>Gets a value indicating whether an item has the value <paramref name="value"/> (createItems.ts:147-149).</summary>
    /// <param name="value">The value.</param>
    /// <param name="isItemEqualToValue">The root's comparer, or <see langword="null"/> for the default equality.</param>
    /// <returns><see langword="true"/> when an item has that value.</returns>
    bool HasValue(TValue value, Func<TValue, TValue, bool>? isItemEqualToValue);
}

// itemCollection.ts:9-29 (findCollectionItem): the exact value first; with a custom comparer, every derived value in order.
/// <summary>The lazily built value index of a collection's source items.</summary>
/// <typeparam name="TItem">The type of the source items.</typeparam>
/// <typeparam name="TValue">The type of the values.</typeparam>
internal sealed class ComboboxItemIndex<TItem, TValue>(Func<TItem, TValue> getValue, Func<TItem, string>? getLabel, Func<IEnumerable<TItem>> leaves)
{
    private List<(TValue Value, TItem Item)>? entries;

    private Dictionary<Key, TItem>? exact;

    private bool hasNull;

    private TItem? nullItem;

    private List<TValue>? duplicates;

    /// <summary>Gets the derived values two or more items share, each once, in order.</summary>
    public IReadOnlyList<TValue> DuplicateValues
    {
        get
        {
            Build();
            return duplicates ?? (IReadOnlyList<TValue>)[];
        }
    }

    /// <summary>Gets an item's value.</summary>
    /// <param name="item">The item.</param>
    /// <returns>The value; the default value for a <see langword="null"/> item.</returns>
    public TValue ValueOf(TItem item) => item is null ? default! : getValue(item);

    /// <summary>Finds the first item whose value is <paramref name="value"/>.</summary>
    /// <param name="value">The value.</param>
    /// <param name="isItemEqualToValue">The root's comparer, or <see langword="null"/>.</param>
    /// <param name="item">The item, or the default value.</param>
    /// <returns><see langword="true"/> when an item has that value.</returns>
    public bool TryFind(TValue value, Func<TValue, TValue, bool>? isItemEqualToValue, out TItem? item)
    {
        Build();
        if (ValueNull.IsNull(value))
        {
            item = nullItem;
            return hasNull;
        }

        if (exact!.TryGetValue(new Key(value), out TItem? found))
        {
            item = found;
            return true;
        }

        if (isItemEqualToValue is not null)
        {
            foreach ((TValue derived, TItem candidate) in entries!)
            {
                if (ItemEquality.Compare(derived, value, isItemEqualToValue))
                {
                    item = candidate;
                    return true;
                }
            }
        }

        item = default;
        return false;
    }

    /// <summary>Gets the label of the first item whose value is <paramref name="value"/>.</summary>
    /// <param name="value">The value.</param>
    /// <param name="isItemEqualToValue">The root's comparer, or <see langword="null"/>.</param>
    /// <param name="label">The label, or the empty string.</param>
    /// <returns><see langword="true"/> when a labelled item has that value.</returns>
    public bool TryGetLabel(TValue value, Func<TValue, TValue, bool>? isItemEqualToValue, out string label)
    {
        if (getLabel is not null && TryFind(value, isItemEqualToValue, out TItem? item))
        {
            label = getLabel(item!) ?? string.Empty;
            return true;
        }

        label = string.Empty;
        return false;
    }

    private void AddDuplicate(TValue value)
    {
        duplicates ??= [];
        foreach (TValue known in duplicates)
        {
            if (Equality.Is(known, value))
            {
                return;
            }
        }

        duplicates.Add(value);
    }

    private void Build()
    {
        if (exact is not null)
        {
            return;
        }

        List<(TValue, TItem)> list = [];
        Dictionary<Key, TItem> map = [];
        foreach (TItem item in leaves())
        {
            // createItems.ts:83-86: nullish entries are skipped.
            if (item is null)
            {
                continue;
            }

            TValue value = getValue(item);
            list.Add((value, item));
            if (ValueNull.IsNull(value))
            {
                if (!hasNull)
                {
                    hasNull = true;
                    nullItem = item;
                }
                else
                {
                    AddDuplicate(value);
                }
            }
            else if (!map.TryAdd(new Key(value), item))
            {
                // The first occurrence wins; Base UI reports the duplicate in development (createItems.ts:116-124).
                AddDuplicate(value);
            }
        }

        entries = list;
        exact = map;
    }

    // A non-null dictionary key over the unconstrained value type, compared by the type's default equality.
    private readonly record struct Key(TValue Value);
}

using Blazix.Primitives.Store;

namespace Blazix.Primitives.Utilities;

// UTL-ALG-07, UTL-I18N-13 (internals/resolveValueLabel.tsx), ADR 08 Amendments 2026-11-03 (2); design point 2 of M5 track 4,
// settled by the orchestrator 2026-11-04: the input is the internal LabeledItems<TValue> view the families adapt their
// public `Items` to, and a label defaults to the value's invariant text (FormValue.Serialize, Base UI's String(value)). An
// object's `label` and `value` members are never read (ADR 16 rule 1): ItemToStringLabel and ItemToStringValue convert
// instead. Leaves compare with ===, Equality.StrictEquals, as resolveSelectedLabel does.
/// <summary>Resolves the text a list shows and submits for a selected value.</summary>
internal static class ValueLabels
{
    /// <summary>Gets a value indicating whether the items give a <see langword="null"/> value a label.</summary>
    /// <typeparam name="TValue">The type of the values.</typeparam>
    /// <param name="items">The items.</param>
    /// <returns>
    /// <see langword="true"/> when a leaf with a <see langword="null"/> value has a label, or the keyed form has the
    /// <c>"null"</c> key.
    /// </returns>
    public static bool HasNullItemLabel<TValue>(LabeledItems<TValue> items)
    {
        if (items.Labels is { } keyed)
        {
            return keyed.ContainsKey(LabeledItems<TValue>.NullKey);
        }

        foreach (LabeledItem<TValue> leaf in items.Leaves)
        {
            if (ValueNull.IsNull(leaf.Value) && leaf.Label is not null)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Gets the text a list shows for an item or a value when no label is found.</summary>
    /// <typeparam name="TValue">The type of the value.</typeparam>
    /// <param name="value">The value.</param>
    /// <param name="itemToStringLabel">The converter, or <see langword="null"/>; never called with <see langword="null"/>.</param>
    /// <returns>The converter's text, or the value's invariant text; the empty string for <see langword="null"/>.</returns>
    public static string StringifyAsLabel<TValue>(TValue value, Func<TValue, string>? itemToStringLabel)
    {
        if (itemToStringLabel is not null && !ValueNull.IsNull(value))
        {
            return itemToStringLabel(value) ?? string.Empty;
        }

        return FormValue.Serialize(value) ?? string.Empty;
    }

    /// <summary>Gets the text a list submits or matches for an item or a value.</summary>
    /// <typeparam name="TValue">The type of the value.</typeparam>
    /// <param name="value">The value.</param>
    /// <param name="itemToStringValue">The converter, or <see langword="null"/>; never called with <see langword="null"/>.</param>
    /// <returns>The converter's text, or the value's invariant text; the empty string for <see langword="null"/>.</returns>
    public static string StringifyAsValue<TValue>(TValue value, Func<TValue, string>? itemToStringValue)
    {
        if (itemToStringValue is not null && !ValueNull.IsNull(value))
        {
            return itemToStringValue(value) ?? string.Empty;
        }

        return FormValue.Serialize(value) ?? string.Empty;
    }

    /// <summary>Resolves the label of a selected value.</summary>
    /// <typeparam name="TValue">The type of the value.</typeparam>
    /// <param name="value">The selected value.</param>
    /// <param name="items">The items whose labels apply.</param>
    /// <param name="itemToStringLabel">The converter, or <see langword="null"/>; it wins for a non-null value.</param>
    /// <returns>The converter's text, the matching leaf's or key's label, or else <see cref="StringifyAsLabel"/>.</returns>
    public static string ResolveSelectedLabel<TValue>(TValue value, LabeledItems<TValue> items, Func<TValue, string>? itemToStringLabel)
    {
        if (itemToStringLabel is not null && !ValueNull.IsNull(value))
        {
            return itemToStringLabel(value) ?? string.Empty;
        }

        if (items.Labels is { } keyed)
        {
            string key = ValueNull.IsNull(value) ? LabeledItems<TValue>.NullKey : FormValue.Serialize(value)!;
            return keyed.TryGetValue(key, out string? label) && label is not null ? label : StringifyAsLabel(value, itemToStringLabel);
        }

        foreach (LabeledItem<TValue> leaf in items.Leaves)
        {
            if (Equality.StrictEquals(leaf.Value, value))
            {
                return leaf.Label ?? StringifyAsLabel(value, itemToStringLabel);
            }
        }

        return StringifyAsLabel(value, itemToStringLabel);
    }

    /// <summary>Resolves the labels of the selected values of a multiple list.</summary>
    /// <typeparam name="TValue">The type of the values.</typeparam>
    /// <param name="values">The selected values.</param>
    /// <param name="items">The items whose labels apply.</param>
    /// <param name="itemToStringLabel">The converter, or <see langword="null"/>.</param>
    /// <returns>Each value's <see cref="ResolveSelectedLabel"/>, joined with <c>", "</c>.</returns>
    public static string ResolveMultipleLabels<TValue>(IReadOnlyList<TValue> values, LabeledItems<TValue> items, Func<TValue, string>? itemToStringLabel)
    {
        if (values.Count == 0)
        {
            return string.Empty;
        }

        if (values.Count == 1)
        {
            return ResolveSelectedLabel(values[0], items, itemToStringLabel);
        }

        string[] labels = new string[values.Count];
        for (int i = 0; i < labels.Length; i++)
        {
            labels[i] = ResolveSelectedLabel(values[i], items, itemToStringLabel);
        }

        return string.Join(", ", labels);
    }
}

// UTL-ALG-07's input (design point 2 (a) of M5 track 4): Base UI's three `items` forms, typed. A flat list of leaves, groups of
// leaves (the group's heading and other members stay the family's), or the record form keyed by the value's invariant text
// with "null" for null (Object.hasOwn's key). `default` is no items.
/// <summary>Holds the labeled items of a list in one of Base UI's three forms.</summary>
/// <typeparam name="TValue">The type of the values.</typeparam>
internal readonly struct LabeledItems<TValue>
{
    /// <summary>Represents the key of the keyed form that labels a <see langword="null"/> value.</summary>
    public const string NullKey = "null";

    private readonly IReadOnlyList<LabeledItem<TValue>>? flat;
    private readonly IReadOnlyList<IReadOnlyList<LabeledItem<TValue>>>? groups;

    private LabeledItems(IReadOnlyList<LabeledItem<TValue>>? flat, IReadOnlyList<IReadOnlyList<LabeledItem<TValue>>>? groups, IReadOnlyDictionary<string, string?>? keyed)
    {
        this.flat = flat;
        this.groups = groups;
        Labels = keyed;
    }

    /// <summary>Gets the keyed form's labels, or <see langword="null"/> for the other forms.</summary>
    public IReadOnlyDictionary<string, string?>? Labels { get; }

    /// <summary>Gets a value indicating whether the items are groups of leaves.</summary>
    public bool IsGrouped => groups is not null;

    /// <summary>Gets the leaves of the flat or grouped form, in order; none for the keyed form.</summary>
    public LeafEnumerable Leaves => new(flat, groups);

    /// <summary>Creates the flat form.</summary>
    /// <param name="items">The leaves.</param>
    /// <returns>The view.</returns>
    public static LabeledItems<TValue> Flat(IReadOnlyList<LabeledItem<TValue>> items) => new(items, null, null);

    /// <summary>Creates the grouped form.</summary>
    /// <param name="groups">The groups' leaves, in order.</param>
    /// <returns>The view.</returns>
    public static LabeledItems<TValue> Grouped(IReadOnlyList<IReadOnlyList<LabeledItem<TValue>>> groups) => new(null, groups, null);

    /// <summary>Creates the keyed form.</summary>
    /// <param name="labels">The labels by the value's invariant text, <c>"null"</c> for <see langword="null"/>.</param>
    /// <returns>The view.</returns>
    public static LabeledItems<TValue> Keyed(IReadOnlyDictionary<string, string?> labels) => new(null, null, labels);

    /// <summary>Enumerates the leaves of the flat or grouped form without allocating.</summary>
    /// <param name="flat">The flat leaves, or <see langword="null"/>.</param>
    /// <param name="groups">The groups, or <see langword="null"/>.</param>
    public readonly struct LeafEnumerable(IReadOnlyList<LabeledItem<TValue>>? flat, IReadOnlyList<IReadOnlyList<LabeledItem<TValue>>>? groups)
    {
        /// <summary>Gets the enumerator.</summary>
        /// <returns>The enumerator.</returns>
        public LeafEnumerator GetEnumerator() => new(flat, groups);
    }

    /// <summary>Walks the leaves of the flat or grouped form in order.</summary>
    public struct LeafEnumerator
    {
        private readonly IReadOnlyList<LabeledItem<TValue>>? flat;
        private readonly IReadOnlyList<IReadOnlyList<LabeledItem<TValue>>>? groups;
        private int group;
        private int index;

        /// <summary>Initializes a new instance of the <see cref="LeafEnumerator"/> struct.</summary>
        /// <param name="flat">The flat leaves, or <see langword="null"/>.</param>
        /// <param name="groups">The groups, or <see langword="null"/>.</param>
        public LeafEnumerator(IReadOnlyList<LabeledItem<TValue>>? flat, IReadOnlyList<IReadOnlyList<LabeledItem<TValue>>>? groups)
        {
            this.flat = flat;
            this.groups = groups;
            group = 0;
            index = -1;
        }

        /// <summary>Gets the current leaf.</summary>
        public readonly LabeledItem<TValue> Current => flat is not null ? flat[index] : groups![group][index];

        /// <summary>Advances to the next leaf.</summary>
        /// <returns><see langword="true"/> while a leaf remains.</returns>
        public bool MoveNext()
        {
            index++;
            if (flat is not null)
            {
                return index < flat.Count;
            }

            if (groups is null)
            {
                return false;
            }

            while (group < groups.Count)
            {
                if (index < groups[group].Count)
                {
                    return true;
                }

                group++;
                index = 0;
            }

            return false;
        }
    }
}

/// <summary>Holds one item of a list with its label.</summary>
/// <typeparam name="TValue">The type of the value.</typeparam>
/// <param name="Value">The item's value.</param>
/// <param name="Label">The item's label, or <see langword="null"/> for none.</param>
internal readonly record struct LabeledItem<TValue>(TValue Value, string? Label);

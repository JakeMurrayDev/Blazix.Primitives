using Blazix.Primitives.Utilities;

namespace Blazix.Primitives;

// CMB-STA-22 and CMB-STA-23 (AriaCombobox.tsx:342-452): the bypass and the filtered-items derivation, pure C# (ADR 04 §1's
// last paragraph: results that are state are C#'s; the brief's [L1] derivation tests). The store calls Derive once per
// parameter set or committed query and keeps the previous result when the membership is unchanged (CMB-API-55, EDGE-10).
/// <summary>Derives a combobox's filtered items from its items, the query, the limit and the filter.</summary>
internal static class ComboboxItemFiltering
{
    // AriaCombobox.tsx:342-349: single mode, no query typed since open, a non-empty query, and the selected label equal to it
    // under the collator (same length, `contains`).
    /// <summary>Determines whether filtering is bypassed because the query only mirrors the single selection.</summary>
    /// <param name="single">Whether the combobox is in single mode.</param>
    /// <param name="queryChangedAfterOpen">Whether a query was typed since the popup opened.</param>
    /// <param name="query">The trimmed query.</param>
    /// <param name="selectedLabel">The selected value's label, or the empty string.</param>
    /// <param name="collator">The collator filter of the root's culture.</param>
    /// <returns><see langword="true"/> when the effective filter query is the empty string.</returns>
    public static bool ShouldBypassFiltering(bool single, bool queryChangedAfterOpen, string query, string selectedLabel, CollatorFilter collator) =>
        single && !queryChangedAfterOpen && query.Length != 0 && selectedLabel.Length == query.Length && collator.Contains(selectedLabel, query);

    // AriaCombobox.tsx:363-439, in order: (1) the external result when given and not ignored; (2) nothing without items;
    // (3) groups, walked in order up to the limit, a group kept only when it contributed; (4) flat items up to the limit,
    // the items' own list when the query is empty and nothing limits it.
    /// <summary>Derives the filtered items.</summary>
    /// <typeparam name="TValue">The type of the items' values.</typeparam>
    /// <param name="inputs">The derivation's inputs.</param>
    /// <returns>The filtered items, flat, and their groups when the items are grouped.</returns>
    public static ComboboxFilteredItems<TValue> Derive<TValue>(in ComboboxFilterInputs<TValue> inputs)
    {
        if (!inputs.IgnoreExternal)
        {
            if (inputs.FilteredGroups is { } externalGroups)
            {
                return new(Flatten(externalGroups), externalGroups);
            }

            if (inputs.FilteredItems is { } external)
            {
                return new(external, null);
            }
        }

        if (inputs.Groups is { } groups)
        {
            return DeriveGroups(groups, inputs);
        }

        if (inputs.Items is not { } items)
        {
            return new([], null);
        }

        int limit = inputs.Limit;
        if (inputs.FilterQuery.Length == 0)
        {
            if (limit < 0 || limit >= items.Count)
            {
                return new(items, null);
            }

            TValue[] head = new TValue[limit];
            for (int i = 0; i < limit; i++)
            {
                head[i] = items[i];
            }

            return new(head, null);
        }

        List<TValue> matched = [];
        foreach (TValue item in items)
        {
            if (limit > -1 && matched.Count >= limit)
            {
                break;
            }

            if (inputs.Filter.Matches(item, inputs.FilterQuery))
            {
                matched.Add(item);
            }
        }

        return new(matched, null);
    }

    private static ComboboxFilteredItems<TValue> DeriveGroups<TValue>(IReadOnlyList<ComboboxItemGroup<TValue>> groups, in ComboboxFilterInputs<TValue> inputs)
    {
        int limit = inputs.Limit;
        List<ComboboxItemGroup<TValue>> kept = [];
        List<TValue> flat = [];
        foreach (ComboboxItemGroup<TValue> group in groups)
        {
            if (limit > -1 && flat.Count >= limit)
            {
                break;
            }

            int remaining = limit > -1 ? limit - flat.Count : int.MaxValue;
            List<TValue> taken = [];
            foreach (TValue item in group.Items)
            {
                if (taken.Count >= remaining)
                {
                    break;
                }

                if (inputs.FilterQuery.Length == 0 || inputs.Filter.Matches(item, inputs.FilterQuery))
                {
                    taken.Add(item);
                }
            }

            if (taken.Count > 0)
            {
                kept.Add(group with { Items = taken });
                flat.AddRange(taken);
            }
        }

        return new(flat, kept);
    }

    private static IReadOnlyList<TValue> Flatten<TValue>(IReadOnlyList<ComboboxItemGroup<TValue>> groups)
    {
        List<TValue> flat = [];
        foreach (ComboboxItemGroup<TValue> group in groups)
        {
            flat.AddRange(group.Items);
        }

        return flat;
    }
}

/// <summary>The inputs of a filtered-items derivation.</summary>
/// <typeparam name="TValue">The type of the items' values.</typeparam>
/// <param name="Items">The flat items, or <see langword="null"/>.</param>
/// <param name="Groups">The grouped items, or <see langword="null"/>.</param>
/// <param name="FilteredItems">The externally filtered flat items, or <see langword="null"/>.</param>
/// <param name="FilteredGroups">The externally filtered groups, or <see langword="null"/>.</param>
/// <param name="IgnoreExternal">Whether a bypass ignores the external result.</param>
/// <param name="FilterQuery">The effective filter query: the empty string when bypassed.</param>
/// <param name="Limit">The most items kept; <c>-1</c> keeps all.</param>
/// <param name="Filter">The predicate.</param>
internal readonly record struct ComboboxFilterInputs<TValue>(
    IReadOnlyList<TValue>? Items,
    IReadOnlyList<ComboboxItemGroup<TValue>>? Groups,
    IReadOnlyList<TValue>? FilteredItems,
    IReadOnlyList<ComboboxItemGroup<TValue>>? FilteredGroups,
    bool IgnoreExternal,
    string FilterQuery,
    int Limit,
    ComboboxItemPredicate<TValue> Filter);

/// <summary>The result of a filtered-items derivation.</summary>
/// <typeparam name="TValue">The type of the items' values.</typeparam>
/// <param name="Flat">The filtered items across groups, in order.</param>
/// <param name="Groups">The groups that kept items, with their filtered items, or <see langword="null"/> for flat items.</param>
internal readonly record struct ComboboxFilteredItems<TValue>(IReadOnlyList<TValue> Flat, IReadOnlyList<ComboboxItemGroup<TValue>>? Groups);

// AriaCombobox.tsx:300-311: a supplied null filter keeps every item; a supplied filter is called with the item, the query
// and the label converter; the default is createCollatorItemFilter (index.ts:23-36: a null item never matches).
/// <summary>The predicate a derivation applies to each item.</summary>
/// <typeparam name="TValue">The type of the items' values.</typeparam>
internal readonly struct ComboboxItemPredicate<TValue>
{
    private readonly Func<TValue, string, Func<TValue, string>?, bool>? custom;
    private readonly CollatorFilter collator;
    private readonly Func<TValue, string>? itemToString;
    private readonly bool off;

    private ComboboxItemPredicate(Func<TValue, string, Func<TValue, string>?, bool>? custom, CollatorFilter collator, Func<TValue, string>? itemToString, bool off)
    {
        this.custom = custom;
        this.collator = collator;
        this.itemToString = itemToString;
        this.off = off;
    }

    /// <summary>Gets the predicate that keeps every item, Base UI's <c>filter={null}</c>.</summary>
    public static ComboboxItemPredicate<TValue> Off => new(null, default, null, off: true);

    /// <summary>Creates the default predicate: the collator's <c>contains</c> over the item's label, never matching a null item.</summary>
    /// <param name="collator">The collator filter of the root's culture.</param>
    /// <param name="itemToString">The label converter, or <see langword="null"/> for the invariant text.</param>
    /// <returns>The predicate.</returns>
    public static ComboboxItemPredicate<TValue> Collator(CollatorFilter collator, Func<TValue, string>? itemToString) => new(null, collator, itemToString, off: false);

    /// <summary>Creates a predicate over the consumer's filter.</summary>
    /// <param name="filter">The consumer's filter.</param>
    /// <param name="itemToString">The label converter passed to it.</param>
    /// <returns>The predicate.</returns>
    public static ComboboxItemPredicate<TValue> Custom(Func<TValue, string, Func<TValue, string>?, bool> filter, Func<TValue, string>? itemToString) =>
        new(filter, default, itemToString, off: false);

    /// <summary>Determines whether an item passes the filter.</summary>
    /// <param name="item">The item.</param>
    /// <param name="query">The non-empty filter query.</param>
    /// <returns><see langword="true"/> when the item is kept.</returns>
    public bool Matches(TValue item, string query)
    {
        if (off)
        {
            return true;
        }

        if (custom is not null)
        {
            return custom(item, query, itemToString);
        }

        return !ValueNull.IsNull(item) && collator.Contains(item, query, itemToString);
    }
}

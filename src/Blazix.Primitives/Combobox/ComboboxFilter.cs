using System.Globalization;
using Blazix.Primitives.Utilities;

namespace Blazix.Primitives;

// CMB-API-65 (combobox/root/utils/useFilter.ts:14-46, root/utils/index.ts:23-68): Base UI's Combobox.useFilter, named by
// M5 track 10 (its dated ADR 08 line). A value over the shared collator filter (UTL-I18N-08..13): equal options give equal
// filters, Base UI's referential stability. Its Contains is the Combobox-aware matcher; StartsWith and EndsWith are the
// shared ones. The root's default filter is the multiple-mode matcher in every mode (CMB-API-14); this type is a consumer
// utility, for a filter that keeps every item visible while the query still mirrors the selection.
/// <summary>Creates the matchers a combobox's <c>Filter</c> and a consumer's own filtering use.</summary>
public static class ComboboxFilter
{
    /// <summary>Creates matchers for items of <typeparamref name="TValue"/> with no selected value.</summary>
    /// <typeparam name="TValue">The type of the items.</typeparam>
    /// <param name="multiple">Whether the combobox keeps several values; then <see cref="ComboboxFilter{TValue}.Contains"/> never shows every item.</param>
    /// <param name="culture">The culture of the comparison, or <see langword="null"/> for the current culture.</param>
    /// <param name="options">The comparison options, which replace the default (case, marks, symbols, kana type and width ignored).</param>
    /// <returns>The matchers.</returns>
    public static ComboboxFilter<TValue> Create<TValue>(bool multiple = false, CultureInfo? culture = null, CompareOptions? options = null) =>
        new(CollatorFilter.Create(culture, options), multiple, null);

    /// <summary>Creates matchers for items of <typeparamref name="TValue"/> that keep every item visible while the query is the selected value's label.</summary>
    /// <typeparam name="TValue">The type of the items.</typeparam>
    /// <param name="value">The selected value, or <see langword="null"/> for none.</param>
    /// <param name="multiple">Whether the combobox keeps several values; then the selected value is not consulted.</param>
    /// <param name="culture">The culture of the comparison, or <see langword="null"/> for the current culture.</param>
    /// <param name="options">The comparison options, which replace the default (case, marks, symbols, kana type and width ignored).</param>
    /// <returns>The matchers.</returns>
    public static ComboboxFilter<TValue> Create<TValue>(TValue? value, bool multiple = false, CultureInfo? culture = null, CompareOptions? options = null) =>
        new(CollatorFilter.Create(culture, options), multiple, new ComboboxFilterValue<TValue>(value));
}

/// <summary>The matchers of a combobox filter, created by <see cref="ComboboxFilter"/>.</summary>
/// <typeparam name="TValue">The type of the items.</typeparam>
/// <remarks>
/// Pass <see cref="Contains"/> as a combobox root's <c>Filter</c>: <c>Filter="@filter.Contains"</c>. Two filters created
/// with equal arguments are equal.
/// </remarks>
public readonly struct ComboboxFilter<TValue> : IEquatable<ComboboxFilter<TValue>>
{
    private readonly CollatorFilter collator;
    private readonly bool multiple;
    private readonly ComboboxFilterValue<TValue>? selected;

    internal ComboboxFilter(CollatorFilter collator, bool multiple, ComboboxFilterValue<TValue>? selected)
    {
        this.collator = collator;
        this.multiple = multiple;
        this.selected = selected;
    }

    /// <summary>Determines whether two filters are equal.</summary>
    /// <param name="left">The first filter.</param>
    /// <param name="right">The second filter.</param>
    /// <returns><see langword="true"/> when both were created with equal arguments.</returns>
    public static bool operator ==(ComboboxFilter<TValue> left, ComboboxFilter<TValue> right) => left.Equals(right);

    /// <summary>Determines whether two filters differ.</summary>
    /// <param name="left">The first filter.</param>
    /// <param name="right">The second filter.</param>
    /// <returns><see langword="true"/> when the filters were created with different arguments.</returns>
    public static bool operator !=(ComboboxFilter<TValue> left, ComboboxFilter<TValue> right) => !left.Equals(right);

    // index.ts:23-68: multiple mode is the collator item filter; single mode adds the empty query and the selected label.
    /// <summary>Gets a value indicating whether an item's text contains the query.</summary>
    /// <param name="item">The item; <see langword="null"/> never matches.</param>
    /// <param name="query">The query.</param>
    /// <param name="itemToString">The item's text, or <see langword="null"/> for its invariant text.</param>
    /// <returns>
    /// <see langword="true"/> when the item's text contains <paramref name="query"/>; without <c>multiple</c>, also for an
    /// empty query and while the selected value's label is the query, compared the same way and of the same length.
    /// </returns>
    public bool Contains(TValue item, string query, Func<TValue, string>? itemToString = null)
    {
        if (ValueNull.IsNull(item))
        {
            return false;
        }

        if (!multiple)
        {
            if (query.Length == 0)
            {
                return true;
            }

            if (selected is { } value && !ValueNull.IsNull(value.Value))
            {
                string selectedLabel = ValueLabels.StringifyAsLabel<TValue>(value.Value!, itemToString);
                if (selectedLabel.Length == query.Length && selectedLabel.Length != 0 && collator.Contains(selectedLabel, query))
                {
                    return true;
                }
            }
        }

        return collator.Contains(item, query, itemToString);
    }

    /// <summary>Gets a value indicating whether an item's text starts with the query.</summary>
    /// <param name="item">The item.</param>
    /// <param name="query">The query; an empty query matches every item.</param>
    /// <param name="itemToString">The item's text, or <see langword="null"/> for its invariant text.</param>
    /// <returns><see langword="true"/> when the item's text starts with <paramref name="query"/>.</returns>
    public bool StartsWith(TValue item, string query, Func<TValue, string>? itemToString = null) => collator.StartsWith(item, query, itemToString);

    /// <summary>Gets a value indicating whether an item's text ends with the query.</summary>
    /// <param name="item">The item.</param>
    /// <param name="query">The query; an empty query matches every item.</param>
    /// <param name="itemToString">The item's text, or <see langword="null"/> for its invariant text.</param>
    /// <returns><see langword="true"/> when the item's text ends with <paramref name="query"/>.</returns>
    public bool EndsWith(TValue item, string query, Func<TValue, string>? itemToString = null) => collator.EndsWith(item, query, itemToString);

    /// <inheritdoc />
    public bool Equals(ComboboxFilter<TValue> other) =>
        collator == other.collator && multiple == other.multiple && Nullable.Equals(selected, other.selected);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is ComboboxFilter<TValue> other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(collator, multiple);
}

// The selected value a single-mode filter consults, compared with Object.is (the item-equality default) for the filter's
// equality, so equal arguments give equal filters (useFilter.ts:33-41's memo inputs).
/// <summary>Holds the selected value a single-mode filter consults.</summary>
/// <typeparam name="TValue">The type of the value.</typeparam>
/// <param name="Value">The selected value.</param>
internal readonly record struct ComboboxFilterValue<TValue>(TValue? Value)
{
    /// <inheritdoc />
    public bool Equals(ComboboxFilterValue<TValue> other) => Store.Equality.Is(Value, other.Value);

    /// <inheritdoc />
    public override int GetHashCode() => 0;
}

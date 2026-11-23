using System.Globalization;
using Blazix.Primitives.Utilities;

namespace Blazix.Primitives;

// AUT-API-19 (autocomplete/index.parts.ts:24, combobox/root/utils/useFilter.ts:12, internals/filter.ts:6-83): Base UI's
// Autocomplete.useFilter, the shared collator filter factory with no selection awareness, named by M5 track 11 (its dated
// ADR 08 line; ComboboxFilter is the precedent). A value over the shared collator filter (UTL-I18N-08..13): equal culture
// and options give equal filters, Base UI's cached object per locale and options.
/// <summary>Creates the matchers an autocomplete's <c>Filter</c> and a consumer's own filtering use.</summary>
public static class AutocompleteFilter
{
    /// <summary>Creates matchers for items of <typeparamref name="TValue"/>.</summary>
    /// <typeparam name="TValue">The type of the items.</typeparam>
    /// <param name="culture">The culture of the comparison, or <see langword="null"/> for the current culture.</param>
    /// <param name="options">The comparison options, which replace the default (case, marks, symbols, kana type and width ignored).</param>
    /// <returns>The matchers.</returns>
    public static AutocompleteFilter<TValue> Create<TValue>(CultureInfo? culture = null, CompareOptions? options = null) =>
        new(CollatorFilter.Create(culture, options));
}

/// <summary>The matchers of an autocomplete filter, created by <see cref="AutocompleteFilter"/>.</summary>
/// <typeparam name="TValue">The type of the items.</typeparam>
/// <remarks>
/// Pass <see cref="Contains"/> as an autocomplete root's <c>Filter</c>: <c>Filter="@filter.Contains"</c>. Two filters created
/// with equal arguments are equal.
/// </remarks>
public readonly struct AutocompleteFilter<TValue> : IEquatable<AutocompleteFilter<TValue>>
{
    private readonly CollatorFilter collator;

    internal AutocompleteFilter(CollatorFilter collator) => this.collator = collator;

    /// <summary>Determines whether two filters are equal.</summary>
    /// <param name="left">The first filter.</param>
    /// <param name="right">The second filter.</param>
    /// <returns><see langword="true"/> when both were created with equal arguments.</returns>
    public static bool operator ==(AutocompleteFilter<TValue> left, AutocompleteFilter<TValue> right) => left.Equals(right);

    /// <summary>Determines whether two filters differ.</summary>
    /// <param name="left">The first filter.</param>
    /// <param name="right">The second filter.</param>
    /// <returns><see langword="true"/> when the filters were created with different arguments.</returns>
    public static bool operator !=(AutocompleteFilter<TValue> left, AutocompleteFilter<TValue> right) => !left.Equals(right);

    /// <summary>Gets a value indicating whether an item's text contains the query.</summary>
    /// <param name="item">The item.</param>
    /// <param name="query">The query; an empty query matches every item.</param>
    /// <param name="itemToString">The item's text, or <see langword="null"/> for its invariant text.</param>
    /// <returns><see langword="true"/> when the item's text contains <paramref name="query"/>.</returns>
    public bool Contains(TValue item, string query, Func<TValue, string>? itemToString = null) => collator.Contains(item, query, itemToString);

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
    public bool Equals(AutocompleteFilter<TValue> other) => collator.Equals(other.collator);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is AutocompleteFilter<TValue> other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => collator.GetHashCode();
}

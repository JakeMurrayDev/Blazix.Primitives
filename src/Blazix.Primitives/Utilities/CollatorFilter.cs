using System.Globalization;

namespace Blazix.Primitives.Utilities;

// UTL-I18N-08..12, UTL-ALG-05 (internals/filter.ts; chosen by the user 2026-11-03, plan decision 10 (a)): Base UI's
// Intl.Collator(locale, { usage: 'search', sensitivity: 'base', ignorePunctuation: true }) is CompareInfo.Compare with
// IgnoreCase | IgnoreNonSpace | IgnoreSymbols | IgnoreKanaType | IgnoreWidth (ICU's `base` strength ignores width and kana
// type too; the last two joined on the collation table's evidence, design point 3 of M5 track 4, settled by the orchestrator
// 2026-11-04; ADR 01 decision 2: equivalent, not byte-identical, with its note of 2026-11-04). The window is Base UI's: query.Length UTF-16 code units (UTL §10 item 6). Base UI caches a
// filter per locale and options; here the filter is a value over the CompareInfo .NET already caches, so equal inputs give
// equal filters and nothing is cached here (design point 3 of M5 track 4, settled by the orchestrator 2026-11-04).
/// <summary>Matches a query against an item's text the way a list's filter does, under a culture's comparison.</summary>
internal readonly record struct CollatorFilter
{
    /// <summary>Specifies the comparison of Base UI's default collator: case, non-spacing marks, symbols, kana type and width are ignored.</summary>
    public const CompareOptions DefaultOptions = CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace | CompareOptions.IgnoreSymbols | CompareOptions.IgnoreKanaType | CompareOptions.IgnoreWidth;

    private CollatorFilter(CompareInfo compareInfo, CompareOptions options)
    {
        CompareInfo = compareInfo;
        Options = options;
    }

    /// <summary>Gets the comparison of the filter's culture.</summary>
    public CompareInfo CompareInfo { get; }

    /// <summary>Gets the options every comparison uses.</summary>
    public CompareOptions Options { get; }

    /// <summary>Creates a filter for a culture.</summary>
    /// <param name="culture">The culture, or <see langword="null"/> for <see cref="CultureInfo.CurrentCulture"/>.</param>
    /// <param name="options">The options, which replace <see cref="DefaultOptions"/> whole; <see langword="null"/> for the default.</param>
    /// <returns>The filter.</returns>
    public static CollatorFilter Create(CultureInfo? culture = null, CompareOptions? options = null) =>
        new((culture ?? CultureInfo.CurrentCulture).CompareInfo, options ?? DefaultOptions);

    /// <summary>Gets a value indicating whether some window of the query's length in the text compares equal to the query.</summary>
    /// <param name="text">The item's text.</param>
    /// <param name="query">The query; an empty query matches every text.</param>
    /// <returns><see langword="true"/> when a window of <paramref name="query"/>'s length compares equal to it.</returns>
    public bool Contains(ReadOnlySpan<char> text, ReadOnlySpan<char> query)
    {
        if (query.IsEmpty)
        {
            return true;
        }

        for (int i = 0; i <= text.Length - query.Length; i++)
        {
            if (CompareInfo.Compare(text.Slice(i, query.Length), query, Options) == 0)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Gets a value indicating whether the text's first code units, as many as the query has, compare equal to the query.</summary>
    /// <param name="text">The item's text.</param>
    /// <param name="query">The query; an empty query matches every text.</param>
    /// <returns><see langword="true"/> when the text's start compares equal to <paramref name="query"/>.</returns>
    public bool StartsWith(ReadOnlySpan<char> text, ReadOnlySpan<char> query) =>
        query.IsEmpty || CompareInfo.Compare(text[..Math.Min(text.Length, query.Length)], query, Options) == 0;

    /// <summary>Gets a value indicating whether the text is at least the query's length and its last code units compare equal to the query.</summary>
    /// <param name="text">The item's text.</param>
    /// <param name="query">The query; an empty query matches every text.</param>
    /// <returns><see langword="true"/> when the text's end compares equal to <paramref name="query"/>.</returns>
    public bool EndsWith(ReadOnlySpan<char> text, ReadOnlySpan<char> query) =>
        query.IsEmpty || (text.Length >= query.Length && CompareInfo.Compare(text[^query.Length..], query, Options) == 0);

    // UTL-I18N-13: an item's text is its stringified label.
    /// <summary>Gets a value indicating whether some window of the query's length in an item's text compares equal to the query.</summary>
    /// <typeparam name="TItem">The type of the item.</typeparam>
    /// <param name="item">The item.</param>
    /// <param name="query">The query; an empty query matches every item.</param>
    /// <param name="itemToString">The item's text, or <see langword="null"/> for its invariant text.</param>
    /// <returns><see langword="true"/> when the item's text contains the query.</returns>
    public bool Contains<TItem>(TItem item, string query, Func<TItem, string>? itemToString) =>
        query.Length == 0 || Contains(ValueLabels.StringifyAsLabel(item, itemToString), query);

    /// <summary>Gets a value indicating whether an item's text starts with the query.</summary>
    /// <typeparam name="TItem">The type of the item.</typeparam>
    /// <param name="item">The item.</param>
    /// <param name="query">The query; an empty query matches every item.</param>
    /// <param name="itemToString">The item's text, or <see langword="null"/> for its invariant text.</param>
    /// <returns><see langword="true"/> when the item's text starts with the query.</returns>
    public bool StartsWith<TItem>(TItem item, string query, Func<TItem, string>? itemToString) =>
        query.Length == 0 || StartsWith(ValueLabels.StringifyAsLabel(item, itemToString), query);

    /// <summary>Gets a value indicating whether an item's text ends with the query.</summary>
    /// <typeparam name="TItem">The type of the item.</typeparam>
    /// <param name="item">The item.</param>
    /// <param name="query">The query; an empty query matches every item.</param>
    /// <param name="itemToString">The item's text, or <see langword="null"/> for its invariant text.</param>
    /// <returns><see langword="true"/> when the item's text ends with the query.</returns>
    public bool EndsWith<TItem>(TItem item, string query, Func<TItem, string>? itemToString) =>
        query.Length == 0 || EndsWith(ValueLabels.StringifyAsLabel(item, itemToString), query);
}

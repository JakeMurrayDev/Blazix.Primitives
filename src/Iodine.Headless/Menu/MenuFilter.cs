using System.Globalization;
using Blazix.Primitives.Utilities;

namespace Blazix.Primitives;

// MNU-API-67 (menu/index.parts.ts:28, internals/filter.ts:6-84): Base UI's Menu.useFilter, the shared getFilter. M5 track
// 12's design point 4 (a), settled by the orchestrator 2026-11-11: a value over the shared collator filter (UTL-I18N-08..13), track 10's
// ComboboxFilter without its type parameter, since a menu item's text is a string. Equal arguments give equal filters,
// Base UI's referential stability.
/// <summary>
/// Represents locale-aware matchers for filtering text: the default matcher of <see cref="MenuFilterProvider"/> and a
/// consumer's own filtering.
/// </summary>
/// <remarks>
/// Pass <see cref="Contains"/> as a provider's <c>Filter</c>: <c>Filter="@filter.Contains"</c>. Two filters created with
/// equal arguments are equal.
/// </remarks>
public readonly struct MenuFilter : IEquatable<MenuFilter>
{
    private readonly CollatorFilter collator;

    private MenuFilter(CollatorFilter collator) => this.collator = collator;

    // The default instance compares as Create() does, under the current culture.
    private CollatorFilter Collator => collator.CompareInfo is null ? CollatorFilter.Create() : collator;

    /// <summary>Creates matchers that ignore case, marks, symbols, kana type and width unless <paramref name="options"/> says otherwise.</summary>
    /// <param name="culture">The culture of the comparison, or <see langword="null"/> for the current culture.</param>
    /// <param name="options">The comparison options, which replace the default (case, marks, symbols, kana type and width ignored).</param>
    /// <returns>The matchers.</returns>
    public static MenuFilter Create(CultureInfo? culture = null, CompareOptions? options = null) => new(CollatorFilter.Create(culture, options));

    /// <summary>Determines whether two filters are equal.</summary>
    /// <param name="left">The first filter.</param>
    /// <param name="right">The second filter.</param>
    /// <returns><see langword="true"/> when both were created with equal arguments.</returns>
    public static bool operator ==(MenuFilter left, MenuFilter right) => left.Equals(right);

    /// <summary>Determines whether two filters differ.</summary>
    /// <param name="left">The first filter.</param>
    /// <param name="right">The second filter.</param>
    /// <returns><see langword="true"/> when the filters were created with different arguments.</returns>
    public static bool operator !=(MenuFilter left, MenuFilter right) => !left.Equals(right);

    /// <summary>Determines whether the text contains the query anywhere.</summary>
    /// <param name="text">The item's text.</param>
    /// <param name="query">The query.</param>
    /// <returns><see langword="true"/> when the text matches; an empty query matches every text.</returns>
    public bool Contains(string text, string query) => Collator.Contains(text, query);

    /// <summary>Determines whether the text starts with the query.</summary>
    /// <param name="text">The item's text.</param>
    /// <param name="query">The query.</param>
    /// <returns><see langword="true"/> when the text matches; an empty query matches every text.</returns>
    public bool StartsWith(string text, string query) => Collator.StartsWith(text, query);

    /// <summary>Determines whether the text ends with the query.</summary>
    /// <param name="text">The item's text.</param>
    /// <param name="query">The query.</param>
    /// <returns><see langword="true"/> when the text matches; an empty query matches every text.</returns>
    public bool EndsWith(string text, string query) => Collator.EndsWith(text, query);

    /// <inheritdoc />
    public bool Equals(MenuFilter other) => Collator.Equals(other.Collator);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is MenuFilter other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => Collator.GetHashCode();
}

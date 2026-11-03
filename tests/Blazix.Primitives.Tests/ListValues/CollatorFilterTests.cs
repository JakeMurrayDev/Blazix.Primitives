using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Blazix.Primitives.Utilities;

namespace Blazix.Primitives.Tests.ListValues;

// UTL-I18N-08..12, UTL-ALG-05 (internals/filter.ts; chosen by the user 2026-11-03, plan decision 10 (a)): the collator filter
// over CompareInfo, with Base UI's window of query.Length UTF-16 code units. filter.test.ts's three titles, the cache titles
// restated as equal and unequal filters (a value type over the CompareInfo .NET caches), and the L1 collation table, whose
// expected column Intl.Collator produced (Data/collation-table.mjs). CI runs under the invariant culture, so each title names
// its culture.
/// <summary>The collator filter Combobox, Autocomplete and Menu's filter mode share.</summary>
public sealed class CollatorFilterTests
{
    [Fact(DisplayName = "caches different locales separately")]
    [Trait("Requirement", "UTL-I18N-08")]
    public void Caches_different_locales_separately()
    {
        CollatorFilter filter1 = CollatorFilter.Create(new CultureInfo("fr-FR"));
        CollatorFilter filter2 = CollatorFilter.Create(new CultureInfo("en-US"));

        Assert.NotEqual(filter1, filter2);
    }

    [Fact(DisplayName = "caches equivalent locale inputs")]
    [Trait("Requirement", "UTL-I18N-08")]
    public void Caches_equivalent_locale_inputs()
    {
        CollatorFilter filter1 = CollatorFilter.Create(new CultureInfo("fr-FR"));
        CollatorFilter filter2 = CollatorFilter.Create(CultureInfo.GetCultureInfo("fr-FR"));

        Assert.Equal(filter1, filter2);
    }

    [Fact(DisplayName = "matches locale-aware substrings without accent sensitivity")]
    [Trait("Requirement", "UTL-I18N-09")]
    public void Matches_locale_aware_substrings_without_accent_sensitivity()
    {
        CollatorFilter filter = CollatorFilter.Create(new CultureInfo("en-US"));

        Assert.True(filter.Contains("Résumé", "resume"));
    }

    [Fact(DisplayName = "port: startsWith and endsWith compare the first and the last query.length code units, and an empty query matches every item")]
    [Trait("Requirement", "UTL-I18N-10")]
    [Trait("Requirement", "UTL-I18N-11")]
    [Trait("Requirement", "UTL-I18N-12")]
    public void StartsWith_and_endsWith_compare_the_first_and_last_code_units_and_an_empty_query_matches()
    {
        CollatorFilter filter = CollatorFilter.Create(new CultureInfo("en-US"));

        Assert.True(filter.StartsWith("Apple pie", "APPLE"));
        Assert.False(filter.StartsWith("Apple pie", "pie"));
        Assert.True(filter.EndsWith("Apple pie", "PIE"));
        Assert.False(filter.EndsWith("pie", "apple pie"));
        Assert.True(filter.Contains("Apple pie", string.Empty));
        Assert.True(filter.StartsWith(string.Empty, string.Empty));
        Assert.True(filter.EndsWith("Apple pie", string.Empty));
    }

    [Fact(DisplayName = "port: the default comparison ignores case, non-spacing marks and symbols, and caller options replace it")]
    [Trait("Requirement", "UTL-I18N-08")]
    public void The_default_comparison_ignores_case_marks_and_symbols_and_caller_options_replace_it()
    {
        CultureInfo culture = new("en-US");

        Assert.Equal(CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace | CompareOptions.IgnoreSymbols, CollatorFilter.Create(culture).Options);
        Assert.False(CollatorFilter.Create(culture, CompareOptions.None).Contains("Résumé", "resume"));
        Assert.NotEqual(CollatorFilter.Create(culture), CollatorFilter.Create(culture, CompareOptions.IgnoreCase));
    }

    [Theory(DisplayName = "port: the collation table row agrees with Intl.Collator")]
    [MemberData(nameof(Rows))]
    [Trait("Requirement", "UTL-I18N-09")]
    [Trait("Requirement", "UTL-ALG-05")]
    public void The_collation_table_row_agrees_with_Intl_Collator(string locale, string text, string query, string rowCase, bool contains, bool startsWith, bool endsWith)
    {
        CollatorFilter filter = CollatorFilter.Create(new CultureInfo(locale));
        (bool Contains, bool StartsWith, bool EndsWith) expected = s_divergences.TryGetValue(rowCase, out var net) ? net : (contains, startsWith, endsWith);

        Assert.Equal(expected, (filter.Contains(text, query), filter.StartsWith(text, query), filter.EndsWith(text, query)));
    }

    public static TheoryData<string, string, string, string, bool, bool, bool> Rows()
    {
        TheoryData<string, string, string, string, bool, bool, bool> data = [];
        using JsonDocument table = JsonDocument.Parse(File.ReadAllText(TablePath()));
        foreach (JsonElement row in table.RootElement.GetProperty("rows").EnumerateArray())
        {
            data.Add(row.GetProperty("locale").GetString()!, row.GetProperty("text").GetString()!, row.GetProperty("query").GetString()!, row.GetProperty("case").GetString()!, row.GetProperty("contains").GetBoolean(), row.GetProperty("startsWith").GetBoolean(), row.GetProperty("endsWith").GetBoolean());
        }

        return data;
    }

    // The rows where .NET's CompareInfo answers otherwise than Intl.Collator, each listed in ADR 01's note under decision 2
    // (equivalent, not byte-identical): what .NET answers.
    private static readonly Dictionary<string, (bool, bool, bool)> s_divergences = new(StringComparer.Ordinal);

    private static string TablePath([CallerFilePath] string source = "") => Path.Combine(Path.GetDirectoryName(source)!, "Data", "collation-table.json");
}

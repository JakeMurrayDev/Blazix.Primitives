using System.Globalization;

// Family tests use the project's root namespace.
namespace Blazix.Primitives.Tests;

// AUT-API-19 (autocomplete/index.parts.ts:24, combobox/root/utils/useFilter.ts:12, internals/filter.ts:6-83) and AUT-API-10's
// collator (Test: none in autocomplete/; the brief's L1 proposal, M5 track 11): Autocomplete.useFilter is the shared collator
// filter factory itself, with no selection awareness, as the public AutocompleteFilter over the shared collator filter
// (UTL-I18N-08..13). CI runs under the invariant culture, so each title names its culture.
/// <summary>Autocomplete's filter factory at L1.</summary>
public sealed class AutocompleteFilterTests
{
    private static readonly CultureInfo s_english = new("en-US");

    [Fact(DisplayName = "port: the filter matches contains, starts and ends with under the default collator options")]
    [Trait("Requirement", "AUT-API-19")]
    public void Matches_contains_starts_and_ends_with_under_the_default_options()
    {
        AutocompleteFilter<string> filter = AutocompleteFilter.Create<string>(s_english);

        Assert.True(filter.Contains("Apple", "app"));
        Assert.True(filter.Contains("Crème brûlée", "BRULEE"));
        Assert.True(filter.Contains("Apple", ""));
        Assert.False(filter.Contains("Banana", "app"));
        Assert.True(filter.StartsWith("Éclair", "ec"));
        Assert.False(filter.StartsWith("Éclair", "clair"));
        Assert.True(filter.EndsWith("Crème brûlée", "brulee"));
        Assert.False(filter.EndsWith("Crème brûlée", "crème"));
    }

    [Fact(DisplayName = "port: the filter reads an item's text through itemToString")]
    [Trait("Requirement", "AUT-API-19")]
    public void Reads_an_items_text_through_item_to_string()
    {
        AutocompleteFilter<Tag> filter = AutocompleteFilter.Create<Tag>(s_english);
        Tag tag = new("t1", "feature");

        Assert.True(filter.Contains(tag, "feat", static t => t.Value));
        Assert.True(filter.StartsWith(tag, "feat", static t => t.Value));
        Assert.True(filter.EndsWith(tag, "ture", static t => t.Value));
        Assert.False(filter.Contains(tag, "t1", static t => t.Value));
    }

    [Fact(DisplayName = "port: equal culture and options give equal filters, and different ones differ")]
    [Trait("Requirement", "AUT-API-19")]
    public void Equal_culture_and_options_give_equal_filters()
    {
        AutocompleteFilter<string> first = AutocompleteFilter.Create<string>(new CultureInfo("en-US"));
        AutocompleteFilter<string> second = AutocompleteFilter.Create<string>(new CultureInfo("en-US"));
        AutocompleteFilter<string> french = AutocompleteFilter.Create<string>(new CultureInfo("fr-FR"));
        AutocompleteFilter<string> exact = AutocompleteFilter.Create<string>(new CultureInfo("en-US"), CompareOptions.None);

        Assert.Equal(first, second);
        Assert.True(first == second);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
        Assert.NotEqual(first, french);
        Assert.True(first != exact);
    }

    [Fact(DisplayName = "port: caller options replace the default collator options")]
    [Trait("Requirement", "AUT-API-19")]
    public void Caller_options_replace_the_default_options()
    {
        AutocompleteFilter<string> exact = AutocompleteFilter.Create<string>(s_english, CompareOptions.None);

        Assert.True(exact.Contains("Apple", "App"));
        Assert.False(exact.Contains("Apple", "app"));
    }

    [Fact(DisplayName = "port: with the Turkish culture, i matches İzmir and not Isparta")]
    [Trait("Requirement", "AUT-API-10")]
    [Trait("Requirement", "AUT-API-19")]
    public void Turkish_i_matches_dotted_capital_i()
    {
        AutocompleteFilter<string> turkish = AutocompleteFilter.Create<string>(new CultureInfo("tr-TR"));

        Assert.True(turkish.StartsWith("İzmir", "i"));
        Assert.False(turkish.StartsWith("Isparta", "i"));
    }

    [Fact(DisplayName = "port: the filter keeps no selected value, so a query equal to an item's text filters as any other")]
    [Trait("Requirement", "AUT-API-19")]
    public void Keeps_no_selected_value()
    {
        AutocompleteFilter<string> filter = AutocompleteFilter.Create<string>(s_english);

        Assert.True(filter.Contains("Apple", "Apple"));
        Assert.False(filter.Contains("Banana", "Apple"));
        Assert.DoesNotContain(
            typeof(AutocompleteFilter).GetMethods(),
            method => method.Name == nameof(AutocompleteFilter.Create) && method.GetParameters().Length > 2);
    }

    private sealed record Tag(string Id, string Value);
}

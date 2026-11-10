using System.Globalization;

// Family tests use the project's root namespace (a test namespace is never named like a part).
namespace Blazix.Primitives.Tests;

/// <summary>
/// L1: <see cref="MenuFilter"/>, Base UI's <c>Menu.useFilter</c> (MNU-API-67, <c>Test: none</c> upstream; M5 track 12's
/// design point 4 (a)), over the shared collator filter (UTL-I18N-08..13 also proven through it).
/// </summary>
public sealed class MenuFilterTests
{
    private static readonly CultureInfo s_english = CultureInfo.GetCultureInfo("en-US");

    [Fact(DisplayName = "port: contains, startsWith and endsWith ignore case, marks and punctuation under the culture")]
    [Trait("Requirement", "MNU-API-67")]
    public void Contains_startsWith_and_endsWith_ignore_case_marks_and_punctuation()
    {
        MenuFilter filter = MenuFilter.Create(s_english);

        Assert.True(filter.Contains("Crème brûlée", "BRULEE"));
        Assert.True(filter.Contains("Save as…", "save as"));
        Assert.False(filter.Contains("Apple", "x"));
        Assert.True(filter.StartsWith("Crème brûlée", "creme"));
        Assert.False(filter.StartsWith("Crème brûlée", "brulee"));
        Assert.True(filter.EndsWith("Crème brûlée", "Brûlée"));
        Assert.False(filter.EndsWith("Crème brûlée", "creme"));
    }

    [Fact(DisplayName = "port: an empty query matches every text")]
    [Trait("Requirement", "MNU-API-67")]
    public void An_empty_query_matches_every_text()
    {
        MenuFilter filter = MenuFilter.Create(s_english);

        Assert.True(filter.Contains("Apple", ""));
        Assert.True(filter.StartsWith("Apple", ""));
        Assert.True(filter.EndsWith("", ""));
    }

    [Fact(DisplayName = "port: the culture decides the comparison, so Turkish matches the dotless i")]
    [Trait("Requirement", "MNU-API-67")]
    [Trait("Requirement", "MNU-API-56")]
    public void The_culture_decides_the_comparison()
    {
        MenuFilter turkish = MenuFilter.Create(CultureInfo.GetCultureInfo("tr-TR"));

        Assert.True(turkish.Contains("Istanbul", "ı"));
    }

    [Fact(DisplayName = "port: replacing the options compares exactly, so case counts")]
    [Trait("Requirement", "MNU-API-67")]
    public void Replacing_the_options_compares_exactly()
    {
        MenuFilter exact = MenuFilter.Create(s_english, CompareOptions.None);

        Assert.False(exact.Contains("Apple", "apple"));
        Assert.True(exact.Contains("Apple", "Appl"));
    }

    [Fact(DisplayName = "port: filters created with equal arguments are equal")]
    [Trait("Requirement", "MNU-API-67")]
    public void Filters_created_with_equal_arguments_are_equal()
    {
        MenuFilter first = MenuFilter.Create(s_english);
        MenuFilter second = MenuFilter.Create(CultureInfo.GetCultureInfo("en-US"));
        MenuFilter french = MenuFilter.Create(CultureInfo.GetCultureInfo("fr-FR"));
        MenuFilter exact = MenuFilter.Create(s_english, CompareOptions.None);

        Assert.True(first == second);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
        Assert.True(first != french);
        Assert.False(first.Equals(exact));
        Assert.False(first.Equals((object)"en-US"));
    }
}

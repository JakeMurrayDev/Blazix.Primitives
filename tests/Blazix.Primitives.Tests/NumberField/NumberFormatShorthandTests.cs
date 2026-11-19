using System.Globalization;
using Blazix.Primitives.Utilities;

namespace Blazix.Primitives.Tests;

// ADR 08 Amendments 2026-11-18 (decision 10 (a), chosen by the user 2026-11-18): NumberFieldRoot.Format takes a .NET numeric
// format string as a shorthand for the options record. The standard strings N, F, P and E with their precision, and the
// custom patterns of digit placeholders, grouping, a decimal point, a percent sign and an exponent, convert; the others
// (C, which would need the culture's currency, D, G, R, X and sections) are rejected.
/// <summary>The conversion of a .NET numeric format string to <see cref="NumberFormatOptions"/>.</summary>
public sealed class NumberFormatShorthandTests
{
    private static readonly CultureInfo s_enUs = CultureInfo.GetCultureInfo("en-US");

    [Theory(DisplayName = "port: a standard format string converts to its options")]
    [InlineData("N2", "1,234.50")]
    [InlineData("N0", "1,235")]
    [InlineData("F1", "1234.5")]
    [InlineData("F3", "1234.500")]
    [InlineData("E2", "1.23E3")]
    [Trait("Requirement", "NUM-API-11")]
    public void A_standard_format_string_converts_to_its_options(string format, string expected)
    {
        NumberFormatOptions options = format;

        Assert.Equal(expected, NumberFormatter.Get(s_enUs, options).Format(1234.5));
    }

    [Theory(DisplayName = "port: a custom format pattern converts to its options")]
    [InlineData("#,##0.00", 1234.5, "1,234.50")]
    [InlineData("0.###", 1234.5678, "1234.568")]
    [InlineData("000", 7, "007")]
    [InlineData("#,##0.0#", 1234.567, "1,234.57")]
    [InlineData("0%", 0.256, "26%")]
    [InlineData("0.0%", 0.256, "25.6%")]
    [InlineData("0.00E0", 1234.5, "1.23E3")]
    [Trait("Requirement", "NUM-API-11")]
    public void A_custom_format_pattern_converts_to_its_options(string format, double value, string expected)
    {
        NumberFormatOptions options = format;

        Assert.Equal(expected, NumberFormatter.Get(s_enUs, options).Format(value));
    }

    [Fact(DisplayName = "port: the percent standard format is a percent style with its precision")]
    [Trait("Requirement", "NUM-API-11")]
    public void The_percent_standard_format_is_a_percent_style_with_its_precision()
    {
        NumberFormatOptions options = "P1";

        Assert.Equal(new NumberFormatOptions { Style = NumberFormatStyle.Percent, MinimumFractionDigits = 1, MaximumFractionDigits = 1 }, options);
        Assert.Equal("25.6%", NumberFormatter.Get(s_enUs, options).Format(0.256));
    }

    [Theory(DisplayName = "port: a format string with no equivalent is rejected")]
    [InlineData("C")]
    [InlineData("D3")]
    [InlineData("G")]
    [InlineData("X")]
    [InlineData("#;(#)")]
    [InlineData("")]
    [Trait("Requirement", "NUM-API-11")]
    public void A_format_string_with_no_equivalent_is_rejected(string format)
    {
        Assert.Equal(new NumberFormatOptions { MinimumFractionDigits = 2, MaximumFractionDigits = 2 }, (NumberFormatOptions)"N2");

        ArgumentException error = Assert.Throws<ArgumentException>(() => (NumberFormatOptions)format);
        Assert.StartsWith("Blazix: ", error.Message, StringComparison.Ordinal);
    }
}

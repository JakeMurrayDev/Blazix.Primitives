using System.Globalization;
using Iodine.Headless.Utilities;

namespace Iodine.Headless.Tests;

// NUM-I18N-01, NUM-I18N-07, NUM-I18N-13, UTL-I18N-05..07: port titles for the formatter, each expectation the text
// Intl.NumberFormat writes in a browser (Chromium, ICU 74), except where a comment names the accepted equivalence (ADR 01
// decision 2 as amended: equivalent, not byte-identical). Cultures are set explicitly; CI runs the invariant culture.
/// <summary>The number field's formatter.</summary>
public sealed class NumberFormatterTests
{
    [Theory(DisplayName = "port: formats with Intl.NumberFormat's defaults: grouping and at most three fraction digits")]
    [InlineData("en-US", 1234.5678, "1,234.568")]
    [InlineData("de-DE", 1234.5, "1.234,5")]
    [InlineData("fr-FR", 1234.5, "1{group}234,5")]
    [InlineData("en-IN", 1234567, "12,34,567")]
    [InlineData("pl", 1234, "1234")]
    [InlineData("pl", 12345, "12 345")]
    [InlineData("en-US", 0.00000015, "0")]
    [InlineData("en-US", 1e21, "1,000,000,000,000,000,000,000")]
    [Trait("Requirement", "NUM-I18N-01")]
    public void Formats_with_the_defaults(string culture, double value, string expected)
    {
        // fr-FR's group separator is culture data that changed across CLDR versions (U+00A0, then U+202F), so `{group}` reads
        // it from the host's culture, as the formatter does.
        string group = CultureInfo.GetCultureInfo(culture).NumberFormat.NumberGroupSeparator;
        Assert.Equal(expected.Replace("{group}", group, StringComparison.Ordinal), Format(culture, value, null));
    }

    [Theory(DisplayName = "port: writes the culture's numbering system and signs")]
    [InlineData("ar-EG", -1234.5, "؜-١٬٢٣٤٫٥")]
    [InlineData("fa-IR", -12.5, "‎−۱۲٫۵")]
    [InlineData("en-US", -0.0, "-0")]
    [InlineData("en-US", -0.0001, "-0")]
    [Trait("Requirement", "NUM-I18N-01")]
    public void Writes_the_cultures_numbering_system_and_signs(string culture, double value, string expected) => Assert.Equal(expected, Format(culture, value, null));

    [Fact(DisplayName = "port: formats percent, currency and unit styles")]
    [Trait("Requirement", "NUM-I18N-01")]
    public void Formats_percent_currency_and_unit_styles()
    {
        Assert.Equal("26%", Format("en-US", 0.256, new() { Style = NumberFormatStyle.Percent }));
        Assert.Equal("-30 %", Format("de-DE", -0.3, new() { Style = NumberFormatStyle.Percent }));
        Assert.Equal("-%50", Format("tr-TR", -0.5, new() { Style = NumberFormatStyle.Percent }));
        Assert.Equal("($1.50)", Format("en-US", -1.5, new() { Style = NumberFormatStyle.Currency, Currency = "USD", CurrencySign = NumberFormatCurrencySign.Accounting }));
        Assert.Equal("-$1.50", Format("en-US", -1.5, new() { Style = NumberFormatStyle.Currency, Currency = "USD" }));
        Assert.Equal("12°C", Format("en-US", 12, new() { Style = NumberFormatStyle.Unit, Unit = NumberFormatUnit.Celsius }));
        Assert.Equal("12 %", Format("de-DE", 12, new() { Style = NumberFormatStyle.Unit, Unit = NumberFormatUnit.Percent }));
        Assert.Equal("12 km/h", Format("en-US", 12, new() { Style = NumberFormatStyle.Unit, Unit = NumberFormatUnit.KilometerPerHour }));
    }

    // Equivalences: Intl writes JPY in ja-JP as the fullwidth U+FFE5, USD in fr-FR as "$US" and the name as the plural "euros"
    // after a U+0020; .NET takes the symbol, the separators and the region's currency name from the host's culture data,
    // and the pattern space is U+00A0. The glyphs that culture data supplies are read here, at test time, from the same
    // CultureInfo and RegionInfo the formatter reads, because they differ across ICU versions: macOS's ICU gives ja-JP's
    // symbol as U+00A5 and Linux's as U+FFE5, and fr-FR's group separator is U+202F or U+00A0 by version. The literal
    // parts are the CLDR patterns, which do not vary: the symbol before the digits in ja-JP, after them in fr-FR with a
    // U+00A0, the name after the digits and the code before them in en-US. fr-FR is asserted with its own currency, EUR: a
    // foreign currency's symbol comes from the culture the host's ICU lists first for it (USD's is the first culture named
    // "*-US" in ordinal order, `chr-US` where the host has it), which this title would otherwise depend on.
    [Fact(DisplayName = "port: takes a currency's symbol, digits and name from the culture data")]
    [Trait("Requirement", "NUM-I18N-01")]
    public void Takes_a_currencys_symbol_digits_and_name_from_the_culture_data()
    {
        NumberFormatInfo japanese = CultureInfo.GetCultureInfo("ja-JP").NumberFormat;
        NumberFormatInfo french = CultureInfo.GetCultureInfo("fr-FR").NumberFormat;
        string euro = new RegionInfo("DE").CurrencyEnglishName;

        // JPY is ja-JP's own currency, so its symbol is the culture's, and it has no fraction digits.
        Assert.Equal($"{japanese.CurrencySymbol}1{japanese.CurrencyGroupSeparator}234", Format("ja-JP", 1234, new() { Style = NumberFormatStyle.Currency, Currency = "JPY" }));
        Assert.Equal($"1{french.CurrencyGroupSeparator}234{french.CurrencyDecimalSeparator}50\u00A0{french.CurrencySymbol}", Format("fr-FR", 1234.5, new() { Style = NumberFormatStyle.Currency, Currency = "EUR" }));
        Assert.Equal($"1,234.50\u00A0{euro}", Format("en-US", 1234.5, new() { Style = NumberFormatStyle.Currency, Currency = "EUR", CurrencyDisplay = NumberFormatCurrencyDisplay.Name }));
        Assert.Equal("EUR\u00A01,234.50", Format("en-US", 1234.5, new() { Style = NumberFormatStyle.Currency, Currency = "EUR", CurrencyDisplay = NumberFormatCurrencyDisplay.Code }));
    }

    [Fact(DisplayName = "port: formats scientific and compact notation")]
    [Trait("Requirement", "NUM-I18N-01")]
    public void Formats_scientific_and_compact_notation()
    {
        Assert.Equal("1.235E4", Format("en-US", 12345, new() { Notation = NumberFormatNotation.Scientific }));
        Assert.Equal("١٫٢٣أس٤", Format("ar-EG", 12345, new() { Notation = NumberFormatNotation.Scientific, MaximumFractionDigits = 2 }));
        Assert.Equal("1.2K", Format("en-US", 1234, new() { Notation = NumberFormatNotation.Compact }));
        Assert.Equal("12K", Format("en-US", 12345, new() { Notation = NumberFormatNotation.Compact }));
        Assert.Equal("1M", Format("en-US", 999999, new() { Notation = NumberFormatNotation.Compact }));
    }

    [Fact(DisplayName = "port: displays the sign, the minimum integer digits and the rounding increment")]
    [Trait("Requirement", "NUM-I18N-01")]
    public void Displays_the_sign_the_minimum_integer_digits_and_the_rounding_increment()
    {
        Assert.Equal("+5", Format("en-US", 5, new() { SignDisplay = NumberFormatSignDisplay.ExceptZero }));
        Assert.Equal("0", Format("en-US", 0, new() { SignDisplay = NumberFormatSignDisplay.ExceptZero }));
        Assert.Equal("0", Format("en-US", -0.0001, new() { SignDisplay = NumberFormatSignDisplay.Negative }));
        Assert.Equal("005", Format("en-US", 5, new() { MinimumIntegerDigits = 3 }));
        Assert.Equal("1.5", Format("en-US", 1.26, new() { MinimumFractionDigits = 1, MaximumFractionDigits = 1, RoundingIncrement = 5 }));
    }

    [Fact(DisplayName = "port: rejects options Intl.NumberFormat rejects")]
    [Trait("Requirement", "NUM-I18N-01")]
    public void Rejects_options_Intl_NumberFormat_rejects()
    {
        Assert.Equal("1.5", Format("en-US", 1.5, new() { MaximumFractionDigits = 1 }));
        Assert.StartsWith("Iodine: ", Assert.Throws<ArgumentException>(() => Format("en-US", 1, new() { Style = NumberFormatStyle.Currency })).Message, StringComparison.Ordinal);
        Assert.Throws<ArgumentException>(() => Format("en-US", 1, new() { RoundingIncrement = 3 }));
        Assert.Throws<ArgumentException>(() => Format("en-US", 1, new() { RoundingIncrement = 5, MaximumSignificantDigits = 2 }));
        Assert.Throws<ArgumentException>(() => Format("en-US", 1, new() { MinimumFractionDigits = 3, MaximumFractionDigits = 1 }));
    }

    // UTL-I18N-06 and NUM-I18N-13: one formatter per culture name and equal options; a different culture is a different one.
    [Fact(DisplayName = "port: caches one formatter per culture and equal options")]
    [Trait("Requirement", "NUM-I18N-13")]
    public void Caches_one_formatter_per_culture_and_equal_options()
    {
        CultureInfo deDe = CultureInfo.GetCultureInfo("de-DE");
        NumberFormatter first = NumberFormatter.Get(deDe, new NumberFormatOptions { MaximumFractionDigits = 2 });

        Assert.Same(first, NumberFormatter.Get(CultureInfo.GetCultureInfo("de-DE"), new NumberFormatOptions { MaximumFractionDigits = 2 }));
        Assert.NotSame(first, NumberFormatter.Get(CultureInfo.GetCultureInfo("fr-FR"), new NumberFormatOptions { MaximumFractionDigits = 2 }));
    }

    [Fact(DisplayName = "port: formats null as the empty string")]
    [Trait("Requirement", "NUM-I18N-01")]
    public void Formats_null_as_the_empty_string()
    {
        CultureInfo enUs = CultureInfo.GetCultureInfo("en-US");
        Assert.Equal("1,234", NumberFormatter.FormatNumber(1234, enUs, null));
        Assert.Equal(string.Empty, NumberFormatter.FormatNumber(null, enUs, null));
    }

    private static string Format(string culture, double value, NumberFormatOptions? options) => NumberFormatter.Get(CultureInfo.GetCultureInfo(culture), options).Format(value);
}

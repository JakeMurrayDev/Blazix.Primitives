using System.Globalization;
using Blazix.Primitives.Utilities;

namespace Blazix.Primitives.Tests;

// NUM-I18N-02..12: Base UI's `number-field/utils/parse.test.ts` (54 titles), verbatim over the C# parser. ADR 01 decision 2 as
// amended: the formatter that produces the inputs is the port's, equivalent to Intl.NumberFormat rather than identical, so a
// title that formats with `new Intl.NumberFormat(...)` formats with NumberFormatter here. A call without a locale reads the
// runtime default, which the browser tests run as en-US; it runs under en-US here (CI runs the invariant culture).
/// <summary>The number field's parser.</summary>
public sealed class NumberParserTests
{
    private static readonly CultureInfo s_enUs = CultureInfo.GetCultureInfo("en-US");

    [Fact(DisplayName = "returns the number locale details")]
    [Trait("Requirement", "NUM-I18N-07")]
    public void Returns_the_number_locale_details()
    {
        NumberLocaleDetails details = NumberParser.GetLocaleDetails(s_enUs, null);
        Assert.Equal(".", details.Decimal);
        Assert.Equal(",", details.Group);
        Assert.Null(details.Currency);
        Assert.Null(details.PercentSign);
        Assert.Null(details.Unit);
    }

    [Fact(DisplayName = "parses a number")]
    [Trait("Requirement", "NUM-I18N-02")]
    public void Parses_a_number()
    {
        string numberString = Default(() => NumberFormatter.Get(null, null).Format(1234.56));
        Assert.Equal(1234.56, Parse(numberString));
    }

    [Fact(DisplayName = "parses percentages by default")]
    [Trait("Requirement", "NUM-I18N-05")]
    public void Parses_percentages_by_default() => Assert.Equal(0.12, Parse("12%"));

    // Base UI skips this title unconditionally (`skip()` on its first line, "browser doesn't support Arabic numerals"); the
    // parser has no browser dependency here, so it runs.
    [Fact(DisplayName = "parses a number with Arabic numerals")]
    [Trait("Requirement", "NUM-I18N-03")]
    public void Parses_a_number_with_Arabic_numerals() => Assert.Equal(1234.56, Parse("١٬٢٣٤٫٥٦"));

    [Fact(DisplayName = "parses a number with Han numerals")]
    [Trait("Requirement", "NUM-I18N-03")]
    public void Parses_a_number_with_Han_numerals() => Assert.Equal(1234.56, Parse("一,二三四.五六"));

    [Fact(DisplayName = "parses percentages with Arabic numerals")]
    [Trait("Requirement", "NUM-I18N-05")]
    public void Parses_percentages_with_Arabic_numerals() => Assert.Equal(0.12, Parse("١٢٪"));

    [Fact(DisplayName = "parses percentages with Han numerals")]
    [Trait("Requirement", "NUM-I18N-05")]
    public void Parses_percentages_with_Han_numerals() => Assert.Equal(0.12, Parse("一二%"));

    [Fact(DisplayName = "returns null for an invalid number")]
    [Trait("Requirement", "NUM-I18N-06")]
    public void Returns_null_for_an_invalid_number() => Assert.Null(Parse("invalid"));

    [Fact(DisplayName = "handles percentages with style: \"percent\"")]
    [Trait("Requirement", "NUM-I18N-05")]
    public void Handles_percentages_with_style_percent()
    {
        Assert.Equal(0.12, NumberParser.Parse("12%", s_enUs, new NumberFormatOptions { Style = NumberFormatStyle.Percent }));
    }

    [Fact(DisplayName = "parses prefix percentages")]
    [Trait("Requirement", "NUM-I18N-05")]
    public void Parses_prefix_percentages()
    {
        CultureInfo trTr = CultureInfo.GetCultureInfo("tr-TR");
        var format = new NumberFormatOptions { Style = NumberFormatStyle.Percent, MaximumFractionDigits = 2 };
        string formatted = NumberFormatter.Get(trTr, format).Format(0.0123);

        Assert.StartsWith("%", formatted, StringComparison.Ordinal);
        Assert.Equal(0.0123, NumberParser.Parse(formatted, trTr, format));
    }

    [Fact(DisplayName = "parses scientific notation percentages")]
    [Trait("Requirement", "NUM-I18N-05")]
    public void Parses_scientific_notation_percentages()
    {
        Assert.Equal(1e-9, NumberParser.Parse("1e-7%", s_enUs, new NumberFormatOptions { Style = NumberFormatStyle.Percent }));
    }

    [Fact(DisplayName = "handles percentages with style: \"unit\" and unit: \"percent\"")]
    [Trait("Requirement", "NUM-I18N-05")]
    public void Handles_percentages_with_style_unit_and_unit_percent()
    {
        Assert.Equal(12, NumberParser.Parse("12%", s_enUs, UnitFormat(NumberFormatUnit.Percent)));
    }

    [Fact(DisplayName = "strips an interleaved percent sign (1%2)")]
    [Trait("Requirement", "NUM-I18N-05")]
    public void Strips_an_interleaved_percent_sign()
    {
        Assert.Equal(0.12, NumberParser.Parse("1%2", s_enUs, new NumberFormatOptions { Style = NumberFormatStyle.Percent }));
        Assert.Equal(12, NumberParser.Parse("1%2", s_enUs, UnitFormat(NumberFormatUnit.Percent)));
    }

    [Fact(DisplayName = "parses fullwidth digits and punctuation")]
    [Trait("Requirement", "NUM-I18N-04")]
    public void Parses_fullwidth_digits_and_punctuation()
    {
        Assert.Equal(1234.56, Parse("１，２３４．５６"));
        Assert.Equal(0.12, Parse("１２％"));
    }

    [Fact(DisplayName = "parses Persian digits")]
    [Trait("Requirement", "NUM-I18N-03")]
    [Trait("Requirement", "NUM-I18N-04")]
    public void Parses_Persian_digits()
    {
        Assert.Equal(1234, Parse("۱۲۳۴"));
        Assert.Equal(12.34, Parse("۱۲٫۳۴"));
        Assert.Equal(0.12, Parse("۱۲٪"));
    }

    [Fact(DisplayName = "parses Persian digits with Arabic thousands and decimal characters")]
    [Trait("Requirement", "NUM-I18N-04")]
    public void Parses_Persian_digits_with_Arabic_thousands_and_decimal_characters() => Assert.Equal(12345.67, Parse("۱۲٬۳۴۵٫۶۷"));

    [Fact(DisplayName = "parses permille values")]
    [Trait("Requirement", "NUM-I18N-05")]
    public void Parses_permille_values()
    {
        Assert.Equal(0.012, Parse("12‰"));
        Assert.Equal(0.012, Parse("12؉"));
    }

    [Fact(DisplayName = "strips bidi/control characters")]
    [Trait("Requirement", "NUM-I18N-11")]
    public void Strips_bidi_control_characters()
    {
        Assert.Equal(1234.56, Parse("1‎234.56"));
        Assert.Equal(0.12, Parse("‎12‏%"));
    }

    [Fact(DisplayName = "handles unicode minus and plus signs")]
    [Trait("Requirement", "NUM-I18N-10")]
    public void Handles_unicode_minus_and_plus_signs()
    {
        Assert.Equal(-1234, Parse("−1234"));
        Assert.Equal(-1234, Parse("1234−"));
        Assert.Equal(1234, Parse("＋1234"));
        Assert.Equal(1234, Parse("1234＋"));
    }

    [Fact(DisplayName = "parses french formatted numbers with narrow no-break space grouping")]
    [Trait("Requirement", "NUM-I18N-08")]
    [Trait("Requirement", "NUM-I18N-10")]
    public void Parses_french_formatted_numbers_with_narrow_no_break_space_grouping()
    {
        CultureInfo frFr = CultureInfo.GetCultureInfo("fr-FR");
        string fr = NumberFormatter.Get(frFr, null).Format(1234.5);

        // The group separator is the host culture's (U+202F in current CLDR, U+00A0 in older versions); Base UI's browser writes U+202F.
        Assert.Equal($"1{frFr.NumberFormat.NumberGroupSeparator}234,5", fr);
        Assert.Equal(1234.5, NumberParser.Parse(fr, frFr));
        Assert.Equal(-1234.5, NumberParser.Parse(fr + "−", frFr));
    }

    [Fact(DisplayName = "parses currency when options specify currency style")]
    [Trait("Requirement", "NUM-I18N-12")]
    public void Parses_currency_when_options_specify_currency_style()
    {
        Assert.Equal(1234.56, NumberParser.Parse("$1,234.56", s_enUs, CurrencyFormat("USD")));
    }

    [Fact(DisplayName = "parses scientific notation with currency codes")]
    [Trait("Requirement", "NUM-I18N-12")]
    public void Parses_scientific_notation_with_currency_codes()
    {
        var format = CurrencyFormat("EUR") with
        {
            CurrencyDisplay = NumberFormatCurrencyDisplay.Code,
            Notation = NumberFormatNotation.Scientific,
            MaximumFractionDigits = 2,
        };
        string formatted = NumberFormatter.Get(s_enUs, format).Format(12345);

        Assert.Equal("EUR 1.23E4", formatted);
        Assert.Equal(12300, NumberParser.Parse(formatted, s_enUs, format));
    }

    [Theory(DisplayName = "parses localized scientific notation for %s")]
    [InlineData("ar-EG")]
    [InlineData("fa-IR")]
    [Trait("Requirement", "NUM-I18N-11")]
    [Trait("Requirement", "NUM-I18N-12")]
    [Trait("Requirement", "NUM-EDGE-18")]
    public void Parses_localized_scientific_notation(string locale)
    {
        CultureInfo culture = CultureInfo.GetCultureInfo(locale);
        var format = new NumberFormatOptions { Notation = NumberFormatNotation.Scientific, MaximumFractionDigits = 2 };
        string formatted = NumberFormatter.Get(culture, format).Format(12345);

        Assert.Equal(12300, NumberParser.Parse(formatted, culture, format));
    }

    [Theory(DisplayName = "parses localized percent scientific notation with a negative exponent for %s")]
    [InlineData("ar-EG")]
    [InlineData("fa-IR")]
    [Trait("Requirement", "NUM-I18N-11")]
    [Trait("Requirement", "NUM-EDGE-18")]
    public void Parses_localized_percent_scientific_notation_with_a_negative_exponent(string locale)
    {
        CultureInfo culture = CultureInfo.GetCultureInfo(locale);
        var format = new NumberFormatOptions
        {
            Style = NumberFormatStyle.Percent,
            Notation = NumberFormatNotation.Scientific,
            MaximumFractionDigits = 2,
        };
        const double value = 0.0000012345;
        string formatted = NumberFormatter.Get(culture, format).Format(value);

        Assert.Equal(0.00000123, NumberParser.Parse(formatted, culture, format));
    }

    [Theory(DisplayName = "parses localized tiny percent scientific notation for %s")]
    [InlineData("ar-EG")]
    [InlineData("fa-IR")]
    [Trait("Requirement", "NUM-I18N-11")]
    [Trait("Requirement", "NUM-EDGE-18")]
    public void Parses_localized_tiny_percent_scientific_notation(string locale)
    {
        CultureInfo culture = CultureInfo.GetCultureInfo(locale);
        var format = new NumberFormatOptions
        {
            Style = NumberFormatStyle.Percent,
            Notation = NumberFormatNotation.Scientific,
            MaximumFractionDigits = 2,
        };
        const double value = 0.0000000000012345;
        string formatted = NumberFormatter.Get(culture, format).Format(value);

        Assert.Equal(0.00000000000123, NumberParser.Parse(formatted, culture, format));
    }

    [Fact(DisplayName = "parses scientific notation permille values")]
    [Trait("Requirement", "NUM-I18N-05")]
    public void Parses_scientific_notation_permille_values() => Assert.Equal(1e-10, NumberParser.Parse("1e-7‰", s_enUs));

    [Fact(DisplayName = "parses units when options specify unit style")]
    [Trait("Requirement", "NUM-I18N-12")]
    public void Parses_units_when_options_specify_unit_style()
    {
        Assert.Equal(12, NumberParser.Parse("12 kg", s_enUs, UnitFormat(NumberFormatUnit.Kilogram)));
    }

    [Fact(DisplayName = "returns null for Infinity-like inputs")]
    [Trait("Requirement", "NUM-I18N-06")]
    public void Returns_null_for_Infinity_like_inputs()
    {
        Assert.Null(Parse("Infinity"));
        Assert.Null(Parse("-Infinity"));
        Assert.Null(Parse("∞"));
    }

    [Fact(DisplayName = "returns null when parsing overflows to Infinity")]
    [Trait("Requirement", "NUM-I18N-06")]
    public void Returns_null_when_parsing_overflows_to_Infinity()
    {
        string formatted = NumberFormatter.Get(s_enUs, new NumberFormatOptions { Style = NumberFormatStyle.Percent, MaximumFractionDigits = 2 })
            .Format(double.MaxValue);

        Assert.EndsWith("%", formatted, StringComparison.Ordinal);
        Assert.Null(NumberParser.Parse(formatted, s_enUs, new NumberFormatOptions { Style = NumberFormatStyle.Percent }));
    }

    [Fact(DisplayName = "collapses extra dots from mixed-locale inputs")]
    [Trait("Requirement", "NUM-I18N-09")]
    public void Collapses_extra_dots_from_mixed_locale_inputs() => Assert.Equal(1234567.89, Parse("1.234.567.89"));

    [Fact(DisplayName = "parses number with mixed separators (FR)")]
    [Trait("Requirement", "NUM-I18N-09")]
    public void Parses_number_with_mixed_separators_FR() => Assert.Equal(1234567.89, NumberParser.Parse("1.234.567,89", CultureInfo.GetCultureInfo("fr-FR")));

    [Fact(DisplayName = "parses number with mixed separators (US)")]
    [Trait("Requirement", "NUM-I18N-09")]
    public void Parses_number_with_mixed_separators_US() => Assert.Equal(1234.56789, NumberParser.Parse("1.234.567,89", s_enUs));

    [Fact(DisplayName = "returns null for empty and whitespace-only input")]
    [Trait("Requirement", "NUM-I18N-06")]
    public void Returns_null_for_empty_and_whitespace_only_input()
    {
        Assert.Null(Parse(""));
        Assert.Null(Parse("   "));
    }

    [Fact(DisplayName = "returns null for just a sign")]
    [Trait("Requirement", "NUM-I18N-06")]
    public void Returns_null_for_just_a_sign()
    {
        Assert.Null(Parse("-"));
        Assert.Null(Parse("+"));
    }

    [Fact(DisplayName = "handles ASCII leading and trailing signs")]
    [Trait("Requirement", "NUM-I18N-10")]
    public void Handles_ASCII_leading_and_trailing_signs()
    {
        Assert.Equal(1234, Parse("+1234"));
        Assert.Equal(1234, Parse("1234+"));
        Assert.Equal(-1234, Parse("-1234"));
        Assert.Equal(-1234, Parse("1234-"));
    }

    [Fact(DisplayName = "supports multiple unicode minus variants")]
    [Trait("Requirement", "NUM-I18N-10")]
    public void Supports_multiple_unicode_minus_variants()
    {
        Assert.Equal(-123, Parse("‒123"));
        Assert.Equal(-123, Parse("–123"));
        Assert.Equal(-123, Parse("—123"));
        Assert.Equal(-123, Parse("－123"));
        Assert.Equal(-123, Parse("﹣123"));
    }

    [Fact(DisplayName = "supports additional unicode plus variants")]
    [Trait("Requirement", "NUM-I18N-10")]
    public void Supports_additional_unicode_plus_variants() => Assert.Equal(123, Parse("﹢123"));

    [Fact(DisplayName = "handles additional percent symbol variants")]
    [Trait("Requirement", "NUM-I18N-05")]
    public void Handles_additional_percent_symbol_variants()
    {
        Assert.Equal(0.12, Parse("12％"));
        Assert.Equal(0.12, Parse("12﹪"));
        Assert.Equal(0.12, Parse("12٪"));
    }

    [Fact(DisplayName = "parses Arabic punctuation with ASCII digits")]
    [Trait("Requirement", "NUM-I18N-08")]
    public void Parses_Arabic_punctuation_with_ASCII_digits() => Assert.Equal(1234.56, Parse("1٬234٫56"));

    [Fact(DisplayName = "removes various Unicode space groupings (fr-FR)")]
    [Trait("Requirement", "NUM-I18N-08")]
    public void Removes_various_Unicode_space_groupings()
    {
        CultureInfo frFr = CultureInfo.GetCultureInfo("fr-FR");
        Assert.Equal(1234.56, NumberParser.Parse("1 234,56", frFr));
        Assert.Equal(1234.56, NumberParser.Parse("1 234,56", frFr));
        Assert.Equal(1234.56, NumberParser.Parse("1 234,56", frFr));
        Assert.Equal(1234.56, NumberParser.Parse("1 234,56", frFr));
    }

    [Fact(DisplayName = "handles Swiss grouping apostrophe")]
    [Trait("Requirement", "NUM-I18N-08")]
    public void Handles_Swiss_grouping_apostrophe()
    {
        CultureInfo deCh = CultureInfo.GetCultureInfo("de-CH");
        Assert.Equal(1234.56, NumberParser.Parse("1’234.56", deCh));
        Assert.Equal(1234.56, NumberParser.Parse("1'234.56", deCh));
    }

    [Fact(DisplayName = "parses de-DE formatted numbers")]
    [Trait("Requirement", "NUM-I18N-08")]
    public void Parses_de_DE_formatted_numbers()
    {
        CultureInfo deDe = CultureInfo.GetCultureInfo("de-DE");
        Assert.Equal(1234.56, NumberParser.Parse("1.234,56", deDe));
        Assert.Equal(1234567.89, NumberParser.Parse("1.234.567,89", deDe));
    }

    [Fact(DisplayName = "parses currency prefix/suffix across locales")]
    [Trait("Requirement", "NUM-I18N-12")]
    public void Parses_currency_prefix_suffix_across_locales()
    {
        CultureInfo frFr = CultureInfo.GetCultureInfo("fr-FR");
        NumberFormatOptions euro = CurrencyFormat("EUR");
        string en = NumberFormatter.Get(s_enUs, euro).Format(1234.56);
        string fr = NumberFormatter.Get(frFr, euro).Format(1234.56);

        Assert.Equal("€1,234.56", en);
        Assert.Equal($"1{frFr.NumberFormat.CurrencyGroupSeparator}234,56\u00A0{frFr.NumberFormat.CurrencySymbol}", fr);
        Assert.Equal(1234.56, NumberParser.Parse(en, s_enUs, euro));
        Assert.Equal(1234.56, NumberParser.Parse(fr, frFr, euro));
    }

    [Fact(DisplayName = "parses units with different formats")]
    [Trait("Requirement", "NUM-I18N-12")]
    public void Parses_units_with_different_formats()
    {
        Assert.Equal(12, NumberParser.Parse("12 km/h", s_enUs, UnitFormat(NumberFormatUnit.KilometerPerHour)));
        Assert.Equal(12, NumberParser.Parse("12 m/s", s_enUs, UnitFormat(NumberFormatUnit.MeterPerSecond)));
    }

    [Fact(DisplayName = "treats bidi/format controls as ignorable in the middle of input")]
    [Trait("Requirement", "NUM-I18N-11")]
    public void Treats_bidi_format_controls_as_ignorable_in_the_middle_of_input()
    {
        Assert.Equal(1234.56, Parse("1‪234‬.56"));
        Assert.Equal(0.12, Parse("‪12‬%"));
    }

    [Fact(DisplayName = "returns null for Infinity with explicit sign and surrounding spaces")]
    [Trait("Requirement", "NUM-I18N-06")]
    public void Returns_null_for_Infinity_with_explicit_sign_and_surrounding_spaces()
    {
        Assert.Null(Parse(" +Infinity "));
        Assert.Null(Parse(" -∞ "));
        Assert.Null(Parse("+Infinity"));
    }

    [Fact(DisplayName = "collapses multiple consecutive dots keeping only the last as decimal")]
    [Trait("Requirement", "NUM-I18N-09")]
    public void Collapses_multiple_consecutive_dots_keeping_only_the_last_as_decimal()
    {
        Assert.Equal(1.5, Parse("1..5"));
        Assert.Equal(123456789.01, Parse("123..456..789.01"));
        Assert.Equal(0.5, Parse("....5"));
    }

    [Fact(DisplayName = "maps the full Persian digit range")]
    [Trait("Requirement", "NUM-I18N-04")]
    public void Maps_the_full_Persian_digit_range() => Assert.Equal(9876543210, Parse("۹۸۷۶۵۴۳۲۱۰"));

    [Fact(DisplayName = "maps the full fullwidth digit range")]
    [Trait("Requirement", "NUM-I18N-04")]
    public void Maps_the_full_fullwidth_digit_range() => Assert.Equal(123456789, Parse("０１２３４５６７８９"));

    // Base UI runs this title under jsdom only (`it.skipIf(!isJSDOM)`, "Browser Intl handles Arabic-Indic digits
    // inconsistently"); the parser has no browser dependency here, so it runs.
    [Fact(DisplayName = "maps the full Arabic-Indic digit range")]
    [Trait("Requirement", "NUM-I18N-04")]
    public void Maps_the_full_Arabic_Indic_digit_range() => Assert.Equal(9876543210, Parse("٩٨٧٦٥٤٣٢١٠"));

    [Fact(DisplayName = "maps the full Han digit range")]
    [Trait("Requirement", "NUM-I18N-04")]
    public void Maps_the_full_Han_digit_range() => Assert.Equal(9876543210, Parse("九八七六五四三二一〇"));

    [Fact(DisplayName = "maps both Han zero forms to 0")]
    [Trait("Requirement", "NUM-I18N-04")]
    public void Maps_both_Han_zero_forms_to_0()
    {
        Assert.Equal(0, Parse("零"));
        Assert.Equal(0, Parse("〇"));
    }

    [Fact(DisplayName = "accepts a digit from every supported numeral system")]
    [Trait("Requirement", "NUM-I18N-04")]
    [Trait("Requirement", "NUM-KEY-06")]
    public void Accepts_a_digit_from_every_supported_numeral_system()
    {
        foreach (char c in "09٠٩۰۹０９零〇九")
        {
            Assert.True(NumberParser.IsNumeralChar(c), $"'{c}' is a numeral");
        }
    }

    [Fact(DisplayName = "rejects non-digit characters")]
    [Trait("Requirement", "NUM-KEY-06")]
    public void Rejects_non_digit_characters()
    {
        Assert.True(NumberParser.IsNumeralChar('5'));
        foreach (char c in ".,-+%٫a ")
        {
            Assert.False(NumberParser.IsNumeralChar(c), $"'{c}' is not a numeral");
        }
    }

    private static double? Parse(string text) => Default(() => NumberParser.Parse(text));

    private static T Default<T>(Func<T> action)
    {
        CultureInfo previous = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = s_enUs;
        try
        {
            return action();
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    private static NumberFormatOptions UnitFormat(NumberFormatUnit unit) => new() { Style = NumberFormatStyle.Unit, Unit = unit };

    private static NumberFormatOptions CurrencyFormat(string currency) => new() { Style = NumberFormatStyle.Currency, Currency = currency };
}

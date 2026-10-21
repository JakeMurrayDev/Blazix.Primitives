using System.Globalization;
using Blazix.Primitives.Utilities;

namespace Blazix.Primitives.Tests;

// MTR-ARIA-04, MTR-I18N-01..03 and (for Progress) PRG-I18N-01..03 under ADR 01 decision 2 as amended 2026-10-18: .NET
// globalization, output equivalent and not byte-identical to Intl.NumberFormat. ADR 08 Amendments 2026-10-21: Format is a
// .NET numeric format string, Culture a CultureInfo, and the default is the percentage divided by 100 under "P0".
// UTL-I18N-05..07 are met by double.ToString(format, culture): no formatter instance, no cache, no locale string.
/// <summary>The formatted value of a meter or progress bar through the shared formatting.</summary>
public sealed class NumberFormattingTests
{
    private static readonly CultureInfo s_enUs = CultureInfo.GetCultureInfo("en-US");
    private static readonly CultureInfo s_deDe = CultureInfo.GetCultureInfo("de-DE");

    [Fact(DisplayName = "defaults to the position within the range as a percentage")]
    [Trait("Requirement", "MTR-I18N-01")]
    [Trait("Requirement", "MTR-API-04")]
    public void Defaults_to_the_position_within_the_range_as_a_percentage()
    {
        Assert.Equal("50%", NumberFormatting.FormatMeasure(0.5, 50, null, s_enUs));
        Assert.Equal("50%", NumberFormatting.FormatMeasure(30, 50, null, s_enUs));
    }

    // Intl.NumberFormat('de-DE', { style: 'percent' }) separates the sign with U+00A0; .NET's P pattern for de-DE writes an
    // ASCII space. A port divergence accepted under ADR 01 decision 2 (MTR-I18N-01), recorded in the pull request.
    [Fact(DisplayName = "formats 30 percent in de-DE with a space before the percent sign")]
    [Trait("Requirement", "MTR-ARIA-04")]
    [Trait("Requirement", "MTR-I18N-01")]
    public void Formats_30_percent_in_de_DE_with_a_space_before_the_percent_sign()
    {
        Assert.Equal("30 %", NumberFormatting.FormatMeasure(30, 30, null, s_deDe));
    }

    [Fact(DisplayName = "rounds the default text to whole percent like the displayed value")]
    [Trait("Requirement", "MTR-ARIA-04")]
    [Trait("Requirement", "MTR-I18N-01")]
    public void Rounds_the_default_text_to_whole_percent_like_the_displayed_value()
    {
        Assert.Equal("33%", NumberFormatting.FormatMeasure(33.333, 33.333, null, s_enUs));
        Assert.Equal("78%", NumberFormatting.FormatMeasure(77.5, 77.5, null, s_enUs));
    }

    // MTR-I18N-02: with a format the clamped value is formatted with exactly that format, not the percentage.
    [Fact(DisplayName = "formats the clamped value with the given format")]
    [Trait("Requirement", "MTR-I18N-02")]
    [Trait("Requirement", "MTR-API-04")]
    public void Formats_the_clamped_value_with_the_given_format()
    {
        Assert.Equal("$30.00", NumberFormatting.FormatMeasure(30, 30, "C", s_enUs));
        Assert.Equal("$100.00", NumberFormatting.FormatMeasure(100, 100, "C2", s_enUs));
    }

    // Intl formats { style: 'currency', currency: 'USD' } as USD in every locale; .NET's "C" takes the culture's own currency
    // symbol, so the same call under de-DE writes euros (MTR-I18N-02, accepted divergence).
    [Fact(DisplayName = "takes the currency symbol from the culture")]
    [Trait("Requirement", "MTR-I18N-02")]
    public void Takes_the_currency_symbol_from_the_culture()
    {
        Assert.Equal("30,00 €", NumberFormatting.FormatMeasure(30, 30, "C", s_deDe));
    }

    [Fact(DisplayName = "treats an empty format like the general format")]
    [Trait("Requirement", "MTR-API-04")]
    public void Treats_an_empty_format_like_the_general_format()
    {
        // Base UI's `format ? …` is true for an empty options object (MTR-API-04), so the percentage default does not apply.
        Assert.Equal("30", NumberFormatting.FormatMeasure(30, 30, string.Empty, s_enUs));
    }

    [Fact(DisplayName = "uses the culture to select the decimal separator")]
    [Trait("Requirement", "MTR-I18N-03")]
    [Trait("Requirement", "MTR-API-05")]
    public void Uses_the_culture_to_select_the_decimal_separator()
    {
        Assert.Equal("86,49", NumberFormatting.FormatMeasure(86.49, 86.49, "F2", s_deDe));
        Assert.Equal("86.49", NumberFormatting.FormatMeasure(86.49, 86.49, "F2", s_enUs));
        Assert.Equal("86,49", NumberFormatting.FormatMeasure(86.49, 86.49, "N2", s_deDe));
    }

    [Fact(DisplayName = "uses the current culture when no culture is given")]
    [Trait("Requirement", "MTR-I18N-03")]
    [Trait("Requirement", "MTR-API-05")]
    public void Uses_the_current_culture_when_no_culture_is_given()
    {
        CultureInfo previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = s_deDe;
            Assert.Equal("30 %", NumberFormatting.FormatMeasure(30, 30, null, null));
            Assert.Equal("86,49", NumberFormatting.FormatMeasure(86.49, 86.49, "F2", null));

            CultureInfo.CurrentCulture = s_enUs;
            Assert.Equal("30%", NumberFormatting.FormatMeasure(30, 30, null, null));
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact(DisplayName = "formats a number with a format and a culture")]
    [Trait("Requirement", "UTL-I18N-05")]
    public void Formats_a_number_with_a_format_and_a_culture()
    {
        Assert.Equal("1,234.50", NumberFormatting.Format(1234.5, "N2", s_enUs));
        Assert.Equal("1.234,50", NumberFormatting.Format(1234.5, "N2", s_deDe));
    }

    // UTL-I18N-05: `new Intl.NumberFormat(locale, options).format(value)` per call; the port keeps no formatter, so equal
    // (culture, format) pairs trivially give equal text (UTL-I18N-06) and a culture object needs no key (UTL-I18N-07).
    [Fact(DisplayName = "gives equal text for equal culture and format pairs")]
    [Trait("Requirement", "UTL-I18N-06")]
    [Trait("Requirement", "UTL-I18N-07")]
    public void Gives_equal_text_for_equal_culture_and_format_pairs()
    {
        string first = NumberFormatting.Format(1234.5, "N2", CultureInfo.GetCultureInfo("de-DE"));
        string second = NumberFormatting.Format(1234.5, "N2", new CultureInfo("de-DE"));

        Assert.Equal(first, second);
    }
}

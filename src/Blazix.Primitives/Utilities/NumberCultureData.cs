using System.Collections.Concurrent;
using System.Globalization;

namespace Blazix.Primitives.Utilities;

// UTL-I18N-05, NUM-I18N-01, NUM-I18N-12; ADR 01 decision 2 as amended (equivalent to Intl.NumberFormat, not byte-identical) and
// ADR 08 Amendments 2026-11-18. The locale data Intl reads from CLDR and NumberFormatInfo does not carry: the symbol of a
// currency that is not the culture's own, a currency's digits, the unit texts, the exponent separator of the Arabic numbering
// systems and the minimum grouping digits. Each is derived from .NET's culture data where it has the fact and from a short
// CLDR-sourced table where it does not; the tables are the port's equivalence, recorded in Utilities/CONTEXT.md.
/// <summary>Provides the locale data of the number formatter that <see cref="NumberFormatInfo"/> does not carry.</summary>
internal static class NumberCultureData
{
    private const string NoBreakSpace = " ";

    private static readonly Lazy<Dictionary<string, CultureInfo[]>> s_culturesByCurrency = new(IndexCulturesByCurrency);
    private static readonly ConcurrentDictionary<(string Culture, string Code, NumberFormatCurrencyDisplay Display), string> s_currencyTexts = new();

    /// <summary>Gets the digits the culture's default numbering system writes, or <see langword="null"/> for the ASCII digits.</summary>
    /// <param name="format">The culture's number format.</param>
    /// <returns>The ten native digits, or <see langword="null"/>.</returns>
    public static string[]? NativeDigits(NumberFormatInfo format)
    {
        // .NET over ICU reports the default numbering system's digits with a substitution other than None (ar-EG, fa-IR) and
        // the ASCII digits with None (ar-MA), as CLDR's defaultNumberingSystem does.
        if (format.DigitSubstitution == DigitShapes.None || format.NativeDigits.Length != 10 || format.NativeDigits[0] == "0")
        {
            return null;
        }

        return format.NativeDigits;
    }

    // CLDR exponential symbols: "E" for latn, "أس" for arab, "×۱۰^" for arabext.
    /// <summary>Gets the exponent separator of scientific notation for a numbering system.</summary>
    /// <param name="nativeDigits">The native digits, or <see langword="null"/> for the ASCII digits.</param>
    /// <returns>The exponent separator.</returns>
    public static string ExponentSeparator(string[]? nativeDigits) => nativeDigits?[0] switch
    {
        "٠" => "أس",
        "۰" => "×۱۰^",
        _ => "E",
    };

    /// <summary>Gets the minimum number of digits before the first group separator for grouping to apply.</summary>
    /// <param name="culture">The culture.</param>
    /// <returns>1 or 2.</returns>
    // CLDR minimumGroupingDigits = 2 (a four-digit integer is not grouped): Polish, Bulgarian, Spain's Spanish and European
    // Portuguese; Latin American Spanish (es-419 and its regions) and Brazilian Portuguese group from four digits.
    public static int MinimumGroupingDigits(CultureInfo culture)
    {
        string language = culture.TwoLetterISOLanguageName;
        return language is "pl" or "bg" || culture.Name is "es" or "es-ES" or "pt-PT" ? 2 : 1;
    }

    /// <summary>Gets the number of fraction digits of a currency (ISO 4217's minor units).</summary>
    /// <param name="code">The upper-case currency code.</param>
    /// <returns>The currency's digits, or 2 when no culture uses the currency.</returns>
    public static int CurrencyDigits(string code) => HomeCulture(code)?.NumberFormat.CurrencyDecimalDigits ?? 2;

    /// <summary>Gets the text that displays a currency in a culture.</summary>
    /// <param name="culture">The culture.</param>
    /// <param name="code">The upper-case currency code.</param>
    /// <param name="display">How the currency is displayed.</param>
    /// <returns>The symbol, code or name.</returns>
    public static string CurrencyText(CultureInfo culture, string code, NumberFormatCurrencyDisplay display)
    {
        return s_currencyTexts.GetOrAdd((culture.Name, code, display), static (key, culture) => ResolveCurrencyText(culture, key.Code, key.Display), culture);
    }

    // CLDR short unit patterns. Temperatures join the symbol without a space in English; other units take a space, which
    // English writes as U+0020 and other languages as U+00A0.
    /// <summary>Gets the text of a unit and the separator between the number and the unit in a culture.</summary>
    /// <param name="culture">The culture.</param>
    /// <param name="unit">The unit, other than <see cref="NumberFormatUnit.Percent"/>.</param>
    /// <returns>The separator and the unit text.</returns>
    public static (string Separator, string Text) UnitText(CultureInfo culture, NumberFormatUnit unit)
    {
        bool english = culture.TwoLetterISOLanguageName == "en";
        string text = unit switch
        {
            NumberFormatUnit.Kilogram => "kg",
            NumberFormatUnit.Gram => "g",
            NumberFormatUnit.Meter => "m",
            NumberFormatUnit.Kilometer => "km",
            NumberFormatUnit.Centimeter => "cm",
            NumberFormatUnit.Millimeter => "mm",
            NumberFormatUnit.Liter => "L",
            NumberFormatUnit.Milliliter => "mL",
            NumberFormatUnit.KilometerPerHour => "km/h",
            NumberFormatUnit.MeterPerSecond => "m/s",
            NumberFormatUnit.Celsius => "°C",
            NumberFormatUnit.Fahrenheit => "°F",
            NumberFormatUnit.Kilobyte => "kB",
            NumberFormatUnit.Megabyte => "MB",
            NumberFormatUnit.Gigabyte => "GB",
            _ => throw new ArgumentOutOfRangeException(nameof(unit), "Blazix: the unit has no text."),
        };

        bool temperature = unit is NumberFormatUnit.Celsius or NumberFormatUnit.Fahrenheit;
        string separator = english ? (temperature ? string.Empty : " ") : NoBreakSpace;
        return (separator, text);
    }

    private static string ResolveCurrencyText(CultureInfo culture, string code, NumberFormatCurrencyDisplay display)
    {
        if (display == NumberFormatCurrencyDisplay.Code)
        {
            return code;
        }

        CultureInfo? home = HomeCulture(code);
        if (display == NumberFormatCurrencyDisplay.Name)
        {
            if (home is null)
            {
                return code;
            }

            var region = new RegionInfo(home.Name);
            return home.TwoLetterISOLanguageName == culture.TwoLetterISOLanguageName ? region.CurrencyNativeName : region.CurrencyEnglishName;
        }

        if (display == NumberFormatCurrencyDisplay.Symbol)
        {
            if (OwnCurrency(culture) == code)
            {
                return culture.NumberFormat.CurrencySymbol;
            }

            // A culture of the same language that uses the currency, so en-US writes the euro as en-IE does.
            if (s_culturesByCurrency.Value.TryGetValue(code, out CultureInfo[]? users))
            {
                foreach (CultureInfo user in users)
                {
                    if (user.TwoLetterISOLanguageName == culture.TwoLetterISOLanguageName)
                    {
                        return user.NumberFormat.CurrencySymbol;
                    }
                }
            }
        }

        return home?.NumberFormat.CurrencySymbol ?? code;
    }

    private static string? OwnCurrency(CultureInfo culture)
    {
        if (culture.IsNeutralCulture || culture.Name.Length == 0)
        {
            return null;
        }

        try
        {
            return new RegionInfo(culture.Name).ISOCurrencySymbol;
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    // The culture of the currency's issuing region (USD: a culture of US), else the first culture that uses it (EUR).
    private static CultureInfo? HomeCulture(string code)
    {
        if (!s_culturesByCurrency.Value.TryGetValue(code, out CultureInfo[]? users))
        {
            return null;
        }

        foreach (CultureInfo user in users)
        {
            if (user.Name.EndsWith(code[..2], StringComparison.Ordinal))
            {
                return user;
            }
        }

        return users[0];
    }

    private static Dictionary<string, CultureInfo[]> IndexCulturesByCurrency()
    {
        var index = new Dictionary<string, List<CultureInfo>>(StringComparer.Ordinal);
        CultureInfo[] cultures = CultureInfo.GetCultures(CultureTypes.SpecificCultures);
        Array.Sort(cultures, static (a, b) => string.CompareOrdinal(a.Name, b.Name));
        foreach (CultureInfo culture in cultures)
        {
            string? code = OwnCurrency(culture);
            if (code is null || code.Length != 3)
            {
                continue;
            }

            if (!index.TryGetValue(code, out List<CultureInfo>? list))
            {
                list = [];
                index[code] = list;
            }

            list.Add(culture);
        }

        var result = new Dictionary<string, CultureInfo[]>(index.Count, StringComparer.Ordinal);
        foreach (KeyValuePair<string, List<CultureInfo>> entry in index)
        {
            result[entry.Key] = [.. entry.Value];
        }

        return result;
    }
}

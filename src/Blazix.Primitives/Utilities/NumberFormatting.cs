using System.Globalization;

namespace Blazix.Primitives.Utilities;

// MTR-API-04, MTR-API-05, MTR-I18N-01..03 (PRG-I18N-01..03 for Progress); UTL-I18N-05..07. ADR 01 decision 2 as amended
// 2026-10-18: .NET globalization, output equivalent and not byte-identical to Intl.NumberFormat. ADR 08 Amendments
// 2026-10-21: Format is a .NET numeric format string, Culture a CultureInfo (null: the current culture), and the default is
// the percentage divided by 100 under "P0". UTL-I18N-05..07 describe a cached Intl.NumberFormat per (locale, options); the
// port calls double.ToString(format, culture) directly, so it has no formatter instance, no cache and no locale string.
/// <summary>Provides the number formatting of the value-in-a-range parts.</summary>
internal static class NumberFormatting
{
    private const string PercentFormat = "P0";

    // UTL-I18N-05.
    /// <summary>Formats a number with a format string under a culture.</summary>
    /// <param name="value">The number.</param>
    /// <param name="format">A .NET numeric format string, or <see langword="null"/> for the general format.</param>
    /// <param name="culture">The culture, or <see langword="null"/> for <see cref="CultureInfo.CurrentCulture"/>.</param>
    /// <returns>The formatted text.</returns>
    public static string Format(double value, string? format, CultureInfo? culture) => value.ToString(format, culture ?? CultureInfo.CurrentCulture);

    // MTR-I18N-01: the percentage divided by 100, whole percent (Intl's `{ style: 'percent' }` default of 0 fraction digits).
    /// <summary>Formats a percentage between 0 and 100 as a whole percent under a culture.</summary>
    /// <param name="percentage">The percentage, where 100 reads as one hundred percent.</param>
    /// <param name="culture">The culture, or <see langword="null"/> for <see cref="CultureInfo.CurrentCulture"/>.</param>
    /// <returns>The percentage divided by 100 formatted with <c>P0</c>.</returns>
    public static string FormatPercentage(double percentage, CultureInfo? culture) => (percentage / 100).ToString(PercentFormat, culture ?? CultureInfo.CurrentCulture);

    // MTR-API-04: any format, including an empty one (Base UI's `format ?` is true for an empty options object), formats
    // the clamped value; without a format the percentage is formatted.
    /// <summary>Formats the value of a part that shows either a value or its position in a range.</summary>
    /// <param name="clampedValue">The value limited to its range.</param>
    /// <param name="percentage">The value's position in its range, from 0 to 100.</param>
    /// <param name="format">A .NET numeric format string for the value, or <see langword="null"/> to format the percentage.</param>
    /// <param name="culture">The culture, or <see langword="null"/> for <see cref="CultureInfo.CurrentCulture"/>.</param>
    /// <returns>The clamped value formatted with <paramref name="format"/>, or the percentage as a whole percent.</returns>
    public static string FormatMeasure(double clampedValue, double percentage, string? format, CultureInfo? culture)
    {
        return format is not null ? Format(clampedValue, format, culture) : FormatPercentage(percentage, culture);
    }
}

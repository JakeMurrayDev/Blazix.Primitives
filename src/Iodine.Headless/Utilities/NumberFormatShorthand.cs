using System.Globalization;

namespace Iodine.Headless.Utilities;

// ADR 08 Amendments 2026-11-18 (decision 10 (a), chosen by the user 2026-11-18): NumberFieldRoot.Format's implicit conversion
// from a .NET numeric format string. The standard strings N, F, P and E with their precision (the .NET defaults: 2, and 6 for
// E) and custom patterns of `0`, `#`, `,`, `.`, `%` and an exponent map onto the options record; a string whose meaning the
// record cannot carry (C needs the culture's currency; D, G, R and X; sections; literals; scaling commas) is rejected.
/// <summary>Converts a .NET numeric format string to <see cref="NumberFormatOptions"/>.</summary>
internal static class NumberFormatShorthand
{
    private const int DefaultPrecision = 2;
    private const int DefaultExponentPrecision = 6;

    /// <summary>Converts a .NET numeric format string to the options it means.</summary>
    /// <param name="format">The format string, such as <c>N2</c>, <c>P0</c> or <c>#,##0.00</c>.</param>
    /// <returns>The options.</returns>
    /// <exception cref="ArgumentException">The string has no options equivalent.</exception>
    public static NumberFormatOptions Parse(string format)
    {
        ArgumentNullException.ThrowIfNull(format);
        if (format.Length > 0 && char.IsAsciiLetter(format[0]) && TryParseStandard(format, out NumberFormatOptions? standard))
        {
            return standard;
        }

        return ParseCustom(format);
    }

    private static bool TryParseStandard(string format, out NumberFormatOptions options)
    {
        options = null!;
        int precision = -1;
        if (format.Length > 1)
        {
            if (!int.TryParse(format.AsSpan(1), NumberStyles.None, CultureInfo.InvariantCulture, out precision) || precision > 100)
            {
                return false;
            }
        }

        char kind = char.ToUpperInvariant(format[0]);
        int digits = precision < 0 ? (kind == 'E' ? DefaultExponentPrecision : DefaultPrecision) : precision;
        switch (kind)
        {
            case 'N':
                options = new NumberFormatOptions { MinimumFractionDigits = digits, MaximumFractionDigits = digits };
                return true;
            case 'F':
                options = new NumberFormatOptions { UseGrouping = false, MinimumFractionDigits = digits, MaximumFractionDigits = digits };
                return true;
            case 'P':
                options = new NumberFormatOptions { Style = NumberFormatStyle.Percent, MinimumFractionDigits = digits, MaximumFractionDigits = digits };
                return true;
            case 'E':
                options = new NumberFormatOptions { Notation = NumberFormatNotation.Scientific, MinimumFractionDigits = digits, MaximumFractionDigits = digits };
                return true;
            default:
                throw Unsupported(format);
        }
    }

    private static NumberFormatOptions ParseCustom(string format)
    {
        int integerZeros = 0;
        bool sawPlaceholder = false;
        bool grouping = false;
        bool afterPoint = false;
        bool percent = false;
        bool scientific = false;
        int fractionZeros = 0;
        int fractionPlaces = 0;

        for (int i = 0; i < format.Length; i++)
        {
            char c = format[i];
            switch (c)
            {
                case '0' or '#':
                    if (afterPoint)
                    {
                        fractionPlaces++;
                        if (c == '0')
                        {
                            // A `0` after a `#` in the fraction still pads to its position, as .NET does.
                            fractionZeros = fractionPlaces;
                        }
                    }
                    else if (c == '0')
                    {
                        integerZeros++;
                    }

                    sawPlaceholder = true;
                    break;
                case ',':
                    // A comma before the point that ends the integer digits scales by 1000 in .NET; only a separator groups.
                    if (afterPoint || !sawPlaceholder || i + 1 >= format.Length || format[i + 1] is not ('0' or '#'))
                    {
                        throw Unsupported(format);
                    }

                    grouping = true;
                    break;
                case '.':
                    if (afterPoint)
                    {
                        throw Unsupported(format);
                    }

                    afterPoint = true;
                    break;
                case '%':
                    percent = true;
                    break;
                case 'E' or 'e':
                    int j = i + 1;
                    if (j < format.Length && format[j] is '+' or '-')
                    {
                        j++;
                    }

                    if (!sawPlaceholder || j >= format.Length || format[j] != '0')
                    {
                        throw Unsupported(format);
                    }

                    while (j < format.Length && format[j] == '0')
                    {
                        j++;
                    }

                    scientific = true;
                    i = j - 1;
                    break;
                default:
                    throw Unsupported(format);
            }
        }

        if (!sawPlaceholder)
        {
            throw Unsupported(format);
        }

        return new NumberFormatOptions
        {
            Style = percent ? NumberFormatStyle.Percent : NumberFormatStyle.Decimal,
            Notation = scientific ? NumberFormatNotation.Scientific : NumberFormatNotation.Standard,
            UseGrouping = grouping,
            MinimumIntegerDigits = integerZeros > 1 ? integerZeros : null,
            MinimumFractionDigits = fractionZeros,
            MaximumFractionDigits = fractionPlaces,
        };
    }

    private static ArgumentException Unsupported(string format) => new(
        $"Iodine: the format string `{format}` has no `NumberFormatOptions` equivalent. Use `N`, `F`, `P` or `E` with an optional precision, a pattern of `0`, `#`, `,`, `.`, `%` and `E0`, or a `NumberFormatOptions`.",
        nameof(format));
}

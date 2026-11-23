using System.Globalization;
using System.Text;

namespace Iodine.Headless.Utilities;

// NUM-KEY-07, NUM-EVT-15, NUM-EDGE-06 (NumberFieldRoot.tsx:152-206, getAllowedNonNumericKeys; NumberFieldInput.tsx:247-258;
// parse.ts:37-52): what a number field's input accepts besides numerals. C# computes the set for the `number-field` module's
// keydown gate and runs the character test again on every text the module sends (decision 7 (a); issue 277, 6b).
/// <summary>Computes the characters a number field's input accepts.</summary>
internal static class NumberInputCharacters
{
    private const string BaseSymbols = ".,．，٫٬";
    private const string PlusSigns = "+＋﹢";
    private const string MinusSigns = "-−－‒–—﹣";
    private const string PercentSigns = "%٪％﹪";
    private const string PermilleSigns = "‰؉";

    /// <summary>Gets the characters other than numerals that the input accepts for a culture and format.</summary>
    /// <param name="culture">The culture, or <see langword="null"/> for the current culture.</param>
    /// <param name="options">The format, or <see langword="null"/> for the default.</param>
    /// <param name="allowMinus">Whether negatives are valid (a negative minimum, or out-of-range entry allowed).</param>
    /// <returns>The characters, each once, in the order Base UI adds them.</returns>
    public static string Allowed(CultureInfo? culture, NumberFormatOptions? options, bool allowMinus)
    {
        NumberFormatter formatter = NumberFormatter.Get(culture, options);
        List<NumberFormatPart> parts = formatter.FormatToParts(NumberLocaleDetails.SampleNumber);
        StringBuilder keys = new(BaseSymbols);

        // Integer formats omit the decimal from the parts; it stays typeable.
        string? decimalPart = null;
        foreach (NumberFormatPart part in parts)
        {
            if (part.Type == NumberFormatPartType.Decimal)
            {
                decimalPart = part.Value;
                break;
            }
        }

        Add(keys, decimalPart ?? formatter.LocaleDetails().Decimal);

        foreach (NumberFormatPart part in parts)
        {
            if (part.Type is NumberFormatPartType.Integer or NumberFormatPartType.Fraction or NumberFormatPartType.ExponentInteger or NumberFormatPartType.Compact)
            {
                continue;
            }

            Add(keys, part.Value);
            if (ContainsSpaceSeparator(part.Value))
            {
                Add(keys, " ");
            }
        }

        NumberFormatStyle style = options?.Style ?? NumberFormatStyle.Decimal;
        if (style == NumberFormatStyle.Percent || (style == NumberFormatStyle.Unit && options?.Unit == NumberFormatUnit.Percent))
        {
            Add(keys, PercentSigns);
        }

        // NumberFormatUnit has no permille; Base UI's `unit: 'permille'` is unreachable here.
        if (style == NumberFormatStyle.Percent)
        {
            Add(keys, PermilleSigns);
        }

        Add(keys, PlusSigns);
        if (allowMinus)
        {
            Add(keys, MinusSigns);
        }

        return keys.ToString();
    }

    /// <summary>Gets whether every character of a text is a numeral, a minus variant, an allowed character or a format control.</summary>
    /// <param name="text">The text.</param>
    /// <param name="allowed">The allowed characters, from <see cref="Allowed"/>.</param>
    /// <returns><see langword="true"/> when the input accepts the text.</returns>
    public static bool IsAllowedText(string text, string allowed)
    {
        foreach (char c in text)
        {
            if (!NumberParser.IsNumeralChar(c)
                && !MinusSigns.Contains(c, StringComparison.Ordinal)
                && !allowed.Contains(c, StringComparison.Ordinal)
                && CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.Format)
            {
                return false;
            }
        }

        return true;
    }

    private static void Add(StringBuilder keys, string? characters)
    {
        if (characters is null)
        {
            return;
        }

        foreach (char c in characters)
        {
            if (keys.ToString().IndexOf(c, StringComparison.Ordinal) < 0)
            {
                keys.Append(c);
            }
        }
    }

    private static bool ContainsSpaceSeparator(string value)
    {
        foreach (char c in value)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.SpaceSeparator)
            {
                return true;
            }
        }

        return false;
    }
}

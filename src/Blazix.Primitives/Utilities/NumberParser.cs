using System.Globalization;
using System.Text;

namespace Blazix.Primitives.Utilities;

// NUM-I18N-02..12 (number-field/utils/parse.ts): Base UI's parseNumber step for step, with ECMAScript's character classes
// written out (sheet D contradiction 12): `\s` and String.prototype.trim are ECMAScript's WhiteSpace and LineTerminator, never
// char.IsWhiteSpace (which adds U+0085 and omits U+FEFF); `\d` is ASCII; `\p{Cf}` and `\p{Zs}` are the Unicode categories, as
// the `u` flag reads them. parseFloat is ECMAScript's (the longest StrDecimalLiteral prefix). The locale details and unit texts
// come from the port's formatter (Utilities/NumberFormatter.cs), so a formatted value always parses back.
/// <summary>Parses the text of a number field under a culture and a set of number format options.</summary>
internal static class NumberParser
{
    // Base UI's HAN_NUMERALS: both zero forms first, so a character's digit is max(indexOf - 1, 0).
    private const string HanNumerals = "零〇一二三四五六七八九";
    private const string PercentSigns = "%٪％﹪";
    private const string PermilleSigns = "‰؉";
    private const string MinusSigns = "-−－‒–—﹣";
    private const string PlusSigns = "+＋﹢";

    [ThreadStatic]
    private static StringBuilder? s_builder;

    // NUM-KEY-06, isNumeralChar: ASCII, Arabic-Indic, Persian, fullwidth and Han digits.
    /// <summary>Determines whether a character is a digit of a numeral system the field accepts.</summary>
    /// <param name="c">The character.</param>
    /// <returns><see langword="true"/> when the character is a digit; otherwise, <see langword="false"/>.</returns>
    public static bool IsNumeralChar(char c) => char.IsAsciiDigit(c) || IsArabicPersianDigit(c) || c is >= '０' and <= '９' || HanNumerals.Contains(c, StringComparison.Ordinal);

    // NUM-I18N-07.
    /// <summary>Gets the symbols a culture and a set of options render.</summary>
    /// <param name="culture">The culture, or <see langword="null"/> for <see cref="CultureInfo.CurrentCulture"/>.</param>
    /// <param name="options">The options, or <see langword="null"/> for the defaults.</param>
    /// <returns>The locale details.</returns>
    public static NumberLocaleDetails GetLocaleDetails(CultureInfo? culture, NumberFormatOptions? options) => NumberFormatter.Get(culture, options).LocaleDetails();

    // NUM-I18N-02, the procedure of number-field.md §4.1.
    /// <summary>Parses the text of a number field.</summary>
    /// <param name="text">The text.</param>
    /// <param name="culture">
    /// The culture, or <see langword="null"/> to infer Arabic from Arabic-Indic or Persian digits and Chinese from Han numerals,
    /// else to use <see cref="CultureInfo.CurrentCulture"/>.
    /// </param>
    /// <param name="options">The options the text was formatted with, or <see langword="null"/>.</param>
    /// <returns>The number, or <see langword="null"/> when the text is not a finite number.</returns>
    public static double? Parse(string text, CultureInfo? culture = null, NumberFormatOptions? options = null)
    {
        string input = Trim(RemoveFormatControls(text));
        input = NormalizeSigns(input);

        bool negative = false;

        // /([+-])\s*$/ then /^\s*([+-])/: after the trim, the trailing sign is the last character.
        if (input.Length > 0 && input[^1] is '+' or '-')
        {
            negative = input[^1] == '-';
            input = input[..^1];
        }

        int leading = 0;
        while (leading < input.Length && IsEcmaWhiteSpace(input[leading]))
        {
            leading++;
        }

        if (leading < input.Length && input[leading] is '+' or '-')
        {
            negative |= input[leading] == '-';
            input = input[(leading + 1)..];
        }

        CultureInfo? computed = culture;
        if (computed is null)
        {
            if (ContainsArabicPersianDigit(input))
            {
                computed = CultureInfo.GetCultureInfo("ar");
            }
            else if (ContainsHan(input))
            {
                computed = CultureInfo.GetCultureInfo("zh");
            }
        }

        NumberFormatter formatter = NumberFormatter.Get(computed, options);
        NumberLocaleDetails details = formatter.LocaleDetails();

        StringBuilder s = s_builder ??= new StringBuilder(32);
        s.Clear().Append(input);

        string? group = details.Group;
        if (!string.IsNullOrEmpty(group))
        {
            if (ContainsSpaceSeparator(group))
            {
                RemoveWhere(s, static c => CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.SpaceSeparator);
            }
            else if (group is "'" or "’")
            {
                RemoveWhere(s, static c => c is '\'' or '’');
            }
            else
            {
                s.Replace(group, string.Empty);
            }
        }

        s.Replace(details.Decimal!, ".");
        s.Replace('．', '.').Replace('٫', '.');
        RemoveWhere(s, static c => c is '，' or '٬');
        if (!string.IsNullOrEmpty(details.Currency))
        {
            s.Replace(details.Currency, string.Empty);
        }

        RemoveAlternation(s, formatter.UnitParts());
        RemoveWhere(s, static c => PercentSigns.Contains(c, StringComparison.Ordinal));
        RemoveWhere(s, static c => PermilleSigns.Contains(c, StringComparison.Ordinal));
        if (!string.IsNullOrEmpty(details.ExponentSeparator))
        {
            s.Replace(details.ExponentSeparator, "e");
        }

        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];
            if (IsArabicPersianDigit(c) || c is >= '０' and <= '９')
            {
                s[i] = (char)('0' + (c % 16));
            }
            else if (HanNumerals.IndexOf(c, StringComparison.Ordinal) is int han and >= 0)
            {
                s[i] = (char)('0' + Math.Max(han - 1, 0));
            }
        }

        string unformatted = s.ToString();

        // Mixed-locale safety: keep only the last '.'.
        int lastDot = unformatted.LastIndexOf('.');
        if (lastDot != -1)
        {
            unformatted = unformatted[..lastDot].Replace(".", string.Empty, StringComparison.Ordinal) + "." + unformatted[(lastDot + 1)..].Replace(".", string.Empty, StringComparison.Ordinal);
        }

        // /^[-+]?Infinity$/i or the infinity symbol.
        ReadOnlySpan<char> unsigned = input.Length > 0 && input[0] is '+' or '-' ? input.AsSpan(1) : input.AsSpan();
        if (unsigned.Equals("Infinity", StringComparison.OrdinalIgnoreCase) || input.Contains('∞', StringComparison.Ordinal))
        {
            return null;
        }

        double number = EcmaParseFloat((negative ? "-" : string.Empty) + unformatted);

        bool isUnitPercent = options is { Style: NumberFormatStyle.Unit, Unit: NumberFormatUnit.Percent };
        bool hasPercentSymbol = ContainsAny(text, PercentSigns) || options?.Style == NumberFormatStyle.Percent;
        bool hasPermilleSymbol = ContainsAny(text, PermilleSigns);
        if (hasPermilleSymbol)
        {
            number = ShiftDecimal(number, -3);
        }
        else if (!isUnitPercent && hasPercentSymbol)
        {
            number = ShiftDecimal(number, -2);
        }

        return double.IsFinite(number) ? number : null;
    }

    // ECMAScript parseFloat: leading StrWhiteSpaceChar skipped, then the longest prefix that is a StrDecimalLiteral.
    /// <summary>Parses the longest decimal-literal prefix of a text the way ECMAScript's <c>parseFloat</c> does.</summary>
    /// <param name="text">The text.</param>
    /// <returns>The number, or <see cref="double.NaN"/> when no prefix is a number.</returns>
    public static double EcmaParseFloat(string text)
    {
        int i = 0;
        while (i < text.Length && IsEcmaWhiteSpace(text[i]))
        {
            i++;
        }

        int start = i;
        bool negative = false;
        if (i < text.Length && text[i] is '+' or '-')
        {
            negative = text[i] == '-';
            i++;
        }

        if (string.CompareOrdinal(text, i, "Infinity", 0, 8) == 0)
        {
            return negative ? double.NegativeInfinity : double.PositiveInfinity;
        }

        int integerStart = i;
        while (i < text.Length && char.IsAsciiDigit(text[i]))
        {
            i++;
        }

        int integerDigits = i - integerStart;
        int fractionDigits = 0;
        if (i < text.Length && text[i] == '.')
        {
            int fractionStart = i + 1;
            int j = fractionStart;
            while (j < text.Length && char.IsAsciiDigit(text[j]))
            {
                j++;
            }

            fractionDigits = j - fractionStart;
            if (integerDigits > 0 || fractionDigits > 0)
            {
                i = j;
            }
        }

        if (integerDigits == 0 && fractionDigits == 0)
        {
            return double.NaN;
        }

        if (i < text.Length && text[i] is 'e' or 'E')
        {
            int j = i + 1;
            if (j < text.Length && text[j] is '+' or '-')
            {
                j++;
            }

            int exponentStart = j;
            while (j < text.Length && char.IsAsciiDigit(text[j]))
            {
                j++;
            }

            if (j > exponentStart)
            {
                i = j;
            }
        }

        ReadOnlySpan<char> literal = text.AsSpan(start, i - start);
        if (literal.Length > 0 && literal[^1] == '.')
        {
            literal = literal[..^1];
        }

        return double.Parse(literal, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint | NumberStyles.AllowExponent, CultureInfo.InvariantCulture);
    }

    // ECMAScript WhiteSpace and LineTerminator: TAB, VT, FF, ZWNBSP, any Zs, LF, CR, LS and PS.
    /// <summary>Determines whether a character is white space or a line terminator under ECMAScript's rules.</summary>
    /// <param name="c">The character.</param>
    /// <returns><see langword="true"/> for ECMAScript white space; otherwise, <see langword="false"/>.</returns>
    public static bool IsEcmaWhiteSpace(char c) => c is '\t' or '\n' or '\v' or '\f' or '\r' or '\uFEFF' or '\u2028' or '\u2029'
        || CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.SpaceSeparator;

    // \p{Cf} (format controls), code point by code point.
    /// <summary>Removes every Unicode format-control character from a text.</summary>
    /// <param name="text">The text.</param>
    /// <returns>The text without format controls, the same instance when it has none.</returns>
    public static string RemoveFormatControls(string text)
    {
        int first = -1;
        for (int i = 0; i < text.Length; i++)
        {
            if (IsFormatControlAt(text, i, out _))
            {
                first = i;
                break;
            }
        }

        if (first < 0)
        {
            return text;
        }

        var builder = new StringBuilder(text.Length);
        builder.Append(text, 0, first);
        for (int i = first; i < text.Length; i++)
        {
            if (IsFormatControlAt(text, i, out int width))
            {
                i += width - 1;
                continue;
            }

            builder.Append(text[i]);
        }

        return builder.ToString();
    }

    // shiftDecimal: String(value) split at 'e', the exponent moved, re-read with Number(...), so 12% is 0.12 exactly.
    private static double ShiftDecimal(double value, int exponentDelta)
    {
        if (!double.IsFinite(value))
        {
            return double.NaN;
        }

        string text = NumberSerializer.Serialize(value);
        int e = text.IndexOf('e', StringComparison.Ordinal);
        string coefficient = e >= 0 ? text[..e] : text;
        int exponent = e >= 0 ? int.Parse(text.AsSpan(e + 1), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture) : 0;
        return double.Parse(coefficient + "e" + (exponent + exponentDelta).ToString(CultureInfo.InvariantCulture), NumberStyles.Float, CultureInfo.InvariantCulture);
    }

    private static bool IsFormatControlAt(string text, int index, out int width)
    {
        width = 1;
        if (char.IsHighSurrogate(text[index]) && index + 1 < text.Length && char.IsLowSurrogate(text[index + 1]))
        {
            width = 2;
            return CharUnicodeInfo.GetUnicodeCategory(char.ConvertToUtf32(text[index], text[index + 1])) == UnicodeCategory.Format;
        }

        return CharUnicodeInfo.GetUnicodeCategory(text[index]) == UnicodeCategory.Format;
    }

    private static string Trim(string text)
    {
        int start = 0;
        int end = text.Length;
        while (start < end && IsEcmaWhiteSpace(text[start]))
        {
            start++;
        }

        while (end > start && IsEcmaWhiteSpace(text[end - 1]))
        {
            end--;
        }

        return start == 0 && end == text.Length ? text : text[start..end];
    }

    private static string NormalizeSigns(string text)
    {
        if (text.AsSpan().IndexOfAny(MinusSigns.AsSpan(1)) < 0 && text.AsSpan().IndexOfAny(PlusSigns.AsSpan(1)) < 0)
        {
            return text;
        }

        var builder = new StringBuilder(text.Length);
        foreach (char c in text)
        {
            builder.Append(MinusSigns.Contains(c, StringComparison.Ordinal) ? '-' : PlusSigns.Contains(c, StringComparison.Ordinal) ? '+' : c);
        }

        return builder.ToString();
    }

    private static bool IsArabicPersianDigit(char c) => c is >= '٠' and <= '٩' or >= '۰' and <= '۹';

    private static bool ContainsArabicPersianDigit(string text)
    {
        foreach (char c in text)
        {
            if (IsArabicPersianDigit(c))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ContainsHan(string text) => text.AsSpan().IndexOfAny(HanNumerals) >= 0;

    private static bool ContainsAny(string text, string characters) => text.AsSpan().IndexOfAny(characters) >= 0;

    private static bool ContainsSpaceSeparator(string text)
    {
        foreach (char c in text)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.SpaceSeparator)
            {
                return true;
            }
        }

        return false;
    }

    private static void RemoveWhere(StringBuilder builder, Func<char, bool> predicate)
    {
        int write = 0;
        for (int read = 0; read < builder.Length; read++)
        {
            char c = builder[read];
            if (!predicate(c))
            {
                builder[write++] = c;
            }
        }

        builder.Length = write;
    }

    // A global alternation of literal texts: at each position the first alternative that matches is removed.
    private static void RemoveAlternation(StringBuilder builder, string[] alternatives)
    {
        if (alternatives.Length == 0)
        {
            return;
        }

        string text = builder.ToString();
        builder.Clear();
        int i = 0;
        while (i < text.Length)
        {
            bool matched = false;
            foreach (string alternative in alternatives)
            {
                if (alternative.Length > 0 && string.CompareOrdinal(text, i, alternative, 0, alternative.Length) == 0)
                {
                    i += alternative.Length;
                    matched = true;
                    break;
                }
            }

            if (!matched)
            {
                builder.Append(text[i]);
                i++;
            }
        }
    }
}

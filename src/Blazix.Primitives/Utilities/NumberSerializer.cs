using System.Globalization;

namespace Blazix.Primitives.Utilities;

// UTL-DOM-37 (shared-utilities §4.12.1); MTR-ALG-03. Base UI writes numbers into attributes and inline styles through
// ECMAScript Number::toString(x, 10). double.ToString() agrees on the shortest round-trip digits but spells exponents
// `E+21` / `E-07` and switches to them at other thresholds, so the digits are taken from the "R" form and laid out by the
// ECMAScript rules.
/// <summary>Provides the serialization of numbers written into attribute values and inline styles.</summary>
internal static class NumberSerializer
{
    private const int MaxLength = 40;
    private const double WholeNumberLimit = 1e15;

    /// <summary>Serializes a number the way ECMAScript's <c>Number.prototype.toString</c> does.</summary>
    /// <param name="value">The number.</param>
    /// <returns>
    /// The shortest decimal text that reads back as <paramref name="value"/>, without grouping or culture-specific
    /// separators: <c>"NaN"</c>, <c>"Infinity"</c> and <c>"-Infinity"</c> for the special values, <c>"0"</c> for both zeros,
    /// positional notation for magnitudes from <c>1e-6</c> up to below <c>1e21</c> and exponent notation (<c>1e+21</c>,
    /// <c>1.5e-7</c>) beyond.
    /// </returns>
    public static string Serialize(double value)
    {
        if (double.IsNaN(value))
        {
            return "NaN";
        }

        if (value == 0)
        {
            return "0";
        }

        if (double.IsPositiveInfinity(value))
        {
            return "Infinity";
        }

        if (double.IsNegativeInfinity(value))
        {
            return "-Infinity";
        }

        // The common attribute value is a whole number: its digits are the text, with no buffer.
        if (Math.Abs(value) < WholeNumberLimit && value == Math.Floor(value))
        {
            return ((long)value).ToString(CultureInfo.InvariantCulture);
        }

        Span<char> round = stackalloc char[32];
        value.TryFormat(round, out int written, "R", CultureInfo.InvariantCulture);
        ReadOnlySpan<char> text = round[..written];

        bool negative = text[0] == '-';
        if (negative)
        {
            text = text[1..];
        }

        int exponentAt = text.IndexOf('E');
        int exponent = 0;
        if (exponentAt >= 0)
        {
            exponent = int.Parse(text[(exponentAt + 1)..], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);
            text = text[..exponentAt];
        }

        // digits: the significant digits without the point; point: the number of digits before it, shifted by the exponent.
        Span<char> digits = stackalloc char[32];
        int count = 0;
        int point = 0;
        bool seenPoint = false;
        foreach (char c in text)
        {
            if (c == '.')
            {
                seenPoint = true;
                continue;
            }

            digits[count++] = c;
            if (!seenPoint)
            {
                point++;
            }
        }

        point += exponent;

        // x = 0.D * 10^point, with D free of leading and trailing zeros.
        int start = 0;
        while (start < count - 1 && digits[start] == '0')
        {
            start++;
            point--;
        }

        int end = count;
        while (end > start + 1 && digits[end - 1] == '0')
        {
            end--;
        }

        ReadOnlySpan<char> significant = digits[start..end];
        return Compose(negative, significant, point);
    }

    // The layout rules of UTL-DOM-37: k = digits, n = point.
    private static string Compose(bool negative, ReadOnlySpan<char> digits, int n)
    {
        int k = digits.Length;
        Span<char> output = stackalloc char[MaxLength];
        int length = 0;
        if (negative)
        {
            output[length++] = '-';
        }

        if (k <= n && n <= 21)
        {
            digits.CopyTo(output[length..]);
            length += k;
            output.Slice(length, n - k).Fill('0');
            length += n - k;
        }
        else if (0 < n && n <= 21)
        {
            digits[..n].CopyTo(output[length..]);
            length += n;
            output[length++] = '.';
            digits[n..].CopyTo(output[length..]);
            length += k - n;
        }
        else if (-6 < n && n <= 0)
        {
            output[length++] = '0';
            output[length++] = '.';
            output.Slice(length, -n).Fill('0');
            length += -n;
            digits.CopyTo(output[length..]);
            length += k;
        }
        else
        {
            int e = n - 1;
            output[length++] = digits[0];
            if (k > 1)
            {
                output[length++] = '.';
                digits[1..].CopyTo(output[length..]);
                length += k - 1;
            }

            output[length++] = 'e';
            output[length++] = e >= 0 ? '+' : '-';
            Math.Abs(e).TryFormat(output[length..], out int exponentLength, default, CultureInfo.InvariantCulture);
            length += exponentLength;
        }

        return new string(output[..length]);
    }
}

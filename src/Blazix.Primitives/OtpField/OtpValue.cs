using System.Text;

namespace Blazix.Primitives;

// OTP-STA-04..08, OTP-API-08, OTP-EDGE-07, OTP-EDGE-12 (otp-field/utils/otp.ts; issue 278, M6 track 7): the value helpers C#
// owns (decision 7 (a), Q-OTP-1 (c)). One consumer, so the family folder.
// - The classes are ECMAScript's: `\s` is JavaScript's whitespace set, `\d` and the letters are ASCII (sheet D contradiction 12).
// - The filters and the slices work on UTF-16 code units, as JavaScript's regular expressions without the `u` flag and
//   `String.prototype.slice` do; only the clamp counts code points, as `Array.from` does (§16 item 2, replicated).
/// <summary>Normalizes, replaces and removes the characters of a one-time code.</summary>
internal static class OtpValue
{
    /// <summary>Removes every whitespace character from a value.</summary>
    /// <param name="value">The value, or <see langword="null"/>.</param>
    /// <returns>The value without whitespace; empty for <see langword="null"/>.</returns>
    public static string StripWhitespace(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        int first = 0;
        while (first < value.Length && !IsWhitespace(value[first]))
        {
            first++;
        }

        if (first == value.Length)
        {
            return value;
        }

        StringBuilder builder = new(value.Length);
        builder.Append(value, 0, first);
        for (int i = first + 1; i < value.Length; i++)
        {
            if (!IsWhitespace(value[i]))
            {
                builder.Append(value[i]);
            }
        }

        return builder.ToString();
    }

    /// <summary>Normalizes a value: strips whitespace, filters by the validation type, applies the custom normalization and clamps to the length.</summary>
    /// <param name="value">The raw value, or <see langword="null"/>.</param>
    /// <param name="length">The number of slots; a negative length clamps to empty.</param>
    /// <param name="validationType">The accepted characters, or <see langword="null"/> for numeric.</param>
    /// <param name="normalize">The custom normalization, or <see langword="null"/>.</param>
    /// <returns>The normalized value.</returns>
    public static string Normalize(string? value, int length, OtpFieldValidationType? validationType, Func<string, string>? normalize = null)
    {
        return NormalizeWithDetails(value, length, validationType, normalize).Value;
    }

    /// <summary>Normalizes a value and reports whether characters were rejected.</summary>
    /// <param name="value">The raw value, or <see langword="null"/>.</param>
    /// <param name="length">The number of slots; a negative length clamps to empty.</param>
    /// <param name="validationType">The accepted characters, or <see langword="null"/> for numeric.</param>
    /// <param name="normalize">The custom normalization, or <see langword="null"/>.</param>
    /// <returns>The normalized value, and whether a character was filtered out, removed by the custom normalization or clamped away; whitespace removal is not a rejection.</returns>
    public static (string Value, bool Rejected) NormalizeWithDetails(string? value, int length, OtpFieldValidationType? validationType, Func<string, string>? normalize = null)
    {
        OtpFieldValidationType type = validationType ?? OtpFieldValidationType.Numeric;
        string stripped = StripWhitespace(value);
        string filtered = Filter(stripped, type);
        bool rejected = stripped.Length > filtered.Length;

        if (normalize is not null)
        {
            string custom = normalize(filtered) ?? string.Empty;
            rejected |= filtered.Length > custom.Length;
            filtered = Filter(custom, type);
            rejected |= custom.Length > filtered.Length;
        }

        int max = length < 0 ? 0 : length;
        int end = 0;
        int codePoints = 0;
        while (end < filtered.Length && codePoints < max)
        {
            end += char.IsHighSurrogate(filtered[end]) && end + 1 < filtered.Length && char.IsLowSurrogate(filtered[end + 1]) ? 2 : 1;
            codePoints++;
        }

        bool clamped = end < filtered.Length;
        return (clamped ? filtered[..end] : filtered, rejected || clamped);
    }

    /// <summary>Overwrites the characters from a slot with a normalized text and normalizes the result.</summary>
    /// <param name="current">The current value.</param>
    /// <param name="index">The slot the text starts at.</param>
    /// <param name="next">The text to write.</param>
    /// <param name="length">The number of slots.</param>
    /// <param name="validationType">The accepted characters, or <see langword="null"/> for numeric.</param>
    /// <param name="normalize">The custom normalization, or <see langword="null"/>.</param>
    /// <returns>The new value; characters pushed past the last slot are dropped.</returns>
    public static string Replace(string current, int index, string next, int length, OtpFieldValidationType? validationType, Func<string, string>? normalize = null)
    {
        string normalized = Normalize(next, length, validationType, normalize);
        int start = Math.Clamp(index, 0, current.Length);
        int resume = Math.Clamp(index + normalized.Length, 0, current.Length);
        return Normalize(string.Concat(current.AsSpan(0, start), normalized, current.AsSpan(resume)), length, validationType, normalize);
    }

    /// <summary>Removes the character at a slot; later characters move one slot left.</summary>
    /// <param name="current">The current value.</param>
    /// <param name="index">The slot to clear.</param>
    /// <returns>The new value, or <paramref name="current"/> when the slot holds no character.</returns>
    public static string Remove(string current, int index)
    {
        if (index < 0 || index >= current.Length)
        {
            return current;
        }

        return current.Remove(index, 1);
    }

    /// <summary>Gets a value indicating whether a character belongs to a validation type's class.</summary>
    /// <param name="c">The UTF-16 code unit.</param>
    /// <param name="validationType">The validation type.</param>
    /// <returns><see langword="true"/> when the character is accepted.</returns>
    public static bool Accepts(char c, OtpFieldValidationType validationType) => validationType switch
    {
        OtpFieldValidationType.Numeric => char.IsAsciiDigit(c),
        OtpFieldValidationType.Alpha => char.IsAsciiLetter(c),
        OtpFieldValidationType.Alphanumeric => char.IsAsciiLetterOrDigit(c),
        _ => true,
    };

    private static string Filter(string value, OtpFieldValidationType validationType)
    {
        if (validationType == OtpFieldValidationType.None)
        {
            return value;
        }

        int first = 0;
        while (first < value.Length && Accepts(value[first], validationType))
        {
            first++;
        }

        if (first == value.Length)
        {
            return value;
        }

        StringBuilder builder = new(value.Length);
        builder.Append(value, 0, first);
        for (int i = first + 1; i < value.Length; i++)
        {
            if (Accepts(value[i], validationType))
            {
                builder.Append(value[i]);
            }
        }

        return builder.ToString();
    }

    // ECMAScript's WhiteSpace and LineTerminator productions, the set `\s` matches.
    private static bool IsWhitespace(char c) => (int)c switch
    {
        0x09 or 0x0A or 0x0B or 0x0C or 0x0D or 0x20 => true,
        0xA0 or 0x1680 or 0x2028 or 0x2029 or 0x202F or 0x205F or 0x3000 or 0xFEFF => true,
        >= 0x2000 and <= 0x200A => true,
        _ => false,
    };
}

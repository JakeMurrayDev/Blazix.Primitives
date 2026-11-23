using System.Globalization;
using System.Numerics;

namespace Iodine.Headless.Utilities;

// ADR 01 Amendments 2026-10-20 (`:115`): the nine Intl rounding modes and `roundingIncrement` as one C# helper, tested with
// `validate.test.ts`; MidpointRounding lacks HalfCeil, HalfFloor, HalfTrunc and Expand. NUM-STA-10..12, UTL-I18N-05: the rounding
// of ECMA-402 ToRawPrecision and ToRawFixed (with GetUnsignedRoundingMode and ApplyUnsignedRoundingMode), over the exact decimal
// value of the shortest round-trip digits of a double, which is what ICU rounds (so 1.235 is a tie under HalfEven).
/// <summary>Provides decimal rounding of non-negative decimal numbers under the nine rounding modes and a rounding increment.</summary>
internal static class DecimalRounding
{
    // GetUnsignedRoundingMode (ECMA-402 Table 15): the signed mode applied to the magnitude of a number of the given sign.
    /// <summary>Converts a rounding mode to the rounding of a number's magnitude.</summary>
    /// <param name="mode">The rounding mode.</param>
    /// <param name="negative"><see langword="true"/> when the number is negative.</param>
    /// <returns>The rounding of the magnitude.</returns>
    public static UnsignedRounding ToUnsigned(NumberFormatRoundingMode mode, bool negative) => mode switch
    {
        NumberFormatRoundingMode.Ceil => negative ? UnsignedRounding.Zero : UnsignedRounding.Infinity,
        NumberFormatRoundingMode.Floor => negative ? UnsignedRounding.Infinity : UnsignedRounding.Zero,
        NumberFormatRoundingMode.Expand => UnsignedRounding.Infinity,
        NumberFormatRoundingMode.Trunc => UnsignedRounding.Zero,
        NumberFormatRoundingMode.HalfCeil => negative ? UnsignedRounding.HalfZero : UnsignedRounding.HalfInfinity,
        NumberFormatRoundingMode.HalfFloor => negative ? UnsignedRounding.HalfInfinity : UnsignedRounding.HalfZero,
        NumberFormatRoundingMode.HalfTrunc => UnsignedRounding.HalfZero,
        NumberFormatRoundingMode.HalfEven => UnsignedRounding.HalfEven,
        _ => UnsignedRounding.HalfInfinity,
    };

    /// <summary>Reads the exact decimal value of the shortest round-trip digits of a finite double's magnitude.</summary>
    /// <param name="value">A finite double.</param>
    /// <returns>The magnitude as <c>coefficient × 10^exponent</c>, with no trailing zero in the coefficient (zero is <c>0 × 10^0</c>).</returns>
    public static DecimalNumber FromDouble(double value)
    {
        double magnitude = Math.Abs(value);
        if (magnitude == 0)
        {
            return default;
        }

        Span<char> buffer = stackalloc char[32];
        magnitude.TryFormat(buffer, out int written, "R", CultureInfo.InvariantCulture);
        ReadOnlySpan<char> text = buffer[..written];

        int exponent = 0;
        int exponentAt = text.IndexOf('E');
        if (exponentAt >= 0)
        {
            exponent = int.Parse(text[(exponentAt + 1)..], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);
            text = text[..exponentAt];
        }

        ulong coefficient = 0;
        int digits = 0;
        bool afterPoint = false;
        foreach (char c in text)
        {
            if (c == '.')
            {
                afterPoint = true;
                continue;
            }

            if (digits == 0 && c == '0')
            {
                if (afterPoint)
                {
                    exponent--;
                }

                continue;
            }

            coefficient = (coefficient * 10) + (ulong)(c - '0');
            digits++;
            if (afterPoint)
            {
                exponent--;
            }
        }

        while (coefficient % 10 == 0)
        {
            coefficient /= 10;
            exponent++;
        }

        return new DecimalNumber(coefficient, exponent);
    }

    // ApplyUnsignedRoundingMode at the unit increment × 10^magnitude: x / unit as a rational, rounded to an integer.
    /// <summary>Rounds a non-negative decimal number to a multiple of <c>increment × 10^magnitude</c>.</summary>
    /// <param name="value">The non-negative number.</param>
    /// <param name="magnitude">The power of ten of the rounding unit.</param>
    /// <param name="increment">The multiple of the power of ten, at least 1.</param>
    /// <param name="rounding">The rounding of the magnitude.</param>
    /// <returns>The rounded number, with its exponent at <paramref name="magnitude"/>.</returns>
    public static DecimalNumber Round(DecimalNumber value, int magnitude, int increment, UnsignedRounding rounding)
    {
        if (value.Coefficient.IsZero)
        {
            return new DecimalNumber(BigInteger.Zero, magnitude);
        }

        int shift = value.Exponent - magnitude;
        BigInteger numerator = shift >= 0 ? value.Coefficient * BigInteger.Pow(10, shift) : value.Coefficient;
        BigInteger denominator = shift >= 0 ? new BigInteger(increment) : BigInteger.Pow(10, -shift) * increment;

        BigInteger quotient = BigInteger.DivRem(numerator, denominator, out BigInteger remainder);
        if (!remainder.IsZero && RoundsUp(quotient, remainder, denominator, rounding))
        {
            quotient += 1;
        }

        return new DecimalNumber(quotient * increment, magnitude);
    }

    private static bool RoundsUp(BigInteger quotient, BigInteger remainder, BigInteger denominator, UnsignedRounding rounding)
    {
        if (rounding == UnsignedRounding.Zero)
        {
            return false;
        }

        if (rounding == UnsignedRounding.Infinity)
        {
            return true;
        }

        int half = (remainder * 2).CompareTo(denominator);
        return half switch
        {
            < 0 => false,
            > 0 => true,
            _ => rounding switch
            {
                UnsignedRounding.HalfInfinity => true,
                UnsignedRounding.HalfZero => false,
                _ => !quotient.IsEven,
            },
        };
    }
}

using System.Numerics;

namespace Iodine.Headless.Utilities;

// NUM-STA-12, UTL-I18N-05: the exact decimal value DecimalRounding and NumberFormatter work on.
/// <summary>Represents a non-negative decimal number <c>Coefficient × 10^Exponent</c>.</summary>
/// <param name="Coefficient">The non-negative integer coefficient.</param>
/// <param name="Exponent">The power of ten.</param>
internal readonly record struct DecimalNumber(BigInteger Coefficient, int Exponent)
{
    /// <summary>Gets the power of ten of the number's leading digit, or 0 for zero.</summary>
    public int Magnitude => Coefficient.IsZero ? 0 : DigitCount(Coefficient) - 1 + Exponent;

    /// <summary>Returns the number multiplied by a power of ten.</summary>
    /// <param name="power">The power of ten.</param>
    /// <returns>The shifted number.</returns>
    public DecimalNumber Shift(int power) => new(Coefficient, Exponent + power);

    /// <summary>Counts the decimal digits of a positive integer.</summary>
    /// <param name="value">The positive integer.</param>
    /// <returns>The number of decimal digits.</returns>
    public static int DigitCount(BigInteger value)
    {
        if (value < 10)
        {
            return 1;
        }

        int estimate = (int)Math.Floor(BigInteger.Log10(value)) + 1;

        // Log10 can land on either side of an exact power of ten.
        BigInteger power = BigInteger.Pow(10, estimate - 1);
        if (value < power)
        {
            return estimate - 1;
        }

        return value >= power * 10 ? estimate + 1 : estimate;
    }
}

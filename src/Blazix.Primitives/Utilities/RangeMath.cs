namespace Blazix.Primitives.Utilities;

// UTL-EDGE-14, UTL-DOM-31; MTR-ALG-01, MTR-ALG-02 (and PRG-ALG-01 for Progress): IEEE-754 double arithmetic, the same
// operations in the same order as Base UI's clamp and valueToPercent.
/// <summary>Provides the range arithmetic of the value-in-a-range parts: clamping and the position of a value as a percentage.</summary>
internal static class RangeMath
{
    // UTL-EDGE-14: max(min, min(value, max)). An inverted range returns the lower bound; a NaN value stays NaN.
    /// <summary>Limits a value to a range, with the lower bound winning when the range is inverted.</summary>
    /// <param name="value">The value to limit.</param>
    /// <param name="min">The lower bound.</param>
    /// <param name="max">The upper bound.</param>
    /// <returns><c>max(min, min(value, max))</c>; <see cref="double.NaN"/> when <paramref name="value"/> is <see cref="double.NaN"/>.</returns>
    public static double Clamp(double value, double min, double max) => Math.Max(min, Math.Min(value, max));

    // UTL-DOM-31: no clamping and no guard for max == min.
    /// <summary>Computes the position of a value within a range as a percentage, without clamping.</summary>
    /// <param name="value">The value.</param>
    /// <param name="min">The lower bound.</param>
    /// <param name="max">The upper bound.</param>
    /// <returns><c>((value - min) * 100) / (max - min)</c>, which is infinite or <see cref="double.NaN"/> when the range is empty.</returns>
    public static double ValueToPercent(double value, double min, double max) => ((value - min) * 100) / (max - min);

    // MTR-ALG-01: NaN (0 / 0) becomes 0, then the result is clamped to [0, 100] (so ±infinity become 100 and 0).
    /// <summary>Computes the position of a value within a range as a percentage between 0 and 100.</summary>
    /// <param name="value">The value.</param>
    /// <param name="min">The lower bound.</param>
    /// <param name="max">The upper bound.</param>
    /// <returns>The percentage of <see cref="ValueToPercent"/> with <see cref="double.NaN"/> replaced by 0, limited to 0 through 100.</returns>
    public static double ClampedPercentage(double value, double min, double max)
    {
        double percentage = ValueToPercent(value, min, max);
        return Clamp(double.IsNaN(percentage) ? 0 : percentage, 0, 100);
    }

    // MTR-ALG-02: a NaN value becomes min before it is clamped.
    /// <summary>Limits a value to a range, reading <see cref="double.NaN"/> as the lower bound.</summary>
    /// <param name="value">The value.</param>
    /// <param name="min">The lower bound.</param>
    /// <param name="max">The upper bound.</param>
    /// <returns>The value limited by <see cref="Clamp"/>, or <paramref name="min"/> limited likewise when the value is <see cref="double.NaN"/>.</returns>
    public static double ClampedValue(double value, double min, double max) => Clamp(double.IsNaN(value) ? min : value, min, max);
}

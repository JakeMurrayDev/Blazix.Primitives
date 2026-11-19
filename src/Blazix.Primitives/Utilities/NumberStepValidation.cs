using System.Collections.Concurrent;
using System.Globalization;

namespace Blazix.Primitives.Utilities;

// NUM-STA-07..12 (number-field/utils/validate.ts): toValidatedNumber, snapToStep and removeFloatingPointErrors, the same IEEE
// operations in the same order. clamp is UTL-EDGE-14 (RangeMath.Clamp); Math.round is ECMAScript's (ties toward +∞).
/// <summary>Provides the number field's step pipeline: snapping to the step, clamping and the floating-point cleanup.</summary>
internal static class NumberStepValidation
{
    /// <summary>The smallest safe integer, <c>-(2^53 - 1)</c>, the minimum when none is set (Number.MIN_SAFE_INTEGER).</summary>
    public const double MinSafeInteger = -9007199254740991;

    /// <summary>The largest safe integer, <c>2^53 - 1</c>, the maximum when none is set (Number.MAX_SAFE_INTEGER).</summary>
    public const double MaxSafeInteger = 9007199254740991;

    // A relative factor scaled by the step size when snapping (STEP_EPSILON_FACTOR).
    private const double StepEpsilonFactor = 1e-10;

    // An absolute cap on how far the floating-point cleanup may move a value (MAX_FLOATING_POINT_CLEANUP_DELTA).
    private const double MaxCleanupDelta = 1e-10;

    // Number.EPSILON, 2^-52 (not double.Epsilon, the smallest subnormal).
    private const double NumberEpsilon = 2.220446049250313e-16;

    private static readonly CultureInfo s_enUs = CultureInfo.GetCultureInfo("en-US");
    private static readonly ConcurrentDictionary<NumberFormatOptions, NumberFormatOptions> s_cleanupOptions = new();

    // hasNumberFormatRoundingOptions (NUM-STA-10).
    /// <summary>Determines whether a format carries a digit or rounding option.</summary>
    /// <param name="format">The format, or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when the format carries a fraction-digit, significant-digit or rounding option; otherwise, <see langword="false"/>.</returns>
    public static bool HasRoundingOptions(NumberFormatOptions? format) => format is not null
        && (format.MaximumFractionDigits is not null
            || format.MinimumFractionDigits is not null
            || format.MaximumSignificantDigits is not null
            || format.MinimumSignificantDigits is not null
            || format.RoundingIncrement is not null
            || format.RoundingMode is not null
            || format.RoundingPriority is not null);

    // NUM-STA-11 and NUM-STA-12.
    /// <summary>Removes the binary floating-point noise of a value, or rounds it to the format's displayed precision.</summary>
    /// <param name="value">The value.</param>
    /// <param name="format">The format, or <see langword="null"/>.</param>
    /// <returns>The cleaned value, or <paramref name="value"/> when cleaning would change it beyond its noise.</returns>
    public static double RemoveFloatingPointErrors(double value, NumberFormatOptions? format)
    {
        if (!double.IsFinite(value))
        {
            return value;
        }

        if (!HasRoundingOptions(format))
        {
            double rounded = double.Parse(value.ToString("E14", CultureInfo.InvariantCulture), NumberStyles.Float, CultureInfo.InvariantCulture);
            double cleanupDelta = Math.Abs(rounded - value);
            double cleanupTolerance = Math.Min(NumberEpsilon * Math.Max(1, Math.Abs(value)), MaxCleanupDelta);
            return cleanupDelta <= cleanupTolerance ? rounded : value;
        }

        // The display decoration that does not change the numeric rounding is overridden.
        NumberFormatOptions cleanup = s_cleanupOptions.GetOrAdd(format!, static f => f with
        {
            SignDisplay = NumberFormatSignDisplay.Auto,
            CurrencySign = NumberFormatCurrencySign.Standard,
            Notation = f.Notation == NumberFormatNotation.Compact ? NumberFormatNotation.Standard : f.Notation,
            UseGrouping = false,
        });

        NumberFormatter formatter = NumberFormatter.Get(s_enUs, cleanup);
        string roundedText = formatter.Format(value);
        double? roundedValue = NumberParser.Parse(roundedText, s_enUs, format);
        if (roundedValue is not double parsed)
        {
            return value;
        }

        return formatter.Format(parsed) == roundedText ? parsed : value;
    }

    // NUM-STA-07..09: snap (only with a defined, non-zero step and snapOnStep), clamp, round, clamp.
    /// <summary>Validates a requested value: snaps it to the step, clamps it and rounds it to the format's precision.</summary>
    /// <param name="value">The requested value, or <see langword="null"/>.</param>
    /// <param name="step">The signed step of a step request, or <see langword="null"/> for direct entry.</param>
    /// <param name="minWithDefault">The minimum, or the smallest safe integer when none is set.</param>
    /// <param name="maxWithDefault">The maximum, or the largest safe integer when none is set.</param>
    /// <param name="minWithZeroDefault">The minimum, or 0 when none is set.</param>
    /// <param name="format">The format, or <see langword="null"/>.</param>
    /// <param name="snapOnStep"><see langword="true"/> to snap step requests to the step grid.</param>
    /// <param name="small"><see langword="true"/> for a small step, which snaps to the nearest grid point.</param>
    /// <param name="shouldClamp"><see langword="true"/> to clamp to the bounds.</param>
    /// <returns>The validated value, or <see langword="null"/> for a <see langword="null"/> request.</returns>
    public static double? ToValidatedNumber(
        double? value,
        double? step,
        double minWithDefault,
        double maxWithDefault,
        double minWithZeroDefault,
        NumberFormatOptions? format,
        bool snapOnStep,
        bool small,
        bool shouldClamp)
    {
        if (value is not double nextValue)
        {
            return null;
        }

        if (step is double signedStep && snapOnStep && signedStep != 0)
        {
            double baseValue = small || minWithDefault == MinSafeInteger ? minWithZeroDefault : minWithDefault;
            nextValue = SnapToStep(nextValue, baseValue, signedStep, small);
        }

        if (shouldClamp)
        {
            nextValue = RangeMath.Clamp(nextValue, minWithDefault, maxWithDefault);
        }

        // Direct entry performs no arithmetic, so there is no binary noise to clean and every digit is kept.
        if (step is null && !HasRoundingOptions(format))
        {
            return nextValue;
        }

        double roundedValue = RemoveFloatingPointErrors(nextValue, format);
        return shouldClamp ? RangeMath.Clamp(roundedValue, minWithDefault, maxWithDefault) : roundedValue;
    }

    // NUM-STA-08.
    private static double SnapToStep(double value, double baseValue, double step, bool nearest)
    {
        double stepSize = Math.Abs(step);
        double direction = Math.Sign(step);
        double tolerance = stepSize * StepEpsilonFactor * direction;
        double rawSteps = value - baseValue + tolerance;

        if (nearest)
        {
            return baseValue + (EcmaRound(rawSteps / step) * step);
        }

        double snappedSteps = direction > 0 ? Math.Floor(rawSteps / stepSize) : Math.Ceiling(rawSteps / stepSize);
        return baseValue + (snappedSteps * stepSize);
    }

    // ECMAScript Math.round: the nearest integer, ties toward +∞ (Math.Floor(x + 0.5) is wrong for 0.49999999999999994).
    private static double EcmaRound(double x)
    {
        if (!double.IsFinite(x))
        {
            return x;
        }

        double floor = Math.Floor(x);
        return x - floor >= 0.5 ? floor + 1 : floor;
    }
}

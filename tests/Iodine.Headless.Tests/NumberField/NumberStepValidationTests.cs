using System.Globalization;
using Iodine.Headless.Utilities;

namespace Iodine.Headless.Tests;

// NUM-STA-07..12: Base UI's `number-field/utils/validate.test.ts` (73 titles), verbatim over the C# step pipeline. ADR 01
// Amendments 2026-10-20: the nine rounding modes and the rounding increment are one C# helper, tested with these titles. A
// title that compares `new Intl.NumberFormat('en-US', format)` texts compares NumberFormatter's.
/// <summary>The number field's step pipeline and floating-point cleanup.</summary>
public sealed class NumberStepValidationTests
{
    private const double Min = -9007199254740991;
    private const double Max = 9007199254740991;
    private static readonly CultureInfo s_enUs = CultureInfo.GetCultureInfo("en-US");

    private static readonly Options s_default = new(1, Min, Max, 0, null, true, false, true);

    [Fact(DisplayName = "returns 0.3 for 0.2 + 0.1")]
    [Trait("Requirement", "NUM-STA-11")]
    public void Returns_0_3_for_0_2_plus_0_1() => Assert.Equal(0.3, NumberStepValidation.RemoveFloatingPointErrors(0.2 + 0.1, null));

    [Fact(DisplayName = "cleans negative floating point noise without a format")]
    [Trait("Requirement", "NUM-STA-11")]
    public void Cleans_negative_floating_point_noise_without_a_format() => Assert.Equal(-0.3, NumberStepValidation.RemoveFloatingPointErrors(-0.1 - 0.2, null));

    [Fact(DisplayName = "preserves precision finer than 3 fraction digits without a format")]
    [Trait("Requirement", "NUM-STA-11")]
    public void Preserves_precision_finer_than_3_fraction_digits_without_a_format()
    {
        Assert.Equal(0.0005, NumberStepValidation.RemoveFloatingPointErrors(0.0005, null));
        Assert.Equal(1.23456, NumberStepValidation.RemoveFloatingPointErrors(1.23456, null));
    }

    [Fact(DisplayName = "preserves safe integers exactly without a format")]
    [Trait("Requirement", "NUM-STA-11")]
    public void Preserves_safe_integers_exactly_without_a_format()
    {
        Assert.Equal(Max, NumberStepValidation.RemoveFloatingPointErrors(Max, null));
        Assert.Equal(Min, NumberStepValidation.RemoveFloatingPointErrors(Min, null));
    }

    [Fact(DisplayName = "cleans 16-digit noise patterns from arithmetic")]
    [Trait("Requirement", "NUM-STA-11")]
    public void Cleans_16_digit_noise_patterns_from_arithmetic()
    {
        Assert.Equal(0.8, NumberStepValidation.RemoveFloatingPointErrors(0.1 + 0.7, null));
        Assert.Equal(0.6, NumberStepValidation.RemoveFloatingPointErrors(0.1 + 0.2 + 0.3, null));
    }

    [Fact(DisplayName = "leaves large-magnitude noise uncleaned once one ULP exceeds the absolute cap")]
    [Trait("Requirement", "NUM-STA-11")]
    public void Leaves_large_magnitude_noise_uncleaned_once_one_ULP_exceeds_the_absolute_cap()
    {
        double sum = Add(1000000.1, 0.2);
        Assert.Equal(sum, NumberStepValidation.RemoveFloatingPointErrors(sum, null));
        Assert.NotEqual(1000000.3, NumberStepValidation.RemoveFloatingPointErrors(sum, null));
    }

    [Fact(DisplayName = "returns non-finite values untouched")]
    [Trait("Requirement", "NUM-STA-11")]
    public void Returns_non_finite_values_untouched()
    {
        Assert.Equal(double.PositiveInfinity, NumberStepValidation.RemoveFloatingPointErrors(double.PositiveInfinity, null));
        Assert.Equal(double.NegativeInfinity, NumberStepValidation.RemoveFloatingPointErrors(double.NegativeInfinity, null));
        Assert.True(double.IsNaN(NumberStepValidation.RemoveFloatingPointErrors(double.NaN, null)));
    }

    [Fact(DisplayName = "rounds compact notation against its standard equivalent")]
    [Trait("Requirement", "NUM-STA-12")]
    public void Rounds_compact_notation_against_its_standard_equivalent()
    {
        Assert.Equal(1234.6, Clean(1234.567, new() { Notation = NumberFormatNotation.Compact, MaximumFractionDigits = 1 }));
    }

    [Fact(DisplayName = "returns 0.3 for 0.2 + 0.1 with maximumFractionDigits")]
    [Trait("Requirement", "NUM-STA-12")]
    public void Returns_0_3_for_0_2_plus_0_1_with_maximumFractionDigits() => Assert.Equal(0.3, Clean(0.2 + 0.1, new() { MaximumFractionDigits = 1 }));

    [Fact(DisplayName = "respects roundingMode when maximumFractionDigits is provided")]
    [Trait("Requirement", "NUM-STA-12")]
    public void Respects_roundingMode_when_maximumFractionDigits_is_provided()
    {
        Assert.Equal(1.23, Clean(1.239, new() { MaximumFractionDigits = 2, RoundingMode = NumberFormatRoundingMode.Floor }));
    }

    [Fact(DisplayName = "respects half-even rounding at ties")]
    [Trait("Requirement", "NUM-STA-12")]
    public void Respects_half_even_rounding_at_ties()
    {
        Assert.Equal(1.24, Clean(1.235, new() { MaximumFractionDigits = 2, RoundingMode = NumberFormatRoundingMode.HalfEven }));
        Assert.Equal(1.24, Clean(1.245, new() { MaximumFractionDigits = 2, RoundingMode = NumberFormatRoundingMode.HalfEven }));
    }

    [Fact(DisplayName = "respects rounding mode differences for negative values")]
    [Trait("Requirement", "NUM-STA-12")]
    public void Respects_rounding_mode_differences_for_negative_values()
    {
        Assert.Equal(-1.24, Clean(-1.239, new() { MaximumFractionDigits = 2, RoundingMode = NumberFormatRoundingMode.Floor }));
        Assert.Equal(-1.23, Clean(-1.239, new() { MaximumFractionDigits = 2, RoundingMode = NumberFormatRoundingMode.Trunc }));
    }

    [Fact(DisplayName = "rounds percent values at display scale when maximumFractionDigits is provided")]
    [Trait("Requirement", "NUM-STA-12")]
    public void Rounds_percent_values_at_display_scale_when_maximumFractionDigits_is_provided()
    {
        Assert.Equal(0.0124, Clean(0.01236, new() { Style = NumberFormatStyle.Percent, MaximumFractionDigits = 2 }));
        Assert.Equal(0.0123, Clean(0.01239, new() { Style = NumberFormatStyle.Percent, MaximumFractionDigits = 2, RoundingMode = NumberFormatRoundingMode.Floor }));
    }

    [Theory(DisplayName = "preserves exact percent precision boundaries with roundingMode: %s")]
    [InlineData(NumberFormatRoundingMode.Floor)]
    [InlineData(NumberFormatRoundingMode.Ceil)]
    [InlineData(NumberFormatRoundingMode.Trunc)]
    [InlineData(NumberFormatRoundingMode.Expand)]
    [Trait("Requirement", "NUM-STA-12")]
    public void Preserves_exact_percent_precision_boundaries_with_roundingMode(NumberFormatRoundingMode roundingMode)
    {
        Assert.Equal(0.0046, Clean(0.0046, new() { Style = NumberFormatStyle.Percent, MaximumFractionDigits = 2, RoundingMode = roundingMode }));
    }

    [Fact(DisplayName = "rounds percent values at display scale when only minimumFractionDigits is provided")]
    [Trait("Requirement", "NUM-STA-12")]
    public void Rounds_percent_values_at_display_scale_when_only_minimumFractionDigits_is_provided()
    {
        Assert.Equal(0.0123, Clean(0.01239, new() { Style = NumberFormatStyle.Percent, MinimumFractionDigits = 2, RoundingMode = NumberFormatRoundingMode.Floor }));
    }

    [Fact(DisplayName = "rounds values with significant digit precision")]
    [Trait("Requirement", "NUM-STA-12")]
    public void Rounds_values_with_significant_digit_precision()
    {
        Assert.Equal(12300, Clean(12345, new() { MaximumSignificantDigits = 3, RoundingMode = NumberFormatRoundingMode.Floor }));
    }

    [Fact(DisplayName = "rounds percent values at display scale when significant digits are provided")]
    [Trait("Requirement", "NUM-STA-12")]
    public void Rounds_percent_values_at_display_scale_when_significant_digits_are_provided()
    {
        Assert.Equal(0.0123, Clean(0.01239, new() { Style = NumberFormatStyle.Percent, MaximumSignificantDigits = 3, RoundingMode = NumberFormatRoundingMode.Floor }));
    }

    [Fact(DisplayName = "rounds percent significant digit values without reintroducing binary noise")]
    [Trait("Requirement", "NUM-STA-12")]
    public void Rounds_percent_significant_digit_values_without_reintroducing_binary_noise()
    {
        var format = new NumberFormatOptions { Style = NumberFormatStyle.Percent, MaximumSignificantDigits = 2, RoundingMode = NumberFormatRoundingMode.Floor };
        double rounded = Clean(0.009995, format);

        Assert.Equal(0.0099, rounded);
        Assert.Equal(FormatEn(0.009995, format), FormatEn(rounded, format));
    }

    [Fact(DisplayName = "preserves tiny percent significant digit values after scaling back")]
    [Trait("Requirement", "NUM-STA-12")]
    public void Preserves_tiny_percent_significant_digit_values_after_scaling_back()
    {
        var format = new NumberFormatOptions { Style = NumberFormatStyle.Percent, MaximumSignificantDigits = 2 };
        const double value = 0.000001234;
        double rounded = Clean(value, format);

        Assert.Equal(0.0000012, rounded);
        Assert.Equal(FormatEn(value, format), FormatEn(rounded, format));
    }

    [Fact(DisplayName = "preserves meaningful percent precision above directional rounding boundaries")]
    [Trait("Requirement", "NUM-STA-12")]
    public void Preserves_meaningful_percent_precision_above_directional_rounding_boundaries()
    {
        var format = new NumberFormatOptions { Style = NumberFormatStyle.Percent, MaximumFractionDigits = 2, RoundingMode = NumberFormatRoundingMode.Ceil };
        double rounded = Clean(0.01230000001, format);

        Assert.Equal(0.0124, rounded);
        Assert.Equal(FormatEn(0.01230000001, format), FormatEn(rounded, format));
    }

    [Fact(DisplayName = "preserves high-precision percent fraction digits")]
    [Trait("Requirement", "NUM-STA-12")]
    public void Preserves_high_precision_percent_fraction_digits()
    {
        const double value = 0.001234567890123456;
        Assert.Equal(value, Clean(value, new() { Style = NumberFormatStyle.Percent, MaximumFractionDigits = 16 }));
    }

    [Fact(DisplayName = "preserves high-precision percent boundaries with directional rounding")]
    [Trait("Requirement", "NUM-STA-12")]
    public void Preserves_high_precision_percent_boundaries_with_directional_rounding()
    {
        var format = new NumberFormatOptions { Style = NumberFormatStyle.Percent, MaximumFractionDigits = 16, RoundingMode = NumberFormatRoundingMode.Floor };
        const double value = 0.0046;
        double rounded = Clean(value, format);

        Assert.Equal(value, rounded);
        Assert.Equal(FormatEn(value, format), FormatEn(rounded, format));
    }

    [Fact(DisplayName = "preserves tiny values when Intl supports more than 20 fraction digits")]
    [Trait("Requirement", "NUM-STA-12")]
    public void Preserves_tiny_values_when_Intl_supports_more_than_20_fraction_digits() => Assert.Equal(1e-21, Clean(1e-21, new() { MaximumFractionDigits = 21 }));

    [Fact(DisplayName = "uses percent fraction defaults when significant digits use roundingPriority")]
    [Trait("Requirement", "NUM-STA-12")]
    public void Uses_percent_fraction_defaults_when_significant_digits_use_roundingPriority()
    {
        var format = new NumberFormatOptions
        {
            Style = NumberFormatStyle.Percent,
            MaximumSignificantDigits = 3,
            RoundingPriority = NumberFormatRoundingPriority.MorePrecision,
        };
        double rounded = Clean(0.0123456, format);

        Assert.Equal(0.0123, rounded);
        Assert.Equal(FormatEn(0.0123456, format), FormatEn(rounded, format));
    }

    [Fact(DisplayName = "respects roundingPriority when fraction and significant digits are provided")]
    [Trait("Requirement", "NUM-STA-12")]
    public void Respects_roundingPriority_when_fraction_and_significant_digits_are_provided()
    {
        Assert.Equal(1.239, Clean(1.2399, new()
        {
            MinimumFractionDigits = 2,
            MaximumSignificantDigits = 3,
            RoundingMode = NumberFormatRoundingMode.Floor,
            RoundingPriority = NumberFormatRoundingPriority.MorePrecision,
        }));
    }

    [Fact(DisplayName = "does not scale unit percent values when maximumFractionDigits is provided")]
    [Trait("Requirement", "NUM-STA-12")]
    public void Does_not_scale_unit_percent_values_when_maximumFractionDigits_is_provided()
    {
        Assert.Equal(1.23, Clean(1.239, new()
        {
            Style = NumberFormatStyle.Unit,
            Unit = NumberFormatUnit.Percent,
            MaximumFractionDigits = 2,
            RoundingMode = NumberFormatRoundingMode.Floor,
        }));
    }

    [Fact(DisplayName = "rounds currency values without percent scaling")]
    [Trait("Requirement", "NUM-STA-12")]
    public void Rounds_currency_values_without_percent_scaling()
    {
        Assert.Equal(1.23, Clean(1.239, new()
        {
            Style = NumberFormatStyle.Currency,
            Currency = "USD",
            MaximumFractionDigits = 2,
            RoundingMode = NumberFormatRoundingMode.Floor,
        }));
    }

    [Fact(DisplayName = "rounds scientific currency code values")]
    [Trait("Requirement", "NUM-STA-12")]
    public void Rounds_scientific_currency_code_values()
    {
        Assert.Equal(12300, Clean(12345, new()
        {
            Style = NumberFormatStyle.Currency,
            Currency = "EUR",
            CurrencyDisplay = NumberFormatCurrencyDisplay.Code,
            Notation = NumberFormatNotation.Scientific,
            MaximumFractionDigits = 2,
        }));
    }

    [Fact(DisplayName = "preserves negative values when signDisplay hides the sign")]
    [Trait("Requirement", "NUM-STA-12")]
    public void Preserves_negative_values_when_signDisplay_hides_the_sign()
    {
        var format = new NumberFormatOptions { MaximumFractionDigits = 2, SignDisplay = NumberFormatSignDisplay.Never };
        double rounded = Clean(-1.239, format);

        Assert.Equal(-1.24, rounded);
        Assert.Equal(FormatEn(-1.239, format), FormatEn(rounded, format));
    }

    [Fact(DisplayName = "rounds negative accounting currency values")]
    [Trait("Requirement", "NUM-STA-12")]
    public void Rounds_negative_accounting_currency_values()
    {
        var format = new NumberFormatOptions
        {
            Style = NumberFormatStyle.Currency,
            Currency = "USD",
            CurrencySign = NumberFormatCurrencySign.Accounting,
            MaximumFractionDigits = 2,
        };
        double rounded = Clean(-1.239, format);

        Assert.Equal(-1.24, rounded);
        Assert.Equal(FormatEn(-1.239, format), FormatEn(rounded, format));
    }

    [Fact(DisplayName = "respects roundingMode when no precision is provided")]
    [Trait("Requirement", "NUM-STA-12")]
    public void Respects_roundingMode_when_no_precision_is_provided()
    {
        Assert.Equal(1.239, Clean(1.2399, new() { MinimumIntegerDigits = 1, RoundingMode = NumberFormatRoundingMode.Floor }));
    }

    [Fact(DisplayName = "respects roundingIncrement when maximumFractionDigits is provided")]
    [Trait("Requirement", "NUM-STA-12")]
    public void Respects_roundingIncrement_when_maximumFractionDigits_is_provided()
    {
        Assert.Equal(1.5, Clean(1.26, new() { MinimumFractionDigits = 1, MaximumFractionDigits = 1, RoundingIncrement = 5 }));
    }

    [Fact(DisplayName = "keeps the original finite value when percent scaling overflows before Intl rounding")]
    [Trait("Requirement", "NUM-STA-12")]
    public void Keeps_the_original_finite_value_when_percent_scaling_overflows_before_Intl_rounding()
    {
        Assert.Equal(double.MaxValue, Clean(double.MaxValue, new()
        {
            Style = NumberFormatStyle.Percent,
            MaximumFractionDigits = 2,
            RoundingMode = NumberFormatRoundingMode.Floor,
        }));
    }

    [Fact(DisplayName = "rounds scientific notation values at their formatted scale")]
    [Trait("Requirement", "NUM-STA-12")]
    public void Rounds_scientific_notation_values_at_their_formatted_scale()
    {
        var format = new NumberFormatOptions { Notation = NumberFormatNotation.Scientific, MaximumFractionDigits = 2 };
        double rounded = Clean(0.000123456, format);

        Assert.Equal(0.000123, rounded);
        Assert.Equal(FormatEn(0.000123456, format), FormatEn(rounded, format));
    }

    [Fact(DisplayName = "preserves non-invertible scientific rounded zero buckets")]
    [Trait("Requirement", "NUM-STA-12")]
    public void Preserves_non_invertible_scientific_rounded_zero_buckets()
    {
        var format = new NumberFormatOptions
        {
            Notation = NumberFormatNotation.Scientific,
            MinimumFractionDigits = 0,
            MaximumFractionDigits = 0,
            RoundingIncrement = 5,
        };
        const double value = 12345;
        double rounded = Clean(value, format);

        Assert.Equal(value, rounded);
        Assert.Equal(FormatEn(value, format), FormatEn(rounded, format));
    }

    [Fact(DisplayName = "rounds invertible scientific zero buckets")]
    [Trait("Requirement", "NUM-STA-12")]
    public void Rounds_invertible_scientific_zero_buckets()
    {
        Assert.Equal(0, Clean(1, new()
        {
            Notation = NumberFormatNotation.Scientific,
            MinimumFractionDigits = 0,
            MaximumFractionDigits = 0,
            RoundingIncrement = 5,
        }));
    }

    [Fact(DisplayName = "returns 1000 for 1000, ignoring grouping")]
    [Trait("Requirement", "NUM-STA-11")]
    public void Returns_1000_for_1000_ignoring_grouping() => Assert.Equal(1000, NumberStepValidation.RemoveFloatingPointErrors(1000, null));

    [Fact(DisplayName = "ignores formatting style")]
    [Trait("Requirement", "NUM-STA-10")]
    public void Ignores_formatting_style() => Assert.Equal(1000, Clean(1000, new() { Style = NumberFormatStyle.Currency, Currency = "USD" }));

    [Fact(DisplayName = "uses resolved maximumFractionDigits when only minimum is provided")]
    [Trait("Requirement", "NUM-STA-12")]
    public void Uses_resolved_maximumFractionDigits_when_only_minimum_is_provided() => Assert.Equal(1.23457, Clean(1.234567, new() { MinimumFractionDigits = 5 }));

    [Fact(DisplayName = "returns null when value is null")]
    [Trait("Requirement", "NUM-STA-07")]
    public void Returns_null_when_value_is_null()
    {
        Assert.Equal(5, Validate(5, s_default));
        Assert.Null(Validate(null, s_default));
    }

    [Fact(DisplayName = "skips clamping when clamp is false")]
    [Trait("Requirement", "NUM-STA-07")]
    public void Skips_clamping_when_clamp_is_false()
    {
        Assert.Equal(12, Validate(12, s_default with { MinWithDefault = 0, MaxWithDefault = 10, Step = null, SnapOnStep = false, Clamp = false }));
    }

    [Fact(DisplayName = "preserves parsed input beyond 15 significant digits when step is undefined")]
    [Trait("Requirement", "NUM-STA-10")]
    public void Preserves_parsed_input_beyond_15_significant_digits_when_step_is_undefined()
    {
        Assert.Equal(1.234567890123456, Validate(1.234567890123456, s_default with { Step = null }));
        Assert.Equal(0.1234567890123456, Validate(0.1234567890123456, s_default with { Step = null }));
    }

    [Fact(DisplayName = "cleans arithmetic noise when stepping")]
    [Trait("Requirement", "NUM-STA-11")]
    public void Cleans_arithmetic_noise_when_stepping() => Assert.Equal(0.8, Validate(0.1 + 0.7, s_default with { Step = 0.1, SnapOnStep = false }));

    [Fact(DisplayName = "preserves large fractional values when stepping cleanup would be too coarse")]
    [Trait("Requirement", "NUM-STA-11")]
    public void Preserves_large_fractional_values_when_stepping_cleanup_would_be_too_coarse()
    {
        double steppedValue = Add(100000000000000.1, 0.1);
        Assert.Equal(steppedValue, Validate(steppedValue, s_default with { Step = 0.1, SnapOnStep = false }));
    }

    [Fact(DisplayName = "preserves high-significance step values")]
    [Trait("Requirement", "NUM-STA-11")]
    public void Preserves_high_significance_step_values()
    {
        const double step = 0.1234567890123456;
        Assert.Equal(step, Validate(step, s_default with { Step = step, SnapOnStep = false }));
    }

    [Fact(DisplayName = "snaps 5 to 5 when step is 1")]
    [Trait("Requirement", "NUM-STA-08")]
    public void Snaps_5_to_5_when_step_is_1() => Assert.Equal(5, Validate(5, s_default));

    [Fact(DisplayName = "snaps 5.5 to 5 when step is 1")]
    [Trait("Requirement", "NUM-STA-08")]
    public void Snaps_5_5_to_5_when_step_is_1() => Assert.Equal(5, Validate(5.5, s_default));

    [Fact(DisplayName = "snaps -0.3 to -1 when step is 1")]
    [Trait("Requirement", "NUM-STA-08")]
    public void Snaps_minus_0_3_to_minus_1_when_step_is_1() => Assert.Equal(-1, Validate(-0.3, s_default));

    [Fact(DisplayName = "be same value when step is undefined and within bounds")]
    [Trait("Requirement", "NUM-STA-08")]
    public void Be_same_value_when_step_is_undefined_and_within_bounds_incrementing() => Assert.Equal(5.5, Validate(5.5, s_default with { Step = null }));

    [Fact(DisplayName = "snaps 9 to 5 when step is 5 and within bounds")]
    [Trait("Requirement", "NUM-STA-08")]
    public void Snaps_9_to_5_when_step_is_5_and_within_bounds() => Assert.Equal(5, Validate(9, s_default with { Step = 5 }));

    [Fact(DisplayName = "snaps 12 to 10 when step is 5 and within bounds")]
    [Trait("Requirement", "NUM-STA-08")]
    public void Snaps_12_to_10_when_step_is_5_and_within_bounds() => Assert.Equal(10, Validate(12, s_default with { Step = 5 }));

    [Fact(DisplayName = "preserves exact value when snapOnStep is false")]
    [Trait("Requirement", "NUM-STA-08")]
    public void Preserves_exact_value_when_snapOnStep_is_false_incrementing() => Assert.Equal(9.7, Validate(9.7, s_default with { Step = 5, SnapOnStep = false }));

    [Fact(DisplayName = "snaps 5 to 5 when step is -1")]
    [Trait("Requirement", "NUM-STA-08")]
    public void Snaps_5_to_5_when_step_is_minus_1() => Assert.Equal(5, Validate(5, s_default with { Step = -1 }));

    [Fact(DisplayName = "snaps 5.5 to 6 when step is -1")]
    [Trait("Requirement", "NUM-STA-08")]
    public void Snaps_5_5_to_6_when_step_is_minus_1() => Assert.Equal(6, Validate(5.5, s_default with { Step = -1 }));

    [Fact(DisplayName = "snaps -0.3 to 0 when step is -1")]
    [Trait("Requirement", "NUM-STA-08")]
    public void Snaps_minus_0_3_to_0_when_step_is_minus_1() => Assert.Equal(0, Validate(-0.3, s_default with { Step = -1 }));

    [Fact(DisplayName = "be same value when step is undefined and within bounds")]
    [Trait("Requirement", "NUM-STA-08")]
    public void Be_same_value_when_step_is_undefined_and_within_bounds_decrementing() => Assert.Equal(5.5, Validate(5.5, s_default with { Step = null }));

    [Fact(DisplayName = "snaps 9 to 10 when step is -5")]
    [Trait("Requirement", "NUM-STA-08")]
    public void Snaps_9_to_10_when_step_is_minus_5() => Assert.Equal(10, Validate(9, s_default with { Step = -5 }));

    [Fact(DisplayName = "snaps 12 to 15 when step is -5")]
    [Trait("Requirement", "NUM-STA-08")]
    public void Snaps_12_to_15_when_step_is_minus_5() => Assert.Equal(15, Validate(12, s_default with { Step = -5 }));

    [Fact(DisplayName = "preserves exact value when snapOnStep is false")]
    [Trait("Requirement", "NUM-STA-08")]
    public void Preserves_exact_value_when_snapOnStep_is_false_decrementing() => Assert.Equal(12.3, Validate(12.3, s_default with { Step = -5, SnapOnStep = false }));

    [Fact(DisplayName = "applies roundingMode after step validation")]
    [Trait("Requirement", "NUM-STA-09")]
    public void Applies_roundingMode_after_step_validation()
    {
        Assert.Equal(1.23, Validate(1.239, s_default with
        {
            Step = 0.001,
            SnapOnStep = true,
            Format = new() { MaximumFractionDigits = 2, RoundingMode = NumberFormatRoundingMode.Floor },
        }));
    }

    [Fact(DisplayName = "clamps the final value after percent rounding crosses max")]
    [Trait("Requirement", "NUM-STA-09")]
    public void Clamps_the_final_value_after_percent_rounding_crosses_max()
    {
        Assert.Equal(0.01235, Validate(0.01236, s_default with
        {
            Step = null,
            SnapOnStep = false,
            MaxWithDefault = 0.01235,
            Format = new() { Style = NumberFormatStyle.Percent, MaximumFractionDigits = 2 },
        }));
    }

    [Fact(DisplayName = "clamps the final value after directional percent rounding crosses min")]
    [Trait("Requirement", "NUM-STA-09")]
    public void Clamps_the_final_value_after_directional_percent_rounding_crosses_min()
    {
        Assert.Equal(0.01235, Validate(0.01234, s_default with
        {
            Step = null,
            SnapOnStep = false,
            MinWithDefault = 0.01235,
            MinWithZeroDefault = 0.01235,
            Format = new() { Style = NumberFormatStyle.Percent, MaximumFractionDigits = 2, RoundingMode = NumberFormatRoundingMode.Floor },
        }));
    }

    [Fact(DisplayName = "removes floating point errors when stepping")]
    [Trait("Requirement", "NUM-STA-11")]
    public void Removes_floating_point_errors_when_stepping() => Assert.Equal(0.3, Validate(0.2 + 0.1, s_default with { Step = 0.1, SnapOnStep = false }));

    [Fact(DisplayName = "clamps before rounding for non-integer bounds")]
    [Trait("Requirement", "NUM-STA-09")]
    public void Clamps_before_rounding_for_non_integer_bounds()
    {
        Assert.Equal(1, Validate(0.4, s_default with
        {
            Step = null,
            SnapOnStep = false,
            MinWithDefault = 0.6,
            MinWithZeroDefault = 0,
            MaxWithDefault = 10,
            Format = new() { MaximumFractionDigits = 0 },
        }));
    }

    [Fact(DisplayName = "handles increment with step 0.1 without getting stuck")]
    [Trait("Requirement", "NUM-STA-08")]
    public void Handles_increment_with_step_0_1_without_getting_stuck() => Assert.Equal(100.2, Validate(Add(100.1, 0.1), s_default with { Step = 0.1, SnapOnStep = true }));

    [Fact(DisplayName = "handles decrement with step -0.1 without getting stuck")]
    [Trait("Requirement", "NUM-STA-08")]
    public void Handles_decrement_with_step_minus_0_1_without_getting_stuck() => Assert.Equal(100, Validate(Add(100.1, -0.1), s_default with { Step = -0.1, SnapOnStep = true }));

    [Fact(DisplayName = "handles multiple increments with step 0.01")]
    [Trait("Requirement", "NUM-STA-08")]
    public void Handles_multiple_increments_with_step_0_01() => Assert.Equal(0.02, Validate(Add(0.01, 0.01), s_default with { Step = 0.01, SnapOnStep = true }));

    [Fact(DisplayName = "handles fractional step when a minimum is set")]
    [Trait("Requirement", "NUM-STA-08")]
    public void Handles_fractional_step_when_a_minimum_is_set()
    {
        Assert.Equal(3.4, Validate(Add(Add(3, 0.2), 0.2), s_default with { Step = 0.2, MinWithDefault = 3, MinWithZeroDefault = 3 }));
    }

    [Fact(DisplayName = "rounds to the nearest value when using small step")]
    [Trait("Requirement", "NUM-STA-08")]
    public void Rounds_to_the_nearest_value_when_using_small_step() => Assert.Equal(0.2, Validate(0.15, s_default with { Step = 0.1, SnapOnStep = true, Small = true }));

    [Fact(DisplayName = "rounds negative small steps to the nearest value")]
    [Trait("Requirement", "NUM-STA-08")]
    public void Rounds_negative_small_steps_to_the_nearest_value() => Assert.Equal(-0.2, Validate(-0.15, s_default with { Step = -0.1, SnapOnStep = true, Small = true }));

    [Fact(DisplayName = "keeps rounded out-of-range values when clamping is disabled")]
    [Trait("Requirement", "NUM-STA-07")]
    public void Keeps_rounded_out_of_range_values_when_clamping_is_disabled()
    {
        Assert.Equal(12.35, Validate(12.349, s_default with
        {
            Step = null,
            SnapOnStep = false,
            Format = new() { MaximumFractionDigits = 2 },
            MinWithDefault = 0,
            MinWithZeroDefault = 0,
            MaxWithDefault = 10,
            Clamp = false,
        }));
    }

    [Fact(DisplayName = "clamps to a non-step-aligned max after snapping so the boundary is reachable")]
    [Trait("Requirement", "NUM-STA-09")]
    public void Clamps_to_a_non_step_aligned_max_after_snapping_so_the_boundary_is_reachable()
    {
        Assert.Equal(10, Validate(13, s_default with { Step = 3, MinWithDefault = 0, MinWithZeroDefault = 0, MaxWithDefault = 10 }));
    }

    [Fact(DisplayName = "clamps to a non-step-aligned min after snapping so the boundary is reachable")]
    [Trait("Requirement", "NUM-STA-09")]
    public void Clamps_to_a_non_step_aligned_min_after_snapping_so_the_boundary_is_reachable()
    {
        Assert.Equal(-10, Validate(-13, s_default with { Step = -3, MinWithDefault = -10, MinWithZeroDefault = -10, MaxWithDefault = 0 }));
    }

    // Keeps a sum out of the compiler's constant folding, so it is computed with the run-time double addition, as in the
    // browser (C# folds constants with the same IEEE arithmetic, so this documents intent rather than changing a result).
    private static double Add(double a, double b) => a + b;

    private static double Clean(double value, NumberFormatOptions format) => NumberStepValidation.RemoveFloatingPointErrors(value, format);

    private static string FormatEn(double value, NumberFormatOptions format) => NumberFormatter.Get(s_enUs, format).Format(value);

    private static double? Validate(double? value, Options options) => NumberStepValidation.ToValidatedNumber(
        value,
        options.Step,
        options.MinWithDefault,
        options.MaxWithDefault,
        options.MinWithZeroDefault,
        options.Format,
        options.SnapOnStep,
        options.Small,
        options.Clamp);

    // The adapter of validate.test.ts over the positional signature.
    private sealed record Options(
        double? Step,
        double MinWithDefault,
        double MaxWithDefault,
        double MinWithZeroDefault,
        NumberFormatOptions? Format,
        bool SnapOnStep,
        bool Small,
        bool Clamp);
}

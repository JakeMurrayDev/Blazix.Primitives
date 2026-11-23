using System.Globalization;
using Blazix.Primitives.Utilities;

// Family tests use the project's root namespace.
namespace Blazix.Primitives.Tests;

// meter/root/MeterRoot.test.tsx and meter/indicator/MeterIndicator.test.tsx, the clauses MTR-ALG-01 and MTR-ALG-02 cite at L1:
// the pure computation behind aria-valuenow, the formatted text and the indicator width. The titles are Base UI's; the
// cultures are explicit so the .NET output is the same on every host (ADR 01 decision 2: equivalent, not byte-identical).
/// <summary>The percentage and clamped value of a meter (MTR-ALG-01, MTR-ALG-02) through the shared range helpers.</summary>
public sealed class MeterAlgorithmTests
{
    private static readonly CultureInfo s_enUs = CultureInfo.GetCultureInfo("en-US");

    [Fact(DisplayName = "formats the value as its position within a custom range and keeps the indicator in sync")]
    [Trait("Requirement", "MTR-ALG-01")]
    [Trait("Requirement", "MTR-ALG-03")]
    public void Formats_the_value_as_its_position_within_a_custom_range_and_keeps_the_indicator_in_sync()
    {
        double percentage = RangeMath.ClampedPercentage(0.5, 0, 1);
        double clamped = RangeMath.ClampedValue(0.5, 0, 1);

        Assert.Equal(50, percentage);
        Assert.Equal("50%", NumberFormatting.FormatMeasure(clamped, percentage, null, s_enUs));
        // aria-valuenow keeps the value as written, the indicator width the percentage (MTR-ALG-03).
        Assert.Equal("0.5", NumberSerializer.Serialize(clamped));
        Assert.Equal("50", NumberSerializer.Serialize(percentage));
    }

    [Fact(DisplayName = "formats the value relative to a non-zero min")]
    [Trait("Requirement", "MTR-ALG-01")]
    public void Formats_the_value_relative_to_a_non_zero_min()
    {
        double percentage = RangeMath.ClampedPercentage(30, 20, 40);

        Assert.Equal(50, percentage);
        Assert.Equal("50%", NumberFormatting.FormatMeasure(RangeMath.ClampedValue(30, 20, 40), percentage, null, s_enUs));
    }

    [Fact(DisplayName = "clamps the width to 100% when the value exceeds max")]
    [Trait("Requirement", "MTR-ALG-01")]
    public void Clamps_the_width_to_100_percent_when_the_value_exceeds_max()
    {
        Assert.Equal(100, RangeMath.ClampedPercentage(150, 0, 100));
        Assert.Equal("100", NumberSerializer.Serialize(RangeMath.ClampedPercentage(150, 0, 100)));
    }

    [Fact(DisplayName = "clamps the width to 0% when the value is below min")]
    [Trait("Requirement", "MTR-ALG-01")]
    public void Clamps_the_width_to_0_percent_when_the_value_is_below_min()
    {
        Assert.Equal(0, RangeMath.ClampedPercentage(-10, 0, 100));
        Assert.Equal("0", NumberSerializer.Serialize(RangeMath.ClampedPercentage(-10, 0, 100)));
    }

    [Fact(DisplayName = "produces a finite width when min equals max")]
    [Trait("Requirement", "MTR-ALG-01")]
    [Trait("Requirement", "MTR-EDGE-03")]
    public void Produces_a_finite_width_when_min_equals_max()
    {
        // 0 / 0 is NaN, which becomes 0; a value off the single point divides by zero and clamps to the edge.
        Assert.Equal(0, RangeMath.ClampedPercentage(5, 5, 5));
        Assert.Equal(100, RangeMath.ClampedPercentage(6, 5, 5));
        Assert.Equal(0, RangeMath.ClampedPercentage(4, 5, 5));
    }

    [Theory(DisplayName = "normalizes aria attributes when $label")]
    [Trait("Requirement", "MTR-ALG-02")]
    [Trait("Requirement", "MTR-EDGE-02")]
    [Trait("Requirement", "MTR-EDGE-03")]
    [Trait("Requirement", "MTR-EDGE-04")]
    [InlineData("value exceeds max", 150.0, 0.0, 100.0, 100.0, "100%")]
    [InlineData("value is below min", -10.0, 0.0, 100.0, 0.0, "0%")]
    [InlineData("min equals max", 5.0, 5.0, 5.0, 5.0, "0%")]
    [InlineData("value is NaN", double.NaN, 0.0, 100.0, 0.0, "0%")]
    public void Normalizes_aria_attributes(string label, double value, double min, double max, double expectedNow, string expectedText)
    {
        _ = label;

        double clamped = RangeMath.ClampedValue(value, min, max);
        double percentage = RangeMath.ClampedPercentage(value, min, max);

        Assert.Equal(expectedNow, clamped);
        Assert.Equal(expectedText, NumberFormatting.FormatMeasure(clamped, percentage, null, s_enUs));
    }

    [Fact(DisplayName = "formats the clamped value while clamping range attributes and indicator width")]
    [Trait("Requirement", "MTR-ALG-02")]
    [Trait("Requirement", "MTR-I18N-02")]
    [Trait("Requirement", "MTR-EDGE-04")]
    public void Formats_the_clamped_value_while_clamping_range_attributes_and_indicator_width()
    {
        double clamped = RangeMath.ClampedValue(150, 0, 100);
        double percentage = RangeMath.ClampedPercentage(150, 0, 100);

        Assert.Equal(100, clamped);
        Assert.Equal(100, percentage);
        // With a format the clamped value is formatted, not the percentage (MTR-API-04).
        Assert.Equal("$100.00", NumberFormatting.FormatMeasure(clamped, percentage, "C", s_enUs));
    }

    // MTR-EDGE-05: an inverted range is not validated; Clamp returns its lower bound (UTL-EDGE-14).
    [Fact(DisplayName = "follows the formulas literally for an inverted range")]
    [Trait("Requirement", "MTR-EDGE-05")]
    [Trait("Requirement", "UTL-EDGE-14")]
    public void Follows_the_formulas_literally_for_an_inverted_range()
    {
        Assert.Equal(70, RangeMath.ClampedPercentage(30, 100, 0));
        Assert.Equal(100, RangeMath.ClampedValue(30, 100, 0));
    }

    [Fact(DisplayName = "keeps infinities inside the percentage range")]
    [Trait("Requirement", "MTR-ALG-01")]
    public void Keeps_infinities_inside_the_percentage_range()
    {
        Assert.Equal(100, RangeMath.ClampedPercentage(double.PositiveInfinity, 0, 100));
        Assert.Equal(0, RangeMath.ClampedPercentage(double.NegativeInfinity, 0, 100));
        Assert.Equal(100, RangeMath.ClampedValue(double.PositiveInfinity, 0, 100));
        Assert.Equal(0, RangeMath.ClampedValue(double.NegativeInfinity, 0, 100));
    }

    [Fact(DisplayName = "clamps a value based on min and max")]
    [Trait("Requirement", "UTL-EDGE-14")]
    public void Clamps_a_value_based_on_min_and_max()
    {
        Assert.Equal(5, RangeMath.Clamp(5, 0, 10));
        Assert.Equal(0, RangeMath.Clamp(-5, 0, 10));
        Assert.Equal(10, RangeMath.Clamp(15, 0, 10));
        Assert.True(double.IsNaN(RangeMath.Clamp(double.NaN, 0, 10)));
        // Math.max(min, Math.min(value, max)) puts +0 ahead of -0.
        Assert.True(double.IsPositive(RangeMath.Clamp(-0.0, 0, 10)));
    }

    [Fact(DisplayName = "computes the percentage without clamping or guarding a zero range")]
    [Trait("Requirement", "UTL-DOM-31")]
    public void Computes_the_percentage_without_clamping_or_guarding_a_zero_range()
    {
        Assert.Equal(150, RangeMath.ValueToPercent(15, 0, 10));
        Assert.Equal(-50, RangeMath.ValueToPercent(-5, 0, 10));
        Assert.Equal(double.PositiveInfinity, RangeMath.ValueToPercent(6, 5, 5));
        Assert.True(double.IsNaN(RangeMath.ValueToPercent(5, 5, 5)));
    }
}

using System.Globalization;

// Family tests use the project's root namespace.
namespace Blazix.Primitives.Tests;

// progress/root/ProgressRoot.test.tsx, the clauses PRG-ALG-01 and PRG-ALG-02 cite at L1: the derivation of a progress root
// (status, percentage, clamped value, formatted value, attribute text) over the shared helpers of Utilities/ (Meter's
// RangeMath, NumberSerializer, NumberFormatting). The titles are Base UI's; the cultures are explicit so the .NET output
// is the same on every host (ADR 01 decision 2: equivalent, not byte-identical).
/// <summary>The values a progress root derives from its parameters (PRG-ALG-01..03, PRG-STA-02, PRG-STA-03, PRG-EDGE-02..05).</summary>
public sealed class ProgressDerivationTests
{
    private static readonly CultureInfo s_enUs = CultureInfo.GetCultureInfo("en-US");
    private static readonly CultureInfo s_deDe = CultureInfo.GetCultureInfo("de-DE");

    private static ProgressRootContext Derive(double? value, double min = 0, double max = 100, string? format = null, CultureInfo? culture = null)
    {
        ProgressRootContext context = new();
        context.Update(value, min, max, format, culture ?? s_enUs);
        return context;
    }

    [Fact(DisplayName = "normalizes the formatted value, aria-valuetext, and indicator within a custom range")]
    [Trait("Requirement", "PRG-ALG-01")]
    [Trait("Requirement", "PRG-STA-04")]
    [Trait("Requirement", "PRG-DOM-06")]
    public void Normalizes_the_formatted_value_aria_valuetext_and_indicator_within_a_custom_range()
    {
        ProgressRootContext context = Derive(30, min: 20, max: 40);

        Assert.Equal(50, context.Percentage);
        Assert.Equal("50%", context.FormattedValue);
        Assert.Equal("50%", context.DefaultAriaValueText);
        Assert.Equal("inset-inline-start: 0; height: inherit; width: 50%", context.IndicatorStyle);
    }

    [Fact(DisplayName = "clamps aria-valuenow, the value text, and the indicator when the value overshoots max")]
    [Trait("Requirement", "PRG-ALG-01")]
    [Trait("Requirement", "PRG-ALG-02")]
    [Trait("Requirement", "PRG-EDGE-03")]
    public void Clamps_aria_valuenow_the_value_text_and_the_indicator_when_the_value_overshoots_max()
    {
        ProgressRootContext context = Derive(50, min: 0, max: 40);

        Assert.Equal("40", context.ValueNowText);
        Assert.Equal("40", context.MaxText);
        Assert.Equal("100%", context.FormattedValue);
        Assert.Equal("100%", context.DefaultAriaValueText);
        Assert.Equal(100, context.Percentage);
        Assert.Equal("inset-inline-start: 0; height: inherit; width: 100%", context.IndicatorStyle);
    }

    [Fact(DisplayName = "clamps aria-valuenow, the value text, and the indicator when the value undershoots min")]
    [Trait("Requirement", "PRG-ALG-01")]
    [Trait("Requirement", "PRG-ALG-02")]
    [Trait("Requirement", "PRG-EDGE-03")]
    public void Clamps_aria_valuenow_the_value_text_and_the_indicator_when_the_value_undershoots_min()
    {
        ProgressRootContext context = Derive(10, min: 20, max: 40);

        Assert.Equal("20", context.ValueNowText);
        Assert.Equal("20", context.MinText);
        Assert.Equal("0%", context.FormattedValue);
        Assert.Equal(0, context.Percentage);
        Assert.Equal("inset-inline-start: 0; height: inherit; width: 0%", context.IndicatorStyle);
    }

    [Fact(DisplayName = "normalizes aria attributes when min equals max")]
    [Trait("Requirement", "PRG-ALG-01")]
    [Trait("Requirement", "PRG-EDGE-02")]
    public void Normalizes_aria_attributes_when_min_equals_max()
    {
        ProgressRootContext context = Derive(5, min: 5, max: 5);

        Assert.Equal("5", context.ValueNowText);
        Assert.Equal("0%", context.FormattedValue);
        Assert.Equal("0%", context.DefaultAriaValueText);
        Assert.Equal("inset-inline-start: 0; height: inherit; width: 0%", context.IndicatorStyle);
        // PRG-EDGE-02: the clamped value equals max, so the status is complete although the width is 0%.
        Assert.Equal(ProgressStatus.Complete, context.Status);
    }

    [Theory(DisplayName = "formats the clamped value $expectedValue when a custom-formatted value $value is outside the range")]
    [Trait("Requirement", "PRG-ALG-02")]
    [Trait("Requirement", "PRG-ALG-03")]
    [Trait("Requirement", "PRG-I18N-02")]
    [Trait("Requirement", "PRG-EDGE-03")]
    [InlineData(50.0, 40.0, "$40.00")]
    [InlineData(10.0, 20.0, "$20.00")]
    public void Formats_the_clamped_value_when_a_custom_formatted_value_is_outside_the_range(double value, double expectedValue, string expectedText)
    {
        ProgressRootContext context = Derive(value, min: 20, max: 40, format: "C");

        Assert.Equal(expectedText, context.FormattedValue);
        // PRG-ALG-03: aria-valuenow is String(expectedValue), the number unformatted.
        Assert.Equal(((int)expectedValue).ToString(CultureInfo.InvariantCulture), context.ValueNowText);
        Assert.Equal(value, context.Value);
    }

    // PRG-ARIA-04 samples through the shared formatting; Intl writes U+00A0 before the sign in de-DE where .NET writes an
    // ASCII space, an accepted divergence under ADR 01 decision 2 (PRG-I18N-01).
    [Fact(DisplayName = "formats 30 percent in de-DE with a space and 33.333 percent in en-US as 33%")]
    [Trait("Requirement", "PRG-ARIA-04")]
    [Trait("Requirement", "PRG-I18N-01")]
    public void Formats_30_percent_in_de_DE_with_a_space_and_33_333_percent_in_en_US_as_33_percent()
    {
        Assert.Equal("30 %", Derive(30, culture: s_deDe).DefaultAriaValueText);
        Assert.Equal("33%", Derive(33.333, culture: s_enUs).DefaultAriaValueText);
    }

    [Fact(DisplayName = "selects the culture for the formatted value")]
    [Trait("Requirement", "PRG-API-05")]
    [Trait("Requirement", "PRG-I18N-03")]
    public void Selects_the_culture_for_the_formatted_value()
    {
        Assert.Equal("70,51", Derive(70.51, format: "F2", culture: s_deDe).FormattedValue);
        Assert.Equal("70.51", Derive(70.51, format: "F2", culture: s_enUs).FormattedValue);
    }

    [Theory(DisplayName = "keeps non-finite value $label indeterminate")]
    [Trait("Requirement", "PRG-STA-02")]
    [Trait("Requirement", "PRG-EDGE-04")]
    [InlineData("null", null)]
    [InlineData("NaN", double.NaN)]
    [InlineData("+Infinity", double.PositiveInfinity)]
    [InlineData("-Infinity", double.NegativeInfinity)]
    public void Keeps_non_finite_value_indeterminate(string label, double? value)
    {
        _ = label;

        ProgressRootContext context = Derive(value);

        Assert.Equal(ProgressStatus.Indeterminate, context.Status);
        Assert.Null(context.ValueNowText);
        Assert.Null(context.IndicatorStyle);
        Assert.Equal(string.Empty, context.FormattedValue);
        Assert.Equal("indeterminate progress", context.DefaultAriaValueText);
        // PRG-EDGE-04: callbacks receive the raw value.
        Assert.Equal(value, context.Value);
        // PRG-ARIA-03: the range attributes stay in every status.
        Assert.Equal("0", context.MinText);
        Assert.Equal("100", context.MaxText);
    }

    [Theory(DisplayName = "reports complete when the clamped value reaches max")]
    [Trait("Requirement", "PRG-STA-03")]
    [Trait("Requirement", "PRG-EDGE-03")]
    [InlineData(0.0, 40.0, 45.0, ProgressStatus.Complete)]
    [InlineData(0.0, 40.0, 40.0, ProgressStatus.Complete)]
    [InlineData(0.0, 40.0, 39.5, ProgressStatus.Progressing)]
    [InlineData(0.0, 100.0, 0.0, ProgressStatus.Progressing)]
    [InlineData(20.0, 40.0, 10.0, ProgressStatus.Progressing)]
    public void Reports_complete_when_the_clamped_value_reaches_max(double min, double max, double value, ProgressStatus expected)
    {
        Assert.Equal(expected, Derive(value, min, max).Status);
    }

    [Fact(DisplayName = "follows an inverted range literally")]
    [Trait("Requirement", "PRG-EDGE-05")]
    public void Follows_an_inverted_range_literally()
    {
        ProgressRootContext context = Derive(30, min: 100, max: 0);

        // The clamp returns its lower bound: aria-valuenow is min, the percentage 70, and complete is unreachable.
        Assert.Equal("100", context.ValueNowText);
        Assert.Equal(70, context.Percentage);
        Assert.Equal(ProgressStatus.Progressing, context.Status);
    }

    [Fact(DisplayName = "writes the indicator width with full precision")]
    [Trait("Requirement", "PRG-ALG-03")]
    public void Writes_the_indicator_width_with_full_precision()
    {
        ProgressRootContext context = Derive(1, min: 0, max: 3);

        Assert.Equal("inset-inline-start: 0; height: inherit; width: 33.333333333333336%", context.IndicatorStyle);
    }

    [Fact(DisplayName = "keeps the English indeterminate text under any culture and format")]
    [Trait("Requirement", "PRG-I18N-05")]
    public void Keeps_the_English_indeterminate_text_under_any_culture_and_format()
    {
        Assert.Equal("indeterminate progress", Derive(null, culture: s_deDe).DefaultAriaValueText);
        Assert.Equal("indeterminate progress", Derive(null, format: "C", culture: s_deDe).DefaultAriaValueText);
    }

    [Fact(DisplayName = "reflects a changed format in the same update")]
    [Trait("Requirement", "PRG-I18N-02")]
    [Trait("Requirement", "PRG-API-01")]
    public void Reflects_a_changed_format_in_the_same_update()
    {
        ProgressRootContext context = new();
        context.Update(30, 0, 100, "C2", s_enUs);
        Assert.Equal("$30.00", context.FormattedValue);

        context.Update(30, 0, 100, "C0", s_enUs);

        Assert.Equal("$30", context.FormattedValue);
    }

    [Fact(DisplayName = "notifies the parts whose output changed along the status cycle")]
    [Trait("Requirement", "PRG-STA-04")]
    [Trait("Requirement", "PRG-DOM-02")]
    public void Notifies_the_parts_whose_output_changed_along_the_status_cycle()
    {
        ProgressRootContext context = new();
        context.Update(null, 0, 100, null, s_enUs);
        int status = 0;
        int indicator = 0;
        int value = 0;
        context.AddStatusListener(() => status++);
        context.AddIndicatorListener(() => indicator++);
        context.AddValueListener(() => value++);

        // indeterminate -> progressing: every kind of part changes.
        context.Update(50, 0, 100, null, s_enUs);
        Assert.Equal((1, 1, 1), (status, indicator, value));

        // the same inputs again: nothing changes.
        context.Update(50, 0, 100, null, s_enUs);
        Assert.Equal((1, 1, 1), (status, indicator, value));

        // progressing -> progressing with another value: the indicator and the value text, not the status attributes.
        context.Update(60, 0, 100, null, s_enUs);
        Assert.Equal((1, 2, 2), (status, indicator, value));

        // progressing -> complete.
        context.Update(100, 0, 100, null, s_enUs);
        Assert.Equal((2, 3, 3), (status, indicator, value));

        // complete -> indeterminate.
        context.Update(null, 0, 100, null, s_enUs);
        Assert.Equal((3, 4, 4), (status, indicator, value));
    }

    [Fact(DisplayName = "withdraws the label id only while it is still the tracked one")]
    [Trait("Requirement", "PRG-STA-05")]
    [Trait("Requirement", "FFI-ARIA-08")]
    public void Withdraws_the_label_id_only_while_it_is_still_the_tracked_one()
    {
        ProgressRootContext context = new();
        context.Label.Publish("older");
        context.Label.Publish("newer");

        context.Label.Withdraw("older");
        Assert.Equal("newer", context.Label.Id);

        context.Label.Withdraw("newer");
        Assert.Null(context.Label.Id);
    }
}

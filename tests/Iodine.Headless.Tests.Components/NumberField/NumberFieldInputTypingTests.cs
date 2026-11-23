using System.Globalization;
using System.Text.Json;
using Iodine.Headless.Utilities;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

// The namespace is the project's root: a namespace named after the family would hide the parts' types.
namespace Iodine.Headless.Tests.Components;

// 6b's input role at L2 (issue 277): NumberFieldInput.test.tsx's titles that type and blur, the step from smallStep and the
// precision titles. Base UI's fireEvent.change and blur are the `number-field` module's reports here (ADR 06 §1), and its
// user.keyboard is one change report per typed prefix, as the module reports each keystroke the gate lets through. A Base
// UI test's useState parent is the Host below: it mirrors onValueChange into `value` unless the test's parent does not.
// Intl.NumberFormat's expected texts are the port's formatter's (NUM-I18N-01), the cultures explicit.
/// <summary>Typing and blurring the number field input.</summary>
public sealed class NumberFieldInputTypingTests : IDisposable
{
    private static readonly CultureInfo s_enUs = CultureInfo.GetCultureInfo("en-US");

    private readonly NumberFieldTestContext test = new();

    /// <inheritdoc />
    public void Dispose() => test.Dispose();

    [Fact(DisplayName = "advances by a smallStep finer than 3 fraction digits")]
    [Trait("Requirement", "NUM-STA-11")]
    [Trait("Requirement", "NUM-STA-16")]
    public async Task Advances_by_a_smallStep_finer_than_3_fraction_digits()
    {
        IRenderedComponent<Host> cut = Render(0, p => p.Add(x => x.SmallStep, 0.0001));
        await test.FocusAsync(cut);

        await test.KeyAsync(cut, "ArrowUp", altKey: true);

        Assert.Equal(0.0001, cut.Instance.Changes[^1]);
        Assert.Equal("0", test.Text);
    }

    [Fact(DisplayName = "should commit validated number on blur (max)")]
    [Trait("Requirement", "NUM-EVT-06")]
    [Trait("Requirement", "NUM-API-08")]
    public async Task Should_commit_validated_number_on_blur_max()
    {
        IRenderedComponent<Host> cut = Render(null, p => p.Add(x => x.Uncontrolled, true).Add(x => x.Max, 0));
        await test.FocusAsync(cut);

        await test.ChangeAsync(cut, "1");
        Assert.Equal("1", test.Text);

        await BlurAsync(cut);
        Assert.Equal("0", test.Text);
    }

    [Fact(DisplayName = "should not snap number to step on blur")]
    [Trait("Requirement", "NUM-EVT-06")]
    [Trait("Requirement", "NUM-API-08")]
    public async Task Should_not_snap_number_to_step_on_blur()
    {
        IRenderedComponent<Host> cut = Render(null, p => p.Add(x => x.Uncontrolled, true).Add(x => x.Step, (StepValue?)0.5).Add(x => x.SnapOnStep, true));
        await test.FocusAsync(cut);

        // 1.3 is off the 0.5 step grid, so any snapping on blur would change it.
        await test.ChangeAsync(cut, "1.3");
        Assert.Equal("1.3", test.Text);

        await BlurAsync(cut);
        Assert.Equal("1.3", test.Text);
    }

    [Fact(DisplayName = "should commit validated number on blur (step and min)")]
    [Trait("Requirement", "NUM-EVT-06")]
    [Trait("Requirement", "NUM-API-08")]
    public async Task Should_commit_validated_number_on_blur_step_and_min()
    {
        IRenderedComponent<Host> cut = Render(null, p => p.Add(x => x.Uncontrolled, true).Add(x => x.Min, 2).Add(x => x.Step, (StepValue?)2));
        await test.FocusAsync(cut);

        await test.ChangeAsync(cut, "3");
        Assert.Equal("3", test.Text);

        await BlurAsync(cut);
        Assert.Equal("3", test.Text);
    }

    [Fact(DisplayName = "should allow typing after default formatting on blur")]
    [Trait("Requirement", "NUM-EVT-06")]
    [Trait("Requirement", "NUM-STA-06")]
    public async Task Should_allow_typing_after_default_formatting_on_blur()
    {
        IRenderedComponent<Host> cut = Render(null, p => p.Add(x => x.Mirror, false));

        await cut.InvokeAsync(() => cut.Instance.External(1.23456));
        Assert.Equal("1.235", test.Text);

        await test.FocusAsync(cut);
        await BlurAsync(cut);
        Assert.Equal("1.235", test.Text);

        await test.FocusAsync(cut);
        await test.ChangeAsync(cut, string.Empty);
        await TypeAsync(cut, "1.234567");
        Assert.Equal("1.234567", test.Text);

        await BlurAsync(cut);
        Assert.Equal("1.235", test.Text);
    }

    [Fact(DisplayName = "should format to default canonical representation on blur")]
    [Trait("Requirement", "NUM-EVT-06")]
    [Trait("Requirement", "NUM-STA-06")]
    public async Task Should_format_to_default_canonical_representation_on_blur()
    {
        IRenderedComponent<Host> cut = Render(null, p => p.Add(x => x.Mirror, false));

        await cut.InvokeAsync(() => cut.Instance.External(1.23456));
        Assert.Equal("1.235", test.Text);

        await test.FocusAsync(cut);
        await test.ChangeAsync(cut, string.Empty);
        await TypeAsync(cut, "1.23456000");
        Assert.Equal("1.23456000", test.Text);

        await BlurAsync(cut);
        Assert.Equal("1.235", test.Text);
    }

    [Fact(DisplayName = "should handle edge case where parsed value equals current value but input differs")]
    [Trait("Requirement", "NUM-EVT-06")]
    [Trait("Requirement", "NUM-STA-06")]
    public async Task Should_handle_edge_case_where_parsed_value_equals_current_value_but_input_differs()
    {
        IRenderedComponent<Host> cut = Render(null, p => p.Add(x => x.Mirror, false));

        await cut.InvokeAsync(() => cut.Instance.External(1.5));
        Assert.Equal("1.5", test.Text);

        await test.FocusAsync(cut);
        await test.ChangeAsync(cut, string.Empty);
        await TypeAsync(cut, "1.50");
        Assert.Equal("1.50", test.Text);

        await BlurAsync(cut);
        Assert.Matches("^1[.,]5", test.Text);
    }

    // §16 Q4: Base UI's user.click on the "set pi" button moves the focus first, so the input blurs before the value changes.
    [Fact(DisplayName = "should apply default formatting after external change during typing")]
    [Trait("Requirement", "NUM-STA-06")]
    public async Task Should_apply_default_formatting_after_external_change_during_typing()
    {
        IRenderedComponent<Host> cut = Render(null);
        await test.FocusAsync(cut);

        await TypeAsync(cut, "2.7");
        Assert.Equal("2.7", test.Text);

        await BlurAsync(cut);
        await cut.InvokeAsync(() => cut.Instance.External(3.14159265));
        Assert.Equal("3.142", test.Text);

        await BlurAsync(cut);
        Assert.Equal("3.142", test.Text);
    }

    [Fact(DisplayName = "should round to explicit maximumFractionDigits on blur")]
    [Trait("Requirement", "NUM-EVT-06")]
    [Trait("Requirement", "NUM-STA-12")]
    public async Task Should_round_to_explicit_maximumFractionDigits_on_blur()
    {
        IRenderedComponent<Host> cut = Render(null, p => p.Add(x => x.Mirror, false).Add(x => x.Format, new NumberFormatOptions { MaximumFractionDigits = 2 }));

        await cut.InvokeAsync(() => cut.Instance.External(1.23456));
        Assert.Equal("1.23", test.Text);

        await test.FocusAsync(cut);
        await BlurAsync(cut);

        Assert.Equal("1.23", test.Text);
        Assert.Equal(1.23, Assert.Single(cut.Instance.Changes));
    }

    [Theory(DisplayName = "should respect roundingMode when rounding to explicit maximumFractionDigits on blur in %s")]
    [InlineData("en-US", "1.239")]
    [InlineData("fr-FR", "1,239")]
    [InlineData("ar-EG", "١٫٢٣٩")]
    [Trait("Requirement", "NUM-EVT-06")]
    [Trait("Requirement", "NUM-STA-12")]
    public async Task Should_respect_roundingMode_when_rounding_to_explicit_maximumFractionDigits_on_blur(string locale, string inputText)
    {
        CultureInfo culture = CultureInfo.GetCultureInfo(locale);
        NumberFormatOptions format = new() { MaximumFractionDigits = 2, RoundingMode = NumberFormatRoundingMode.Floor };
        IRenderedComponent<Host> cut = await RenderControlledAsync(format, culture);

        await TypeAsync(cut, inputText);
        await BlurAsync(cut);

        Assert.Equal(1.23, cut.Instance.Changes[^1]);
        Assert.Equal(NumberFormatter.Get(culture, format).Format(1.239), test.Text);
    }

    [Fact(DisplayName = "should commit roundingIncrement values on blur")]
    [Trait("Requirement", "NUM-EVT-06")]
    [Trait("Requirement", "NUM-STA-12")]
    public async Task Should_commit_roundingIncrement_values_on_blur()
    {
        NumberFormatOptions format = new() { MinimumFractionDigits = 1, MaximumFractionDigits = 1, RoundingIncrement = 5 };
        IRenderedComponent<Host> cut = await RenderControlledAsync(format);

        await TypeAsync(cut, "1.26");
        Assert.Empty(cut.Instance.Commits);

        await BlurAsync(cut);

        Assert.Equal(1.5, cut.Instance.Changes[^1]);
        Assert.Equal(1.5, cut.Instance.Commits[^1]);
        Assert.Equal(NumberFormatter.Get(s_enUs, format).Format(1.26), test.Text);
    }

    [Fact(DisplayName = "should commit significant digit rounded values on blur")]
    [Trait("Requirement", "NUM-EVT-06")]
    [Trait("Requirement", "NUM-STA-12")]
    public async Task Should_commit_significant_digit_rounded_values_on_blur()
    {
        NumberFormatOptions format = new() { MaximumSignificantDigits = 3, RoundingMode = NumberFormatRoundingMode.Floor };
        IRenderedComponent<Host> cut = await RenderControlledAsync(format);

        await TypeAsync(cut, "12345");
        await BlurAsync(cut);

        Assert.Equal(12300, cut.Instance.Changes[^1]);
        Assert.Equal(12300, cut.Instance.Commits[^1]);
        Assert.Equal(NumberFormatter.Get(s_enUs, format).Format(12345), test.Text);
    }

    [Fact(DisplayName = "should preserve tiny percent significant digit rounded values on blur")]
    [Trait("Requirement", "NUM-EVT-06")]
    [Trait("Requirement", "NUM-STA-12")]
    public async Task Should_preserve_tiny_percent_significant_digit_rounded_values_on_blur()
    {
        NumberFormatOptions format = new() { Style = NumberFormatStyle.Percent, MaximumSignificantDigits = 2 };
        IRenderedComponent<Host> cut = await RenderControlledAsync(format);

        await TypeAsync(cut, "0.0001234%");
        await BlurAsync(cut);

        Assert.Equal(0.0000012, cut.Instance.Changes[^1]);
        Assert.Equal(0.0000012, cut.Instance.Commits[^1]);
        Assert.Equal(NumberFormatter.Get(s_enUs, format).Format(0.000001234), test.Text);
    }

    [Fact(DisplayName = "should commit roundingMode values on blur without explicit precision")]
    [Trait("Requirement", "NUM-EVT-06")]
    [Trait("Requirement", "NUM-STA-12")]
    public async Task Should_commit_roundingMode_values_on_blur_without_explicit_precision()
    {
        NumberFormatOptions format = new() { MinimumIntegerDigits = 1, RoundingMode = NumberFormatRoundingMode.Floor };
        IRenderedComponent<Host> cut = await RenderControlledAsync(format);

        await TypeAsync(cut, "1.2399");
        await BlurAsync(cut);

        Assert.Equal(1.239, cut.Instance.Changes[^1]);
        Assert.Equal(1.239, cut.Instance.Commits[^1]);
        Assert.Equal(NumberFormatter.Get(s_enUs, format).Format(1.2399), test.Text);
    }

    [Fact(DisplayName = "should format controlled values with rounding options after external value changes")]
    [Trait("Requirement", "NUM-STA-12")]
    [Trait("Requirement", "NUM-STA-06")]
    public async Task Should_format_controlled_values_with_rounding_options_after_external_value_changes()
    {
        NumberFormatOptions format = new() { MinimumIntegerDigits = 1, RoundingMode = NumberFormatRoundingMode.Floor };
        IRenderedComponent<Host> cut = Render(null, p => p.Add(x => x.Mirror, false).Add(x => x.Format, format));

        await cut.InvokeAsync(() => cut.Instance.External(1.2399));

        Assert.Equal(NumberFormatter.Get(s_enUs, format).Format(1.2399), test.Text);
    }

    [Theory(DisplayName = "should round %s values on blur")]
    [InlineData("percent", 0.0123)]
    [InlineData("percent with min-only precision", 0.0123)]
    [InlineData("unit percent", 1.23)]
    [Trait("Requirement", "NUM-EVT-06")]
    [Trait("Requirement", "NUM-STA-12")]
    public async Task Should_round_values_on_blur(string kind, double expectedValue)
    {
        NumberFormatOptions format = kind switch
        {
            "percent" => new() { Style = NumberFormatStyle.Percent, MaximumFractionDigits = 2, RoundingMode = NumberFormatRoundingMode.Floor },
            "percent with min-only precision" => new() { Style = NumberFormatStyle.Percent, MinimumFractionDigits = 2, RoundingMode = NumberFormatRoundingMode.Floor },
            _ => new() { Style = NumberFormatStyle.Unit, Unit = NumberFormatUnit.Percent, MaximumFractionDigits = 2, RoundingMode = NumberFormatRoundingMode.Floor },
        };
        IRenderedComponent<Host> cut = await RenderControlledAsync(format);

        await TypeAsync(cut, "1.239%");
        await BlurAsync(cut);

        Assert.Equal(expectedValue, cut.Instance.Changes[^1]);
        Assert.Equal("1.23%", test.Text);
    }

    [Fact(DisplayName = "should parse locale prefix percent values on blur")]
    [Trait("Requirement", "NUM-I18N-05")]
    [Trait("Requirement", "NUM-EVT-06")]
    public async Task Should_parse_locale_prefix_percent_values_on_blur()
    {
        CultureInfo trTr = CultureInfo.GetCultureInfo("tr-TR");
        NumberFormatOptions format = new() { Style = NumberFormatStyle.Percent, MaximumFractionDigits = 2 };
        string formatted = NumberFormatter.Get(trTr, format).Format(0.0123);
        IRenderedComponent<Host> cut = await RenderControlledAsync(format, trTr);

        await test.ChangeAsync(cut, formatted);
        await BlurAsync(cut);

        Assert.Equal(0.0123, cut.Instance.Changes[^1]);
        Assert.Equal(0.0123, cut.Instance.Commits[^1]);
        Assert.Equal(formatted, test.Text);
    }

    [Fact(DisplayName = "should preserve exact percent precision boundaries with directional roundingMode on blur")]
    [Trait("Requirement", "NUM-EVT-06")]
    [Trait("Requirement", "NUM-STA-12")]
    public async Task Should_preserve_exact_percent_precision_boundaries_with_directional_roundingMode_on_blur()
    {
        NumberFormatOptions format = new() { Style = NumberFormatStyle.Percent, MaximumFractionDigits = 2, RoundingMode = NumberFormatRoundingMode.Floor };
        IRenderedComponent<Host> cut = await RenderControlledAsync(format);

        await TypeAsync(cut, "0.46%");
        await BlurAsync(cut);

        Assert.Equal(0.0046, cut.Instance.Changes[^1]);
        Assert.Equal(0.0046, cut.Instance.Commits[^1]);
        Assert.Equal("0.46%", test.Text);
    }

    [Fact(DisplayName = "should preserve high-precision percent boundaries with directional roundingMode on blur")]
    [Trait("Requirement", "NUM-EVT-06")]
    [Trait("Requirement", "NUM-STA-12")]
    public async Task Should_preserve_high_precision_percent_boundaries_with_directional_roundingMode_on_blur()
    {
        NumberFormatOptions format = new() { Style = NumberFormatStyle.Percent, MaximumFractionDigits = 16, RoundingMode = NumberFormatRoundingMode.Floor };
        IRenderedComponent<Host> cut = await RenderControlledAsync(format);

        await TypeAsync(cut, "0.46%");
        await BlurAsync(cut);

        Assert.Equal(0.0046, cut.Instance.Changes[^1]);
        Assert.Equal(0.0046, cut.Instance.Commits[^1]);
        Assert.Equal("0.46%", test.Text);
    }

    [Fact(DisplayName = "should commit the clamped value when blur rounding crosses a boundary")]
    [Trait("Requirement", "NUM-EVT-06")]
    [Trait("Requirement", "NUM-STA-09")]
    [Trait("Requirement", "NUM-EVT-09")]
    public async Task Should_commit_the_clamped_value_when_blur_rounding_crosses_a_boundary()
    {
        NumberFormatOptions format = new() { Style = NumberFormatStyle.Percent, MaximumFractionDigits = 2 };
        IRenderedComponent<Host> cut = await RenderControlledAsync(format, max: 0.01235);

        await TypeAsync(cut, "1.236%");
        await BlurAsync(cut);

        Assert.Equal(0.01235, cut.Instance.Changes[^1]);
        Assert.Equal(0.01235, cut.Instance.Commits[^1]);
    }

    [Fact(DisplayName = "should round currency values on blur without percent scaling")]
    [Trait("Requirement", "NUM-EVT-06")]
    [Trait("Requirement", "NUM-STA-12")]
    public async Task Should_round_currency_values_on_blur_without_percent_scaling()
    {
        NumberFormatOptions format = new() { Style = NumberFormatStyle.Currency, Currency = "USD", MaximumFractionDigits = 2, RoundingMode = NumberFormatRoundingMode.Floor };
        IRenderedComponent<Host> cut = await RenderControlledAsync(format);

        await TypeAsync(cut, "1.239");
        await BlurAsync(cut);

        Assert.Equal(1.23, cut.Instance.Changes[^1]);
        Assert.Equal(NumberFormatter.Get(s_enUs, format).Format(1.239), test.Text);
    }

    [Fact(DisplayName = "should not commit values that overflow while parsing on blur")]
    [Trait("Requirement", "NUM-EVT-06")]
    [Trait("Requirement", "NUM-EDGE-05")]
    [Trait("Requirement", "NUM-I18N-06")]
    public async Task Should_not_commit_values_that_overflow_while_parsing_on_blur()
    {
        NumberFormatOptions format = new() { Style = NumberFormatStyle.Percent, MaximumFractionDigits = 2, RoundingMode = NumberFormatRoundingMode.Floor };
        string formattedOverflow = NumberFormatter.Get(s_enUs, format).Format(double.MaxValue);
        IRenderedComponent<Host> cut = Render(null, p => p.Add(x => x.Uncontrolled, true).Add(x => x.Format, format));
        await test.FocusAsync(cut);

        await test.ChangeAsync(cut, formattedOverflow);
        await BlurAsync(cut);

        Assert.Empty(cut.Instance.Changes);
        Assert.Empty(cut.Instance.Commits);
        Assert.Equal(formattedOverflow, test.Text);
    }

    [Fact(DisplayName = "should not commit a canceled blur change")]
    [Trait("Requirement", "NUM-EVT-04")]
    public async Task Should_not_commit_a_canceled_blur_change()
    {
        IRenderedComponent<Host> cut = Render(0, p => p.Add(x => x.Mirror, false).Add(x => x.Format, new NumberFormatOptions { MaximumFractionDigits = 2 }));
        cut.Instance.Cancel = true;
        await test.FocusAsync(cut);

        await test.ChangeAsync(cut, "1.239");
        await BlurAsync(cut);

        Assert.Empty(cut.Instance.Commits);
        Assert.Equal("1.239", test.Text);
    }

    [Fact(DisplayName = "should preserve default numeric precision while formatting display on blur")]
    [Trait("Requirement", "NUM-STA-10")]
    [Trait("Requirement", "NUM-EVT-06")]
    public async Task Should_preserve_default_numeric_precision_while_formatting_display_on_blur()
    {
        IRenderedComponent<Host> cut = Render(null);
        await test.FocusAsync(cut);

        await TypeAsync(cut, "1.23456");
        Assert.Equal("1.23456", test.Text);

        // Without explicit rounding options the numeric value keeps full precision rather than being truncated to the
        // display default of 3 fraction digits.
        Assert.Equal(1.23456, cut.Instance.Changes[^1]);

        await BlurAsync(cut);

        Assert.Equal(1.23456, cut.Instance.Commits[^1]);
        Assert.Equal("1.235", test.Text);
    }

    [Fact(DisplayName = "should preserve values typed with more than 15 significant digits")]
    [Trait("Requirement", "NUM-STA-10")]
    [Trait("Requirement", "NUM-EVT-06")]
    public async Task Should_preserve_values_typed_with_more_than_15_significant_digits()
    {
        IRenderedComponent<Host> cut = Render(null);
        await test.FocusAsync(cut);

        await TypeAsync(cut, "1.234567890123456");
        Assert.Equal("1.234567890123456", test.Text);
        Assert.Equal(1.234567890123456, cut.Instance.Changes[^1]);

        await BlurAsync(cut);

        Assert.Equal(1.234567890123456, cut.Instance.Commits[^1]);
        Assert.Equal("1.235", test.Text);
    }

    [Fact(DisplayName = "keeps full numeric precision across no-edit focus/blur cycles and subsequent stepping")]
    [Trait("Requirement", "NUM-STA-10")]
    [Trait("Requirement", "NUM-EVT-06")]
    [Trait("Requirement", "NUM-EVT-08")]
    public async Task Keeps_full_numeric_precision_across_no_edit_focus_blur_cycles_and_subsequent_stepping()
    {
        IRenderedComponent<Host> cut = Render(null, p => p.Add(x => x.Increment, true));
        await test.FocusAsync(cut);
        await TypeAsync(cut, "1.23456");
        await BlurAsync(cut);

        // Display rounds to the default; the committed numeric value keeps full precision.
        Assert.Equal("1.235", test.Text);
        Assert.Equal(1.23456, cut.Instance.Commits[^1]);

        // Re-focusing and blurring without edits must not re-parse the rounded display text, so no new commit fires.
        await test.FocusAsync(cut);
        await BlurAsync(cut);
        Assert.Single(cut.Instance.Commits);

        // Stepping advances from the full-precision numeric value, not the rounded display (2.235).
        await test.ClickAsync(cut, cut.FindComponent<NumberFieldIncrement>().Instance);
        Assert.Equal(2.23456, cut.Instance.Changes[^1]);
    }

    [Fact(DisplayName = "commits parsed value on blur and normalizes display for fr-FR")]
    [Trait("Requirement", "NUM-EVT-06")]
    [Trait("Requirement", "NUM-I18N-08")]
    public async Task Commits_parsed_value_on_blur_and_normalizes_display_for_fr_FR()
    {
        CultureInfo frFr = CultureInfo.GetCultureInfo("fr-FR");
        IRenderedComponent<Host> cut = Render(null, p => p.Add(x => x.Uncontrolled, true).Add(x => x.Culture, frFr));
        await test.FocusAsync(cut);

        await test.ChangeAsync(cut, "1234,5");
        Assert.Equal("1234,5", test.Text);

        await BlurAsync(cut);

        Assert.Equal(1234.5, Assert.Single(cut.Instance.Changes));

        // (1234.5).toLocaleString('fr-FR'): the group separator is the culture's own, which differs between ICU builds.
        Assert.Equal(1234.5.ToString("#,##0.###", frFr), test.Text);
    }

    // user.keyboard types one character at a time: the module reports each text the gate lets through.
    private async Task TypeAsync(IRenderedComponent<Host> cut, string text)
    {
        string current = test.Text;
        foreach (char ch in text)
        {
            current += ch;
            await test.ChangeAsync(cut, current);
        }
    }

    // A blur reports the text the input shows, the module's last `value`, with the revision `update` echoed.
    private Task BlurAsync(IRenderedComponent<Host> cut)
    {
        int revision = JsonDocument.Parse(test.NumberFieldOptions[^1]).RootElement.GetProperty("revision").GetInt32();
        return test.BlurAsync(cut, test.Text, revision);
    }

    // NumberFieldInput.test.tsx's renderControlledNumberField: a mirroring parent from `null`, the input focused.
    private async Task<IRenderedComponent<Host>> RenderControlledAsync(NumberFormatOptions format, CultureInfo? culture = null, double? max = null)
    {
        IRenderedComponent<Host> cut = Render(null, p => p.Add(x => x.Format, format).Add(x => x.Culture, culture ?? s_enUs).Add(x => x.Max, max));
        await test.FocusAsync(cut);
        return cut;
    }

    private IRenderedComponent<Host> Render(double? value, Action<ComponentParameterCollectionBuilder<Host>>? parameters = null) =>
        test.Context.Render<Host>(p =>
        {
            p.Add(x => x.Initial, value);
            parameters?.Invoke(p);
        });

    // The Base UI tests' parent: `value` is its own, set from onValueChange when it mirrors (useState, which renders only
    // when the value changes) and from an "external" button; Uncontrolled renders `defaultValue` instead.
    private sealed class Host : ComponentBase
    {
        private double? value;
        private bool initialized;

        [Parameter] public double? Initial { get; set; }

        [Parameter] public bool Uncontrolled { get; set; }

        [Parameter] public bool Mirror { get; set; } = true;

        [Parameter] public bool Increment { get; set; }

        [Parameter] public NumberFormatOptions? Format { get; set; }

        [Parameter] public CultureInfo Culture { get; set; } = s_enUs;

        [Parameter] public double? Min { get; set; }

        [Parameter] public double? Max { get; set; }

        [Parameter] public StepValue? Step { get; set; }

        [Parameter] public double SmallStep { get; set; } = 0.1;

        [Parameter] public bool SnapOnStep { get; set; }

        public List<double?> Changes { get; } = [];

        public List<double?> Commits { get; } = [];

        public bool Cancel { get; set; }

        public void External(double? next)
        {
            value = next;
            StateHasChanged();
        }

        protected override void OnParametersSet()
        {
            if (!initialized)
            {
                initialized = true;
                value = Initial;
            }
        }

        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.OpenComponent<NumberFieldRoot>(0);
            if (Uncontrolled)
            {
                builder.AddComponentParameter(1, nameof(NumberFieldRoot.DefaultValue), Initial);
            }
            else
            {
                builder.AddComponentParameter(2, nameof(NumberFieldRoot.Value), value);
            }

            builder.AddComponentParameter(3, nameof(NumberFieldRoot.Culture), Culture);
            builder.AddComponentParameter(4, nameof(NumberFieldRoot.Format), Format);
            builder.AddComponentParameter(5, nameof(NumberFieldRoot.Min), Min);
            builder.AddComponentParameter(6, nameof(NumberFieldRoot.Max), Max);
            builder.AddComponentParameter(7, nameof(NumberFieldRoot.Step), Step);
            builder.AddComponentParameter(8, nameof(NumberFieldRoot.SmallStep), SmallStep);
            builder.AddComponentParameter(9, nameof(NumberFieldRoot.SnapOnStep), SnapOnStep);
            // No receiver: a callback does not re-render the parent, as a React parent renders only when its state changes.
            builder.AddComponentParameter(10, nameof(NumberFieldRoot.OnValueChange), new EventCallback<ValueChangeEventArgs<double?>>(null, (Action<ValueChangeEventArgs<double?>>)OnChange));
            builder.AddComponentParameter(11, nameof(NumberFieldRoot.OnValueCommitted), new EventCallback<ValueCommittedEventArgs<double?>>(null, (Action<ValueCommittedEventArgs<double?>>)(e => Commits.Add(e.Value))));
            builder.AddComponentParameter(12, nameof(NumberFieldRoot.ChildContent), (RenderFragment)(b =>
            {
                b.OpenComponent<NumberFieldInput>(0);
                b.CloseComponent();
                if (Increment)
                {
                    b.OpenComponent<NumberFieldIncrement>(1);
                    b.CloseComponent();
                }
            }));
            builder.CloseComponent();
        }

        private void OnChange(ValueChangeEventArgs<double?> args)
        {
            Changes.Add(args.Value);
            if (Cancel)
            {
                args.Details.Cancel();
                return;
            }

            if (Mirror && !Uncontrolled && !Equals(value, args.Value))
            {
                value = args.Value;
                StateHasChanged();
            }
        }
    }
}

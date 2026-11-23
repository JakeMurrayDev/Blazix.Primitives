using System.Globalization;
using System.Text.Json;
using Iodine.Headless.Utilities;
using Bunit;
using Microsoft.AspNetCore.Components;

// The namespace is the project's root: a namespace named after the family would hide the parts' types.
namespace Iodine.Headless.Tests.Components;

// NumberFieldInput.test.tsx's and NumberFieldRoot.test.tsx's titles that need no typed text (NUM-EVT-12 and, from 6b's
// first commits, the eleven titles PR 292 named as reachable without the input role); the typing titles are the input role's. A blur reports the text the input shows, which is the module's last `value`,
// with the revision `update` echoed.
/// <summary>The number field input's blur after an external value change.</summary>
public sealed class NumberFieldInputTests : IDisposable
{
    private static readonly CultureInfo s_enUs = CultureInfo.GetCultureInfo("en-US");

    private readonly NumberFieldTestContext test = new();
    private int changes;

    /// <inheritdoc />
    public void Dispose() => test.Dispose();

    [Fact(DisplayName = "should preserve default formatting on first blur after external value change")]
    [Trait("Requirement", "NUM-EVT-12")]
    public async Task Should_preserve_default_formatting_on_first_blur_after_external_value_change()
    {
        IRenderedComponent<NumberFieldRoot> cut = Render(null);

        cut.Render(p => p.Add(x => x.Value, 1.23456));
        Assert.Equal("1.235", test.Text);

        await FocusAndBlurAsync(cut);

        // A focus and blur with no edits must not re-parse the rounded display text and overwrite the numeric value.
        Assert.Equal("1.235", test.Text);
        Assert.Equal(0, changes);
    }

    [Fact(DisplayName = "should handle multiple blur cycles with default formatting")]
    [Trait("Requirement", "NUM-EVT-12")]
    public async Task Should_handle_multiple_blur_cycles_with_default_formatting()
    {
        IRenderedComponent<NumberFieldRoot> cut = Render(null);

        cut.Render(p => p.Add(x => x.Value, 1.23456789));
        Assert.Equal("1.235", test.Text);

        await FocusAndBlurAsync(cut);
        Assert.Equal("1.235", test.Text);
        Assert.Equal(0, changes);

        await FocusAndBlurAsync(cut);

        // Repeated no-edit blur cycles keep the full-precision numeric value; only the display is formatted.
        Assert.Equal("1.235", test.Text);
        Assert.Equal(0, changes);
        Assert.Equal(1.23456789, cut.Instance.Value);
    }

    // NUM-STA-05/06: after a step, an external value change reaches the text (sync allowed again after the step).
    [Fact(DisplayName = "should update input value after increment/decrement followed by external value change")]
    [Trait("Requirement", "NUM-STA-06")]
    [Trait("Requirement", "NUM-PTR-01")]
    public async Task Should_update_input_value_after_increment_followed_by_external_value_change()
    {
        IRenderedComponent<NumberFieldStepperHost> cut = Host(increment: true, 0);
        Assert.Equal("0", test.Text);

        await test.ClickAsync(cut, cut.FindComponent<NumberFieldIncrement>().Instance);
        Assert.Equal("1", test.Text);
        Assert.Single(cut.Instance.Changes);

        await cut.InvokeAsync(() => cut.Instance.ExternalValue(1.23456));
        Assert.Equal("1.235", test.Text);
    }

    [Fact(DisplayName = "should update input value after decrement followed by external value change")]
    [Trait("Requirement", "NUM-STA-06")]
    [Trait("Requirement", "NUM-PTR-01")]
    public async Task Should_update_input_value_after_decrement_followed_by_external_value_change()
    {
        IRenderedComponent<NumberFieldStepperHost> cut = Host(increment: false, 5);
        Assert.Equal("5", test.Text);

        await test.ClickAsync(cut, cut.FindComponent<NumberFieldDecrement>().Instance);
        Assert.Equal("4", test.Text);
        Assert.Single(cut.Instance.Changes);

        await cut.InvokeAsync(() => cut.Instance.ExternalValue(2.98765));
        Assert.Equal("2.988", test.Text);
    }

    // NUM-I18N-01, NUM-STA-12: Intl's roundingIncrement 5 at one fraction digit writes 1.2 as "1.0" (halfExpand to 0.5s).
    [Fact(DisplayName = "should not throw on blur when format uses roundingIncrement with fixed fraction digits")]
    [Trait("Requirement", "NUM-I18N-01")]
    [Trait("Requirement", "NUM-EVT-06")]
    public async Task Should_not_throw_on_blur_when_format_uses_rounding_increment_with_fixed_fraction_digits()
    {
        NumberFormatOptions format = new() { MinimumFractionDigits = 1, MaximumFractionDigits = 1, RoundingIncrement = 5 };
        string expected = NumberFormatter.Get(s_enUs, format).Format(1.2);
        RenderFragment parts = b =>
        {
            b.OpenComponent<NumberFieldInput>(0);
            b.CloseComponent();
        };
        IRenderedComponent<NumberFieldRoot> cut = test.Context.Render<NumberFieldRoot>(p => p
            .Add(x => x.Culture, s_enUs)
            .Add(x => x.DefaultValue, 1.2)
            .Add(x => x.Format, format)
            .Add(x => x.ChildContent, parts));
        Assert.Equal("1.0", expected);
        Assert.Equal(expected, test.Text);

        await FocusAndBlurAsync(cut);

        Assert.Equal(expected, test.Text);
    }

    // NUM-STA-07..10: a step finer than the display's three fraction digits still advances the value.
    [Fact(DisplayName = "advances by a step finer than 3 fraction digits")]
    [Trait("Requirement", "NUM-STA-09")]
    [Trait("Requirement", "NUM-PTR-01")]
    public async Task Advances_by_a_step_finer_than_3_fraction_digits()
    {
        IRenderedComponent<NumberFieldStepperHost> cut = Host(increment: true, 0, p => p.Add(x => x.Step, (StepValue?)0.0001));
        NumberFieldIncrement increase = cut.FindComponent<NumberFieldIncrement>().Instance;

        await test.ClickAsync(cut, increase);
        Assert.Equal(0.0001, cut.Instance.Changes[^1]);
        Assert.Equal("0", test.Text);

        await test.ClickAsync(cut, increase);
        Assert.Equal(0.0002, cut.Instance.Changes[^1]);
        Assert.Equal("0", test.Text);
    }

    [Fact(DisplayName = "cleans binary floating point noise introduced by stepping")]
    [Trait("Requirement", "NUM-STA-10")]
    [Trait("Requirement", "NUM-PTR-01")]
    public async Task Cleans_binary_floating_point_noise_introduced_by_stepping()
    {
        IRenderedComponent<NumberFieldStepperHost> cut = Host(increment: true, 0.7, p => p.Add(x => x.Step, (StepValue?)0.1), controlled: false);

        await test.ClickAsync(cut, cut.FindComponent<NumberFieldIncrement>().Instance);

        // 0.7 + 0.1 is 0.7999999999999999 in binary floating point.
        Assert.Equal(0.8, cut.Instance.Changes[^1]);
    }

    private IRenderedComponent<NumberFieldStepperHost> Host(bool increment, double value, Action<ComponentParameterCollectionBuilder<NumberFieldStepperHost>>? parameters = null, bool controlled = true) =>
        test.Context.Render<NumberFieldStepperHost>(p =>
        {
            p.Add(x => x.Increment, increment);
            p.Add(x => x.Controlled, controlled);
            p.Add(x => x.DefaultValue, value);
            parameters?.Invoke(p);
        });

    private async Task FocusAndBlurAsync(IRenderedComponent<NumberFieldRoot> cut)
    {
        int revision = JsonDocument.Parse(test.NumberFieldOptions[^1]).RootElement.GetProperty("revision").GetInt32();
        await test.FocusAsync(cut);
        await test.BlurAsync(cut, test.Text, revision);
    }

    private IRenderedComponent<NumberFieldRoot> Render(double? value)
    {
        RenderFragment parts = b =>
        {
            b.OpenComponent<NumberFieldInput>(0);
            b.CloseComponent();
        };

        return test.Context.Render<NumberFieldRoot>(p => p
            .Add(x => x.Culture, s_enUs)
            .Add(x => x.Value, value)
            .Add(x => x.OnValueChange, (ValueChangeEventArgs<double?> _) => changes++)
            .Add(x => x.ChildContent, parts));
    }
}

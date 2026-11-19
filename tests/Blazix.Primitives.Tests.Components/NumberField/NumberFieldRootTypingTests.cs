using System.Globalization;
using Blazix.Primitives.Utilities;
using Bunit;
using Microsoft.AspNetCore.Components;

// The namespace is the project's root: a namespace named after the family would hide the parts' types.
namespace Blazix.Primitives.Tests.Components;

// 6b's input role at L2 (issue 277), batch B1: NumberFieldRoot.test.tsx's titles below line 1390 that type, blur, step with
// a key or a modified press, or clamp to min and max. Base UI's fireEvent.change, keyDown, blur and wheel are the
// `number-field` module's reports here (ADR 06 §1), as NumberFieldTypingTests' header explains; a stepper's
// fireEvent.pointerDown is the `button` module's press report (NumberFieldTestContext.PressAsync). A text the module would
// revert is still sent where Base UI sends it, so C#'s own character test answers (decision 7 (a)). Base UI formats with
// the runtime's default locale, en-US in its browser runs; these pass the culture explicitly, as CI runs the invariant
// culture, and read a locale's glyphs from the port's formatter where Base UI reads them from `Intl.NumberFormat`.
/// <summary>Typing, blur commits, key steps and modified presses on a number field, and its min and max while typing.</summary>
public sealed class NumberFieldRootTypingTests : IDisposable
{
    private static readonly CultureInfo s_enUs = CultureInfo.GetCultureInfo("en-US");

    private readonly NumberFieldTestContext test = new();
    private readonly List<ValueChangeEventArgs<double?>> changes = [];
    private readonly List<ValueCommittedEventArgs<double?>> commits = [];
    private IRenderedComponent<NumberFieldRoot>? root;

    /// <inheritdoc />
    public void Dispose() => test.Dispose();

    [Fact(DisplayName = "should be `null` when the input is empty but not trimmed")]
    [Trait("Requirement", "NUM-EVT-15")]
    public async Task Should_be_null_when_the_input_is_empty_but_not_trimmed()
    {
        Render(p => p.Add(x => x.Value, 1));

        await test.ChangeAsync(root!, "  ");

        Assert.Null(changes[0].Value);
    }

    [Fact(DisplayName = "includes the reason for wheel scrubbing")]
    [Trait("Requirement", "NUM-EVT-02")]
    public async Task Includes_the_reason_for_wheel_scrubbing()
    {
        Render(p => p.Add(x => x.AllowWheelScrub, true).Add(x => x.DefaultValue, 4));

        await test.FocusAsync(root!);
        // deltaY -100: the wheel turned up, a step of +1.
        await test.WheelAsync(root!, 1);

        Assert.Equal(EventReason.Wheel, Assert.Single(changes).Details.Reason);
    }

    // Base UI's compositionStart and compositionEnd are the module's (an IME keydown's `which` 229 passes the gate); C#
    // sees the composed texts as change reports.
    [Fact(DisplayName = "does not fire onValueChange for non-numeric composition/partial input")]
    [Trait("Requirement", "NUM-EVT-15")]
    [Trait("Requirement", "NUM-I18N-04")]
    [Trait("Requirement", "NUM-EVT-05")]
    public async Task Does_not_fire_onValueChange_for_non_numeric_composition_partial_input()
    {
        Render();

        await test.ChangeAsync(root!, "n");
        await test.ChangeAsync(root!, "ni");

        Assert.Empty(changes);

        await test.ChangeAsync(root!, "一");
        Assert.Equal(1, Assert.Single(changes).Value);

        Assert.Empty(commits);
        await test.BlurAsync(root!, "一", revision: 3);
        Assert.Equal(1, Assert.Single(commits).Value);
    }

    [Fact(DisplayName = "accepts grouping while typing and parses progressively")]
    [Trait("Requirement", "NUM-EVT-15")]
    [Trait("Requirement", "NUM-KEY-07")]
    [Trait("Requirement", "NUM-I18N-08")]
    public async Task Accepts_grouping_while_typing_and_parses_progressively()
    {
        string group = s_enUs.NumberFormat.NumberGroupSeparator;
        Assert.NotEqual(string.Empty, group);
        Render();

        await test.ChangeAsync(root!, "1");
        await test.ChangeAsync(root!, $"1{group}");
        await test.ChangeAsync(root!, $"1{group}2");
        await test.ChangeAsync(root!, $"1{group}23");
        await test.ChangeAsync(root!, $"1{group}234");

        Assert.Equal([1d, 1d, 12d, 123d, 1234d], changes.Select(c => c.Value));

        Assert.Empty(commits);
        await test.BlurAsync(root!, $"1{group}234", revision: 5);
        Assert.Equal(1234, Assert.Single(commits).Value);
    }

    [Fact(DisplayName = "parses percent while typing and commits canonical percent value")]
    [Trait("Requirement", "NUM-I18N-05")]
    [Trait("Requirement", "NUM-EVT-06")]
    [Trait("Requirement", "NUM-EDGE-17")]
    public async Task Parses_percent_while_typing_and_commits_canonical_percent_value()
    {
        Render(p => p.Add(x => x.Format, new NumberFormatOptions { Style = NumberFormatStyle.Percent }));

        // Typing digits in percent style represents a fraction (12 -> 0.12); the sign keeps it 0.12.
        await test.ChangeAsync(root!, "12");
        await test.ChangeAsync(root!, "12%");

        Assert.Equal([0.12, 0.12], changes.Select(c => c.Value));
        Assert.Empty(commits);

        await test.BlurAsync(root!, "12%", revision: 2);
        Assert.Equal(0.12, Assert.Single(commits).Value);
    }

    [Fact(DisplayName = "parses an interleaved percent sign while typing (1%2 -> 12%)")]
    [Trait("Requirement", "NUM-I18N-05")]
    [Trait("Requirement", "NUM-EVT-06")]
    [Trait("Requirement", "NUM-EDGE-17")]
    public async Task Parses_an_interleaved_percent_sign_while_typing()
    {
        Render(p => p.Add(x => x.DefaultValue, 0.01).Add(x => x.Format, new NumberFormatOptions { Style = NumberFormatStyle.Percent }));
        Assert.Equal("1%", test.Text);

        // Typing `2` after the rendered `1%` yields `1%2`, which reformats to `12%` on blur.
        await test.FocusAsync(root!);
        await test.ChangeAsync(root!, "1%2");
        await test.BlurAsync(root!, "1%2", revision: 1);

        Assert.Equal("12%", test.Text);
        Assert.Equal(0.12, Assert.Single(commits).Value);
    }

    [Fact(DisplayName = "accepts currency symbol while typing and parses numeric value")]
    [Trait("Requirement", "NUM-KEY-07")]
    [Trait("Requirement", "NUM-I18N-12")]
    public async Task Accepts_currency_symbol_while_typing_and_parses_numeric_value()
    {
        NumberFormatOptions format = new() { Style = NumberFormatStyle.Currency, Currency = "USD" };
        List<NumberFormatPart> parts = NumberFormatter.Get(s_enUs, format).FormatToParts(12345);
        string group = parts.FirstOrDefault(part => part.Type == NumberFormatPartType.Group).Value ?? string.Empty;
        Assert.NotEqual(string.Empty, group);

        // Base UI's formatPartialValue: the number's parts replaced by the partial text, the currency and literals kept.
        string Partial(string value)
        {
            bool inserted = false;
            return string.Concat(parts.Select(part =>
            {
                if (part.Type is NumberFormatPartType.Integer or NumberFormatPartType.Group or NumberFormatPartType.Decimal or NumberFormatPartType.Fraction)
                {
                    if (!inserted)
                    {
                        inserted = true;
                        return value;
                    }

                    return string.Empty;
                }

                return part.Value;
            }));
        }

        Render(p => p.Add(x => x.Format, format));

        await test.ChangeAsync(root!, Partial("1"));
        await test.ChangeAsync(root!, Partial($"1{group}2"));

        Assert.Equal([1d, 12d], changes.Select(c => c.Value));
    }

    [Fact(DisplayName = "accepts multi-character unit symbols while typing (e.g. km/h)")]
    [Trait("Requirement", "NUM-KEY-07")]
    [Trait("Requirement", "NUM-I18N-12")]
    public async Task Accepts_multi_character_unit_symbols_while_typing()
    {
        Render(p => p.Add(x => x.Format, new NumberFormatOptions { Style = NumberFormatStyle.Unit, Unit = NumberFormatUnit.KilometerPerHour }));

        // The `km/h` unit must not block editing the numeric region.
        await test.ChangeAsync(root!, "1 km/h");
        await test.ChangeAsync(root!, "12 km/h");

        Assert.Equal([1d, 12d], changes.Select(c => c.Value));
    }

    [Fact(DisplayName = "ignores bidi/format control characters in the value (e.g. RTL exponent signs)")]
    [Trait("Requirement", "NUM-I18N-11")]
    [Trait("Requirement", "NUM-EVT-15")]
    public async Task Ignores_bidi_format_control_characters_in_the_value()
    {
        Render(p => p.Add(x => x.Format, new NumberFormatOptions { Notation = NumberFormatNotation.Scientific }));

        // A U+200E LEFT-TO-RIGHT MARK around the exponent sign, as RTL locales format it.
        await test.ChangeAsync(root!, "5E‎-1");

        Assert.Equal(0.5, Assert.Single(changes).Value);
    }

    [Fact(DisplayName = "allows deleting trailing currency symbols with locale literals")]
    [Trait("Requirement", "NUM-KEY-07")]
    [Trait("Requirement", "NUM-I18N-12")]
    public async Task Allows_deleting_trailing_currency_symbols_with_locale_literals()
    {
        NumberFormatOptions format = new() { Style = NumberFormatStyle.Currency, Currency = "EUR", MinimumFractionDigits = 2, MaximumFractionDigits = 2 };
        CultureInfo deDe = CultureInfo.GetCultureInfo("de-DE");
        Render(p => p.Add(x => x.DefaultValue, 12.34).Add(x => x.Format, format), deDe);
        string formatted = NumberFormatter.FormatNumber(12.34, deDe, format);
        Assert.Equal(formatted, test.Text);
        string withoutCurrency = formatted.Replace("€", string.Empty, StringComparison.Ordinal);

        await test.ChangeAsync(root!, withoutCurrency);

        Assert.Equal(withoutCurrency, test.Text);
        Assert.Equal(12.34, Assert.Single(changes).Value);
    }

    [Fact(DisplayName = "does not commit on blur for invalid input")]
    [Trait("Requirement", "NUM-EDGE-05")]
    public async Task Does_not_commit_on_blur_for_invalid_input()
    {
        Render();

        await test.ChangeAsync(root!, ".");
        Assert.Equal(".", test.Text);
        await test.BlurAsync(root!, ".", revision: 1);

        Assert.Empty(commits);
    }

    [Fact(DisplayName = "fires on blur with committed numeric value")]
    [Trait("Requirement", "NUM-API-04")]
    [Trait("Requirement", "NUM-EVT-05")]
    [Trait("Requirement", "NUM-EVT-06")]
    public async Task Fires_on_blur_with_committed_numeric_value()
    {
        Render();

        await test.FocusAsync(root!);
        await test.ChangeAsync(root!, "123.");
        await test.BlurAsync(root!, "123.", revision: 1);

        // Canonicalizes to 123.
        Assert.Equal(123, Assert.Single(commits).Value);
    }

    [Fact(DisplayName = "fires null on blur when input is cleared")]
    [Trait("Requirement", "NUM-EVT-05")]
    [Trait("Requirement", "NUM-EVT-08")]
    public async Task Fires_null_on_blur_when_input_is_cleared()
    {
        Render(p => p.Add(x => x.DefaultValue, 5));

        await test.FocusAsync(root!);
        await test.ChangeAsync(root!, string.Empty);
        await test.BlurAsync(root!, string.Empty, revision: 1);

        Assert.Null(Assert.Single(commits).Value);
    }

    [Fact(DisplayName = "reports and displays the clamped value on blur, not the raw input")]
    [Trait("Requirement", "NUM-EVT-06")]
    [Trait("Requirement", "NUM-EVT-09")]
    public async Task Reports_and_displays_the_clamped_value_on_blur_not_the_raw_input()
    {
        Render(p => p.Add(x => x.Max, 10));

        await test.FocusAsync(root!);
        await test.ChangeAsync(root!, "1000");
        await test.BlurAsync(root!, "1000", revision: 1);

        Assert.Equal(10, Assert.Single(commits).Value);
        Assert.Equal("10", test.Text);
    }

    [Fact(DisplayName = "reports and displays the min-clamped value on blur, not the raw input")]
    [Trait("Requirement", "NUM-EVT-06")]
    [Trait("Requirement", "NUM-EVT-09")]
    public async Task Reports_and_displays_the_min_clamped_value_on_blur_not_the_raw_input()
    {
        Render(p => p.Add(x => x.Min, 0));

        await test.FocusAsync(root!);
        await test.ChangeAsync(root!, "-50");
        await test.BlurAsync(root!, "-50", revision: 1);

        Assert.Equal(0, Assert.Single(commits).Value);
        Assert.Equal("0", test.Text);
    }

    [Fact(DisplayName = "does not fire on keyboard steps that do not change the value")]
    [Trait("Requirement", "NUM-KEY-01")]
    [Trait("Requirement", "NUM-EVT-13")]
    public async Task Does_not_fire_on_keyboard_steps_that_do_not_change_the_value()
    {
        Render(p => p.Add(x => x.DefaultValue, 5).Add(x => x.Min, 0).Add(x => x.Max, 5));
        await test.FocusAsync(root!);

        await test.KeyAsync(root!, "ArrowUp");
        await test.KeyAsync(root!, "End");
        Assert.Empty(commits);

        await test.KeyAsync(root!, "ArrowDown");
        Assert.Equal(4, Assert.Single(commits).Value);
    }

    [Fact(DisplayName = "does not commit a canceled keyboard change")]
    [Trait("Requirement", "NUM-KEY-01")]
    [Trait("Requirement", "NUM-EVT-04")]
    public async Task Does_not_commit_a_canceled_keyboard_change()
    {
        Render(p => p.Add(x => x.DefaultValue, 0), cancel: true);
        await test.FocusAsync(root!);

        await test.KeyAsync(root!, "ArrowUp");

        Assert.Single(changes);
        Assert.Equal("0", test.Text);
        Assert.Empty(commits);
    }

    // Base UI's App keeps the value in useState and sets it from onValueChange: the stepper host's controlled mode.
    [Fact(DisplayName = "prevents the raw value from going below the `min` prop")]
    [Trait("Requirement", "NUM-API-05")]
    [Trait("Requirement", "NUM-STA-18")]
    [Trait("Requirement", "NUM-EVT-01")]
    public async Task Prevents_the_raw_value_from_going_below_the_min_prop()
    {
        IRenderedComponent<NumberFieldStepperHost> cut = Host(p => p.Add(x => x.Min, 5));

        await test.ChangeAsync(cut, "4");

        Assert.Equal("4", test.Text);
        Assert.Equal(5, cut.Instance.Changes[0]);
    }

    [Fact(DisplayName = "allows the value to go above the `min` prop")]
    [Trait("Requirement", "NUM-API-05")]
    [Trait("Requirement", "NUM-EVT-13")]
    public async Task Allows_the_value_to_go_above_the_min_prop()
    {
        IRenderedComponent<NumberFieldStepperHost> cut = Host(p => p.Add(x => x.Min, 5));

        await test.ChangeAsync(cut, "6");

        Assert.Equal("6", test.Text);
    }

    [Fact(DisplayName = "prevents the value from going above the `max` prop")]
    [Trait("Requirement", "NUM-API-05")]
    [Trait("Requirement", "NUM-EVT-13")]
    public async Task Prevents_the_value_from_going_above_the_max_prop()
    {
        IRenderedComponent<NumberFieldStepperHost> cut = Host(p => p.Add(x => x.Max, 5));

        await test.ChangeAsync(cut, "6");

        Assert.Equal("6", test.Text);
        Assert.Equal(5, cut.Instance.Changes[0]);
    }

    [Fact(DisplayName = "allows the value to go below the `max` prop")]
    [Trait("Requirement", "NUM-API-05")]
    [Trait("Requirement", "NUM-EVT-13")]
    public async Task Allows_the_value_to_go_below_the_max_prop()
    {
        IRenderedComponent<NumberFieldStepperHost> cut = Host(p => p.Add(x => x.Max, 5));

        await test.ChangeAsync(cut, "4");

        Assert.Equal("4", test.Text);
        Assert.Equal(4, cut.Instance.Changes[0]);
    }

    // Base UI's describe.skipIf(isJSDOM) blocks (jsdom drops a pointerdown's modifier keys); the press report carries
    // them, so the titles run at L2.
    [Fact(DisplayName = "should decrement the value by the default `largeStep` prop of 10 while holding the shift key")]
    [Trait("Requirement", "NUM-API-07")]
    [Trait("Requirement", "NUM-STA-16")]
    [Trait("Requirement", "NUM-PTR-03")]
    public async Task Should_decrement_by_the_default_largeStep_while_holding_shift()
    {
        Render(p => p.Add(x => x.DefaultValue, 6));

        await test.PressAsync(root!, root!.FindComponent<NumberFieldDecrement>().Instance, shiftKey: true);

        Assert.Equal("-4", test.Text);
    }

    [Fact(DisplayName = "should not use the `largeStep` prop if no longer holding the shift key")]
    [Trait("Requirement", "NUM-API-07")]
    [Trait("Requirement", "NUM-STA-16")]
    [Trait("Requirement", "NUM-PTR-03")]
    public async Task Should_not_use_the_largeStep_prop_if_no_longer_holding_shift()
    {
        Render(p => p.Add(x => x.DefaultValue, 5).Add(x => x.LargeStep, 5));
        NumberFieldIncrement increment = root!.FindComponent<NumberFieldIncrement>().Instance;

        await test.PressAsync(root!, increment, shiftKey: true);
        Assert.Equal("10", test.Text);

        // Base UI's keyUp on the input changes nothing: the amount is read from the press's own modifiers (NUM-STA-16).
        await test.PressAsync(root!, increment, shiftKey: true);
        Assert.Equal("15", test.Text);
    }

    [Fact(DisplayName = "should decrement the value by the default `smallStep` prop of 0.1 while holding the alt key")]
    [Trait("Requirement", "NUM-API-07")]
    [Trait("Requirement", "NUM-STA-16")]
    public async Task Should_decrement_by_the_default_smallStep_while_holding_alt()
    {
        Render(p => p.Add(x => x.DefaultValue, 6));

        await test.PressAsync(root!, root!.FindComponent<NumberFieldDecrement>().Instance, altKey: true);

        Assert.Equal(5.9.ToString(s_enUs), test.Text);
    }

    [Fact(DisplayName = "should use explicit `smallStep` value if provided while holding the alt key")]
    [Trait("Requirement", "NUM-API-07")]
    [Trait("Requirement", "NUM-STA-16")]
    public async Task Should_use_explicit_smallStep_value_while_holding_alt()
    {
        Render(p => p.Add(x => x.DefaultValue, 5).Add(x => x.SmallStep, 0.5));

        await test.PressAsync(root!, root!.FindComponent<NumberFieldIncrement>().Instance, altKey: true);

        Assert.Equal(5.5.ToString(s_enUs), test.Text);
    }

    private void Render(Action<ComponentParameterCollectionBuilder<NumberFieldRoot>>? parameters = null, CultureInfo? culture = null, bool cancel = false)
    {
        RenderFragment parts = b =>
        {
            b.OpenComponent<NumberFieldGroup>(0);
            b.AddComponentParameter(1, nameof(NumberFieldGroup.ChildContent), (RenderFragment)(g =>
            {
                g.OpenComponent<NumberFieldInput>(0);
                g.CloseComponent();
                g.OpenComponent<NumberFieldIncrement>(1);
                g.CloseComponent();
                g.OpenComponent<NumberFieldDecrement>(2);
                g.CloseComponent();
            }));
            b.CloseComponent();
        };

        root = test.Context.Render<NumberFieldRoot>(p =>
        {
            p.Add(x => x.Culture, culture ?? s_enUs)
                .Add(x => x.OnValueChange, (ValueChangeEventArgs<double?> e) =>
                {
                    changes.Add(e);
                    if (cancel)
                    {
                        e.Details.Cancel();
                    }
                })
                .Add(x => x.OnValueCommitted, (ValueCommittedEventArgs<double?> e) => commits.Add(e))
                .Add(x => x.ChildContent, parts);
            parameters?.Invoke(p);
        });
    }

    private IRenderedComponent<NumberFieldStepperHost> Host(Action<ComponentParameterCollectionBuilder<NumberFieldStepperHost>> parameters) =>
        test.Context.Render<NumberFieldStepperHost>(p =>
        {
            p.Add(x => x.Controlled, true).Add(x => x.DefaultValue, 5);
            parameters(p);
        });
}

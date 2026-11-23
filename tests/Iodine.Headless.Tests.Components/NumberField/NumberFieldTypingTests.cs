using System.Globalization;
using Bunit;
using Microsoft.AspNetCore.Components;

// The namespace is the project's root: a namespace named after the family would hide the parts' types.
namespace Iodine.Headless.Tests.Components;

// 6b's input role at L2 (issue 277): NumberFieldRoot.test.tsx's titles that type, press a step key, paste, wheel or autofill.
// Base UI's fireEvent.change, keyDown, paste and wheel are the `number-field` module's reports here (ADR 06 §1): the module
// decides in the dispatch and C# answers. A text the module would revert is still sent where Base UI sends it, so C#'s own
// character test answers (decision 7 (a)).
/// <summary>Typing, step keys, paste, the wheel and autofill on a number field.</summary>
public sealed class NumberFieldTypingTests : IDisposable
{
    private static readonly CultureInfo s_enUs = CultureInfo.GetCultureInfo("en-US");

    private readonly NumberFieldTestContext test = new();
    private readonly List<ValueChangeEventArgs<double?>> changes = [];
    private readonly List<ValueCommittedEventArgs<double?>> commits = [];
    private IRenderedComponent<NumberFieldRoot>? root;

    /// <inheritdoc />
    public void Dispose() => test.Dispose();

    [Fact(DisplayName = "should be called with a number when transitioning from `null`")]
    [Trait("Requirement", "NUM-API-03")]
    [Trait("Requirement", "NUM-EVT-15")]
    public async Task Should_be_called_with_a_number_when_transitioning_from_null()
    {
        IRenderedComponent<NumberFieldStepperHost> cut = Host(null);

        await test.ChangeAsync(cut, "5");

        Assert.Equal([5d], cut.Instance.Changes);
    }

    [Fact(DisplayName = "should be called with `null` when empty and transitioning from a number")]
    [Trait("Requirement", "NUM-API-03")]
    [Trait("Requirement", "NUM-EVT-15")]
    public async Task Should_be_called_with_null_when_empty_and_transitioning_from_a_number()
    {
        IRenderedComponent<NumberFieldStepperHost> cut = Host(5);

        await test.ChangeAsync(cut, string.Empty);

        Assert.Equal([(double?)null], cut.Instance.Changes);
    }

    [Fact(DisplayName = "includes the reason for parseable typing")]
    [Trait("Requirement", "NUM-EVT-15")]
    public async Task Includes_the_reason_for_parseable_typing()
    {
        Render(null);

        await test.ChangeAsync(root!, "12");

        Assert.Equal(EventReason.InputChange, Assert.Single(changes).Details.Reason);
    }

    [Fact(DisplayName = "includes the reason when clearing the value")]
    [Trait("Requirement", "NUM-EVT-15")]
    public async Task Includes_the_reason_when_clearing_the_value()
    {
        Render(5);

        await test.ChangeAsync(root!, string.Empty);

        Assert.Equal(EventReason.InputClear, Assert.Single(changes).Details.Reason);
    }

    [Fact(DisplayName = "fires onValueChange for each parseable change while typing")]
    [Trait("Requirement", "NUM-EVT-15")]
    [Trait("Requirement", "NUM-EDGE-06")]
    public async Task Fires_onValueChange_for_each_parseable_change_while_typing()
    {
        Render(null);

        await test.ChangeAsync(root!, "1");
        await test.ChangeAsync(root!, "12");
        await test.ChangeAsync(root!, "12.");
        await test.ChangeAsync(root!, "12.a");

        Assert.Equal([1d, 12d, 12d], changes.Select(c => c.Value));
        Assert.Empty(commits);
    }

    [Fact(DisplayName = "handles sign and decimal partials vs. parseable numbers")]
    [Trait("Requirement", "NUM-EVT-15")]
    [Trait("Requirement", "NUM-EVT-06")]
    public async Task Handles_sign_and_decimal_partials_vs_parseable_numbers()
    {
        Render(null, min: -10);

        await test.ChangeAsync(root!, "-");
        await test.ChangeAsync(root!, ".");
        await test.ChangeAsync(root!, "0.");
        await test.ChangeAsync(root!, "-1");
        await test.ChangeAsync(root!, "-1.5");

        Assert.Equal([0d, -1d, -1.5], changes.Select(c => c.Value));
        Assert.Empty(commits);

        await test.BlurAsync(root!, "-1.5", revision: 5);
        Assert.Equal(-1.5, Assert.Single(commits).Value);
    }

    [Fact(DisplayName = "respects locale decimal separator while typing (de-DE)")]
    [Trait("Requirement", "NUM-I18N-02")]
    [Trait("Requirement", "NUM-EVT-15")]
    public async Task Respects_locale_decimal_separator_while_typing()
    {
        Render(null, culture: CultureInfo.GetCultureInfo("de-DE"));

        await test.ChangeAsync(root!, "1");
        await test.ChangeAsync(root!, "1,");
        await test.ChangeAsync(root!, "1,5");

        Assert.Equal([1d, 1d, 1.5], changes.Select(c => c.Value));
        await test.BlurAsync(root!, "1,5", revision: 3);
        Assert.Equal(1.5, Assert.Single(commits).Value);
    }

    [Fact(DisplayName = "fires on keyboard interactions (ArrowUp/Down/Home/End)")]
    [Trait("Requirement", "NUM-KEY-01")]
    [Trait("Requirement", "NUM-KEY-02")]
    public async Task Fires_on_keyboard_interactions()
    {
        Render(0, min: -10, max: 10);

        await test.KeyAsync(root!, "ArrowUp");
        Assert.Equal(1, commits[^1].Value);
        await test.KeyAsync(root!, "ArrowDown");
        Assert.Equal(0, commits[^1].Value);
        await test.KeyAsync(root!, "Home");
        Assert.Equal(-10, commits[^1].Value);
        await test.KeyAsync(root!, "End");
        Assert.Equal(10, commits[^1].Value);
        Assert.Equal(4, commits.Count);
    }

    [Fact(DisplayName = "includes the reason for keyboard increments")]
    [Trait("Requirement", "NUM-KEY-01")]
    public async Task Includes_the_reason_for_keyboard_increments()
    {
        Render(1);

        await test.KeyAsync(root!, "ArrowUp");

        Assert.Equal(EventReason.Keyboard, Assert.Single(changes).Details.Reason);
    }

    [Fact(DisplayName = "should allow pasting a valid number")]
    [Trait("Requirement", "NUM-EVT-16")]
    public async Task Should_allow_pasting_a_valid_number()
    {
        Render(null);

        await test.PasteAsync(root!, "123");

        Assert.Equal("123", test.Text);
        Assert.Equal(123, Assert.Single(changes).Value);
        Assert.Equal(EventReason.InputPaste, changes[0].Details.Reason);
    }

    [Fact(DisplayName = "ignores a paste that does not parse to a number")]
    [Trait("Requirement", "NUM-EVT-16")]
    public async Task Ignores_a_paste_that_does_not_parse_to_a_number()
    {
        Render(1);
        int updates = test.NumberFieldOptions.Count;

        // Base UI selects the whole text and pastes "abc", so the spliced text is "abc".
        await test.PasteAsync(root!, "abc");

        Assert.Equal("1", test.Text);
        Assert.Empty(changes);
        Assert.Equal(updates, test.NumberFieldOptions.Count);
    }

    [Fact(DisplayName = "calls onValueChange and onValueCommitted on wheel")]
    [Trait("Requirement", "NUM-PTR-07")]
    public async Task Calls_onValueChange_and_onValueCommitted_on_wheel()
    {
        Render(5, wheel: true);

        await test.WheelAsync(root!, -1);
        Assert.Equal(4, changes[^1].Value);
        Assert.Equal(4, commits[^1].Value);
        Assert.Equal(EventReason.Wheel, commits[^1].Details.Reason);

        await test.WheelAsync(root!, 1);
        Assert.Equal(5, changes[^1].Value);
        Assert.Equal(2, commits.Count);

        // Blur doesn't commit again; the wheel changes were already committed.
        await test.BlurAsync(root!, test.Text, revision: 0);
        Assert.Equal(2, commits.Count);
    }

    [Fact(DisplayName = "applies an autofilled value to the visible input")]
    [Trait("Requirement", "NUM-FRM-05")]
    public async Task Applies_an_autofilled_value_to_the_visible_input()
    {
        Render(null);

        await test.AutofillAsync(root!, 7);

        Assert.Equal("7", test.Text);
        Assert.Equal(7, changes[^1].Value);
    }

    [Fact(DisplayName = "should not allow non-numeric characters on change")]
    [Trait("Requirement", "NUM-EVT-15")]
    [Trait("Requirement", "NUM-EDGE-06")]
    public async Task Should_not_allow_non_numeric_characters_on_change()
    {
        Render(null);

        await test.ChangeAsync(root!, "abc");

        Assert.Equal(string.Empty, test.Text);
        Assert.Empty(changes);
    }

    [Fact(DisplayName = "should allow numeric characters on change")]
    [Trait("Requirement", "NUM-EVT-15")]
    public async Task Should_allow_numeric_characters_on_change()
    {
        Render(null);

        await test.ChangeAsync(root!, "123");

        Assert.Equal("123", test.Text);
    }

    [Fact(DisplayName = "increments with keyboard from numeric state, not rounded display text")]
    [Trait("Requirement", "NUM-KEY-12")]
    public async Task Increments_with_keyboard_from_numeric_state_not_rounded_display_text()
    {
        Render(1.23456);
        Assert.Equal("1.235", test.Text);

        await test.KeyAsync(root!, "ArrowUp");

        Assert.Equal(2.23456, changes[^1].Value);
    }

    [Fact(DisplayName = "steps keyboard from dirty input text when the controlled value lags onValueChange")]
    [Trait("Requirement", "NUM-KEY-12")]
    public async Task Steps_keyboard_from_dirty_input_text_when_the_controlled_value_lags()
    {
        Render(0, controlled: true);

        await test.ChangeAsync(root!, "1.5");
        Assert.Equal(1.5, changes[^1].Value);

        // ArrowUp steps from the dirty text (1.5 -> 2.5), not the stale numeric state (0 -> 1).
        await test.KeyAsync(root!, "ArrowUp");
        Assert.Equal(2.5, changes[^1].Value);
    }

    [Fact(DisplayName = "commits formatted value only on blur")]
    [Trait("Requirement", "NUM-EVT-06")]
    [Trait("Requirement", "NUM-STA-05")]
    public async Task Commits_formatted_value_only_on_blur()
    {
        Render(null);

        await test.ChangeAsync(root!, "1234");
        Assert.Equal("1234", test.Text);

        await test.BlurAsync(root!, "1234", revision: 1);
        Assert.Equal("1,234", test.Text);
    }

    [Fact(DisplayName = "should commit validated number on blur (min)")]
    [Trait("Requirement", "NUM-EVT-06")]
    [Trait("Requirement", "NUM-STA-07")]
    public async Task Should_commit_validated_number_on_blur_min()
    {
        Render(null, min: 0);

        await test.ChangeAsync(root!, "-1");
        Assert.Equal("-1", test.Text);

        await test.BlurAsync(root!, "-1", revision: 1);
        Assert.Equal("0", test.Text);
    }

    private void Render(double? value, double? min = null, double? max = null, CultureInfo? culture = null, bool wheel = false, bool controlled = false)
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
            p.Add(x => x.Culture, culture ?? s_enUs);
            if (controlled)
            {
                p.Add(x => x.Value, value);
            }
            else
            {
                p.Add(x => x.DefaultValue, value);
            }

            p.Add(x => x.Min, min)
                .Add(x => x.Max, max)
                .Add(x => x.AllowWheelScrub, wheel)
                .Add(x => x.OnValueChange, (ValueChangeEventArgs<double?> e) => changes.Add(e))
                .Add(x => x.OnValueCommitted, (ValueCommittedEventArgs<double?> e) => commits.Add(e))
                .Add(x => x.ChildContent, parts);
        });
    }

    private IRenderedComponent<NumberFieldStepperHost> Host(double? value) =>
        test.Context.Render<NumberFieldStepperHost>(p => p.Add(x => x.Controlled, true).Add(x => x.DefaultValue, value));
}

using System.Globalization;
using Bunit;

// The namespace is the project's root: a namespace named after the family would hide the parts' types.
namespace Blazix.Primitives.Tests.Components;

// 6b's input role at L2 (issue 277): NumberFieldRoot.test.tsx's "integration: exotic inputs and IME" titles that change the
// text with fireEvent.change, which is the `number-field` module's change report here (ADR 06 §1). The module sends a text
// its own character test would revert as well, so C#'s character test answers (decision 7 (a)). Each Base UI App is a
// controlled root whose OnValueChange sets the value. The wheel, paste and key titles of the same block are L4
// (tests/browser/number-field.input-b2.spec.ts).
/// <summary>Exotic numerals, separators, signs and symbols typed into a number field.</summary>
public sealed class NumberFieldWheelPasteTests : IDisposable
{
    private static readonly CultureInfo s_enUs = CultureInfo.GetCultureInfo("en-US");

    private readonly NumberFieldTestContext test = new();
    private IRenderedComponent<NumberFieldStepperHost>? host;

    private List<double?> Changes => host!.Instance.Changes;

    /// <inheritdoc />
    public void Dispose() => test.Dispose();

    [Fact(DisplayName = "parses Persian digits and separators via change events")]
    [Trait("Requirement", "NUM-I18N-04")]
    public async Task Parses_Persian_digits_and_separators_via_change_events()
    {
        RenderApp();

        // ۱۲٫۳۴ => 12.34
        await test.ChangeAsync(host!, "۱۲٫۳۴");

        Assert.Equal([12.34], Changes);
    }

    [Fact(DisplayName = "parses Persian digits with Arabic group/decimal separators")]
    [Trait("Requirement", "NUM-I18N-04")]
    [Trait("Requirement", "NUM-I18N-08")]
    public async Task Parses_Persian_digits_with_Arabic_group_decimal_separators()
    {
        RenderApp();

        // ۱۲٬۳۴۵٫۶۷ => 12345.67
        await test.ChangeAsync(host!, "۱۲٬۳۴۵٫۶۷");

        Assert.Equal([12345.67], Changes);
    }

    [Fact(DisplayName = "parses percent and permille signs in exotic forms when formatted as percent")]
    [Trait("Requirement", "NUM-I18N-05")]
    [Trait("Requirement", "NUM-EDGE-17")]
    public async Task Parses_percent_and_permille_signs_in_exotic_forms_when_formatted_as_percent()
    {
        RenderApp(new NumberFormatOptions { Style = NumberFormatStyle.Percent });

        await test.ChangeAsync(host!, "١٢٪");
        Assert.Equal([0.12], Changes);

        // reset by typing again
        await test.ChangeAsync(host!, "12؉");
        Assert.Equal(2, Changes.Count);
        Assert.Equal(0.012, Changes[1]);
    }

    [Fact(DisplayName = "ignores percent and permille symbols when not formatted as percent")]
    [Trait("Requirement", "NUM-EVT-15")]
    [Trait("Requirement", "NUM-EDGE-17")]
    public async Task Ignores_percent_and_permille_symbols_when_not_formatted_as_percent()
    {
        // Base UI renders an uncontrolled root here.
        RenderApp(controlled: false);

        await test.ChangeAsync(host!, "12");
        Assert.Equal([12d], Changes);

        await test.ChangeAsync(host!, "12%");
        await test.ChangeAsync(host!, "12‰");

        Assert.Single(Changes);
        Assert.Equal("12", test.Text);
    }

    [Fact(DisplayName = "parses trailing unicode minus")]
    [Trait("Requirement", "NUM-I18N-10")]
    public async Task Parses_trailing_unicode_minus()
    {
        RenderApp();

        await test.ChangeAsync(host!, "1234−");

        Assert.Equal([-1234d], Changes);
    }

    [Fact(DisplayName = "treats parentheses negatives as invalid input")]
    [Trait("Requirement", "NUM-I18N-06")]
    [Trait("Requirement", "NUM-EVT-15")]
    [Trait("Requirement", "NUM-EDGE-16")]
    public async Task Treats_parentheses_negatives_as_invalid_input()
    {
        RenderApp();

        await test.ChangeAsync(host!, "(1,234.5)");

        Assert.Empty(Changes);
        Assert.Equal(string.Empty, test.Text);
    }

    // Base UI's App: a root whose OnValueChange records the value and, when controlled, sets it (the host's own state).
    private void RenderApp(NumberFormatOptions? format = null, bool controlled = true) =>
        host = test.Context.Render<NumberFieldStepperHost>(p => p
            .Add(x => x.Controlled, controlled)
            .Add(x => x.Culture, s_enUs)
            .Add(x => x.Format, format));
}

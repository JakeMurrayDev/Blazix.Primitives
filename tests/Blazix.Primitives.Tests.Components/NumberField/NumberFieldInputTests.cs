using System.Globalization;
using System.Text.Json;
using Bunit;
using Microsoft.AspNetCore.Components;

// The namespace is the project's root: a namespace named after the family would hide the parts' types.
namespace Blazix.Primitives.Tests.Components;

// NUM-EVT-12: NumberFieldInput.test.tsx's titles that focus and blur after an external value change, with no typed text
// (the typing titles are 6b's input role). A blur reports the text the input shows, which is the module's last `value`,
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

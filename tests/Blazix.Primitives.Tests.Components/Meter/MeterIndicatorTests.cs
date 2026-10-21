using AngleSharp.Dom;
using Bunit;
using Microsoft.Extensions.DependencyInjection;

// The namespace is the project's root: a namespace named after the family would hide the parts' types.
namespace Blazix.Primitives.Tests.Components;

// MeterIndicator.test.tsx. "sets positioning styles" and "sets zero width when value is 0" read computed pixels in a
// browser; here they are the inline declarations MTR-DOM-03 requires, and tests/browser/meter.spec.ts asserts the pixels.
/// <summary>The indicator of a meter: its inline sizing style.</summary>
public sealed class MeterIndicatorTests : IDisposable
{
    private readonly BunitContext context = new();

    public MeterIndicatorTests()
    {
        context.Services.AddBlazixPrimitives();
    }

    /// <inheritdoc />
    public void Dispose() => context.Dispose();

    private IElement RenderIndicator(double value, double min = 0, double max = 100, Action<Bunit.ComponentParameterCollectionBuilder<MeterIndicator>>? indicator = null)
    {
        IRenderedComponent<MeterRoot> cut = context.Render<MeterRoot>(p => p
            .Add(x => x.Value, value)
            .Add(x => x.Min, min)
            .Add(x => x.Max, max)
            .AddChildContent<MeterTrack>(t => t.AddChildContent<MeterIndicator>(i =>
            {
                i.AddUnmatched("data-testid", "indicator");
                indicator?.Invoke(i);
            })));

        return cut.Find("[data-testid=indicator]");
    }

    private static IReadOnlyList<string> Declarations(IElement element)
    {
        return (element.GetAttribute("style") ?? string.Empty).Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
    }

    [Fact(DisplayName = "clamps the width to 100% when the value exceeds max")]
    [Trait("Requirement", "MTR-ALG-01")]
    [Trait("Requirement", "MTR-DOM-03")]
    public void Clamps_the_width_to_100_percent_when_the_value_exceeds_max()
    {
        Assert.Contains("width: 100%", Declarations(RenderIndicator(150)));
    }

    [Fact(DisplayName = "clamps the width to 0% when the value is below min")]
    [Trait("Requirement", "MTR-ALG-01")]
    [Trait("Requirement", "MTR-DOM-03")]
    public void Clamps_the_width_to_0_percent_when_the_value_is_below_min()
    {
        Assert.Contains("width: 0%", Declarations(RenderIndicator(-10)));
    }

    [Fact(DisplayName = "produces a finite width when min equals max")]
    [Trait("Requirement", "MTR-ALG-01")]
    [Trait("Requirement", "MTR-EDGE-03")]
    public void Produces_a_finite_width_when_min_equals_max()
    {
        Assert.Contains("width: 0%", Declarations(RenderIndicator(5, min: 5, max: 5)));
    }

    // MTR-DOM-03: the three declarations are present for every value, in this order, written like a style attribute.
    [Fact(DisplayName = "sets positioning styles")]
    [Trait("Requirement", "MTR-DOM-03")]
    public void Sets_positioning_styles()
    {
        IReadOnlyList<string> declarations = Declarations(RenderIndicator(33));

        Assert.Equal(["inset-inline-start: 0", "height: inherit", "width: 33%"], declarations);
    }

    [Fact(DisplayName = "sets zero width when value is 0")]
    [Trait("Requirement", "MTR-DOM-03")]
    public void Sets_zero_width_when_value_is_0()
    {
        Assert.Equal(["inset-inline-start: 0", "height: inherit", "width: 0%"], Declarations(RenderIndicator(0)));
    }

    // MTR-ALG-03: the width keeps full precision and uses ECMAScript number text, not the culture's or "R" exponent text.
    [Fact(DisplayName = "writes the width with full precision")]
    [Trait("Requirement", "MTR-ALG-03")]
    [Trait("Requirement", "MTR-DOM-03")]
    public void Writes_the_width_with_full_precision()
    {
        Assert.Contains("width: 33.333333333333336%", Declarations(RenderIndicator(1, min: 0, max: 3)));
    }

    [Fact(DisplayName = "lets a consumer style replace the built-in declarations key by key")]
    [Trait("Requirement", "MTR-DOM-04")]
    public void Lets_a_consumer_style_replace_the_built_in_declarations_key_by_key()
    {
        IElement indicator = RenderIndicator(33, indicator: i => i.AddUnmatched("style", "width: 10px; color: red"));

        IReadOnlyList<string> declarations = Declarations(indicator);
        Assert.Contains("width: 10px", declarations);
        Assert.DoesNotContain("width: 33%", declarations);
        Assert.Contains("color: red", declarations);
        Assert.Contains("inset-inline-start: 0", declarations);
        Assert.Contains("height: inherit", declarations);
    }

    [Fact(DisplayName = "lets a style function replace the built-in declarations key by key")]
    [Trait("Requirement", "MTR-DOM-04")]
    [Trait("Requirement", "MTR-API-08")]
    public void Lets_a_style_function_replace_the_built_in_declarations_key_by_key()
    {
        IElement indicator = RenderIndicator(33, indicator: i => i.Add(x => x.StyleValue, _ => "height: 5px"));

        IReadOnlyList<string> declarations = Declarations(indicator);
        Assert.Contains("height: 5px", declarations);
        Assert.DoesNotContain("height: inherit", declarations);
        Assert.Contains("width: 33%", declarations);
    }

    [Fact(DisplayName = "emits no data attribute and no tabindex")]
    [Trait("Requirement", "MTR-DOM-02")]
    [Trait("Requirement", "MTR-FOC-01")]
    public void Emits_no_data_attribute_and_no_tabindex()
    {
        IElement indicator = RenderIndicator(33);

        Assert.DoesNotContain(indicator.Attributes, a => a.Name.StartsWith("data-", StringComparison.Ordinal) && a.Name != "data-testid");
        Assert.False(indicator.HasAttribute("tabindex"));
    }

    [Fact(DisplayName = "throws a descriptive error when rendered outside <MeterRoot>")]
    [Trait("Requirement", "MTR-EDGE-01")]
    [Trait("Requirement", "MTR-STR-02")]
    public void Throws_a_descriptive_error_when_rendered_outside_MeterRoot()
    {
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => context.Render<MeterIndicator>());

        Assert.Equal("Blazix: The `MeterRoot` context is missing. Meter parts must be placed within `<MeterRoot>`.", error.Message);
    }
}

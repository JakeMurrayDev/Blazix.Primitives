using System.Globalization;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using static Blazix.Primitives.Tests.Components.SliderTestContext;

// The namespace is the project's root: a namespace named after the family would hide the parts' types.
namespace Blazix.Primitives.Tests.Components;

// SliderRoot.test.tsx's prop titles at [L2] (docs/components/slider.md §13): the development warning, the step and
// min props, the range locale format, and SliderControl.test.tsx's tabindex title. The warning goes to the logger while
// the development-warnings switch is on, which it is for the whole run (Button/DevelopmentWarningsSwitch).
/// <summary>The slider root's props as C# renders them.</summary>
public sealed class SliderRootPropsTests : IDisposable
{
    private readonly SliderTestContext test = new();
    private readonly CapturingLogger<SliderRoot> logger = new();

    public SliderRootPropsTests()
    {
        test.Context.Services.AddSingleton<ILogger<SliderRoot>>(logger);
    }

    private BunitContext Context => test.Context;

    /// <inheritdoc />
    public void Dispose() => test.Dispose();

    [Fact(DisplayName = "warns when max is not greater than min")]
    [Trait("Requirement", "SLD-EDGE-02")]
    [Trait("Requirement", "SLD-API-06")]
    public void Warns_when_max_is_not_greater_than_min()
    {
        IRenderedComponent<SliderRoot> cut = Context.Render<SliderRoot>(p => p.Add(x => x.DefaultValue, 10).Add(x => x.Min, 10).Add(x => x.Max, 10).AddChildContent(Anatomy()));
        cut.Render(p => p.Add(x => x.DefaultValue, 10).Add(x => x.Min, 10).Add(x => x.Max, 10).AddChildContent(Anatomy()));

        (LogLevel _, string message) = Assert.Single(logger.Entries);
        Assert.Equal("Blazix: Slider `max` must be greater than `min`.", message);
    }

    [Fact(DisplayName = "supports non-integer values")]
    [Trait("Requirement", "SLD-ARIA-02")]
    [Trait("Requirement", "SLD-STA-03")]
    public void Supports_non_integer_values()
    {
        RenderFragment Slider(double value) => Part<SliderRoot>(p => p
            .Add(x => x.Value, value)
            .Add(x => x.Min, -100)
            .Add(x => x.Max, 100)
            .Add(x => x.Step, 0.00000001)
            .AddChildContent(Anatomy()));

        IRenderedComponent<SliderHost> cut = Context.Render<SliderHost>(p => p.AddChildContent(Fragments(Slider(51.1), Slider(0.00000005), Slider(1e-7))));

        IReadOnlyList<AngleSharp.Dom.IElement> inputs = Inputs(cut);
        Assert.Equal("51.1", inputs[0].GetAttribute("aria-valuenow"));
        Assert.Equal("5e-8", inputs[1].GetAttribute("aria-valuenow"));
        Assert.Equal("1e-7", inputs[2].GetAttribute("aria-valuenow"));
    }

    [Fact(DisplayName = "should use min as the step origin")]
    [Trait("Requirement", "SLD-STA-03")]
    public void Should_use_min_as_the_step_origin()
    {
        IRenderedComponent<SliderRoot> cut = Context.Render<SliderRoot>(p => p
            .Add(x => x.DefaultValue, 150)
            .Add(x => x.Step, 100)
            .Add(x => x.Max, 750)
            .Add(x => x.Min, 150)
            .AddChildContent(Anatomy()));

        Assert.Equal("150", Inputs(cut)[0].GetAttribute("aria-valuenow"));
    }

    // SLD-I18N-02: Base UI's `de-DE` with two fraction digits becomes `N2` under de-DE (the single-value title's precedent).
    [Fact(DisplayName = "port: sets the locale when formatting a range value")]
    [Trait("Requirement", "SLD-I18N-02")]
    [Trait("Requirement", "SLD-API-09")]
    public void Port_sets_the_locale_when_formatting_a_range_value()
    {
        IRenderedComponent<SliderRoot> cut = Context.Render<SliderRoot>(p => p
            .Add(x => x.Values, new double[] { 24.8, 70.51 })
            .Add(x => x.Format, "N2")
            .Add(x => x.Step, 0.01)
            .Add(x => x.Culture, CultureInfo.GetCultureInfo("de-DE"))
            .AddChildContent(Anatomy(Thumb(0), Thumb(1))));

        Assert.Equal("24,80 – 70,51", cut.Find("[data-testid=value]").TextContent);
    }

    [Fact(DisplayName = "does not apply a tabIndex by default")]
    [Trait("Requirement", "SLD-DOM-03")]
    [Trait("Requirement", "SLD-FOC-01")]
    public void Does_not_apply_a_tabIndex_by_default()
    {
        IRenderedComponent<SliderRoot> cut = Context.Render<SliderRoot>(p => p.Add(x => x.DefaultValue, 50).AddChildContent(Anatomy()));

        Assert.False(cut.Find("[data-testid=control]").HasAttribute("tabindex"));
    }
}

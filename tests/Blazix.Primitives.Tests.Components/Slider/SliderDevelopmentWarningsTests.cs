using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using static Blazix.Primitives.Tests.Components.SliderTestContext;

// The namespace is the project's root: a namespace named after the family would hide the parts' types.
namespace Blazix.Primitives.Tests.Components;

// SLD-STA-01 and SLD-EDGE-03 through the development warnings switch, which is on for the whole run
// (Button/DevelopmentWarningsSwitch): the root's messages are Controlled<T>'s (UTL-EDGE-22/23) with the name `Slider` and
// the state `value`, and the mode fixed at the first parameter set holds (the TabsDevelopmentWarningsTests precedent).
/// <summary>The controlled-state development messages of the slider root, and the ownership they leave unchanged.</summary>
public sealed class SliderDevelopmentWarningsTests : IDisposable
{
    private readonly SliderTestContext test = new();
    private readonly CapturingLogger<SliderRoot> logger = new();

    public SliderDevelopmentWarningsTests()
    {
        test.Context.Services.AddSingleton<ILogger<SliderRoot>>(logger);
    }

    /// <inheritdoc />
    public void Dispose() => test.Dispose();

    [Fact(DisplayName = "port: a value that stops being supplied logs the controlled-state error once, with the name Slider")]
    [Trait("Requirement", "SLD-STA-01")]
    [Trait("Requirement", "SLD-EDGE-03")]
    public void A_value_that_stops_being_supplied_logs_once()
    {
        RenderFragment content = Anatomy(Thumb());
        IRenderedComponent<SliderRoot> cut = test.Context.Render<SliderRoot>(p => p.Add(x => x.Value, 30).AddChildContent(content));

        cut.Render(p => p.AddChildContent(content));
        cut.Render(p => p.AddChildContent(content));

        (LogLevel _, string message) = Assert.Single(logger.Entries);
        Assert.StartsWith("A component is changing the controlled value state of Slider to be uncontrolled.", message, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "port: a value supplied after the first render logs the controlled-state error once, with the name Slider")]
    [Trait("Requirement", "SLD-STA-01")]
    [Trait("Requirement", "SLD-EDGE-03")]
    public void A_value_supplied_after_the_first_render_logs_once()
    {
        RenderFragment content = Anatomy(Thumb());
        IRenderedComponent<SliderRoot> cut = test.Context.Render<SliderRoot>(p => p.Add(x => x.DefaultValue, 20).AddChildContent(content));

        cut.Render(p => p.Add(x => x.DefaultValue, 20).Add(x => x.Value, 70).AddChildContent(content));
        cut.Render(p => p.Add(x => x.DefaultValue, 20).Add(x => x.Value, 80).AddChildContent(content));

        (LogLevel _, string message) = Assert.Single(logger.Entries);
        Assert.StartsWith("A component is changing the uncontrolled value state of Slider to be controlled.", message, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "port: a changed default value logs the default-value warning with the name Slider and keeps the value")]
    [Trait("Requirement", "SLD-EDGE-03")]
    [Trait("Requirement", "SLD-STA-01")]
    public void A_changed_default_value_logs_the_warning_and_keeps_the_value()
    {
        RenderFragment content = Anatomy(Thumb());
        IRenderedComponent<SliderRoot> cut = test.Context.Render<SliderRoot>(p => p.Add(x => x.DefaultValue, 20).AddChildContent(content));

        cut.Render(p => p.Add(x => x.DefaultValue, 60).AddChildContent(content));

        Assert.Single(logger.Entries, e => e.Message.StartsWith(
            "A component is changing the default value state of an uncontrolled Slider after being initialized.",
            StringComparison.Ordinal));
        Assert.Equal("20", cut.Find("input").GetAttribute("aria-valuenow"));
    }
}

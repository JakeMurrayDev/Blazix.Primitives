using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

// The namespace is the project's root: a namespace named after the family would hide the parts' types.
namespace Blazix.Primitives.Tests.Components;

// ProgressIndicator.test.tsx. "determinate", "sets zero width when value is 0" and "indeterminate" read computed styles in
// a browser; here they are the inline declarations PRG-DOM-03 and PRG-DOM-04 require, and tests/browser/progress.spec.ts
// asserts the computed styles.
/// <summary>The indicator of a progress bar: its inline sizing style and its status attributes.</summary>
public sealed class ProgressIndicatorTests : IDisposable
{
    private readonly BunitContext context = new();

    public ProgressIndicatorTests()
    {
        context.Services.AddBlazixPrimitives();
    }

    /// <inheritdoc />
    public void Dispose() => context.Dispose();

    private IElement RenderIndicator(double? value, Action<ComponentParameterCollectionBuilder<ProgressIndicator>>? indicator = null)
    {
        IRenderedComponent<ProgressRoot> cut = context.Render<ProgressRoot>(p => p
            .Add(x => x.Value, value)
            .AddChildContent<ProgressTrack>(t => t.AddChildContent<ProgressIndicator>(i =>
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

    [Fact(DisplayName = "determinate")]
    [Trait("Requirement", "PRG-DOM-03")]
    public void Determinate()
    {
        // The browser clause renders a span (render={<span />}); the declarations are the same on a substitute element.
        IElement indicator = RenderIndicator(33, i => i.Add(x => x.Render, builder =>
        {
            builder.OpenElement(0, "span");
            builder.CloseElement();
        }));

        Assert.Equal("span", indicator.TagName, ignoreCase: true);
        Assert.Equal(["inset-inline-start: 0", "height: inherit", "width: 33%"], Declarations(indicator));
    }

    [Fact(DisplayName = "sets zero width when value is 0")]
    [Trait("Requirement", "PRG-DOM-03")]
    public void Sets_zero_width_when_value_is_0()
    {
        Assert.Equal(["inset-inline-start: 0", "height: inherit", "width: 0%"], Declarations(RenderIndicator(0)));
    }

    [Fact(DisplayName = "indeterminate")]
    [Trait("Requirement", "PRG-DOM-04")]
    public void Indeterminate()
    {
        IElement indicator = RenderIndicator(null);

        // No built-in declaration at all: the element carries no style attribute.
        Assert.False(indicator.HasAttribute("style"));
        Assert.True(indicator.HasAttribute("data-indeterminate"));
    }

    [Fact(DisplayName = "writes the width with full precision")]
    [Trait("Requirement", "PRG-ALG-03")]
    [Trait("Requirement", "PRG-DOM-03")]
    public void Writes_the_width_with_full_precision()
    {
        IRenderedComponent<ProgressRoot> cut = context.Render<ProgressRoot>(p => p
            .Add(x => x.Value, 1)
            .Add(x => x.Max, 3)
            .AddChildContent<ProgressIndicator>(i => i.AddUnmatched("data-testid", "indicator")));

        Assert.Contains("width: 33.333333333333336%", Declarations(cut.Find("[data-testid=indicator]")));
    }

    [Fact(DisplayName = "lets a consumer style replace the built-in declarations key by key")]
    [Trait("Requirement", "PRG-DOM-05")]
    public void Lets_a_consumer_style_replace_the_built_in_declarations_key_by_key()
    {
        IElement indicator = RenderIndicator(33, i => i.AddUnmatched("style", "width: 10px; color: red"));

        IReadOnlyList<string> declarations = Declarations(indicator);
        Assert.Contains("width: 10px", declarations);
        Assert.DoesNotContain("width: 33%", declarations);
        Assert.Contains("color: red", declarations);
        Assert.Contains("inset-inline-start: 0", declarations);
        Assert.Contains("height: inherit", declarations);
    }

    [Fact(DisplayName = "removes the built-in declarations and keeps the consumer's when the status turns indeterminate")]
    [Trait("Requirement", "PRG-DOM-04")]
    [Trait("Requirement", "PRG-DOM-05")]
    public void Removes_the_built_in_declarations_and_keeps_the_consumers_when_the_status_turns_indeterminate()
    {
        IRenderedComponent<ProgressRoot> root = context.Render<ProgressRoot>(p => p
            .Add(x => x.Value, 50)
            .AddChildContent<ProgressIndicator>(i => i.AddUnmatched("data-testid", "indicator").AddUnmatched("style", "color: red")));
        string[] determinate = Declarations(root.Find("[data-testid=indicator]")).Order().ToArray();
        Assert.Equal(new[] { "color: red", "height: inherit", "inset-inline-start: 0", "width: 50%" }, determinate);

        root.Render(p => p.Add(x => x.Value, (double?)null));

        Assert.Equal(["color: red"], Declarations(root.Find("[data-testid=indicator]")));
    }

    [Fact(DisplayName = "carries exactly one status attribute")]
    [Trait("Requirement", "PRG-DOM-02")]
    public void Carries_exactly_one_status_attribute()
    {
        IElement indicator = RenderIndicator(40);

        Assert.True(indicator.HasAttribute("data-progressing"));
        Assert.False(indicator.HasAttribute("data-indeterminate"));
        Assert.False(indicator.HasAttribute("data-complete"));
        Assert.False(indicator.HasAttribute("data-status"));
        Assert.False(indicator.HasAttribute("tabindex"));
    }

    [Fact(DisplayName = "throws a descriptive error when rendered outside <ProgressRoot>")]
    [Trait("Requirement", "PRG-EDGE-01")]
    [Trait("Requirement", "PRG-STR-02")]
    public void Throws_a_descriptive_error_when_rendered_outside_ProgressRoot()
    {
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => context.Render<ProgressIndicator>());

        Assert.Equal("Blazix: The `ProgressRoot` context is missing. Progress parts must be placed within `<ProgressRoot>`.", error.Message);
    }
}

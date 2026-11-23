using System.Globalization;
using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

// The namespace is the project's root: a namespace named after the family would hide the parts' types.
namespace Blazix.Primitives.Tests.Components;

// ProgressValue.test.tsx. Base UI's `children` function is the template parameter ValueContent (ADR 08 Amendments
// 2026-10-20: named for what it renders, used with Context); its argument is ProgressValueArguments(FormattedValue, Value),
// where FormattedValue is the literal "indeterminate" while the bar is indeterminate (PRG-API-10; the two sentinels differ
// by design, §16 item 3). Plain ChildContent is discarded (PRG-API-11).
/// <summary>The value text of a progress bar.</summary>
public sealed class ProgressValueTests : IDisposable
{
    private static readonly CultureInfo s_enUs = CultureInfo.GetCultureInfo("en-US");

    private readonly BunitContext context = new();

    public ProgressValueTests()
    {
        context.Services.AddBlazixPrimitives();
    }

    /// <inheritdoc />
    public void Dispose() => context.Dispose();

    [Fact(DisplayName = "renders the value when children is not provided")]
    [Trait("Requirement", "PRG-API-11")]
    [Trait("Requirement", "PRG-I18N-01")]
    public void Renders_the_value_when_children_is_not_provided()
    {
        IRenderedComponent<ProgressRoot> cut = context.Render<ProgressRoot>(p => p
            .Add(x => x.Value, 30)
            .Add(x => x.Culture, s_enUs)
            .AddChildContent<ProgressValue>(v => v.AddUnmatched("data-testid", "value")));

        Assert.Equal("30%", cut.Find("[data-testid=value]").TextContent);
    }

    [Fact(DisplayName = "renders a formatted value when a format is provided")]
    [Trait("Requirement", "PRG-API-11")]
    [Trait("Requirement", "PRG-I18N-02")]
    public void Renders_a_formatted_value_when_a_format_is_provided()
    {
        IRenderedComponent<ProgressRoot> cut = context.Render<ProgressRoot>(p => p
            .Add(x => x.Value, 30)
            .Add(x => x.Format, "C")
            .Add(x => x.Culture, s_enUs)
            .AddChildContent<ProgressValue>(v => v.AddUnmatched("data-testid", "value")));

        Assert.Equal("$30.00", cut.Find("[data-testid=value]").TextContent);
    }

    [Fact(DisplayName = "numerical value")]
    [Trait("Requirement", "PRG-API-10")]
    [Trait("Requirement", "PRG-EVT-02")]
    public void Numerical_value()
    {
        List<ProgressValueArguments> calls = [];
        context.Render<ProgressRoot>(p => p
            .Add(x => x.Value, 30)
            .Add(x => x.Format, "C")
            .Add(x => x.Culture, s_enUs)
            .AddChildContent<ProgressValue>(v => v.Add(x => x.ValueContent, args =>
            {
                calls.Add(args);
                return builder => builder.AddContent(0, args.FormattedValue);
            })));

        Assert.Equal(new ProgressValueArguments("$30.00", 30), calls[^1]);
    }

    [Theory(DisplayName = "indeterminate value $label")]
    [Trait("Requirement", "PRG-API-10")]
    [Trait("Requirement", "PRG-EVT-02")]
    [Trait("Requirement", "PRG-EDGE-04")]
    [InlineData("null", null)]
    [InlineData("NaN", double.NaN)]
    public void Indeterminate_value(string label, double? value)
    {
        _ = label;

        List<ProgressValueArguments> calls = [];
        IRenderedComponent<ProgressRoot> cut = context.Render<ProgressRoot>(p => p
            .Add(x => x.Value, value)
            .Add(x => x.Format, "C")
            .Add(x => x.Culture, s_enUs)
            .AddChildContent<ProgressValue>(v => v.Add(x => x.ValueContent, args =>
            {
                calls.Add(args);
                return builder => builder.AddContent(0, args.FormattedValue);
            })));

        // The first argument is the literal "indeterminate"; the raw value passes through as given.
        Assert.Equal("indeterminate", calls[^1].FormattedValue);
        Assert.Equal(value, calls[^1].Value);
        Assert.Equal("indeterminate", cut.Find("span").TextContent);
    }

    [Fact(DisplayName = "passes updated arguments to the template when the value changes")]
    [Trait("Requirement", "PRG-EVT-02")]
    public void Passes_updated_arguments_to_the_template_when_the_value_changes()
    {
        List<ProgressValueArguments> calls = [];
        RenderFragment<ProgressValueArguments> content = args =>
        {
            calls.Add(args);
            return builder => builder.AddContent(0, args.FormattedValue);
        };
        IRenderedComponent<ProgressAnatomyWithTemplate> cut = context.Render<ProgressAnatomyWithTemplate>(p => p
            .Add(x => x.Value, 30)
            .Add(x => x.Culture, s_enUs)
            .Add(x => x.Content, content));
        Assert.Equal(new ProgressValueArguments("30%", 30), calls[^1]);

        cut.Render(p => p.Add(x => x.Value, 60));
        Assert.Equal(new ProgressValueArguments("60%", 60), calls[^1]);
        Assert.Equal("60%", cut.Find("span[aria-hidden]").TextContent);

        cut.Render(p => p.Add(x => x.Value, (double?)null));
        Assert.Equal(new ProgressValueArguments("indeterminate", null), calls[^1]);
    }

    [Fact(DisplayName = "renders no child node while indeterminate")]
    [Trait("Requirement", "PRG-API-11")]
    [Trait("Requirement", "PRG-DOM-06")]
    public void Renders_no_child_node_while_indeterminate()
    {
        IRenderedComponent<ProgressRoot> cut = context.Render<ProgressRoot>(p => p
            .Add(x => x.Value, (double?)null)
            .AddChildContent<ProgressValue>(v => v.AddUnmatched("data-testid", "value")));

        Assert.Empty(cut.Find("[data-testid=value]").ChildNodes);
    }

    [Fact(DisplayName = "discards plain child content")]
    [Trait("Requirement", "PRG-API-11")]
    public void Discards_plain_child_content()
    {
        IRenderedComponent<ProgressRoot> cut = context.Render<ProgressRoot>(p => p
            .Add(x => x.Value, 30)
            .Add(x => x.Culture, s_enUs)
            .AddChildContent<ProgressValue>(v => v
                .AddUnmatched("data-testid", "value")
                .AddChildContent("ignored")));

        Assert.Equal("30%", cut.Find("[data-testid=value]").TextContent);
    }

    [Fact(DisplayName = "hides the text from assistive technology in every status unless the consumer overrides it")]
    [Trait("Requirement", "PRG-DOM-07")]
    [Trait("Requirement", "PRG-DOM-09")]
    public void Hides_the_text_from_assistive_technology_in_every_status_unless_the_consumer_overrides_it()
    {
        foreach (double? value in new double?[] { 30, 100, null })
        {
            IRenderedComponent<ProgressRoot> cut = context.Render<ProgressRoot>(p => p
                .Add(x => x.Value, value)
                .AddChildContent<ProgressValue>(v => v.AddUnmatched("data-testid", "default")));
            Assert.Equal("true", cut.Find("[data-testid=default]").GetAttribute("aria-hidden"));
        }

        IRenderedComponent<ProgressRoot> mine = context.Render<ProgressRoot>(p => p
            .Add(x => x.Value, 30)
            .AddChildContent<ProgressValue>(v => v.AddUnmatched("data-testid", "mine").AddUnmatched("aria-hidden", "false")));
        Assert.Equal("false", mine.Find("[data-testid=mine]").GetAttribute("aria-hidden"));
    }

    [Fact(DisplayName = "renders the same text as the root's default aria-valuetext while determinate")]
    [Trait("Requirement", "PRG-DOM-06")]
    public void Renders_the_same_text_as_the_roots_default_aria_valuetext_while_determinate()
    {
        IRenderedComponent<ProgressRoot> cut = context.Render<ProgressRoot>(p => p
            .Add(x => x.Value, 12.5)
            .Add(x => x.Culture, s_enUs)
            .AddChildContent<ProgressValue>(v => v.AddUnmatched("data-testid", "value")));

        Assert.Equal(cut.Find("[role=progressbar]").GetAttribute("aria-valuetext"), cut.Find("[data-testid=value]").TextContent);
    }

    [Fact(DisplayName = "throws a descriptive error when rendered outside <ProgressRoot>")]
    [Trait("Requirement", "PRG-EDGE-01")]
    [Trait("Requirement", "PRG-STR-02")]
    public void Throws_a_descriptive_error_when_rendered_outside_ProgressRoot()
    {
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => context.Render<ProgressValue>());

        Assert.Equal("Blazix: The `ProgressRoot` context is missing. Progress parts must be placed within `<ProgressRoot>`.", error.Message);
    }
}

using System.Globalization;
using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

// The namespace is the project's root: a namespace named after the family would hide the parts' types.
namespace Blazix.Primitives.Tests.Components;

// MeterValue.test.tsx. Base UI's `children` function is the template parameter ValueContent (ADR 08 Amendments
// 2026-10-20: named for what it renders, used with Context); its argument is MeterValueArguments(FormattedValue, Value).
// Plain ChildContent is discarded, as Base UI discards a non-function children (MTR-API-11).
/// <summary>The value text of a meter.</summary>
public sealed class MeterValueTests : IDisposable
{
    private static readonly CultureInfo s_enUs = CultureInfo.GetCultureInfo("en-US");

    private readonly BunitContext context = new();

    public MeterValueTests()
    {
        context.Services.AddBlazixPrimitives();
    }

    /// <inheritdoc />
    public void Dispose() => context.Dispose();

    [Fact(DisplayName = "renders the value when children is not provided")]
    [Trait("Requirement", "MTR-API-11")]
    [Trait("Requirement", "MTR-I18N-01")]
    public void Renders_the_value_when_children_is_not_provided()
    {
        IRenderedComponent<MeterRoot> cut = context.Render<MeterRoot>(p => p
            .Add(x => x.Value, 30)
            .Add(x => x.Culture, s_enUs)
            .AddChildContent<MeterValue>(v => v.AddUnmatched("data-testid", "value")));

        Assert.Equal("30%", cut.Find("[data-testid=value]").TextContent);
    }

    [Fact(DisplayName = "renders a formatted value when a format is provided")]
    [Trait("Requirement", "MTR-API-11")]
    [Trait("Requirement", "MTR-I18N-02")]
    public void Renders_a_formatted_value_when_a_format_is_provided()
    {
        IRenderedComponent<MeterRoot> cut = context.Render<MeterRoot>(p => p
            .Add(x => x.Value, 30)
            .Add(x => x.Format, "C")
            .Add(x => x.Culture, s_enUs)
            .AddChildContent<MeterValue>(v => v.AddUnmatched("data-testid", "value")));

        Assert.Equal("$30.00", cut.Find("[data-testid=value]").TextContent);
    }

    [Fact(DisplayName = "accepts a render function")]
    [Trait("Requirement", "MTR-API-10")]
    public void Accepts_a_render_function()
    {
        List<MeterValueArguments> calls = [];
        IRenderedComponent<MeterRoot> cut = context.Render<MeterRoot>(p => p
            .Add(x => x.Value, 30)
            .Add(x => x.Format, "C")
            .Add(x => x.Culture, s_enUs)
            .AddChildContent<MeterValue>(v => v
                .AddUnmatched("data-testid", "value")
                .Add(x => x.ValueContent, args =>
                {
                    calls.Add(args);
                    return builder => builder.AddContent(0, $"{args.FormattedValue} / {args.Value}");
                })));

        Assert.Equal("$30.00", calls[^1].FormattedValue);
        Assert.Equal(30, calls[^1].Value);
        Assert.Equal("$30.00 / 30", cut.Find("[data-testid=value]").TextContent);
    }

    [Fact(DisplayName = "passes updated arguments to the render function when value changes")]
    [Trait("Requirement", "MTR-API-10")]
    [Trait("Requirement", "MTR-EVT-02")]
    public void Passes_updated_arguments_to_the_render_function_when_value_changes()
    {
        List<MeterValueArguments> calls = [];
        RenderFragment<MeterValueArguments> content = args =>
        {
            calls.Add(args);
            return builder => builder.AddContent(0, args.FormattedValue);
        };

        IRenderedComponent<MeterRoot> cut = context.Render<MeterRoot>(p => p
            .Add(x => x.Value, 30)
            .Add(x => x.Culture, s_enUs)
            .AddChildContent<MeterValue>(v => v.Add(x => x.ValueContent, content)));

        Assert.Equal(new MeterValueArguments("30%", 30), calls[^1]);

        cut.Render(p => p.Add(x => x.Value, 60));

        Assert.Equal(new MeterValueArguments("60%", 60), calls[^1]);
        Assert.Equal("60%", cut.Find("span").TextContent);
    }

    // MTR-EDGE-04: the template receives the formatted clamped value and the raw value.
    [Fact(DisplayName = "passes the raw value beside the formatted clamped value")]
    [Trait("Requirement", "MTR-EDGE-04")]
    [Trait("Requirement", "MTR-API-10")]
    public void Passes_the_raw_value_beside_the_formatted_clamped_value()
    {
        MeterValueArguments? last = null;
        context.Render<MeterRoot>(p => p
            .Add(x => x.Value, 150)
            .Add(x => x.Culture, s_enUs)
            .AddChildContent<MeterValue>(v => v.Add(x => x.ValueContent, args =>
            {
                last = args;
                return builder => { };
            })));

        Assert.Equal(new MeterValueArguments("100%", 150), last);
    }

    [Fact(DisplayName = "discards plain child content")]
    [Trait("Requirement", "MTR-API-11")]
    public void Discards_plain_child_content()
    {
        IRenderedComponent<MeterRoot> cut = context.Render<MeterRoot>(p => p
            .Add(x => x.Value, 30)
            .Add(x => x.Culture, s_enUs)
            .AddChildContent<MeterValue>(v => v
                .AddUnmatched("data-testid", "value")
                .AddChildContent("ignored")));

        Assert.Equal("30%", cut.Find("[data-testid=value]").TextContent);
    }

    [Fact(DisplayName = "hides the text from assistive technology unless the consumer overrides it")]
    [Trait("Requirement", "MTR-DOM-06")]
    [Trait("Requirement", "MTR-DOM-08")]
    public void Hides_the_text_from_assistive_technology_unless_the_consumer_overrides_it()
    {
        IRenderedComponent<MeterRoot> cut = context.Render<MeterRoot>(p => p
            .Add(x => x.Value, 30)
            .AddChildContent<MeterValue>(v => v.AddUnmatched("data-testid", "default")));
        Assert.Equal("true", cut.Find("[data-testid=default]").GetAttribute("aria-hidden"));

        cut = context.Render<MeterRoot>(p => p
            .Add(x => x.Value, 30)
            .AddChildContent<MeterValue>(v => v.AddUnmatched("data-testid", "mine").AddUnmatched("aria-hidden", "false")));
        Assert.Equal("false", cut.Find("[data-testid=mine]").GetAttribute("aria-hidden"));
    }

    [Fact(DisplayName = "renders the same text as the root's default aria-valuetext")]
    [Trait("Requirement", "MTR-DOM-05")]
    public void Renders_the_same_text_as_the_roots_default_aria_valuetext()
    {
        IRenderedComponent<MeterRoot> cut = context.Render<MeterRoot>(p => p
            .Add(x => x.Value, 12.5)
            .Add(x => x.Culture, s_enUs)
            .AddChildContent<MeterValue>(v => v.AddUnmatched("data-testid", "value")));

        Assert.Equal(cut.Find("[role=meter]").GetAttribute("aria-valuetext"), cut.Find("[data-testid=value]").TextContent);
    }

    [Fact(DisplayName = "throws a descriptive error when rendered outside <MeterRoot>")]
    [Trait("Requirement", "MTR-EDGE-01")]
    [Trait("Requirement", "MTR-STR-02")]
    public void Throws_a_descriptive_error_when_rendered_outside_MeterRoot()
    {
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => context.Render<MeterValue>());

        Assert.Equal("Blazix: The `MeterRoot` context is missing. Meter parts must be placed within `<MeterRoot>`.", error.Message);
    }
}

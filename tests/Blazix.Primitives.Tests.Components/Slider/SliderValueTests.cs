using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components;
using static Blazix.Primitives.Tests.Components.SliderTestContext;

// The namespace is the project's root: a namespace named after the family would hide the parts' types.
namespace Blazix.Primitives.Tests.Components;

// SliderValue.test.tsx and SliderLabel.test.tsx, the [L2] rows: the output's text, `for` and aria-live; the label's id.
/// <summary>The slider value and label parts.</summary>
public sealed class SliderValueTests : IDisposable
{
    private readonly SliderTestContext test = new();

    private BunitContext Context => test.Context;

    /// <inheritdoc />
    public void Dispose() => test.Dispose();

    [Fact(DisplayName = "renders a single value")]
    [Trait("Requirement", "SLD-DOM-18")]
    [Trait("Requirement", "SLD-API-26")]
    public void Renders_a_single_value()
    {
        IRenderedComponent<SliderRoot> cut = Context.Render<SliderRoot>(p => p.Add(x => x.DefaultValue, 40).Add(x => x.Culture, EnUs).AddChildContent(Anatomy()));

        IElement output = cut.Find("[data-testid=value]");
        Assert.Equal("OUTPUT", output.TagName);
        Assert.Equal("40", output.TextContent);
        Assert.Equal("off", output.GetAttribute("aria-live"));
    }

    [Fact(DisplayName = "renders a range")]
    [Trait("Requirement", "SLD-DOM-18")]
    public void Renders_a_range()
    {
        IRenderedComponent<SliderRoot> cut = Context.Render<SliderRoot>(p => p.Add(x => x.DefaultValues, new double[] { 40, 65 }).Add(x => x.Culture, EnUs).AddChildContent(Anatomy(Thumb(0), Thumb(1))));

        Assert.Equal("40 – 65", cut.Find("[data-testid=value]").TextContent);
    }

    [Fact(DisplayName = "renders all thumb values")]
    [Trait("Requirement", "SLD-DOM-18")]
    [Trait("Requirement", "SLD-EDGE-11")]
    public void Renders_all_thumb_values()
    {
        IRenderedComponent<SliderRoot> cut = Context.Render<SliderRoot>(p => p.Add(x => x.DefaultValues, new double[] { 40, 60, 80, 95 }).Add(x => x.Culture, EnUs).AddChildContent(Anatomy(Thumb(0), Thumb(1))));

        Assert.Equal("40 – 60 – 80 – 95", cut.Find("[data-testid=value]").TextContent);
    }

    [Fact(DisplayName = "accepts a render function")]
    [Trait("Requirement", "SLD-API-26")]
    public void Accepts_a_render_function()
    {
        RenderFragment<SliderValueArguments> template = args => builder =>
        {
            builder.OpenElement(0, "span");
            builder.AddAttribute(1, "data-testid", "custom");
            builder.AddContent(2, string.Join(",", args.FormattedValues) + "|" + string.Join(",", args.Values));
            builder.CloseElement();
        };
        IRenderedComponent<SliderRoot> cut = Context.Render<SliderRoot>(p => p
            .Add(x => x.DefaultValues, new double[] { 1234, 5678 })
            .Add(x => x.Max, 10000)
            .Add(x => x.Culture, EnUs)
            .AddChildContent(Fragments(Part<SliderValue>(v => v.Add(x => x.ValueContent, template)), Anatomy(Thumb(0), Thumb(1)))));

        Assert.Equal("1,234,5,678|1234,5678", cut.Find("[data-testid=custom]").TextContent);
    }

    [Fact(DisplayName = "recomputes the formatted output when the format option changes")]
    [Trait("Requirement", "SLD-EDGE-17")]
    public void Recomputes_the_formatted_output_when_the_format_option_changes()
    {
        IRenderedComponent<SliderRoot> cut = Context.Render<SliderRoot>(p => p.Add(x => x.DefaultValue, 0.5).Add(x => x.Max, 1).Add(x => x.Step, 0.1).Add(x => x.Culture, EnUs).AddChildContent(Anatomy()));
        Assert.Equal("0.5", cut.Find("[data-testid=value]").TextContent);

        cut.Render(p => p.Add(x => x.DefaultValue, 0.5).Add(x => x.Max, 1).Add(x => x.Step, 0.1).Add(x => x.Format, "P0").Add(x => x.Culture, EnUs).AddChildContent(Anatomy()));

        Assert.Equal("50%", cut.Find("[data-testid=value]").TextContent);
    }

    [Fact(DisplayName = "associates the output with every thumb input")]
    [Trait("Requirement", "SLD-DOM-17")]
    [Trait("Requirement", "SLD-DOM-15")]
    [Trait("Requirement", "SLD-ANI-06")]
    public async Task Associates_the_output_with_every_thumb_input()
    {
        IRenderedComponent<SliderRoot> cut = Context.Render<SliderRoot>(p => p.Add(x => x.DefaultValues, new double[] { 20, 80 }).AddChildContent(Anatomy(Thumb(0), Thumb(1))));
        IElement output = cut.Find("[data-testid=value]");
        Assert.False(output.HasAttribute("for"));

        await test.ReportThumbsAsync(cut);

        IReadOnlyList<IElement> inputs = Inputs(cut);
        Assert.NotEqual(inputs[0].Id, inputs[1].Id);
        Assert.False(string.IsNullOrEmpty(inputs[0].Id));
        Assert.Equal($"{inputs[0].Id} {inputs[1].Id}", cut.Find("[data-testid=value]").GetAttribute("for"));
    }

    // SLD-API-27, SLD-DOM-19: the label's id is always `<root id>-label`.
    [Fact(DisplayName = "port: the label's id is the root's with -label, whatever id the consumer gives")]
    [Trait("Requirement", "SLD-API-27")]
    [Trait("Requirement", "SLD-DOM-19")]
    public void Port_the_labels_id_is_the_roots_with_label()
    {
        IRenderedComponent<SliderRoot> cut = Context.Render<SliderRoot>(p => p
            .AddUnmatched("id", "volume")
            .AddChildContent(Fragments(Part<SliderLabel>(l => l.AddUnmatched("id", "ignored").AddUnmatched("data-testid", "label").AddChildContent("Volume")), Anatomy())));

        IElement label = cut.Find("[data-testid=label]");
        Assert.Equal("volume-label", label.Id);
        Assert.Equal("DIV", label.TagName);
        Assert.False(label.HasAttribute("for"));
    }

    [Fact(DisplayName = "Slider.Label")]
    [Trait("Requirement", "SLD-ARIA-06")]
    [Trait("Requirement", "SLD-ARIA-07")]
    [Trait("Requirement", "SLD-DOM-19")]
    public void Slider_Label()
    {
        IRenderedComponent<SliderRoot> cut = Context.Render<SliderRoot>(p => p
            .AddUnmatched("id", "volume")
            .AddChildContent(Fragments(Part<SliderLabel>(l => l.AddChildContent("Volume")), Anatomy())));

        Assert.Equal("volume-label", cut.Find("[role=group]").GetAttribute("aria-labelledby"));
        Assert.Equal("volume-label", Inputs(cut)[0].GetAttribute("aria-labelledby"));
    }

    [Fact(DisplayName = "updates Slider.Label linkage when root id changes")]
    [Trait("Requirement", "SLD-API-13")]
    [Trait("Requirement", "SLD-ARIA-07")]
    public void Updates_Slider_Label_linkage_when_root_id_changes()
    {
        RenderFragment content = Fragments(Part<SliderLabel>(l => l.AddUnmatched("data-testid", "label").AddChildContent("Volume")), Anatomy());
        IRenderedComponent<SliderRoot> cut = Context.Render<SliderRoot>(p => p.AddUnmatched("id", "first").AddChildContent(content));
        Assert.Equal("first-label", cut.Find("[data-testid=label]").Id);

        cut.Render(p => p.AddUnmatched("id", "second").AddChildContent(content));

        Assert.Equal("second-label", cut.Find("[data-testid=label]").Id);
        Assert.Equal("second-label", cut.Find("[role=group]").GetAttribute("aria-labelledby"));
    }

    [Fact(DisplayName = "does not set fallback aria-labelledby when no label is rendered")]
    [Trait("Requirement", "SLD-ARIA-05")]
    [Trait("Requirement", "SLD-ARIA-07")]
    public void Does_not_set_fallback_aria_labelledby_when_no_label_is_rendered()
    {
        IRenderedComponent<SliderRoot> cut = Context.Render<SliderRoot>(p => p.AddChildContent(Anatomy()));

        Assert.False(cut.Find("[role=group]").HasAttribute("aria-labelledby"));
        Assert.False(Inputs(cut)[0].HasAttribute("aria-labelledby"));
    }
}

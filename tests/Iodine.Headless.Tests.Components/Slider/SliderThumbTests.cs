using System.Text.Json;
using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using static Blazix.Primitives.Tests.Components.SliderTestContext;

// The namespace is the project's root: a namespace named after the family would hide the parts' types.
namespace Blazix.Primitives.Tests.Components;

// SliderThumb.test.tsx, the [L2] rows: the thumb element, its nested input and what C# renders under the handover. The
// positions, keys and focus are the module's (L3, L4).
/// <summary>The slider thumb: its element, its input and its registration.</summary>
public sealed class SliderThumbTests : IDisposable
{
    private readonly SliderTestContext test = new();

    private BunitContext Context => test.Context;

    /// <inheritdoc />
    public void Dispose() => test.Dispose();

    [Fact(DisplayName = "renders the nested input as a sibling to children")]
    [Trait("Requirement", "SLD-STR-02")]
    [Trait("Requirement", "SLD-API-24")]
    public void Renders_the_nested_input_as_a_sibling_to_children()
    {
        IRenderedComponent<SliderRoot> cut = Context.Render<SliderRoot>(p => p.AddChildContent(Anatomy(Thumb(parameters: t => t.AddChildContent(builder =>
        {
            builder.OpenElement(0, "span");
            builder.AddAttribute(1, "data-testid", "child");
            builder.CloseElement();
        })))));

        IElement thumb = cut.Find("[data-testid=thumb]");
        Assert.Equal(2, thumb.Children.Length);
        Assert.Equal("child", thumb.Children[0].GetAttribute("data-testid"));
        Assert.Equal("INPUT", thumb.Children[1].TagName);
    }

    [Fact(DisplayName = "renders the nested input when using the short form render prop")]
    [Trait("Requirement", "SLD-STR-02")]
    public void Renders_the_nested_input_when_using_the_short_form_render_prop()
    {
        RenderFragment substitute = builder =>
        {
            builder.OpenElement(0, "span");
            builder.AddAttribute(1, "data-testid", "custom");
            builder.CloseElement();
        };
        IRenderedComponent<SliderRoot> cut = Context.Render<SliderRoot>(p => p.AddChildContent(Anatomy(Part<SliderThumb>(t => t.AddUnmatched("Render", substitute)))));

        IElement custom = cut.Find("[data-testid=custom]");
        Assert.Equal("SPAN", custom.TagName);
        Assert.Equal("INPUT", Assert.Single(custom.Children).TagName);
    }

    [Fact(DisplayName = "renders the nested input when using the long form render prop")]
    [Trait("Requirement", "SLD-STR-02")]
    public void Renders_the_nested_input_when_using_the_long_form_render_prop()
    {
        RenderFragment<Blazix.Primitives.Rendering.RenderProps<SliderThumbState>> render = props => builder =>
        {
            builder.OpenElement(0, "span");
            builder.AddMultipleAttributes(1, props.Attributes);
            builder.AddAttribute(2, "data-testid", "custom");
            builder.AddContent(3, props.ChildContent);
            builder.CloseElement();
        };
        IRenderedComponent<SliderRoot> cut = Context.Render<SliderRoot>(p => p.AddChildContent(Anatomy(Part<SliderThumb>(t => t.Add(x => x.RenderFunction, render)))));

        IElement custom = cut.Find("[data-testid=custom]");
        Assert.Equal("INPUT", Assert.Single(custom.Children).TagName);
    }

    [Fact(DisplayName = "sets the thumb index data attribute")]
    [Trait("Requirement", "SLD-DOM-08")]
    [Trait("Requirement", "SLD-API-17")]
    public void Sets_the_thumb_index_data_attribute()
    {
        IRenderedComponent<SliderRoot> cut = Context.Render<SliderRoot>(p => p
            .Add(x => x.DefaultValues, new double[] { 20, 40 })
            .AddChildContent(Anatomy(Thumb(0), Thumb(1))));

        Assert.Equal("0", cut.Find("[data-testid=thumb-0]").GetAttribute("data-index"));
        Assert.Equal("1", cut.Find("[data-testid=thumb-1]").GetAttribute("data-index"));
    }

    // SLD-API-17: without Index, a range thumb is -1 until the registry reports (SLD-STR-03, SLD-ANI-06).
    [Fact(DisplayName = "port: a range thumb without an index takes its registry position")]
    [Trait("Requirement", "SLD-API-17")]
    [Trait("Requirement", "SLD-STR-03")]
    public async Task Port_a_range_thumb_without_an_index_takes_its_registry_position()
    {
        IRenderedComponent<SliderRoot> cut = Context.Render<SliderRoot>(p => p
            .Add(x => x.DefaultValues, new double[] { 20, 40 })
            .AddChildContent(Anatomy(Part<SliderThumb>(t => t.AddUnmatched("data-testid", "a")), Part<SliderThumb>(t => t.AddUnmatched("data-testid", "b")))));
        Assert.Equal("-1", cut.Find("[data-testid=a]").GetAttribute("data-index"));
        Assert.False(Inputs(cut)[0].HasAttribute("aria-valuenow"));

        await test.ReportThumbsAsync(cut);

        Assert.Equal("0", cut.Find("[data-testid=a]").GetAttribute("data-index"));
        Assert.Equal("1", cut.Find("[data-testid=b]").GetAttribute("data-index"));
        Assert.Equal("40", Inputs(cut)[1].GetAttribute("aria-valuenow"));
    }

    [Fact(DisplayName = "does not commit NaN when more thumbs are rendered than values")]
    [Trait("Requirement", "SLD-STR-03")]
    [Trait("Requirement", "SLD-EDGE-11")]
    [Trait("Requirement", "SLD-DOM-11")]
    public void Does_not_commit_NaN_when_more_thumbs_are_rendered_than_values()
    {
        IRenderedComponent<SliderRoot> cut = Context.Render<SliderRoot>(p => p
            .Add(x => x.DefaultValues, new double[] { 20, 40 })
            .AddChildContent(Anatomy(Thumb(0), Thumb(1), Thumb(2))));

        IElement extra = Inputs(cut)[2];
        Assert.False(extra.HasAttribute("aria-valuenow"));
        Assert.Equal(string.Empty, extra.GetAttribute("value"));
        Assert.StartsWith("clip-path: inset(50%)", cut.Find("[data-testid=thumb-2]").GetAttribute("style"), StringComparison.Ordinal);
    }

    [Fact(DisplayName = "forwards aria-label to the input")]
    [Trait("Requirement", "SLD-API-18")]
    [Trait("Requirement", "SLD-ARIA-05")]
    public void Forwards_aria_label_to_the_input()
    {
        IRenderedComponent<SliderRoot> cut = Context.Render<SliderRoot>(p => p.AddChildContent(Anatomy(Thumb(parameters: t => t.AddUnmatched("aria-label", "Volume")))));

        Assert.Equal("Volume", Inputs(cut)[0].GetAttribute("aria-label"));
        Assert.False(cut.Find("[data-testid=thumb]").HasAttribute("aria-label"));
    }

    [Fact(DisplayName = "does not set aria-labelledby when getAriaLabel is provided")]
    [Trait("Requirement", "SLD-API-18")]
    [Trait("Requirement", "SLD-ARIA-05")]
    [Trait("Requirement", "SLD-ARIA-06")]
    public void Does_not_set_aria_labelledby_when_getAriaLabel_is_provided()
    {
        IRenderedComponent<SliderRoot> cut = Context.Render<SliderRoot>(p => p
            .AddUnmatched("aria-labelledby", "root-label")
            .AddChildContent(Anatomy(Thumb(parameters: t => t.Add(x => x.GetAriaLabel, (Func<int, string?>)(i => $"Thumb {i}"))))));

        IElement input = Inputs(cut)[0];
        Assert.Equal("Thumb 0", input.GetAttribute("aria-label"));
        Assert.False(input.HasAttribute("aria-labelledby"));
    }

    [Fact(DisplayName = "prefers getAriaValueText over a direct aria-valuetext prop")]
    [Trait("Requirement", "SLD-API-19")]
    [Trait("Requirement", "SLD-ARIA-04")]
    public void Prefers_getAriaValueText_over_a_direct_aria_valuetext_prop()
    {
        IRenderedComponent<SliderRoot> cut = Context.Render<SliderRoot>(p => p
            .Add(x => x.DefaultValue, 50)
            .Add(x => x.Culture, EnUs)
            .AddChildContent(Anatomy(Thumb(parameters: t => t
                .AddUnmatched("aria-valuetext", "direct")
                .Add(x => x.GetAriaValueText, (Func<string, double, int, string?>)((formatted, value, index) => $"{formatted}|{value}|{index}"))))));

        Assert.Equal("50|50|0", Inputs(cut)[0].GetAttribute("aria-valuetext"));
    }

    [Fact(DisplayName = "forwards aria-valuetext to the input")]
    [Trait("Requirement", "SLD-API-19")]
    public void Forwards_aria_valuetext_to_the_input()
    {
        IRenderedComponent<SliderRoot> cut = Context.Render<SliderRoot>(p => p.AddChildContent(Anatomy(Thumb(parameters: t => t.AddUnmatched("aria-valuetext", "loud")))));

        Assert.Equal("loud", Inputs(cut)[0].GetAttribute("aria-valuetext"));
    }

    [Fact(DisplayName = "forwards aria-labelledby to the input")]
    [Trait("Requirement", "SLD-API-20")]
    [Trait("Requirement", "SLD-ARIA-06")]
    public void Forwards_aria_labelledby_to_the_input()
    {
        IRenderedComponent<SliderRoot> cut = Context.Render<SliderRoot>(p => p.AddChildContent(Anatomy(Thumb(parameters: t => t.AddUnmatched("aria-labelledby", "custom")))));

        Assert.Equal("custom", Inputs(cut)[0].GetAttribute("aria-labelledby"));
        Assert.False(cut.Find("[data-testid=thumb]").HasAttribute("aria-labelledby"));
    }

    [Fact(DisplayName = "forwards aria-describedby to the input")]
    [Trait("Requirement", "SLD-API-20")]
    [Trait("Requirement", "SLD-ARIA-08")]
    public void Forwards_aria_describedby_to_the_input()
    {
        IRenderedComponent<SliderRoot> cut = Context.Render<SliderRoot>(p => p.AddChildContent(Anatomy(Thumb(parameters: t => t.AddUnmatched("aria-describedby", "help")))));

        Assert.Equal("help", Inputs(cut)[0].GetAttribute("aria-describedby"));
        Assert.False(cut.Find("[data-testid=thumb]").HasAttribute("aria-describedby"));
    }

    [Fact(DisplayName = "does not apply tabIndex to the thumb element by default")]
    [Trait("Requirement", "SLD-API-23")]
    [Trait("Requirement", "SLD-DOM-13")]
    [Trait("Requirement", "SLD-FOC-01")]
    public void Does_not_apply_tabIndex_to_the_thumb_element_by_default()
    {
        IRenderedComponent<SliderRoot> cut = Context.Render<SliderRoot>(p => p.AddChildContent(Anatomy()));

        Assert.False(cut.Find("[data-testid=thumb]").HasAttribute("tabindex"));
        Assert.False(Inputs(cut)[0].HasAttribute("tabindex"));
        Assert.False(cut.Find("[data-testid=control]").HasAttribute("tabindex"));
    }

    [Fact(DisplayName = "can be removed from the tab sequence")]
    [Trait("Requirement", "SLD-API-23")]
    public void Can_be_removed_from_the_tab_sequence()
    {
        IRenderedComponent<SliderRoot> cut = Context.Render<SliderRoot>(p => p.AddChildContent(Anatomy(Thumb(parameters: t => t.AddUnmatched("tabindex", -1)))));

        Assert.Equal("-1", Inputs(cut)[0].GetAttribute("tabindex"));
        Assert.False(cut.Find("[data-testid=thumb]").HasAttribute("tabindex"));
    }

    // SLD-DOM-09, SLD-ANI-05: the centre-mode position C# renders before the module attaches.
    [Fact(DisplayName = "port: renders the centre-mode position of the value at once")]
    [Trait("Requirement", "SLD-ANI-05")]
    [Trait("Requirement", "SLD-DOM-09")]
    [Trait("Requirement", "SLD-STA-20")]
    public void Port_renders_the_centre_mode_position_of_the_value_at_once()
    {
        IRenderedComponent<SliderRoot> cut = Context.Render<SliderRoot>(p => p.Add(x => x.DefaultValue, 30).AddChildContent(Anatomy()));

        Assert.Equal("position: absolute; inset-inline-start: 30%; top: 50%; translate: -50% -50%", cut.Find("[data-testid=thumb]").GetAttribute("style"));
        Assert.Equal("position: relative; height: inherit; inset-inline-start: 0; width: 30%", cut.Find("[data-testid=indicator]").GetAttribute("style"));
    }

    // SLD-DOM-10, SLD-ANI-02/04: an inset thumb is hidden at 0% until the module's first pass.
    [Fact(DisplayName = "port: renders an inset thumb hidden until the module positions it")]
    [Trait("Requirement", "SLD-ANI-02")]
    [Trait("Requirement", "SLD-DOM-10")]
    public void Port_renders_an_inset_thumb_hidden_until_the_module_positions_it()
    {
        IRenderedComponent<SliderRoot> cut = Context.Render<SliderRoot>(p => p.Add(x => x.DefaultValue, 30).Add(x => x.ThumbAlignment, SliderThumbAlignment.Edge).AddChildContent(Anatomy()));

        string style = cut.Find("[data-testid=thumb]").GetAttribute("style")!;
        Assert.Contains("inset-inline-start: var(--position)", style, StringComparison.Ordinal);
        Assert.Contains("--position: 0%", style, StringComparison.Ordinal);
        Assert.Contains("visibility: hidden", style, StringComparison.Ordinal);
        Assert.StartsWith("visibility: hidden", cut.Find("[data-testid=indicator]").GetAttribute("style"), StringComparison.Ordinal);
        Assert.True(test.LastOptions.GetProperty("inset").GetBoolean());
    }

    // ADR 04 Amendments 2026-10-18 (the handover): after the attach C# keeps the style and the input value it rendered.
    [Fact(DisplayName = "port: the thumb keeps the style and value it attached with, and sends the consumer's style")]
    [Trait("Requirement", "SLD-DOM-09")]
    [Trait("Requirement", "SLD-DOM-16")]
    [Trait("Requirement", "SLD-API-25")]
    public async Task Port_the_thumb_keeps_the_style_and_value_it_attached_with()
    {
        IRenderedComponent<SliderRoot> cut = Context.Render<SliderRoot>(p => p.Add(x => x.DefaultValue, 30).AddChildContent(Anatomy(Thumb(parameters: t => t.AddUnmatched("style", "width: 10px")))));
        string attached = cut.Find("[data-testid=thumb]").GetAttribute("style")!;
        Assert.Equal("position: absolute; inset-inline-start: 30%; top: 50%; translate: -50% -50%; width: 10px", attached.TrimEnd(';'));

        await test.RequestAsync(cut, "keyboard", 0, [31]);

        Assert.Equal(attached, cut.Find("[data-testid=thumb]").GetAttribute("style"));
        Assert.Equal("30", Inputs(cut)[0].GetAttribute("value"));
        Assert.Equal("31", Inputs(cut)[0].GetAttribute("aria-valuenow"));
        Assert.Equal("width: 10px", test.LastOptions.GetProperty("thumbs")[0].GetProperty("style").GetString());
    }

    // SLD-API-22: the consumer's focus, blur and key handlers are declined on the thumb element and run from the module.
    [Fact(DisplayName = "port: forwards focus and blur to the input from the module's report")]
    [Trait("Requirement", "SLD-API-22")]
    public async Task Port_forwards_focus_and_blur_to_the_input_from_the_modules_report()
    {
        List<string> events = [];
        IRenderedComponent<SliderRoot> cut = Context.Render<SliderRoot>(p => p.AddChildContent(Anatomy(Thumb(parameters: t => t
            .AddUnmatched("onfocus", EventCallback.Factory.Create<FocusEventArgs>(this, e => events.Add(e.Type ?? string.Empty)))
            .AddUnmatched("onblur", EventCallback.Factory.Create<FocusEventArgs>(this, e => events.Add(e.Type ?? string.Empty)))))));
        Assert.False(cut.Find("[data-testid=thumb]").HasAttribute("onfocus"));

        await test.FocusAsync(cut, 0, focused: true);
        await test.FocusAsync(cut, 0, focused: false);

        Assert.Equal(["focus", "blur"], events);
    }

    [Fact(DisplayName = "port: a consumer keydown asks the key decision point")]
    [Trait("Requirement", "SLD-API-22")]
    [Trait("Requirement", "SLD-KEY-01")]
    [Trait("Requirement", "SLD-EVT-09")]
    public async Task Port_a_consumer_keydown_asks_the_key_decision_point()
    {
        IRenderedComponent<SliderRoot> cut = Context.Render<SliderRoot>(p => p.AddChildContent(Anatomy(Thumb(parameters: t => t
            .AddUnmatched("onkeydown", EventCallback.Factory.Create<KeyboardEventArgs>(this, e => e.PreventPartHandler()))))));
        Assert.True(test.LastOptions.GetProperty("keyHandlers").GetBoolean());

        string inputId = Inputs(cut)[0].Id!;
        string? verdict = await cut.InvokeAsync(() => test.SliderTwin().Decide(Blazix.Primitives.Interop.SliderTwin.KeyPoint, KeyJson("ArrowRight", inputId)));

        Assert.Equal("stop", verdict);
    }
}

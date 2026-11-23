using System.Globalization;
using AngleSharp.Dom;
using Blazix.Primitives.Rendering;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

// The namespace is the project's root: a namespace named after the family would hide the parts' types.
namespace Blazix.Primitives.Tests.Components;

// MeterRoot.test.tsx, with Base UI's titles. The cultures are explicit (en-US, de-DE) wherever Base UI compares with
// Intl.NumberFormat in the same runtime, because the port's text comes from .NET globalization (ADR 01 decision 2:
// equivalent, not byte-identical). Where a test follows the current culture it computes the expectation the same way the
// part must (MTR-API-05).
/// <summary>The root of a meter: its ARIA attributes, formatting, range handling and callbacks.</summary>
public sealed class MeterRootTests : IDisposable
{
    private static readonly CultureInfo s_enUs = CultureInfo.GetCultureInfo("en-US");
    private static readonly CultureInfo s_deDe = CultureInfo.GetCultureInfo("de-DE");

    private readonly BunitContext context = new();

    public MeterRootTests()
    {
        context.Services.AddBlazixPrimitives();
    }

    /// <inheritdoc />
    public void Dispose() => context.Dispose();

    private static string Percent(double fraction, CultureInfo? culture = null) => fraction.ToString("P0", culture ?? CultureInfo.CurrentCulture);

    private IRenderedComponent<MeterAnatomy> RenderAnatomy(Action<ComponentParameterCollectionBuilder<MeterAnatomy>> parameters)
    {
        return context.Render<MeterAnatomy>(parameters);
    }

    private static string Width(IElement indicator)
    {
        // Parse the width declaration out of the inline style the way a style declaration reader would.
        string style = indicator.GetAttribute("style") ?? string.Empty;
        foreach (string declaration in style.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            int colon = declaration.IndexOf(':');
            if (colon > 0 && declaration[..colon].Trim() == "width")
            {
                return declaration[(colon + 1)..].Trim();
            }
        }

        return string.Empty;
    }

    [Fact(DisplayName = "sets the correct aria attributes")]
    [Trait("Requirement", "MTR-STR-01")]
    [Trait("Requirement", "MTR-ARIA-01")]
    [Trait("Requirement", "MTR-ARIA-02")]
    [Trait("Requirement", "MTR-ARIA-03")]
    [Trait("Requirement", "MTR-ARIA-04")]
    [Trait("Requirement", "MTR-ARIA-06")]
    [Trait("Requirement", "MTR-API-02")]
    [Trait("Requirement", "MTR-API-03")]
    [Trait("Requirement", "MTR-DOM-07")]
    public void Sets_the_correct_aria_attributes()
    {
        IRenderedComponent<MeterAnatomy> cut = RenderAnatomy(p => p.Add(x => x.Value, 30).Add(x => x.ShowLabel, true));

        IElement meter = cut.Find("[role=meter]");
        Assert.Equal("30", meter.GetAttribute("aria-valuenow"));
        Assert.Equal("0", meter.GetAttribute("aria-valuemin"));
        Assert.Equal("100", meter.GetAttribute("aria-valuemax"));
        Assert.Equal(Percent(0.3), meter.GetAttribute("aria-valuetext"));
        IElement label = cut.Find("[data-testid=label]");
        Assert.False(string.IsNullOrEmpty(label.Id));
        Assert.Equal(label.Id, meter.GetAttribute("aria-labelledby"));
        Assert.Equal("presentation", label.GetAttribute("role"));
    }

    // German percent formatting separates the sign from the number (Intl writes U+00A0, .NET a plain space): the .NET
    // output is the recorded equivalent (MTR-I18N-01).
    [Fact(DisplayName = "defaults aria-valuetext to the localized formatted value, matching Meter.Value")]
    [Trait("Requirement", "MTR-I18N-01")]
    [Trait("Requirement", "MTR-ARIA-04")]
    [Trait("Requirement", "MTR-DOM-05")]
    [Trait("Requirement", "MTR-STA-02")]
    public void Defaults_aria_valuetext_to_the_localized_formatted_value_matching_Meter_Value()
    {
        IRenderedComponent<MeterAnatomy> cut = RenderAnatomy(p => p.Add(x => x.Value, 30).Add(x => x.Culture, s_deDe));

        string? valueText = cut.Find("[role=meter]").GetAttribute("aria-valuetext");
        Assert.Equal("30 %", valueText);
        Assert.Equal(valueText, cut.Find("[data-testid=value]").TextContent);
    }

    [Fact(DisplayName = "rounds the default aria-valuetext like the displayed value")]
    [Trait("Requirement", "MTR-I18N-01")]
    [Trait("Requirement", "MTR-ARIA-04")]
    [Trait("Requirement", "MTR-STA-02")]
    public void Rounds_the_default_aria_valuetext_like_the_displayed_value()
    {
        IRenderedComponent<MeterAnatomy> cut = RenderAnatomy(p => p.Add(x => x.Value, 33.333).Add(x => x.Culture, s_enUs));

        string? valueText = cut.Find("[role=meter]").GetAttribute("aria-valuetext");
        Assert.Equal("33%", valueText);
        Assert.Equal(valueText, cut.Find("[data-testid=value]").TextContent);
    }

    [Fact(DisplayName = "refreshes aria-valuenow, aria-valuetext, the value text, and the indicator when value changes")]
    [Trait("Requirement", "MTR-API-01")]
    [Trait("Requirement", "MTR-STA-02")]
    [Trait("Requirement", "MTR-DOM-03")]
    public void Refreshes_aria_valuenow_aria_valuetext_the_value_text_and_the_indicator_when_value_changes()
    {
        IRenderedComponent<MeterAnatomy> cut = RenderAnatomy(p => p.Add(x => x.Value, 50).Add(x => x.Culture, s_enUs));

        IElement meter = cut.Find("[role=meter]");
        Assert.Equal("50", meter.GetAttribute("aria-valuenow"));
        Assert.Equal("50%", meter.GetAttribute("aria-valuetext"));
        Assert.Equal("50%", cut.Find("[data-testid=value]").TextContent);
        Assert.Equal("50%", Width(cut.Find("[data-testid=indicator]")));

        cut.Render(p => p.Add(x => x.Value, 77));

        meter = cut.Find("[role=meter]");
        Assert.Equal("77", meter.GetAttribute("aria-valuenow"));
        Assert.Equal("77%", meter.GetAttribute("aria-valuetext"));
        Assert.Equal("77%", cut.Find("[data-testid=value]").TextContent);
        Assert.Equal("77%", Width(cut.Find("[data-testid=indicator]")));
    }

    [Fact(DisplayName = "uses the returned text and receives the formatted and raw value")]
    [Trait("Requirement", "MTR-API-06")]
    [Trait("Requirement", "MTR-ARIA-05")]
    [Trait("Requirement", "MTR-EVT-01")]
    [Trait("Requirement", "MTR-DOM-05")]
    public void Uses_the_returned_text_and_receives_the_formatted_and_raw_value()
    {
        List<(string Formatted, double Value)> calls = [];
        Func<string, double, string> getAriaValueText = (formatted, value) =>
        {
            calls.Add((formatted, value));
            return $"{value} of 100 ({formatted})";
        };

        IRenderedComponent<MeterAnatomy> cut = RenderAnatomy(p => p
            .Add(x => x.Value, 30)
            .Add(x => x.Culture, s_enUs)
            .Add(x => x.GetAriaValueText, getAriaValueText));

        Assert.Contains(("30%", 30d), calls);
        Assert.Equal("30 of 100 (30%)", cut.Find("[role=meter]").GetAttribute("aria-valuetext"));
        // getAriaValueText only affects the spoken text, not the visible value.
        Assert.Equal("30%", cut.Find("[data-testid=value]").TextContent);
    }

    [Fact(DisplayName = "formats the value as its position within a custom range and keeps the indicator in sync")]
    [Trait("Requirement", "MTR-ALG-01")]
    [Trait("Requirement", "MTR-ALG-03")]
    [Trait("Requirement", "MTR-ARIA-02")]
    public void Formats_the_value_as_its_position_within_a_custom_range_and_keeps_the_indicator_in_sync()
    {
        IRenderedComponent<MeterAnatomy> cut = RenderAnatomy(p => p
            .Add(x => x.Value, 0.5)
            .Add(x => x.Min, 0)
            .Add(x => x.Max, 1)
            .Add(x => x.Culture, s_enUs));

        IElement meter = cut.Find("[role=meter]");
        Assert.Equal("0.5", meter.GetAttribute("aria-valuenow"));
        Assert.Equal("50%", meter.GetAttribute("aria-valuetext"));
        Assert.Equal("50%", cut.Find("[data-testid=value]").TextContent);
        Assert.Equal("50%", Width(cut.Find("[data-testid=indicator]")));
    }

    [Fact(DisplayName = "formats the value relative to a non-zero min")]
    [Trait("Requirement", "MTR-ALG-01")]
    [Trait("Requirement", "MTR-I18N-01")]
    public void Formats_the_value_relative_to_a_non_zero_min()
    {
        IRenderedComponent<MeterAnatomy> cut = RenderAnatomy(p => p
            .Add(x => x.Value, 30)
            .Add(x => x.Min, 20)
            .Add(x => x.Max, 40)
            .Add(x => x.Culture, s_enUs));

        Assert.Equal("50%", cut.Find("[role=meter]").GetAttribute("aria-valuetext"));
        Assert.Equal("50%", cut.Find("[data-testid=value]").TextContent);
    }

    [Fact(DisplayName = "keeps range attributes, formatted text, and the indicator synchronized on rerender")]
    [Trait("Requirement", "MTR-API-01")]
    [Trait("Requirement", "MTR-ARIA-03")]
    public void Keeps_range_attributes_formatted_text_and_the_indicator_synchronized_on_rerender()
    {
        IRenderedComponent<MeterAnatomy> cut = RenderAnatomy(p => p
            .Add(x => x.Value, 20)
            .Add(x => x.Min, 10)
            .Add(x => x.Max, 30)
            .Add(x => x.Culture, s_enUs));

        IElement meter = cut.Find("[role=meter]");
        Assert.Equal("10", meter.GetAttribute("aria-valuemin"));
        Assert.Equal("30", meter.GetAttribute("aria-valuemax"));
        Assert.Equal("20", meter.GetAttribute("aria-valuenow"));
        Assert.Equal("50%", meter.GetAttribute("aria-valuetext"));
        Assert.Equal("50%", cut.Find("[data-testid=value]").TextContent);
        Assert.Equal("50%", Width(cut.Find("[data-testid=indicator]")));

        cut.Render(p => p.Add(x => x.Min, 20).Add(x => x.Max, 60).Add(x => x.Value, 50));

        meter = cut.Find("[role=meter]");
        Assert.Equal("20", meter.GetAttribute("aria-valuemin"));
        Assert.Equal("60", meter.GetAttribute("aria-valuemax"));
        Assert.Equal("50", meter.GetAttribute("aria-valuenow"));
        Assert.Equal("75%", meter.GetAttribute("aria-valuetext"));
        Assert.Equal("75%", cut.Find("[data-testid=value]").TextContent);
        Assert.Equal("75%", Width(cut.Find("[data-testid=indicator]")));
    }

    [Theory(DisplayName = "normalizes aria attributes when $label")]
    [Trait("Requirement", "MTR-ALG-02")]
    [Trait("Requirement", "MTR-EDGE-02")]
    [Trait("Requirement", "MTR-EDGE-03")]
    [Trait("Requirement", "MTR-EDGE-04")]
    [InlineData("value exceeds max", 150.0, 0.0, 100.0, "100", "100%")]
    [InlineData("value is below min", -10.0, 0.0, 100.0, "0", "0%")]
    [InlineData("min equals max", 5.0, 5.0, 5.0, "5", "0%")]
    [InlineData("value is NaN", double.NaN, 0.0, 100.0, "0", "0%")]
    public void Normalizes_aria_attributes(string label, double value, double min, double max, string ariaValueNow, string ariaValueText)
    {
        _ = label;

        IRenderedComponent<MeterAnatomy> cut = RenderAnatomy(p => p
            .Add(x => x.Value, value)
            .Add(x => x.Min, min)
            .Add(x => x.Max, max)
            .Add(x => x.Culture, s_enUs));

        IElement meter = cut.Find("[role=meter]");
        Assert.Equal(ariaValueNow, meter.GetAttribute("aria-valuenow"));
        Assert.Equal(ariaValueText, meter.GetAttribute("aria-valuetext"));
    }

    [Fact(DisplayName = "formats the value")]
    [Trait("Requirement", "MTR-API-04")]
    [Trait("Requirement", "MTR-I18N-02")]
    public void Formats_the_value()
    {
        IRenderedComponent<MeterAnatomy> cut = RenderAnatomy(p => p
            .Add(x => x.Value, 30)
            .Add(x => x.Format, "C")
            .Add(x => x.Culture, s_enUs));

        Assert.Equal("$30.00", cut.Find("[data-testid=value]").TextContent);
        Assert.Equal("$30.00", cut.Find("[role=meter]").GetAttribute("aria-valuetext"));
    }

    [Fact(DisplayName = "formats the clamped value while clamping range attributes and indicator width")]
    [Trait("Requirement", "MTR-I18N-02")]
    [Trait("Requirement", "MTR-ALG-02")]
    [Trait("Requirement", "MTR-API-06")]
    [Trait("Requirement", "MTR-EVT-01")]
    [Trait("Requirement", "MTR-EDGE-04")]
    public void Formats_the_clamped_value_while_clamping_range_attributes_and_indicator_width()
    {
        List<(string Formatted, double Value)> calls = [];
        Func<string, double, string> getAriaValueText = (formatted, raw) =>
        {
            calls.Add((formatted, raw));
            return $"{formatted} (raw: {raw})";
        };

        IRenderedComponent<MeterAnatomy> cut = RenderAnatomy(p => p
            .Add(x => x.Value, 150)
            .Add(x => x.Format, "C")
            .Add(x => x.Culture, s_enUs)
            .Add(x => x.GetAriaValueText, getAriaValueText));

        IElement meter = cut.Find("[role=meter]");
        Assert.Equal("$100.00", cut.Find("[data-testid=value]").TextContent);
        Assert.Equal("100", meter.GetAttribute("aria-valuenow"));
        Assert.Equal(("$100.00", 150d), calls[^1]);
        Assert.Equal("$100.00 (raw: 150)", meter.GetAttribute("aria-valuetext"));
        Assert.Equal("100%", Width(cut.Find("[data-testid=indicator]")));
    }

    [Fact(DisplayName = "sets the locale when formatting the value")]
    [Trait("Requirement", "MTR-API-05")]
    [Trait("Requirement", "MTR-I18N-03")]
    public void Sets_the_locale_when_formatting_the_value()
    {
        // In the German culture numbers use a dot for thousands and a comma for decimals.
        IRenderedComponent<MeterAnatomy> cut = RenderAnatomy(p => p
            .Add(x => x.Value, 86.49)
            .Add(x => x.Format, "F2")
            .Add(x => x.Culture, s_deDe));

        Assert.Equal("86,49", cut.Find("[data-testid=value]").TextContent);
    }

    // ---- The port's own tests, for the rows that cite no Base UI test or that the typed port reads differently ----

    [Fact(DisplayName = "uses the current culture when no culture is given")]
    [Trait("Requirement", "MTR-API-05")]
    [Trait("Requirement", "MTR-I18N-03")]
    public void Uses_the_current_culture_when_no_culture_is_given()
    {
        CultureInfo previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = s_deDe;
            IRenderedComponent<MeterAnatomy> cut = RenderAnatomy(p => p.Add(x => x.Value, 30));

            Assert.Equal("30 %", cut.Find("[role=meter]").GetAttribute("aria-valuetext"));
            Assert.Equal("30 %", cut.Find("[data-testid=value]").TextContent);
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact(DisplayName = "renders the children followed by one visually hidden span with the character x")]
    [Trait("Requirement", "MTR-STR-01")]
    [Trait("Requirement", "MTR-DOM-01")]
    public void Renders_the_children_followed_by_one_visually_hidden_span_with_the_character_x()
    {
        IRenderedComponent<MeterRoot> cut = context.Render<MeterRoot>(p => p
            .Add(x => x.Value, 30)
            .AddChildContent("child"));

        IElement meter = cut.Find("[role=meter]");
        Assert.Equal("div", meter.TagName, ignoreCase: true);
        IElement hidden = meter.QuerySelector("span[role=presentation]")!;
        Assert.Equal("x", hidden.TextContent);
        Assert.Equal(
            "clip-path: inset(50%); overflow: hidden; white-space: nowrap; border: 0; padding: 0; width: 1px; height: 1px; margin: 0; position: fixed; top: 0; left: 0",
            hidden.GetAttribute("style")?.TrimEnd(';'));
        // The span is the last child, after the consumer's content, and the root's text ends with it (docs §14).
        Assert.Same(hidden, meter.LastElementChild);
        Assert.Equal("childx", meter.TextContent);
    }

    [Fact(DisplayName = "emits no data attribute of its own")]
    [Trait("Requirement", "MTR-DOM-02")]
    [Trait("Requirement", "MTR-FOC-01")]
    public void Emits_no_data_attribute_of_its_own()
    {
        IRenderedComponent<MeterAnatomy> cut = RenderAnatomy(p => p.Add(x => x.Value, 30).Add(x => x.ShowLabel, true));

        foreach (IElement element in cut.FindAll("*"))
        {
            foreach (IAttr attribute in element.Attributes)
            {
                // The anatomy's data-testid attributes are the consumer's; nothing else starts with data-.
                Assert.False(
                    attribute.Name.StartsWith("data-", StringComparison.Ordinal) && attribute.Name != "data-testid",
                    $"{element.TagName} carries {attribute.Name}");
                Assert.NotEqual("tabindex", attribute.Name);
            }
        }
    }

    [Fact(DisplayName = "lets a consumer aria-valuetext replace the computed one while getAriaValueText still runs")]
    [Trait("Requirement", "MTR-API-07")]
    [Trait("Requirement", "MTR-EVT-01")]
    public void Lets_a_consumer_aria_valuetext_replace_the_computed_one_while_getAriaValueText_still_runs()
    {
        int calls = 0;
        IRenderedComponent<MeterRoot> cut = context.Render<MeterRoot>(p => p
            .Add(x => x.Value, 30)
            .Add(x => x.Culture, s_enUs)
            .Add(x => x.GetAriaValueText, (formatted, value) => { calls++; return "from the function"; })
            .AddUnmatched("aria-valuetext", "from the consumer"));

        Assert.Equal("from the consumer", cut.Find("[role=meter]").GetAttribute("aria-valuetext"));
        Assert.True(calls > 0);
    }

    [Fact(DisplayName = "lets consumer attributes replace the built-in ones")]
    [Trait("Requirement", "MTR-DOM-08")]
    [Trait("Requirement", "MTR-API-09")]
    [Trait("Requirement", "MTR-ARIA-06")]
    public void Lets_consumer_attributes_replace_the_built_in_ones()
    {
        IRenderedComponent<MeterRoot> cut = context.Render<MeterRoot>(p => p
            .Add(x => x.Value, 30)
            .AddUnmatched("role", "progressbar")
            .AddUnmatched("aria-valuenow", "7")
            .AddUnmatched("aria-valuemin", "1")
            .AddUnmatched("aria-valuemax", "9")
            .AddUnmatched("aria-labelledby", "mine")
            .AddUnmatched("data-testid", "meter"));

        IElement meter = cut.Find("[data-testid=meter]");
        Assert.Equal("progressbar", meter.GetAttribute("role"));
        Assert.Equal("7", meter.GetAttribute("aria-valuenow"));
        Assert.Equal("1", meter.GetAttribute("aria-valuemin"));
        Assert.Equal("9", meter.GetAttribute("aria-valuemax"));
        Assert.Equal("mine", meter.GetAttribute("aria-labelledby"));
    }

    [Fact(DisplayName = "follows an inverted range literally")]
    [Trait("Requirement", "MTR-EDGE-05")]
    public void Follows_an_inverted_range_literally()
    {
        IRenderedComponent<MeterAnatomy> cut = RenderAnatomy(p => p
            .Add(x => x.Value, 30)
            .Add(x => x.Min, 100)
            .Add(x => x.Max, 0)
            .Add(x => x.Culture, s_enUs));

        // min > max is not validated: the clamp returns its lower bound and the percentage is 70.
        Assert.Equal("100", cut.Find("[role=meter]").GetAttribute("aria-valuenow"));
        Assert.Equal("100", cut.Find("[role=meter]").GetAttribute("aria-valuemin"));
        Assert.Equal("0", cut.Find("[role=meter]").GetAttribute("aria-valuemax"));
        Assert.Equal("70%", Width(cut.Find("[data-testid=indicator]")));
    }

    [Fact(DisplayName = "passes the empty state to the class and style functions")]
    [Trait("Requirement", "MTR-STA-01")]
    [Trait("Requirement", "MTR-API-08")]
    public void Passes_the_empty_state_to_the_class_and_style_functions()
    {
        EmptyState? classState = null;
        EmptyState? styleState = null;

        IRenderedComponent<MeterRoot> cut = context.Render<MeterRoot>(p => p
            .Add(x => x.Value, 30)
            .Add(x => x.ClassValue, state => { classState = state; return "from-state"; })
            .Add(x => x.StyleValue, state => { styleState = state; return "color: red"; }));

        Assert.Same(EmptyState.Instance, classState);
        Assert.Same(EmptyState.Instance, styleState);
        IElement meter = cut.Find("[role=meter]");
        Assert.Contains("from-state", meter.ClassName);
        Assert.Contains("color: red", meter.GetAttribute("style"));
    }

    [Fact(DisplayName = "calls getAriaValueText on every render of the root")]
    [Trait("Requirement", "MTR-EVT-01")]
    public void Calls_getAriaValueText_on_every_render_of_the_root()
    {
        int calls = 0;
        IRenderedComponent<MeterRoot> cut = context.Render<MeterRoot>(p => p
            .Add(x => x.Value, 30)
            .Add(x => x.Culture, s_enUs)
            .Add(x => x.GetAriaValueText, (formatted, value) => { calls++; return formatted; }));
        int afterFirst = calls;

        // The same parameters again: the root renders again and the function is called again (no memoization).
        cut.Render(p => p.Add(x => x.Value, 30));
        Assert.Equal(afterFirst + 1, calls);
        cut.Render(p => p.Add(x => x.Value, 31));
        Assert.Equal(afterFirst + 2, calls);
    }
}

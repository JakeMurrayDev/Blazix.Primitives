using System.Globalization;
using AngleSharp.Dom;
using Bunit;
using Microsoft.Extensions.DependencyInjection;

// The namespace is the project's root: a namespace named after the family would hide the parts' types.
namespace Blazix.Primitives.Tests.Components;

// ProgressRoot.test.tsx, with Base UI's titles. The cultures are explicit (en-US, de-DE) wherever Base UI compares with
// Intl.NumberFormat in the same runtime, because the port's text comes from .NET globalization (ADR 01 decision 2:
// equivalent, not byte-identical). Where a test follows the current culture it computes the expectation the way the part
// must (PRG-API-05).
/// <summary>The root of a progress bar: its status, ARIA attributes, formatting, range handling and callbacks.</summary>
public sealed class ProgressRootTests : IDisposable
{
    private static readonly CultureInfo s_enUs = CultureInfo.GetCultureInfo("en-US");
    private static readonly CultureInfo s_deDe = CultureInfo.GetCultureInfo("de-DE");

    private readonly BunitContext context = new();

    public ProgressRootTests()
    {
        context.Services.AddBlazixPrimitives();
    }

    /// <inheritdoc />
    public void Dispose() => context.Dispose();

    private IRenderedComponent<ProgressAnatomy> RenderAnatomy(Action<Bunit.ComponentParameterCollectionBuilder<ProgressAnatomy>> parameters)
    {
        return context.Render<ProgressAnatomy>(parameters);
    }

    private static string? InlineWidth(IElement indicator)
    {
        string style = indicator.GetAttribute("style") ?? string.Empty;
        foreach (string declaration in style.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            int colon = declaration.IndexOf(':');
            if (colon > 0 && declaration[..colon].Trim() == "width")
            {
                return declaration[(colon + 1)..].Trim();
            }
        }

        return null;
    }

    private static void AssertStatus(IEnumerable<IElement> parts, string expected)
    {
        foreach (IElement part in parts)
        {
            foreach (string status in new[] { "indeterminate", "progressing", "complete" })
            {
                Assert.Equal(status == expected, part.HasAttribute($"data-{status}"));
            }

            // PRG-DOM-02: the value is empty and the default projection's data-status is never written.
            Assert.Equal(string.Empty, part.GetAttribute($"data-{expected}"));
            Assert.False(part.HasAttribute("data-status"));
        }
    }

    private static IElement[] Parts(IRenderedComponent<ProgressAnatomy> cut) =>
    [
        cut.Find("[data-testid=root]"),
        cut.Find("[data-testid=label]"),
        cut.Find("[data-testid=value]"),
        cut.Find("[data-testid=track]"),
        cut.Find("[data-testid=indicator]"),
    ];

    [Fact(DisplayName = "sets the correct aria attributes")]
    [Trait("Requirement", "PRG-STR-01")]
    [Trait("Requirement", "PRG-ARIA-01")]
    [Trait("Requirement", "PRG-ARIA-02")]
    [Trait("Requirement", "PRG-ARIA-03")]
    [Trait("Requirement", "PRG-ARIA-04")]
    [Trait("Requirement", "PRG-ARIA-06")]
    [Trait("Requirement", "PRG-API-02")]
    [Trait("Requirement", "PRG-API-03")]
    [Trait("Requirement", "PRG-DOM-08")]
    public void Sets_the_correct_aria_attributes()
    {
        IRenderedComponent<ProgressAnatomy> cut = RenderAnatomy(p => p.Add(x => x.Value, 30).Add(x => x.Culture, s_enUs));

        IElement progressbar = cut.Find("[role=progressbar]");
        Assert.Equal("30", progressbar.GetAttribute("aria-valuenow"));
        Assert.Equal("0", progressbar.GetAttribute("aria-valuemin"));
        Assert.Equal("100", progressbar.GetAttribute("aria-valuemax"));
        Assert.Equal("30%", progressbar.GetAttribute("aria-valuetext"));
        IElement label = cut.Find("[data-testid=label]");
        Assert.False(string.IsNullOrEmpty(label.Id));
        Assert.Equal(label.Id, progressbar.GetAttribute("aria-labelledby"));
        Assert.Equal("presentation", label.GetAttribute("role"));
    }

    [Fact(DisplayName = "should update aria-valuenow when value changes")]
    [Trait("Requirement", "PRG-API-01")]
    [Trait("Requirement", "PRG-ARIA-02")]
    public void Should_update_aria_valuenow_when_value_changes()
    {
        IRenderedComponent<ProgressAnatomy> cut = RenderAnatomy(p => p.Add(x => x.Value, 50));

        cut.Render(p => p.Add(x => x.Value, 77));

        Assert.Equal("77", cut.Find("[role=progressbar]").GetAttribute("aria-valuenow"));
    }

    [Fact(DisplayName = "keeps every composed part synchronized through the status cycle")]
    [Trait("Requirement", "PRG-STA-01")]
    [Trait("Requirement", "PRG-STA-02")]
    [Trait("Requirement", "PRG-STA-03")]
    [Trait("Requirement", "PRG-STA-04")]
    [Trait("Requirement", "PRG-DOM-02")]
    [Trait("Requirement", "PRG-DOM-03")]
    [Trait("Requirement", "PRG-DOM-04")]
    [Trait("Requirement", "PRG-API-11")]
    [Trait("Requirement", "PRG-ARIA-02")]
    [Trait("Requirement", "PRG-ARIA-04")]
    [Trait("Requirement", "PRG-STR-03")]
    [Trait("Requirement", "PRG-I18N-05")]
    public void Keeps_every_composed_part_synchronized_through_the_status_cycle()
    {
        IRenderedComponent<ProgressAnatomy> cut = RenderAnatomy(p => p.Add(x => x.Value, (double?)null).Add(x => x.Culture, s_enUs));

        AssertStatus(Parts(cut), "indeterminate");
        IElement progressbar = cut.Find("[role=progressbar]");
        Assert.False(progressbar.HasAttribute("aria-valuenow"));
        Assert.Equal("indeterminate progress", progressbar.GetAttribute("aria-valuetext"));
        Assert.Equal(string.Empty, cut.Find("[data-testid=value]").InnerHtml);
        Assert.Null(InlineWidth(cut.Find("[data-testid=indicator]")));

        cut.Render(p => p.Add(x => x.Value, 50));
        AssertStatus(Parts(cut), "progressing");
        Assert.Equal("50", cut.Find("[role=progressbar]").GetAttribute("aria-valuenow"));
        Assert.Equal("50%", cut.Find("[data-testid=value]").TextContent);
        Assert.Equal("50%", InlineWidth(cut.Find("[data-testid=indicator]")));

        cut.Render(p => p.Add(x => x.Value, 100));
        AssertStatus(Parts(cut), "complete");
        Assert.Equal("100", cut.Find("[role=progressbar]").GetAttribute("aria-valuenow"));
        Assert.Equal("100%", cut.Find("[data-testid=value]").TextContent);
        Assert.Equal("100%", InlineWidth(cut.Find("[data-testid=indicator]")));

        cut.Render(p => p.Add(x => x.Value, (double?)null));
        AssertStatus(Parts(cut), "indeterminate");
        progressbar = cut.Find("[role=progressbar]");
        Assert.False(progressbar.HasAttribute("aria-valuenow"));
        Assert.Equal("indeterminate progress", progressbar.GetAttribute("aria-valuetext"));
        Assert.Equal(string.Empty, cut.Find("[data-testid=value]").InnerHtml);
        Assert.Null(InlineWidth(cut.Find("[data-testid=indicator]")));
    }

    [Fact(DisplayName = "normalizes the formatted value, aria-valuetext, and indicator within a custom range")]
    [Trait("Requirement", "PRG-ALG-01")]
    [Trait("Requirement", "PRG-STA-04")]
    [Trait("Requirement", "PRG-DOM-06")]
    [Trait("Requirement", "PRG-I18N-01")]
    public void Normalizes_the_formatted_value_aria_valuetext_and_indicator_within_a_custom_range()
    {
        IRenderedComponent<ProgressAnatomy> cut = RenderAnatomy(p => p
            .Add(x => x.Min, 20)
            .Add(x => x.Max, 40)
            .Add(x => x.Value, 30)
            .Add(x => x.Culture, s_enUs));

        Assert.Equal("50%", InlineWidth(cut.Find("[data-testid=indicator]")));
        Assert.Equal("50%", cut.Find("[data-testid=value]").TextContent);
        Assert.Equal("50%", cut.Find("[role=progressbar]").GetAttribute("aria-valuetext"));
    }

    [Fact(DisplayName = "clamps aria-valuenow, the value text, and the indicator when the value overshoots max")]
    [Trait("Requirement", "PRG-ALG-01")]
    [Trait("Requirement", "PRG-ALG-02")]
    [Trait("Requirement", "PRG-ARIA-02")]
    [Trait("Requirement", "PRG-ARIA-03")]
    [Trait("Requirement", "PRG-EDGE-03")]
    public void Clamps_aria_valuenow_the_value_text_and_the_indicator_when_the_value_overshoots_max()
    {
        IRenderedComponent<ProgressAnatomy> cut = RenderAnatomy(p => p
            .Add(x => x.Min, 0)
            .Add(x => x.Max, 40)
            .Add(x => x.Value, 50)
            .Add(x => x.Culture, s_enUs));

        IElement progressbar = cut.Find("[role=progressbar]");
        Assert.Equal("40", progressbar.GetAttribute("aria-valuenow"));
        Assert.Equal("40", progressbar.GetAttribute("aria-valuemax"));
        Assert.Equal("100%", progressbar.GetAttribute("aria-valuetext"));
        Assert.Equal("100%", cut.Find("[data-testid=value]").TextContent);
        Assert.Equal("100%", InlineWidth(cut.Find("[data-testid=indicator]")));
    }

    [Fact(DisplayName = "clamps aria-valuenow, the value text, and the indicator when the value undershoots min")]
    [Trait("Requirement", "PRG-ALG-01")]
    [Trait("Requirement", "PRG-ALG-02")]
    [Trait("Requirement", "PRG-ARIA-02")]
    [Trait("Requirement", "PRG-ARIA-03")]
    [Trait("Requirement", "PRG-EDGE-03")]
    public void Clamps_aria_valuenow_the_value_text_and_the_indicator_when_the_value_undershoots_min()
    {
        IRenderedComponent<ProgressAnatomy> cut = RenderAnatomy(p => p
            .Add(x => x.Min, 20)
            .Add(x => x.Max, 40)
            .Add(x => x.Value, 10)
            .Add(x => x.Culture, s_enUs));

        IElement progressbar = cut.Find("[role=progressbar]");
        Assert.Equal("20", progressbar.GetAttribute("aria-valuenow"));
        Assert.Equal("20", progressbar.GetAttribute("aria-valuemin"));
        Assert.Equal("0%", progressbar.GetAttribute("aria-valuetext"));
        Assert.Equal("0%", cut.Find("[data-testid=value]").TextContent);
        Assert.Equal("0%", InlineWidth(cut.Find("[data-testid=indicator]")));
    }

    [Theory(DisplayName = "formats the clamped value $expectedValue when a custom-formatted value $value is outside the range")]
    [Trait("Requirement", "PRG-I18N-02")]
    [Trait("Requirement", "PRG-ALG-02")]
    [Trait("Requirement", "PRG-ALG-03")]
    [Trait("Requirement", "PRG-API-06")]
    [Trait("Requirement", "PRG-EDGE-03")]
    [InlineData(50.0, 40, "$40.00")]
    [InlineData(10.0, 20, "$20.00")]
    public void Formats_the_clamped_value_when_a_custom_formatted_value_is_outside_the_range(double value, int expectedValue, string expectedText)
    {
        List<(string Formatted, double? Value)> calls = [];
        Func<string, double?, string> getAriaValueText = (formatted, raw) =>
        {
            calls.Add((formatted, raw));
            return $"{formatted} (raw: {raw})";
        };

        IRenderedComponent<ProgressAnatomy> cut = RenderAnatomy(p => p
            .Add(x => x.Min, 20)
            .Add(x => x.Max, 40)
            .Add(x => x.Value, value)
            .Add(x => x.Format, "C")
            .Add(x => x.Culture, s_enUs)
            .Add(x => x.GetAriaValueText, getAriaValueText));

        IElement progressbar = cut.Find("[role=progressbar]");
        Assert.Equal(expectedValue.ToString(CultureInfo.InvariantCulture), progressbar.GetAttribute("aria-valuenow"));
        Assert.Equal(expectedText, cut.Find("[data-testid=value]").TextContent);
        Assert.Equal((expectedText, (double?)value), calls[^1]);
        Assert.Equal($"{expectedText} (raw: {value})", progressbar.GetAttribute("aria-valuetext"));
    }

    [Fact(DisplayName = "reports complete when the value reaches or exceeds max")]
    [Trait("Requirement", "PRG-STA-03")]
    [Trait("Requirement", "PRG-DOM-02")]
    [Trait("Requirement", "PRG-EDGE-03")]
    public void Reports_complete_when_the_value_reaches_or_exceeds_max()
    {
        IRenderedComponent<ProgressRoot> cut = context.Render<ProgressRoot>(p => p
            .Add(x => x.Min, 0)
            .Add(x => x.Max, 40)
            .Add(x => x.Value, 45)
            .AddChildContent<ProgressTrack>(t => t.AddChildContent<ProgressIndicator>()));

        Assert.True(cut.Find("[role=progressbar]").HasAttribute("data-complete"));
    }

    [Fact(DisplayName = "normalizes aria attributes when min equals max")]
    [Trait("Requirement", "PRG-EDGE-02")]
    [Trait("Requirement", "PRG-ALG-01")]
    public void Normalizes_aria_attributes_when_min_equals_max()
    {
        IRenderedComponent<ProgressAnatomy> cut = RenderAnatomy(p => p
            .Add(x => x.Min, 5)
            .Add(x => x.Max, 5)
            .Add(x => x.Value, 5)
            .Add(x => x.Culture, s_enUs));

        IElement progressbar = cut.Find("[role=progressbar]");
        Assert.Equal("5", progressbar.GetAttribute("aria-valuenow"));
        Assert.Equal("0%", progressbar.GetAttribute("aria-valuetext"));
        Assert.Equal("0%", cut.Find("[data-testid=value]").TextContent);
        Assert.Equal("0%", InlineWidth(cut.Find("[data-testid=indicator]")));
        // PRG-EDGE-02: complete although the width is 0%.
        Assert.True(progressbar.HasAttribute("data-complete"));
    }

    [Theory(DisplayName = "keeps non-finite value $label indeterminate")]
    [Trait("Requirement", "PRG-STA-02")]
    [Trait("Requirement", "PRG-EDGE-04")]
    [Trait("Requirement", "PRG-ARIA-02")]
    [Trait("Requirement", "PRG-ARIA-04")]
    [Trait("Requirement", "PRG-DOM-04")]
    [InlineData("NaN", double.NaN)]
    [InlineData("+Infinity", double.PositiveInfinity)]
    [InlineData("-Infinity", double.NegativeInfinity)]
    public void Keeps_non_finite_value_indeterminate(string label, double value)
    {
        _ = label;

        IRenderedComponent<ProgressAnatomy> cut = RenderAnatomy(p => p.Add(x => x.Value, value));

        IElement progressbar = cut.Find("[role=progressbar]");
        Assert.True(progressbar.HasAttribute("data-indeterminate"));
        Assert.False(progressbar.HasAttribute("aria-valuenow"));
        Assert.Equal("indeterminate progress", progressbar.GetAttribute("aria-valuetext"));
        Assert.Equal(string.Empty, cut.Find("[data-testid=value]").InnerHtml);
        Assert.Null(InlineWidth(cut.Find("[data-testid=indicator]")));
    }

    [Fact(DisplayName = "receives the formatted and raw values for determinate and indeterminate states")]
    [Trait("Requirement", "PRG-API-04")]
    [Trait("Requirement", "PRG-API-06")]
    [Trait("Requirement", "PRG-ARIA-05")]
    [Trait("Requirement", "PRG-EVT-01")]
    [Trait("Requirement", "PRG-DOM-06")]
    public void Receives_the_formatted_and_raw_values_for_determinate_and_indeterminate_states()
    {
        List<(string Formatted, double? Value)> calls = [];
        Func<string, double?, string> getAriaValueText = (formatted, value) =>
        {
            calls.Add((formatted, value));
            return value is null ? "Waiting to start" : $"{formatted} uploaded";
        };

        IRenderedComponent<ProgressAnatomy> cut = RenderAnatomy(p => p
            .Add(x => x.Value, 30)
            .Add(x => x.Culture, s_enUs)
            .Add(x => x.GetAriaValueText, getAriaValueText));

        IElement progressbar = cut.Find("[role=progressbar]");
        Assert.Equal(("30%", (double?)30), calls[^1]);
        Assert.Equal("30% uploaded", progressbar.GetAttribute("aria-valuetext"));
        Assert.Equal("30%", cut.Find("[data-testid=value]").TextContent);

        cut.Render(p => p.Add(x => x.Value, (double?)null));

        Assert.Equal((string.Empty, (double?)null), calls[^1]);
        Assert.Equal("Waiting to start", cut.Find("[role=progressbar]").GetAttribute("aria-valuetext"));
        Assert.Equal(string.Empty, cut.Find("[data-testid=value]").InnerHtml);
    }

    [Fact(DisplayName = "formats the value")]
    [Trait("Requirement", "PRG-API-04")]
    [Trait("Requirement", "PRG-I18N-02")]
    public void Formats_the_value()
    {
        IRenderedComponent<ProgressAnatomy> cut = RenderAnatomy(p => p
            .Add(x => x.Value, 30)
            .Add(x => x.Format, "C")
            .Add(x => x.Culture, s_enUs));

        Assert.Equal("$30.00", cut.Find("[data-testid=value]").TextContent);
        Assert.Equal("$30.00", cut.Find("[role=progressbar]").GetAttribute("aria-valuetext"));
    }

    [Fact(DisplayName = "reflects format changes without lagging a commit")]
    [Trait("Requirement", "PRG-API-01")]
    [Trait("Requirement", "PRG-I18N-02")]
    public void Reflects_format_changes_without_lagging_a_commit()
    {
        IRenderedComponent<ProgressAnatomy> cut = RenderAnatomy(p => p
            .Add(x => x.Value, 30)
            .Add(x => x.Format, "C2")
            .Add(x => x.Culture, s_enUs));
        Assert.Equal("$30.00", cut.Find("[data-testid=value]").TextContent);

        cut.Render(p => p.Add(x => x.Format, "C0"));

        Assert.Equal("$30", cut.Find("[data-testid=value]").TextContent);
        Assert.Equal("$30", cut.Find("[role=progressbar]").GetAttribute("aria-valuetext"));
    }

    [Fact(DisplayName = "sets the locale when formatting the value")]
    [Trait("Requirement", "PRG-API-05")]
    [Trait("Requirement", "PRG-I18N-03")]
    public void Sets_the_locale_when_formatting_the_value()
    {
        // In the German culture numbers use a dot for thousands and a comma for decimals.
        IRenderedComponent<ProgressAnatomy> cut = RenderAnatomy(p => p
            .Add(x => x.Value, 70.51)
            .Add(x => x.Format, "F2")
            .Add(x => x.Culture, s_deDe));

        Assert.Equal("70,51", cut.Find("[data-testid=value]").TextContent);
    }

    // ---- The port's own tests, for the rows that cite no Base UI test or that the typed port reads differently ----

    // German percent formatting separates the sign from the number (Intl writes U+00A0, .NET a plain space): the .NET
    // output is the recorded equivalent (PRG-I18N-01).
    [Fact(DisplayName = "defaults aria-valuetext to the localized formatted value, matching Progress.Value")]
    [Trait("Requirement", "PRG-I18N-01")]
    [Trait("Requirement", "PRG-ARIA-04")]
    [Trait("Requirement", "PRG-DOM-06")]
    public void Defaults_aria_valuetext_to_the_localized_formatted_value_matching_Progress_Value()
    {
        IRenderedComponent<ProgressAnatomy> cut = RenderAnatomy(p => p.Add(x => x.Value, 30).Add(x => x.Culture, s_deDe));

        string? valueText = cut.Find("[role=progressbar]").GetAttribute("aria-valuetext");
        Assert.Equal("30 %", valueText);
        Assert.Equal(valueText, cut.Find("[data-testid=value]").TextContent);
    }

    [Fact(DisplayName = "keeps the English indeterminate text under a non-English culture")]
    [Trait("Requirement", "PRG-I18N-05")]
    public void Keeps_the_English_indeterminate_text_under_a_non_English_culture()
    {
        IRenderedComponent<ProgressAnatomy> cut = RenderAnatomy(p => p
            .Add(x => x.Value, (double?)null)
            .Add(x => x.Format, "C")
            .Add(x => x.Culture, s_deDe));

        Assert.Equal("indeterminate progress", cut.Find("[role=progressbar]").GetAttribute("aria-valuetext"));
    }

    [Fact(DisplayName = "uses the current culture when no culture is given")]
    [Trait("Requirement", "PRG-API-05")]
    [Trait("Requirement", "PRG-I18N-03")]
    public void Uses_the_current_culture_when_no_culture_is_given()
    {
        CultureInfo previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = s_deDe;
            IRenderedComponent<ProgressAnatomy> cut = RenderAnatomy(p => p.Add(x => x.Value, 30));

            Assert.Equal("30 %", cut.Find("[role=progressbar]").GetAttribute("aria-valuetext"));
            Assert.Equal("30 %", cut.Find("[data-testid=value]").TextContent);
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact(DisplayName = "renders the children followed by one visually hidden span with the character x in every status")]
    [Trait("Requirement", "PRG-STR-01")]
    [Trait("Requirement", "PRG-DOM-01")]
    public void Renders_the_children_followed_by_one_visually_hidden_span_with_the_character_x_in_every_status()
    {
        foreach (double? value in new double?[] { 30, null })
        {
            IRenderedComponent<ProgressRoot> cut = context.Render<ProgressRoot>(p => p
                .Add(x => x.Value, value)
                .AddChildContent("child"));

            IElement progressbar = cut.Find("[role=progressbar]");
            Assert.Equal("div", progressbar.TagName, ignoreCase: true);
            IElement hidden = progressbar.QuerySelector("span[role=presentation]")!;
            Assert.Equal("x", hidden.TextContent);
            Assert.Equal(
                "clip-path: inset(50%); overflow: hidden; white-space: nowrap; border: 0; padding: 0; width: 1px; height: 1px; margin: 0; position: fixed; top: 0; left: 0",
                hidden.GetAttribute("style")?.TrimEnd(';'));
            Assert.Same(hidden, progressbar.LastElementChild);
            Assert.Equal("childx", progressbar.TextContent);
        }
    }

    [Fact(DisplayName = "lets a consumer aria-valuetext replace the computed one while getAriaValueText still runs")]
    [Trait("Requirement", "PRG-API-07")]
    [Trait("Requirement", "PRG-EVT-01")]
    public void Lets_a_consumer_aria_valuetext_replace_the_computed_one_while_getAriaValueText_still_runs()
    {
        int calls = 0;
        IRenderedComponent<ProgressRoot> cut = context.Render<ProgressRoot>(p => p
            .Add(x => x.Value, 30)
            .Add(x => x.GetAriaValueText, (formatted, value) => { calls++; return "from the function"; })
            .AddUnmatched("aria-valuetext", "from the consumer"));

        Assert.Equal("from the consumer", cut.Find("[role=progressbar]").GetAttribute("aria-valuetext"));
        Assert.True(calls > 0);
    }

    [Fact(DisplayName = "lets consumer attributes replace the built-in ones, a status attribute included")]
    [Trait("Requirement", "PRG-DOM-09")]
    [Trait("Requirement", "PRG-API-09")]
    [Trait("Requirement", "PRG-ARIA-06")]
    public void Lets_consumer_attributes_replace_the_built_in_ones_a_status_attribute_included()
    {
        IRenderedComponent<ProgressRoot> cut = context.Render<ProgressRoot>(p => p
            .Add(x => x.Value, 30)
            .AddUnmatched("role", "meter")
            .AddUnmatched("aria-valuenow", "7")
            .AddUnmatched("aria-valuemin", "1")
            .AddUnmatched("aria-valuemax", "9")
            .AddUnmatched("aria-labelledby", "mine")
            .AddUnmatched("data-progressing", "custom")
            .AddUnmatched("data-testid", "root"));

        IElement root = cut.Find("[data-testid=root]");
        Assert.Equal("meter", root.GetAttribute("role"));
        Assert.Equal("7", root.GetAttribute("aria-valuenow"));
        Assert.Equal("1", root.GetAttribute("aria-valuemin"));
        Assert.Equal("9", root.GetAttribute("aria-valuemax"));
        Assert.Equal("mine", root.GetAttribute("aria-labelledby"));
        Assert.Equal("custom", root.GetAttribute("data-progressing"));
    }

    [Fact(DisplayName = "is not focusable")]
    [Trait("Requirement", "PRG-FOC-01")]
    public void Is_not_focusable()
    {
        IRenderedComponent<ProgressAnatomy> cut = RenderAnatomy(p => p.Add(x => x.Value, 30));

        foreach (IElement element in cut.FindAll("*"))
        {
            Assert.False(element.HasAttribute("tabindex"));
        }
    }

    [Fact(DisplayName = "follows an inverted range literally")]
    [Trait("Requirement", "PRG-EDGE-05")]
    public void Follows_an_inverted_range_literally()
    {
        IRenderedComponent<ProgressAnatomy> cut = RenderAnatomy(p => p
            .Add(x => x.Min, 100)
            .Add(x => x.Max, 0)
            .Add(x => x.Value, 30)
            .Add(x => x.Culture, s_enUs));

        IElement progressbar = cut.Find("[role=progressbar]");
        Assert.Equal("100", progressbar.GetAttribute("aria-valuenow"));
        Assert.True(progressbar.HasAttribute("data-progressing"));
        Assert.Equal("70%", InlineWidth(cut.Find("[data-testid=indicator]")));
    }

    [Fact(DisplayName = "keeps the range attributes while indeterminate")]
    [Trait("Requirement", "PRG-ARIA-03")]
    public void Keeps_the_range_attributes_while_indeterminate()
    {
        IRenderedComponent<ProgressAnatomy> cut = RenderAnatomy(p => p
            .Add(x => x.Value, (double?)null)
            .Add(x => x.Min, 10)
            .Add(x => x.Max, 30));

        IElement progressbar = cut.Find("[role=progressbar]");
        Assert.Equal("10", progressbar.GetAttribute("aria-valuemin"));
        Assert.Equal("30", progressbar.GetAttribute("aria-valuemax"));
    }

    [Fact(DisplayName = "passes the same state to the class and style functions of every part")]
    [Trait("Requirement", "PRG-STA-01")]
    [Trait("Requirement", "PRG-API-08")]
    public void Passes_the_same_status_to_the_class_and_style_functions_of_every_part()
    {
        List<ProgressStatus> statuses = [];
        IRenderedComponent<ProgressRoot> cut = context.Render<ProgressRoot>(p => p
            .Add(x => x.Value, 100)
            .Add(x => x.ClassValue, state => { statuses.Add(state.Status); return $"root-{state.Status}"; })
            .Add(x => x.StyleValue, state => { statuses.Add(state.Status); return null; })
            .AddChildContent<ProgressLabel>(l => l.Add(x => x.ClassValue, state => { statuses.Add(state.Status); return null; }))
            .AddChildContent<ProgressTrack>(t => t.Add(x => x.ClassValue, state => { statuses.Add(state.Status); return null; })));

        Assert.NotEmpty(statuses);
        Assert.All(statuses, status => Assert.Equal(ProgressStatus.Complete, status));
        Assert.Contains("root-Complete", cut.Find("[role=progressbar]").ClassName);
    }

    [Fact(DisplayName = "calls getAriaValueText on every render of the root in every status")]
    [Trait("Requirement", "PRG-EVT-01")]
    public void Calls_getAriaValueText_on_every_render_of_the_root_in_every_status()
    {
        int calls = 0;
        IRenderedComponent<ProgressAnatomy> cut = RenderAnatomy(p => p
            .Add(x => x.Value, 30)
            .Add(x => x.Culture, s_enUs)
            .Add(x => x.GetAriaValueText, (formatted, value) => { calls++; return formatted; }));
        int afterFirst = calls;

        cut.Render(p => p.Add(x => x.Value, 30));
        Assert.Equal(afterFirst + 1, calls);
        cut.Render(p => p.Add(x => x.Value, (double?)null));
        Assert.Equal(afterFirst + 2, calls);
    }
}

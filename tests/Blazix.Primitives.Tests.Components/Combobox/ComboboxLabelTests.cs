using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.Web.HtmlRendering;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

// Family tests use the project's root namespace.
namespace Blazix.Primitives.Tests.Components;

// CMB §3.2 at L2 (ComboboxLabel.test.tsx and ComboboxRoot.test.tsx's labelling titles whose assertions are what C# renders and
// sends): the label's id, the trigger's aria-labelledby, the `form` module's label role, and the development warning of
// CMB-STR-10, restored without the owner stack (ADR 12 Amendments 2026-11-03 item (5)). The label's press is the module's.
/// <summary>Combobox's Label at L2.</summary>
public sealed class ComboboxLabelTests : IDisposable
{
    private const string Warning = "Blazix: `<ComboboxLabel>` labels `<ComboboxTrigger>` only.";

    private readonly ComboboxTestContext test = new();

    private readonly CapturingLogger<ComboboxLabel> logger = new();

    /// <summary>Initializes a new instance of the <see cref="ComboboxLabelTests"/> class.</summary>
    public ComboboxLabelTests() => test.Context.Services.AddSingleton<ILogger<ComboboxLabel>>(logger);

    /// <inheritdoc />
    public void Dispose() => test.Dispose();

    [Fact(DisplayName = "Combobox.Label links to Combobox.Trigger when input is inside popup and trigger has an explicit id")]
    [Trait("Requirement", "CMB-ARIA-16")]
    [Trait("Requirement", "CMB-API-45")]
    public void Links_to_the_trigger_with_an_explicit_id()
    {
        IRenderedComponent<ComboboxHarness> cut = Render(new() { ["Id"] = "fruit" }, inputInside: true, triggerId: "my-trigger");

        Assert.Equal("fruit-label", cut.Find("[data-testid=label]").Id);
        Assert.Equal("fruit-label", cut.Find("[data-testid=trigger]").GetAttribute("aria-labelledby"));
        Assert.Contains("\"controlId\":\"my-trigger\"", LabelOptions(), StringComparison.Ordinal);
    }

    [Fact(DisplayName = "does not set fallback aria-labelledby when no label is rendered")]
    [Trait("Requirement", "CMB-ARIA-16")]
    public void Does_not_set_fallback_aria_labelledby_when_no_label_is_rendered()
    {
        IRenderedComponent<ComboboxHarness> labelled = Render(new() { ["Id"] = "a" }, inputInside: true);
        Assert.Equal("a-label", labelled.Find("[data-testid=trigger]").GetAttribute("aria-labelledby"));

        IRenderedComponent<ComboboxHarness> cut = Render(new() { ["Id"] = "b" }, inputInside: true, withLabel: false);
        Assert.False(cut.Find("[data-testid=trigger]").HasAttribute("aria-labelledby"));
    }

    [Fact(DisplayName = "updates Combobox.Label linkage when root id changes")]
    [Trait("Requirement", "CMB-ARIA-16")]
    public void Updates_the_linkage_when_the_root_id_changes()
    {
        IRenderedComponent<ComboboxHarness> cut = Render(new() { ["Id"] = "first" }, inputInside: true);
        Assert.Equal("first-label", cut.Find("[data-testid=trigger]").GetAttribute("aria-labelledby"));

        cut.Render(p => p.Add(x => x.RootParameters, new Dictionary<string, object?> { ["Id"] = "second" }));

        Assert.Equal("second-label", cut.Find("[data-testid=label]").Id);
        Assert.Equal("second-label", cut.Find("[data-testid=trigger]").GetAttribute("aria-labelledby"));
    }

    [Fact(DisplayName = "warns without relying on React.captureOwnerStack when labeling an external input")]
    [Trait("Requirement", "CMB-STR-10")]
    [Trait("Requirement", "CMB-EDGE-03")]
    public void Warns_when_labeling_an_external_input()
    {
        Render(new(), inputInside: false);

        Assert.Single(logger.Entries, entry => entry.Message.StartsWith(Warning, StringComparison.Ordinal));
    }

    [Fact(DisplayName = "port: no warning while the input is inside the popup")]
    [Trait("Requirement", "CMB-STR-10")]
    public void No_warning_while_the_input_is_inside_the_popup()
    {
        IRenderedComponent<ComboboxHarness> cut = Render(new() { ["Id"] = "fruit" }, inputInside: true);

        Assert.Equal("fruit-label", cut.Find("[data-testid=label]").Id);
        Assert.DoesNotContain(logger.Entries, entry => entry.Message.StartsWith(Warning, StringComparison.Ordinal));
    }

    [Fact(DisplayName = "port: the label's id is the root's, whatever id the consumer gives")]
    [Trait("Requirement", "CMB-ARIA-16")]
    public void The_labels_id_is_the_roots()
    {
        IRenderedComponent<ComboboxHarness> cut = Render(new() { ["Id"] = "fruit" }, inputInside: true, labelId: "mine");

        Assert.Equal("fruit-label", cut.Find("[data-testid=label]").Id);
        Assert.Equal("div", cut.Find("[data-testid=label]").TagName.ToLowerInvariant());
    }

    [Fact(DisplayName = "port: the label attaches the form module's non-native label role, focusing the trigger")]
    [Trait("Requirement", "CMB-ARIA-16")]
    public void Attaches_the_non_native_label_role()
    {
        Render(new() { ["Id"] = "fruit" }, inputInside: true);

        string options = LabelOptions();
        Assert.Contains("\"native\":false", options, StringComparison.Ordinal);
        Assert.Contains("\"controlId\":\"fruit\"", options, StringComparison.Ordinal);
    }

    // ComboboxLabel.tsx:37 (localControlId = triggerElement?.id ?? (inputInsidePopup ? rootId : undefined)): outside the popup
    // Base UI renders no trigger id of its own, so the label focuses only an id the consumer gave the trigger.
    [Fact(DisplayName = "port: with the input outside the popup, the label focuses no trigger without an id the consumer gave")]
    [Trait("Requirement", "CMB-ARIA-16")]
    public void With_the_input_outside_the_label_focuses_no_trigger_without_a_consumer_id()
    {
        IRenderedComponent<ComboboxHarness> cut = Render(new(), inputInside: false);

        Assert.NotNull(cut.Find("[data-testid=trigger]"));
        Assert.Contains("\"native\":false", LabelOptions(), StringComparison.Ordinal);
        Assert.DoesNotContain("\"controlId\"", LabelOptions(), StringComparison.Ordinal);
    }

    // useLabel.ts:28 (resolvedControlId = contextControlId ?? fallbackControlId): inside a Field the label focuses the field's
    // control, the root's id the combobox claimed (AriaCombobox.tsx:160), which the input outside the popup renders.
    [Fact(DisplayName = "port: inside a Field the label focuses the field's control id")]
    [Trait("Requirement", "CMB-ARIA-16")]
    public void Inside_a_field_the_label_focuses_the_fields_control_id()
    {
        void Extra(RenderTreeBuilder builder)
        {
            builder.OpenComponent<ComboboxLabel>(0);
            builder.CloseComponent();
            builder.OpenComponent<ComboboxTrigger>(1);
            builder.AddComponentParameter(2, "data-testid", "trigger");
            builder.CloseComponent();
        }

        IRenderedComponent<FieldRoot> cut = test.Context.Render<FieldRoot>(p => p.AddChildContent<ComboboxHarness>(h => h
            .Add(x => x.RootParameters, new Dictionary<string, object?>())
            .Add(x => x.Extra, (RenderFragment)Extra)));

        string inputId = cut.Find("[data-testid=input]").Id!;
        Assert.NotEqual(inputId, cut.Find("[data-testid=trigger]").Id);
        Assert.Contains("\"controlId\":\"" + inputId + "\"", LabelOptions(), StringComparison.Ordinal);
    }

    // Base UI renders the tree with renderToString, which runs no layout effect, so the label's registration never reaches
    // the trigger. HtmlRenderer renders the same HTML a prerender does: no OnAfterRender runs.
    [Fact(DisplayName = "does not link Combobox.Label to trigger before hydration")]
    [Trait("Requirement", "CMB-ARIA-07")]
    [Trait("Requirement", "CMB-EDGE-06")]
    public async Task Does_not_link_the_label_to_the_trigger_before_hydration()
    {
        RenderFragment content = builder =>
        {
            builder.OpenComponent<ComboboxLabel>(0);
            builder.AddAttribute(1, "data-testid", "label");
            builder.AddComponentParameter(2, nameof(ComboboxLabel.ChildContent), (RenderFragment)(b => b.AddContent(0, "Food")));
            builder.CloseComponent();
            builder.OpenComponent<ComboboxTrigger>(3);
            builder.AddAttribute(4, "data-testid", "trigger");
            builder.CloseComponent();
            builder.OpenComponent<ComboboxInput>(5);
            builder.AddAttribute(6, "data-testid", "input");
            builder.CloseComponent();
        };
        await using HtmlRenderer renderer = new(test.Context.Services, NullLoggerFactory.Instance);
        string html = await renderer.Dispatcher.InvokeAsync(async () =>
        {
            HtmlRootComponent root = await renderer.RenderComponentAsync<ComboboxRoot<string>>(ParameterView.FromDictionary(new Dictionary<string, object?>
            {
                [nameof(ComboboxRoot<string>.Inline)] = true,
                [nameof(ComboboxRoot<string>.ChildContent)] = content,
            }));
            return root.ToHtmlString();
        });
        using AngleSharp.Html.Dom.IHtmlDocument document = new AngleSharp.Html.Parser.HtmlParser().ParseDocument(html);

        Assert.NotEqual(string.Empty, document.QuerySelector("[data-testid=label]")!.Id ?? string.Empty);
        Assert.NotEqual(string.Empty, document.QuerySelector("[data-testid=trigger]")!.Id ?? string.Empty);
        Assert.False(document.QuerySelector("[data-testid=trigger]")!.HasAttribute("aria-labelledby"));
    }

    private string LabelOptions()
    {
        string? options = null;
        foreach (JSRuntimeInvocation attach in test.Form.Invocations["attach"])
        {
            if (attach.Arguments[1] is string json && json.Contains("\"label\":", StringComparison.Ordinal))
            {
                options = json;
            }
        }

        foreach (JSRuntimeInvocation update in test.FormHandle.Invocations["update"])
        {
            if (update.Arguments[0] is string json && json.Contains("\"label\":", StringComparison.Ordinal))
            {
                options = json;
            }
        }

        return options ?? string.Empty;
    }

    private IRenderedComponent<ComboboxHarness> Render(Dictionary<string, object?> root, bool inputInside, bool withLabel = true, string? triggerId = null, string? labelId = null)
    {
        void Extra(RenderTreeBuilder builder)
        {
            if (withLabel)
            {
                builder.OpenComponent<ComboboxLabel>(0);
                builder.AddComponentParameter(1, "data-testid", "label");
                if (labelId is not null)
                {
                    builder.AddComponentParameter(2, "id", labelId);
                }

                builder.CloseComponent();
            }

            builder.OpenComponent<ComboboxTrigger>(3);
            builder.AddComponentParameter(4, "data-testid", "trigger");
            if (triggerId is not null)
            {
                builder.AddComponentParameter(5, "id", triggerId);
            }

            builder.CloseComponent();
        }

        return test.Context.Render<ComboboxHarness>(p => p
            .Add(x => x.RootParameters, root)
            .Add(x => x.InputInsidePopup, inputInside)
            .Add(x => x.Extra, (RenderFragment)Extra));
    }
}

using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

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

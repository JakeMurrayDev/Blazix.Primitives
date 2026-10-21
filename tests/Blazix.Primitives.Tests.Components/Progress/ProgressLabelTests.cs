using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

// The namespace is the project's root: a namespace named after the family would hide the parts' types.
namespace Blazix.Primitives.Tests.Components;

// ProgressLabel.test.tsx. The Root is the label's scope (PRG-STA-05, FFI-ARIA-08): the label publishes its id to the root and
// withdraws it only while the root still holds that id. The port publishes while the label's parameters are set, so the
// root's re-render lands in the same render batch as the label's own, on Server as well as WebAssembly
// (docs/components/progress.md §14; the PRG-ARIA-07 row, PRG-EDGE-06 port note).
/// <summary>The label of a progress bar and its association with the root's <c>aria-labelledby</c>.</summary>
public sealed class ProgressLabelTests : IDisposable
{
    private readonly BunitContext context = new();

    public ProgressLabelTests()
    {
        context.Services.AddBlazixPrimitives();
    }

    /// <inheritdoc />
    public void Dispose() => context.Dispose();

    [Fact(DisplayName = "updates and clears the progress bar label association")]
    [Trait("Requirement", "PRG-API-12")]
    [Trait("Requirement", "PRG-STA-05")]
    [Trait("Requirement", "PRG-ARIA-06")]
    [Trait("Requirement", "PRG-ARIA-07")]
    public void Updates_and_clears_the_progress_bar_label_association()
    {
        IRenderedComponent<ProgressAnatomy> cut = context.Render<ProgressAnatomy>(p => p
            .Add(x => x.Value, 40)
            .Add(x => x.ShowLabel, true)
            .Add(x => x.LabelId, "label-a"));

        Assert.Equal("label-a", cut.Find("[role=progressbar]").GetAttribute("aria-labelledby"));
        Assert.Equal("label-a", cut.Find("[data-testid=label]").Id);

        cut.Render(p => p.Add(x => x.LabelId, "label-b"));
        Assert.Equal("label-b", cut.Find("[role=progressbar]").GetAttribute("aria-labelledby"));
        Assert.Equal("label-b", cut.Find("[data-testid=label]").Id);

        cut.Render(p => p.Add(x => x.ShowLabel, false));
        Assert.False(cut.Find("[role=progressbar]").HasAttribute("aria-labelledby"));
    }

    [Fact(DisplayName = "throws a descriptive error when rendered outside <ProgressRoot>")]
    [Trait("Requirement", "PRG-EDGE-01")]
    [Trait("Requirement", "PRG-STR-02")]
    public void Throws_a_descriptive_error_when_rendered_outside_ProgressRoot()
    {
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => context.Render<ProgressLabel>());

        Assert.Equal("Blazix: The `ProgressRoot` context is missing. Progress parts must be placed within `<ProgressRoot>`.", error.Message);
    }

    [Fact(DisplayName = "draws a generated id when the consumer supplies none")]
    [Trait("Requirement", "PRG-API-12")]
    [Trait("Requirement", "PRG-DOM-08")]
    public void Draws_a_generated_id_when_the_consumer_supplies_none()
    {
        IRenderedComponent<ProgressRoot> cut = context.Render<ProgressRoot>(p => p
            .Add(x => x.Value, 40)
            .AddChildContent<ProgressLabel>(l => l.AddChildContent("Upload progress")));

        IElement label = cut.Find("span[role=presentation]");
        Assert.Matches("^base-ui-[0-9]+$", label.Id);
        Assert.Equal(label.Id, cut.Find("[role=progressbar]").GetAttribute("aria-labelledby"));
    }

    [Fact(DisplayName = "keeps the newer label's id when an older label unmounts")]
    [Trait("Requirement", "PRG-STA-05")]
    [Trait("Requirement", "FFI-ARIA-08")]
    public void Keeps_the_newer_labels_id_when_an_older_label_unmounts()
    {
        bool showOlder = true;
        IRenderedComponent<ProgressRoot> cut = context.Render<ProgressRoot>(p => p
            .Add(x => x.Value, 40)
            .Add(x => x.ChildContent, (RenderFragment)(builder =>
            {
                if (showOlder)
                {
                    builder.OpenComponent<ProgressLabel>(0);
                    builder.AddAttribute(1, "id", "older");
                    builder.CloseComponent();
                }

                builder.OpenComponent<ProgressLabel>(2);
                builder.AddAttribute(3, "id", "newer");
                builder.CloseComponent();
            })));
        Assert.Equal("newer", cut.Find("[role=progressbar]").GetAttribute("aria-labelledby"));

        showOlder = false;
        cut.Render(p => p.Add(x => x.Value, 41));

        Assert.Equal("newer", cut.Find("[role=progressbar]").GetAttribute("aria-labelledby"));
    }

    [Fact(DisplayName = "renders aria-labelledby in the first markup of the root")]
    [Trait("Requirement", "PRG-ARIA-06")]
    [Trait("Requirement", "PRG-ARIA-07")]
    [Trait("Requirement", "PRG-EDGE-06")]
    public void Renders_aria_labelledby_in_the_first_markup_of_the_root()
    {
        IRenderedComponent<ProgressAnatomy> cut = context.Render<ProgressAnatomy>(p => p.Add(x => x.Value, 40));

        Assert.Equal(cut.Find("[data-testid=label]").Id, cut.Find("[role=progressbar]").GetAttribute("aria-labelledby"));
    }

    [Fact(DisplayName = "carries the status attribute of the root")]
    [Trait("Requirement", "PRG-DOM-02")]
    public void Carries_the_status_attribute_of_the_root()
    {
        IRenderedComponent<ProgressAnatomy> cut = context.Render<ProgressAnatomy>(p => p.Add(x => x.Value, (double?)null));

        IElement label = cut.Find("[data-testid=label]");
        Assert.True(label.HasAttribute("data-indeterminate"));
        Assert.False(label.HasAttribute("data-progressing"));
    }
}

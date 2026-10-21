using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

// The namespace is the project's root: a namespace named after the family would hide the parts' types.
namespace Blazix.Primitives.Tests.Components;

// MeterLabel.test.tsx. The Root is the label's scope (MTR-STA-03, FFI-ARIA-08): the label publishes its id to the root and
// withdraws it only while the root still holds that id. The port publishes while the label initializes and when its id
// changes, so the root's re-render lands in the same render batch as the label's own, on Server as well as WebAssembly
// (docs/components/meter.md §14; the MTR-ARIA-07 row, MTR-EDGE-06 port note).
/// <summary>The label of a meter and its association with the root's <c>aria-labelledby</c>.</summary>
public sealed class MeterLabelTests : IDisposable
{
    private readonly BunitContext context = new();

    public MeterLabelTests()
    {
        context.Services.AddBlazixPrimitives();
    }

    /// <inheritdoc />
    public void Dispose() => context.Dispose();

    [Fact(DisplayName = "updates and clears the meter label association")]
    [Trait("Requirement", "MTR-API-12")]
    [Trait("Requirement", "MTR-STA-03")]
    [Trait("Requirement", "MTR-ARIA-06")]
    [Trait("Requirement", "MTR-ARIA-07")]
    public void Updates_and_clears_the_meter_label_association()
    {
        IRenderedComponent<MeterAnatomy> cut = context.Render<MeterAnatomy>(p => p
            .Add(x => x.Value, 50)
            .Add(x => x.ShowLabel, true)
            .Add(x => x.LabelId, "label-a"));

        Assert.Equal("label-a", cut.Find("[role=meter]").GetAttribute("aria-labelledby"));
        Assert.Equal("label-a", cut.Find("[data-testid=label]").Id);

        cut.Render(p => p.Add(x => x.LabelId, "label-b"));
        Assert.Equal("label-b", cut.Find("[role=meter]").GetAttribute("aria-labelledby"));
        Assert.Equal("label-b", cut.Find("[data-testid=label]").Id);

        cut.Render(p => p.Add(x => x.ShowLabel, false));
        Assert.False(cut.Find("[role=meter]").HasAttribute("aria-labelledby"));
    }

    [Fact(DisplayName = "throws a descriptive error when rendered outside <MeterRoot>")]
    [Trait("Requirement", "MTR-EDGE-01")]
    [Trait("Requirement", "MTR-STR-02")]
    public void Throws_a_descriptive_error_when_rendered_outside_MeterRoot()
    {
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => context.Render<MeterLabel>());

        Assert.Equal("Blazix: The `MeterRoot` context is missing. Meter parts must be placed within `<MeterRoot>`.", error.Message);
    }

    [Fact(DisplayName = "draws a generated id when the consumer supplies none")]
    [Trait("Requirement", "MTR-API-12")]
    [Trait("Requirement", "MTR-DOM-07")]
    public void Draws_a_generated_id_when_the_consumer_supplies_none()
    {
        IRenderedComponent<MeterRoot> cut = context.Render<MeterRoot>(p => p
            .Add(x => x.Value, 50)
            .AddChildContent<MeterLabel>(l => l.AddChildContent("Battery level")));

        IElement label = cut.Find("span[role=presentation]");
        Assert.Matches("^base-ui-[0-9]+$", label.Id);
        Assert.Equal(label.Id, cut.Find("[role=meter]").GetAttribute("aria-labelledby"));
    }

    [Fact(DisplayName = "publishes an empty consumer id as written")]
    [Trait("Requirement", "MTR-API-12")]
    public void Publishes_an_empty_consumer_id_as_written()
    {
        // MTR §16 item 8: an empty id is used verbatim (RND-DOM-28) and published, so the root reads aria-labelledby="".
        IRenderedComponent<MeterRoot> cut = context.Render<MeterRoot>(p => p
            .Add(x => x.Value, 50)
            .AddChildContent<MeterLabel>(l => l.AddUnmatched("id", string.Empty)));

        Assert.Equal(string.Empty, cut.Find("[role=meter]").GetAttribute("aria-labelledby"));
    }

    [Fact(DisplayName = "keeps the newer label's id when an older label unmounts")]
    [Trait("Requirement", "MTR-STA-03")]
    [Trait("Requirement", "FFI-ARIA-08")]
    public void Keeps_the_newer_labels_id_when_an_older_label_unmounts()
    {
        // MTR §16 item 9: the most recently registered label wins; the older label's cleanup does not clear the newer id.
        bool showOlder = true;
        IRenderedComponent<MeterRoot> cut = context.Render<MeterRoot>(p => p
            .Add(x => x.Value, 50)
            .Add(x => x.ChildContent, (RenderFragment)(builder =>
            {
                if (showOlder)
                {
                    builder.OpenComponent<MeterLabel>(0);
                    builder.AddAttribute(1, "id", "older");
                    builder.CloseComponent();
                }

                builder.OpenComponent<MeterLabel>(2);
                builder.AddAttribute(3, "id", "newer");
                builder.CloseComponent();
            })));
        Assert.Equal("newer", cut.Find("[role=meter]").GetAttribute("aria-labelledby"));

        showOlder = false;
        cut.Render(p => p.Add(x => x.Value, 51));

        Assert.Equal("newer", cut.Find("[role=meter]").GetAttribute("aria-labelledby"));
    }

    [Fact(DisplayName = "renders aria-labelledby in the first markup of the root")]
    [Trait("Requirement", "MTR-ARIA-06")]
    [Trait("Requirement", "MTR-ARIA-07")]
    [Trait("Requirement", "MTR-EDGE-06")]
    public void Renders_aria_labelledby_in_the_first_markup_of_the_root()
    {
        // docs §14: a port that knows the label's id by the end of the first render may emit it there. The label registers
        // while it initializes, which is inside the render batch that created the root, so the first markup has it.
        IRenderedComponent<MeterAnatomy> cut = context.Render<MeterAnatomy>(p => p
            .Add(x => x.Value, 50)
            .Add(x => x.ShowLabel, true));

        Assert.Equal(cut.Find("[data-testid=label]").Id, cut.Find("[role=meter]").GetAttribute("aria-labelledby"));
    }

    [Fact(DisplayName = "lets a consumer aria-labelledby replace the tracked id")]
    [Trait("Requirement", "MTR-ARIA-06")]
    [Trait("Requirement", "MTR-API-09")]
    public void Lets_a_consumer_aria_labelledby_replace_the_tracked_id()
    {
        IRenderedComponent<MeterRoot> cut = context.Render<MeterRoot>(p => p
            .Add(x => x.Value, 50)
            .AddUnmatched("aria-labelledby", "mine")
            .AddChildContent<MeterLabel>(l => l.AddUnmatched("id", "label")));

        Assert.Equal("mine", cut.Find("[role=meter]").GetAttribute("aria-labelledby"));
    }

    [Fact(DisplayName = "renders no element and publishes no id while disabled")]
    [Trait("Requirement", "MTR-ARIA-06")]
    public void Renders_no_element_and_publishes_no_id_while_disabled()
    {
        IRenderedComponent<MeterRoot> cut = context.Render<MeterRoot>(p => p
            .Add(x => x.Value, 50)
            .AddChildContent<MeterLabel>(l => l.Add(x => x.Enabled, false).AddUnmatched("id", "label")));

        Assert.False(cut.Find("[role=meter]").HasAttribute("aria-labelledby"));
        Assert.Empty(cut.FindAll("#label"));
    }
}

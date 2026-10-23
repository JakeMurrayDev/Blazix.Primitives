using AngleSharp.Dom;
using Blazix.Primitives.Rendering;
using Bunit;
using Microsoft.AspNetCore.Components;

// The namespace is the project's root: a namespace named after the family would hide the parts' types.
namespace Blazix.Primitives.Tests.Components;

// RadioGroup.test.tsx's Fieldset titles at L2 (issue 84; RDG-ARIA-05, RDG-STR-04, FST-FRM-03, FST-STR-01): the group's
// aria-labelledby is the consumer's, else the field's label, else the nearest fieldset's legend, re-resolved as each mounts,
// unmounts or is replaced. "keeps inputRef available after an ancestor fieldset is enabled" is withdrawn with inputRef
// (ADR 02 Amendments 2026-10-22); its port title is L4 (radio-group.spec.ts).
/// <summary>A radio group inside a fieldset: its name from the legend and the label precedence.</summary>
public sealed class RadioGroupFieldsetTests : IDisposable
{
    private readonly RadioTestContext test = new();

    private BunitContext Context => test.Context;

    /// <inheritdoc />
    public void Dispose() => test.Dispose();

    [Fact(DisplayName = "labels the radio group from the fieldset legend")]
    [Trait("Requirement", "RDG-ARIA-05")]
    [Trait("Requirement", "RDG-STR-04")]
    [Trait("Requirement", "FST-FRM-03")]
    [Trait("Requirement", "FST-STR-01")]
    public void Labels_the_radio_group_from_the_fieldset_legend()
    {
        RenderFragment<RenderProps<FieldsetRootState>> group = props => builder =>
        {
            builder.OpenComponent<RadioGroup<string>>(0);
            builder.AddMultipleAttributes(1, props.Attributes);
            builder.AddComponentParameter(2, nameof(RadioGroup<string>.ChildContent), props.ChildContent);
            builder.CloseComponent();
        };
        IRenderedComponent<FieldRoot> cut = Context.Render<FieldRoot>(p => p
            .Add(x => x.Name, "test")
            .AddChildContent<FieldsetRoot>(f => f
                .Add(x => x.RenderFunction, group)
                .AddChildContent(builder =>
                {
                    builder.OpenComponent<FieldsetLegend>(0);
                    builder.AddAttribute(1, "data-testid", "legend");
                    builder.CloseComponent();
                    builder.OpenComponent<FieldItem>(2);
                    builder.AddComponentParameter(3, nameof(FieldItem.ChildContent), RadioTestContext.StringRadios("a"));
                    builder.CloseComponent();
                })));

        IElement radioGroup = cut.Find("[role=radiogroup]");
        Assert.Equal(cut.Find("[data-testid=legend]").GetAttribute("id"), radioGroup.GetAttribute("aria-labelledby"));
    }

    [Fact(DisplayName = "updates label precedence without retaining replaced or unmounted IDs")]
    [Trait("Requirement", "RDG-ARIA-05")]
    [Trait("Requirement", "RDG-DOM-06")]
    [Trait("Requirement", "FST-FRM-03")]
    [Trait("Requirement", "FFI-ARIA-08")]
    [Trait("Requirement", "FFI-ARIA-09")]
    public void Updates_label_precedence_without_retaining_replaced_or_unmounted_IDs()
    {
        IRenderedComponent<RadioGroupLabelHost> cut = Context.Render<RadioGroupLabelHost>();
        string? LabelledBy() => cut.Find("[role=radiogroup]").GetAttribute("aria-labelledby");

        Assert.Equal("explicit-label", LabelledBy());

        cut.Render(p => p.Add(x => x.Explicit, false));
        Assert.Equal("field-label-a", LabelledBy());

        cut.Render(p => p.Add(x => x.Explicit, false).Add(x => x.ShowFieldLabel, false));
        Assert.Equal("legend-a", LabelledBy());

        cut.Render(p => p.Add(x => x.Explicit, false).Add(x => x.FieldLabelId, "field-label-b").Add(x => x.ShowFieldLabel, true));
        Assert.Equal("field-label-b", LabelledBy());

        cut.Render(p => p.Add(x => x.Explicit, false).Add(x => x.FieldLabelId, "field-label-b").Add(x => x.ShowFieldLabel, false));
        Assert.Equal("legend-a", LabelledBy());

        cut.Render(p => p.Add(x => x.Explicit, false).Add(x => x.ShowFieldLabel, false).Add(x => x.ShowLegend, false));
        Assert.Null(LabelledBy());

        cut.Render(p => p.Add(x => x.Explicit, false).Add(x => x.ShowFieldLabel, false).Add(x => x.LegendId, "legend-b").Add(x => x.ShowLegend, true));
        Assert.Equal("legend-b", LabelledBy());

        cut.Render(p => p.Add(x => x.Explicit, false).Add(x => x.ShowFieldLabel, false).Add(x => x.LegendId, "legend-b").Add(x => x.ShowLegend, false));
        Assert.Null(LabelledBy());
    }

    // RDG §16 Q7: a consumer aria-label does not suppress the label from the field or the legend (unlike a radio's).
    [Fact(DisplayName = "port: a consumer aria-label does not suppress the group's aria-labelledby")]
    [Trait("Requirement", "RDG-ARIA-05")]
    public void Port_a_consumer_aria_label_does_not_suppress_the_groups_aria_labelledby()
    {
        IRenderedComponent<FieldsetRoot> cut = Context.Render<FieldsetRoot>(p => p.AddChildContent(builder =>
        {
            builder.OpenComponent<FieldsetLegend>(0);
            builder.AddAttribute(1, "id", "legend");
            builder.CloseComponent();
            builder.OpenComponent<RadioGroup<string>>(2);
            builder.AddAttribute(3, "aria-label", "Fruit");
            builder.CloseComponent();
        }));

        Assert.Equal("legend", cut.Find("[role=radiogroup]").GetAttribute("aria-labelledby"));
        Assert.Equal("Fruit", cut.Find("[role=radiogroup]").GetAttribute("aria-label"));
    }
}

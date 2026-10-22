using AngleSharp.Dom;
using Blazix.Primitives.Rendering;
using Bunit;
using Microsoft.AspNetCore.Components;
using Scenario = Blazix.Primitives.Tests.Components.FieldsetScenarios.FieldsetScenario;

namespace Blazix.Primitives.Tests.Components;

/// <summary>
/// The <c>[L2]</c> titles of <c>FieldsetRoot.test.tsx</c>, <c>FieldsetLegend.test.tsx</c> and <c>useRegisteredLabelId.test.tsx</c>,
/// with the port titles for FST-DOM-04, FST-EDGE-03/04 and the Button re-citation of FST-DOM-09.
/// </summary>
public sealed class FieldsetTests : IDisposable
{
    private readonly FieldTestContext host = new();

    private BunitContext Context => host.Context;

    /// <inheritdoc />
    public void Dispose() => host.Dispose();

    // The native consequence, a disabled descendant input, is the browser's (FST-FRM-02, L4 fieldset.spec.ts).
    [Fact(DisplayName = "sets the native disabled attribute")]
    [Trait("Requirement", "FST-API-01")]
    [Trait("Requirement", "FST-DOM-01")]
    public void Sets_the_native_disabled_attribute()
    {
        RenderFragment input = builder =>
        {
            builder.OpenElement(0, "input");
            builder.CloseElement();
        };
        IRenderedComponent<FieldsetRoot> cut = Context.Render<FieldsetRoot>(p => p.Add(x => x.Disabled, true).AddUnmatched("data-testid", "fieldset").AddChildContent(input));

        Assert.True(cut.Find("[data-testid=fieldset]").HasAttribute("disabled"));
        Assert.True(cut.Find("[data-testid=fieldset]").HasAttribute("data-disabled"));
    }

    [Fact(DisplayName = "keeps nested fieldsets disabled when an ancestor fieldset is disabled")]
    [Trait("Requirement", "FST-STR-03")]
    [Trait("Requirement", "FST-API-01")]
    [Trait("Requirement", "FST-FRM-01")]
    public void Keeps_nested_fieldsets_disabled_when_an_ancestor_fieldset_is_disabled()
    {
        IRenderedComponent<FieldsetRoot> cut = Context.Render<FieldsetRoot>(p => p
            .Add(x => x.Disabled, true)
            .AddChildContent<FieldsetRoot>(inner => inner
                .AddChildContent<FieldRoot>(root => root
                    .AddChildContent<FieldControl>(c => c.AddUnmatched("data-testid", "control")))));

        Assert.True(cut.Find("[data-testid=control]").HasAttribute("disabled"));
    }

    [Fact(DisplayName = "updates nested disabled precedence in both directions")]
    [Trait("Requirement", "FST-STR-03")]
    [Trait("Requirement", "FST-STA-01")]
    [Trait("Requirement", "FST-EDGE-01")]
    [Trait("Requirement", "FST-FRM-01")]
    public void Updates_nested_disabled_precedence_in_both_directions()
    {
        IRenderedComponent<FieldsetScenarios> cut = Context.Render<FieldsetScenarios>(p => p
            .Add(x => x.Scenario, Scenario.NestedPrecedence).Add(x => x.OuterDisabled, false).Add(x => x.InnerDisabled, true));

        Assert.True(cut.Find("[data-testid=control]").HasAttribute("disabled"));
        Assert.True(cut.Find("[data-testid=root]").HasAttribute("data-disabled"));
        cut.Render(p => p.Add(x => x.OuterDisabled, true));
        cut.Render(p => p.Add(x => x.InnerDisabled, false));
        Assert.True(cut.Find("[data-testid=control]").HasAttribute("disabled"));
        Assert.True(cut.Find("[data-testid=root]").HasAttribute("data-disabled"));
        cut.Render(p => p.Add(x => x.OuterDisabled, false));
        Assert.False(cut.Find("[data-testid=control]").HasAttribute("disabled"));
        Assert.False(cut.Find("[data-testid=root]").HasAttribute("data-disabled"));
    }

    // FST-DOM-09 and FST-STR-01 re-cited (ADR 13 Amendments 2026-10-22): Base UI's title substitutes RadioGroup,
    // CheckboxGroup and Slider, which arrive in tracks 8 and 6 and in M5; Button, whose Disabled receives the computed
    // value, is the substitute that exists. Blazor matches a component parameter by name case-insensitively, so the spread
    // `disabled` reaches Disabled; a field inside the substituted root sees the fieldset scope.
    [Fact(DisplayName = "passes disabled to a rendered Button root, and the scope reaches the fields inside it")]
    [Trait("Requirement", "FST-DOM-09")]
    [Trait("Requirement", "FST-STR-01")]
    public void Passes_disabled_to_a_rendered_Button_root()
    {
        RenderFragment<RenderProps<FieldsetRootState>> button = props => builder =>
        {
            builder.OpenComponent<Button>(0);
            builder.AddMultipleAttributes(1, props.Attributes);
            builder.AddComponentParameter(2, nameof(Button.NativeButton), false);
            builder.AddComponentParameter(3, nameof(Button.Render), (RenderFragment)(b =>
            {
                b.OpenElement(0, "div");
                b.CloseElement();
            }));
            builder.AddComponentParameter(4, nameof(Button.ChildContent), props.ChildContent);
            builder.CloseComponent();
        };
        IRenderedComponent<FieldsetRoot> cut = Context.Render<FieldsetRoot>(p => p
            .Add(x => x.Disabled, true)
            .Add(x => x.RenderFunction, button)
            .AddUnmatched("data-testid", "button")
            .AddChildContent<FieldRoot>(root => root.AddUnmatched("data-testid", "root")));

        IElement element = cut.Find("[data-testid=button]");
        Assert.Equal("true", element.GetAttribute("aria-disabled"));
        Assert.True(element.HasAttribute("data-disabled"));
        Assert.True(cut.Find("[data-testid=root]").HasAttribute("data-disabled"));
    }

    [Fact(DisplayName = "should set aria-labelledby on the fieldset automatically")]
    [Trait("Requirement", "FST-API-05")]
    [Trait("Requirement", "FST-DOM-03")]
    [Trait("Requirement", "FST-ARIA-01")]
    public void Should_set_aria_labelledby_on_the_fieldset_automatically()
    {
        IRenderedComponent<FieldsetRoot> cut = Context.Render<FieldsetRoot>(p => p
            .AddChildContent<FieldsetLegend>(l => l.AddUnmatched("data-testid", "legend").AddChildContent("Legend")));

        string? id = cut.Find("[data-testid=legend]").GetAttribute("id");
        Assert.False(string.IsNullOrEmpty(id));
        Assert.Equal(id, cut.Find("fieldset").GetAttribute("aria-labelledby"));
        Assert.False(cut.Find("fieldset").HasAttribute("role"));
    }

    [Fact(DisplayName = "should set aria-labelledby on the fieldset with custom id")]
    [Trait("Requirement", "FST-API-05")]
    [Trait("Requirement", "FST-DOM-03")]
    public void Should_set_aria_labelledby_on_the_fieldset_with_custom_id()
    {
        IRenderedComponent<FieldsetRoot> cut = Context.Render<FieldsetRoot>(p => p
            .AddChildContent<FieldsetLegend>(l => l.AddUnmatched("id", "legend-id")));

        Assert.Equal("legend-id", cut.Find("fieldset").GetAttribute("aria-labelledby"));
    }

    [Fact(DisplayName = "reflects the disabled state")]
    [Trait("Requirement", "FST-API-06")]
    [Trait("Requirement", "FST-STA-02")]
    [Trait("Requirement", "FST-DOM-06")]
    public void Reflects_the_disabled_state()
    {
        IRenderedComponent<FieldsetRoot> cut = Context.Render<FieldsetRoot>(p => p
            .Add(x => x.Disabled, true)
            .AddChildContent<FieldsetLegend>(l => l.AddUnmatched("data-testid", "legend").AddChildContent("Legend")));

        Assert.True(cut.Find("[data-testid=legend]").HasAttribute("data-disabled"));
    }

    [Fact(DisplayName = "updates and clears the legend association")]
    [Trait("Requirement", "FST-DOM-03")]
    [Trait("Requirement", "FST-DOM-07")]
    [Trait("Requirement", "FFI-ARIA-08")]
    public void Updates_and_clears_the_legend_association()
    {
        IRenderedComponent<FieldsetScenarios> cut = Context.Render<FieldsetScenarios>(p => p.Add(x => x.Scenario, Scenario.LegendAssociation));

        Assert.Equal("legend-a", cut.Find("fieldset").GetAttribute("aria-labelledby"));
        cut.Render(p => p.Add(x => x.LegendId, "legend-b"));
        Assert.Equal("legend-b", cut.Find("fieldset").GetAttribute("aria-labelledby"));
        cut.Render(p => p.Add(x => x.ShowLegend, false));
        Assert.False(cut.Find("fieldset").HasAttribute("aria-labelledby"));
    }

    [Fact(DisplayName = "throws a descriptive error when rendered outside <Fieldset.Root>")]
    [Trait("Requirement", "FST-STR-02")]
    [Trait("Requirement", "FST-EDGE-02")]
    public void Throws_a_descriptive_error_when_rendered_outside_Fieldset_Root()
    {
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => Context.Render<FieldsetLegend>());

        Assert.Equal("Blazix: The `FieldsetRoot` context is missing. Fieldset parts must be placed within `<FieldsetRoot>`.", error.Message);
    }

    // The server-markup half (the prerendered root carries no aria-labelledby) is the fieldset-prerender fixture (L4).
    [Fact(DisplayName = "does not set `aria-labelledby` during SSR when legend is absent")]
    [Trait("Requirement", "FST-DOM-08")]
    public void Does_not_set_aria_labelledby_during_SSR_when_legend_is_absent()
    {
        IRenderedComponent<FieldsetRoot> cut = Context.Render<FieldsetRoot>(p => p.AddUnmatched("data-testid", "fieldset"));

        Assert.False(cut.Find("[data-testid=fieldset]").HasAttribute("aria-labelledby"));
    }

    [Fact(DisplayName = "sets `aria-labelledby` after hydration without a custom legend id")]
    [Trait("Requirement", "FST-DOM-05")]
    [Trait("Requirement", "FST-DOM-08")]
    public void Sets_aria_labelledby_after_hydration_without_a_custom_legend_id()
    {
        IRenderedComponent<FieldsetRoot> cut = Context.Render<FieldsetRoot>(p => p
            .AddUnmatched("data-testid", "fieldset")
            .AddChildContent<FieldsetLegend>(l => l.AddUnmatched("data-testid", "legend").AddChildContent("Legend")));

        string? id = cut.Find("[data-testid=legend]").GetAttribute("id");
        Assert.False(string.IsNullOrEmpty(id));
        Assert.Equal(id, cut.Find("[data-testid=fieldset]").GetAttribute("aria-labelledby"));
    }

    [Fact(DisplayName = "does not let an older label cleanup clear a newer label")]
    [Trait("Requirement", "FFI-ARIA-08")]
    [Trait("Requirement", "FST-EDGE-03")]
    public void Does_not_let_an_older_label_cleanup_clear_a_newer_label()
    {
        IRenderedComponent<FieldsetScenarios> cut = Context.Render<FieldsetScenarios>(p => p.Add(x => x.Scenario, Scenario.TwoLegends).Add(x => x.Labels, "old"));
        Assert.Equal("old-label", cut.Find("[data-testid=fieldset]").GetAttribute("aria-labelledby"));

        cut.Render(p => p.Add(x => x.Labels, "both"));
        Assert.Equal("new-label", cut.Find("[data-testid=fieldset]").GetAttribute("aria-labelledby"));

        cut.Render(p => p.Add(x => x.Labels, "new"));
        Assert.Equal("new-label", cut.Find("[data-testid=fieldset]").GetAttribute("aria-labelledby"));
    }

    [Fact(DisplayName = "a consumer aria-labelledby replaces the legend's id")]
    [Trait("Requirement", "FST-DOM-04")]
    public void A_consumer_aria_labelledby_replaces_the_legends_id()
    {
        IRenderedComponent<FieldsetRoot> cut = Context.Render<FieldsetRoot>(p => p
            .AddUnmatched("aria-labelledby", "external")
            .AddChildContent<FieldsetLegend>(l => l.AddUnmatched("id", "legend-id")));

        Assert.Equal("external", cut.Find("fieldset").GetAttribute("aria-labelledby"));
    }

    [Fact(DisplayName = "an empty legend id is used verbatim")]
    [Trait("Requirement", "FST-EDGE-04")]
    public void An_empty_legend_id_is_used_verbatim()
    {
        IRenderedComponent<FieldsetRoot> cut = Context.Render<FieldsetRoot>(p => p
            .AddChildContent<FieldsetLegend>(l => l.AddUnmatched("data-testid", "legend").AddUnmatched("id", string.Empty)));

        Assert.Equal(string.Empty, cut.Find("[data-testid=legend]").GetAttribute("id"));
        Assert.Equal(string.Empty, cut.Find("fieldset").GetAttribute("aria-labelledby"));
    }
}

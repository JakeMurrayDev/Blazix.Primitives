using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

// Family tests use the project's root namespace.
namespace Blazix.Primitives.Tests.Components;

// ComboboxCollection.test.tsx, ComboboxGroup.test.tsx and ComboboxGroupLabel.test.tsx at L2 (PR 193 delta review B1): what
// the Collection renders, the Group's role and its labelling by the GroupLabel.
/// <summary>Combobox's Collection, Group and GroupLabel at L2.</summary>
public sealed class ComboboxGroupTitlesTests : IDisposable
{
    private readonly ComboboxTestContext test = new();

    /// <inheritdoc />
    public void Dispose() => test.Dispose();

    [Fact(DisplayName = "renders filtered items")]
    [Trait("Requirement", "CMB-STR-06")]
    [Trait("Requirement", "CMB-API-55")]
    [Trait("Requirement", "CMB-API-62")]
    [Trait("Requirement", "CMB-API-63")]
    public void Renders_filtered_items()
    {
        IRenderedComponent<ComboboxRoot<string>> cut = RenderRoot(["alpha", "beta", "alpine"], list =>
        {
            list.OpenComponent<ComboboxCollection<string>>(0);
            list.AddComponentParameter(1, nameof(ComboboxCollection<string>.ItemContent), (RenderFragment<ComboboxListItem<string>>)(item => inner =>
            {
                inner.OpenComponent<ComboboxItem<string>>(0);
                inner.SetKey(item.Value);
                inner.AddComponentParameter(1, "Value", item.Value);
                inner.AddComponentParameter(2, "data-testid", "item-" + item.Value);
                inner.AddComponentParameter(3, "ChildContent", (RenderFragment)(text => text.AddContent(0, item.Value)));
                inner.CloseComponent();
            }));
            list.CloseComponent();
        });

        Assert.NotNull(cut.Find("[data-testid=item-alpha]"));
        Assert.NotNull(cut.Find("[data-testid=item-beta]"));
        Assert.NotNull(cut.Find("[data-testid=item-alpine]"));
    }

    [Fact(DisplayName = "renders nothing when a nested group does not provide items")]
    [Trait("Requirement", "CMB-STR-06")]
    [Trait("Requirement", "CMB-API-63")]
    public void Renders_nothing_when_a_nested_group_does_not_provide_items()
    {
        IRenderedComponent<ComboboxRoot<string>> cut = RenderRoot(null, list =>
        {
            list.OpenComponent<ComboboxGroup<string>>(0);
            list.AddComponentParameter(1, "data-testid", "group");
            list.AddComponentParameter(2, "ChildContent", (RenderFragment)(group =>
            {
                group.OpenComponent<ComboboxCollection<string>>(0);
                group.AddComponentParameter(1, nameof(ComboboxCollection<string>.ItemContent), (RenderFragment<ComboboxListItem<string>>)(item => inner =>
                {
                    inner.OpenElement(0, "span");
                    inner.AddContent(1, item.Value);
                    inner.CloseElement();
                }));
                group.CloseComponent();
            }));
            list.CloseComponent();
        });

        Assert.Equal(string.Empty, cut.Find("[data-testid=group]").InnerHtml.Trim());
    }

    [Fact(DisplayName = "should render group with label")]
    [Trait("Requirement", "CMB-API-62")]
    [Trait("Requirement", "CMB-ARIA-10")]
    public void Should_render_group_with_label()
    {
        IRenderedComponent<ComboboxRoot<string>> cut = RenderGroup(LabelPart("Fruits"));

        Assert.True(cut.Find("[role=group]").HasAttribute("aria-labelledby"));
        Assert.Contains("Fruits", cut.Find("[role=group]").TextContent, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "should associate label with group")]
    [Trait("Requirement", "CMB-ARIA-10")]
    public void Should_associate_label_with_group()
    {
        IRenderedComponent<ComboboxRoot<string>> cut = RenderGroup(LabelPart("Vegetables"));

        Assert.Equal(cut.Find("[data-testid=group-label]").Id, cut.Find("[role=group]").GetAttribute("aria-labelledby"));
    }

    [Fact(DisplayName = "wires to group aria-labelledby")]
    [Trait("Requirement", "CMB-ARIA-10")]
    public void Wires_to_group_aria_labelledby()
    {
        IRenderedComponent<ComboboxRoot<string>> cut = RenderGroup(LabelPart("Label"));

        Assert.Equal(cut.Find("[data-testid=group-label]").Id, cut.Find("[role=group]").GetAttribute("aria-labelledby"));
    }

    [Fact(DisplayName = "is hidden from the accessibility tree by default")]
    [Trait("Requirement", "CMB-ARIA-10")]
    public void Is_hidden_from_the_accessibility_tree_by_default()
    {
        IRenderedComponent<ComboboxRoot<string>> cut = RenderGroup(LabelPart("Label"));

        Assert.Equal("true", cut.Find("[data-testid=group-label]").GetAttribute("aria-hidden"));
    }

    [Fact(DisplayName = "allows overriding aria-hidden")]
    [Trait("Requirement", "CMB-ARIA-10")]
    public void Allows_overriding_aria_hidden()
    {
        // Base UI removes it with `aria-hidden={undefined}`; a null value is not expressible in Razor (ADR 02), so the port's
        // override is an explicit value, as Menu's and Select's GroupLabel titles do.
        IRenderedComponent<ComboboxRoot<string>> cut = RenderGroup(LabelPart("Label", ("aria-hidden", "false")));

        Assert.Equal("false", cut.Find("[data-testid=group-label]").GetAttribute("aria-hidden"));
    }

    [Fact(DisplayName = "uses provided id in aria-labelledby")]
    [Trait("Requirement", "CMB-ARIA-10")]
    [Trait("Requirement", "CMB-STA-16")]
    public void Uses_provided_id_in_aria_labelledby()
    {
        IRenderedComponent<ComboboxRoot<string>> cut = RenderGroup(LabelPart("Label", ("id", "test-group")));

        Assert.Equal("test-group", cut.Find("[role=group]").GetAttribute("aria-labelledby"));
    }

    [Fact(DisplayName = "does not let an older label cleanup clear a newer label")]
    [Trait("Requirement", "CMB-STA-16")]
    public void Does_not_let_an_older_label_cleanup_clear_a_newer_label()
    {
        IRenderedComponent<ComboboxRoot<string>> cut = RenderGroup(Labels("old"));
        Assert.Equal("old-label", cut.Find("[role=group]").GetAttribute("aria-labelledby"));

        cut.Render(p => p.Add(x => x.Open, true).Add(x => x.ChildContent, GroupContent(Labels("both"))));
        Assert.Equal("new-label", cut.Find("[role=group]").GetAttribute("aria-labelledby"));

        cut.Render(p => p.Add(x => x.Open, true).Add(x => x.ChildContent, GroupContent(Labels("new"))));
        Assert.Equal("new-label", cut.Find("[role=group]").GetAttribute("aria-labelledby"));
    }

    private static RenderFragment Labels(string which) => builder =>
    {
        if (which != "new")
        {
            builder.OpenComponent<ComboboxGroupLabel>(0);
            builder.SetKey("old");
            builder.AddComponentParameter(1, "id", "old-label");
            builder.AddComponentParameter(2, "ChildContent", (RenderFragment)(text => text.AddContent(0, "Old")));
            builder.CloseComponent();
        }

        if (which != "old")
        {
            builder.OpenComponent<ComboboxGroupLabel>(3);
            builder.SetKey("new");
            builder.AddComponentParameter(4, "id", "new-label");
            builder.AddComponentParameter(5, "ChildContent", (RenderFragment)(text => text.AddContent(0, "New")));
            builder.CloseComponent();
        }
    };

    private static RenderFragment LabelPart(string text, params (string Name, object? Value)[] attributes) => builder =>
    {
        builder.OpenComponent<ComboboxGroupLabel>(0);
        builder.AddComponentParameter(1, "data-testid", "group-label");
        foreach ((string name, object? value) in attributes)
        {
            builder.AddComponentParameter(2, name, value);
        }

        builder.AddComponentParameter(3, "ChildContent", (RenderFragment)(inner => inner.AddContent(0, text)));
        builder.CloseComponent();
    };

    private static RenderFragment GroupContent(RenderFragment labels) => builder =>
    {
        builder.OpenComponent<ComboboxPortal>(0);
        builder.AddComponentParameter(1, "ChildContent", (RenderFragment)(portal =>
        {
            portal.OpenComponent<ComboboxPositioner>(0);
            portal.AddComponentParameter(1, "ChildContent", (RenderFragment)(positioner =>
            {
                positioner.OpenComponent<ComboboxPopup>(0);
                positioner.AddComponentParameter(1, "ChildContent", (RenderFragment)(popup =>
                {
                    popup.OpenComponent<ComboboxGroup<string>>(0);
                    popup.AddComponentParameter(1, "ChildContent", labels);
                    popup.CloseComponent();
                }));
                positioner.CloseComponent();
            }));
            portal.CloseComponent();
        }));
        builder.CloseComponent();
    };

    private IRenderedComponent<ComboboxRoot<string>> RenderGroup(RenderFragment labels) =>
        test.Context.Render<ComboboxRoot<string>>(p => p.Add(x => x.Open, true).Add(x => x.ChildContent, GroupContent(labels)));

    private IRenderedComponent<ComboboxRoot<string>> RenderRoot(IReadOnlyList<string>? items, Action<RenderTreeBuilder> listContent)
    {
        void Content(RenderTreeBuilder builder)
        {
            builder.OpenComponent<ComboboxInput>(0);
            builder.CloseComponent();
            builder.OpenComponent<ComboboxPortal>(1);
            builder.AddComponentParameter(2, "ChildContent", (RenderFragment)(portal =>
            {
                portal.OpenComponent<ComboboxPositioner>(0);
                portal.AddComponentParameter(1, "ChildContent", (RenderFragment)(positioner =>
                {
                    positioner.OpenComponent<ComboboxPopup>(0);
                    positioner.AddComponentParameter(1, "ChildContent", (RenderFragment)(popup =>
                    {
                        popup.OpenComponent<ComboboxList<string>>(0);
                        popup.AddComponentParameter(1, "ChildContent", (RenderFragment)(list => listContent(list)));
                        popup.CloseComponent();
                    }));
                    positioner.CloseComponent();
                }));
                portal.CloseComponent();
            }));
            builder.CloseComponent();
        }

        return test.Context.Render<ComboboxRoot<string>>(p => p.Add(x => x.Items, items).Add(x => x.DefaultOpen, true).Add(x => x.ChildContent, (RenderFragment)Content));
    }
}

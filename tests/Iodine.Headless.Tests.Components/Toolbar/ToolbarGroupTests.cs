using AngleSharp.Dom;
using Bunit;
using LibraryOrientation = Blazix.Primitives.Orientation;

// The namespace is the project's root: a namespace named after the family would hide the parts' types. `Orientation` is
// aliased because SampleRoot.cs declares a test-local `Orientation` in this namespace that would shadow the library's.
namespace Blazix.Primitives.Tests.Components;

// ToolbarGroup.test.tsx's [L2] titles verbatim and port titles (marked as such).
/// <summary>The toolbar group: a structural <c>role="group"</c> that contributes its disabled state.</summary>
public sealed class ToolbarGroupTests : IDisposable
{
    private readonly ToolbarTestContext test = new();

    private BunitContext Context => test.Context;

    /// <inheritdoc />
    public void Dispose() => test.Dispose();

    [Fact(DisplayName = "renders a group")]
    [Trait("Requirement", "TLB-DOM-09")]
    [Trait("Requirement", "TLB-ARIA-06")]
    public void Renders_a_group()
    {
        IRenderedComponent<ToolbarRoot> cut = Context.Render<ToolbarRoot>(p => p.AddChildContent<ToolbarGroup>(g => g.AddUnmatched("data-testid", "group")));

        IElement group = cut.Find("[data-testid=group]");
        Assert.Equal("group", group.GetAttribute("role"));
        Assert.Equal("DIV", group.TagName);
    }

    [Fact(DisplayName = "disables all toolbar items except links in the group")]
    [Trait("Requirement", "TLB-API-15")]
    [Trait("Requirement", "TLB-STA-05")]
    [Trait("Requirement", "TLB-ARIA-03")]
    [Trait("Requirement", "TLB-ARIA-04")]
    [Trait("Requirement", "TLB-ARIA-05")]
    [Trait("Requirement", "TLB-EDGE-03")]
    public void Disables_all_toolbar_items_except_links_in_the_group()
    {
        IRenderedComponent<ToolbarRoot> cut = Context.Render<ToolbarRoot>(p => p.AddChildContent<ToolbarGroup>(g => g
            .Add(x => x.Disabled, true)
            .AddChildContent(builder =>
            {
                builder.OpenComponent<ToolbarButton>(0);
                builder.CloseComponent();
                builder.OpenComponent<ToolbarLink>(1);
                builder.AddAttribute(2, "href", "https://base-ui.com");
                builder.CloseComponent();
                builder.OpenComponent<ToolbarInput>(3);
                builder.AddComponentParameter(4, nameof(ToolbarInput.DefaultValue), string.Empty);
                builder.CloseComponent();
            })));

        Assert.All([cut.Find("button"), cut.Find("input")], item =>
        {
            Assert.Equal("true", item.GetAttribute("aria-disabled"));
            Assert.True(item.HasAttribute("data-disabled"));
        });

        Assert.False(cut.Find("a").HasAttribute("data-disabled"));
        Assert.False(cut.Find("a").HasAttribute("aria-disabled"));
    }

    [Fact(DisplayName = "port: the group's disabled state is the root's or its own, its orientation the root's, and it is not an item")]
    [Trait("Requirement", "TLB-API-15")]
    [Trait("Requirement", "TLB-DOM-09")]
    [Trait("Requirement", "TLB-STR-03")]
    [Trait("Requirement", "TLB-ARIA-06")]
    [Trait("Requirement", "TLB-STA-01")]
    public void Port_the_groups_disabled_state_is_the_roots_or_its_own()
    {
        List<ToolbarGroupState> states = [];
        IRenderedComponent<ToolbarRoot> cut = Context.Render<ToolbarRoot>(p => p
            .Add(x => x.Orientation, LibraryOrientation.Vertical)
            .AddChildContent<ToolbarGroup>(g => g.Add(x => x.ClassValue, state =>
            {
                states.Add(state);
                return null;
            })));

        IElement group = cut.Find("[role=group]");
        Assert.False(group.HasAttribute("data-disabled"));
        Assert.Equal("vertical", group.GetAttribute("data-orientation"));
        Assert.False(group.HasAttribute("aria-disabled"));
        Assert.False(group.HasAttribute("tabindex"));
        Assert.Equal(new ToolbarGroupState(Disabled: false, Orientation: LibraryOrientation.Vertical), states[^1]);
        Assert.Empty(ToolbarTestContext.ItemIds(test.LastCompositeOptions));

        cut.Render(p => p.Add(x => x.Orientation, LibraryOrientation.Vertical).Add(x => x.Disabled, true));
        Assert.Equal(string.Empty, cut.Find("[role=group]").GetAttribute("data-disabled"));
        Assert.Equal(new ToolbarGroupState(Disabled: true, Orientation: LibraryOrientation.Vertical), states[^1]);

        IRenderedComponent<ToolbarRoot> own = Context.Render<ToolbarRoot>(p => p.AddChildContent<ToolbarGroup>(g => g.Add(x => x.Disabled, true)));
        Assert.Equal(string.Empty, own.Find("[role=group]").GetAttribute("data-disabled"));
        Assert.Equal("data-disabled", ToolbarGroupDataAttributes.Disabled);
        Assert.Equal("data-orientation", ToolbarGroupDataAttributes.Orientation);
    }

    [Fact(DisplayName = "port: a consumer role wins over the group's")]
    [Trait("Requirement", "TLB-API-16")]
    [Trait("Requirement", "TLB-DOM-11")]
    public void Port_a_consumer_role_wins_over_the_groups()
    {
        IRenderedComponent<ToolbarRoot> cut = Context.Render<ToolbarRoot>(p => p.AddChildContent<ToolbarGroup>(g => g
            .AddUnmatched("role", "radiogroup")
            .AddUnmatched("aria-label", "Numerical format")));

        IElement group = cut.Find("[aria-label='Numerical format']");
        Assert.Equal("radiogroup", group.GetAttribute("role"));
    }
}

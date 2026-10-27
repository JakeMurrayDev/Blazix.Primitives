using System.Text.Json;
using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using LibraryOrientation = Blazix.Primitives.Orientation;

// The namespace is the project's root: a namespace named after the family would hide the parts' types. `Orientation` is
// aliased because SampleRoot.cs declares a test-local `Orientation` in this namespace that would shadow the library's.
namespace Blazix.Primitives.Tests.Components;

// ToolbarRoot.test.tsx's [L2] titles verbatim and port titles (marked as such) for the root's element, its composite twin,
// its contexts and the Test: none rows C# observes. The browser halves are tests/browser/toolbar.spec.ts.
/// <summary>The toolbar root: its element, its composite root and the contexts it shares.</summary>
public sealed class ToolbarRootTests : IDisposable
{
    private readonly ToolbarTestContext test = new();

    private BunitContext Context => test.Context;

    /// <inheritdoc />
    public void Dispose() => test.Dispose();

    [Fact(DisplayName = "has role=\"toolbar\"")]
    [Trait("Requirement", "TLB-ARIA-01")]
    [Trait("Requirement", "TLB-EDGE-13")]
    public void Has_role_toolbar()
    {
        IRenderedComponent<ToolbarRoot> cut = Context.Render<ToolbarRoot>();

        IElement root = Assert.Single(cut.Nodes.OfType<IElement>());
        Assert.Equal("toolbar", root.GetAttribute("role"));
        Assert.Empty(ToolbarTestContext.ItemIds(test.LastCompositeOptions));
    }

    [Fact(DisplayName = "allows optional consumers both outside and inside a toolbar")]
    [Trait("Requirement", "TLB-STR-02")]
    [Trait("Requirement", "TLB-API-02")]
    public void Allows_optional_consumers_both_outside_and_inside_a_toolbar()
    {
        IRenderedComponent<ContainerFragment> cut = Context.Render<ContainerFragment>(p => p.AddChildContent(builder =>
        {
            builder.OpenComponent<OptionalToolbarConsumer>(0);
            builder.CloseComponent();
            builder.OpenComponent<ToolbarRoot>(1);
            builder.AddComponentParameter(2, nameof(ToolbarRoot.Orientation), LibraryOrientation.Vertical);
            builder.AddComponentParameter(3, nameof(ToolbarRoot.ChildContent), (RenderFragment)(content =>
            {
                content.OpenComponent<OptionalToolbarConsumer>(0);
                content.CloseComponent();
            }));
            builder.CloseComponent();
        }));

        Assert.Equal(["outside", "vertical"], cut.FindAll("span").Select(span => span.TextContent));
    }

    [Fact(DisplayName = "port: the toolbar's composite root sends its orientation and loopFocus, with Home and End off and handled keys stopped")]
    [Trait("Requirement", "TLB-API-02")]
    [Trait("Requirement", "TLB-API-03")]
    [Trait("Requirement", "TLB-KEY-04")]
    [Trait("Requirement", "TLB-KEY-06")]
    [Trait("Requirement", "TLB-KEY-07")]
    [Trait("Requirement", "TLB-PTR-03")]
    public void Port_the_toolbars_composite_root_sends_its_options()
    {
        IRenderedComponent<ToolbarRoot> cut = Context.Render<ToolbarRoot>();

        JsonElement attach = JsonDocument.Parse(Assert.Single(test.CompositeAttaches)).RootElement;
        Assert.Equal("horizontal", attach.GetProperty("orientation").GetString());
        Assert.True(attach.GetProperty("loopFocus").GetBoolean());
        Assert.False(attach.GetProperty("enableHomeAndEndKeys").GetBoolean());
        Assert.True(attach.GetProperty("stopEventPropagation").GetBoolean());
        Assert.False(attach.GetProperty("highlightItemOnHover").GetBoolean());
        Assert.Equal(0, attach.GetProperty("modifierKeys").GetArrayLength());
        Assert.False(attach.GetProperty("selectOnNavigation").GetBoolean());

        cut.Render(p => p.Add(x => x.Orientation, LibraryOrientation.Vertical).Add(x => x.LoopFocus, false));

        JsonElement update = JsonDocument.Parse(Assert.Single(test.CompositeUpdates)).RootElement;
        Assert.Equal("vertical", update.GetProperty("orientation").GetString());
        Assert.False(update.GetProperty("loopFocus").GetBoolean());
    }

    [Fact(DisplayName = "port: the root always renders aria-orientation and data-orientation, data-disabled while disabled, and no tabindex")]
    [Trait("Requirement", "TLB-ARIA-02")]
    [Trait("Requirement", "TLB-DOM-01")]
    [Trait("Requirement", "TLB-DOM-02")]
    [Trait("Requirement", "TLB-API-01")]
    public void Port_the_root_renders_its_attributes_and_no_tabindex()
    {
        IRenderedComponent<ToolbarRoot> cut = Context.Render<ToolbarRoot>();

        IElement root = ToolbarTestContext.Root(cut);
        Assert.Equal("horizontal", root.GetAttribute("aria-orientation"));
        Assert.Equal("horizontal", root.GetAttribute("data-orientation"));
        Assert.False(root.HasAttribute("data-disabled"));
        Assert.False(root.HasAttribute("tabindex"));
        Assert.False(root.HasAttribute("disabled"));
        Assert.False(root.HasAttribute("aria-disabled"));

        cut.Render(p => p.Add(x => x.Orientation, LibraryOrientation.Vertical).Add(x => x.Disabled, true));

        root = ToolbarTestContext.Root(cut);
        Assert.Equal("vertical", root.GetAttribute("aria-orientation"));
        Assert.Equal("vertical", root.GetAttribute("data-orientation"));
        Assert.Equal(string.Empty, root.GetAttribute("data-disabled"));
        Assert.False(root.HasAttribute("tabindex"));
    }

    [Fact(DisplayName = "port: a consumer role and aria-orientation win over the root's")]
    [Trait("Requirement", "TLB-API-05")]
    [Trait("Requirement", "TLB-DOM-11")]
    public void Port_a_consumer_role_and_aria_orientation_win()
    {
        IRenderedComponent<ToolbarRoot> cut = Context.Render<ToolbarRoot>(p => p
            .AddUnmatched("role", "menubar")
            .AddUnmatched("aria-orientation", "vertical")
            .AddUnmatched("dir", "rtl"));

        IElement root = Assert.Single(cut.Nodes.OfType<IElement>());
        Assert.Equal("menubar", root.GetAttribute("role"));
        Assert.Equal("vertical", root.GetAttribute("aria-orientation"));
        Assert.Equal("rtl", root.GetAttribute("dir"));

        // TLB-EDGE-06: the `dir` attribute is forwarded but not read; the direction comes from a provider only.
        Assert.Equal("ltr", JsonDocument.Parse(test.LastCompositeOptions).RootElement.GetProperty("direction").GetString());
    }

    [Fact(DisplayName = "port: the root declines its consumer onkeydown, which the composite runs at navigate")]
    [Trait("Requirement", "TLB-API-05")]
    [Trait("Requirement", "TLB-DOM-02")]
    public async Task Port_the_root_declines_its_consumer_onkeydown()
    {
        List<string> keys = [];
        EventCallback<KeyboardEventArgs> onKeyDown = EventCallback.Factory.Create<KeyboardEventArgs>(this, e =>
        {
            keys.Add(e.Key);
            e.PreventPartHandler();
        });
        IRenderedComponent<ToolbarRoot> cut = Context.Render<ToolbarRoot>(p => p
            .AddUnmatched("onkeydown", onKeyDown)
            .AddChildContent<ToolbarLink>(l => l.AddUnmatched("href", "#")));
        string id = cut.Find("a").GetAttribute("id")!;

        Assert.Equal("stop", await test.DecideNavigateAsync(cut, "ArrowRight", id, "A"));
        ToolbarTestContext.Root(cut).KeyDown(new KeyboardEventArgs { Key = "ArrowRight", Code = "ArrowRight" });

        Assert.Equal(["ArrowRight"], keys);
        Assert.Contains("\"reportHandledKeys\":true", test.LastCompositeOptions, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "disables all toolbar items except links")]
    [Trait("Requirement", "TLB-API-01")]
    [Trait("Requirement", "TLB-API-06")]
    [Trait("Requirement", "TLB-API-11")]
    [Trait("Requirement", "TLB-API-12")]
    [Trait("Requirement", "TLB-API-15")]
    [Trait("Requirement", "TLB-STA-05")]
    [Trait("Requirement", "TLB-DOM-06")]
    [Trait("Requirement", "TLB-DOM-09")]
    [Trait("Requirement", "TLB-ARIA-03")]
    [Trait("Requirement", "TLB-ARIA-04")]
    [Trait("Requirement", "TLB-ARIA-05")]
    [Trait("Requirement", "TLB-EDGE-03")]
    public void Disables_all_toolbar_items_except_links()
    {
        RenderFragment items = builder =>
        {
            builder.OpenComponent<ToolbarButton>(0);
            builder.CloseComponent();
            builder.OpenComponent<ToolbarLink>(1);
            builder.AddAttribute(2, "href", "https://base-ui.com");
            builder.AddComponentParameter(3, nameof(ToolbarLink.ChildContent), (RenderFragment)(text => text.AddContent(0, "Link")));
            builder.CloseComponent();
            builder.OpenComponent<ToolbarInput>(4);
            builder.AddComponentParameter(5, nameof(ToolbarInput.DefaultValue), string.Empty);
            builder.CloseComponent();
        };
        IRenderedComponent<ToolbarRoot> cut = Context.Render<ToolbarRoot>(p => p
            .Add(x => x.Disabled, true)
            .AddChildContent(builder =>
            {
                builder.AddContent(0, items);
                builder.OpenComponent<ToolbarGroup>(1);
                builder.AddComponentParameter(2, nameof(ToolbarGroup.ChildContent), items);
                builder.CloseComponent();
            }));

        IReadOnlyList<IElement> toolbarItems = [.. cut.FindAll("button"), .. cut.FindAll("input")];
        Assert.Equal(4, toolbarItems.Count);
        Assert.All(toolbarItems, item =>
        {
            Assert.Equal("true", item.GetAttribute("aria-disabled"));
            Assert.True(item.HasAttribute("data-disabled"));
        });

        Assert.True(cut.Find("[role=group]").HasAttribute("data-disabled"));

        IReadOnlyList<IElement> links = cut.FindAll("a");
        Assert.Equal(2, links.Count);
        Assert.All(links, link =>
        {
            Assert.False(link.HasAttribute("data-disabled"));
            Assert.False(link.HasAttribute("aria-disabled"));
        });
    }

    [Fact(DisplayName = "port: the state objects are the root's Disabled and Orientation")]
    [Trait("Requirement", "TLB-STA-01")]
    [Trait("Requirement", "TLB-API-04")]
    public void Port_the_state_objects_are_the_roots_disabled_and_orientation()
    {
        List<ToolbarRootState> states = [];
        Context.Render<ToolbarRoot>(p => p
            .Add(x => x.Disabled, true)
            .Add(x => x.Orientation, LibraryOrientation.Vertical)
            .Add(x => x.ClassValue, state =>
            {
                states.Add(state);
                return null;
            }));

        Assert.Equal(new ToolbarRootState(Disabled: true, Orientation: LibraryOrientation.Vertical), states[^1]);
        Assert.Equal("data-disabled", ToolbarRootDataAttributes.Disabled);
        Assert.Equal("data-orientation", ToolbarRootDataAttributes.Orientation);
    }

    [Fact(DisplayName = "port: a change of the root's Disabled renders only the parts whose state changed")]
    [Trait("Requirement", "TLB-STA-05")]
    [Trait("Requirement", "TLB-API-01")]
    [Trait("Requirement", "TLB-API-02")]
    public void Port_a_change_of_the_roots_disabled_renders_only_the_parts_whose_state_changed()
    {
        // Design point 14.4 (a): the contexts are listener lists cascaded fixed; each part compares what it renders.
        IRenderedComponent<ToolbarRoot> cut = Context.Render<ToolbarRoot>(p => p.AddChildContent(builder =>
        {
            builder.OpenComponent<ToolbarGroup>(0);
            builder.CloseComponent();
            builder.OpenComponent<ToolbarLink>(1);
            builder.CloseComponent();
            builder.OpenComponent<ToolbarSeparator>(2);
            builder.CloseComponent();
        }));
        IRenderedComponent<ToolbarGroup> group = cut.FindComponent<ToolbarGroup>();
        IRenderedComponent<ToolbarLink> link = cut.FindComponent<ToolbarLink>();
        IRenderedComponent<ToolbarSeparator> separator = cut.FindComponent<ToolbarSeparator>();
        int[] before = [group.RenderCount, link.RenderCount, separator.RenderCount];

        cut.Render(p => p.Add(x => x.Disabled, true));

        Assert.Equal([before[0] + 1, before[1], before[2]], [group.RenderCount, link.RenderCount, separator.RenderCount]);
        Assert.Equal(string.Empty, cut.Find("[role=group]").GetAttribute("data-disabled"));

        cut.Render(p => p.Add(x => x.Disabled, true).Add(x => x.Orientation, LibraryOrientation.Vertical));

        Assert.Equal([before[0] + 2, before[1] + 1, before[2] + 1], [group.RenderCount, link.RenderCount, separator.RenderCount]);
        Assert.Equal("vertical", cut.Find("a").GetAttribute("data-orientation"));
        Assert.Equal("horizontal", cut.Find("[role=separator]").GetAttribute("aria-orientation"));
    }

    [Fact(DisplayName = "port: the toolbar names nothing itself, and a consumer aria-label is forwarded")]
    [Trait("Requirement", "TLB-ARIA-08")]
    public void Port_the_toolbar_names_nothing_itself()
    {
        IRenderedComponent<ToolbarRoot> unnamed = Context.Render<ToolbarRoot>(p => p.AddChildContent<ToolbarGroup>());
        IRenderedComponent<ToolbarRoot> named = Context.Render<ToolbarRoot>(p => p.AddUnmatched("aria-label", "Formatting"));

        foreach (IElement element in new[] { ToolbarTestContext.Root(unnamed), unnamed.Find("[role=group]") })
        {
            Assert.False(element.HasAttribute("aria-label"));
            Assert.False(element.HasAttribute("aria-labelledby"));
        }

        Assert.Equal("Formatting", ToolbarTestContext.Root(named).GetAttribute("aria-label"));
    }

    [Fact(DisplayName = "port: an element that is not a toolbar part is not an item, and every item renders tabindex -1 before the module publishes")]
    [Trait("Requirement", "TLB-EDGE-09")]
    [Trait("Requirement", "TLB-EDGE-07")]
    [Trait("Requirement", "TLB-STR-01")]
    public void Port_an_element_that_is_not_a_toolbar_part_is_not_an_item()
    {
        IRenderedComponent<ToolbarRoot> cut = Context.Render<ToolbarRoot>(p => p.AddChildContent(builder =>
        {
            builder.OpenElement(0, "button");
            builder.AddAttribute(1, "type", "button");
            builder.AddAttribute(2, "data-testid", "plain");
            builder.CloseElement();
            builder.OpenComponent<ToolbarButton>(3);
            builder.CloseComponent();
            builder.OpenComponent<ToolbarLink>(4);
            builder.AddAttribute(5, "href", "#");
            builder.CloseComponent();
            builder.OpenComponent<ToolbarInput>(6);
            builder.CloseComponent();
        }));

        IElement plain = cut.Find("[data-testid=plain]");
        IElement[] items = [cut.Find("[role=toolbar] > button:not([data-testid])"), cut.Find("a"), cut.Find("input")];
        Assert.Equal(items.Select(item => item.GetAttribute("id")!), ToolbarTestContext.ItemIds(test.LastCompositeOptions));
        Assert.False(plain.HasAttribute("tabindex"));
        Assert.False(plain.HasAttribute("id"));

        // The C# markup, also in prerender: the tab stop is the module's, written from its first publication.
        Assert.All(items, item => Assert.Equal("-1", item.GetAttribute("tabindex")));
    }

    [Theory(DisplayName = "port: a toolbar part outside ToolbarRoot throws the missing-context error")]
    [Trait("Requirement", "TLB-STR-02")]
    [Trait("Requirement", "TLB-EDGE-01")]
    [InlineData(typeof(ToolbarButton))]
    [InlineData(typeof(ToolbarInput))]
    [InlineData(typeof(ToolbarGroup))]
    [InlineData(typeof(ToolbarLink))]
    [InlineData(typeof(ToolbarSeparator))]
    public void Port_a_toolbar_part_outside_toolbar_root_throws_the_missing_context_error(Type part)
    {
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => Context.Render(builder =>
        {
            builder.OpenComponent(0, part);
            builder.CloseComponent();
        }));

        Assert.Equal("Blazix: The `ToolbarRoot` context is missing. Toolbar parts must be placed within `<ToolbarRoot>`.", error.Message);
    }

    /// <summary>A plain host for a fragment, so a test can render content beside a toolbar.</summary>
    private sealed class ContainerFragment : ComponentBase
    {
        [Parameter]
        public RenderFragment? ChildContent { get; set; }

        /// <inheritdoc />
        protected override void BuildRenderTree(Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder builder)
        {
            builder.OpenElement(0, "div");
            builder.AddContent(1, ChildContent);
            builder.CloseElement();
        }
    }
}

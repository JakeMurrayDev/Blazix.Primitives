using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using LibraryOrientation = Blazix.Primitives.Orientation;

// The namespace is the project's root: a namespace named after the family would hide the parts' types. `Orientation` is
// aliased because SampleRoot.cs declares a test-local `Orientation` in this namespace that would shadow the library's.
namespace Blazix.Primitives.Tests.Components;

// ToolbarLink.test.tsx's titles verbatim and port titles (marked as such).
/// <summary>The toolbar link: an anchor that is a composite item, never disabled.</summary>
public sealed class ToolbarLinkTests : IDisposable
{
    private readonly ToolbarTestContext test = new();

    private BunitContext Context => test.Context;

    /// <inheritdoc />
    public void Dispose() => test.Dispose();

    [Fact(DisplayName = "renders an anchor")]
    [Trait("Requirement", "TLB-DOM-06")]
    public void Renders_an_anchor()
    {
        IRenderedComponent<ToolbarRoot> cut = Context.Render<ToolbarRoot>(p => p.AddChildContent<ToolbarLink>(l => l
            .AddUnmatched("data-testid", "link")
            .AddUnmatched("href", "https://base-ui.com")));

        IElement link = cut.Find("[data-testid=link]");
        Assert.Equal("A", link.TagName);
        Assert.Equal("https://base-ui.com", link.GetAttribute("href"));
    }

    [Fact(DisplayName = "port: a link is an item with tabindex -1 and data-orientation only, whatever the toolbar's disabled state")]
    [Trait("Requirement", "TLB-DOM-06")]
    [Trait("Requirement", "TLB-ARIA-05")]
    [Trait("Requirement", "TLB-EDGE-03")]
    [Trait("Requirement", "TLB-API-11")]
    [Trait("Requirement", "TLB-STR-01")]
    [Trait("Requirement", "TLB-STA-01")]
    public void Port_a_link_is_an_item_whatever_the_toolbars_disabled_state()
    {
        List<ToolbarLinkState> states = [];
        IRenderedComponent<ToolbarRoot> cut = Context.Render<ToolbarRoot>(p => p
            .Add(x => x.Disabled, true)
            .Add(x => x.Orientation, LibraryOrientation.Vertical)
            .AddChildContent<ToolbarLink>(l => l
                .AddUnmatched("href", "#")
                .Add(x => x.ClassValue, state =>
                {
                    states.Add(state);
                    return null;
                })));

        IElement link = cut.Find("a");
        string id = link.GetAttribute("id")!;
        Assert.StartsWith("base-ui-", id, StringComparison.Ordinal);
        Assert.Equal([id], ToolbarTestContext.ItemIds(test.LastCompositeOptions));
        Assert.Equal("-1", link.GetAttribute("tabindex"));
        Assert.Equal("vertical", link.GetAttribute("data-orientation"));
        foreach (string attribute in new[] { "disabled", "aria-disabled", "data-disabled", "data-focusable", "type", "role" })
        {
            Assert.False(link.HasAttribute(attribute), attribute);
        }

        Assert.Equal(new ToolbarLinkState(LibraryOrientation.Vertical), states[^1]);
        Assert.Equal("data-orientation", ToolbarLinkDataAttributes.Orientation);
        Assert.Empty(test.ButtonAttaches);
    }

    [Fact(DisplayName = "port: a link's C# onkeydown runs once through the item gate")]
    [Trait("Requirement", "TLB-KEY-16")]
    [Trait("Requirement", "TLB-API-11")]
    public async Task Port_a_links_onkeydown_runs_once_through_the_item_gate()
    {
        List<string> keys = [];
        EventCallback<KeyboardEventArgs> onKeyDown = EventCallback.Factory.Create<KeyboardEventArgs>(this, e =>
        {
            keys.Add(e.Key);
            e.PreventPartHandler();
        });
        IRenderedComponent<ToolbarRoot> cut = Context.Render<ToolbarRoot>(p => p.AddChildContent<ToolbarLink>(l => l
            .AddUnmatched("href", "#")
            .AddUnmatched("onkeydown", onKeyDown)));
        string id = cut.Find("a").GetAttribute("id")!;

        // WebAssembly: `navigate` runs the handler inside the dispatch; its veto leaves the key to Blazor, which dispatches it.
        Assert.Equal("stop", await test.DecideNavigateAsync(cut, "ArrowRight", id, "A"));
        cut.Find("a").KeyDown(new KeyboardEventArgs { Key = "ArrowRight", Code = "ArrowRight" });
        Assert.Equal(["ArrowRight"], keys);

        // A key the root does not handle runs from the link's own handler; Enter is the browser's link activation.
        cut.Find("a").KeyDown(new KeyboardEventArgs { Key = "Enter", Code = "Enter" });
        Assert.Equal(["ArrowRight", "Enter"], keys);
    }
}

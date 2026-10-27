using System.Text.Json;
using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using LibraryOrientation = Blazix.Primitives.Orientation;

// The namespace is the project's root: a namespace named after the family would hide the parts' types. `Orientation` is
// aliased because SampleRoot.cs declares a test-local `Orientation` in this namespace that would shadow the library's.
namespace Blazix.Primitives.Tests.Components;

// The toolbar clauses of toggle-group.md's [L2] rows (M3 track 4): a ToggleGroup inside a ToolbarRoot is no composite root
// of its own (TGG-STR-02, TLB-STR-04). The browser halves are in tests/browser/toggle-group.spec.ts and toolbar.spec.ts.
/// <summary>The toggle group inside a toolbar: its toggles are the toolbar's items, and the toolbar's disabled states reach it.</summary>
public sealed class ToggleGroupToolbarTests : IDisposable
{
    private readonly ToolbarTestContext test = new();

    private BunitContext Context => test.Context;

    /// <inheritdoc />
    public void Dispose() => test.Dispose();

    [Fact(DisplayName = "port: inside a toolbar the group is no composite root, and its toggles register with the toolbar's")]
    [Trait("Requirement", "TGG-STR-02")]
    [Trait("Requirement", "TGG-ARIA-05")]
    [Trait("Requirement", "TLB-STR-01")]
    [Trait("Requirement", "TLB-STR-04")]
    public void Port_inside_a_toolbar_the_group_is_no_composite_root()
    {
        IRenderedComponent<ToolbarRoot> cut = Context.Render<ToolbarRoot>(p => p.AddChildContent<ToggleGroup>(g => g
            .AddChildContent(ToggleTestContext.Toggles("one", "two"))));

        IElement group = cut.Find("[role=group]");
        IReadOnlyList<IElement> toggles = ToggleTestContext.Toggles(cut);
        Assert.Single(test.CompositeAttaches);
        Assert.Equal(toggles.Select(toggle => toggle.GetAttribute("id")!), ToolbarTestContext.ItemIds(test.LastCompositeOptions));
        Assert.False(group.HasAttribute("tabindex"));
        Assert.False(group.HasAttribute("id"));
        Assert.All(toggles, toggle => Assert.Equal("-1", toggle.GetAttribute("tabindex")));
        Assert.All(test.ButtonAttaches, options => Assert.Contains("\"composite\":true", options, StringComparison.Ordinal));

        // The value semantics stay the group's.
        toggles[1].Click();
        Assert.Equal("true", ToggleTestContext.Toggles(cut)[1].GetAttribute("aria-pressed"));
        toggles[0].Click();
        Assert.Equal("false", ToggleTestContext.Toggles(cut)[1].GetAttribute("aria-pressed"));
    }

    [Fact(DisplayName = "port: inside a toolbar the group's disabled state is the toolbar's, the toolbar group's or its own")]
    [Trait("Requirement", "TGG-API-04")]
    [Trait("Requirement", "TGG-DOM-03")]
    [Trait("Requirement", "TLB-STA-05")]
    [Trait("Requirement", "TLB-API-15")]
    public void Port_inside_a_toolbar_the_groups_disabled_state_is_the_toolbars()
    {
        IRenderedComponent<ToolbarRoot> cut = Context.Render<ToolbarRoot>(p => p.AddChildContent<ToolbarGroup>(tg => tg
            .AddChildContent<ToggleGroup>(g => g.AddChildContent(ToggleTestContext.Toggles("one")))));
        Assert.False(cut.Find("[role=group] [role=group]").HasAttribute("data-disabled"));

        // The toolbar group's Disabled.
        cut.FindComponent<ToolbarGroup>().Render(p => p.Add(x => x.Disabled, true));
        Assert.Equal(string.Empty, cut.Find("[role=group] [role=group]").GetAttribute("data-disabled"));
        Assert.True(ToggleTestContext.Toggles(cut)[0].HasAttribute("data-disabled"));

        cut.FindComponent<ToolbarGroup>().Render(p => p.Add(x => x.Disabled, false));
        Assert.False(ToggleTestContext.Toggles(cut)[0].HasAttribute("data-disabled"));

        // The toolbar's Disabled.
        cut.Render(p => p.Add(x => x.Disabled, true));
        Assert.Equal(string.Empty, cut.Find("[role=group] [role=group]").GetAttribute("data-disabled"));
        Assert.True(ToggleTestContext.Toggles(cut)[0].HasAttribute("data-disabled"));

        // The group's own Disabled.
        IRenderedComponent<ToolbarRoot> own = Context.Render<ToolbarRoot>(p => p.AddChildContent<ToggleGroup>(g => g
            .Add(x => x.Disabled, true)
            .AddChildContent(ToggleTestContext.Toggles("one"))));
        Assert.True(ToggleTestContext.Toggles(own)[0].HasAttribute("data-disabled"));
    }

    [Fact(DisplayName = "port: inside a toolbar the group's Orientation and LoopFocus send nothing, and data-orientation is its own")]
    [Trait("Requirement", "TGG-EDGE-06")]
    [Trait("Requirement", "TGG-KEY-10")]
    [Trait("Requirement", "TGG-API-05")]
    [Trait("Requirement", "TGG-API-06")]
    public void Port_inside_a_toolbar_the_groups_orientation_and_loop_focus_send_nothing()
    {
        IRenderedComponent<ToolbarRoot> cut = Context.Render<ToolbarRoot>(p => p.AddChildContent<ToggleGroup>(g => g
            .Add(x => x.Orientation, LibraryOrientation.Vertical)
            .Add(x => x.LoopFocus, false)
            .AddChildContent(ToggleTestContext.Toggles("one"))));

        Assert.Equal("vertical", cut.Find("[role=group]").GetAttribute("data-orientation"));
        JsonElement options = JsonDocument.Parse(test.LastCompositeOptions).RootElement;
        Assert.Equal("horizontal", options.GetProperty("orientation").GetString());
        Assert.True(options.GetProperty("loopFocus").GetBoolean());
        Assert.False(options.GetProperty("enableHomeAndEndKeys").GetBoolean());
    }

    [Fact(DisplayName = "port: inside a toolbar the group forwards its consumer onkeydown and renders no handler of its own")]
    [Trait("Requirement", "TGG-DOM-06")]
    [Trait("Requirement", "TGG-API-09")]
    public void Port_inside_a_toolbar_the_group_forwards_its_consumer_onkeydown()
    {
        List<string> keys = [];
        IRenderedComponent<ToolbarRoot> cut = Context.Render<ToolbarRoot>(p => p.AddChildContent<ToggleGroup>(g => g
            .AddUnmatched("onkeydown", EventCallback.Factory.Create<KeyboardEventArgs>(this, e => keys.Add(e.Key)))
            .AddChildContent(ToggleTestContext.Toggles("one"))));

        cut.Find("[role=group]").KeyDown(new KeyboardEventArgs { Key = "a", Code = "KeyA" });

        Assert.Equal(["a"], keys);
        Assert.DoesNotContain("\"reportHandledKeys\":true", test.LastCompositeOptions, StringComparison.Ordinal);
    }
}

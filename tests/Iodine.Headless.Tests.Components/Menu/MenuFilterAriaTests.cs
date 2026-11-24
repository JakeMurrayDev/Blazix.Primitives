using AngleSharp.Dom;
using Bunit;
using Microsoft.Extensions.DependencyInjection;

// Family tests use the project's root namespace (tests/Iodine.Headless.Tests.Components/CONTEXT.md).
namespace Iodine.Headless.Tests.Components;

/// <summary>
/// L2: the popup semantics a filter gives a trigger (MNU-ARIA-01, ARIA-13, STR-14; M5 track 12), over
/// <see cref="MenuFilterAriaHost"/>, <c>MenuFilterRoot.scoping.test.tsx</c>'s and <c>useMenuFilterSubmenuTrigger.test.tsx</c>'s
/// titles verbatim. A title's press or typing step is the module's (L4) and is left out where noted.
/// </summary>
public sealed class MenuFilterAriaTests : BunitContext
{
    public MenuFilterAriaTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddIodineHeadless();
    }

    [Fact(DisplayName = "leaves a plain menu untouched")]
    [Trait("Requirement", "MNU-ARIA-01")]
    public void Leaves_a_plain_menu_untouched()
    {
        IRenderedComponent<MenuFilterAriaHost> cut = Render<MenuFilterAriaHost>(p => p.Add(x => x.Case, "plain"));

        Assert.Equal("menu", cut.Find("button").GetAttribute("aria-haspopup"));
        Assert.Single(cut.FindAll("[role=menu]"));
        Assert.Empty(cut.FindAll("[role=dialog]"));
    }

    [Fact(DisplayName = "filters a submenu of a plain menu")]
    [Trait("Requirement", "MNU-STR-14")]
    [Trait("Requirement", "MNU-ARIA-13")]
    public void Filters_a_submenu_of_a_plain_menu()
    {
        IRenderedComponent<MenuFilterAriaHost> cut = Render<MenuFilterAriaHost>(p => p.Add(x => x.Case, "plain-parent"));

        Assert.Equal("menu", cut.Find("button").GetAttribute("aria-haspopup"));
        IElement submenuTrigger = cut.FindAll("[role=menuitem]").Single(item => item.TextContent.Trim() == "Move to");
        Assert.Equal("dialog", submenuTrigger.GetAttribute("aria-haspopup"));
        IElement dialog = cut.Find("[role=dialog]");
        Assert.Equal(submenuTrigger.Id, dialog.GetAttribute("aria-labelledby"));
        Assert.Equal("Filter folders", dialog.QuerySelector("[role=searchbox]")?.GetAttribute("aria-label"));
    }

    // Base UI's title then clicks the trigger and waits for the input's focus, which is the module's (L4).
    [Fact(DisplayName = "announces a dialog from a detached trigger attached to a filterable root")]
    [Trait("Requirement", "MNU-ARIA-01")]
    public void Announces_a_dialog_from_a_detached_trigger()
    {
        IRenderedComponent<MenuFilterAriaHost> cut = Render<MenuFilterAriaHost>(p => p.Add(x => x.Case, "detached"));

        cut.WaitForAssertion(() => Assert.Equal("dialog", cut.Find("button").GetAttribute("aria-haspopup")));
    }

    [Fact(DisplayName = "reports a menu popup on a trigger whose submenu has no filter")]
    [Trait("Requirement", "MNU-ARIA-13")]
    public void Reports_a_menu_popup_for_a_plain_submenu()
    {
        IRenderedComponent<MenuFilterAriaHost> cut = Render<MenuFilterAriaHost>(p => p.Add(x => x.Case, "plain-submenu"));

        Assert.Equal("menu", cut.FindAll("[role=menuitem]").Single(item => item.TextContent.Trim() == "Sort by").GetAttribute("aria-haspopup"));
    }

    [Fact(DisplayName = "lets a consumer override aria-haspopup")]
    [Trait("Requirement", "MNU-ARIA-13")]
    public void Lets_a_consumer_override_the_submenu_trigger()
    {
        IRenderedComponent<MenuFilterAriaHost> cut = Render<MenuFilterAriaHost>(p => p.Add(x => x.Case, "override-submenu"));

        Assert.Equal("menu", cut.FindAll("[role=menuitem]").Single(item => item.TextContent.Trim() == "Move to").GetAttribute("aria-haspopup"));
    }

    // Base UI's title opens the menu with a click; the host's starts open.
    [Fact(DisplayName = "uses an explicit popup label instead of the trigger label")]
    [Trait("Requirement", "MNU-ARIA-19")]
    public void Uses_an_explicit_popup_label()
    {
        IRenderedComponent<MenuFilterAriaHost> cut = Render<MenuFilterAriaHost>(p => p.Add(x => x.Case, "popup-label"));

        IElement popup = cut.Find("[role=dialog]");
        Assert.Equal("Available commands", popup.GetAttribute("aria-label"));
        Assert.False(popup.HasAttribute("aria-labelledby"));
    }

    [Fact(DisplayName = "does not point the popup or list at a trigger that never rendered")]
    [Trait("Requirement", "MNU-EDGE-08")]
    public void Does_not_point_the_popup_or_list_at_a_trigger_that_never_rendered()
    {
        IRenderedComponent<MenuFilterAriaHost> cut = Render<MenuFilterAriaHost>(p => p.Add(x => x.Case, "triggerless"));

        Assert.False(cut.Find("[role=dialog]").HasAttribute("aria-labelledby"));
        Assert.False(cut.Find("[role=menu]").HasAttribute("aria-labelledby"));
    }

    // Base UI's in-popup button is a parameter change here.
    [Fact(DisplayName = "releases a popup id override when the prop is removed")]
    [Trait("Requirement", "MNU-API-32")]
    public void Releases_a_popup_id_override()
    {
        IRenderedComponent<MenuFilterAriaHost> cut = Render<MenuFilterAriaHost>(p => p.Add(x => x.Case, "popup-id").Add(x => x.PopupId, "custom-popup"));
        Assert.Equal("custom-popup", cut.Find("[role=dialog]").Id);

        cut.Render(p => p.Add(x => x.PopupId, null));

        cut.WaitForAssertion(() => Assert.NotEqual("custom-popup", cut.Find("[role=dialog]").Id));
        IElement popup = cut.Find("[role=dialog]");
        Assert.False(string.IsNullOrEmpty(popup.Id), popup.OuterHtml[..Math.Min(200, popup.OuterHtml.Length)]);
        Assert.Equal(popup.Id, cut.Find("button").GetAttribute("aria-controls"));
        Assert.False(string.IsNullOrEmpty(cut.Find("[role=menuitem]").Id));
    }

    // Issue 244: the list's substitute element keeps its explicitly empty id (RND-DOM-32 (1)), and the input's IDREF follows it.
    [Fact(DisplayName = "omits aria-controls when the list renders with an explicitly empty id")]
    [Trait("Requirement", "MNU-API-62")]
    public void Omits_aria_controls_when_the_list_renders_with_an_explicitly_empty_id()
    {
        IRenderedComponent<MenuFilterAriaHost> cut = Render<MenuFilterAriaHost>(p => p.Add(x => x.Case, "empty-list-id"));

        IElement list = cut.Find("[role=menu]");
        Assert.Equal(string.Empty, list.GetAttribute("id"));
        cut.WaitForAssertion(() => Assert.False(cut.Find("input").HasAttribute("aria-controls"), cut.Find("input").OuterHtml));
    }

    [Fact(DisplayName = "lets a consumer override aria-haspopup on the root trigger")]
    [Trait("Requirement", "MNU-ARIA-01")]
    public void Lets_a_consumer_override_the_root_trigger()
    {
        IRenderedComponent<MenuFilterAriaHost> cut = Render<MenuFilterAriaHost>(p => p.Add(x => x.Case, "override-root"));

        Assert.Equal("menu", cut.Find("button").GetAttribute("aria-haspopup"));
    }
}

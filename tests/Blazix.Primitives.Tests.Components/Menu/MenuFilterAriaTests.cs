using AngleSharp.Dom;
using Bunit;
using Microsoft.Extensions.DependencyInjection;

// Family tests use the project's root namespace (tests/Blazix.Primitives.Tests.Components/CONTEXT.md).
namespace Blazix.Primitives.Tests.Components;

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
        Services.AddBlazixPrimitives();
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

    [Fact(DisplayName = "lets a consumer override aria-haspopup on the root trigger")]
    [Trait("Requirement", "MNU-ARIA-01")]
    public void Lets_a_consumer_override_the_root_trigger()
    {
        IRenderedComponent<MenuFilterAriaHost> cut = Render<MenuFilterAriaHost>(p => p.Add(x => x.Case, "override-root"));

        Assert.Equal("menu", cut.Find("button").GetAttribute("aria-haspopup"));
    }
}

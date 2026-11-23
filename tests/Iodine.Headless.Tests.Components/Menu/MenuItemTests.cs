using AngleSharp.Dom;
using Bunit;
using Microsoft.Extensions.DependencyInjection;

// Family tests use the project's root namespace (tests/Blazix.Primitives.Tests.Components/CONTEXT.md).
namespace Blazix.Primitives.Tests.Components;

/// <summary>
/// L2: <c>MenuItem.test.tsx</c>'s titles over <see cref="MenuItemHost"/> (MNU-STR-07, EDGE-03, API-34, API-35, API-39,
/// PTR-15, EVT-15). A press is the item's C# <c>onclick</c>; the closing titles read the root's recorded change.
/// </summary>
public sealed class MenuItemTests : BunitContext
{
    public MenuItemTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddBlazixPrimitives();
    }

    [Fact(DisplayName = "throws when rendered outside Menu.Root")]
    [Trait("Requirement", "MNU-STR-07")]
    [Trait("Requirement", "MNU-EDGE-03")]
    public void Throws_when_rendered_outside_the_root()
    {
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => Render<MenuItemHost>(p => p.Add(x => x.Outside, true)));

        Assert.Equal("Blazix: The `MenuRoot` context is missing. Menu parts must be placed within `<MenuRoot>`.", error.Message);
    }

    [Fact(DisplayName = "calls the onClick handler when clicked")]
    [Trait("Requirement", "MNU-API-39")]
    [Trait("Requirement", "MNU-PTR-15")]
    [Trait("Requirement", "MNU-EVT-15")]
    public void Calls_the_onclick_handler_when_clicked()
    {
        IRenderedComponent<MenuItemHost> cut = Render<MenuItemHost>();

        Item(cut).Click();

        Assert.Equal(1, cut.Instance.Clicks);
    }

    [Fact(DisplayName = "does not close the menu when onClick prevents Base UI handler")]
    [Trait("Requirement", "MNU-API-39")]
    [Trait("Requirement", "MNU-PTR-15")]
    [Trait("Requirement", "MNU-EVT-15")]
    public void Does_not_close_the_menu_when_onclick_prevents_the_part_handler()
    {
        IRenderedComponent<MenuItemHost> cut = Render<MenuItemHost>(p => p.Add(x => x.PreventOnClick, true));

        Item(cut).Click();

        Assert.Equal(1, cut.Instance.Clicks);
        Assert.Empty(cut.Instance.OpenChanges);
        Assert.Equal("true", cut.Find("[data-testid=trigger]").GetAttribute("aria-expanded"));
    }

    [Fact(DisplayName = "allows onMouseDown to call preventBaseUIHandler")]
    [Trait("Requirement", "MNU-API-39")]
    [Trait("Requirement", "MNU-EVT-15")]
    public void Allows_onmousedown_to_call_prevent_part_handler()
    {
        IRenderedComponent<MenuItemHost> cut = Render<MenuItemHost>(p => p.Add(x => x.PreventOnMouseDown, true));

        Exception? error = Record.Exception(() => Item(cut).MouseDown());

        Assert.Null(error);
    }

    [Fact(DisplayName = "closes the menu when the item is clicked by default")]
    [Trait("Requirement", "MNU-API-34")]
    [Trait("Requirement", "MNU-PTR-15")]
    public void Closes_the_menu_when_the_item_is_clicked_by_default()
    {
        IRenderedComponent<MenuItemHost> cut = Render<MenuItemHost>();

        Item(cut).Click();

        Assert.Equal(["False:ItemPress"], cut.Instance.OpenChanges);
        Assert.Equal("false", cut.Find("[data-testid=trigger]").GetAttribute("aria-expanded"));
    }

    [Fact(DisplayName = "when `closeOnClick=false` does not close the menu when the item is clicked")]
    [Trait("Requirement", "MNU-API-34")]
    [Trait("Requirement", "MNU-PTR-15")]
    public void Does_not_close_the_menu_with_close_on_click_false()
    {
        IRenderedComponent<MenuItemHost> cut = Render<MenuItemHost>(p => p.Add(x => x.CloseOnClick, false));

        Item(cut).Click();

        Assert.Empty(cut.Instance.OpenChanges);
        Assert.Equal("true", cut.Find("[data-testid=trigger]").GetAttribute("aria-expanded"));
    }

    [Fact(DisplayName = "can be focused but not interacted with when disabled")]
    [Trait("Requirement", "MNU-API-35")]
    public void Can_be_focused_but_not_interacted_with_when_disabled()
    {
        IRenderedComponent<MenuItemHost> cut = Render<MenuItemHost>(p => p.Add(x => x.Disabled, true));
        IElement item = Item(cut);

        // Focusable when disabled: no `disabled` attribute, `aria-disabled` instead (the focus itself is the L4 title's).
        Assert.False(item.HasAttribute("disabled"));
        Assert.Equal("true", item.GetAttribute("aria-disabled"));

        item.KeyDown(new Microsoft.AspNetCore.Components.Web.KeyboardEventArgs { Key = "Enter" });
        item.KeyUp(new Microsoft.AspNetCore.Components.Web.KeyboardEventArgs { Key = " " });
        item.Click();

        Assert.Equal((0, 0, 0), (cut.Instance.KeyDowns, cut.Instance.KeyUps, cut.Instance.Clicks));
        Assert.Empty(cut.Instance.OpenChanges);
    }

    private static IElement Item(IRenderedComponent<MenuItemHost> cut) => cut.Find("#item");
}

using AngleSharp.Dom;
using Bunit;
using Microsoft.Extensions.DependencyInjection;

// Family tests use the project's root namespace (tests/Blazix.Primitives.Tests.Components/CONTEXT.md).
namespace Blazix.Primitives.Tests.Components;

/// <summary>
/// L2: the Popup of a filterable menu (MNU-ARIA-06; M5 track 12), over <see cref="MenuFilterInputHost"/>. The trigger's
/// <c>aria-haspopup="dialog"</c> (MNU-ARIA-01) comes with the triggers' filter branch.
/// </summary>
public sealed class MenuFilterPopupTests : BunitContext
{
    public MenuFilterPopupTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddBlazixPrimitives();
    }

    [Theory(DisplayName = "port: a filterable menu's popup is a dialog labelled by its trigger, and its list keeps the menu semantics")]
    [InlineData(Blazix.Primitives.Orientation.Vertical)]
    [InlineData(Blazix.Primitives.Orientation.Horizontal)]
    [Trait("Requirement", "MNU-ARIA-06")]
    public void The_popup_is_a_dialog(Blazix.Primitives.Orientation orientation)
    {
        IRenderedComponent<MenuFilterInputHost> cut = Render<MenuFilterInputHost>(p => p.Add(x => x.Orientation, orientation));
        IElement trigger = cut.Find("button");

        IElement popup = cut.Find("[role=dialog]");
        Assert.Equal(trigger.Id, popup.GetAttribute("aria-labelledby"));
        Assert.False(popup.HasAttribute("aria-orientation"));
        Assert.False(popup.HasAttribute("aria-activedescendant"));
        Assert.NotNull(popup.QuerySelector("[role=searchbox]"));

        IElement list = Assert.Single(popup.QuerySelectorAll("[role=menu]"));
        Assert.Equal(trigger.Id, list.GetAttribute("aria-labelledby"));
        Assert.Equal(orientation == Blazix.Primitives.Orientation.Horizontal ? "horizontal" : null, list.GetAttribute("aria-orientation"));
    }

    [Fact(DisplayName = "enables lazy flipping for a filter menu")]
    [Trait("Requirement", "MNU-POS-05")]
    public void Positioner_enables_lazy_flipping_for_a_filter_menu()
    {
        BunitJSModuleInterop positioning = JSInterop.SetupModule("./_content/Blazix.Primitives/js/positioning.js");
        positioning.SetupModule("attach", _ => true);

        Render<MenuFilterInputHost>();

        string[] options = positioning.Invocations["attach"].Select(attach => (string)attach.Arguments[1]!).ToArray();
        Assert.NotEmpty(options);
        Assert.All(options, json => Assert.Contains("\"lazyFlip\":\"placement\"", json, StringComparison.Ordinal));
    }

    // MenuFilterRoot.scoping.test.tsx's tree has three items; the host's list has one.
    [Fact(DisplayName = "makes the menu filterable")]
    [Trait("Requirement", "MNU-ARIA-01")]
    public void Makes_the_menu_filterable()
    {
        IRenderedComponent<MenuFilterInputHost> cut = Render<MenuFilterInputHost>();

        Assert.Equal("dialog", cut.Find("button").GetAttribute("aria-haspopup"));
        IElement popup = cut.Find("[role=dialog]");
        Assert.NotNull(popup.QuerySelector("[role=searchbox]"));
        Assert.NotNull(popup.QuerySelector("[role=menu]"));
        Assert.Single(cut.FindAll("[role=menuitem]"));
    }

    // MenuFilterRoot.test.tsx's tree opens the submenu by hovering its trigger; the host's submenu starts open.
    [Fact(DisplayName = "uses dialog semantics for a filterable submenu")]
    [Trait("Requirement", "MNU-ARIA-13")]
    [Trait("Requirement", "MNU-ARIA-06")]
    public void Uses_dialog_semantics_for_a_filterable_submenu()
    {
        IRenderedComponent<MenuFilterSubmenuHost> cut = Render<MenuFilterSubmenuHost>();
        IElement submenuTrigger = cut.FindAll("[role=menuitem]").Single(element => element.TextContent.Trim() == "Move to folder");

        Assert.Equal("dialog", submenuTrigger.GetAttribute("aria-haspopup"));
        IElement popup = cut.FindAll("[role=dialog]").Single(element => element.QuerySelector("[aria-label='Filter folders']") is not null);
        Assert.Equal(popup.Id, submenuTrigger.GetAttribute("aria-controls"));
        Assert.Equal(submenuTrigger.Id, popup.GetAttribute("aria-labelledby"));
    }
}

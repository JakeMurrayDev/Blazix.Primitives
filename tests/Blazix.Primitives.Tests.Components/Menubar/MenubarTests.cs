using AngleSharp.Dom;
using Bunit;
using Microsoft.Extensions.DependencyInjection;

// Family tests use the project's root namespace (tests/Blazix.Primitives.Tests.Components/CONTEXT.md).
namespace Blazix.Primitives.Tests.Components;

/// <summary>
/// L2: the <c>Menubar</c> part (MBR §3.1, §4, §5.1; M5 track 8, issue 195): its element, attributes and state, and the
/// flag its menus set (MBR-STA-02..04, DOM-03).
/// </summary>
public sealed class MenubarTests : BunitContext
{
    public MenubarTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddBlazixPrimitives();
    }

    [Fact(DisplayName = "port: Menubar renders a div with role menubar, its id, aria-orientation and its state attributes")]
    [Trait("Requirement", "MBR-STR-01")]
    [Trait("Requirement", "MBR-ARIA-01")]
    [Trait("Requirement", "MBR-ARIA-02")]
    [Trait("Requirement", "MBR-DOM-01")]
    [Trait("Requirement", "MBR-DOM-02")]
    [Trait("Requirement", "MBR-DOM-03")]
    [Trait("Requirement", "MBR-DOM-04")]
    public void Renders_a_div_with_role_menubar_its_id_and_its_attributes()
    {
        IRenderedComponent<MenubarHost> cut = Render<MenubarHost>();

        IElement bar = cut.Find("[role=\"menubar\"]");
        Assert.Equal("DIV", bar.TagName);
        Assert.Equal("bar", bar.Id);
        Assert.Equal("horizontal", bar.GetAttribute("aria-orientation"));
        Assert.Equal("horizontal", bar.GetAttribute("data-orientation"));
        Assert.Equal("", bar.GetAttribute("data-modal"));
        Assert.False(bar.HasAttribute("data-has-submenu-open"));
    }

    [Fact(DisplayName = "port: a vertical, non-modal Menubar renders vertical orientation and no data-modal")]
    [Trait("Requirement", "MBR-API-01")]
    [Trait("Requirement", "MBR-API-03")]
    [Trait("Requirement", "MBR-ARIA-02")]
    [Trait("Requirement", "MBR-DOM-02")]
    public void A_vertical_non_modal_menubar_renders_its_attributes()
    {
        IRenderedComponent<MenubarHost> cut = Render<MenubarHost>(p => p.Add(x => x.Orientation, Blazix.Primitives.Orientation.Vertical).Add(x => x.Modal, false));

        IElement bar = cut.Find("[role=\"menubar\"]");
        Assert.Equal("vertical", bar.GetAttribute("aria-orientation"));
        Assert.Equal("vertical", bar.GetAttribute("data-orientation"));
        Assert.False(bar.HasAttribute("data-modal"));
    }

    [Fact(DisplayName = "port: a consumer role and aria-orientation replace the part's, and a Menubar without an id renders a generated one")]
    [Trait("Requirement", "MBR-API-05")]
    [Trait("Requirement", "MBR-API-06")]
    public void Consumer_attributes_win_and_an_id_is_generated()
    {
        IRenderedComponent<Menubar> cut = Render<Menubar>(p => p.AddUnmatched("role", "toolbar").AddUnmatched("aria-orientation", "vertical"));

        IElement bar = cut.Find("div");
        Assert.Equal("toolbar", bar.GetAttribute("role"));
        Assert.Equal("vertical", bar.GetAttribute("aria-orientation"));
        Assert.False(string.IsNullOrEmpty(bar.Id));
    }

    [Fact(DisplayName = "port: the state is MenubarState with the orientation, modal and whether a menu is open")]
    [Trait("Requirement", "MBR-STA-01")]
    public void The_state_is_the_orientation_modal_and_the_flag()
    {
        MenubarState? seen = null;
        Render<Menubar>(p => p.Add(x => x.Modal, false).Add(x => x.ClassValue, state =>
        {
            seen = state;
            return null;
        }));

        Assert.Equal(new MenubarState(Blazix.Primitives.Orientation.Horizontal, false, false), seen);
    }

    [Fact(DisplayName = "port: a bar menu's open sets data-has-submenu-open, and its close by another reason clears it")]
    [Trait("Requirement", "MBR-STA-02")]
    [Trait("Requirement", "MBR-STA-03")]
    [Trait("Requirement", "MBR-DOM-03")]
    public async Task A_bar_menus_open_sets_the_flag_and_its_close_clears_it()
    {
        IRenderedComponent<MenubarHost> cut = Render<MenubarHost>(p => p.Add(x => x.DefaultOpen, true));
        cut.WaitForAssertion(() => Assert.True(cut.Find("[role=\"menubar\"]").HasAttribute("data-has-submenu-open")), TimeSpan.FromSeconds(2));

        await cut.InvokeAsync(() => cut.FindComponent<MenuRoot<NoPayload>>().Instance.CloseAsync());

        cut.WaitForAssertion(() => Assert.False(cut.Find("[role=\"menubar\"]").HasAttribute("data-has-submenu-open")), TimeSpan.FromSeconds(2));
    }
}

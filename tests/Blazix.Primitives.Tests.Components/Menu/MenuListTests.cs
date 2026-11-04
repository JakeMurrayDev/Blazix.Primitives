using AngleSharp.Dom;
using Bunit;
using Microsoft.Extensions.DependencyInjection;

// Family tests use the project's root namespace (tests/Blazix.Primitives.Tests.Components/CONTEXT.md).
namespace Blazix.Primitives.Tests.Components;

/// <summary>
/// L2: <c>MenuList.test.tsx</c>'s titles that read the render (MNU-STR-13, API-62, ARIA-03, ARIA-05, STA-13, EDGE-09) over
/// <see cref="MenuListHost"/>; its focus and keyboard titles are L4.
/// </summary>
public sealed class MenuListTests : BunitContext
{
    public MenuListTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddBlazixPrimitives();
    }

    [Fact(DisplayName = "takes the menu role and label from the popup")]
    [Trait("Requirement", "MNU-STR-13")]
    [Trait("Requirement", "MNU-ARIA-03")]
    [Trait("Requirement", "MNU-ARIA-05")]
    public void Takes_the_menu_role_and_label_from_the_popup()
    {
        IRenderedComponent<MenuListHost> cut = Render<MenuListHost>();

        IElement trigger = cut.Find("#trigger");
        IElement list = cut.Find("[data-testid=list]");
        IElement popup = cut.Find("[data-testid=popup]");
        Assert.Equal("menu", list.GetAttribute("role"));
        Assert.Equal(trigger.Id, list.GetAttribute("aria-labelledby"));
        Assert.Equal("presentation", popup.GetAttribute("role"));
        Assert.False(popup.HasAttribute("aria-labelledby"));
        Assert.Equal(list.Id, trigger.GetAttribute("aria-controls"));
    }

    [Fact(DisplayName = "updates the trigger when the open list changes its id")]
    [Trait("Requirement", "MNU-API-62")]
    [Trait("Requirement", "MNU-ARIA-03")]
    [Trait("Requirement", "MNU-EDGE-09")]
    public void Updates_the_trigger_when_the_open_list_changes_its_id()
    {
        IRenderedComponent<MenuListHost> cut = Render<MenuListHost>(p => p.Add(x => x.ListId, "old-list"));
        Assert.Equal("old-list", cut.Find("#trigger").GetAttribute("aria-controls"));

        cut.Render(p => p.Add(x => x.ListId, "new-list"));

        cut.WaitForAssertion(() => Assert.Equal("new-list", cut.Find("#trigger").GetAttribute("aria-controls")));
    }

    [Fact(DisplayName = "renders the orientation on the list")]
    [Trait("Requirement", "MNU-STA-13")]
    public void Renders_the_orientation_on_the_list()
    {
        IRenderedComponent<MenuListHost> cut = Render<MenuListHost>(p => p.Add(x => x.Orientation, Blazix.Primitives.Orientation.Horizontal));

        Assert.Equal("horizontal", cut.Find("[data-testid=list]").GetAttribute("aria-orientation"));
        Assert.False(cut.Find("[data-testid=popup]").HasAttribute("aria-orientation"));
    }

    [Fact(DisplayName = "does not point an inactive trigger at the list")]
    [Trait("Requirement", "MNU-ARIA-03")]
    public void Does_not_point_an_inactive_trigger_at_the_list()
    {
        IRenderedComponent<MenuListHost> cut = Render<MenuListHost>(p => p.Add(x => x.TwoTriggers, true));

        Assert.Equal(cut.Find("[data-testid=list]").Id, cut.Find("#first").GetAttribute("aria-controls"));
        Assert.False(cut.Find("#second").HasAttribute("aria-controls"));
    }

    [Fact(DisplayName = "does not re-render an inactive trigger when the list mounts")]
    [Trait("Requirement", "MNU-ARIA-03")]
    public void Does_not_re_render_an_inactive_trigger_when_the_list_mounts()
    {
        IRenderedComponent<MenuListHost> cut = Render<MenuListHost>(p => p
            .Add(x => x.TwoTriggers, true)
            .Add(x => x.StartOpen, false));
        int passesBeforeList = cut.Instance.SecondTriggerPasses;

        // The first trigger's press, through the store: the host itself does not re-render.
        cut.InvokeAsync(() => cut.Instance.Handle.OpenAsync("first"));

        cut.WaitForAssertion(() => Assert.Equal(cut.Find("[data-testid=list]").Id, cut.Find("#first").GetAttribute("aria-controls")));
        Assert.Equal(passesBeforeList, cut.Instance.SecondTriggerPasses);
    }
}

using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

// Family tests use the project's root namespace (tests/Blazix.Primitives.Tests.Components/CONTEXT.md).
namespace Blazix.Primitives.Tests.Components;

/// <summary>
/// L2: C#'s initial render of every item part (MNU-DOM-13, DOM-14, DOM-16, DOM-18 under ADR 04 Amendments 2026-11-03 (2)):
/// the role, <c>tabindex="-1"</c> and no <c>data-highlighted</c>, which the <c>composite</c> list handle owns from attach.
/// </summary>
public sealed class MenuItemInitialRenderTests : BunitContext
{
    public MenuItemInitialRenderTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddBlazixPrimitives();
    }

    [Fact(DisplayName = "port: every item part renders its role, tabindex=\"-1\" and no data-highlighted before the list handle attaches")]
    [Trait("Requirement", "MNU-DOM-13")]
    [Trait("Requirement", "MNU-DOM-14")]
    [Trait("Requirement", "MNU-DOM-16")]
    [Trait("Requirement", "MNU-DOM-18")]
    public void Every_item_part_renders_its_role_tabindex_minus_one_and_no_data_highlighted_before_the_list_handle_attaches()
    {
        IRenderedComponent<MenuRoot<NoPayload>> rendered = Render<MenuRoot<NoPayload>>(p => p
            .Add(x => x.Open, true)
            .Add(x => x.ChildContent, (RenderFragment)(builder =>
            {
                builder.OpenComponent<MenuPortal>(0);
                builder.AddComponentParameter(1, nameof(MenuPortal.ChildContent), (RenderFragment)(portal =>
                {
                    portal.OpenComponent<MenuPositioner>(0);
                    portal.AddComponentParameter(1, nameof(MenuPositioner.ChildContent), (RenderFragment)(positioner =>
                    {
                        positioner.OpenComponent<MenuPopup>(0);
                        positioner.AddComponentParameter(1, nameof(MenuPopup.ChildContent), (RenderFragment)(popup =>
                        {
                            popup.OpenComponent<MenuItem>(0);
                            popup.AddAttribute(1, "data-testid", "item");
                            popup.CloseComponent();
                            popup.OpenComponent<MenuLinkItem>(2);
                            popup.AddAttribute(3, "data-testid", "link");
                            popup.CloseComponent();
                            popup.OpenComponent<MenuCheckboxItem>(4);
                            popup.AddAttribute(5, "data-testid", "checkbox");
                            popup.CloseComponent();
                            popup.OpenComponent<MenuRadioGroup<string>>(6);
                            popup.AddComponentParameter(7, nameof(MenuRadioGroup<string>.ChildContent), (RenderFragment)(group =>
                            {
                                group.OpenComponent<MenuRadioItem<string>>(0);
                                group.AddComponentParameter(1, nameof(MenuRadioItem<string>.Value), "a");
                                group.AddAttribute(2, "data-testid", "radio");
                                group.CloseComponent();
                            }));
                            popup.CloseComponent();
                            popup.OpenComponent<MenuSubmenuRoot>(8);
                            popup.AddComponentParameter(9, nameof(MenuSubmenuRoot.ChildContent), (RenderFragment)(submenu =>
                            {
                                submenu.OpenComponent<MenuSubmenuTrigger>(0);
                                submenu.AddAttribute(1, "data-testid", "submenu-trigger");
                                submenu.CloseComponent();
                            }));
                            popup.CloseComponent();
                        }));
                        positioner.CloseComponent();
                    }));
                    portal.CloseComponent();
                }));
                builder.CloseComponent();
            })));

        (string TestId, string Role)[] parts =
        [
            ("item", "menuitem"),
            ("link", "menuitem"),
            ("checkbox", "menuitemcheckbox"),
            ("radio", "menuitemradio"),
            ("submenu-trigger", "menuitem"),
        ];
        foreach ((string testId, string role) in parts)
        {
            AngleSharp.Dom.IElement element = rendered.Find($"[data-testid='{testId}']");
            Assert.Equal((testId, role, "-1", false), (testId, element.GetAttribute("role"), element.GetAttribute("tabindex"), element.HasAttribute("data-highlighted")));
        }
    }
}

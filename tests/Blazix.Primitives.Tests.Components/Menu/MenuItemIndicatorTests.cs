using Bunit;
using Microsoft.Extensions.DependencyInjection;

// Family tests use the project's root namespace (tests/Blazix.Primitives.Tests.Components/CONTEXT.md).
namespace Blazix.Primitives.Tests.Components;

/// <summary>
/// L2: the missing-context titles of <c>MenuCheckboxItemIndicator.test.tsx</c> and <c>MenuRadioItemIndicator.test.tsx</c>
/// (MNU-STR-09, EDGE-03); their animation titles are L4 (MNU-DOM-17, ANI-13).
/// </summary>
public sealed class MenuItemIndicatorTests : BunitContext
{
    public MenuItemIndicatorTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddBlazixPrimitives();
    }

    [Fact(DisplayName = "throws when rendered outside Menu.CheckboxItem")]
    [Trait("Requirement", "MNU-STR-09")]
    [Trait("Requirement", "MNU-EDGE-03")]
    public void Throws_when_rendered_outside_a_checkbox_item()
    {
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => Render<MenuCheckboxItemIndicator>());

        Assert.Equal("Blazix: The `MenuCheckboxItem` context is missing. MenuCheckboxItem parts must be placed within `<MenuCheckboxItem>`.", error.Message);
    }

    [Fact(DisplayName = "throws when rendered outside Menu.RadioItem")]
    [Trait("Requirement", "MNU-STR-09")]
    [Trait("Requirement", "MNU-EDGE-03")]
    public void Throws_when_rendered_outside_a_radio_item()
    {
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => Render<MenuRadioItemIndicator>());

        Assert.Equal("Blazix: The `MenuRadioItem` context is missing. MenuRadioItem parts must be placed within `<MenuRadioItem>`.", error.Message);
    }
}

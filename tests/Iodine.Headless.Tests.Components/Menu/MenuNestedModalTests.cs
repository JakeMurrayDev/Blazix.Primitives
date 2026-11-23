using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

// Family tests use the project's root namespace (tests/Blazix.Primitives.Tests.Components/CONTEXT.md).
namespace Blazix.Primitives.Tests.Components;

/// <summary>
/// L2: <c>MenuRoot.test.tsx</c>'s nested-modal warning (MNU-API-08, API-49, EDGE-02) on a test-only parent. The production
/// title is the development warnings switch being off, which the L2 run keeps on (Button/DevelopmentWarningsSwitch).
/// </summary>
public sealed class MenuNestedModalTests : BunitContext
{
    private readonly CapturingLogger<MenuRoot<NoPayload>> logger = new();

    public MenuNestedModalTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddBlazixPrimitives();
        Services.AddSingleton<ILogger<MenuRoot<NoPayload>>>(logger);
    }

    [Fact(DisplayName = "warns that nested menus ignore the modal prop")]
    [Trait("Requirement", "MNU-API-08")]
    [Trait("Requirement", "MNU-API-49")]
    [Trait("Requirement", "MNU-EDGE-02")]
    public void Warns_that_nested_menus_ignore_the_modal_parameter()
    {
        Render<MenuParentedHost>(p => p.Add(x => x.Modal, false));

        Assert.Contains(logger.Entries, entry => entry.Message == "Blazix: The `Modal` parameter is not supported on nested menus. It will be ignored.");
    }

    [Fact(DisplayName = "port: a nested menu without a supplied Modal does not warn")]
    [Trait("Requirement", "MNU-EDGE-02")]
    public void A_nested_menu_without_modal_does_not_warn()
    {
        Render<MenuParentedHost>();

        Assert.DoesNotContain(logger.Entries, entry => entry.Message.Contains("not supported on nested menus", StringComparison.Ordinal));
    }
}

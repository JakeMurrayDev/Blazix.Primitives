using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

// Family tests use the project's root namespace (tests/Blazix.Primitives.Tests.Components/CONTEXT.md).
namespace Blazix.Primitives.Tests.Components;

/// <summary>
/// L2: a bar menu's <c>Modal</c> (MBR-API-07, EDGE-02; MenuRoot.tsx:207-213). A Root whose parent is a Menubar is a nested
/// menu for its modality: an explicit <c>Modal</c> is ignored and warns as MNU-EDGE-02's does, on the real bar. The
/// development warnings switch stays on in the L2 run (Button/DevelopmentWarningsSwitch).
/// </summary>
public sealed class MenubarMenuModalTests : BunitContext
{
    private const string Warning = "Blazix: The `Modal` parameter is not supported on nested menus. It will be ignored.";

    private readonly CapturingLogger<MenuRoot<NoPayload>> logger = new();

    public MenubarMenuModalTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddBlazixPrimitives();
        Services.AddSingleton<ILogger<MenuRoot<NoPayload>>>(logger);
    }

    [Theory(DisplayName = "port: a bar menu given an explicit Modal warns that nested menus ignore it")]
    [InlineData(true)]
    [InlineData(false)]
    [Trait("Requirement", "MBR-API-07")]
    [Trait("Requirement", "MBR-EDGE-02")]
    public void A_bar_menu_given_an_explicit_modal_warns(bool modal)
    {
        Render<MenubarHost>(p => p.Add(x => x.RootModal, modal));

        Assert.Contains(logger.Entries, entry => entry.Message == Warning);
    }

    [Fact(DisplayName = "port: a bar menu without a supplied Modal does not warn")]
    [Trait("Requirement", "MBR-EDGE-02")]
    public void A_bar_menu_without_modal_does_not_warn()
    {
        Render<MenubarHost>();

        Assert.DoesNotContain(logger.Entries, entry => entry.Message == Warning);
    }
}

using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

// Family tests use the project's root namespace (tests/Blazix.Primitives.Tests.Components/CONTEXT.md).
namespace Blazix.Primitives.Tests.Components;

/// <summary>
/// L2: the development warning of a filterable menu opened without its input (MNU-EDGE-05; M5 track 12), over
/// <see cref="MenuFilterWarningHost"/>. The production title is the development warnings switch being off, which the L2
/// run keeps on (Button/DevelopmentWarningsSwitch).
/// </summary>
public sealed class MenuFilterWarningTests : BunitContext
{
    private const string Message =
        "Blazix: A filterable menu opened without a `<MenuInput>`. Render the input inside `<MenuPopup>`, or drop `<MenuFilterProvider>` for a menu that does not filter.";

    private readonly CapturingLogger<MenuRoot<NoPayload>> logger = new();

    public MenuFilterWarningTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddBlazixPrimitives();
        Services.AddSingleton<ILogger<MenuRoot<NoPayload>>>(logger);
    }

    [Fact(DisplayName = "warns when a filterable menu opens without an input")]
    [Trait("Requirement", "MNU-EDGE-05")]
    public void Warns_without_an_input()
    {
        IRenderedComponent<MenuFilterWarningHost> cut = Render<MenuFilterWarningHost>();

        cut.WaitForAssertion(() => Assert.Contains(logger.Entries, entry => entry.Message == Message));
    }

    [Fact(DisplayName = "port: does not warn for a filterable menu with an input")]
    [Trait("Requirement", "MNU-EDGE-05")]
    public void Does_not_warn_with_an_input()
    {
        IRenderedComponent<MenuFilterWarningHost> cut = Render<MenuFilterWarningHost>(p => p.Add(x => x.HasInput, true));

        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("[role=menuitem]")));
        Assert.DoesNotContain(logger.Entries, entry => entry.Message.Contains("without a `<MenuInput>`", StringComparison.Ordinal));
    }

    [Fact(DisplayName = "does not warn for a filterable menu with an input and a detached trigger")]
    [Trait("Requirement", "MNU-EDGE-05")]
    public void Does_not_warn_with_an_input_and_a_detached_trigger()
    {
        IRenderedComponent<MenuFilterWarningHost> cut = Render<MenuFilterWarningHost>(p => p.Add(x => x.HasInput, true).Add(x => x.Detached, true));

        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("[role=menuitem]")));
        Assert.DoesNotContain(logger.Entries, entry => entry.Message.Contains("without a `<MenuInput>`", StringComparison.Ordinal));
    }
}

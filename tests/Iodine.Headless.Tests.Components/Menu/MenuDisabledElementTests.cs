using Blazix.Primitives.Interop;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

// Family tests use the project's root namespace (tests/Blazix.Primitives.Tests.Components/CONTEXT.md).
namespace Blazix.Primitives.Tests.Components;

/// <summary>
/// L2: <c>MenuSubmenuTrigger.test.tsx</c>'s disabled-element warning (MNU-EDGE-06). The `button` module checks the rendered
/// element (L3); here its report reaches the trigger's logger. The production title is the development warnings switch
/// being off, which the L2 run keeps on (Button/DevelopmentWarningsSwitch).
/// </summary>
public sealed class MenuDisabledElementTests : BunitContext
{
    private readonly CapturingLogger<MenuSubmenuTrigger> logger = new();

    public MenuDisabledElementTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddBlazixPrimitives();
        Services.AddSingleton<ILogger<MenuSubmenuTrigger>>(logger);
    }

    [Fact(DisplayName = "should warn when a disabled element is detected via render prop with JSX element")]
    [Trait("Requirement", "MNU-EDGE-06")]
    public async Task Warns_when_a_disabled_element_is_detected_via_the_render_function()
    {
        BunitJSModuleInterop button = JSInterop.SetupModule("./_content/Blazix.Primitives/js/button.js");
        button.SetupModule("attach", _ => true);

        IRenderedComponent<MenuPartsHost> cut = Render<MenuPartsHost>(p => p.Add(x => x.Case, "submenu-disabled-element"));

        JSRuntimeInvocation attach = Assert.Single(
            button.Invocations["attach"],
            invocation => ((string)invocation.Arguments[1]!).Contains("\"disabledElementCheck\":true", StringComparison.Ordinal));
        ButtonTwin twin = ((DotNetObjectReference<ButtonTwin>)attach.Arguments[2]!).Value;
        await cut.InvokeAsync(() => twin.OnDisabledElementDetected("{}"));

        string message = Assert.Single(logger.Entries).Message;
        Assert.Equal(
            "Blazix: A disabled element was detected on `<MenuSubmenuTrigger>`. To properly disable the trigger, use the `Disabled` parameter on the component instead of setting it on the rendered element.",
            message);
    }
}

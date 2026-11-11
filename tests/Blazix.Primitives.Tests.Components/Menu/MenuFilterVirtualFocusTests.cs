using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

// Family tests use the project's root namespace (tests/Blazix.Primitives.Tests.Components/CONTEXT.md).
namespace Blazix.Primitives.Tests.Components;

/// <summary>
/// L2: virtual focus in a filterable menu (MNU-STA-22, KEY-22; MenuRoot.tsx:622-634; M5 track 12), over
/// <see cref="MenuFilterInputHost"/>: the Input holds real focus and is the list's reference, so the active trigger only
/// opens the list.
/// </summary>
public sealed class MenuFilterVirtualFocusTests : BunitContext
{
    private readonly BunitJSModuleInterop composite;

    public MenuFilterVirtualFocusTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddBlazixPrimitives();
        composite = JSInterop.SetupModule("./_content/Blazix.Primitives/js/composite.js");
        composite.SetupModule("attach", _ => true).SetupVoid("update", _ => true).SetVoidResult();
    }

    [Fact(DisplayName = "port: the active trigger of a filterable menu takes the list's trigger role, not its reference")]
    [Trait("Requirement", "MNU-STA-22")]
    public void The_active_trigger_is_not_the_reference()
    {
        IRenderedComponent<MenuFilterInputHost> cut = Render<MenuFilterInputHost>();

        cut.WaitForAssertion(() =>
        {
            string options = LastOptionsOf(cut.FindComponent<MenuTrigger<NoPayload>>().Instance.Element?.Id);
            Assert.Contains("\"trigger\":{}", options, StringComparison.Ordinal);
            Assert.DoesNotContain("\"reference\":", options, StringComparison.Ordinal);
        });
    }

    // The options the trigger's composite handle was attached with.
    private string LastOptionsOf(string? id)
    {
        JSRuntimeInvocation attach = composite.Invocations["attach"].Last(i => i.Arguments[0] is ElementReference reference && string.Equals(reference.Id, id, StringComparison.Ordinal));
        return (string)attach.Arguments[1]!;
    }
}

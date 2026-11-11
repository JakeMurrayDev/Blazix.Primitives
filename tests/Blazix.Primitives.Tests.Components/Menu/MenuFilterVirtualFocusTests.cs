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

    [Fact(DisplayName = "port: a filterable menu's list navigates under virtual focus with the filter's options")]
    [Trait("Requirement", "MNU-STA-22")]
    [Trait("Requirement", "MNU-KEY-22")]
    public void The_list_takes_the_filter_options()
    {
        IRenderedComponent<MenuFilterInputHost> cut = Render<MenuFilterInputHost>();

        // MenuRoot.tsx:516-557: virtual, allowEscape (loopFocus and no autoHighlight), the vertical trigger axis, and no
        // typeahead, since the input owns typing.
        cut.WaitForAssertion(() =>
        {
            string options = LastOptionsOf(cut.FindComponent<MenuTrigger<NoPayload>>().Instance.Element?.Id);
            string list = options[options.IndexOf("\"list\":", StringComparison.Ordinal)..];
            Assert.Contains("\"virtual\":true", list, StringComparison.Ordinal);
            Assert.Contains("\"allowEscape\":true", list, StringComparison.Ordinal);
            Assert.Contains("\"triggerOrientation\":\"vertical\"", list, StringComparison.Ordinal);
            Assert.DoesNotContain("\"typeahead\"", list, StringComparison.Ordinal);
        });
    }

    [Fact(DisplayName = "port: the Input is the list's reference under virtual focus")]
    [Trait("Requirement", "MNU-STA-22")]
    [Trait("Requirement", "MNU-KEY-22")]
    public void The_input_is_the_reference()
    {
        IRenderedComponent<MenuFilterInputHost> cut = Render<MenuFilterInputHost>();

        cut.WaitForAssertion(() =>
            Assert.Contains("\"reference\":{}", LastOptionsOf(cut.FindComponent<MenuInput>().Instance.Element?.Id), StringComparison.Ordinal));
    }

    // The options the part's composite handle was attached with.
    private string LastOptionsOf(string? id)
    {
        JSRuntimeInvocation attach = composite.Invocations["attach"].Last(i => i.Arguments[0] is ElementReference reference && string.Equals(reference.Id, id, StringComparison.Ordinal));
        return (string)attach.Arguments[1]!;
    }
}

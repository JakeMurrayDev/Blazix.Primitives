using AngleSharp.Dom;
using Bunit;
using Microsoft.Extensions.DependencyInjection;

// Family tests use the project's root namespace (tests/Blazix.Primitives.Tests.Components/CONTEXT.md).
namespace Blazix.Primitives.Tests.Components;

/// <summary>
/// L2: <see cref="MenuClear"/> (MNU-API-60, DOM-22, ARIA-16, EVT-17, PTR-21; M5 track 12), over
/// <see cref="MenuFilterClearHost"/>. The input's refocus and the <c>mousedown</c> default are L4.
/// </summary>
public sealed class MenuFilterClearTests : BunitContext
{
    private const string ClearSelector = "[aria-label='Clear filter']";

    public MenuFilterClearTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddBlazixPrimitives();
    }

    [Fact(DisplayName = "port: renders the clear button only while the query is not empty, hidden from the tab order and the accessibility tree")]
    [Trait("Requirement", "MNU-API-60")]
    [Trait("Requirement", "MNU-DOM-22")]
    [Trait("Requirement", "MNU-ARIA-16")]
    public void Renders_only_while_the_query_is_not_empty()
    {
        IRenderedComponent<MenuFilterClearHost> empty = Render<MenuFilterClearHost>();
        Assert.Empty(empty.FindAll(ClearSelector));

        IRenderedComponent<MenuFilterClearHost> cut = Render<MenuFilterClearHost>(p => p.Add(x => x.DefaultQuery, "r"));
        IElement clear = cut.Find(ClearSelector);

        Assert.Equal("BUTTON", clear.TagName);
        Assert.Equal(
            ["button", "-1", "true"],
            new[] { "type", "tabindex", "aria-hidden" }.Select(clear.GetAttribute));
        Assert.False(clear.HasAttribute(MenuClearDataAttributes.Disabled));
    }

    // MenuFilterParts.test.tsx: aria-hidden takes the button out of the accessibility tree, which bUnit does not compute.
    [Fact(DisplayName = "excludes Clear from the tab order and accessibility tree")]
    [Trait("Requirement", "MNU-ARIA-16")]
    [Trait("Requirement", "MNU-PTR-21")]
    public void Excludes_Clear_from_the_tab_order_and_accessibility_tree()
    {
        IRenderedComponent<MenuFilterClearHost> cut = Render<MenuFilterClearHost>(p => p.Add(x => x.DefaultQuery, "query"));
        IElement clear = cut.Find(ClearSelector);

        Assert.Equal("-1", clear.GetAttribute("tabindex"));
        Assert.Equal("true", clear.GetAttribute("aria-hidden"));
    }

    [Fact(DisplayName = "reports clear-press when the clear button is used")]
    [Trait("Requirement", "MNU-EVT-17")]
    [Trait("Requirement", "MNU-PTR-21")]
    [Trait("Requirement", "MNU-API-58")]
    public void Reports_clear_press()
    {
        List<(string Value, EventReason Reason)> requests = [];
        IRenderedComponent<MenuFilterClearHost> cut = Render<MenuFilterClearHost>(p => p
            .Add(x => x.DefaultQuery, "r")
            .Add(x => x.OnValueChange, (ValueChangeEventArgs<string> args) => requests.Add((args.Value, args.Details.Reason))));

        cut.Find(ClearSelector).Click();

        Assert.Equal(("", EventReason.ClearPress), requests[^1]);
        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(ClearSelector)));
    }

    [Theory(DisplayName = "disables filter controls when the root is disabled")]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [Trait("Requirement", "MNU-API-60")]
    [Trait("Requirement", "MNU-DOM-22")]
    public void Disables_the_controls_with_the_root(bool rootDisabled, bool clearDisabled)
    {
        List<EventReason> requests = [];
        IRenderedComponent<MenuFilterClearHost> cut = Render<MenuFilterClearHost>(p => p
            .Add(x => x.DefaultQuery, "a")
            .Add(x => x.Disabled, rootDisabled)
            .Add(x => x.ClearDisabled, clearDisabled)
            .Add(x => x.OnValueChange, (ValueChangeEventArgs<string> args) => requests.Add(args.Details.Reason)));
        IElement clear = cut.Find(ClearSelector);

        Assert.True(clear.HasAttribute("disabled"));
        Assert.True(clear.HasAttribute(MenuClearDataAttributes.Disabled));
        Assert.Equal(rootDisabled, cut.Find("input").HasAttribute("disabled"));
        clear.Click();
        Assert.Empty(requests);
    }
}

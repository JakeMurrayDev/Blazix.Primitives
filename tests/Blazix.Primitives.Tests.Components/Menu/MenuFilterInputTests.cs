using AngleSharp.Dom;
using Bunit;
using Microsoft.Extensions.DependencyInjection;

// Family tests use the project's root namespace (tests/Blazix.Primitives.Tests.Components/CONTEXT.md).
namespace Blazix.Primitives.Tests.Components;

/// <summary>
/// L2: <see cref="MenuInput"/>'s own element, C#'s render before the <c>menu-filter</c> handle attaches (MNU-API-59, DOM-21;
/// M5 track 12), over <see cref="MenuFilterInputHost"/>.
/// </summary>
public sealed class MenuFilterInputTests : BunitContext
{
    public MenuFilterInputTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddBlazixPrimitives();
    }

    [Fact(DisplayName = "uses the id passed to Menu.Input")]
    [Trait("Requirement", "MNU-API-59")]
    public void Uses_the_id_passed_to_the_input()
    {
        IRenderedComponent<MenuFilterInputHost> cut = Render<MenuFilterInputHost>(p => p.Add(x => x.InputId, "action-filter"));

        Assert.Equal("action-filter", Input(cut).Id);
        Assert.Single(cut.FindAll("[role=searchbox]"));
    }

    [Fact(DisplayName = "port: renders a text searchbox with the filter attributes, a generated id and the query as its value")]
    [Trait("Requirement", "MNU-DOM-21")]
    [Trait("Requirement", "MNU-API-57")]
    public void Renders_a_text_searchbox_with_the_filter_attributes()
    {
        IRenderedComponent<MenuFilterInputHost> cut = Render<MenuFilterInputHost>(p => p.Add(x => x.DefaultQuery, "re"));
        IElement input = Input(cut);

        Assert.Equal(
            ["text", "searchbox", "search", "off", "false", "off", "none", "re"],
            new[] { "type", "role", "inputmode", "autocomplete", "spellcheck", "autocorrect", "autocapitalize", "value" }.Select(input.GetAttribute));
        Assert.False(string.IsNullOrEmpty(input.Id));
        Assert.False(input.HasAttribute("aria-autocomplete"));
        Assert.False(input.HasAttribute("disabled"));
    }

    [Theory(DisplayName = "port: the input is disabled with its menu or on its own")]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [Trait("Requirement", "MNU-API-59")]
    [Trait("Requirement", "MNU-DOM-21")]
    public void The_input_is_disabled_with_its_menu_or_on_its_own(bool rootDisabled, bool inputDisabled)
    {
        IRenderedComponent<MenuFilterInputHost> cut = Render<MenuFilterInputHost>(p => p
            .Add(x => x.Disabled, rootDisabled)
            .Add(x => x.InputDisabled, inputDisabled));

        Assert.True(Input(cut).HasAttribute("disabled"));
    }

    [Fact(DisplayName = "port: points aria-controls at the filter list's id, its own or generated")]
    [Trait("Requirement", "MNU-API-62")]
    [Trait("Requirement", "MNU-DOM-21")]
    public void Points_aria_controls_at_the_list()
    {
        IRenderedComponent<MenuFilterInputHost> generated = Render<MenuFilterInputHost>();
        generated.WaitForAssertion(() => Assert.Equal(generated.Find("[role=menu]").Id, Input(generated).GetAttribute("aria-controls")));
        Assert.False(string.IsNullOrEmpty(generated.Find("[role=menu]").Id));

        IRenderedComponent<MenuFilterInputHost> own = Render<MenuFilterInputHost>(p => p.Add(x => x.ListId, "action-list"));
        own.WaitForAssertion(() => Assert.Equal("action-list", Input(own).GetAttribute("aria-controls")));
    }

    private static IElement Input(IRenderedComponent<MenuFilterInputHost> cut) => cut.Find("input");
}

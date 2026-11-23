using AngleSharp.Dom;
using Bunit;
using Microsoft.Extensions.DependencyInjection;

// Family tests use the project's root namespace (tests/Blazix.Primitives.Tests.Components/CONTEXT.md).
namespace Blazix.Primitives.Tests.Components;

/// <summary>L2: <c>MenuGroupLabel.test.tsx</c>'s titles over <see cref="MenuGroupLabelHost"/> (MNU-STR-09, API-48, DOM-19, ARIA-10, ARIA-11).</summary>
public sealed class MenuGroupLabelTests : BunitContext
{
    public MenuGroupLabelTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddBlazixPrimitives();
    }

    [Fact(DisplayName = "throws when rendered outside Menu.Group or Menu.RadioGroup")]
    [Trait("Requirement", "MNU-STR-09")]
    public void Throws_when_rendered_outside_a_group()
    {
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => Render<MenuGroupLabelHost>(p => p.Add(x => x.Case, "outside")));

        Assert.Equal("Blazix: The `MenuGroup` context is missing. Menu group parts must be used within `<MenuGroup>` or `<MenuRadioGroup>`.", error.Message);
    }

    [Fact(DisplayName = "is hidden from the accessibility tree by default")]
    [Trait("Requirement", "MNU-ARIA-11")]
    public void Is_hidden_from_the_accessibility_tree_by_default()
    {
        IRenderedComponent<MenuGroupLabelHost> cut = Render<MenuGroupLabelHost>();

        Assert.Equal("true", Label(cut).GetAttribute("aria-hidden"));
    }

    [Fact(DisplayName = "allows overriding aria-hidden")]
    [Trait("Requirement", "MNU-ARIA-11")]
    public void Allows_overriding_aria_hidden()
    {
        // Base UI removes it with `aria-hidden={undefined}`; a null value is not expressible in Razor (ADR 02), so the port's
        // override is a value.
        IRenderedComponent<MenuGroupLabelHost> cut = Render<MenuGroupLabelHost>(p => p.Add(x => x.AriaHidden, "false"));

        Assert.Equal("false", Label(cut).GetAttribute("aria-hidden"));
    }

    [Fact(DisplayName = "should reference the generated id in Group's `aria-labelledby`")]
    [Trait("Requirement", "MNU-API-48")]
    [Trait("Requirement", "MNU-ARIA-10")]
    public void References_the_generated_id_in_the_group()
    {
        IRenderedComponent<MenuGroupLabelHost> cut = Render<MenuGroupLabelHost>();

        IElement label = Label(cut);
        Assert.False(string.IsNullOrEmpty(label.Id));
        Assert.Equal(label.Id, cut.Find("[role=group]").GetAttribute("aria-labelledby"));
    }

    [Fact(DisplayName = "should reference the provided id in Group's `aria-labelledby`")]
    [Trait("Requirement", "MNU-API-48")]
    [Trait("Requirement", "MNU-DOM-19")]
    public void References_the_provided_id_in_the_group()
    {
        IRenderedComponent<MenuGroupLabelHost> cut = Render<MenuGroupLabelHost>(p => p.Add(x => x.LabelId, "test-group"));

        Assert.Equal("test-group", cut.Find("[role=group]").GetAttribute("aria-labelledby"));
    }

    [Fact(DisplayName = "should reference the generated id in RadioGroup's `aria-labelledby`")]
    [Trait("Requirement", "MNU-ARIA-10")]
    public void References_the_generated_id_in_the_radio_group()
    {
        IRenderedComponent<MenuGroupLabelHost> cut = Render<MenuGroupLabelHost>(p => p.Add(x => x.Case, "radio-group"));

        IElement label = Label(cut);
        Assert.False(string.IsNullOrEmpty(label.Id));
        Assert.Equal(label.Id, cut.Find("[role=group]").GetAttribute("aria-labelledby"));
    }

    [Fact(DisplayName = "should reference the provided id in RadioGroup's `aria-labelledby`")]
    [Trait("Requirement", "MNU-ARIA-10")]
    public void References_the_provided_id_in_the_radio_group()
    {
        IRenderedComponent<MenuGroupLabelHost> cut = Render<MenuGroupLabelHost>(p => p
            .Add(x => x.Case, "radio-group")
            .Add(x => x.LabelId, "test-group"));

        Assert.Equal("test-group", cut.Find("[role=group]").GetAttribute("aria-labelledby"));
    }

    [Fact(DisplayName = "should support GroupLabel when RadioGroup is rendered as Group")]
    [Trait("Requirement", "MNU-ARIA-10")]
    public void Supports_the_label_when_a_radio_group_renders_the_group()
    {
        IRenderedComponent<MenuGroupLabelHost> cut = Render<MenuGroupLabelHost>(p => p.Add(x => x.Case, "group-as-radio-group"));

        IElement label = Label(cut);
        Assert.Equal(label.Id, Assert.Single(cut.FindAll("[role=group]")).GetAttribute("aria-labelledby"));
    }

    [Fact(DisplayName = "does not let an older label cleanup clear a newer label")]
    [Trait("Requirement", "MNU-ARIA-10")]
    public void Does_not_let_an_older_label_cleanup_clear_a_newer_label()
    {
        IRenderedComponent<MenuGroupLabelHost> cut = Render<MenuGroupLabelHost>(p => p
            .Add(x => x.Case, "labels")
            .Add(x => x.Labels, "old"));
        Assert.Equal("old-label", cut.Find("[role=group]").GetAttribute("aria-labelledby"));

        cut.Render(p => p.Add(x => x.Labels, "both"));
        Assert.Equal("new-label", cut.Find("[role=group]").GetAttribute("aria-labelledby"));

        cut.Render(p => p.Add(x => x.Labels, "new"));
        Assert.Equal("new-label", cut.Find("[role=group]").GetAttribute("aria-labelledby"));
    }

    private static IElement Label(IRenderedComponent<MenuGroupLabelHost> cut) =>
        cut.FindAll("div").Single(element => element.TextContent == "Test group" && element.Children.Length == 0);
}

using Bunit;
using Microsoft.Extensions.DependencyInjection;

// Family tests use the project's root namespace (tests/Blazix.Primitives.Tests.Components/CONTEXT.md).
namespace Blazix.Primitives.Tests.Components;

/// <summary>
/// L2: the filter parts' placement (MNU-STR-15, EDGE-04; M5 track 12), <c>MenuFilterParts.test.tsx</c>'s and
/// <c>MenuFilterRoot.scoping.test.tsx</c>'s throwing titles over <see cref="MenuFilterPartsHost"/>, with Base UI's message
/// in the port's C# names.
/// </summary>
public sealed class MenuFilterPartsTests : BunitContext
{
    public MenuFilterPartsTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddBlazixPrimitives();
    }

    [Theory(DisplayName = "throws when <Menu.%s> is rendered without a filter provider")]
    [InlineData("Input")]
    [InlineData("Clear")]
    [InlineData("Empty")]
    [Trait("Requirement", "MNU-EDGE-04")]
    [Trait("Requirement", "MNU-STR-15")]
    public void Throws_when_a_filter_part_is_rendered_without_a_filter_provider(string part)
    {
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() =>
            Render<MenuFilterPartsHost>(p => p.Add(x => x.Case, $"{part.ToLowerInvariant()}-alone")));

        Assert.Equal(Message(part), error.Message);
    }

    // MenuFilterParts.test.tsx renders the trigger alone, as MenuTrigger.test.tsx does; the port's host is Menu's own.
    [Fact(DisplayName = "throws when Trigger is rendered without a root or handle")]
    [Trait("Requirement", "MNU-EDGE-01")]
    public void Throws_when_the_trigger_is_rendered_without_a_root_or_handle()
    {
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => Render<MenuPartsHost>(p => p.Add(x => x.Case, "trigger-alone")));

        Assert.Equal("Blazix: `<MenuTrigger>` must be either used within a `<MenuRoot>` component or provided with a handle.", error.Message);
    }

    [Fact(DisplayName = "throws when a filter part is rendered in a plain menu")]
    [Trait("Requirement", "MNU-EDGE-04")]
    [Trait("Requirement", "MNU-STR-15")]
    public void Throws_when_a_filter_part_is_rendered_in_a_plain_menu()
    {
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() =>
            Render<MenuFilterPartsHost>(p => p.Add(x => x.Case, "plain-menu-input")));

        Assert.Equal(Message("Input"), error.Message);
    }

    [Fact(DisplayName = "throws when a filter part is rendered in a plain submenu of a filterable menu")]
    [Trait("Requirement", "MNU-EDGE-04")]
    [Trait("Requirement", "MNU-STR-14")]
    public void Throws_when_a_filter_part_is_rendered_in_a_plain_submenu_of_a_filterable_menu()
    {
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() =>
            Render<MenuFilterPartsHost>(p => p.Add(x => x.Case, "plain-submenu-input")));

        Assert.Equal(Message("Input"), error.Message);
    }

    [Fact(DisplayName = "port: the provider makes exactly the root directly inside it filterable, and each searchable submenu needs its own")]
    [Trait("Requirement", "MNU-STR-14")]
    public void The_provider_makes_exactly_the_root_directly_inside_it_filterable()
    {
        Dictionary<string, bool> seen = [];

        Render<MenuFilterPartsHost>(p => p.Add(x => x.Case, "scopes").Add(x => x.Seen, seen));

        Assert.Equal(
            new Dictionary<string, bool> { ["root"] = true, ["plain-submenu"] = false, ["filterable-submenu"] = true, ["plain-root"] = false },
            seen);
    }

    private static string Message(string part) =>
        $"Blazix: `<Menu{part}>` must be placed in a menu wrapped in `<MenuFilterProvider>`. It reads the filter query and the "
        + "matching items from the provider, which a plain menu does not have. Wrap the `<MenuRoot>` or `<MenuSubmenuRoot>` it "
        + "belongs to in `<MenuFilterProvider>`.";
}

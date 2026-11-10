using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

// Family tests use the project's root namespace.
namespace Blazix.Primitives.Tests.Components;

// CMB §3.10 at L2 (ComboboxValue.test.tsx's titles whose items carry no labels of their own): the content's order, the
// placeholder, the labels in both modes, and the templates. Base UI's one `children` prop is the port's ChildContent beside a
// template per mode (ValueContent, ValuesContent), so the port titles cover the template that does not fit the root's mode.
// The titles whose items label a value, the null item among them, wait for ComboboxItems.Create (design point 2).
/// <summary>Combobox's Value part at L2.</summary>
public sealed class ComboboxValueTests : IDisposable
{
    private static readonly string[] s_fruits = ["apple", "banana", "cherry"];

    private readonly ComboboxTestContext test = new();

    /// <inheritdoc />
    public void Dispose() => test.Dispose();

    [Fact(DisplayName = "renders current selected value via function child")]
    [Trait("Requirement", "CMB-API-46")]
    public void Renders_current_selected_value_via_function_child()
    {
        IRenderedComponent<ComboboxHarness> cut = Render(
            new() { ["DefaultValue"] = "apple" },
            ("ValueContent", (RenderFragment<string>)(value => builder => builder.AddContent(0, "fruit: " + value))));

        Assert.Equal("fruit: apple", ValueText(cut));
    }

    [Fact(DisplayName = "renders function child with null when no value selected")]
    [Trait("Requirement", "CMB-API-46")]
    public void Renders_function_child_with_null_when_no_value_selected()
    {
        IRenderedComponent<ComboboxHarness> cut = Render(
            new(),
            ("ValueContent", (RenderFragment<string>)(value => builder => builder.AddContent(0, value ?? "none"))));

        Assert.Equal("none", ValueText(cut));
    }

    [Fact(DisplayName = "overrides the value display when children is a static ReactNode")]
    [Trait("Requirement", "CMB-API-46")]
    public void Overrides_the_value_display_when_children_is_static_content()
    {
        IRenderedComponent<ComboboxHarness> cut = Render(new() { ["DefaultValue"] = "apple" }, ("ChildContent", Text("Custom")));

        Assert.Equal("Custom", ValueText(cut));
    }

    [Fact(DisplayName = "port: the mode's template takes precedence over child content")]
    [Trait("Requirement", "CMB-API-46")]
    public void The_modes_template_takes_precedence_over_child_content()
    {
        IRenderedComponent<ComboboxHarness> cut = Render(
            new() { ["DefaultValue"] = "apple" },
            ("ChildContent", Text("Custom")),
            ("ValueContent", (RenderFragment<string>)(value => builder => builder.AddContent(0, "fruit: " + value))));

        Assert.Equal("fruit: apple", ValueText(cut));
    }

    [Fact(DisplayName = "displays placeholder when no value is selected")]
    [Trait("Requirement", "CMB-API-47")]
    public void Displays_placeholder_when_no_value_is_selected()
    {
        IRenderedComponent<ComboboxHarness> cut = Render(new(), ("Placeholder", Text("Select a fruit")));

        Assert.Equal("Select a fruit", ValueText(cut));
    }

    [Fact(DisplayName = "does not display placeholder when value is selected")]
    [Trait("Requirement", "CMB-API-47")]
    public void Does_not_display_placeholder_when_value_is_selected()
    {
        IRenderedComponent<ComboboxHarness> cut = Render(new() { ["DefaultValue"] = "apple" }, ("Placeholder", Text("Select a fruit")));

        Assert.Equal("apple", ValueText(cut));
    }

    [Fact(DisplayName = "children prop takes precedence over placeholder")]
    [Trait("Requirement", "CMB-API-47")]
    public void Children_prop_takes_precedence_over_placeholder()
    {
        IRenderedComponent<ComboboxHarness> cut = Render(new(), ("ChildContent", Text("Custom")), ("Placeholder", Text("Select a fruit")));

        Assert.Equal("Custom", ValueText(cut));
    }

    [Fact(DisplayName = "children function takes precedence over placeholder")]
    [Trait("Requirement", "CMB-API-47")]
    public void Children_function_takes_precedence_over_placeholder()
    {
        IRenderedComponent<ComboboxHarness> cut = Render(
            new(),
            ("ValueContent", (RenderFragment<string>)(value => builder => builder.AddContent(0, value ?? "none"))),
            ("Placeholder", Text("Select a fruit")));

        Assert.Equal("none", ValueText(cut));
    }

    [Fact(DisplayName = "displays placeholder when multiple mode has empty array")]
    [Trait("Requirement", "CMB-API-47")]
    public void Displays_placeholder_when_multiple_mode_has_empty_array()
    {
        IRenderedComponent<ComboboxHarness> cut = Render(
            new() { ["Multiple"] = true, ["DefaultValues"] = (IReadOnlyList<string>)[] },
            ("Placeholder", Text("Select fruits")));

        Assert.Equal("Select fruits", ValueText(cut));
    }

    [Fact(DisplayName = "falls back to raw values when items are not provided")]
    [Trait("Requirement", "CMB-API-46")]
    public void Falls_back_to_raw_values_when_items_are_not_provided()
    {
        IRenderedComponent<ComboboxHarness> cut = Render(new() { ["Multiple"] = true, ["DefaultValues"] = (IReadOnlyList<string>)["apple", "cherry"] });

        Assert.Equal("apple, cherry", ValueText(cut));
    }

    [Fact(DisplayName = "uses custom itemToStringLabel function")]
    [Trait("Requirement", "CMB-API-46")]
    public void Uses_custom_itemToStringLabel_function()
    {
        IRenderedComponent<ComboboxHarness> cut = Render(new()
        {
            ["DefaultValue"] = "apple",
            ["ItemToStringLabel"] = (Func<string, string>)(value => value.ToUpperInvariant()),
        });

        Assert.Equal("APPLE", ValueText(cut));
    }

    [Fact(DisplayName = "updates the label when value changes with items array")]
    [Trait("Requirement", "CMB-API-46")]
    public async Task Updates_the_label_when_value_changes()
    {
        IRenderedComponent<ComboboxHarness> cut = Render(
            new() { ["DefaultOpen"] = true, ["Items"] = (IReadOnlyList<string>)s_fruits },
            ("Placeholder", Text("Select a fruit")));
        Assert.Equal("Select a fruit", ValueText(cut));

        await cut.InvokeAsync(() => cut.Find("[data-testid=item-banana]").Click());

        Assert.Equal("banana", ValueText(cut));
    }

    [Fact(DisplayName = "port: ValuesContent receives the selected values in multiple mode")]
    [Trait("Requirement", "CMB-API-46")]
    public void ValuesContent_receives_the_selected_values_in_multiple_mode()
    {
        IRenderedComponent<ComboboxHarness> cut = Render(
            new() { ["Multiple"] = true, ["DefaultValues"] = (IReadOnlyList<string>)["apple", "cherry"] },
            ("ValuesContent", (RenderFragment<IReadOnlyList<string>>)(values => builder => builder.AddContent(0, values.Count + ": " + string.Join('|', values)))));

        Assert.Equal("2: apple|cherry", ValueText(cut));
    }

    [Fact(DisplayName = "port: ValuesContent receives an empty list when nothing is selected")]
    [Trait("Requirement", "CMB-API-46")]
    public void ValuesContent_receives_an_empty_list_when_nothing_is_selected()
    {
        IRenderedComponent<ComboboxHarness> cut = Render(
            new() { ["Multiple"] = true, ["Values"] = null },
            ("ValuesContent", (RenderFragment<IReadOnlyList<string>>)(values => builder => builder.AddContent(0, "count " + values.Count))));

        Assert.Equal("count 0", ValueText(cut));
    }

    [Fact(DisplayName = "port: ValueContent on a multiple root throws")]
    [Trait("Requirement", "CMB-API-46")]
    public void ValueContent_on_a_multiple_root_throws()
    {
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => Render(
            new() { ["Multiple"] = true },
            ("ValueContent", (RenderFragment<string>)(value => builder => builder.AddContent(0, value)))));

        Assert.StartsWith("Blazix: ", error.Message, StringComparison.Ordinal);
        Assert.Contains("ValuesContent", error.Message, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "port: ValuesContent on a single root throws")]
    [Trait("Requirement", "CMB-API-46")]
    public void ValuesContent_on_a_single_root_throws()
    {
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => Render(
            new(),
            ("ValuesContent", (RenderFragment<IReadOnlyList<string>>)(values => builder => builder.AddContent(0, values.Count)))));

        Assert.StartsWith("Blazix: ", error.Message, StringComparison.Ordinal);
        Assert.Contains("ValueContent", error.Message, StringComparison.Ordinal);
    }

    private static RenderFragment Text(string text) => builder => builder.AddContent(0, text);

    private static string ValueText(IRenderedComponent<ComboboxHarness> cut) => cut.Find("[data-testid=value]").TextContent;

    private IRenderedComponent<ComboboxHarness> Render(Dictionary<string, object?> root, params (string Name, object? Value)[] value)
    {
        void Extra(RenderTreeBuilder builder)
        {
            builder.OpenElement(0, "span");
            builder.AddAttribute(1, "data-testid", "value");
            builder.OpenComponent<ComboboxValue<string>>(2);
            foreach ((string name, object? parameter) in value)
            {
                builder.AddComponentParameter(3, name, parameter);
            }

            builder.CloseComponent();
            builder.CloseElement();
        }

        return test.Context.Render<ComboboxHarness>(p => p.Add(x => x.RootParameters, root).Add(x => x.Extra, (RenderFragment)Extra));
    }
}

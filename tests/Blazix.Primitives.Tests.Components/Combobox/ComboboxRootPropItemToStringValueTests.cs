using AngleSharp.Dom;
using Bunit;

// Family tests use the project's root namespace.
namespace Blazix.Primitives.Tests.Components;

// ComboboxRoot.test.tsx's `prop: itemToStringValue` titles cited at L2, over object values (ComboboxTypedHarness).
// getByDisplayValue(code) is the one input whose value is the code.
/// <summary>Combobox's root at L2: ComboboxRoot.test.tsx's <c>prop: itemToStringValue</c> block.</summary>
public sealed class ComboboxRootPropItemToStringValueTests : IDisposable
{
    private static readonly Country[] s_items = [new("United States", "US"), new("Canada", "CA"), new("Australia", "AU")];

    private readonly ComboboxTestContext test = new();

    /// <inheritdoc />
    public void Dispose() => test.Dispose();

    [Fact(DisplayName = "uses itemToStringValue for form submission")]
    [Trait("Requirement", "CMB-API-33")]
    public void Uses_itemToStringValue_for_form_submission()
    {
        IRenderedComponent<ComboboxTypedHarness<Country>> cut = Render(new()
        {
            ["Name"] = "country",
            ["DefaultValue"] = s_items[0],
        });

        IElement hiddenInput = ByDisplayValue(cut, "US");
        Assert.Equal("INPUT", hiddenInput.TagName);
        Assert.Equal("country", hiddenInput.GetAttribute("name"));
    }

    [Fact(DisplayName = "uses itemToStringValue for multiple selection form submission")]
    [Trait("Requirement", "CMB-API-33")]
    [Trait("Requirement", "CMB-DOM-19")]
    public void Uses_itemToStringValue_for_multiple_selection_form_submission()
    {
        Country[] values = [s_items[0], s_items[1]];
        IRenderedComponent<ComboboxTypedHarness<Country>> cut = Render(new()
        {
            ["Name"] = "countries",
            ["Multiple"] = true,
            ["DefaultValues"] = (IReadOnlyList<Country>)values,
        });

        foreach (Country value in values)
        {
            IElement input = ByDisplayValue(cut, value.Code);
            Assert.Equal("INPUT", input.TagName);
            Assert.Equal("countries", input.GetAttribute("name"));
        }
    }

    private static IElement ByDisplayValue(IRenderedComponent<ComboboxTypedHarness<Country>> cut, string value) =>
        Assert.Single(cut.FindAll("input"), input => string.Equals(input.GetAttribute("value"), value, StringComparison.Ordinal));

    private IRenderedComponent<ComboboxTypedHarness<Country>> Render(Dictionary<string, object?> parameters)
    {
        parameters["Items"] = (IReadOnlyList<Country>)s_items;
        parameters["ItemToStringLabel"] = (Func<Country, string>)(item => item.Name);
        parameters["ItemToStringValue"] = (Func<Country, string>)(item => item.Code);
        return test.Context.Render<ComboboxTypedHarness<Country>>(p => p
            .Add(x => x.RootParameters, parameters)
            .Add(x => x.ItemText, item => item.Name));
    }

    private sealed record Country(string Name, string Code);
}

using Bunit;

// Family tests use the project's root namespace.
namespace Blazix.Primitives.Tests.Components;

// ComboboxRoot.test.tsx's `prop: itemToStringLabel` titles cited at L2, over object values (ComboboxTypedHarness).
/// <summary>Combobox's root at L2: ComboboxRoot.test.tsx's <c>prop: itemToStringLabel</c> block.</summary>
public sealed class ComboboxRootPropItemToStringLabelTests : IDisposable
{
    private static readonly Country[] s_items = [new("United States", "US"), new("Canada", "CA"), new("Australia", "AU")];

    private readonly ComboboxTestContext test = new();

    /// <inheritdoc />
    public void Dispose() => test.Dispose();

    [Fact(DisplayName = "uses itemToStringLabel for input value synchronization")]
    [Trait("Requirement", "CMB-API-32")]
    [Trait("Requirement", "CMB-STA-20")]
    public async Task Uses_itemToStringLabel_for_input_value_synchronization()
    {
        IRenderedComponent<ComboboxTypedHarness<Country>> cut = test.Context.Render<ComboboxTypedHarness<Country>>(p => p
            .Add(x => x.RootParameters, new Dictionary<string, object?>
            {
                ["Items"] = (IReadOnlyList<Country>)s_items,
                ["ItemToStringLabel"] = (Func<Country, string>)(item => item.Name),
                ["ItemToStringValue"] = (Func<Country, string>)(item => item.Code),
                ["DefaultOpen"] = true,
            })
            .Add(x => x.ItemText, item => item.Name));

        await cut.ClickOptionAsync("Canada");

        Assert.Equal("Canada", cut.InputText());
    }

    [Fact(DisplayName = "shows the label for a controlled object value not in items")]
    [Trait("Requirement", "CMB-API-32")]
    [Trait("Requirement", "CMB-EDGE-11")]
    public void Shows_the_label_for_a_controlled_object_value_not_in_items()
    {
        Country value = new("Japan", "JP");

        IRenderedComponent<ComboboxTypedHarness<Country>> cut = test.Context.Render<ComboboxTypedHarness<Country>>(p => p
            .Add(x => x.RootParameters, new Dictionary<string, object?>
            {
                ["Items"] = (IReadOnlyList<Country>)s_items,
                ["Value"] = value,
                ["ItemToStringLabel"] = (Func<Country, string>)(item => item.Name),
                ["ItemToStringValue"] = (Func<Country, string>)(item => item.Code),
            })
            .Add(x => x.WithList, false));

        Assert.Equal("Japan", cut.InputText());
    }

    private sealed record Country(string Name, string Code);
}

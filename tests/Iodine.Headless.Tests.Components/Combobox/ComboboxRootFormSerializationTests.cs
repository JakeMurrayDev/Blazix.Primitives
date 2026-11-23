using Bunit;

// Family tests use the project's root namespace.
namespace Blazix.Primitives.Tests.Components;

// CMB-API-33, DOM-18 and DOM-19 at L2 (ComboboxRoot.test.tsx, describe "Form > serialization for object values"): the hidden
// inputs carry an object value's form text. The port never reads an object's `value` member (ADR 16 rule 1; shared-utilities
// ALG-07 and I18N-13's port note): ItemToStringValue stands in for Base UI's default, so the first two titles pass the
// converter that reads the member Base UI reads.
/// <summary>ComboboxRoot's form serialization of object values at L2.</summary>
public sealed class ComboboxRootFormSerializationTests : IDisposable
{
    private static readonly CountryOption[] s_items = [new("US", "United States"), new("CA", "Canada"), new("AU", "Australia")];

    private readonly ComboboxTestContext test = new();

    /// <inheritdoc />
    public void Dispose() => test.Dispose();

    [Fact(DisplayName = "serializes {value,label} objects using their value field")]
    [Trait("Requirement", "CMB-API-33")]
    [Trait("Requirement", "CMB-DOM-18")]
    public void Serializes_value_label_objects_using_their_value_field()
    {
        IRenderedComponent<ComboboxTypedHarness<CountryOption>> cut = Render<CountryOption>(new()
        {
            ["Name"] = "country",
            ["Items"] = (IReadOnlyList<CountryOption>)s_items,
            ["ItemToStringLabel"] = (Func<CountryOption, string>)(item => item.Label),
            ["ItemToStringValue"] = (Func<CountryOption, string>)(item => item.Value),
            ["DefaultValue"] = s_items[1],
        });

        AngleSharp.Dom.IElement hidden = Assert.Single(cut.FindAll("input"), input => input.GetAttribute("value") == "CA");
        Assert.Equal("input", hidden.TagName.ToLowerInvariant());
        Assert.Equal("country", hidden.GetAttribute("name"));
    }

    [Fact(DisplayName = "serializes multiple {value,label} objects into multiple hidden inputs")]
    [Trait("Requirement", "CMB-DOM-19")]
    public void Serializes_multiple_value_label_objects_into_multiple_hidden_inputs()
    {
        CountryOption[] values = [s_items[0], s_items[2]];
        IRenderedComponent<ComboboxTypedHarness<CountryOption>> cut = Render<CountryOption>(new()
        {
            ["Name"] = "countries",
            ["Items"] = (IReadOnlyList<CountryOption>)s_items,
            ["ItemToStringLabel"] = (Func<CountryOption, string>)(item => item.Label),
            ["ItemToStringValue"] = (Func<CountryOption, string>)(item => item.Value),
            ["Multiple"] = true,
            ["DefaultValues"] = (IReadOnlyList<CountryOption>)values,
        });

        Assert.Equal(values.Select(item => item.Value), cut.FindAll("input[name=countries]").Select(input => input.GetAttribute("value")));
    }

    [Fact(DisplayName = "falls back to itemToStringValue when object lacks value")]
    [Trait("Requirement", "CMB-API-33")]
    public void Falls_back_to_itemToStringValue_when_object_lacks_value()
    {
        CodeItem[] items = [new("US", "United States"), new("CA", "Canada"), new("AU", "Australia")];
        IRenderedComponent<ComboboxTypedHarness<CodeItem>> cut = Render<CodeItem>(new()
        {
            ["Name"] = "country",
            ["Items"] = (IReadOnlyList<CodeItem>)items,
            ["ItemToStringLabel"] = (Func<CodeItem, string>)(item => item.Name),
            ["ItemToStringValue"] = (Func<CodeItem, string>)(item => item.Code),
            ["DefaultValue"] = items[0],
        });

        Assert.Equal("US", cut.Find("input[name=country]").GetAttribute("value"));
    }

    private IRenderedComponent<ComboboxTypedHarness<TValue>> Render<TValue>(Dictionary<string, object?> root) =>
        test.Context.Render<ComboboxTypedHarness<TValue>>(p => p.Add(x => x.RootParameters, root));

    private sealed record CountryOption(string Value, string Label);

    private sealed record CodeItem(string Code, string Name);
}

using System.Globalization;

namespace Blazix.Primitives.Tests.Radio;

// RAD-FRM-02, RAD-DOM-11, FFI-ALG-11 (radio.md's dated deviation note of 2026-10-23, ADR 08 Amendments 2026-10-22): the text a
// hidden radio input submits. CI runs under the invariant culture, so every title sets a culture whose decimal separator
// and casing differ (de-DE) to show the invariant culture is used regardless.
/// <summary>The form value of a radio's value, and its null test.</summary>
public sealed class RadioValuesTests : IDisposable
{
    private readonly CultureInfo previous = CultureInfo.CurrentCulture;

    public RadioValuesTests()
    {
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
    }

    /// <inheritdoc />
    public void Dispose() => CultureInfo.CurrentCulture = previous;

    [Fact(DisplayName = "port: a string is submitted as is, the empty string included")]
    [Trait("Requirement", "RAD-FRM-02")]
    [Trait("Requirement", "RAD-DOM-11")]
    public void Port_a_string_is_submitted_as_is()
    {
        Assert.Equal("a", RadioValues.Serialize("a"));
        Assert.Equal(string.Empty, RadioValues.Serialize(string.Empty));
    }

    [Fact(DisplayName = "port: a null value renders no value attribute")]
    [Trait("Requirement", "RAD-DOM-11")]
    [Trait("Requirement", "RAD-API-01")]
    public void Port_a_null_value_renders_no_value_attribute()
    {
        Assert.Null(RadioValues.Serialize<string?>(null));
        Assert.Null(RadioValues.Serialize<int?>(null));
        Assert.True(RadioValues.IsNull<string?>(null));
        Assert.True(RadioValues.IsNull<int?>(null));
        Assert.False(RadioValues.IsNull<int?>(0));
        Assert.False(RadioValues.IsNull(0));
        Assert.False(RadioValues.IsNull(string.Empty));
    }

    [Fact(DisplayName = "port: an int is submitted in the invariant culture")]
    [Trait("Requirement", "RAD-FRM-02")]
    public void Port_an_int_is_submitted_in_the_invariant_culture()
    {
        Assert.Equal("1234567", RadioValues.Serialize(1234567));
        Assert.Equal("-1", RadioValues.Serialize(-1));
        Assert.Equal("5", RadioValues.Serialize<int?>(5));
    }

    [Fact(DisplayName = "port: a double is submitted in the invariant culture")]
    [Trait("Requirement", "RAD-FRM-02")]
    public void Port_a_double_is_submitted_in_the_invariant_culture()
    {
        Assert.Equal("1.5", RadioValues.Serialize(1.5));
        Assert.Equal("1234.25", RadioValues.Serialize(1234.25));
        Assert.Equal("NaN", RadioValues.Serialize(double.NaN));
    }

    // FFI-ALG-11's dated deviation: JSON.stringify(true) is "true"; the port submits Boolean's text.
    [Fact(DisplayName = "port: a bool is submitted as its .NET text")]
    [Trait("Requirement", "RAD-FRM-02")]
    public void Port_a_bool_is_submitted_as_its_dotnet_text()
    {
        Assert.Equal("True", RadioValues.Serialize(true));
        Assert.Equal("False", RadioValues.Serialize(false));
    }

    [Fact(DisplayName = "port: an enum is submitted as its name")]
    [Trait("Requirement", "RAD-FRM-02")]
    public void Port_an_enum_is_submitted_as_its_name()
    {
        Assert.Equal("Pear", RadioValues.Serialize(Fruit.Pear));
    }

    private enum Fruit
    {
        Apple,
        Pear,
    }
}

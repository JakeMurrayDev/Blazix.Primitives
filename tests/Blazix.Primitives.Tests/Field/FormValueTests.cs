using System.Globalization;
using Blazix.Primitives.Utilities;

namespace Blazix.Primitives.Tests;

// RAD-FRM-02, RAD-DOM-11, FFI-ALG-11 (radio.md's dated deviation note of 2026-10-23, ADR 08 Amendments 2026-10-22): the text a
// hidden radio input submits. CI runs under the invariant culture, so every title sets a culture whose decimal separator
// and casing differ (de-DE) to show the invariant culture is used regardless.
/// <summary>The form value of a radio's value, and its null test.</summary>
public sealed class FormValueTests : IDisposable
{
    private readonly CultureInfo previous = CultureInfo.CurrentCulture;

    public FormValueTests()
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
        Assert.Equal("a", FormValue.Serialize("a"));
        Assert.Equal(string.Empty, FormValue.Serialize(string.Empty));
    }

    [Fact(DisplayName = "port: a null value renders no value attribute")]
    [Trait("Requirement", "RAD-DOM-11")]
    [Trait("Requirement", "RAD-API-01")]
    public void Port_a_null_value_renders_no_value_attribute()
    {
        Assert.Null(FormValue.Serialize<string?>(null));
        Assert.Null(FormValue.Serialize<int?>(null));
        Assert.True(ValueNull.IsNull<string?>(null));
        Assert.True(ValueNull.IsNull<int?>(null));
        Assert.False(ValueNull.IsNull<int?>(0));
        Assert.False(ValueNull.IsNull(0));
        Assert.False(ValueNull.IsNull(string.Empty));
    }

    [Fact(DisplayName = "port: an int is submitted in the invariant culture")]
    [Trait("Requirement", "RAD-FRM-02")]
    public void Port_an_int_is_submitted_in_the_invariant_culture()
    {
        Assert.Equal("1234567", FormValue.Serialize(1234567));
        Assert.Equal("-1", FormValue.Serialize(-1));
        Assert.Equal("5", FormValue.Serialize<int?>(5));
    }

    [Fact(DisplayName = "port: a double is submitted in the invariant culture")]
    [Trait("Requirement", "RAD-FRM-02")]
    public void Port_a_double_is_submitted_in_the_invariant_culture()
    {
        Assert.Equal("1.5", FormValue.Serialize(1.5));
        Assert.Equal("1234.25", FormValue.Serialize(1234.25));
        Assert.Equal("NaN", FormValue.Serialize(double.NaN));
    }

    // FFI-ALG-11's note of 2026-11-11 (CMB-API-68, settled by the orchestrator 2026-11-11): a bool is submitted as
    // JSON.stringify and String() give it, no longer as Boolean's .NET text.
    [Fact(DisplayName = "port: a bool is submitted as JavaScript's text")]
    [Trait("Requirement", "RAD-FRM-02")]
    [Trait("Requirement", "CMB-API-68")]
    public void Port_a_bool_is_submitted_as_javascripts_text()
    {
        Assert.Equal("true", FormValue.Serialize(true));
        Assert.Equal("false", FormValue.Serialize(false));
    }

    [Fact(DisplayName = "port: an enum is submitted as its name")]
    [Trait("Requirement", "RAD-FRM-02")]
    public void Port_an_enum_is_submitted_as_its_name()
    {
        Assert.Equal("Pear", FormValue.Serialize(Fruit.Pear));
    }

    // FFI-EDGE-05's port note (2026-11-04, M5 track 4): Base UI falls back to String(value) when JSON.stringify throws on a
    // cyclic structure or a BigInt. The port never serializes JSON, so a cyclic object and a BigInteger take their text.
    [Fact(DisplayName = "port: a cyclic object and a big integer are submitted as their text, so no value makes the serializer throw")]
    [Trait("Requirement", "FFI-EDGE-05")]
    [Trait("Requirement", "FFI-ALG-11")]
    public void Port_a_cyclic_object_and_a_big_integer_are_submitted_as_their_text()
    {
        Node cyclic = new();
        cyclic.Next = cyclic;

        Assert.Equal("node", FormValue.Serialize(cyclic));
        Assert.Equal("123456789012345678901234567890", FormValue.Serialize(System.Numerics.BigInteger.Parse("123456789012345678901234567890", CultureInfo.InvariantCulture)));
    }

    private enum Fruit
    {
        Apple,
        Pear,
    }

    private sealed class Node
    {
        public Node? Next { get; set; }

        public override string ToString() => "node";
    }
}

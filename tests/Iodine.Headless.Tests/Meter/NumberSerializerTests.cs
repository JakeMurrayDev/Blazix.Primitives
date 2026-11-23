using Blazix.Primitives.Utilities;

namespace Blazix.Primitives.Tests;

// UTL-DOM-37 (shared-utilities §4.12.1) as MTR-ALG-03 cites it: numbers written into attributes and inline styles follow
// ECMAScript Number::toString, which C# double.ToString() does not. The titles are the port's own; the cases are the ones
// the algorithm in UTL-DOM-37 names, plus the exponent thresholds the document warns about.
/// <summary>The ECMAScript number serialization of attribute and inline-style numbers.</summary>
public sealed class NumberSerializerTests
{
    [Theory(DisplayName = "serializes $value like ECMAScript Number::toString")]
    [Trait("Requirement", "UTL-DOM-37")]
    [Trait("Requirement", "MTR-ALG-03")]
    [InlineData(30.0, "30")]
    [InlineData(0.5, "0.5")]
    [InlineData(12.5, "12.5")]
    [InlineData(-12.5, "-12.5")]
    [InlineData(100.0 / 3.0, "33.333333333333336")]
    [InlineData(0.1 + 0.2, "0.30000000000000004")]
    [InlineData(4.35 * 100, "434.99999999999994")]
    [InlineData(0.000001, "0.000001")]
    [InlineData(1e-7, "1e-7")]
    [InlineData(1.5e-7, "1.5e-7")]
    [InlineData(1.2345e-10, "1.2345e-10")]
    [InlineData(1e15, "1000000000000000")]
    [InlineData(1e20, "100000000000000000000")]
    [InlineData(123456789012345680000.0, "123456789012345680000")]
    [InlineData(1e21, "1e+21")]
    [InlineData(1.5e21, "1.5e+21")]
    [InlineData(1.5e300, "1.5e+300")]
    [InlineData(double.MaxValue, "1.7976931348623157e+308")]
    [InlineData(double.Epsilon, "5e-324")]
    [InlineData(9007199254740992.0, "9007199254740992")]
    public void Serializes_like_ECMAScript_Number_toString(double value, string expected)
    {
        Assert.Equal(expected, NumberSerializer.Serialize(value));
    }

    [Fact(DisplayName = "serializes zero, negative zero, NaN and the infinities")]
    [Trait("Requirement", "UTL-DOM-37")]
    public void Serializes_zero_negative_zero_NaN_and_the_infinities()
    {
        Assert.Equal("0", NumberSerializer.Serialize(0));
        Assert.Equal("0", NumberSerializer.Serialize(-0.0));
        Assert.Equal("NaN", NumberSerializer.Serialize(double.NaN));
        Assert.Equal("Infinity", NumberSerializer.Serialize(double.PositiveInfinity));
        Assert.Equal("-Infinity", NumberSerializer.Serialize(double.NegativeInfinity));
    }

    [Fact(DisplayName = "writes the same text under every culture")]
    [Trait("Requirement", "UTL-DOM-37")]
    public void Writes_the_same_text_under_every_culture()
    {
        System.Globalization.CultureInfo previous = System.Globalization.CultureInfo.CurrentCulture;
        try
        {
            System.Globalization.CultureInfo.CurrentCulture = System.Globalization.CultureInfo.GetCultureInfo("de-DE");

            Assert.Equal("33.333333333333336", NumberSerializer.Serialize(100.0 / 3.0));
            Assert.Equal("-1.5e-7", NumberSerializer.Serialize(-1.5e-7));
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentCulture = previous;
        }
    }

    // ADR 16 rule 7: the common attribute values are small integers and short fractions, written without a scratch buffer.
    [Fact(DisplayName = "returns a small whole number as its digits")]
    [Trait("Requirement", "UTL-DOM-37")]
    public void Returns_a_small_whole_number_as_its_digits()
    {
        for (int i = -1000; i <= 1000; i++)
        {
            Assert.Equal(i.ToString(System.Globalization.CultureInfo.InvariantCulture), NumberSerializer.Serialize(i));
        }
    }
}

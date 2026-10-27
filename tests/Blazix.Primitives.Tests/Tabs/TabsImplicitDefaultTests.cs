namespace Blazix.Primitives.Tests.Tabs;

// TAB-API-02, TAB-STA-08; ADR 08 Amendments 2026-10-26 (chosen by the user 2026-10-26): Base UI's implicit
// `defaultValue` is the number 0, so while a Root has neither Value nor DefaultValue a Tab matches it only when TValue
// is a numeric primitive and the Tab's value is zero (JavaScript's `0 === value`). Design point 3 of the Tabs track
// (the helper's branches and its place in Tabs/) was settled by the orchestrator 2026-10-26.
/// <summary>The implicit default of a Root without <c>Value</c> or <c>DefaultValue</c>, per value type.</summary>
public sealed class TabsImplicitDefaultTests
{
    private enum Letter
    {
        A,
        B,
    }

    [Fact(DisplayName = "port: the integral zeros match the implicit default")]
    [Trait("Requirement", "TAB-API-02")]
    [Trait("Requirement", "TAB-STA-08")]
    public void Port_the_integral_zeros_match_the_implicit_default()
    {
        Assert.True(TabsImplicitDefault.Matches(0));
        Assert.True(TabsImplicitDefault.Matches(0L));
        Assert.True(TabsImplicitDefault.Matches((short)0));
        Assert.True(TabsImplicitDefault.Matches((sbyte)0));
        Assert.True(TabsImplicitDefault.Matches((byte)0));
        Assert.True(TabsImplicitDefault.Matches((ushort)0));
        Assert.True(TabsImplicitDefault.Matches(0U));
        Assert.True(TabsImplicitDefault.Matches(0UL));
        Assert.True(TabsImplicitDefault.Matches((nint)0));
        Assert.True(TabsImplicitDefault.Matches((nuint)0));
        Assert.False(TabsImplicitDefault.Matches(1));
        Assert.False(TabsImplicitDefault.Matches(-1L));
        Assert.False(TabsImplicitDefault.Matches((byte)2));
        Assert.False(TabsImplicitDefault.Matches(ulong.MaxValue));
    }

    [Fact(DisplayName = "port: the floating-point zeros match the implicit default, -0 included and NaN excluded")]
    [Trait("Requirement", "TAB-API-02")]
    [Trait("Requirement", "TAB-EDGE-07")]
    public void Port_the_floating_point_zeros_match_the_implicit_default()
    {
        Assert.True(TabsImplicitDefault.Matches(0.0));
        Assert.True(TabsImplicitDefault.Matches(-0.0));
        Assert.True(TabsImplicitDefault.Matches(0f));
        Assert.True(TabsImplicitDefault.Matches(-0f));
        Assert.True(TabsImplicitDefault.Matches(Half.Zero));
        Assert.True(TabsImplicitDefault.Matches(Half.NegativeZero));
        Assert.True(TabsImplicitDefault.Matches(0m));
        Assert.True(TabsImplicitDefault.Matches(-0.0m));
        Assert.False(TabsImplicitDefault.Matches(double.NaN));
        Assert.False(TabsImplicitDefault.Matches(float.NaN));
        Assert.False(TabsImplicitDefault.Matches(Half.NaN));
        Assert.False(TabsImplicitDefault.Matches(double.Epsilon));
        Assert.False(TabsImplicitDefault.Matches(0.5m));
    }

    [Fact(DisplayName = "port: a nullable numeric zero matches the implicit default and null does not")]
    [Trait("Requirement", "TAB-API-02")]
    public void Port_a_nullable_numeric_zero_matches_the_implicit_default()
    {
        Assert.True(TabsImplicitDefault.Matches<int?>(0));
        Assert.True(TabsImplicitDefault.Matches<long?>(0));
        Assert.True(TabsImplicitDefault.Matches<double?>(-0.0));
        Assert.True(TabsImplicitDefault.Matches<decimal?>(0m));
        Assert.True(TabsImplicitDefault.Matches<Half?>(Half.Zero));
        Assert.True(TabsImplicitDefault.Matches<nuint?>(0));
        Assert.False(TabsImplicitDefault.Matches<int?>(null));
        Assert.False(TabsImplicitDefault.Matches<double?>(double.NaN));
        Assert.False(TabsImplicitDefault.Matches<float?>(null));
    }

    [Fact(DisplayName = "port: enums, char, bool, strings and references never match the implicit default")]
    [Trait("Requirement", "TAB-API-02")]
    [Trait("Requirement", "TAB-API-07")]
    public void Port_non_numbers_never_match_the_implicit_default()
    {
        Assert.False(TabsImplicitDefault.Matches(Letter.A));
        Assert.False(TabsImplicitDefault.Matches<Letter?>(Letter.A));
        Assert.False(TabsImplicitDefault.Matches('\0'));
        Assert.False(TabsImplicitDefault.Matches(false));
        Assert.False(TabsImplicitDefault.Matches("0"));
        Assert.False(TabsImplicitDefault.Matches(string.Empty));
        Assert.False(TabsImplicitDefault.Matches<string?>(null));
        Assert.False(TabsImplicitDefault.Matches<object?>(0));
        Assert.False(TabsImplicitDefault.Matches(System.Numerics.BigInteger.Zero));
    }
}

// Family tests use the project's root namespace, never one named like a part.
namespace Iodine.Headless.Tests;

/// <summary>
/// <c>SnapPointValue</c> at L1 (ADR 08 Amendments 2026-11-18, decision 20 (a) and the probe's outcome): Base UI's
/// <c>Drawer.Root.SnapPoint = number | string</c>, compared by <c>Object.is</c> on the number and ordinally on the string,
/// never across the cases (DRW-STA-02's comparison, STA-16's <c>'1'</c> versus <c>1</c>).
/// </summary>
public sealed class SnapPointValueTests
{
    [Fact(DisplayName = "port: a snap point holds a number or a string through its implicit conversions, without boxing either")]
    [Trait("Requirement", "DRW-API-02")]
    public void A_snap_point_holds_a_number_or_a_string()
    {
        SnapPointValue number = 0.5;
        SnapPointValue text = "200px";

        Assert.True(number.HasValue);
        Assert.True(number.TryGetValue(out double fraction));
        Assert.Equal(0.5, fraction);
        Assert.False(number.TryGetValue(out string? _));

        Assert.True(text.TryGetValue(out string? pixels));
        Assert.Equal("200px", pixels);
        Assert.False(text.TryGetValue(out double _));

        Assert.False(default(SnapPointValue).HasValue);
        Assert.Null(default(SnapPointValue).Value);
        Assert.Equal(0.5, number.Value);
        Assert.Throws<ArgumentNullException>(() => new SnapPointValue(null!));
    }

    [Fact(DisplayName = "port: snap points compare by Object.is on the number and ordinally on the string, never across the cases")]
    [Trait("Requirement", "DRW-STA-02")]
    [Trait("Requirement", "DRW-STA-16")]
    public void Snap_points_compare_by_Object_is_and_ordinally()
    {
        Assert.Equal((SnapPointValue)1, (SnapPointValue)1.0);
        Assert.True((SnapPointValue)double.NaN == double.NaN);
        Assert.True((SnapPointValue)0.0 != -0.0);
        Assert.True((SnapPointValue)"1" != 1);
        Assert.True((SnapPointValue)"200px" == "200px");
        Assert.True((SnapPointValue)"200PX" != "200px");
        Assert.Equal(default, default(SnapPointValue));
        Assert.NotEqual(default, (SnapPointValue)0);
        Assert.Equal(((SnapPointValue)"10rem").GetHashCode(), ((SnapPointValue)"10rem").GetHashCode());
        Assert.Equal(((SnapPointValue)double.NaN).GetHashCode(), ((SnapPointValue)double.NaN).GetHashCode());
        Assert.True(((SnapPointValue)0.25).Equals((object)(SnapPointValue)0.25));
    }
}

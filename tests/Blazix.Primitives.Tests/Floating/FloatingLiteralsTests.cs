namespace Blazix.Primitives.Tests;

/// <summary>
/// L1: the enums of floating positioning (ADR 08 Amendments 2026-10-27) and the literals the <c>positioning</c> module
/// receives for them, which are Base UI's (<c>useAnchorPositioning.ts</c> <c>Side</c>, <c>Align</c>,
/// <c>positionMethod</c> and the collision-avoidance modes).
/// </summary>
public sealed class FloatingLiteralsTests
{
    [Fact(DisplayName = "Side, Align and PositionMethod name Base UI's literals")]
    public void Side_align_and_position_method_name_Base_UIs_literals()
    {
        Assert.Equal(["Top", "Bottom", "Left", "Right", "InlineEnd", "InlineStart"], Enum.GetNames<Side>());
        Assert.Equal(["Start", "Center", "End"], Enum.GetNames<Align>());
        Assert.Equal(["Absolute", "Fixed"], Enum.GetNames<PositionMethod>());

        Assert.Equal(
            ["top", "bottom", "left", "right", "inline-end", "inline-start"],
            Enum.GetValues<Side>().Select(FloatingLiterals.ToLiteral));
        Assert.Equal(["start", "center", "end"], Enum.GetValues<Align>().Select(FloatingLiterals.ToLiteral));
        Assert.Equal(["absolute", "fixed"], Enum.GetValues<PositionMethod>().Select(FloatingLiterals.ToLiteral));
    }

    [Fact(DisplayName = "the collision-avoidance modes name Base UI's literals, none included")]
    public void The_collision_avoidance_modes_name_Base_UIs_literals()
    {
        Assert.Equal(["flip", "shift", "none"], Enum.GetValues<CollisionAvoidanceMode>().Select(FloatingLiterals.ToLiteral));
        Assert.Equal(["start", "end", "none"], Enum.GetValues<FallbackAxisSide>().Select(FloatingLiterals.ToLiteral));
    }

    [Fact(DisplayName = "a value outside an enum is reported with the Blazix prefix")]
    public void A_value_outside_an_enum_is_reported_with_the_Blazix_prefix()
    {
        ArgumentOutOfRangeException side = Assert.Throws<ArgumentOutOfRangeException>(() => FloatingLiterals.ToLiteral((Side)42));
        ArgumentOutOfRangeException align = Assert.Throws<ArgumentOutOfRangeException>(() => FloatingLiterals.ToLiteral((Align)42));

        Assert.StartsWith("Blazix: `42` is not a member of `Side`.", side.Message, StringComparison.Ordinal);
        Assert.StartsWith("Blazix: `42` is not a member of `Align`.", align.Message, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "a collision avoidance compares by value and leaves every axis to the default when empty")]
    public void A_collision_avoidance_compares_by_value()
    {
        CollisionAvoidance empty = new();
        CollisionAvoidance none = new(FallbackAxisSide: FallbackAxisSide.None);

        Assert.Null(empty.Side);
        Assert.Null(empty.Align);
        Assert.Null(empty.FallbackAxisSide);
        Assert.Equal(new CollisionAvoidance(FallbackAxisSide: FallbackAxisSide.None), none);
        Assert.NotEqual(empty, none);
        Assert.Equal(new CollisionAvoidance(CollisionAvoidanceMode.Shift, CollisionAvoidanceMode.None), new CollisionAvoidance(CollisionAvoidanceMode.Shift, CollisionAvoidanceMode.None));
    }
}

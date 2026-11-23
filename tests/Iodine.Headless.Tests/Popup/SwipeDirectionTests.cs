// Family tests use the project's root namespace.
namespace Blazix.Primitives.Tests;

/// <summary>
/// <see cref="SwipeDirection"/> at L1: Base UI's <c>'up' | 'down' | 'left' | 'right'</c> (PSH §4.7.5; ADR 08 Amendments
/// 2026-11-18, the Swipe line; physical, never mirrored, PSH-I18N-02), shared by Drawer and Toast, and its one literal
/// mapping.
/// </summary>
public sealed class SwipeDirectionTests
{
    [Fact(DisplayName = "port: names the four physical swipe directions in the literal set's order")]
    public void Names_the_four_physical_swipe_directions_in_the_literal_set_s_order()
    {
        Assert.Equal(["Up", "Down", "Left", "Right"], Enum.GetNames<SwipeDirection>());
    }

    [Theory(DisplayName = "port: maps each direction to its literal and back")]
    [InlineData(SwipeDirection.Up, "up")]
    [InlineData(SwipeDirection.Down, "down")]
    [InlineData(SwipeDirection.Left, "left")]
    [InlineData(SwipeDirection.Right, "right")]
    public void Maps_each_direction_to_its_literal_and_back(SwipeDirection direction, string literal)
    {
        Assert.Equal(literal, SwipeDirectionLiterals.ToLiteral(direction));
        Assert.Equal(direction, SwipeDirectionLiterals.FromLiteral(literal));
    }

    [Fact(DisplayName = "port: a value outside the enum throws, and a literal outside the set maps to no direction")]
    public void A_value_outside_the_enum_throws_and_a_literal_outside_the_set_maps_to_no_direction()
    {
        Assert.Equal("up", SwipeDirectionLiterals.ToLiteral(SwipeDirection.Up));
        Assert.Throws<ArgumentOutOfRangeException>(() => SwipeDirectionLiterals.ToLiteral((SwipeDirection)4));
        Assert.Equal(SwipeDirection.Left, SwipeDirectionLiterals.FromLiteral("left"));
        Assert.Null(SwipeDirectionLiterals.FromLiteral("Up"));
        Assert.Null(SwipeDirectionLiterals.FromLiteral(null));
    }
}

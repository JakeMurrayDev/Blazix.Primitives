namespace Blazix.Primitives.Tests.Tabs;

// TAB-DOM-02, TAB-DOM-07, TAB-STA-15, TAB-API-04: the literals of Base UI's `Tabs.Tab.ActivationDirection` ('left' | 'right' |
// 'up' | 'down' | 'none'), which the `tabs` module reports and C# renders under the handover, and of the orientation the
// List passes to the composite (ADR 08 Amendments 2026-10-26: non-nullable, never 'both').
/// <summary>The literals of the activation direction and the orientation.</summary>
public sealed class TabsLiteralsTests
{
    [Theory(DisplayName = "port: every activation direction has its literal, and the literal parses back")]
    [Trait("Requirement", "TAB-DOM-02")]
    [Trait("Requirement", "TAB-STA-15")]
    [InlineData(TabsActivationDirection.Left, "left")]
    [InlineData(TabsActivationDirection.Right, "right")]
    [InlineData(TabsActivationDirection.Up, "up")]
    [InlineData(TabsActivationDirection.Down, "down")]
    [InlineData(TabsActivationDirection.None, "none")]
    public void Port_every_activation_direction_has_its_literal(TabsActivationDirection direction, string literal)
    {
        Assert.Equal(literal, TabsLiterals.ToLiteral(direction));
        Assert.Equal(direction, TabsLiterals.ParseDirection(literal));
    }

    [Fact(DisplayName = "port: an unknown or missing direction literal reads as none")]
    [Trait("Requirement", "TAB-STA-16")]
    public void Port_an_unknown_direction_literal_reads_as_none()
    {
        Assert.Equal(TabsActivationDirection.None, TabsLiterals.ParseDirection(null));
        Assert.Equal(TabsActivationDirection.None, TabsLiterals.ParseDirection(string.Empty));
        Assert.Equal(TabsActivationDirection.None, TabsLiterals.ParseDirection("Left"));
        Assert.Equal(TabsActivationDirection.None, TabsLiterals.ParseDirection("sideways"));
    }

    [Fact(DisplayName = "port: an undefined activation direction has no literal")]
    [Trait("Requirement", "TAB-DOM-02")]
    public void Port_an_undefined_activation_direction_has_no_literal()
    {
        ArgumentOutOfRangeException error = Assert.Throws<ArgumentOutOfRangeException>(() => TabsLiterals.ToLiteral((TabsActivationDirection)42));
        Assert.StartsWith("Blazix: ", error.Message, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "port: the two orientations reach the composite as horizontal and vertical")]
    [Trait("Requirement", "TAB-API-04")]
    [Trait("Requirement", "TAB-KEY-01")]
    public void Port_the_two_orientations_reach_the_composite()
    {
        Assert.Equal("horizontal", CompositeLiterals.ToLiteral(Orientation.Horizontal));
        Assert.Equal("vertical", CompositeLiterals.ToLiteral(Orientation.Vertical));
    }
}

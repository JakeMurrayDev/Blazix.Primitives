// Family tests use the project's root namespace.
namespace Blazix.Primitives.Tests;

/// <summary>
/// <see cref="InteractionType"/> at L1: Base UI's <c>'mouse' | 'touch' | 'pen' | 'keyboard' | ''</c> (PSH §2, the open
/// interaction type; ADR 08 Amendments 2026-10-27), with <see cref="InteractionType.Unknown"/> for the empty literal, and
/// the one mapping from the literals the <c>button</c> module reports.
/// </summary>
public sealed class InteractionTypeTests
{
    [Fact(DisplayName = "names the five interaction types, Unknown standing for the empty literal")]
    public void Names_the_five_interaction_types_Unknown_standing_for_the_empty_literal()
    {
        Assert.Equal(["Mouse", "Touch", "Pen", "Keyboard", "Unknown"], Enum.GetNames<InteractionType>());
    }

    [Theory(DisplayName = "maps each literal the button module reports to its member and back")]
    [InlineData("mouse", InteractionType.Mouse)]
    [InlineData("touch", InteractionType.Touch)]
    [InlineData("pen", InteractionType.Pen)]
    [InlineData("keyboard", InteractionType.Keyboard)]
    [InlineData("", InteractionType.Unknown)]
    public void Maps_each_literal_the_button_module_reports_to_its_member_and_back(string literal, InteractionType type)
    {
        Assert.Equal(type, InteractionTypeLiterals.FromLiteral(literal));
        Assert.Equal(literal, InteractionTypeLiterals.ToLiteral(type));
    }

    [Fact(DisplayName = "a literal outside the set and a missing one map to Unknown, as an unrecognized pointer type does")]
    public void A_literal_outside_the_set_and_a_missing_one_map_to_Unknown_as_an_unrecognized_pointer_type_does()
    {
        Assert.Equal(InteractionType.Mouse, InteractionTypeLiterals.FromLiteral("mouse"));
        Assert.Equal(InteractionType.Unknown, InteractionTypeLiterals.FromLiteral("eraser"));
        Assert.Equal(InteractionType.Unknown, InteractionTypeLiterals.FromLiteral(null));
    }
}

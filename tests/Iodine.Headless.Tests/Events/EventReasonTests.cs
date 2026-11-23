using System.Text.RegularExpressions;
using Blazix.Primitives.Tests.TestSupport;

namespace Blazix.Primitives.Tests.Events;

/// <summary>
/// The reason enum against its oracle, the table of EVD §3.5 read from <c>docs/architecture/event-details-and-reasons.md</c>
/// at test time (ADR 05 §3 as named by ADR 08 §4): 36 members, in table order, each the PascalCase of its literal and each
/// <see cref="EventReasonExtensions.ToLiteral"/> equal to the literal.
/// </summary>
public sealed partial class EventReasonTests
{
    [Fact(DisplayName = "EventReason has the 36 members of EVD §3.5")]
    [Trait("Requirement", "EVD-API-03")]
    public void EventReason_has_the_36_members_of_EVD_3_5()
    {
        Assert.Equal(36, Enum.GetValues<EventReason>().Length);
        Assert.Equal(36, LiteralsFromDocument().Count);
    }

    [Fact(DisplayName = "every member is the PascalCase form of its literal, in table order")]
    [Trait("Requirement", "EVD-API-03")]
    public void Every_member_is_the_PascalCase_form_of_its_literal_in_table_order()
    {
        string[] expected = LiteralsFromDocument().Select(PascalCase).ToArray();

        Assert.Equal(expected, Enum.GetNames<EventReason>());
    }

    [Fact(DisplayName = "ToLiteral() returns the literal of EVD §3.5 for every member")]
    [Trait("Requirement", "EVD-API-03")]
    public void ToLiteral_returns_the_literal_of_EVD_3_5_for_every_member()
    {
        IReadOnlyList<string> literals = LiteralsFromDocument();
        EventReason[] members = Enum.GetValues<EventReason>();

        Assert.Equal(literals, members.Select(member => member.ToLiteral()).ToArray());
    }

    [Fact(DisplayName = "ToLiteral() rejects a value outside the enum")]
    public void ToLiteral_rejects_a_value_outside_the_enum()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ((EventReason)36).ToLiteral());
    }

    private static IReadOnlyList<string> LiteralsFromDocument()
    {
        string path = Path.Combine(RepositoryRoot.Find(), "docs", "architecture", "event-details-and-reasons.md");
        string[] lines = File.ReadAllLines(path);
        int header = Array.FindIndex(lines, line => line.StartsWith("| Literal | Constant |", StringComparison.Ordinal));
        Assert.True(header >= 0, "EVD §3.5's table was not found.");

        List<string> literals = [];
        for (int i = header + 2; i < lines.Length && lines[i].StartsWith('|'); i++)
        {
            literals.Add(FirstCell().Match(lines[i]).Groups["literal"].Value);
        }

        return literals;
    }

    private static string PascalCase(string literal) =>
        string.Concat(literal.Split('-').Select(part => char.ToUpperInvariant(part[0]) + part[1..]));

    [GeneratedRegex(@"^\| `(?<literal>[a-z-]+)` \|")]
    private static partial Regex FirstCell();
}

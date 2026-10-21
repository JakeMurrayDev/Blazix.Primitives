using System.Text.Json;
using Blazix.Primitives.Interop;
using Blazix.Primitives.Tests.TestSupport;

namespace Blazix.Primitives.Tests.Interop;

/// <summary>
/// L1: the C# half of the transition status machine (TRN-ALG-01). The evaluation step is checked against the case
/// table the L3 test of the <c>transitions</c> module reads, so the two languages cannot drift apart.
/// </summary>
public sealed class TransitionStatusRulesTests
{
    public static TheoryData<string> CaseNames()
    {
        TheoryData<string> names = [];
        foreach (JsonElement entry in ReadCases())
        {
            names.Add(entry.GetProperty("name").GetString()!);
        }

        return names;
    }

    [Theory]
    [MemberData(nameof(CaseNames))]
    [Trait("Requirement", "TRN-ALG-01")]
    [Trait("Requirement", "TRN-STA-03")]
    [Trait("Requirement", "TRN-STA-06")]
    [Trait("Requirement", "TRN-STA-08")]
    public void The_evaluation_step_matches_the_shared_case_table(string name)
    {
        JsonElement entry = ReadCases().Single(e => e.GetProperty("name").GetString() == name);
        JsonElement expected = entry.GetProperty("expected");

        (bool mounted, TransitionStatus? status) = TransitionStatusRules.Evaluate(
            entry.GetProperty("open").GetBoolean(),
            entry.GetProperty("mounted").GetBoolean(),
            Status(entry.GetProperty("status")),
            entry.GetProperty("deferEndingState").GetBoolean());

        Assert.Equal(expected.GetProperty("mounted").GetBoolean(), mounted);
        Assert.Equal(Status(expected.GetProperty("status")), status);
    }

    [Theory]
    [Trait("Requirement", "TRN-STA-02")]
    [Trait("Requirement", "TRN-STA-12")]
    [Trait("Requirement", "TRN-STA-13")]
    [InlineData(false, false, false, false, null)]
    [InlineData(false, true, true, false, null)]
    [InlineData(true, false, false, true, null)]
    [InlineData(true, true, false, true, TransitionStatus.Idle)]
    [InlineData(true, false, true, true, TransitionStatus.Starting)]
    [InlineData(true, true, true, true, TransitionStatus.Starting)]
    public void The_initial_values_follow_open_and_the_machine_options(bool open, bool enableIdleState, bool animateInitialOpen, bool expectedMounted, TransitionStatus? expectedStatus)
    {
        (bool mounted, TransitionStatus? status) = TransitionStatusRules.Initial(open, enableIdleState, animateInitialOpen);

        Assert.Equal(expectedMounted, mounted);
        Assert.Equal(expectedStatus, status);
    }

    private static JsonElement[] ReadCases()
    {
        string path = Path.Combine(RepositoryRoot.Find(), "src", "Blazix.Primitives", "js", "test-support", "transition-evaluation.cases.json");
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
        return [.. document.RootElement.GetProperty("cases").EnumerateArray().Select(e => e.Clone())];
    }

    private static TransitionStatus? Status(JsonElement literal) => literal.ValueKind switch
    {
        JsonValueKind.Null => null,
        _ => literal.GetString() switch
        {
            "starting" => TransitionStatus.Starting,
            "ending" => TransitionStatus.Ending,
            "idle" => TransitionStatus.Idle,
            string other => throw new InvalidOperationException($"Unknown status literal '{other}'."),
            null => null,
        },
    };
}

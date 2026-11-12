using Bunit;

// Family tests use the project's root namespace.
namespace Blazix.Primitives.Tests.Components;

// ADR 16 rule 7 for Autocomplete (M5 track 11; the brief's L2 plan, PR 245's final review fix 1): the least of five runs
// after a warm-up, around bUnit's synchronous render on the test thread: an unchanged Root re-render sends nothing to any
// module and stays within its budget.
/// <summary>Autocomplete's allocation budget at L2.</summary>
[Collection(AllocationBudgetsCollection.Name)]
public sealed class AutocompleteAllocationBudgetTests : IDisposable
{
    // First measurement 2026-11-12 (arm64, .NET 11, Release; M5 track 11): 102568 bytes in each of three runs (the root, its parts and 100 items), plus the CI runner's +112 bytes floor
    // shift (InteractionsAllocationBudgetTests, 2026-10-29). It only tightens.
    private const long RerenderBudget = 102680;

    private readonly ComboboxTestContext test = AutocompleteTesting.CreateContext();

    /// <inheritdoc />
    public void Dispose() => test.Dispose();

    [Fact(DisplayName = "port: an unchanged Autocomplete root re-render sends nothing and stays within its budget")]
    [Trait("Requirement", "ADR16-7")]
    public void An_unchanged_root_rerender_sends_nothing_and_stays_within_its_budget()
    {
        IReadOnlyList<string> items = [.. Enumerable.Range(0, 100).Select(i => "v" + i.ToString(System.Globalization.CultureInfo.InvariantCulture))];
        IRenderedComponent<AutocompleteHarness> cut = test.Context.Render<AutocompleteHarness>(p => p.Add(x => x.RootParameters, new Dictionary<string, object?> { ["Items"] = items, ["DefaultOpen"] = true }));
        for (int i = 0; i < 20; i++)
        {
            cut.Render();
        }

        int updatesBefore = Updates();
        long rerender = AllocationSampler.LeastOfFive(() => cut.Render());

        Assert.Equal(updatesBefore, Updates());
        Assert.True(rerender <= RerenderBudget, $"a re-render allocated {rerender} bytes; the budget is {RerenderBudget}");
    }

    private int Updates() =>
        test.ComboboxHandle.Invocations["update"].Count + test.CompositeHandle.Invocations["update"].Count
        + test.InteractionsHandle.Invocations["update"].Count + test.FormHandle.Invocations["update"].Count;
}

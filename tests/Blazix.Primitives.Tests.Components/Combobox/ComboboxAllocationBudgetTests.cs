using Bunit;

// Family tests use the project's root namespace.
namespace Blazix.Primitives.Tests.Components;

// ADR 16 rule 7 for Combobox (M5 track 10; the brief's L2 plan): a re-render of an open root with unchanged parameters over
// 1,000 items sends nothing to any module, and its allocation stays within its budget, the least of five runs after a
// warm-up, measured around bUnit's synchronous render on the test thread.
/// <summary>Combobox's allocation budget at L2.</summary>
[Collection(AllocationBudgetsCollection.Name)]
public sealed class ComboboxAllocationBudgetTests : IDisposable
{
    private const int Count = 1000;

    // First measurement 2026-11-11 (arm64, .NET 11, Release; M5 track 10): the envelope of five runs of this class, each the
    // least of five, plus the CI runner's +112 bytes floor shift (InteractionsAllocationBudgetTests, 2026-10-29). The
    // re-render is the root and its 1,000 items with their text (878432 bytes in each of the five). It only tightens.
    private const long RerenderBudget = 878544;

    private readonly ComboboxTestContext test = new();

    /// <inheritdoc />
    public void Dispose() => test.Dispose();

    [Fact(DisplayName = "port: at 1,000 items, an unchanged root re-render sends nothing and stays within its budget")]
    [Trait("Requirement", "ADR16-7")]
    public void An_unchanged_root_rerender_sends_nothing_and_stays_within_its_budget()
    {
        IReadOnlyList<string> items = [.. Enumerable.Range(0, Count).Select(i => "v" + i.ToString(System.Globalization.CultureInfo.InvariantCulture))];
        IRenderedComponent<ComboboxHarness> cut = test.Context.Render<ComboboxHarness>(p => p
            .Add(x => x.RootParameters, new Dictionary<string, object?> { ["Items"] = items, ["DefaultOpen"] = true }));
        Assert.Equal(Count, cut.FindAll("[role=option]").Count);

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

namespace Blazix.Primitives.Tests;

/// <summary>
/// ADR 16 rule 7 for ToggleGroup at L1: the next value of one toggle activation, which builds the new list a commit
/// produces (toggle-group §4, TGG-EVT-05). Measured on the test thread; the budgets were taken from the first measurement
/// and only tighten afterwards.
/// </summary>
[Collection(AllocationBudgetsCollection.Name)]
public sealed class ToggleGroupAllocationBudgetTests
{
    // First measurement 2026-10-26 (arm64, .NET 11 RC2, Release): the new array and nothing else.
    private const long MultipleBudgetBytes = 72;
    private const long SingleBudgetBytes = 32;

    [Fact(DisplayName = "a multi-select activation's next value stays within its budget")]
    [Trait("Requirement", "ADR16-7")]
    public void A_multi_select_activations_next_value_stays_within_its_budget()
    {
        IReadOnlyList<string> value = ["a", "b", "c", "d", "e"];
        for (int i = 0; i < 20; i++)
        {
            _ = ToggleGroupValue.Next(value, "f", nextPressed: true, multiple: true);
        }

        long before = GC.GetAllocatedBytesForCurrentThread();
        IReadOnlyList<string> next = ToggleGroupValue.Next(value, "f", nextPressed: true, multiple: true);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Equal(6, next.Count);
        Assert.True(allocated <= MultipleBudgetBytes, $"an activation allocated {allocated} bytes; the budget is {MultipleBudgetBytes}");
    }

    [Fact(DisplayName = "a single-select activation's next value stays within its budget")]
    [Trait("Requirement", "ADR16-7")]
    public void A_single_select_activations_next_value_stays_within_its_budget()
    {
        IReadOnlyList<string> value = ["a"];
        for (int i = 0; i < 20; i++)
        {
            _ = ToggleGroupValue.Next(value, "b", nextPressed: true, multiple: false);
        }

        long before = GC.GetAllocatedBytesForCurrentThread();
        IReadOnlyList<string> next = ToggleGroupValue.Next(value, "b", nextPressed: true, multiple: false);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Single(next);
        Assert.True(allocated <= SingleBudgetBytes, $"an activation allocated {allocated} bytes; the budget is {SingleBudgetBytes}");
    }
}

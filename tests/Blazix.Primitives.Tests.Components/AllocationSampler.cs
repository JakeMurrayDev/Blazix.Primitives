namespace Blazix.Primitives.Tests.Components;

// ADR 16 rule 7; PR 127's rule, settled by the orchestrator 2026-10-28 (M4 track 8's shared-gaps PR): every allocation
// budget is measured as the least of five runs of its operation, after the warm-up, in the non-parallel AllocationBudgets
// collection. One run may land on a growth of bUnit's or the runtime's own lists, the process-state floor the Positioner
// and Tabs budgets record, so no budget samples once.
/// <summary>Measures the bytes an operation allocates on the test thread, as the least of five runs.</summary>
internal static class AllocationSampler
{
    /// <summary>The number of runs a budget takes the least of.</summary>
    public const int Runs = 5;

    /// <summary>Runs <paramref name="operation"/> five times and returns the fewest bytes one run allocated.</summary>
    /// <param name="operation">The operation, which must leave the state as it found it.</param>
    /// <returns>The least of the five measurements.</returns>
    public static long LeastOfFive(Action operation) => LeastOfFive(_ => operation());

    /// <summary>Runs <paramref name="operation"/> once per run index and returns the fewest bytes one run allocated.</summary>
    /// <param name="operation">The operation, given the run's index (0 to 4), for operations that need a fresh input each run.</param>
    /// <returns>The least of the five measurements.</returns>
    public static long LeastOfFive(Action<int> operation)
    {
        long least = long.MaxValue;
        for (int run = 0; run < Runs; run++)
        {
            long before = GC.GetAllocatedBytesForCurrentThread();
            operation(run);
            least = Math.Min(least, GC.GetAllocatedBytesForCurrentThread() - before);
        }

        return least;
    }
}

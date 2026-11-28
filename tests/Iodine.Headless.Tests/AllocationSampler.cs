namespace Iodine.Headless.Tests;

// ADR 16 Amendments 2026-11-19 (issue 304; chosen by the user 2026-11-19, "256 B tolerance"): the L1 project's
// counterpart of the L2 project's AllocationSampler.AssertWithinBudget, with the same tolerance and message. The largest
// overage the Linux runners showed over a macOS pin was +208 B; the pin stays the measured figure and only tightens.
/// <summary>The shared measurement and assertion of the L1 project's allocation budgets.</summary>
internal static class AllocationSampler
{
    /// <summary>The number of runs a budget takes the least of.</summary>
    public const int Runs = 5;

    /// <summary>The bytes a budget's figure may exceed its pin by and still pass.</summary>
    public const long ToleranceBytes = 256;

    // ADR 16 rule 7 (the least of five runs; issue 337): a single sample can catch a one-off allocation on the test thread
    // during the loop, such as the runtime recompiling the measured path at a higher tier, which is not a cost of the
    // operation. The L2 project's sampler measures the same way.
    /// <summary>Runs <paramref name="operation"/> five times and returns the fewest bytes one run allocated on the current thread.</summary>
    /// <param name="operation">The operation, which must leave the state as it found it.</param>
    /// <returns>The least of the five measurements.</returns>
    public static long LeastOfFive(Action operation)
    {
        long least = long.MaxValue;
        for (int run = 0; run < Runs; run++)
        {
            long before = GC.GetAllocatedBytesForCurrentThread();
            operation();
            least = Math.Min(least, GC.GetAllocatedBytesForCurrentThread() - before);
        }

        return least;
    }

    /// <summary>Asserts that a measured figure is within its budget: at most the pin plus <see cref="ToleranceBytes"/>, or exactly 0 for a pin of 0.</summary>
    /// <param name="figure">The measured bytes, the least of five runs.</param>
    /// <param name="pin">The budget's pin, the figure measured when it was set.</param>
    /// <param name="what">What was measured, the start of the failure message, for example "a store notification".</param>
    public static void AssertWithinBudget(long figure, long pin, string what)
    {
        // Zero pins, chosen by the user 2026-11-19 (issue 304): a title pinned at 0 asserts that a path allocates nothing.
        long tolerance = pin == 0 ? 0 : ToleranceBytes;
        long limit = pin + tolerance;
        Assert.True(figure <= limit, $"{what} allocated {figure} bytes; the pin is {pin} bytes and the tolerance {tolerance} bytes (at most {limit})");
    }
}

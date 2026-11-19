namespace Blazix.Primitives.Tests;

// ADR 16 Amendments 2026-11-19 (issue 304; chosen by the user 2026-11-19, "256 B tolerance"): the L1 project's
// counterpart of the L2 project's AllocationSampler.AssertWithinBudget, with the same tolerance and message. The largest
// overage the Linux runners showed over a macOS pin was +208 B; the pin stays the measured figure and only tightens.
/// <summary>The shared assertion of the L1 project's allocation budgets.</summary>
internal static class AllocationSampler
{
    /// <summary>The bytes a budget's figure may exceed its pin by and still pass.</summary>
    public const long ToleranceBytes = 256;

    /// <summary>Asserts that a measured figure is within its budget: at most the pin plus <see cref="ToleranceBytes"/>, or exactly 0 for a pin of 0.</summary>
    /// <param name="figure">The measured bytes.</param>
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

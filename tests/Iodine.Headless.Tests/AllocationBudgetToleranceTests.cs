using Xunit.Sdk;

namespace Blazix.Primitives.Tests;

// ADR 16 Amendments 2026-11-19 (issue 304; chosen by the user 2026-11-19, "256 B tolerance"): a budget passes when its
// figure is at most its pin plus 256 B; the failure message prints the figure, the pin and the tolerance.
// The fix is the test host's AllocationSampler, not src/, so these pass against the base's src/ and carry the coverage
// marker (ADR 11 Amendments 2026-11-19, fixture-side fixes); their reds are against the old sampler.
/// <summary>The shared budget assertion and its tolerance.</summary>
public sealed class AllocationBudgetToleranceTests
{
    [Fact(DisplayName = "a figure up to the pin plus the 256 B tolerance passes the budget")]
    [Trait("Requirement", "ADR16-7")]
    [Trait("Coverage", "issue-304")]
    public void A_figure_up_to_the_pin_plus_the_tolerance_passes()
    {
        Assert.Equal(256, AllocationSampler.ToleranceBytes);

        AllocationSampler.AssertWithinBudget(2552, 2552, "a hold tick");
        AllocationSampler.AssertWithinBudget(2553, 2552, "a hold tick");
        AllocationSampler.AssertWithinBudget(2808, 2552, "a hold tick");
    }

    [Fact(DisplayName = "a figure over the pin plus the tolerance fails with the figure, the pin and the tolerance")]
    [Trait("Requirement", "ADR16-7")]
    [Trait("Coverage", "issue-304")]
    public void A_figure_over_the_pin_plus_the_tolerance_fails()
    {
        TrueException error = Assert.Throws<TrueException>(() => AllocationSampler.AssertWithinBudget(2809, 2552, "a hold tick"));

        Assert.StartsWith("a hold tick allocated 2809 bytes; the pin is 2552 bytes and the tolerance 256 bytes (at most 2808)", error.Message, StringComparison.Ordinal);
    }

    // Zero pins, chosen by the user 2026-11-19 (issue 304): a title pinned at 0 asserts that a path allocates nothing.
    [Fact(DisplayName = "a pin of 0 B stays exact: 0 bytes pass and 1 byte fails")]
    [Trait("Requirement", "ADR16-7")]
    [Trait("Coverage", "issue-304")]
    public void A_zero_pin_stays_exact()
    {
        AllocationSampler.AssertWithinBudget(0, 0, "a notification");

        TrueException error = Assert.Throws<TrueException>(() => AllocationSampler.AssertWithinBudget(1, 0, "a notification"));

        Assert.StartsWith("a notification allocated 1 bytes; the pin is 0 bytes and the tolerance 0 bytes (at most 0)", error.Message, StringComparison.Ordinal);
    }
}

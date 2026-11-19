using Xunit.Sdk;

namespace Blazix.Primitives.Tests;

// ADR 16 Amendments 2026-11-19 (issue 304; chosen by the user 2026-11-19, "256 B tolerance"): a budget passes when its
// figure is at most its pin plus 256 B; the failure message prints the figure, the pin and the tolerance.
/// <summary>The shared budget assertion and its tolerance.</summary>
public sealed class AllocationBudgetToleranceTests
{
    [Fact(DisplayName = "a figure up to the pin plus the 256 B tolerance passes the budget")]
    [Trait("Requirement", "ADR16-7")]
    public void A_figure_up_to_the_pin_plus_the_tolerance_passes()
    {
        Assert.Equal(256, AllocationSampler.ToleranceBytes);

        AllocationSampler.AssertWithinBudget(2552, 2552, "a hold tick");
        AllocationSampler.AssertWithinBudget(2553, 2552, "a hold tick");
        AllocationSampler.AssertWithinBudget(2808, 2552, "a hold tick");
    }

    [Fact(DisplayName = "a figure over the pin plus the tolerance fails with the figure, the pin and the tolerance")]
    [Trait("Requirement", "ADR16-7")]
    public void A_figure_over_the_pin_plus_the_tolerance_fails()
    {
        TrueException error = Assert.Throws<TrueException>(() => AllocationSampler.AssertWithinBudget(2809, 2552, "a hold tick"));

        Assert.StartsWith("a hold tick allocated 2809 bytes; the pin is 2552 bytes and the tolerance 256 bytes (at most 2808)", error.Message, StringComparison.Ordinal);
    }
}

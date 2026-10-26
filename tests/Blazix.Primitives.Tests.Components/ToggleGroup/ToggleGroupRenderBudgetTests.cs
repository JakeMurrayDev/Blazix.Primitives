using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components;

namespace Blazix.Primitives.Tests.Components;

/// <summary>
/// ADR 16 rule 7 for ToggleGroup at L2: a re-render of a group of three toggles with unchanged parameters (the parent
/// passing the same values again), and the click of a toggle in a component-owned group with no receiver, each measured
/// around bUnit's synchronous work on the test thread. The re-render includes Blazor's own per-render cost, the child
/// content Blazor renders again and the twins' comparisons of their options, which send nothing; the click includes Blazor's
/// dispatch, the request, the new list and the renders of the two toggles whose state changed. The budgets were taken from
/// the first measurement and only tighten afterwards.
/// </summary>
public sealed class ToggleGroupRenderBudgetTests : IDisposable
{
    // First measurement 2026-10-26 (arm64, .NET 11 RC2, Release): 848 bytes for the re-render, which reaches no toggle
    // (bUnit's parameterless Render() passes no parameter, so the cascade keeps its content); 29552 bytes for the click.
    // The click's cost grows with the number of toggles in the group although only the two whose state changed render
    // (ToggleGroupTests' render-count title): the same click costs 1568 bytes when the group cancels it, and 54176 bytes
    // in a group of six, so the rest is bUnit's update of the group's markup after each render.
    private const long GroupRerenderBudgetBytes = 848;
    private const long SelectionBudgetBytes = 29552;

    private readonly ToggleTestContext test = new();

    /// <inheritdoc />
    public void Dispose() => test.Dispose();

    [Fact(DisplayName = "a re-render of a group of three toggles with unchanged parameters stays within its budget")]
    [Trait("Requirement", "ADR16-7")]
    public void A_rerender_of_a_group_of_three_toggles_stays_within_its_budget()
    {
        RenderFragment toggles = ToggleTestContext.Toggles("one", "two", "three");
        IRenderedComponent<ToggleGroup> cut = test.Context.Render<ToggleGroup>(p => p
            .AddUnmatched("class", "consumer")
            .AddUnmatched("aria-label", "Text alignment")
            .AddChildContent(toggles));
        for (int i = 0; i < 20; i++)
        {
            cut.Render();
        }

        long before = GC.GetAllocatedBytesForCurrentThread();
        cut.Render();
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.True(allocated <= GroupRerenderBudgetBytes, $"a re-render allocated {allocated} bytes; the budget is {GroupRerenderBudgetBytes}");
    }

    [Fact(DisplayName = "the click of a toggle in a component-owned group with no receiver stays within its budget")]
    [Trait("Requirement", "ADR16-7")]
    public void The_click_of_a_toggle_in_a_component_owned_group_stays_within_its_budget()
    {
        IRenderedComponent<ToggleGroup> cut = test.Context.Render<ToggleGroup>(p => p.AddChildContent(ToggleTestContext.Toggles("one", "two", "three")));
        for (int i = 0; i < 20; i++)
        {
            ToggleTestContext.Toggles(cut)[i % 3].Click();
        }

        // The least of five clicks, each selecting another toggle: bUnit's own lists grow by doubling, which one click may hit.
        long least = long.MaxValue;
        for (int i = 0; i < 5; i++)
        {
            IElement toggle = ToggleTestContext.Toggles(cut)[i % 3];
            long before = GC.GetAllocatedBytesForCurrentThread();
            toggle.Click();
            least = Math.Min(least, GC.GetAllocatedBytesForCurrentThread() - before);
        }

        Assert.True(least <= SelectionBudgetBytes, $"a click allocated {least} bytes; the budget is {SelectionBudgetBytes}");
    }
}

using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace Blazix.Primitives.Tests.Components;

/// <summary>
/// ADR 16 rule 7 for Toggle: a re-render with unchanged parameters (the parent passing the same values again), and the
/// click of a component-owned toggle with no receiver, each measured around bUnit's synchronous work on the test thread.
/// The re-render includes Blazor's own per-render cost and the twin's comparison of its options, which sends nothing; the
/// click includes Blazor's dispatch, the request and the render of the new state. The budgets were taken from the first
/// measurement and only tighten afterwards.
/// </summary>
[Collection(AllocationBudgetsCollection.Name)]
public sealed class ToggleAllocationBudgetTests : IDisposable
{
    // First measurement 2026-10-26 (arm64, .NET 11 RC2, Release): 624 bytes for the re-render, Button's 560 with the same
    // bag plus the toggle's own onclick chained with the consumer's; 6088 bytes for the click, mostly Blazor's dispatch and
    // the render of the new state. The re-render passes neither Pressed nor DefaultPressed: bUnit's parameterless Render()
    // sends an empty parameter set, which a parent's render never does, and Controlled<bool> would read it as a crossing.
    private const long RerenderBudgetBytes = 624;
    private const long ClickBudgetBytes = 6088;

    private readonly ToggleTestContext test = new();

    /// <inheritdoc />
    public void Dispose() => test.Dispose();

    [Fact(DisplayName = "a re-render of a toggle with unchanged parameters stays within its budget")]
    [Trait("Requirement", "ADR16-7")]
    public void A_rerender_of_a_toggle_with_unchanged_parameters_stays_within_its_budget()
    {
        EventCallback<MouseEventArgs> onClick = EventCallback.Factory.Create<MouseEventArgs>(this, () => { });
        EventCallback<KeyboardEventArgs> onKeyDown = EventCallback.Factory.Create<KeyboardEventArgs>(this, () => { });
        IRenderedComponent<Toggle> cut = test.Context.Render<Toggle>(p => p
            .AddUnmatched("class", "consumer")
            .AddUnmatched("aria-label", "Favorite")
            .AddUnmatched("onclick", onClick)
            .AddUnmatched("onkeydown", onKeyDown)
            .AddChildContent("Favorite"));

        for (int i = 0; i < 20; i++)
        {
            cut.Render();
        }

        long before = GC.GetAllocatedBytesForCurrentThread();
        cut.Render();
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.True(allocated <= RerenderBudgetBytes, $"a re-render allocated {allocated} bytes; the budget is {RerenderBudgetBytes}");
    }

    [Fact(DisplayName = "the click of a component-owned toggle with no receiver stays within its budget")]
    [Trait("Requirement", "ADR16-7")]
    public void The_click_of_a_component_owned_toggle_with_no_receiver_stays_within_its_budget()
    {
        IRenderedComponent<Toggle> cut = test.Context.Render<Toggle>();
        for (int i = 0; i < 20; i++)
        {
            cut.Find("button").Click();
        }

        // The least of five clicks: bUnit's own lists grow by doubling, which one click may hit.
        long least = long.MaxValue;
        for (int i = 0; i < 5; i++)
        {
            IElement button = cut.Find("button");
            long before = GC.GetAllocatedBytesForCurrentThread();
            button.Click();
            least = Math.Min(least, GC.GetAllocatedBytesForCurrentThread() - before);
        }

        Assert.True(least <= ClickBudgetBytes, $"a click allocated {least} bytes; the budget is {ClickBudgetBytes}");
    }
}

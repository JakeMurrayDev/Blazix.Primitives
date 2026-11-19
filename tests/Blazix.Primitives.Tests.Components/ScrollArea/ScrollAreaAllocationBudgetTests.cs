using System.Text.Json;
using Blazix.Primitives.Interop;
using Bunit;

// The namespace is the project's root: a namespace named after the family would hide the parts' types.
namespace Blazix.Primitives.Tests.Components;

/// <summary>
/// ADR 16 rule 7 for ScrollArea (decision 26 of the M6 plan; the brief's Deliverable 14): one overflow report through the
/// root's context (an edge crossing, which renders the root and its subtree once), one scrolling flip (which renders no part
/// without a state function), and a re-render of the six parts with unchanged parameters (which sends no update). Each report
/// is measured inside the renderer's dispatcher call that delivers it, from its arrival to the end of its synchronous work.
/// The budgets were taken from the first measurement and only tighten afterwards.
/// </summary>
[Collection(AllocationBudgetsCollection.Name)]
public sealed class ScrollAreaAllocationBudgetTests : IDisposable
{
    // First measurement 2026-11-19 (macOS arm64, .NET 11, Release, the class alone in its process; the least of five in each
    // of five processes, all equal). 31,152 bytes for an edge crossing: the report's deserialization, the root's render and,
    // in its batch, its subtree's; bUnit rebuilding the root's markup is most of it.
    private const long OverflowReportBudgetBytes = 31152;

    // 128 bytes for a scrolling flip: the report's deserialization alone, as no part without a state function renders.
    private const long ScrollingReportBudgetBytes = 128;

    // 7,776 bytes for a re-render of the six parts with unchanged parameters: the root and its cascade render again; no
    // options record differs, so no update is sent.
    private const long RerenderBudgetBytes = 7776;

    private readonly ScrollAreaTestContext test = new();

    /// <inheritdoc />
    public void Dispose() => test.Dispose();

    [Fact(DisplayName = "an overflow report that crosses an edge stays within its budget")]
    [Trait("Requirement", "ADR16-7")]
    public async Task An_overflow_report_that_crosses_an_edge_stays_within_its_budget()
    {
        IRenderedComponent<ScrollAreaHost> cut = test.Context.Render<ScrollAreaHost>(p => p.Add(x => x.KeepMounted, true));

        long allocated = await MeasureAsync(cut, "OnOverflow", i => JsonSerializer.Serialize(new { hasOverflowX = false, hasOverflowY = true, xStart = false, xEnd = false, yStart = i % 2 == 1, yEnd = true, measured = true }));

        Assert.True(allocated <= OverflowReportBudgetBytes, $"an overflow report allocated {allocated} bytes; the budget is {OverflowReportBudgetBytes}");
    }

    [Fact(DisplayName = "a scrolling flip without state functions stays within its budget")]
    [Trait("Requirement", "ADR16-7")]
    public async Task A_scrolling_flip_without_state_functions_stays_within_its_budget()
    {
        IRenderedComponent<ScrollAreaHost> cut = test.Context.Render<ScrollAreaHost>(p => p.Add(x => x.KeepMounted, true));

        long allocated = await MeasureAsync(cut, "OnScrolling", i => JsonSerializer.Serialize(new { x = false, y = i % 2 == 1 }));

        Assert.True(allocated <= ScrollingReportBudgetBytes, $"a scrolling report allocated {allocated} bytes; the budget is {ScrollingReportBudgetBytes}");
    }

    [Fact(DisplayName = "a re-render of a scroll area with unchanged parameters stays within its budget and sends no update")]
    [Trait("Requirement", "ADR16-7")]
    public void A_rerender_with_unchanged_parameters_stays_within_its_budget()
    {
        IRenderedComponent<ScrollAreaHost> cut = test.Context.Render<ScrollAreaHost>(p => p.Add(x => x.KeepMounted, true));
        for (int i = 0; i < 20; i++)
        {
            cut.Render(p => p.Add(x => x.KeepMounted, true));
        }

        int updates = test.Updates.Count;
        long allocated = AllocationSampler.LeastOfFive(() => cut.Render(p => p.Add(x => x.KeepMounted, true)));

        Assert.True(allocated <= RerenderBudgetBytes, $"a re-render allocated {allocated} bytes; the budget is {RerenderBudgetBytes}");
        Assert.Equal(updates, test.Updates.Count);
    }

    // Twenty reports alternate the state, then five more are measured, each a change. The reports are built before the window.
    private async Task<long> MeasureAsync(IRenderedComponent<ScrollAreaHost> cut, string method, Func<int, string> report)
    {
        ScrollAreaTwin twin = test.RootTwin();
        Action<string> deliver = method == "OnOverflow" ? twin.OnOverflow : twin.OnScrolling;
        string[] reports = [.. Enumerable.Range(1, 25).Select(report)];
        for (int i = 0; i < 20; i++)
        {
            string json = reports[i];
            await cut.InvokeAsync(() => deliver(json));
        }

        long allocated = long.MaxValue;
        for (int run = 0; run < AllocationSampler.Runs; run++)
        {
            string json = reports[20 + run];
            long sample = 0;
            await cut.InvokeAsync(() =>
            {
                long before = GC.GetAllocatedBytesForCurrentThread();
                deliver(json);
                sample = GC.GetAllocatedBytesForCurrentThread() - before;
            });
            allocated = Math.Min(allocated, sample);
        }

        return allocated;
    }
}

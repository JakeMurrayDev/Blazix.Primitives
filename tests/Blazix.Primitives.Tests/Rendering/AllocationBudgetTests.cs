using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace Blazix.Primitives.Tests.Rendering;

/// <summary>
/// ADR 16 rule 7: allocation budgets as tests. Each budget was taken from the first measurement on the machine that
/// wrote it and only tightens afterwards; a loosening needs an ADR amendment. The measurement is per thread and the
/// runs are repeated so a JIT or pool warm-up does not count.
/// </summary>
[Collection(AllocationBudgetsCollection.Name)]
public sealed class AllocationBudgetTests
{
    // First measurement 2026-10-20 (arm64, .NET 11 RC2, Release): 1184 bytes, the result list, its entries array,
    // the merged class and style strings and the chained handler's callback.
    private const long MergeBudgetBytes = 1184;

    [Fact(DisplayName = "a merge of two lists with class, style and a chained handler stays within its budget")]
    [Trait("Requirement", "ADR16-7")]
    public void A_merge_of_two_lists_with_class_style_and_a_chained_handler_stays_within_its_budget()
    {
        AttributeList ours = new();
        ours.Set("class", "internal-class");
        ours.Set("style", "color: blue; padding: 4px");
        ours.Set("role", "group");
        ours.Set("onclick", new EventCallback(null, (Action<MouseEventArgs>)(_ => { })));
        AttributeList theirs = new();
        theirs.Set("class", "external-class");
        theirs.Set("style", "color: red");
        theirs.Set("data-testid", "root");
        theirs.Set("onclick", new EventCallback(null, (Action<MouseEventArgs>)(_ => { })));

        long allocated = Measure(() => MergeProps.Merge(ours, theirs));

        Assert.True(allocated <= MergeBudgetBytes, $"a merge allocated {allocated} bytes; the budget is {MergeBudgetBytes}");
    }

    private static long Measure(Action action)
    {
        for (int i = 0; i < 20; i++)
        {
            action();
        }

        long before = GC.GetAllocatedBytesForCurrentThread();
        action();
        return GC.GetAllocatedBytesForCurrentThread() - before;
    }
}

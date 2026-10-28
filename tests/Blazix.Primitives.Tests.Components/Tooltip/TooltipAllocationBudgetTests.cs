using Bunit;
using Microsoft.AspNetCore.Components;

// The namespace is the project's root: a namespace named after the family would hide the parts' types.
namespace Blazix.Primitives.Tests.Components;

/// <summary>
/// ADR 16 rule 7 for the tooltip root (issue 121): the least of five re-renders of a closed <see cref="TooltipRoot{TPayload}"/>
/// and its trigger with unchanged parameters, measured around bUnit's synchronous render on the test thread, as the
/// positioner's budget is. It includes Blazor's own per-render cost; the root's own share is its cascades, and the
/// trigger's the interactions options it builds after a render, whose comparison sends nothing.
/// </summary>
[Collection(AllocationBudgetsCollection.Name)]
public sealed class TooltipAllocationBudgetTests : IDisposable
{
    // First measurement 2026-10-28 (arm64, .NET 11 RC2, Release): 2912 bytes, the test alone and with the trigger left out
    // alike (the trigger's parameters are unchanged, so it does not render). The root and its two fixed cascades render,
    // the harness rebuilds one parameter expression per parameter, and the session's comparison sends nothing.
    private const long RerenderBudgetBytes = 2912;

    private readonly TooltipTestContext host = new();

    /// <inheritdoc />
    public void Dispose() => host.Dispose();

    [Fact(DisplayName = "a re-render of a tooltip root and its trigger with unchanged parameters sends nothing and stays within its budget")]
    [Trait("Requirement", "ADR16-7")]
    public void A_rerender_with_unchanged_parameters_sends_nothing_and_stays_within_its_budget()
    {
        RenderFragment content = builder =>
        {
            builder.OpenComponent<TooltipTrigger<NoPayload>>(0);
            builder.AddAttribute(1, "id", "trigger");
            builder.CloseComponent();
        };
        Action<ComponentParameterCollectionBuilder<TooltipRoot<NoPayload>>> parameters = p => p
            .Add(x => x.DisableHoverablePopup, true)
            .Add(x => x.ChildContent, content);
        IRenderedComponent<TooltipRoot<NoPayload>> cut = host.Context.Render(parameters);

        for (int i = 0; i < 20; i++)
        {
            cut.Render(parameters);
        }

        int updates = host.InteractionsUpdates.Count;
        long allocated = long.MaxValue;
        for (int i = 0; i < 5; i++)
        {
            long before = GC.GetAllocatedBytesForCurrentThread();
            cut.Render(parameters);
            allocated = Math.Min(allocated, GC.GetAllocatedBytesForCurrentThread() - before);
        }

        Assert.Equal(updates, host.InteractionsUpdates.Count);
        Assert.True(allocated <= RerenderBudgetBytes, $"a re-render allocated {allocated} bytes; the budget is {RerenderBudgetBytes}");
    }
}

using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Blazix.Primitives.Tests.Components;

/// <summary>
/// ADR 16 rule 7 for Popover: a re-render with unchanged parameters (the parent passing the same values again) of a closed
/// root with its trigger, portal, positioner and popup, measured around bUnit's synchronous render on the test thread. It
/// includes Blazor's own per-render cost of the root and its fixed cascade; the session's and the trigger's comparison of
/// their options sends nothing. The budget was taken from the first measurement and only tightens afterwards.
/// </summary>
[Collection(AllocationBudgetsCollection.Name)]
public sealed class PopoverAllocationBudgetTests : IDisposable
{
    // First measurement 2026-10-28 (arm64, .NET 11 RC2, Release; M4 track 5): 1544 bytes in 3 of 3 runs, for the parent
    // passing the same child content again to a closed root with one trigger. The root and its fixed cascade render; the
    // trigger and the portal, whose parameters are the same fragments, are skipped by Blazor's change detection, and the
    // positioner and popup are not mounted. The session behaviour compares its inputs with the options last sent, so
    // nothing is sent.
    private const long RerenderBudgetBytes = 1544;

    private readonly PopoverTestContext test = new();

    /// <inheritdoc />
    public void Dispose() => test.Dispose();

    [Fact(DisplayName = "a re-render of a closed root with its parts and unchanged parameters sends nothing and stays within its budget")]
    [Trait("Requirement", "ADR16-7")]
    public void A_rerender_with_unchanged_parameters_sends_nothing_and_stays_within_its_budget()
    {
        RenderFragment content = Content;
        Action<ComponentParameterCollectionBuilder<PopoverRoot<NoPayload>>> parameters = p => p.AddChildContent(content);
        IRenderedComponent<PopoverRoot<NoPayload>> cut = test.Context.Render(parameters);

        for (int i = 0; i < 20; i++)
        {
            cut.Render(parameters);
        }

        int updates = test.InteractionsHandle.Invocations["update"].Count;
        long before = GC.GetAllocatedBytesForCurrentThread();
        cut.Render(parameters);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Equal(updates, test.InteractionsHandle.Invocations["update"].Count);
        Assert.True(allocated <= RerenderBudgetBytes, $"a re-render allocated {allocated} bytes; the budget is {RerenderBudgetBytes}");
    }

    private static void Content(RenderTreeBuilder builder)
    {
        builder.OpenComponent<PopoverTrigger<NoPayload>>(0);
        builder.AddComponentParameter(1, nameof(PopoverTrigger<NoPayload>.ChildContent), (RenderFragment)(b => b.AddContent(0, "Toggle")));
        builder.CloseComponent();
        builder.OpenComponent<PopoverPortal>(2);
        builder.AddComponentParameter(3, nameof(PopoverPortal.ChildContent), (RenderFragment)(p =>
        {
            p.OpenComponent<PopoverPositioner>(0);
            p.AddComponentParameter(1, nameof(PopoverPositioner.ChildContent), (RenderFragment)(q =>
            {
                q.OpenComponent<PopoverPopup>(0);
                q.CloseComponent();
            }));
            p.CloseComponent();
        }));
        builder.CloseComponent();
    }
}

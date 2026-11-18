using Bunit;

namespace Blazix.Primitives.Tests.Components;

/// <summary>
/// ADR 16 rule 7 for Drawer (decision 26 (a) of the M6 plan, chosen by the user 2026-11-18): the re-render of an open
/// drawer with three snap points and unchanged parameters sends nothing to any module and stays within its budget. The
/// <c>update</c> path with three snap points is the <c>drawer</c> module's (8b). Budgets are the least of five
/// measurements, in the non-parallel collection.
/// </summary>
[Collection(AllocationBudgetsCollection.Name)]
public sealed class DrawerAllocationBudgetTests : IDisposable
{
    // First measurement 2026-11-18 (arm64, .NET 11, Release), the least of five after twenty renders, each class in its own
    // process as allocation-budgets.mjs runs it: 3808 bytes in five of five runs for the re-render of the harness with a
    // root, a probe, a portal, a viewport, a popup and a close, three snap points and an equal list each time (the list is
    // the harness's field, so the root's sequence comparison returns at the reference check). No module is sent an update.
    private const long RerenderBudgetBytes = 3808;

    private readonly DialogTestContext test = new();

    /// <inheritdoc />
    public void Dispose() => test.Dispose();

    [Fact(DisplayName = "port: a re-render of an open drawer with three snap points and unchanged parameters sends nothing and stays within its budget")]
    [Trait("Requirement", "ADR16-7")]
    public void A_rerender_of_an_open_drawer_sends_nothing_and_stays_within_its_budget()
    {
        Action<ComponentParameterCollectionBuilder<DrawerSnapHarness>> parameters = p => p.Add(x => x.Shape, "default");
        IRenderedComponent<DrawerSnapHarness> cut = test.Context.Render(parameters);
        Action render = () => cut.Render(parameters);
        for (int i = 0; i < 20; i++)
        {
            render();
        }

        long allocated = AllocationSampler.LeastOfFive(render);
        int sends = Sends();

        cut.Render(parameters);

        Assert.Equal(sends, Sends());
        Assert.True(allocated <= RerenderBudgetBytes, $"a re-render allocated {allocated} bytes; the budget is {RerenderBudgetBytes}");
    }

    private int Sends() =>
        new[] { test.InteractionsHandle, test.FocusManagerHandle, test.TransitionsHandle, test.PortalHandle, test.ButtonHandle, test.ScrollLockHandle }
            .Sum(handle => DialogTestContext.UpdateOptions(handle).Count);
}

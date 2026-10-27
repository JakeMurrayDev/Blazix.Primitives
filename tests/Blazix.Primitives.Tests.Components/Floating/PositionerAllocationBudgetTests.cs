using Bunit;

namespace Blazix.Primitives.Tests.Components;

/// <summary>
/// ADR 16 rule 7 for the positioner's behavior: the least of five re-renders of <see cref="TestPositioner"/> with unchanged inputs,
/// measured around bUnit's synchronous render on the test thread, as the composite root's budget is. It includes Blazor's
/// own per-render cost, the inputs record the part builds per parameter set and the behavior's comparison of its options,
/// which sends nothing. The budget was taken from the first measurement and only tightens afterwards.
/// </summary>
[Collection(AllocationBudgetsCollection.Name)]
public sealed class PositionerAllocationBudgetTests : IDisposable
{
    // First measurement 2026-10-27 (arm64, .NET 11 RC2, Release): 6208 bytes for the parent passing the same values again to
    // a positioner with an arrow. The positioner, its fixed cascade and the arrow all render (the arrow's child content is
    // a new fragment on every render of the content), and the harness rebuilds one parameter expression per parameter, as
    // the composite budget records. The behaviours' own share is the inputs record the part builds per parameter set;
    // with nothing changed no options are built, so the comparison and the send allocate nothing (6608 before the
    // behaviour skipped building equal options). 2026-10-27 (issue 112): the least of five re-renders, in the non-parallel
    // AllocationBudgets collection (settled by the orchestrator the same day). The five samples of one run are equal, and the
    // test alone always measures 6208, but a full run of the project measures 6208, 6288, 6320 or 6352 by run: the state the
    // earlier tests leave in the process (the harness's pools, the JIT's tiering) moves this floor, not work inside the
    // window. The budget is the envelope of 46 full runs (31 at 6208), taken before it first landed on main.
    private const long RerenderBudgetBytes = 6352;

    private readonly PositioningTestContext host = new();

    /// <inheritdoc />
    public void Dispose() => host.Dispose();

    [Fact(DisplayName = "a re-render of a positioner with unchanged inputs sends nothing and stays within its budget")]
    [Trait("Requirement", "ADR16-7")]
    public void A_rerender_with_unchanged_inputs_sends_nothing_and_stays_within_its_budget()
    {
        Offset offset = new(AnchorWidth: 1) { Overrides = [new OffsetOverride(Side.Top, null, Constant: 4)] };
        Action<ComponentParameterCollectionBuilder<TestPositioner>> parameters = p => p
            .Add(x => x.Side, Side.Top)
            .Add(x => x.SideOffset, offset)
            .Add(x => x.CollisionPadding, 8)
            .AddUnmatched("style", "color: red")
            .AddChildContent<TestPositionerArrow>();
        IRenderedComponent<TestPositioner> cut = host.Context.Render(parameters);

        for (int i = 0; i < 20; i++)
        {
            cut.Render(parameters);
        }

        // The least of five re-renders (the Switch toggle budget's precedent): bUnit's own lists grow by doubling, which one
        // render may hit.
        long allocated = long.MaxValue;
        for (int i = 0; i < 5; i++)
        {
            long before = GC.GetAllocatedBytesForCurrentThread();
            cut.Render(parameters);
            allocated = Math.Min(allocated, GC.GetAllocatedBytesForCurrentThread() - before);
        }

        Assert.Empty(host.UpdateOptions);
        Assert.True(allocated <= RerenderBudgetBytes, $"a re-render allocated {allocated} bytes; the budget is {RerenderBudgetBytes}");
    }
}

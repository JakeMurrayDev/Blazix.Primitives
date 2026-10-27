using Bunit;

namespace Blazix.Primitives.Tests.Components;

/// <summary>
/// ADR 16 rule 7 for the positioner's behavior: a re-render of <see cref="TestPositioner"/> with unchanged inputs,
/// measured around bUnit's synchronous render on the test thread, as the composite root's budget is. It includes Blazor's
/// own per-render cost, the inputs record the part builds per parameter set and the behavior's comparison of its options,
/// which sends nothing. The budget was taken from the first measurement and only tightens afterwards.
/// </summary>
public sealed class PositionerAllocationBudgetTests : IDisposable
{
    // First measurement 2026-10-27 (arm64, .NET 11 RC2, Release): 6208 bytes for the parent passing the same values again to
    // a positioner with an arrow. The positioner, its fixed cascade and the arrow all render (the arrow's child content is
    // a new fragment on every render of the content), and the harness rebuilds one parameter expression per parameter, as
    // the composite budget records. The behaviours' own share is the inputs record the part builds per parameter set;
    // with nothing changed no options are built, so the comparison and the send allocate nothing (6608 before the
    // behaviour skipped building equal options).
    private const long RerenderBudgetBytes = 6208;

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

        long before = GC.GetAllocatedBytesForCurrentThread();
        cut.Render(parameters);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Empty(host.UpdateOptions);
        Assert.True(allocated <= RerenderBudgetBytes, $"a re-render allocated {allocated} bytes; the budget is {RerenderBudgetBytes}");
    }
}

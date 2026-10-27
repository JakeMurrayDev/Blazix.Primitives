using Blazix.Primitives.Interop;

namespace Blazix.Primitives.Tests;

/// <summary>
/// L1: the options record of the <c>positioning</c> module compares by value (ADR 04 and ADR 16 Amendments 2026-10-27), so
/// a Positioner whose re-render builds equal options sends no <c>update</c>, and the comparison allocates nothing.
/// </summary>
public sealed class PositioningOptionsTests
{
    private static PositioningOptions Build(double sideOffset = 8) => new()
    {
        TriggerId = "base-ui-2",
        AnchorRef = 0,
        VirtualAnchor = new PositioningRect(1, 2, 3, 4),
        Side = "top",
        SideOffset = sideOffset,
        SideOffsetFunction = new PositioningOffset(0, 1, 0, 1, 0, [new PositioningOffsetOverride("right", "end", 3, 0, 0, 0, 0)]),
        BoundaryRect = new PositioningRect(0, 0, 300, 200),
        BoundaryRefs = new PositioningIndexes(1, 2),
        CollisionPaddingSides = new PositioningPadding(1, 2, 3, 4),
        CollisionAvoidance = new PositioningCollisionAvoidance(null, "shift", "none"),
        Shift = new PositioningShift(false, "layoutViewport"),
        Style = "color: red",
    };

    [Fact(DisplayName = "two options records built from equal inputs are equal, and one differing field makes them differ")]
    public void Two_options_records_built_from_equal_inputs_are_equal()
    {
        Assert.Equal(Build(), Build());
        Assert.Equal(Build().GetHashCode(), Build().GetHashCode());
        Assert.NotEqual(Build(), Build(sideOffset: 9));
        Assert.NotEqual(Build(), Build() with { Style = "color: blue" });
        Assert.NotEqual(Build(), Build() with { CollisionAvoidance = new PositioningCollisionAvoidance(null, "flip", "none") });

        // The overrides are a list: compared item by item, not by reference.
        Assert.Equal(Build(), Build() with { SideOffsetFunction = new PositioningOffset(0, 1, 0, 1, 0, new List<PositioningOffsetOverride> { new("right", "end", 3, 0, 0, 0, 0) }) });
        Assert.NotEqual(Build(), Build() with { SideOffsetFunction = new PositioningOffset(0, 1, 0, 1, 0, [new PositioningOffsetOverride("left", "end", 3, 0, 0, 0, 0)]) });

        // The boundary's indexes into the references compare by value too.
        Assert.NotEqual(Build(), Build() with { BoundaryRefs = new PositioningIndexes(1, 3) });
        Assert.NotEqual(Build(), Build() with { AnchorRef = null });
    }

    [Fact(DisplayName = "comparing two options records allocates nothing")]
    [Trait("Requirement", "ADR16-7")]
    public void Comparing_two_options_records_allocates_nothing()
    {
        PositioningOptions first = Build();
        PositioningOptions second = Build();
        long total = 0;
        for (int i = 0; i < 2; i++)
        {
            long before = GC.GetAllocatedBytesForCurrentThread();
            bool equal = first.Equals(second) & first == second & EqualityComparer<PositioningOptions>.Default.Equals(first, second);
            total = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.True(equal);
        }

        Assert.Equal(0, total);
    }
}

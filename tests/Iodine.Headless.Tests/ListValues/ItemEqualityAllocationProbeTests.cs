using Blazix.Primitives.Utilities;

namespace Blazix.Primitives.Tests.ListValues;

// ADR 16 rule 7 (the least of five runs), ADR 05 Amendments 2026-10-20 (Is<T> re-measured for M5): the item equality's
// render-path calls allocate nothing on CoreCLR once warm, for an int, a string, an enum and a record class: the comparison
// with and without a comparer, the single and the multiple FindSelectionIndex over 300 items (the multiple one reuses its
// thread-static set), Includes, and both dirty tests. The WebAssembly interpreter is measured by the `wasm`-only L4 probe.
/// <summary>The allocation probe of the item equality on CoreCLR.</summary>
[Collection(AllocationBudgetsCollection.Name)]
public sealed class ItemEqualityAllocationProbeTests
{
    private const int Items = 300;
    private const int Calls = 100;

    [Fact(DisplayName = "port: the item equality allocates nothing for an int over 300 items")]
    [Trait("Requirement", "ADR16-7")]
    [Trait("Requirement", "UTL-ALG-06")]
    public void The_item_equality_allocates_nothing_for_an_int() => AssertNoAllocation(i => i);

    [Fact(DisplayName = "port: the item equality allocates nothing for a string over 300 items")]
    [Trait("Requirement", "ADR16-7")]
    [Trait("Requirement", "UTL-ALG-06")]
    public void The_item_equality_allocates_nothing_for_a_string() => AssertNoAllocation(i => $"item-{i}");

    [Fact(DisplayName = "port: the item equality allocates nothing for an enum over 300 items")]
    [Trait("Requirement", "ADR16-7")]
    [Trait("Requirement", "UTL-ALG-06")]
    public void The_item_equality_allocates_nothing_for_an_enum() => AssertNoAllocation(i => (Shade)i);

    [Fact(DisplayName = "port: the item equality allocates nothing for a record class over 300 items")]
    [Trait("Requirement", "ADR16-7")]
    [Trait("Requirement", "UTL-ALG-06")]
    public void The_item_equality_allocates_nothing_for_a_record_class() => AssertNoAllocation(i => new Item(i));

    private static void AssertNoAllocation<T>(Func<int, T> create)
    {
        ItemSlot<T>[] items = [.. Enumerable.Range(0, Items).Select(i => new ItemSlot<T>(create(i), true))];
        T last = items[^1].Value;
        IReadOnlyList<T> selected = [items[^3].Value, items[^2].Value, last];
        IReadOnlyList<T> initial = [items[^3].Value, items[^2].Value, last];
        Func<T, T, bool> comparer = (item, value) => EqualityComparer<T>.Default.Equals(item, value);
        Exercise(items, last, selected, initial, comparer);

        long least = long.MaxValue;
        for (int run = 0; run < 5; run++)
        {
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < Calls; i++)
            {
                Exercise(items, last, selected, initial, comparer);
            }

            least = Math.Min(least, GC.GetAllocatedBytesForCurrentThread() - before);
        }

        Assert.True(least == 0, $"{least} bytes over {Calls} rounds for {typeof(T).Name}");
    }

    private static void Exercise<T>(ItemSlot<T>[] items, T last, IReadOnlyList<T> selected, IReadOnlyList<T> initial, Func<T, T, bool> comparer)
    {
        Assert.True(ItemEquality.Compare(last, last, null));
        Assert.True(ItemEquality.Compare(last, last, comparer));
        Assert.True(ItemEquality.FindSelectionIndex<T>(items, last, null) == Items - 1);
        Assert.True(ItemEquality.FindSelectionIndex<T>(items, selected, null) == Items - 3);
        Assert.True(ItemEquality.Includes(selected, last, null));
        Assert.False(ItemEquality.IsDirty(selected, initial, null));
        Assert.False(ItemEquality.IsDirty(last, last));
    }

    private enum Shade
    {
        None,
    }

    private sealed record Item(int Id);
}

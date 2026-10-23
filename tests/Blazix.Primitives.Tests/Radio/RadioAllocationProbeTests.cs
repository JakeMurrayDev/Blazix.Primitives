using Blazix.Primitives.Store;

namespace Blazix.Primitives.Tests.Radio;

// ADR 08 Amendments 2026-10-22 (any-typed values; "does not box ... the primitives"), ADR 16 rule 7: the comparisons a radio
// and its group make on the render path, Equality.StrictEquals<TValue>, ValueChangedObserver<TValue>.Observe and the null
// test, allocate nothing on CoreCLR for a string, an enum, an int, a record class and a record struct. The WebAssembly
// interpreter is measured by the `wasm`-only L4 probe on the radio-group-allocation fixture (radio-group.spec.ts).
/// <summary>The allocation probe of the radio's value comparisons on CoreCLR.</summary>
public sealed class RadioAllocationProbeTests
{
    private const int Calls = 1000;

    [Fact(DisplayName = "port: the value comparisons allocate nothing for a string")]
    [Trait("Requirement", "ADR16-7")]
    public void Port_the_value_comparisons_allocate_nothing_for_a_string() => AssertNoAllocation("a", "b");

    [Fact(DisplayName = "port: the value comparisons allocate nothing for an enum")]
    [Trait("Requirement", "ADR16-7")]
    public void Port_the_value_comparisons_allocate_nothing_for_an_enum() => AssertNoAllocation(Fruit.Apple, Fruit.Pear);

    [Fact(DisplayName = "port: the value comparisons allocate nothing for an int")]
    [Trait("Requirement", "ADR16-7")]
    public void Port_the_value_comparisons_allocate_nothing_for_an_int() => AssertNoAllocation(1, 2);

    [Fact(DisplayName = "port: the value comparisons allocate nothing for a record class")]
    [Trait("Requirement", "ADR16-7")]
    public void Port_the_value_comparisons_allocate_nothing_for_a_record_class() => AssertNoAllocation(new Item("a"), new Item("b"));

    [Fact(DisplayName = "port: the value comparisons allocate nothing for a record struct")]
    [Trait("Requirement", "ADR16-7")]
    public void Port_the_value_comparisons_allocate_nothing_for_a_record_struct() => AssertNoAllocation(new Point(1, 2), new Point(3, 4));

    private static void AssertNoAllocation<T>(T first, T second)
    {
        int changes = 0;
        ValueChangedObserver<T> observer = new(first, _ => changes++);
        Exercise(observer, first, second);

        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < Calls; i++)
        {
            Exercise(observer, first, second);
        }

        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.True(changes > 0);
        Assert.True(allocated == 0, $"{allocated} bytes over {Calls} rounds for {typeof(T).Name}");
    }

    private static void Exercise<T>(ValueChangedObserver<T> observer, T first, T second)
    {
        Assert.True(Equality.StrictEquals(first, first));
        Assert.False(Equality.StrictEquals(first, second));
        Assert.False(RadioValues.IsNull(first));
        observer.Observe(second);
        observer.Observe(first);
    }

    private enum Fruit
    {
        Apple,
        Pear,
    }

    private sealed record Item(string Name);

    private readonly record struct Point(int X, int Y);
}

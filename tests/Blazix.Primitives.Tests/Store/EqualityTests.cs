using Blazix.Primitives.Store;

namespace Blazix.Primitives.Tests.Store;

/// <summary>
/// ADR 05 §1's <c>Is&lt;T&gt;</c> against <c>Object.is</c> (UTL §8.2): value types and strings by value, every other
/// reference type by reference, records never structurally, <c>NaN</c> equal to itself and <c>-0</c> distinct from <c>0</c>.
/// <c>StrictEquals</c> is JavaScript's <c>===</c>, used where Base UI compares with <c>!==</c> (UTL-STA-19, UTL-STA-36).
/// <c>SameValueZero</c> is <c>Array.prototype.includes</c>' comparison (ACC-STA-04; ADR 05 note dated 2026-10-26).
/// </summary>
public sealed class EqualityTests
{
    [Fact(DisplayName = "Object.is: value types compare by value")]
    public void Value_types_compare_by_value()
    {
        Assert.True(Equality.Is(1, 1));
        Assert.False(Equality.Is(1, 2));
        Assert.True(Equality.Is(true, true));
        Assert.True(Equality.Is(EventReason.TriggerPress, EventReason.TriggerPress));
        Assert.False(Equality.Is(EventReason.TriggerPress, EventReason.None));
        Assert.True(Equality.Is<int?>(null, null));
        Assert.False(Equality.Is<int?>(null, 0));
        Assert.True(Equality.Is((1, "a"), (1, "a")));
    }

    // ADR 05 Amendments 2026-10-20 as amended 2026-10-23 (design point R8 of M2 tracks 7-8, settled by the orchestrator
    // 2026-10-23): the BCL primitives and their nullables compare with == instead of EqualityComparer<T>, which boxes on
    // the WebAssembly interpreter; the result is the same.
    [Fact(DisplayName = "Object.is and ===: the BCL primitives and their nullables compare by value")]
    public void Primitives_and_their_nullables_compare_by_value()
    {
        AssertByValue(true, false);
        AssertByValue('a', 'b');
        AssertByValue((byte)1, (byte)2);
        AssertByValue((sbyte)-1, (sbyte)2);
        AssertByValue((short)-1, (short)2);
        AssertByValue((ushort)1, (ushort)2);
        AssertByValue(-1, 2);
        AssertByValue(1u, 2u);
        AssertByValue(-1L, 2L);
        AssertByValue(1ul, 2ul);
        AssertByValue((nint)(-1), (nint)2);
        AssertByValue((nuint)1, (nuint)2);
        AssertByValue(1.5m, 2.5m);

        // decimal compares by value, whatever the scale.
        Assert.True(Equality.Is(1.0m, 1.00m));
        Assert.True(Equality.StrictEquals<decimal?>(1.0m, 1.00m));
    }

    private static void AssertByValue<T>(T value, T other)
        where T : struct
    {
        T copy = value;
        Assert.True(Equality.Is(value, copy));
        Assert.False(Equality.Is(value, other));
        Assert.True(Equality.StrictEquals(value, copy));
        Assert.False(Equality.StrictEquals(value, other));
        Assert.True(Equality.Is<T?>(value, copy));
        Assert.False(Equality.Is<T?>(value, other));
        Assert.False(Equality.Is<T?>(value, null));
        Assert.False(Equality.Is<T?>(null, value));
        Assert.True(Equality.Is<T?>(null, null));
        Assert.True(Equality.StrictEquals<T?>(value, copy));
        Assert.False(Equality.StrictEquals<T?>(null, value));
        Assert.True(Equality.StrictEquals<T?>(null, null));
    }

    [Fact(DisplayName = "Object.is: strings compare by value")]
    public void Strings_compare_by_value()
    {
        string built = new('a', 2);

        Assert.True(Equality.Is("aa", built));
        Assert.False(Equality.Is("aa", "AA"));
        Assert.True(Equality.Is<string?>(null, null));
        Assert.False(Equality.Is<string?>(null, ""));
    }

    [Fact(DisplayName = "Object.is: NaN equals NaN")]
    public void NaN_equals_NaN()
    {
        Assert.True(Equality.Is(double.NaN, double.NaN));
        Assert.True(Equality.Is(double.NaN, BitConverter.Int64BitsToDouble(unchecked((long)0xFFF8000000000001))));
        Assert.True(Equality.Is(float.NaN, float.NaN));
        Assert.True(Equality.Is<double?>(double.NaN, double.NaN));
        Assert.True(Equality.Is(Half.NaN, Half.NaN));
    }

    [Fact(DisplayName = "Object.is: -0 differs from 0")]
    public void Negative_zero_differs_from_zero()
    {
        Assert.False(Equality.Is(0.0, -0.0));
        Assert.True(Equality.Is(-0.0, -0.0));
        Assert.False(Equality.Is(0f, -0f));
        Assert.False(Equality.Is<double?>(0.0, -0.0));
        Assert.False(Equality.Is<float?>(0f, -0f));
        Assert.False(Equality.Is(Half.Zero, Half.NegativeZero));
    }

    [Fact(DisplayName = "Object.is: records compare by reference, never structurally")]
    public void Records_compare_by_reference()
    {
        Point point = new(1, 2);

        Assert.True(Equality.Is(point, point));
        Assert.False(Equality.Is(point, new Point(1, 2)));
        Assert.Equal(point, new Point(1, 2));
    }

    [Fact(DisplayName = "Object.is: other reference types compare by reference")]
    public void Reference_types_compare_by_reference()
    {
        int[] array = [1];
        Action action = () => { };
        Action sameTargetAndMethod = (Action)Delegate.CreateDelegate(typeof(Action), action.Target, action.Method);

        Assert.True(Equality.Is(array, array));
        Assert.False(Equality.Is(array, [1]));
        Assert.False(Equality.Is(action, sameTargetAndMethod));
        Assert.True(Equality.Is<object?>(null, null));
        Assert.False(Equality.Is<object>(1, 1));
    }

    [Fact(DisplayName = "===: NaN differs from NaN and -0 equals 0")]
    public void Strict_equality_follows_JavaScript()
    {
        Assert.False(Equality.StrictEquals(double.NaN, double.NaN));
        Assert.True(Equality.StrictEquals(0.0, -0.0));
        Assert.False(Equality.StrictEquals(float.NaN, float.NaN));
        Assert.True(Equality.StrictEquals<double?>(0.0, -0.0));
        Assert.True(Equality.StrictEquals("a", "a"));
        Assert.False(Equality.StrictEquals(new Point(1, 2), new Point(1, 2)));
        Assert.True(Equality.StrictEquals(3, 3));
    }

    // ACC-STA-04, ACC-EDGE-10: Base UI's membership test is Array.prototype.includes, whose SameValueZero is Object.is with
    // 0 equal to -0 (settled by the orchestrator 2026-10-25, M3 plan §Settled).
    [Fact(DisplayName = "SameValueZero: NaN equals NaN and -0 equals 0")]
    [Trait("Requirement", "ACC-STA-04")]
    public void SameValueZero_follows_JavaScript()
    {
        Assert.True(Equality.SameValueZero(double.NaN, double.NaN));
        Assert.True(Equality.SameValueZero(0.0, -0.0));
        Assert.True(Equality.SameValueZero(-0.0, 0.0));
        Assert.False(Equality.SameValueZero(0.0, 1.0));
        Assert.True(Equality.SameValueZero(float.NaN, float.NaN));
        Assert.True(Equality.SameValueZero(0f, -0f));
        Assert.True(Equality.SameValueZero(Half.NaN, Half.NaN));
        Assert.True(Equality.SameValueZero(Half.Zero, Half.NegativeZero));
        Assert.True(Equality.SameValueZero<double?>(0.0, -0.0));
        Assert.True(Equality.SameValueZero<double?>(double.NaN, double.NaN));
        Assert.False(Equality.SameValueZero<double?>(null, 0.0));
        Assert.True(Equality.SameValueZero<double?>(null, null));
        Assert.True(Equality.SameValueZero<float?>(-0f, 0f));
        Assert.True(Equality.SameValueZero<Half?>(Half.NegativeZero, Half.Zero));
        Assert.False(Equality.SameValueZero<Half?>(Half.Zero, null));
        Assert.True(Equality.SameValueZero("a", "a"));
        Assert.False(Equality.SameValueZero(new Point(1, 2), new Point(1, 2)));
        Assert.True(Equality.SameValueZero(3, 3));
        Assert.False(Equality.SameValueZero(3, 4));
    }

    // ADR 05 Amendments 2026-10-20 as amended 2026-10-23 (R8): the primitives allocate nothing.
    [Fact(DisplayName = "SameValueZero: the primitives allocate nothing")]
    [Trait("Requirement", "ACC-STA-04")]
    public void SameValueZero_allocates_nothing_for_primitives()
    {
        bool sink = Run();
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 100; i++)
        {
            sink ^= Run();
        }

        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(0, allocated);
        GC.KeepAlive(sink);

        static bool Run() =>
            Equality.SameValueZero(1, 1)
            & Equality.SameValueZero<int?>(1, null)
            & Equality.SameValueZero(0.0, -0.0)
            & Equality.SameValueZero<double?>(double.NaN, double.NaN)
            & Equality.SameValueZero(true, true)
            & Equality.SameValueZero('a', 'a')
            & Equality.SameValueZero(1m, 1m)
            & Equality.SameValueZero("a", "a");
    }
}

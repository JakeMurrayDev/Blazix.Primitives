using Blazix.Primitives.Store;

namespace Blazix.Primitives.Tests.Store;

/// <summary>
/// ADR 05 §1's <c>Is&lt;T&gt;</c> against <c>Object.is</c> (UTL §8.2): value types and strings by value, every other
/// reference type by reference, records never structurally, <c>NaN</c> equal to itself and <c>-0</c> distinct from <c>0</c>.
/// <c>StrictEquals</c> is JavaScript's <c>===</c>, used where Base UI compares with <c>!==</c> (UTL-STA-19, UTL-STA-36).
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
}

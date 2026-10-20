using System.Runtime.CompilerServices;

namespace Blazix.Primitives.Store;

// ADR 05 §1 (Is<T>)
/// <summary>
/// The store's equality: JavaScript's <c>Object.is</c> decided by the static type, with no
/// boxing. Value types compare by value through <see cref="EqualityComparer{T}.Default"/>, except that floating-point
/// <c>NaN</c> equals <c>NaN</c> and <c>0</c> differs from <c>-0</c> as in <c>Object.is</c>; <see cref="string"/> compares by
/// value (ordinal); every other reference type, records included, compares by reference, never structurally. A value
/// whose static type is <see cref="object"/> or an interface compares by reference even when it holds a boxed value.
/// </summary>
public static class Equality
{
    /// <summary><c>Object.is(left, right)</c> for the static type <typeparamref name="T"/>.</summary>
    public static bool Is<T>(T left, T right)
    {
        // Every branch below is a JIT-time constant for a value-type T, so a call compiles to one comparison.
        if (typeof(T).IsValueType)
        {
            if (typeof(T) == typeof(double))
            {
                return SameDouble(Unsafe.As<T, double>(ref left), Unsafe.As<T, double>(ref right));
            }

            if (typeof(T) == typeof(float))
            {
                return SameSingle(Unsafe.As<T, float>(ref left), Unsafe.As<T, float>(ref right));
            }

            if (typeof(T) == typeof(Half))
            {
                return SameHalf(Unsafe.As<T, Half>(ref left), Unsafe.As<T, Half>(ref right));
            }

            if (typeof(T) == typeof(double?))
            {
                double? l = Unsafe.As<T, double?>(ref left);
                double? r = Unsafe.As<T, double?>(ref right);
                return l.HasValue ? r.HasValue && SameDouble(l.GetValueOrDefault(), r.GetValueOrDefault()) : !r.HasValue;
            }

            if (typeof(T) == typeof(float?))
            {
                float? l = Unsafe.As<T, float?>(ref left);
                float? r = Unsafe.As<T, float?>(ref right);
                return l.HasValue ? r.HasValue && SameSingle(l.GetValueOrDefault(), r.GetValueOrDefault()) : !r.HasValue;
            }

            if (typeof(T) == typeof(Half?))
            {
                Half? l = Unsafe.As<T, Half?>(ref left);
                Half? r = Unsafe.As<T, Half?>(ref right);
                return l.HasValue ? r.HasValue && SameHalf(l.GetValueOrDefault(), r.GetValueOrDefault()) : !r.HasValue;
            }

            return EqualityComparer<T>.Default.Equals(left, right);
        }

        if (typeof(T) == typeof(string))
        {
            return string.Equals((string?)(object?)left, (string?)(object?)right, StringComparison.Ordinal);
        }

        return ReferenceEquals(left, right);
    }

    /// <summary>JavaScript's <c>===</c>: as <see cref="Is{T}(T, T)"/>, except that <c>NaN</c> differs from itself and <c>0</c> equals <c>-0</c>.</summary>
    internal static bool StrictEquals<T>(T left, T right)
    {
        if (typeof(T) == typeof(double))
        {
            return Unsafe.As<T, double>(ref left) == Unsafe.As<T, double>(ref right);
        }

        if (typeof(T) == typeof(float))
        {
            return Unsafe.As<T, float>(ref left) == Unsafe.As<T, float>(ref right);
        }

        if (typeof(T) == typeof(Half))
        {
            return Unsafe.As<T, Half>(ref left) == Unsafe.As<T, Half>(ref right);
        }

        if (typeof(T) == typeof(double?))
        {
            return Unsafe.As<T, double?>(ref left) == Unsafe.As<T, double?>(ref right);
        }

        if (typeof(T) == typeof(float?))
        {
            return Unsafe.As<T, float?>(ref left) == Unsafe.As<T, float?>(ref right);
        }

        if (typeof(T) == typeof(Half?))
        {
            return Unsafe.As<T, Half?>(ref left) == Unsafe.As<T, Half?>(ref right);
        }

        return Is(left, right);
    }

    // Object.is on IEEE values: identical bits, or both NaN (JavaScript has one NaN, so the payload does not matter).
    private static bool SameDouble(double left, double right) =>
        BitConverter.DoubleToInt64Bits(left) == BitConverter.DoubleToInt64Bits(right) || (double.IsNaN(left) && double.IsNaN(right));

    private static bool SameSingle(float left, float right) =>
        BitConverter.SingleToInt32Bits(left) == BitConverter.SingleToInt32Bits(right) || (float.IsNaN(left) && float.IsNaN(right));

    private static bool SameHalf(Half left, Half right) =>
        BitConverter.HalfToInt16Bits(left) == BitConverter.HalfToInt16Bits(right) || (Half.IsNaN(left) && Half.IsNaN(right));
}

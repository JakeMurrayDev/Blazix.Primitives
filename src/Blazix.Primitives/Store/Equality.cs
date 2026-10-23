using System.Runtime.CompilerServices;

namespace Blazix.Primitives.Store;

// ADR 05 §1 (Is<T>)
/// <summary>
/// Provides the store's equality: JavaScript's <c>Object.is</c>, decided by the static type.
/// </summary>
/// <remarks>
/// The comparison never boxes a value.
/// Value types compare by value: the built-in numeric types, <see cref="bool"/>, <see cref="char"/> and their nullable forms
/// with their equality operator, every other value type through <see cref="EqualityComparer{T}.Default"/>. A floating-point
/// <c>NaN</c> equals <c>NaN</c> and <c>0</c> differs from <c>-0</c>, as in <c>Object.is</c>. <see cref="string"/> compares by ordinal
/// value. Every other reference type, records included, compares by reference, never structurally. A value whose static
/// type is <see cref="object"/> or an interface compares by reference even when it holds a boxed value.
/// </remarks>
public static class Equality
{
    /// <summary>
    /// Determines whether two values are the same by JavaScript's <c>Object.is</c> for the static type <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The type of the values, which decides how they are compared.</typeparam>
    /// <param name="left">The first value.</param>
    /// <param name="right">The second value.</param>
    /// <returns><see langword="true"/> if <paramref name="left"/> and <paramref name="right"/> are the same; otherwise, <see langword="false"/>.</returns>
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

            if (TryEqualsPrimitive(ref left, ref right, out bool equal))
            {
                return equal;
            }

            return EqualityComparer<T>.Default.Equals(left, right);
        }

        if (typeof(T) == typeof(string))
        {
            return string.Equals((string?)(object?)left, (string?)(object?)right, StringComparison.Ordinal);
        }

        return ReferenceEquals(left, right);
    }

    /// <summary>
    /// Determines whether two values are equal by JavaScript's <c>===</c>.
    /// </summary>
    /// <typeparam name="T">The type of the values, which decides how they are compared.</typeparam>
    /// <param name="left">The first value.</param>
    /// <param name="right">The second value.</param>
    /// <returns>
    /// The result of <see cref="Is{T}(T, T)"/>, except that <c>NaN</c> is never equal to itself and <c>0</c> equals <c>-0</c>.
    /// </returns>
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

    // ADR 05 Amendments 2026-10-20 as amended 2026-10-23 (design point R8 of M2 tracks 7-8, settled by the orchestrator
    // 2026-10-23, the user's no-boxing rule): EqualityComparer<T>.Default boxes on the WebAssembly interpreter (48 bytes per
    // int comparison, measured by the Radio probe), so the BCL primitives and their nullables compare with == through
    // Unsafe.As, as the floating-point branches do. Each branch is a JIT-time constant for a value-type T; the interpreter
    // tests the types in turn, which allocates nothing. Other value types, record structs included, still box there.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool TryEqualsPrimitive<T>(ref T left, ref T right, out bool equal)
    {
        if (typeof(T) == typeof(int))
        {
            equal = Unsafe.As<T, int>(ref left) == Unsafe.As<T, int>(ref right);
            return true;
        }

        if (typeof(T) == typeof(int?))
        {
            equal = Unsafe.As<T, int?>(ref left) == Unsafe.As<T, int?>(ref right);
            return true;
        }

        if (typeof(T) == typeof(long))
        {
            equal = Unsafe.As<T, long>(ref left) == Unsafe.As<T, long>(ref right);
            return true;
        }

        if (typeof(T) == typeof(long?))
        {
            equal = Unsafe.As<T, long?>(ref left) == Unsafe.As<T, long?>(ref right);
            return true;
        }

        if (typeof(T) == typeof(bool))
        {
            equal = Unsafe.As<T, bool>(ref left) == Unsafe.As<T, bool>(ref right);
            return true;
        }

        if (typeof(T) == typeof(bool?))
        {
            equal = Unsafe.As<T, bool?>(ref left) == Unsafe.As<T, bool?>(ref right);
            return true;
        }

        if (typeof(T) == typeof(char))
        {
            equal = Unsafe.As<T, char>(ref left) == Unsafe.As<T, char>(ref right);
            return true;
        }

        if (typeof(T) == typeof(char?))
        {
            equal = Unsafe.As<T, char?>(ref left) == Unsafe.As<T, char?>(ref right);
            return true;
        }

        if (typeof(T) == typeof(decimal))
        {
            equal = Unsafe.As<T, decimal>(ref left) == Unsafe.As<T, decimal>(ref right);
            return true;
        }

        if (typeof(T) == typeof(decimal?))
        {
            equal = Unsafe.As<T, decimal?>(ref left) == Unsafe.As<T, decimal?>(ref right);
            return true;
        }

        if (typeof(T) == typeof(byte))
        {
            equal = Unsafe.As<T, byte>(ref left) == Unsafe.As<T, byte>(ref right);
            return true;
        }

        if (typeof(T) == typeof(byte?))
        {
            equal = Unsafe.As<T, byte?>(ref left) == Unsafe.As<T, byte?>(ref right);
            return true;
        }

        if (typeof(T) == typeof(sbyte))
        {
            equal = Unsafe.As<T, sbyte>(ref left) == Unsafe.As<T, sbyte>(ref right);
            return true;
        }

        if (typeof(T) == typeof(sbyte?))
        {
            equal = Unsafe.As<T, sbyte?>(ref left) == Unsafe.As<T, sbyte?>(ref right);
            return true;
        }

        if (typeof(T) == typeof(short))
        {
            equal = Unsafe.As<T, short>(ref left) == Unsafe.As<T, short>(ref right);
            return true;
        }

        if (typeof(T) == typeof(short?))
        {
            equal = Unsafe.As<T, short?>(ref left) == Unsafe.As<T, short?>(ref right);
            return true;
        }

        if (typeof(T) == typeof(ushort))
        {
            equal = Unsafe.As<T, ushort>(ref left) == Unsafe.As<T, ushort>(ref right);
            return true;
        }

        if (typeof(T) == typeof(ushort?))
        {
            equal = Unsafe.As<T, ushort?>(ref left) == Unsafe.As<T, ushort?>(ref right);
            return true;
        }

        if (typeof(T) == typeof(uint))
        {
            equal = Unsafe.As<T, uint>(ref left) == Unsafe.As<T, uint>(ref right);
            return true;
        }

        if (typeof(T) == typeof(uint?))
        {
            equal = Unsafe.As<T, uint?>(ref left) == Unsafe.As<T, uint?>(ref right);
            return true;
        }

        if (typeof(T) == typeof(ulong))
        {
            equal = Unsafe.As<T, ulong>(ref left) == Unsafe.As<T, ulong>(ref right);
            return true;
        }

        if (typeof(T) == typeof(ulong?))
        {
            equal = Unsafe.As<T, ulong?>(ref left) == Unsafe.As<T, ulong?>(ref right);
            return true;
        }

        if (typeof(T) == typeof(nint))
        {
            equal = Unsafe.As<T, nint>(ref left) == Unsafe.As<T, nint>(ref right);
            return true;
        }

        if (typeof(T) == typeof(nint?))
        {
            equal = Unsafe.As<T, nint?>(ref left) == Unsafe.As<T, nint?>(ref right);
            return true;
        }

        if (typeof(T) == typeof(nuint))
        {
            equal = Unsafe.As<T, nuint>(ref left) == Unsafe.As<T, nuint>(ref right);
            return true;
        }

        if (typeof(T) == typeof(nuint?))
        {
            equal = Unsafe.As<T, nuint?>(ref left) == Unsafe.As<T, nuint?>(ref right);
            return true;
        }

        equal = false;
        return false;
    }

    // Object.is on IEEE values: identical bits, or both NaN (JavaScript has one NaN, so the payload does not matter).
    private static bool SameDouble(double left, double right) =>
        BitConverter.DoubleToInt64Bits(left) == BitConverter.DoubleToInt64Bits(right) || (double.IsNaN(left) && double.IsNaN(right));

    private static bool SameSingle(float left, float right) =>
        BitConverter.SingleToInt32Bits(left) == BitConverter.SingleToInt32Bits(right) || (float.IsNaN(left) && float.IsNaN(right));

    private static bool SameHalf(Half left, Half right) =>
        BitConverter.HalfToInt16Bits(left) == BitConverter.HalfToInt16Bits(right) || (Half.IsNaN(left) && Half.IsNaN(right));
}

using System.Runtime.CompilerServices;

namespace Blazix.Primitives;

// TAB-API-02, TAB-STA-08; tabs/root/TabsRoot.tsx:30 (`defaultValue = 0`). ADR 08 Amendments 2026-10-26 (chosen by the
// user 2026-10-26): while a Root has neither Value nor DefaultValue it holds an implicit default that is not a TValue,
// and a Tab matches it only when TValue is a numeric primitive and the Tab's value is zero, compared without boxing.
// JavaScript's `0 === value`: -0 matches, NaN does not; an enum, char, bool or BigInteger is not a JavaScript number,
// so none matches. Design point 3 of the Tabs track (the branches and this place, Tabs/ rather than Store/Equality.cs)
// was settled by the orchestrator 2026-10-26: one consumer, so no shared-path helper; a second family that needs it
// moves it to Store/. The branches use Unsafe.As as Equality's do (design point R8 of M2, settled by the orchestrator
// 2026-10-23): each is a JIT-time constant for a value-type TValue, and the interpreter tests them in turn without
// allocating.
/// <summary>Decides whether a tab's value matches the implicit default of a root that has neither a value nor a default value.</summary>
internal static class TabsImplicitDefault
{
    /// <summary>Determines whether a value equals the number zero, as JavaScript's <c>0 === value</c>.</summary>
    /// <typeparam name="TValue">The type of the value; only the numeric primitives and their nullable forms can match.</typeparam>
    /// <param name="value">The tab's value.</param>
    /// <returns><see langword="true"/> for a numeric zero, negative zero included; otherwise, <see langword="false"/>.</returns>
    public static bool Matches<TValue>(TValue value)
    {
        if (!typeof(TValue).IsValueType)
        {
            return false;
        }

        if (typeof(TValue) == typeof(int))
        {
            return Unsafe.As<TValue, int>(ref value) == 0;
        }

        if (typeof(TValue) == typeof(long))
        {
            return Unsafe.As<TValue, long>(ref value) == 0;
        }

        if (typeof(TValue) == typeof(double))
        {
            return Unsafe.As<TValue, double>(ref value) == 0;
        }

        if (typeof(TValue) == typeof(float))
        {
            return Unsafe.As<TValue, float>(ref value) == 0;
        }

        if (typeof(TValue) == typeof(decimal))
        {
            return Unsafe.As<TValue, decimal>(ref value) == 0;
        }

        if (typeof(TValue) == typeof(short))
        {
            return Unsafe.As<TValue, short>(ref value) == 0;
        }

        if (typeof(TValue) == typeof(sbyte))
        {
            return Unsafe.As<TValue, sbyte>(ref value) == 0;
        }

        if (typeof(TValue) == typeof(byte))
        {
            return Unsafe.As<TValue, byte>(ref value) == 0;
        }

        if (typeof(TValue) == typeof(ushort))
        {
            return Unsafe.As<TValue, ushort>(ref value) == 0;
        }

        if (typeof(TValue) == typeof(uint))
        {
            return Unsafe.As<TValue, uint>(ref value) == 0;
        }

        if (typeof(TValue) == typeof(ulong))
        {
            return Unsafe.As<TValue, ulong>(ref value) == 0;
        }

        if (typeof(TValue) == typeof(nint))
        {
            return Unsafe.As<TValue, nint>(ref value) == 0;
        }

        if (typeof(TValue) == typeof(nuint))
        {
            return Unsafe.As<TValue, nuint>(ref value) == 0;
        }

        if (typeof(TValue) == typeof(Half))
        {
            return Unsafe.As<TValue, Half>(ref value) == Half.Zero;
        }

        return MatchesNullable(ref value);
    }

    private static bool MatchesNullable<TValue>(ref TValue value)
    {
        if (typeof(TValue) == typeof(int?))
        {
            return Unsafe.As<TValue, int?>(ref value) == 0;
        }

        if (typeof(TValue) == typeof(long?))
        {
            return Unsafe.As<TValue, long?>(ref value) == 0;
        }

        if (typeof(TValue) == typeof(double?))
        {
            return Unsafe.As<TValue, double?>(ref value) == 0;
        }

        if (typeof(TValue) == typeof(float?))
        {
            return Unsafe.As<TValue, float?>(ref value) == 0;
        }

        if (typeof(TValue) == typeof(decimal?))
        {
            return Unsafe.As<TValue, decimal?>(ref value) == 0;
        }

        if (typeof(TValue) == typeof(short?))
        {
            return Unsafe.As<TValue, short?>(ref value) == 0;
        }

        if (typeof(TValue) == typeof(sbyte?))
        {
            return Unsafe.As<TValue, sbyte?>(ref value) == 0;
        }

        if (typeof(TValue) == typeof(byte?))
        {
            return Unsafe.As<TValue, byte?>(ref value) == 0;
        }

        if (typeof(TValue) == typeof(ushort?))
        {
            return Unsafe.As<TValue, ushort?>(ref value) == 0;
        }

        if (typeof(TValue) == typeof(uint?))
        {
            return Unsafe.As<TValue, uint?>(ref value) == 0;
        }

        if (typeof(TValue) == typeof(ulong?))
        {
            return Unsafe.As<TValue, ulong?>(ref value) == 0;
        }

        if (typeof(TValue) == typeof(nint?))
        {
            return Unsafe.As<TValue, nint?>(ref value) == 0;
        }

        if (typeof(TValue) == typeof(nuint?))
        {
            return Unsafe.As<TValue, nuint?>(ref value) == 0;
        }

        if (typeof(TValue) == typeof(Half?))
        {
            return Unsafe.As<TValue, Half?>(ref value) == Half.Zero;
        }

        return false;
    }
}

namespace Blazix.Primitives.Store;

/// <summary>
/// The store's equality, ADR 05 §1's <c>Is&lt;T&gt;</c>: JavaScript's <c>Object.is</c> decided by the static type, with no
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
        throw new NotImplementedException();
    }

    /// <summary>JavaScript's <c>===</c>: as <see cref="Is{T}(T, T)"/>, except that <c>NaN</c> differs from itself and <c>0</c> equals <c>-0</c>.</summary>
    internal static bool StrictEquals<T>(T left, T right)
    {
        throw new NotImplementedException();
    }
}

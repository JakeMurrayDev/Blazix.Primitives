using System.Collections;
using System.Collections.Immutable;

namespace Blazix.Primitives.Generators;

/// <summary>Represents an immutable array with element-wise equality.</summary>
/// <typeparam name="T">The type of the elements in the array.</typeparam>
/// <remarks>A generator model that holds one stays cacheable by the incremental pipeline.</remarks>
internal readonly struct EquatableArray<T> : IEquatable<EquatableArray<T>>, IReadOnlyList<T>
    where T : IEquatable<T>
{
    private readonly ImmutableArray<T> items;

    /// <summary>Initializes a new instance of the <see cref="EquatableArray{T}"/> struct.</summary>
    /// <param name="items">The elements of the array; a default array is treated as empty.</param>
    public EquatableArray(ImmutableArray<T> items)
    {
        this.items = items.IsDefault ? ImmutableArray<T>.Empty : items;
    }

    /// <inheritdoc />
    public int Count => items.Length;

    /// <inheritdoc />
    public T this[int index] => items[index];

    /// <inheritdoc />
    public bool Equals(EquatableArray<T> other)
    {
        if (items.Length != other.items.Length)
        {
            return false;
        }

        for (int i = 0; i < items.Length; i++)
        {
            if (!items[i].Equals(other.items[i]))
            {
                return false;
            }
        }

        return true;
    }

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is EquatableArray<T> other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode()
    {
        int hash = 17;
        for (int i = 0; i < items.Length; i++)
        {
            hash = unchecked((hash * 31) + items[i].GetHashCode());
        }

        return hash;
    }

    /// <summary>
    /// Returns an enumerator that iterates through the array.
    /// </summary>
    /// <returns>An enumerator positioned before the first element.</returns>
    /// <remarks>The enumerator is a struct, so iterating allocates nothing.</remarks>
    public ImmutableArray<T>.Enumerator GetEnumerator() => items.GetEnumerator();

    /// <inheritdoc />
    IEnumerator<T> IEnumerable<T>.GetEnumerator() => ((IEnumerable<T>)items).GetEnumerator();

    /// <inheritdoc />
    IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable)items).GetEnumerator();
}

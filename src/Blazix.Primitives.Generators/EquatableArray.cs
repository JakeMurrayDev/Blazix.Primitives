using System.Collections;
using System.Collections.Immutable;

namespace Blazix.Primitives.Generators;

/// <summary>An immutable array with element-wise equality, so a generator model that holds one stays cacheable by the incremental pipeline.</summary>
internal readonly struct EquatableArray<T> : IEquatable<EquatableArray<T>>, IReadOnlyList<T>
    where T : IEquatable<T>
{
    private readonly ImmutableArray<T> items;

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

    /// <inheritdoc />
    public ImmutableArray<T>.Enumerator GetEnumerator() => items.GetEnumerator();

    /// <inheritdoc />
    IEnumerator<T> IEnumerable<T>.GetEnumerator() => ((IEnumerable<T>)items).GetEnumerator();

    /// <inheritdoc />
    IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable)items).GetEnumerator();
}

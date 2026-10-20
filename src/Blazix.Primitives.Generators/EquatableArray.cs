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

    public int Count => items.Length;

    public T this[int index] => items[index];

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

    public override bool Equals(object? obj) => obj is EquatableArray<T> other && Equals(other);

    public override int GetHashCode()
    {
        int hash = 17;
        for (int i = 0; i < items.Length; i++)
        {
            hash = unchecked((hash * 31) + items[i].GetHashCode());
        }

        return hash;
    }

    public ImmutableArray<T>.Enumerator GetEnumerator() => items.GetEnumerator();

    IEnumerator<T> IEnumerable<T>.GetEnumerator() => ((IEnumerable<T>)items).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable)items).GetEnumerator();
}

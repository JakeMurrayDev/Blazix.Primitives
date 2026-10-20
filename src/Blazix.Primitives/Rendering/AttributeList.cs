using System.Collections;
using Microsoft.AspNetCore.Components;

namespace Blazix.Primitives.Rendering;

/// <summary>One entry of an <see cref="AttributeList"/>.</summary>
public readonly record struct AttributeEntry(string Name, AttributeValue Value);

/// <summary>
/// The typed entry list the pipeline and <see cref="MergeProps"/> work on (ADR 02 Amendments 2026-10-17): ordered,
/// keyed by attribute name (ordinal), with replace-in-place so a later value keeps the position at which the name was
/// first seen. Setting an absent value removes the entry: Blazor emits no frame for <c>null</c>, and RND-DOM-19's
/// "explicit undefined still overrides" is relaxed for the port (ADR 02). Names are compared as given; the merge
/// normalises the names it treats specially (<c>class</c>, <c>style</c>, handlers).
/// </summary>
public sealed class AttributeList : IReadOnlyList<AttributeEntry>
{
    private const int DefaultCapacity = 8;

    private AttributeEntry[] entries;
    private int count;

    public AttributeList()
        : this(DefaultCapacity)
    {
    }

    public AttributeList(int capacity)
    {
        entries = capacity == 0 ? [] : new AttributeEntry[capacity];
    }

    public int Count => count;

    public AttributeEntry this[int index]
    {
        get
        {
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual((uint)index, (uint)count);
            return entries[index];
        }
    }

    /// <summary>The position of <paramref name="name"/>, or -1.</summary>
    public int IndexOf(string name)
    {
        AttributeEntry[] items = entries;
        for (int i = 0; i < count; i++)
        {
            if (string.Equals(items[i].Name, name, StringComparison.Ordinal))
            {
                return i;
            }
        }

        return -1;
    }

    public bool Contains(string name) => IndexOf(name) >= 0;

    public bool TryGetValue(string name, out AttributeValue value)
    {
        int index = IndexOf(name);
        if (index < 0)
        {
            value = default;
            return false;
        }

        value = entries[index].Value;
        return true;
    }

    /// <summary>Sets <paramref name="name"/> to <paramref name="value"/>, keeping the first-seen position; an absent value removes the entry.</summary>
    public void Set(string name, AttributeValue value)
    {
        ArgumentNullException.ThrowIfNull(name);
        int index = IndexOf(name);
        if (!value.HasValue)
        {
            if (index >= 0)
            {
                RemoveAt(index);
            }

            return;
        }

        if (index >= 0)
        {
            entries[index] = new AttributeEntry(name, value);
            return;
        }

        if (count == entries.Length)
        {
            Array.Resize(ref entries, Math.Max(DefaultCapacity, count * 2));
        }

        entries[count++] = new AttributeEntry(name, value);
    }

    public void Set(string name, string? value) => Set(name, value is null ? default : new AttributeValue(value));

    public void Set(string name, bool value) => Set(name, new AttributeValue(value));

    public void Set(string name, EventCallback value) => Set(name, new AttributeValue(value));

    public void Set(string name, MulticastDelegate? value) => Set(name, value is null ? default : new AttributeValue(value));

    public bool Remove(string name)
    {
        int index = IndexOf(name);
        if (index < 0)
        {
            return false;
        }

        RemoveAt(index);
        return true;
    }

    public void Clear()
    {
        Array.Clear(entries, 0, count);
        count = 0;
    }

    /// <summary>Copies every entry of <paramref name="source"/> into this list in order, replacing same-named entries.</summary>
    public void SetRange(AttributeList source)
    {
        ArgumentNullException.ThrowIfNull(source);
        for (int i = 0; i < source.count; i++)
        {
            Set(source.entries[i].Name, source.entries[i].Value);
        }
    }

    /// <summary>The entries as the dictionary shape Blazor's <c>@attributes</c> accepts; the case values are boxed (ADR 05 §4, an accepted cost of the substitute callback).</summary>
    public Dictionary<string, object> ToDictionary()
    {
        Dictionary<string, object> result = new(count, StringComparer.Ordinal);
        for (int i = 0; i < count; i++)
        {
            result[entries[i].Name] = entries[i].Value.ToObject()!;
        }

        return result;
    }

    public Enumerator GetEnumerator() => new(this);

    IEnumerator<AttributeEntry> IEnumerable<AttributeEntry>.GetEnumerator() => GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    private void RemoveAt(int index)
    {
        count--;
        if (index < count)
        {
            Array.Copy(entries, index + 1, entries, index, count - index);
        }

        entries[count] = default;
    }

    public struct Enumerator : IEnumerator<AttributeEntry>
    {
        private readonly AttributeList list;
        private int index;

        internal Enumerator(AttributeList list)
        {
            this.list = list;
            index = -1;
        }

        public readonly AttributeEntry Current => list.entries[index];

        readonly object IEnumerator.Current => Current;

        public bool MoveNext() => ++index < list.count;

        public void Reset() => index = -1;

        public readonly void Dispose()
        {
        }
    }
}

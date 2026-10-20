using System.Collections;
using Microsoft.AspNetCore.Components;

namespace Blazix.Primitives.Rendering;

/// <summary>Represents one entry of an <see cref="AttributeList"/>: an attribute name and its value.</summary>
/// <param name="Name">The attribute name.</param>
/// <param name="Value">The attribute value.</param>
public readonly record struct AttributeEntry(string Name, AttributeValue Value);

// ADR 02 Amendments 2026-10-17; ADR 02; RND-DOM-19 (relaxed)
/// <summary>
/// Represents the ordered, name-keyed list of attributes that the render pipeline and <see cref="MergeProps"/> work on.
/// </summary>
/// <remarks>
/// <para>
/// Entries are keyed by attribute name with ordinal comparison. Setting a name that is already present replaces its value
/// in place, so a later value keeps the position at which the name was first seen.
/// </para>
/// <para>
/// Setting an absent value (one whose <see cref="AttributeValue.HasValue"/> is <see langword="false"/>) removes the entry,
/// because Blazor emits no frame for <see langword="null"/>; an absent value therefore cannot override an earlier one.
/// </para>
/// <para>
/// Names are compared as given. The merge normalizes the names it treats specially (<c>class</c>, <c>style</c> and
/// handlers).
/// </para>
/// </remarks>
public sealed class AttributeList : IReadOnlyList<AttributeEntry>
{
    private const int DefaultCapacity = 8;

    private AttributeEntry[] entries;
    private int count;

    /// <summary>Initializes a new instance of the <see cref="AttributeList"/> class with room for eight entries.</summary>
    public AttributeList()
        : this(DefaultCapacity)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="AttributeList"/> class with the specified initial capacity.</summary>
    /// <param name="capacity">The number of entries the list can hold before it grows.</param>
    public AttributeList(int capacity)
    {
        entries = capacity == 0 ? [] : new AttributeEntry[capacity];
    }

    /// <inheritdoc />
    public int Count => count;

    /// <inheritdoc />
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is negative or not less than <see cref="Count"/>.</exception>
    public AttributeEntry this[int index]
    {
        get
        {
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual((uint)index, (uint)count);
            return entries[index];
        }
    }

    /// <summary>Returns the position of the entry named <paramref name="name"/>.</summary>
    /// <param name="name">The attribute name, compared ordinally.</param>
    /// <returns>The zero-based index of the entry, or <c>-1</c> if the list has no entry with that name.</returns>
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

    /// <summary>Determines whether the list has an entry named <paramref name="name"/>.</summary>
    /// <param name="name">The attribute name, compared ordinally.</param>
    /// <returns><see langword="true"/> if the list has an entry with that name; otherwise, <see langword="false"/>.</returns>
    public bool Contains(string name) => IndexOf(name) >= 0;

    /// <summary>Gets the value of the entry named <paramref name="name"/>.</summary>
    /// <param name="name">The attribute name, compared ordinally.</param>
    /// <param name="value">
    /// When this method returns, the value of the entry, if one was found; otherwise, the default (absent) value.
    /// </param>
    /// <returns><see langword="true"/> if the list has an entry with that name; otherwise, <see langword="false"/>.</returns>
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

    /// <summary>Sets the entry named <paramref name="name"/> to <paramref name="value"/>.</summary>
    /// <param name="name">The attribute name.</param>
    /// <param name="value">The value to set. An absent value removes the entry.</param>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// An existing entry keeps its first-seen position; a new entry is appended. An absent value removes the entry if
    /// there is one.
    /// </remarks>
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

    /// <summary>Sets the entry named <paramref name="name"/> to a string value.</summary>
    /// <param name="name">The attribute name.</param>
    /// <param name="value">The string value, or <see langword="null"/> to remove the entry.</param>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is <see langword="null"/>.</exception>
    /// <remarks>An existing entry keeps its first-seen position; a new entry is appended.</remarks>
    public void Set(string name, string? value) => Set(name, value is null ? default : new AttributeValue(value));

    /// <summary>Sets the entry named <paramref name="name"/> to a Boolean value.</summary>
    /// <param name="name">The attribute name.</param>
    /// <param name="value">The Boolean value.</param>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is <see langword="null"/>.</exception>
    /// <remarks>An existing entry keeps its first-seen position; a new entry is appended.</remarks>
    public void Set(string name, bool value) => Set(name, new AttributeValue(value));

    /// <summary>Sets the entry named <paramref name="name"/> to a callback.</summary>
    /// <param name="name">The attribute name.</param>
    /// <param name="value">The callback. A callback with no delegate is the absent value and removes the entry.</param>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is <see langword="null"/>.</exception>
    /// <remarks>An existing entry keeps its first-seen position; a new entry is appended.</remarks>
    public void Set(string name, EventCallback value) => Set(name, new AttributeValue(value));

    /// <summary>Sets the entry named <paramref name="name"/> to a delegate.</summary>
    /// <param name="name">The attribute name.</param>
    /// <param name="value">The delegate, or <see langword="null"/> to remove the entry.</param>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is <see langword="null"/>.</exception>
    /// <remarks>An existing entry keeps its first-seen position; a new entry is appended.</remarks>
    public void Set(string name, MulticastDelegate? value) => Set(name, value is null ? default : new AttributeValue(value));

    /// <summary>Removes the entry named <paramref name="name"/>.</summary>
    /// <param name="name">The attribute name, compared ordinally.</param>
    /// <returns>
    /// <see langword="true"/> if an entry was removed; <see langword="false"/> if the list had no entry with that name.
    /// </returns>
    /// <remarks>The entries after the removed one move up one position.</remarks>
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

    /// <summary>Removes all entries from the list.</summary>
    /// <remarks>The capacity is kept, so the list can be refilled without allocating.</remarks>
    public void Clear()
    {
        Array.Clear(entries, 0, count);
        count = 0;
    }

    /// <summary>
    /// Copies every entry of <paramref name="source"/> into this list in order, replacing entries with the same name.
    /// </summary>
    /// <param name="source">The list to copy from.</param>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
    public void SetRange(AttributeList source)
    {
        ArgumentNullException.ThrowIfNull(source);
        for (int i = 0; i < source.count; i++)
        {
            Set(source.entries[i].Name, source.entries[i].Value);
        }
    }

    // ADR 05 §4
    /// <summary>Copies the entries into a dictionary of the shape Blazor's <c>@attributes</c> accepts.</summary>
    /// <returns>
    /// A new dictionary keyed by attribute name (ordinal) whose values are the boxed case values of the entries.
    /// </returns>
    /// <remarks>Boxing the case values is an accepted cost of the substitute callback, which receives this dictionary.</remarks>
    public Dictionary<string, object> ToDictionary()
    {
        Dictionary<string, object> result = new(count, StringComparer.Ordinal);
        for (int i = 0; i < count; i++)
        {
            result[entries[i].Name] = entries[i].Value.ToObject()!;
        }

        return result;
    }

    /// <summary>
    /// Returns an enumerator that iterates through the entries in order.
    /// </summary>
    /// <returns>An <see cref="Enumerator"/> positioned before the first entry.</returns>
    /// <remarks>The enumerator is a struct, so iterating allocates nothing.</remarks>
    public Enumerator GetEnumerator() => new(this);

    /// <inheritdoc />
    IEnumerator<AttributeEntry> IEnumerable<AttributeEntry>.GetEnumerator() => GetEnumerator();

    /// <inheritdoc />
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

    /// <summary>Enumerates the entries of an <see cref="AttributeList"/> in order.</summary>
    /// <remarks>The enumerator is a struct, so iterating allocates nothing.</remarks>
    public struct Enumerator : IEnumerator<AttributeEntry>
    {
        private readonly AttributeList list;
        private int index;

        internal Enumerator(AttributeList list)
        {
            this.list = list;
            index = -1;
        }

        /// <inheritdoc />
        public readonly AttributeEntry Current => list.entries[index];

        /// <inheritdoc />
        readonly object IEnumerator.Current => Current;

        /// <inheritdoc />
        public bool MoveNext() => ++index < list.count;

        /// <inheritdoc />
        public void Reset() => index = -1;

        /// <inheritdoc />
        public readonly void Dispose()
        {
        }
    }
}

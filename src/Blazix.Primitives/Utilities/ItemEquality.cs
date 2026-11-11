using System.Runtime.CompilerServices;
using Blazix.Primitives.Store;

namespace Blazix.Primitives.Utilities;

// UTL-ALG-06 (internals/itemEquality.ts), ADR 08 Amendments 2026-11-03 (2): the item equality of Select, Combobox,
// Autocomplete and Menu's filter mode. The default comparer is Object.is (Equality.Is); IsItemEqualToValue is consulted only
// for two non-null operands, item first (SEL-API-22); the dirty test alone compares scalars with ===. The items arrive as a
// span in rendered order (ADR 04 Amendments 2026-11-03 (1)), an absent slot standing for Base UI's `undefined` item and an
// unmounted hole. Single and multiple mode are overloads, so a list-typed TValue is one value outside multiple mode by
// construction. No Contains and no LINQ (ADR 05 Amendments 2026-10-26).
/// <summary>Compares item values with selected values under a list root's comparer.</summary>
internal static class ItemEquality
{
    /// <summary>Compares an item's value with a selected value.</summary>
    /// <typeparam name="TValue">The type of the values.</typeparam>
    /// <param name="itemValue">The item's value.</param>
    /// <param name="selectedValue">The selected value.</param>
    /// <param name="comparer">The root's comparer, or <see langword="null"/> for <see cref="Equality.Is{T}(T, T)"/>.</param>
    /// <returns>
    /// <see cref="Equality.Is{T}(T, T)"/> when either operand is <see langword="null"/>; otherwise the comparer's answer, called
    /// with the item's value first.
    /// </returns>
    public static bool Compare<TValue>(TValue itemValue, TValue selectedValue, Func<TValue, TValue, bool>? comparer)
    {
        if (comparer is null || ValueNull.IsNull(itemValue) || ValueNull.IsNull(selectedValue))
        {
            return Equality.Is(itemValue, selectedValue);
        }

        return comparer(itemValue, selectedValue);
    }

    /// <summary>Gets a value indicating whether a single value differs from its initial value.</summary>
    /// <typeparam name="TValue">The type of the values.</typeparam>
    /// <param name="current">The current value.</param>
    /// <param name="initial">The initial value.</param>
    /// <returns><see langword="true"/> unless the two are equal under <c>===</c>.</returns>
    public static bool IsDirty<TValue>(TValue current, TValue initial) => !Equality.StrictEquals(current, initial);

    /// <summary>Gets a value indicating whether a list of values differs from its initial list.</summary>
    /// <typeparam name="TValue">The type of the values.</typeparam>
    /// <param name="current">The current values, or <see langword="null"/>.</param>
    /// <param name="initial">The initial values, or <see langword="null"/>.</param>
    /// <param name="comparer">The root's comparer, or <see langword="null"/> for the default.</param>
    /// <returns>
    /// For two lists, whether they differ in length or in a value at the same position under <see cref="Compare"/>; otherwise
    /// whether they are not the same reference.
    /// </returns>
    public static bool IsDirty<TValue>(IReadOnlyList<TValue>? current, IReadOnlyList<TValue>? initial, Func<TValue, TValue, bool>? comparer)
    {
        if (current is null || initial is null)
        {
            return !ReferenceEquals(current, initial);
        }

        if (current.Count != initial.Count)
        {
            return true;
        }

        for (int i = 0; i < current.Count; i++)
        {
            if (!Compare(current[i], initial[i], comparer))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Gets a value indicating whether the selected values include an item's value.</summary>
    /// <typeparam name="TValue">The type of the values.</typeparam>
    /// <param name="selectedValues">The selected values, or <see langword="null"/>.</param>
    /// <param name="itemValue">The item's value.</param>
    /// <param name="comparer">The root's comparer, or <see langword="null"/> for the default.</param>
    /// <returns><see langword="true"/> when a selected value compares equal to the item's value.</returns>
    public static bool Includes<TValue>(IReadOnlyList<TValue>? selectedValues, TValue itemValue, Func<TValue, TValue, bool>? comparer)
    {
        if (selectedValues is null)
        {
            return false;
        }

        for (int i = 0; i < selectedValues.Count; i++)
        {
            if (Compare(itemValue, selectedValues[i], comparer))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Finds the first item whose value compares equal to a selected value.</summary>
    /// <typeparam name="TValue">The type of the values.</typeparam>
    /// <param name="items">The item values in rendered order.</param>
    /// <param name="selectedValue">The selected value.</param>
    /// <param name="comparer">The root's comparer, or <see langword="null"/> for the default.</param>
    /// <returns>The item's index, or -1 when none matches. An absent slot never matches.</returns>
    public static int FindItemIndex<TValue>(ReadOnlySpan<ItemSlot<TValue>> items, TValue selectedValue, Func<TValue, TValue, bool>? comparer)
    {
        for (int i = 0; i < items.Length; i++)
        {
            ref readonly ItemSlot<TValue> item = ref items[i];
            if (item.Present && Compare(item.Value, selectedValue, comparer))
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>Finds the item a single selected value selects.</summary>
    /// <typeparam name="TValue">The type of the value.</typeparam>
    /// <param name="items">The item values in rendered order.</param>
    /// <param name="selectedValue">The selected value.</param>
    /// <param name="comparer">The root's comparer, or <see langword="null"/> for the default.</param>
    /// <returns>The first matching item's index, or <see langword="null"/> when none matches.</returns>
    public static int? FindSelectionIndex<TValue>(ReadOnlySpan<ItemSlot<TValue>> items, TValue selectedValue, Func<TValue, TValue, bool>? comparer)
    {
        int index = FindItemIndex(items, selectedValue, comparer);
        return index == -1 ? null : index;
    }

    // UTL-ALG-06: anchored to the first selected item in rendered order, so the index does not depend on the order in which
    // the values were added. Under the default comparer the selected values are read once into a set, as Base UI indexes
    // them (createSelectionMatcher); a custom comparer may match values that do not hash alike, so it scans.
    /// <summary>Finds the first item, in rendered order, that the selected values of a multiple list select.</summary>
    /// <typeparam name="TValue">The type of the values.</typeparam>
    /// <param name="items">The item values in rendered order.</param>
    /// <param name="selectedValues">The selected values.</param>
    /// <param name="comparer">The root's comparer, or <see langword="null"/> for the default.</param>
    /// <returns>The first selected item's index, or <see langword="null"/> when no selected value is rendered.</returns>
    public static int? FindSelectionIndex<TValue>(ReadOnlySpan<ItemSlot<TValue>> items, IReadOnlyList<TValue> selectedValues, Func<TValue, TValue, bool>? comparer)
    {
        if (comparer is not null)
        {
            for (int i = 0; i < items.Length; i++)
            {
                ref readonly ItemSlot<TValue> item = ref items[i];
                if (item.Present && Includes(selectedValues, item.Value, comparer))
                {
                    return i;
                }
            }

            return null;
        }

        SelectionSet<TValue> index = SelectionSet<TValue>.Rent(selectedValues.Count);
        try
        {
            for (int i = 0; i < selectedValues.Count; i++)
            {
                index.Add(selectedValues[i]);
            }

            for (int i = 0; i < items.Length; i++)
            {
                ref readonly ItemSlot<TValue> item = ref items[i];
                if (item.Present && index.Contains(item.Value))
                {
                    return i;
                }
            }

            return null;
        }
        finally
        {
            index.Clear();
        }
    }

    // UTL-ALG-06 (resolveSelectedIndex): the first selected index as items register or change. A later item takes over only
    // once the current anchor stops being selected, and the holder re-elects the anchor once it stops being selected.
    /// <summary>Resolves the first selected index after one item registered or changed its value.</summary>
    /// <typeparam name="TValue">The type of the values.</typeparam>
    /// <param name="index">The item's index.</param>
    /// <param name="itemValue">The item's value.</param>
    /// <param name="items">The item values in rendered order.</param>
    /// <param name="selectedValues">The selected values.</param>
    /// <param name="comparer">The root's comparer, or <see langword="null"/> for the default.</param>
    /// <param name="currentIndex">The index held before the change, or <see langword="null"/>.</param>
    /// <returns>The index that holds the selection now, or <see langword="null"/>.</returns>
    public static int? ResolveSelectedIndex<TValue>(int index, TValue itemValue, ReadOnlySpan<ItemSlot<TValue>> items, IReadOnlyList<TValue> selectedValues, Func<TValue, TValue, bool>? comparer, int? currentIndex)
    {
        if (Includes(selectedValues, itemValue, comparer))
        {
            if (currentIndex is int current && index > current && current < items.Length)
            {
                ref readonly ItemSlot<TValue> holder = ref items[current];
                if (holder.Present && Includes(selectedValues, holder.Value, comparer))
                {
                    return current;
                }
            }

            return index;
        }

        return index == currentIndex ? FindSelectionIndex(items, selectedValues, comparer) : currentIndex;
    }

    /// <summary>Removes every selected value that compares equal to an item's value.</summary>
    /// <typeparam name="TValue">The type of the values.</typeparam>
    /// <param name="selectedValues">The selected values.</param>
    /// <param name="itemValue">The item's value.</param>
    /// <param name="comparer">The root's comparer, or <see langword="null"/> for the default.</param>
    /// <returns>A new array of the other selected values, in their order.</returns>
    public static TValue[] RemoveItem<TValue>(IReadOnlyList<TValue> selectedValues, TValue itemValue, Func<TValue, TValue, bool>? comparer)
    {
        int kept = 0;
        for (int i = 0; i < selectedValues.Count; i++)
        {
            if (!Compare(itemValue, selectedValues[i], comparer))
            {
                kept++;
            }
        }

        TValue[] result = kept == 0 ? [] : new TValue[kept];
        int next = 0;
        for (int i = 0; i < selectedValues.Count && next < kept; i++)
        {
            TValue value = selectedValues[i];
            if (!Compare(itemValue, value, comparer))
            {
                result[next++] = value;
            }
        }

        return result;
    }

    /// <summary>Maps each value of an item list to its first index under <c>Object.is</c>, for a lookup in constant time.</summary>
    /// <typeparam name="TValue">The type of the values.</typeparam>
    public sealed class IndexMap<TValue>
    {
        /// <summary>Builds the map over an item list, replacing the previous one.</summary>
        /// <param name="items">The item values in rendered order; an absent slot maps nothing.</param>
        public void Build(ReadOnlySpan<ItemSlot<TValue>> items)
        {
        }

        /// <summary>Finds the first index of a value.</summary>
        /// <param name="value">The value.</param>
        /// <returns>The index, or -1 when the list does not hold the value.</returns>
        public int IndexOf(TValue value) => -1;
    }

    // The set of selected values under Object.is: open addressing with linear probing, one per thread and value type, emptied
    // after each use, so a lookup allocates nothing once warm. A render runs on one thread at a time and the default comparer
    // calls no consumer code, so a set is never rented twice at once. HashSet<T> is not used: on the WebAssembly interpreter its
    // lookup boxes a value-type item (its `item != null` test; measured 2026-11-04: 16 bytes per item for int and an enum).
    private sealed class SelectionSet<TValue>
    {
        [ThreadStatic]
        private static SelectionSet<TValue>? s_set;

        private TValue[] values = [];
        private bool[] used = [];
        private int shift;
        private int capacity;

        public static SelectionSet<TValue> Rent(int count)
        {
            SelectionSet<TValue> set = s_set ??= new SelectionSet<TValue>();
            set.Reset(count);
            return set;
        }

        public void Add(TValue value)
        {
            int index = Slot(value);
            while (used[index])
            {
                if (Equality.Is(values[index], value))
                {
                    return;
                }

                index = (index + 1) & (capacity - 1);
            }

            used[index] = true;
            values[index] = value;
        }

        public bool Contains(TValue value)
        {
            int index = Slot(value);
            while (used[index])
            {
                if (Equality.Is(values[index], value))
                {
                    return true;
                }

                index = (index + 1) & (capacity - 1);
            }

            return false;
        }

        public void Clear()
        {
            Array.Clear(used, 0, capacity);
            if (RuntimeHelpers.IsReferenceOrContainsReferences<TValue>())
            {
                // The set outlives the call, so it holds no consumer value.
                Array.Clear(values, 0, capacity);
            }
        }

        // A power of two at least twice the count, so a probe ends at an empty slot; Fibonacci hashing spreads close hashes.
        private void Reset(int count)
        {
            int bits = 2;
            while ((1 << bits) < count * 2)
            {
                bits++;
            }

            capacity = 1 << bits;
            shift = 32 - bits;
            if (values.Length < capacity)
            {
                values = new TValue[capacity];
                used = new bool[capacity];
            }
        }

        private int Slot(TValue value) => (int)(((uint)Hash(ref value) * 0x9E3779B9u) >> shift);

        // A hash that agrees with Equality.Is: a reference by identity, a string by its ordinal text, a floating-point number or
        // a decimal by its own hash (+0 and -0, every NaN, and 1.0m and 1.00m each hash alike), an integral primitive or an enum
        // by its bits, and any other value type by its default hash. EqualityComparer<T>.Default.GetHashCode boxes a primitive
        // or an enum on the interpreter (measured 2026-11-04: 16 bytes per item), so those read their bits through Unsafe.As, as
        // Equality.Is compares them.
        private static int Hash(ref TValue value)
        {
            if (!typeof(TValue).IsValueType)
            {
                if (value is null)
                {
                    return 0;
                }

                return typeof(TValue) == typeof(string) ? StringComparer.Ordinal.GetHashCode((string)(object)value) : RuntimeHelpers.GetHashCode(value);
            }

            if (typeof(TValue) == typeof(double))
            {
                return Unsafe.As<TValue, double>(ref value).GetHashCode();
            }

            if (typeof(TValue) == typeof(float))
            {
                return Unsafe.As<TValue, float>(ref value).GetHashCode();
            }

            if (typeof(TValue) == typeof(Half))
            {
                return Unsafe.As<TValue, Half>(ref value).GetHashCode();
            }

            if (typeof(TValue) == typeof(decimal))
            {
                return Unsafe.As<TValue, decimal>(ref value).GetHashCode();
            }

            if (typeof(TValue) == typeof(int?))
            {
                return Unsafe.As<TValue, int?>(ref value).GetHashCode();
            }

            if (typeof(TValue) == typeof(long?))
            {
                return Unsafe.As<TValue, long?>(ref value).GetHashCode();
            }

            if (typeof(TValue).IsPrimitive || typeof(TValue).IsEnum)
            {
                return Unsafe.SizeOf<TValue>() switch
                {
                    1 => Unsafe.As<TValue, byte>(ref value),
                    2 => Unsafe.As<TValue, ushort>(ref value),
                    4 => Unsafe.As<TValue, int>(ref value),
                    _ => Unsafe.As<TValue, long>(ref value).GetHashCode(),
                };
            }

            return EqualityComparer<TValue>.Default.GetHashCode(value!);
        }
    }
}

// UTL-ALG-06's view: one item's value in rendered order, absent for an item that has not registered a value or has unmounted.
/// <summary>Holds the value of one item of a list in rendered order.</summary>
/// <typeparam name="TValue">The type of the value.</typeparam>
/// <param name="Value">The item's value; meaningless when <paramref name="Present"/> is <see langword="false"/>.</param>
/// <param name="Present"><see langword="false"/> for an item that has no value: one not yet registered, or unmounted.</param>
internal readonly record struct ItemSlot<TValue>(TValue Value, bool Present);

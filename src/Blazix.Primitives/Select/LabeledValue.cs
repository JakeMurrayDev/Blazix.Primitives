using System.Collections;
using Blazix.Primitives.Utilities;

namespace Blazix.Primitives;

// SEL-API-19 (select/root/SelectRoot.tsx:698-702, internals/resolveValueLabel.tsx:5-36); ADR 08 Amendments 2026-11-03 (2)
// and (4): Base UI's `{ value, label }` item and its `{ items, ... }` group, typed, with `string` labels; shared by the
// value-typed lists (Select, Combobox, Autocomplete), so not family-prefixed (ADR 08 §3's shared-concept rule). Design point
// N1 of M5 track 9, settled by the orchestrator 2026-11-04.
/// <summary>Represents a value a list can hold and the label shown for it.</summary>
/// <typeparam name="TValue">The type of the value.</typeparam>
/// <param name="Value">The value.</param>
/// <param name="Label">The label shown for the value, or <see langword="null"/> for the value's own text.</param>
public sealed record LabeledValue<TValue>(TValue Value, string? Label);

/// <summary>Represents a group of values a list can hold, with the group's label.</summary>
/// <typeparam name="TValue">The type of the values.</typeparam>
/// <param name="Label">The group's label, or <see langword="null"/>.</param>
/// <param name="Items">The values of the group with their labels.</param>
public sealed record LabeledValueGroup<TValue>(string? Label, IReadOnlyList<LabeledValue<TValue>> Items);

// The public forms read through track 4's view (ValueLabels over LabeledItems<TValue>): built once per parameter change,
// read without allocating (LabeledItem<TValue> is a struct; the group views are built with the list).
/// <summary>Reads a list of labeled values as the label lookup's flat form.</summary>
/// <typeparam name="TValue">The type of the values.</typeparam>
/// <param name="items">The labeled values.</param>
internal sealed class LabeledValueList<TValue>(IReadOnlyList<LabeledValue<TValue>> items) : IReadOnlyList<LabeledItem<TValue>>
{
    /// <inheritdoc />
    public int Count => items.Count;

    /// <inheritdoc />
    public LabeledItem<TValue> this[int index] => items[index] is { } item ? new LabeledItem<TValue>(item.Value, item.Label) : default;

    /// <inheritdoc />
    public IEnumerator<LabeledItem<TValue>> GetEnumerator()
    {
        for (int i = 0; i < items.Count; i++)
        {
            yield return this[i];
        }
    }

    /// <inheritdoc />
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

/// <summary>Reads a list of groups of labeled values as the label lookup's grouped form.</summary>
/// <typeparam name="TValue">The type of the values.</typeparam>
internal sealed class LabeledValueGroupList<TValue> : IReadOnlyList<IReadOnlyList<LabeledItem<TValue>>>
{
    private readonly LabeledValueList<TValue>[] groups;

    /// <summary>Initializes a new instance of the <see cref="LabeledValueGroupList{TValue}"/> class.</summary>
    /// <param name="groups">The groups.</param>
    public LabeledValueGroupList(IReadOnlyList<LabeledValueGroup<TValue>> groups)
    {
        this.groups = new LabeledValueList<TValue>[groups.Count];
        for (int i = 0; i < groups.Count; i++)
        {
            this.groups[i] = new LabeledValueList<TValue>(groups[i]?.Items ?? []);
        }
    }

    /// <inheritdoc />
    public int Count => groups.Length;

    /// <inheritdoc />
    public IReadOnlyList<LabeledItem<TValue>> this[int index] => groups[index];

    /// <inheritdoc />
    public IEnumerator<IReadOnlyList<LabeledItem<TValue>>> GetEnumerator()
    {
        for (int i = 0; i < groups.Length; i++)
        {
            yield return groups[i];
        }
    }

    /// <inheritdoc />
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

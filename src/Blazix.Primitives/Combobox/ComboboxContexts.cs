using Microsoft.AspNetCore.Components;
using Blazix.Primitives.Utilities;

namespace Blazix.Primitives;

// The part-local contexts (CMB §2: Item, Chip, Chips, Group; ADR 08 §3's <Part>Context row), each cascaded IsFixed with a
// listener list (ADR 16 rule 9). The positioner and the portal reuse the shared PositionerContext and PopupPortalContext.
/// <summary>The state an item shares with its indicator.</summary>
internal sealed class ComboboxItemContext
{
    private readonly List<Action> listeners = [];

    /// <summary>Gets a value indicating whether the item is selected.</summary>
    public bool Selected { get; private set; }

    /// <summary>Gets the item's index in the list, or <c>-1</c> before it is known.</summary>
    public int Index { get; private set; } = -1;

    /// <summary>Adds a listener that runs when <see cref="Selected"/> changes.</summary>
    /// <param name="listener">The listener.</param>
    public void AddListener(Action listener) => listeners.Add(listener);

    /// <summary>Removes a listener added with <see cref="AddListener"/>.</summary>
    /// <param name="listener">The listener.</param>
    public void RemoveListener(Action listener) => listeners.Remove(listener);

    /// <summary>Records the item's state and notifies the listeners when its selection changed.</summary>
    /// <param name="selected">Whether the item is selected.</param>
    /// <param name="index">The item's index.</param>
    public void Set(bool selected, int index)
    {
        Index = index;
        if (Selected == selected)
        {
            return;
        }

        Selected = selected;
        foreach (Action listener in listeners.ToArray())
        {
            listener();
        }
    }
}

/// <summary>The state a group shares with its label, independent of the type of its items.</summary>
internal interface IComboboxGroupContext
{
    /// <summary>Gets the slot the label writes its id into.</summary>
    LabelIdSlot LabelId { get; }
}

/// <summary>The state a group shares with its label and a collection inside it.</summary>
/// <typeparam name="TValue">The type of the items' values.</typeparam>
internal sealed class ComboboxGroupContext<TValue> : IComboboxGroupContext
{
    /// <summary>Initializes a new instance of the <see cref="ComboboxGroupContext{TValue}"/> class.</summary>
    /// <param name="labelChanged">Asks the group to render when its label's id changes.</param>
    public ComboboxGroupContext(Action labelChanged)
    {
        LabelId = new LabelIdSlot { Changed = labelChanged };
    }

    /// <inheritdoc />
    public LabelIdSlot LabelId { get; }

    /// <summary>Gets or sets the group's items, or <see langword="null"/> when the group was given none.</summary>
    public IReadOnlyList<TValue>? Items { get; set; }
}

/// <summary>The state a chips container shares with its chips: the registry.</summary>
internal sealed class ComboboxChipsContext
{
    private readonly List<ComboboxChipContext> chips = [];

    private readonly Action changed;

    /// <summary>Initializes a new instance of the <see cref="ComboboxChipsContext"/> class.</summary>
    /// <param name="list">The composite list the chips register in.</param>
    /// <param name="changed">Called when a chip joins, leaves, or changes its id or index.</param>
    public ComboboxChipsContext(CompositeListContext list, Action changed)
    {
        List = list;
        this.changed = changed;
    }

    /// <summary>Gets the composite list the chips register in.</summary>
    public CompositeListContext List { get; }

    /// <summary>Adds a chip.</summary>
    /// <param name="chip">The chip's context.</param>
    public void Add(ComboboxChipContext chip)
    {
        chips.Add(chip);
        changed();
    }

    /// <summary>Removes a chip.</summary>
    /// <param name="chip">The chip's context.</param>
    public void Remove(ComboboxChipContext chip)
    {
        if (chips.Remove(chip))
        {
            changed();
        }
    }

    /// <summary>Records that a chip's id or index changed.</summary>
    public void Changed() => changed();

    /// <summary>Gets the ids of the chips the composite list has indexed, in index order.</summary>
    /// <returns>The ids; an unindexed chip is left out.</returns>
    public string[] OrderedIds()
    {
        List<ComboboxChipContext> indexed = chips.FindAll(static chip => chip.Index >= 0 && chip.Id is not null);
        indexed.Sort(static (left, right) => left.Index.CompareTo(right.Index));
        string[] ids = new string[indexed.Count];
        for (int i = 0; i < ids.Length; i++)
        {
            ids[i] = indexed[i].Id!;
        }

        return ids;
    }
}

/// <summary>The state a chip shares with its remove button.</summary>
internal sealed class ComboboxChipContext
{
    /// <summary>Occurs when the chip's index changes.</summary>
    public event Action? IndexChanged;

    /// <summary>Gets or sets the id the chip renders.</summary>
    public string? Id { get; set; }

    /// <summary>Gets the chip's index among the chips, or -1 before the list indexed it.</summary>
    public int Index { get; private set; } = -1;

    /// <summary>Records the chip's index and notifies the listeners when it changed.</summary>
    /// <param name="index">The index the composite list reported.</param>
    public void SetIndex(int index)
    {
        if (index == Index)
        {
            return;
        }

        Index = index;
        IndexChanged?.Invoke();
    }
}

// combobox/positioner/ComboboxPositionerContext.tsx: the family's own positioner context. The shared PositionerContext is
// cascaded by every family's positioner, so a Combobox inside another family's popup reads this one to tell whether its
// Input is inside a combobox popup and whether its Popup and Arrow are inside a combobox positioner.
/// <summary>Marks the content of a combobox positioner and carries its shared positioner context.</summary>
/// <param name="positioner">The positioner's shared context.</param>
internal sealed class ComboboxPositionerContext(PositionerContext positioner)
{
    /// <summary>Gets the positioner's shared context.</summary>
    public PositionerContext Positioner { get; } = positioner;
}

// combobox/row/ComboboxRow.tsx's ComboboxRowContext: an item inside a row is a gridcell.
/// <summary>Marks the items inside a combobox row.</summary>
internal sealed class ComboboxRowContext
{
    /// <summary>Gets the one instance a row cascades.</summary>
    public static ComboboxRowContext Instance { get; } = new();
}

// CMB §3.23 (ComboboxRow.tsx): a row cascades its context, so its items render as gridcells. The behaviour is the row's whole
// logic, so a shell over it (Autocomplete's Row) declares only its parameters (ADR 08 Amendments 2026-11-03 (3)).
/// <summary>The behavior of a combobox row: the cascade that makes its items gridcells, kept while the content is unchanged.</summary>
internal sealed class ComboboxRowBehavior
{
    private RenderFragment? cascadedContent;

    private RenderFragment? cascade;

    /// <summary>Wraps the row's content in its context's cascade.</summary>
    /// <param name="content">The part's <c>ChildContent</c>.</param>
    /// <returns>The cascade, the same instance while <paramref name="content"/> is.</returns>
    public RenderFragment Cascade(RenderFragment? content)
    {
        if (cascade is null || !ReferenceEquals(cascadedContent, content))
        {
            cascadedContent = content;
            cascade = builder =>
            {
                builder.OpenComponent<CascadingValue<ComboboxRowContext>>(0);
                builder.AddComponentParameter(1, nameof(CascadingValue<ComboboxRowContext>.Value), ComboboxRowContext.Instance);
                builder.AddComponentParameter(2, nameof(CascadingValue<ComboboxRowContext>.IsFixed), true);
                builder.AddComponentParameter(3, nameof(CascadingValue<ComboboxRowContext>.ChildContent), content);
                builder.CloseComponent();
            };
        }

        return cascade;
    }
}

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
    /// <summary>Initializes a new instance of the <see cref="ComboboxChipsContext"/> class.</summary>
    /// <param name="list">The composite list the chips register in.</param>
    public ComboboxChipsContext(CompositeListContext list)
    {
        List = list;
    }

    /// <summary>Gets the composite list the chips register in.</summary>
    public CompositeListContext List { get; }
}

/// <summary>The state a chip shares with its remove button.</summary>
internal sealed class ComboboxChipContext
{
    /// <summary>Gets or sets the chip's index among the chips.</summary>
    public int Index { get; set; } = -1;

    /// <summary>Gets or sets a value indicating whether the chip is disabled.</summary>
    public bool Disabled { get; set; }
}

// combobox/row/ComboboxRow.tsx's ComboboxRowContext: an item inside a row is a gridcell.
/// <summary>Marks the items inside a combobox row.</summary>
internal sealed class ComboboxRowContext
{
    /// <summary>Gets the one instance a row cascades.</summary>
    public static ComboboxRowContext Instance { get; } = new();
}

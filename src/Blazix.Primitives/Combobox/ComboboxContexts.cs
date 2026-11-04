using Blazix.Primitives.Utilities;

namespace Blazix.Primitives;

// The part-local contexts (CMB §2: Item, Chip, Chips, Group; ADR 08 §3's <Part>Context row), each cascaded IsFixed with a
// listener list (ADR 16 rule 9). The positioner and the portal reuse the shared PositionerContext and PopupPortalContext.
/// <summary>The state an item shares with its indicator.</summary>
internal sealed class ComboboxItemContext
{
    /// <summary>Gets a value indicating whether the item is selected.</summary>
    public bool Selected => throw new NotImplementedException();

    /// <summary>Gets the item's index in the list, or <c>-1</c> before it is known.</summary>
    public int Index => throw new NotImplementedException();

    /// <summary>Adds a listener that runs when <see cref="Selected"/> changes.</summary>
    /// <param name="listener">The listener.</param>
    public void AddListener(Action listener) => throw new NotImplementedException();

    /// <summary>Removes a listener added with <see cref="AddListener"/>.</summary>
    /// <param name="listener">The listener.</param>
    public void RemoveListener(Action listener) => throw new NotImplementedException();
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
    /// <inheritdoc />
    public LabelIdSlot LabelId => throw new NotImplementedException();

    /// <summary>Gets the group's items, or <see langword="null"/> when the group was given none.</summary>
    public IReadOnlyList<TValue>? Items => throw new NotImplementedException();
}

/// <summary>The state a chips container shares with its chips: the registry.</summary>
internal sealed class ComboboxChipsContext
{
    /// <summary>Gets the composite list the chips register in.</summary>
    public CompositeListContext List => throw new NotImplementedException();
}

/// <summary>The state a chip shares with its remove button.</summary>
internal sealed class ComboboxChipContext
{
    /// <summary>Gets the chip's index among the chips.</summary>
    public int Index => throw new NotImplementedException();

    /// <summary>Gets a value indicating whether the chip is disabled.</summary>
    public bool Disabled => throw new NotImplementedException();
}

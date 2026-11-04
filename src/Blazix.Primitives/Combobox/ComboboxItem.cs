using Blazix.Primitives.Rendering;

namespace Blazix.Primitives;

// CMB §3.24, STA-13 (combobox/item/ComboboxItem.tsx): Highlighted is the list handle's data-highlighted from attach (ADR 04 Amendments 2026-11-03 (2)): mirrored.
/// <summary>Represents the state of a combobox item, which the part exposes as data attributes and passes to its class and style functions.</summary>
/// <param name="Disabled">A value indicating whether the item is disabled, by the part or the root; projected as <c>data-disabled</c>.</param>
/// <param name="Selected">A value indicating whether the item's value is selected; projected as <c>data-selected</c>.</param>
/// <param name="Highlighted">A value indicating whether the item is highlighted; its <c>data-highlighted</c> is written by the browser-side behavior.</param>
[PartState]
public sealed partial record ComboboxItemState(
    bool Disabled,
    bool Selected,
    [property: MirroredField] bool Highlighted);

// combobox/item/ComboboxItemDataAttributes.ts (CMB-DOM-09).
/// <summary>Provides the names of the data attributes a combobox item renders.</summary>
public static class ComboboxItemDataAttributes
{
    /// <summary>Represents the attribute present while the item is selected: <c>data-selected</c>.</summary>
    public const string Selected = "data-selected";

    /// <summary>Represents the attribute present while the item is highlighted: <c>data-highlighted</c>.</summary>
    public const string Highlighted = "data-highlighted";

    /// <summary>Represents the attribute present while the item is disabled: <c>data-disabled</c>.</summary>
    public const string Disabled = "data-disabled";
}

using Blazix.Primitives.Rendering;

namespace Blazix.Primitives;

// CMB §3.5, STA-03 (combobox/chip/ComboboxChip.tsx).
/// <summary>Represents the state of a combobox chip, which the part exposes as data attributes and passes to its class and style functions.</summary>
/// <param name="Disabled">A value indicating whether the combobox is disabled; projected as <c>data-disabled</c>.</param>
[PartState]
public sealed partial record ComboboxChipState(
    bool Disabled);

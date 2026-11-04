using Blazix.Primitives.Rendering;

namespace Blazix.Primitives;

// CMB §3.6, STA-04 (combobox/chip-remove/ComboboxChipRemove.tsx).
/// <summary>Represents the state of a combobox chip remove button, which the part exposes as data attributes and passes to its class and style functions.</summary>
/// <param name="Disabled">A value indicating whether the button is disabled, by the part or the root; projected as <c>data-disabled</c>.</param>
[PartState]
public sealed partial record ComboboxChipRemoveState(
    bool Disabled);

using Blazix.Primitives.Rendering;

namespace Blazix.Primitives;

// CMB §3.19, STA-12 (combobox/list/ComboboxList.tsx).
/// <summary>Represents the state of a combobox list, which the part exposes as data attributes and passes to its class and style functions.</summary>
/// <param name="Empty">A value indicating whether the filtered list is empty; projected as <c>data-empty</c>.</param>
[PartState]
public sealed partial record ComboboxListState(
    bool Empty);

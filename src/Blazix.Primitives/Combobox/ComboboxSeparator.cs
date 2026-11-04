using Blazix.Primitives.Rendering;

namespace Blazix.Primitives;

// CMB §3.26, STA-15 (combobox/separator/ComboboxSeparator.tsx; ListboxSeparatorAttributes).
/// <summary>Represents the state of a combobox separator, which the part exposes as data attributes and passes to its class and style functions.</summary>
/// <param name="Orientation">The separator's orientation; projected as <c>data-orientation</c>.</param>
[PartState]
public sealed partial record ComboboxSeparatorState(
    Orientation Orientation);

// utils/listbox-separator/ListboxSeparator.tsx (CMB-DOM-16; CMB-API-69 names the class).
/// <summary>Provides the names of the data attributes a combobox separator renders.</summary>
public static class ComboboxSeparatorDataAttributes
{
    /// <summary>Represents the attribute whose value is the separator's orientation: <c>data-orientation</c>.</summary>
    public const string Orientation = "data-orientation";
}

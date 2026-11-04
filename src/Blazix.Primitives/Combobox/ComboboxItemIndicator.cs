using Blazix.Primitives.Rendering;

namespace Blazix.Primitives;

// CMB §3.25, STA-14 (combobox/item-indicator/ComboboxItemIndicator.tsx; ItemIndicatorBehavior): the status mirrored.
/// <summary>Represents the state of a combobox item indicator, which the part exposes as data attributes and passes to its class and style functions.</summary>
/// <param name="Selected">A value indicating whether the item is selected; projected as <c>data-selected</c>.</param>
/// <param name="TransitionStatus">The enter or exit status; its <c>data-starting-style</c> and <c>data-ending-style</c> are written by the browser-side behavior.</param>
[PartState]
public sealed partial record ComboboxItemIndicatorState(
    bool Selected,
    [property: MirroredField] TransitionStatus? TransitionStatus);

// combobox/item-indicator/ComboboxItemIndicatorDataAttributes.ts (CMB-DOM-10).
/// <summary>Provides the names of the data attributes a combobox item indicator renders.</summary>
public static class ComboboxItemIndicatorDataAttributes
{
    /// <summary>Represents the attribute present while the indicator animates in: <c>data-starting-style</c>.</summary>
    public const string StartingStyle = PopupDataAttributes.StartingStyle;

    /// <summary>Represents the attribute present while the indicator animates out: <c>data-ending-style</c>.</summary>
    public const string EndingStyle = PopupDataAttributes.EndingStyle;
}

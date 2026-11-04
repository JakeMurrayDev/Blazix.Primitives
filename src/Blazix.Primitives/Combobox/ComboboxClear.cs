using Blazix.Primitives.Rendering;

namespace Blazix.Primitives;

// CMB §3.8, STA-06 (combobox/clear/ComboboxClear.tsx): the plain trigger mapping (§16 Q8 replicated), the visibility and its transition (mirrored, ANI-04).
/// <summary>Represents the state of a combobox clear button, which the part exposes as data attributes and passes to its class and style functions.</summary>
/// <param name="Open">A value indicating whether the popup is open; projected as <c>data-popup-open</c>.</param>
/// <param name="Disabled">A value indicating whether the button is disabled; projected as <c>data-disabled</c>.</param>
/// <param name="Visible">A value indicating whether there is something to clear; projected as <c>data-visible</c>.</param>
/// <param name="TransitionStatus">The enter or exit status; its <c>data-starting-style</c> and <c>data-ending-style</c> are written by the browser-side behavior.</param>
[PartState]
public sealed partial record ComboboxClearState(
    bool Open,
    bool Disabled,
    bool Visible,
    [property: MirroredField] TransitionStatus? TransitionStatus);

// combobox/clear/ComboboxClearDataAttributes.ts (CMB-DOM-06).
/// <summary>Provides the names of the data attributes a combobox clear button renders.</summary>
public static class ComboboxClearDataAttributes
{
    /// <summary>Represents the attribute present while the popup is open: <c>data-popup-open</c>.</summary>
    public const string PopupOpen = PopupTriggerDataAttributes.PopupOpen;

    /// <summary>Represents the attribute present while disabled: <c>data-disabled</c>.</summary>
    public const string Disabled = "data-disabled";

    /// <summary>Represents the attribute present while there is something to clear: <c>data-visible</c>.</summary>
    public const string Visible = "data-visible";

    /// <summary>Represents the attribute present while the button animates in: <c>data-starting-style</c>.</summary>
    public const string StartingStyle = PopupDataAttributes.StartingStyle;

    /// <summary>Represents the attribute present while the button animates out: <c>data-ending-style</c>.</summary>
    public const string EndingStyle = PopupDataAttributes.EndingStyle;
}

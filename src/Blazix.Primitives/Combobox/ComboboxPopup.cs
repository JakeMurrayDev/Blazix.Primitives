using Blazix.Primitives.Rendering;

namespace Blazix.Primitives;

// CMB §3.15, STA-10 (combobox/popup/ComboboxPopup.tsx): Side, Align and AnchorHidden read from the Positioner's mirror (rendered by C#, a dated Writer note), the status mirrored.
/// <summary>Represents the state of a combobox popup, which the part exposes as data attributes and passes to its class and style functions.</summary>
/// <param name="Open">A value indicating whether the popup is open; projected as <c>data-open</c> or <c>data-closed</c>.</param>
/// <param name="Side">The side of the anchor the popup is placed on; projected as <c>data-side</c>.</param>
/// <param name="Align">The alignment against the anchor; projected as <c>data-align</c>.</param>
/// <param name="AnchorHidden">A value indicating whether the anchor is hidden; projected as <c>data-anchor-hidden</c>.</param>
/// <param name="TransitionStatus">The enter or exit status; its <c>data-starting-style</c> and <c>data-ending-style</c> are written by the browser-side behavior.</param>
/// <param name="Empty">A value indicating whether the filtered list is empty; projected as <c>data-empty</c>.</param>
[PartState]
public sealed partial record ComboboxPopupState(
    bool Open,
    Side? Side,
    Align? Align,
    bool AnchorHidden,
    [property: MirroredField] TransitionStatus? TransitionStatus,
    bool Empty);

// combobox/popup/ComboboxPopupDataAttributes.ts (CMB-DOM-12).
/// <summary>Provides the names of the data attributes a combobox popup renders.</summary>
public static class ComboboxPopupDataAttributes
{
    /// <summary>Represents the attribute present while the popup is open: <c>data-open</c>.</summary>
    public const string Open = PopupDataAttributes.Open;

    /// <summary>Represents the attribute present while the popup is closed: <c>data-closed</c>.</summary>
    public const string Closed = PopupDataAttributes.Closed;

    /// <summary>Represents the attribute present while the popup animates in: <c>data-starting-style</c>.</summary>
    public const string StartingStyle = PopupDataAttributes.StartingStyle;

    /// <summary>Represents the attribute present while the popup animates out: <c>data-ending-style</c>.</summary>
    public const string EndingStyle = PopupDataAttributes.EndingStyle;

    /// <summary>Represents the attribute whose value is the side of the anchor: <c>data-side</c>.</summary>
    public const string Side = PopupDataAttributes.Side;

    /// <summary>Represents the attribute whose value is the alignment against the anchor: <c>data-align</c>.</summary>
    public const string Align = PopupDataAttributes.Align;

    /// <summary>Represents the attribute present while the anchor is hidden: <c>data-anchor-hidden</c>.</summary>
    public const string AnchorHidden = PopupDataAttributes.AnchorHidden;

    /// <summary>Represents the attribute present while the filtered list is empty: <c>data-empty</c>.</summary>
    public const string Empty = "data-empty";
}

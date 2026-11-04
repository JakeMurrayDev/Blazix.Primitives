using Blazix.Primitives.Rendering;

namespace Blazix.Primitives;

// CMB §3.16, STA-11 (combobox/arrow/ComboboxArrow.tsx): the arrow's placement mirrored.
/// <summary>Represents the state of a combobox arrow, which the part exposes as data attributes and passes to its class and style functions.</summary>
/// <param name="Open">A value indicating whether the popup is open; projected as <c>data-open</c> or <c>data-closed</c>.</param>
/// <param name="Side">The side of the anchor the popup is placed on; its <c>data-side</c> is written by the browser-side behavior.</param>
/// <param name="Align">The alignment against the anchor; its <c>data-align</c> is written by the browser-side behavior.</param>
/// <param name="Uncentered">A value indicating whether the arrow could not be centered on the anchor; its <c>data-uncentered</c> is written by the browser-side behavior.</param>
[PartState]
public sealed partial record ComboboxArrowState(
    bool Open,
    [property: MirroredField] Side? Side,
    [property: MirroredField] Align? Align,
    [property: MirroredField] bool Uncentered);

// combobox/arrow/ComboboxArrowDataAttributes.ts (CMB-DOM-15).
/// <summary>Provides the names of the data attributes a combobox arrow renders.</summary>
public static class ComboboxArrowDataAttributes
{
    /// <summary>Represents the attribute present while the popup is open: <c>data-open</c>.</summary>
    public const string Open = PopupDataAttributes.Open;

    /// <summary>Represents the attribute present while the popup is closed: <c>data-closed</c>.</summary>
    public const string Closed = PopupDataAttributes.Closed;

    /// <summary>Represents the attribute whose value is the side of the anchor: <c>data-side</c>.</summary>
    public const string Side = PopupDataAttributes.Side;

    /// <summary>Represents the attribute whose value is the alignment against the anchor: <c>data-align</c>.</summary>
    public const string Align = PopupDataAttributes.Align;

    /// <summary>Represents the attribute present while the arrow is not centered on the anchor: <c>data-uncentered</c>.</summary>
    public const string Uncentered = "data-uncentered";
}

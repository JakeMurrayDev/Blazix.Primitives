using Blazix.Primitives.Rendering;

namespace Blazix.Primitives;

// CMB §3.14, STA-09 (combobox/positioner/ComboboxPositioner.tsx): Side, Align and AnchorHidden are the `positioning` module's from attach (ADR 04 §2): mirrored.
/// <summary>Represents the state of a combobox positioner, which the part exposes as data attributes and passes to its class and style functions.</summary>
/// <param name="Open">A value indicating whether the popup is open; projected as <c>data-open</c> or <c>data-closed</c>.</param>
/// <param name="Side">The side of the anchor the popup is placed on, after collisions; its <c>data-side</c> is written by the browser-side behavior.</param>
/// <param name="Align">The alignment against the anchor, after collisions; its <c>data-align</c> is written by the browser-side behavior.</param>
/// <param name="AnchorHidden">A value indicating whether the anchor is hidden; its <c>data-anchor-hidden</c> is written by the browser-side behavior.</param>
/// <param name="Empty">A value indicating whether the filtered list is empty; projected as <c>data-empty</c>.</param>
[PartState]
public sealed partial record ComboboxPositionerState(
    bool Open,
    [property: MirroredField] Side? Side,
    [property: MirroredField] Align? Align,
    [property: MirroredField] bool AnchorHidden,
    bool Empty);

// combobox/positioner/ComboboxPositionerDataAttributes.ts (CMB-DOM-13).
/// <summary>Provides the names of the data attributes a combobox positioner renders.</summary>
public static class ComboboxPositionerDataAttributes
{
    /// <summary>Represents the attribute present while the popup is open: <c>data-open</c>.</summary>
    public const string Open = PopupDataAttributes.Open;

    /// <summary>Represents the attribute present while the popup is closed: <c>data-closed</c>.</summary>
    public const string Closed = PopupDataAttributes.Closed;

    /// <summary>Represents the attribute present while the anchor is hidden: <c>data-anchor-hidden</c>.</summary>
    public const string AnchorHidden = PopupDataAttributes.AnchorHidden;

    /// <summary>Represents the attribute whose value is the side of the anchor: <c>data-side</c>.</summary>
    public const string Side = PopupDataAttributes.Side;

    /// <summary>Represents the attribute whose value is the alignment against the anchor: <c>data-align</c>.</summary>
    public const string Align = PopupDataAttributes.Align;

    /// <summary>Represents the attribute present while the filtered list is empty: <c>data-empty</c>.</summary>
    public const string Empty = "data-empty";
}

// combobox/positioner/ComboboxPositionerCssVars.ts (CMB-DOM-13).
/// <summary>Provides the names of the CSS variables a combobox positioner sets.</summary>
public static class ComboboxPositionerCssVars
{
    /// <summary>Represents the width available to the popup: <c>--available-width</c>.</summary>
    public const string AvailableWidth = "--available-width";

    /// <summary>Represents the height available to the popup: <c>--available-height</c>.</summary>
    public const string AvailableHeight = "--available-height";

    /// <summary>Represents the anchor's width: <c>--anchor-width</c>.</summary>
    public const string AnchorWidth = "--anchor-width";

    /// <summary>Represents the anchor's height: <c>--anchor-height</c>.</summary>
    public const string AnchorHeight = "--anchor-height";

    /// <summary>Represents the transform origin of the popup: <c>--transform-origin</c>.</summary>
    public const string TransformOrigin = "--transform-origin";
}

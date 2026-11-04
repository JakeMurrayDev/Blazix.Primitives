using Blazix.Primitives.Rendering;

namespace Blazix.Primitives;

// CMB §3.13, STA-08 (combobox/backdrop/ComboboxBackdrop.tsx): the popup transition mapping; the status mirrored.
/// <summary>Represents the state of a combobox backdrop, which the part exposes as data attributes and passes to its class and style functions.</summary>
/// <param name="Open">A value indicating whether the popup is open; projected as <c>data-open</c> or <c>data-closed</c>.</param>
/// <param name="TransitionStatus">The enter or exit status; its <c>data-starting-style</c> and <c>data-ending-style</c> are written by the browser-side behavior.</param>
[PartState]
public sealed partial record ComboboxBackdropState(
    bool Open,
    [property: MirroredField] TransitionStatus? TransitionStatus);

// combobox/backdrop/ComboboxBackdropDataAttributes.ts (CMB-DOM-14).
/// <summary>Provides the names of the data attributes a combobox backdrop renders.</summary>
public static class ComboboxBackdropDataAttributes
{
    /// <summary>Represents the attribute present while the popup is open: <c>data-open</c>.</summary>
    public const string Open = PopupDataAttributes.Open;

    /// <summary>Represents the attribute present while the popup is closed: <c>data-closed</c>.</summary>
    public const string Closed = PopupDataAttributes.Closed;

    /// <summary>Represents the attribute present while the backdrop animates in: <c>data-starting-style</c>.</summary>
    public const string StartingStyle = PopupDataAttributes.StartingStyle;

    /// <summary>Represents the attribute present while the backdrop animates out: <c>data-ending-style</c>.</summary>
    public const string EndingStyle = PopupDataAttributes.EndingStyle;
}

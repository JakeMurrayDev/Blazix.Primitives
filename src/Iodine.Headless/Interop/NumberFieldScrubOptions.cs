namespace Blazix.Primitives.Interop;

// ADR 04 §1 (the `number-field` component module, its scrub role) and Amendments 2026-11-18; issue 277, 6b. One handle per
// ScrubArea and one per ScrubAreaCursor, told apart by `Part` (the ScrollArea precedent); every unset option is omitted, so
// an unchanged default costs nothing on the wire.
/// <summary>The options of the <c>number-field</c> module attached to a number field's scrub area or its cursor.</summary>
internal sealed record NumberFieldScrubOptions : TwinOptions
{
    /// <summary>Gets the part the handle belongs to.</summary>
    /// <value><see cref="NumberFieldScrubTwin.ScrubPart"/> or <see cref="NumberFieldScrubTwin.CursorPart"/>.</value>
    public string Part { get; init; } = NumberFieldScrubTwin.ScrubPart;

    // NUM-API-24, NUM-PTR-11: `direction`.
    /// <summary>Gets a value indicating whether the scrub area steps on vertical movement, or <see langword="null"/> for horizontal.</summary>
    /// <value><see langword="true"/> for vertical; <see langword="null"/> otherwise.</value>
    public bool? Vertical { get; init; }

    // NUM-API-24, NUM-PTR-11: `pixelSensitivity`.
    /// <summary>Gets the pixels the pointer must move before the value steps, or <see langword="null"/> for the default of 2.</summary>
    /// <value>The sensitivity; <see langword="null"/> for 2.</value>
    public double? PixelSensitivity { get; init; }

    // NUM-API-24, NUM-PTR-16: `teleportDistance`.
    /// <summary>Gets the distance around the scrub area at which the virtual cursor wraps, or <see langword="null"/> for the viewport.</summary>
    /// <value>The distance in pixels; <see langword="null"/> for the visual viewport.</value>
    public double? TeleportDistance { get; init; }

    // NUM-PTR-19: the scrub area does nothing while the root is disabled or read-only.
    /// <summary>Gets a value indicating whether the root is disabled, or <see langword="null"/> when it is not.</summary>
    /// <value><see langword="true"/> while disabled; <see langword="null"/> otherwise.</value>
    public bool? Disabled { get; init; }

    /// <summary>Gets a value indicating whether the root is read-only, or <see langword="null"/> when it is not.</summary>
    /// <value><see langword="true"/> while read-only; <see langword="null"/> otherwise.</value>
    public bool? ReadOnly { get; init; }
}

namespace Blazix.Primitives;

// ADR 08 Amendments 2026-10-27: AnchorValue's virtual-rect case, re-sent on change. Base UI's `VirtualElement` calls
// `getBoundingClientRect()` on every positioning pass; the port has no function case, so a moved virtual anchor is a new
// value (FLT-API-11's function clause, dated port note).
/// <summary>Represents a rectangle in viewport coordinates that a popup is positioned against instead of an element.</summary>
/// <param name="X">The distance from the left edge of the viewport, in CSS pixels.</param>
/// <param name="Y">The distance from the top edge of the viewport, in CSS pixels.</param>
/// <param name="Width">The width, in CSS pixels.</param>
/// <param name="Height">The height, in CSS pixels.</param>
/// <remarks>The rectangle is read when it is supplied; to move the popup, supply a new instance.</remarks>
public sealed record VirtualAnchor(double X, double Y, double Width, double Height);

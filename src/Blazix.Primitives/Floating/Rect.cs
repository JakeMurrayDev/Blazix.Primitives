namespace Blazix.Primitives;

// ADR 08 Amendments 2026-10-27 (CollisionBoundaryValue's rect case); Base UI's `Rect` from @floating-ui/utils.
/// <summary>Represents a rectangle in viewport coordinates.</summary>
/// <param name="X">The distance from the left edge of the viewport, in CSS pixels.</param>
/// <param name="Y">The distance from the top edge of the viewport, in CSS pixels.</param>
/// <param name="Width">The width, in CSS pixels.</param>
/// <param name="Height">The height, in CSS pixels.</param>
public sealed record Rect(double X, double Y, double Width, double Height);

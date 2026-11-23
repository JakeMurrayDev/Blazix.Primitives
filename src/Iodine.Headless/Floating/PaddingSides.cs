namespace Blazix.Primitives;

// ADR 08 Amendments 2026-10-27 (CollisionPaddingValue's sides record, named by M4 track 2); Base UI's
// `Partial<{ top, right, bottom, left }>`, a missing side being 0 (FLT-API-08).
/// <summary>Represents a padding given per side, in CSS pixels.</summary>
/// <param name="Top">The padding from the top edge.</param>
/// <param name="Right">The padding from the right edge.</param>
/// <param name="Bottom">The padding from the bottom edge.</param>
/// <param name="Left">The padding from the left edge.</param>
public sealed record PaddingSides(double Top = 0, double Right = 0, double Bottom = 0, double Left = 0);

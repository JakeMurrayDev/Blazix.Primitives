namespace Iodine.Headless;

// ADR 08 Amendments 2026-11-18 (the ScrollArea bullet; chosen by the user 2026-11-18): Base UI's
// `Partial<{ xStart, xEnd, yStart, yEnd }>` (scroll-area/root/ScrollAreaRoot.tsx:392-402). A missing edge is `null` and
// normalizes to 0 (SCR-API-02, SCR-API-03).
/// <summary>Represents a scroll area's overflow edge thresholds given per edge, in CSS pixels.</summary>
/// <param name="XStart">The threshold of the inline start edge, or <see langword="null"/> for 0.</param>
/// <param name="XEnd">The threshold of the inline end edge, or <see langword="null"/> for 0.</param>
/// <param name="YStart">The threshold of the top edge, or <see langword="null"/> for 0.</param>
/// <param name="YEnd">The threshold of the bottom edge, or <see langword="null"/> for 0.</param>
public sealed record ScrollAreaOverflowEdges(double? XStart = null, double? XEnd = null, double? YStart = null, double? YEnd = null);

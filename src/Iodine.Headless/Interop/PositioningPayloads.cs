namespace Blazix.Primitives.Interop;

// ADR 04 §2 (every Positioner's data-side, data-align, data-anchor-hidden; every Arrow's data-uncentered) and
// Amendments 2026-10-27: one report per change of the four mirrored values, never one per positioning pass (§3, ADR 16
// rule 6); the RND-STA-01 row of ADR 01. FLT-DOM-08, FLT-DOM-09, FLT-POS-09, FLT-POS-10, FLT-I18N-02.
/// <summary>The payload of a change of the rendered placement the module wrote on a positioner and its arrow.</summary>
/// <param name="Side">The rendered side, logical when the requested side was: <c>top</c>, <c>bottom</c>, <c>left</c>, <c>right</c>, <c>inline-end</c> or <c>inline-start</c>.</param>
/// <param name="Align">The rendered alignment: <c>start</c>, <c>center</c> or <c>end</c>.</param>
/// <param name="AnchorHidden">Whether the anchor is clipped from view or not in layout.</param>
/// <param name="Uncentered">Whether the arrow cannot point at the anchor's center.</param>
internal sealed record PositioningPlacement(string Side, string Align, bool AnchorHidden, bool Uncentered);

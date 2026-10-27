namespace Blazix.Primitives;

// ADR 04 §2 (the Positioner's data-side, data-align, data-anchor-hidden; the Arrow's data-uncentered) and ADR 08
// Amendments 2026-10-27 (the families' [MirroredField] Side, Align, AnchorHidden and Uncentered): the values the
// `positioning` module rendered, mirrored once per report (RND-STA-01 row of ADR 01). Before the first report it holds
// what Base UI's hook returns before its first pass: the requested side and alignment, the anchor shown and the arrow
// uncentered (FLT-DOM-09: no arrow data yet).
/// <summary>The placement a positioner's browser-side behavior last rendered, as C# mirrors it.</summary>
/// <param name="Side">The rendered side, logical when the requested side was.</param>
/// <param name="Align">The rendered alignment.</param>
/// <param name="AnchorHidden">Whether the anchor is clipped from view or not in layout.</param>
/// <param name="Uncentered">Whether the arrow cannot point at the anchor's center.</param>
internal sealed record PositionerMirror(Side Side, Align Align, bool AnchorHidden, bool Uncentered);

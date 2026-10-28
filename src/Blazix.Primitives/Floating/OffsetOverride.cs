namespace Blazix.Primitives;

// ADR 08 Amendments 2026-10-27; the positioning track's design point 2, settled by the orchestrator 2026-10-27: an
// override keyed by the evaluated logical side and the alignment, the first match in list order winning.
/// <summary>Represents the terms an <see cref="Offset"/> uses instead of its own when the placement being evaluated has a given side or alignment.</summary>
/// <param name="Side">The side the placement must have, logical when the requested side was logical, or <see langword="null"/> for any side.</param>
/// <param name="Align">The alignment the placement must have, or <see langword="null"/> for any alignment.</param>
/// <param name="Constant">The constant term, in CSS pixels.</param>
/// <param name="AnchorWidth">The multiple of the anchor's width.</param>
/// <param name="AnchorHeight">The multiple of the anchor's height.</param>
/// <param name="PositionerWidth">The multiple of the positioner's width.</param>
/// <param name="PositionerHeight">The multiple of the positioner's height.</param>
public sealed record OffsetOverride(
    Side? Side,
    Align? Align,
    double Constant = 0,
    double AnchorWidth = 0,
    double AnchorHeight = 0,
    double PositionerWidth = 0,
    double PositionerHeight = 0);

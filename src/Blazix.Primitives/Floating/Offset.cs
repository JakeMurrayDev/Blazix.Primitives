namespace Blazix.Primitives;

// ADR 08 Amendments 2026-10-27 (the offset union and its record, "its fields are track 2's"); the fields settled by the
// orchestrator 2026-10-27 as the positioning track's design point 2. Base UI's `OffsetFunction` receives `{ side, align,
// anchor, positioner }` on every positioning pass (FLT-API-12); the port's declarative form is evaluated by the module on
// every pass, so a flipped placement is observed (FLT-API-11's function clause and FLT-API-12, dated port notes). The
// overrides are a list, so equality is written by hand (ADR 16 Amendments 2026-10-27: no reflection, no boxing).
/// <summary>
/// Represents an offset computed on every positioning pass from the measured sizes of the anchor and the positioner:
/// <c>Constant + AnchorWidth × anchor width + AnchorHeight × anchor height + PositionerWidth × positioner width +
/// PositionerHeight × positioner height</c>, unless an entry of <see cref="Overrides"/> matches the placement being evaluated.
/// </summary>
/// <param name="Constant">The constant term, in CSS pixels.</param>
/// <param name="AnchorWidth">The multiple of the anchor's width.</param>
/// <param name="AnchorHeight">The multiple of the anchor's height.</param>
/// <param name="PositionerWidth">The multiple of the positioner's width.</param>
/// <param name="PositionerHeight">The multiple of the positioner's height.</param>
/// <remarks>
/// For example, <c>new Offset(AnchorWidth: 1, PositionerWidth: 1)</c> is the anchor's width plus the positioner's width.
/// Two instances are equal when their terms are equal and their <see cref="Overrides"/> are equal item by item, in order.
/// </remarks>
public sealed record Offset(double Constant = 0, double AnchorWidth = 0, double AnchorHeight = 0, double PositionerWidth = 0, double PositionerHeight = 0)
{
    /// <summary>Gets the terms that replace these terms for a particular side or alignment of the placement being evaluated.</summary>
    /// <value>
    /// The overrides in order, or <see langword="null"/>, the default, for none. The first override whose
    /// <see cref="OffsetOverride.Side"/> and <see cref="OffsetOverride.Align"/> both match applies; a <see langword="null"/>
    /// key matches any side or alignment. The side is the logical side when the requested side was logical.
    /// </value>
    public IReadOnlyList<OffsetOverride>? Overrides { get; init; }

    /// <inheritdoc />
    public bool Equals(Offset? other)
    {
        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return other is not null
            && Constant.Equals(other.Constant)
            && AnchorWidth.Equals(other.AnchorWidth)
            && AnchorHeight.Equals(other.AnchorHeight)
            && PositionerWidth.Equals(other.PositionerWidth)
            && PositionerHeight.Equals(other.PositionerHeight)
            && SameOverrides(Overrides, other.Overrides);
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Constant);
        hash.Add(AnchorWidth);
        hash.Add(AnchorHeight);
        hash.Add(PositionerWidth);
        hash.Add(PositionerHeight);
        if (Overrides is { } overrides)
        {
            // An indexed loop: a foreach over the interface would allocate its enumerator.
            for (int i = 0; i < overrides.Count; i++)
            {
                hash.Add(overrides[i].GetHashCode());
            }
        }

        return hash.ToHashCode();
    }

    private static bool SameOverrides(IReadOnlyList<OffsetOverride>? left, IReadOnlyList<OffsetOverride>? right)
    {
        if (ReferenceEquals(left, right))
        {
            return true;
        }

        if (left is null || right is null || left.Count != right.Count)
        {
            return false;
        }

        for (int i = 0; i < left.Count; i++)
        {
            if (!left[i].Equals(right[i]))
            {
                return false;
            }
        }

        return true;
    }
}

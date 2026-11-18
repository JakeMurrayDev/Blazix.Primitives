using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Probe.Parts;

// ADR 08 Amendments 2026-11-18 (the ScrollArea line): one nullable number per edge.
public sealed record ScrollAreaOverflowEdges(double? XStart = null, double? XEnd = null, double? YStart = null, double? YEnd = null);

// ADR 08 Amendments 2026-11-18 (the ScrollArea line): a hand-written [Union] of `double` and ScrollAreaOverflowEdges with
// implicit conversions from both.
[Union]
public readonly struct OverflowEdgeThresholdValue : IUnion, IEquatable<OverflowEdgeThresholdValue>
{
    private readonly ScrollAreaOverflowEdges? edges;
    private readonly double number;
    private readonly byte kind; // 0 none, 1 number, 2 edges

    public OverflowEdgeThresholdValue(double value)
    {
        number = value;
        kind = 1;
    }

    public OverflowEdgeThresholdValue(ScrollAreaOverflowEdges value)
    {
        ArgumentNullException.ThrowIfNull(value);
        edges = value;
        kind = 2;
    }

    public bool HasValue => kind != 0;

    public object? Value => kind switch { 1 => number, 2 => edges, _ => null };

    public static implicit operator OverflowEdgeThresholdValue(double value) => new(value);

    public static implicit operator OverflowEdgeThresholdValue(ScrollAreaOverflowEdges value) => new(value);

    public static bool operator ==(OverflowEdgeThresholdValue left, OverflowEdgeThresholdValue right) => left.Equals(right);

    public static bool operator !=(OverflowEdgeThresholdValue left, OverflowEdgeThresholdValue right) => !left.Equals(right);

    public bool TryGetValue(out double value)
    {
        value = number;
        return kind == 1;
    }

    public bool TryGetValue([NotNullWhen(true)] out ScrollAreaOverflowEdges? value)
    {
        value = edges;
        return kind == 2;
    }

    public bool Equals(OverflowEdgeThresholdValue other) => kind == other.kind && kind switch
    {
        1 => number.Equals(other.number),
        2 => edges!.Equals(other.edges),
        _ => true,
    };

    public override bool Equals([NotNullWhen(true)] object? obj) => obj is OverflowEdgeThresholdValue other && Equals(other);

    public override int GetHashCode() => kind switch
    {
        1 => HashCode.Combine(1, number),
        2 => HashCode.Combine(2, edges),
        _ => 0,
    };

    public override string ToString() => kind switch
    {
        1 => "number:" + Show.Number(number),
        2 => "edges:" + edges,
        _ => "none",
    };
}

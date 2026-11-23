using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Iodine.Headless;

// ADR 05 §4 (hand-written [Union] structs; CollisionPaddingValue is the model); ADR 08 Amendments 2026-11-18 (chosen by the
// user 2026-11-18) and the probe's line of 2026-11-18 (`OverflowEdgeThreshold="20"` binds). Base UI's
// `overflowEdgeThreshold: number | Partial<{ xStart, xEnd, yStart, yEnd }>` (SCR-API-01..03). Value equality without
// boxing (ADR 16 Amendments 2026-10-27).
/// <summary>Represents the distance a scroll area's viewport must be scrolled from an edge before that edge counts as overflowing: one number for every edge, or a value per edge.</summary>
/// <remarks>
/// <para>
/// This is a hand-written union, marked <see cref="UnionAttribute"/>, with the non-boxing access pattern:
/// <see cref="HasValue"/>, <see cref="TryGetValue(out double)"/> and <see cref="TryGetValue(out ScrollAreaOverflowEdges)"/>.
/// <see cref="Value"/> exists because <see cref="IUnion"/> requires it.
/// </para>
/// <para>In Razor, one number is written as <c>OverflowEdgeThreshold="20"</c>. The default instance has no value.</para>
/// </remarks>
[Union]
public readonly struct OverflowEdgeThresholdValue : IUnion, IEquatable<OverflowEdgeThresholdValue>
{
    private readonly ScrollAreaOverflowEdges? edges;
    private readonly double all;
    private readonly ThresholdKind kind;

    /// <summary>Initializes a new instance of the <see cref="OverflowEdgeThresholdValue"/> struct that holds one threshold for every edge.</summary>
    /// <param name="value">The threshold, in CSS pixels.</param>
    public OverflowEdgeThresholdValue(double value)
    {
        all = value;
        kind = ThresholdKind.All;
    }

    /// <summary>Initializes a new instance of the <see cref="OverflowEdgeThresholdValue"/> struct that holds a threshold per edge.</summary>
    /// <param name="value">The threshold per edge; <see langword="null"/> for no value.</param>
    public OverflowEdgeThresholdValue(ScrollAreaOverflowEdges? value)
    {
        edges = value;
        kind = value is null ? ThresholdKind.None : ThresholdKind.Edges;
    }

    /// <summary>Gets a value indicating whether a value is held.</summary>
    /// <value><see langword="true"/> if a value is held; otherwise, <see langword="false"/>. The default instance holds no value.</value>
    public bool HasValue => kind != ThresholdKind.None;

    // IOD2005: IUnion requires an object-typed Value, so reading it boxes the number case; the library reads the cases
    // through TryGetValue instead.
#pragma warning disable IOD2005
    /// <inheritdoc />
    /// <remarks>Reading it boxes a number.</remarks>
    public object? Value => kind switch
    {
        ThresholdKind.All => all,
        ThresholdKind.Edges => edges,
        _ => null,
    };
#pragma warning restore IOD2005

    /// <summary>Converts a number to an <see cref="OverflowEdgeThresholdValue"/> that holds it for every edge.</summary>
    /// <param name="value">The threshold, in CSS pixels.</param>
    /// <returns>An <see cref="OverflowEdgeThresholdValue"/> that holds <paramref name="value"/>.</returns>
    public static implicit operator OverflowEdgeThresholdValue(double value) => new(value);

    /// <summary>Converts a threshold per edge to an <see cref="OverflowEdgeThresholdValue"/> that holds it.</summary>
    /// <param name="value">The threshold per edge; <see langword="null"/> for no value.</param>
    /// <returns>An <see cref="OverflowEdgeThresholdValue"/> that holds <paramref name="value"/>.</returns>
    public static implicit operator OverflowEdgeThresholdValue(ScrollAreaOverflowEdges? value) => new(value);

    /// <summary>Determines whether two thresholds hold the same case and value.</summary>
    /// <param name="left">The first threshold.</param>
    /// <param name="right">The second threshold.</param>
    /// <returns><see langword="true"/> if the thresholds are equal; otherwise, <see langword="false"/>.</returns>
    public static bool operator ==(OverflowEdgeThresholdValue left, OverflowEdgeThresholdValue right) => left.Equals(right);

    /// <summary>Determines whether two thresholds differ in case or value.</summary>
    /// <param name="left">The first threshold.</param>
    /// <param name="right">The second threshold.</param>
    /// <returns><see langword="true"/> if the thresholds differ; otherwise, <see langword="false"/>.</returns>
    public static bool operator !=(OverflowEdgeThresholdValue left, OverflowEdgeThresholdValue right) => !left.Equals(right);

    /// <summary>Gets the threshold for every edge, if this instance holds one.</summary>
    /// <param name="value">When this method returns, the threshold if one is held; otherwise, 0.</param>
    /// <returns><see langword="true"/> if one threshold for every edge is held; otherwise, <see langword="false"/>.</returns>
    public bool TryGetValue(out double value)
    {
        value = all;
        return kind == ThresholdKind.All;
    }

    /// <summary>Gets the threshold per edge, if this instance holds one.</summary>
    /// <param name="value">When this method returns, the threshold per edge if one is held; otherwise, <see langword="null"/>.</param>
    /// <returns><see langword="true"/> if a threshold per edge is held; otherwise, <see langword="false"/>.</returns>
    public bool TryGetValue([NotNullWhen(true)] out ScrollAreaOverflowEdges? value)
    {
        value = edges;
        return kind == ThresholdKind.Edges;
    }

    /// <inheritdoc />
    public bool Equals(OverflowEdgeThresholdValue other) => kind == other.kind && kind switch
    {
        ThresholdKind.All => all.Equals(other.all),
        ThresholdKind.Edges => edges!.Equals(other.edges),
        _ => true,
    };

    /// <inheritdoc />
    public override bool Equals([NotNullWhen(true)] object? obj) => obj is OverflowEdgeThresholdValue other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => kind switch
    {
        ThresholdKind.All => HashCode.Combine((int)kind, all),
        ThresholdKind.Edges => HashCode.Combine((int)kind, edges!.GetHashCode()),
        _ => 0,
    };

    private enum ThresholdKind : byte
    {
        None,
        All,
        Edges,
    }
}

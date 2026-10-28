using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Blazix.Primitives;

// ADR 05 §4 (hand-written [Union] structs; Duration is the model); ADR 08 Amendments 2026-10-27: a number or a sides
// record. Base UI's `collisionPadding: number | Partial<{ top, right, bottom, left }>`, default 5, normalized by the
// module with missing sides as 0 (internals/useAnchorPositioning.ts:213-234, FLT-API-08). Value equality without boxing
// (ADR 16 Amendments 2026-10-27).
/// <summary>Represents the space a positioned popup keeps from the edges of its collision boundary: one number for every side, or a value per side.</summary>
/// <remarks>
/// <para>
/// This is a hand-written union, marked <see cref="UnionAttribute"/>, with the non-boxing access pattern:
/// <see cref="HasValue"/>, <see cref="TryGetValue(out double)"/> and <see cref="TryGetValue(out PaddingSides)"/>.
/// <see cref="Value"/> exists because <see cref="IUnion"/> requires it.
/// </para>
/// <para>In Razor, one number is written as <c>CollisionPadding="12"</c>. The default instance has no value.</para>
/// </remarks>
[Union]
public readonly struct CollisionPaddingValue : IUnion, IEquatable<CollisionPaddingValue>
{
    private readonly PaddingSides? sides;
    private readonly double all;
    private readonly PaddingKind kind;

    /// <summary>Initializes a new instance of the <see cref="CollisionPaddingValue"/> struct that holds one padding for every side.</summary>
    /// <param name="value">The padding, in CSS pixels.</param>
    public CollisionPaddingValue(double value)
    {
        all = value;
        kind = PaddingKind.All;
    }

    /// <summary>Initializes a new instance of the <see cref="CollisionPaddingValue"/> struct that holds a padding per side.</summary>
    /// <param name="value">The padding per side; <see langword="null"/> for no value.</param>
    public CollisionPaddingValue(PaddingSides? value)
    {
        sides = value;
        kind = value is null ? PaddingKind.None : PaddingKind.Sides;
    }

    /// <summary>Gets a value indicating whether a value is held.</summary>
    /// <value><see langword="true"/> if a value is held; otherwise, <see langword="false"/>. The default instance holds no value.</value>
    public bool HasValue => kind != PaddingKind.None;

    // BLZX2005: IUnion requires an object-typed Value, so reading it boxes the number case; the library reads the cases
    // through TryGetValue instead.
#pragma warning disable BLZX2005
    /// <inheritdoc />
    /// <remarks>Reading it boxes a number.</remarks>
    public object? Value => kind switch
    {
        PaddingKind.All => all,
        PaddingKind.Sides => sides,
        _ => null,
    };
#pragma warning restore BLZX2005

    /// <summary>Converts a number to a <see cref="CollisionPaddingValue"/> that holds it for every side.</summary>
    /// <param name="value">The padding, in CSS pixels.</param>
    /// <returns>A <see cref="CollisionPaddingValue"/> that holds <paramref name="value"/>.</returns>
    public static implicit operator CollisionPaddingValue(double value) => new(value);

    /// <summary>Converts a padding per side to a <see cref="CollisionPaddingValue"/> that holds it.</summary>
    /// <param name="value">The padding per side; <see langword="null"/> for no value.</param>
    /// <returns>A <see cref="CollisionPaddingValue"/> that holds <paramref name="value"/>.</returns>
    public static implicit operator CollisionPaddingValue(PaddingSides? value) => new(value);

    /// <summary>Determines whether two paddings hold the same case and value.</summary>
    /// <param name="left">The first padding.</param>
    /// <param name="right">The second padding.</param>
    /// <returns><see langword="true"/> if the paddings are equal; otherwise, <see langword="false"/>.</returns>
    public static bool operator ==(CollisionPaddingValue left, CollisionPaddingValue right) => left.Equals(right);

    /// <summary>Determines whether two paddings differ in case or value.</summary>
    /// <param name="left">The first padding.</param>
    /// <param name="right">The second padding.</param>
    /// <returns><see langword="true"/> if the paddings differ; otherwise, <see langword="false"/>.</returns>
    public static bool operator !=(CollisionPaddingValue left, CollisionPaddingValue right) => !left.Equals(right);

    /// <summary>Gets the padding for every side, if this instance holds one.</summary>
    /// <param name="value">When this method returns, the padding if one is held; otherwise, 0.</param>
    /// <returns><see langword="true"/> if one padding for every side is held; otherwise, <see langword="false"/>.</returns>
    public bool TryGetValue(out double value)
    {
        value = all;
        return kind == PaddingKind.All;
    }

    /// <summary>Gets the padding per side, if this instance holds one.</summary>
    /// <param name="value">When this method returns, the padding per side if one is held; otherwise, <see langword="null"/>.</param>
    /// <returns><see langword="true"/> if a padding per side is held; otherwise, <see langword="false"/>.</returns>
    public bool TryGetValue([NotNullWhen(true)] out PaddingSides? value)
    {
        value = sides;
        return kind == PaddingKind.Sides;
    }

    /// <inheritdoc />
    public bool Equals(CollisionPaddingValue other) => kind == other.kind && kind switch
    {
        PaddingKind.All => all.Equals(other.all),
        PaddingKind.Sides => sides!.Equals(other.sides),
        _ => true,
    };

    /// <inheritdoc />
    public override bool Equals([NotNullWhen(true)] object? obj) => obj is CollisionPaddingValue other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => kind switch
    {
        PaddingKind.All => HashCode.Combine((int)kind, all),
        PaddingKind.Sides => HashCode.Combine((int)kind, sides!.GetHashCode()),
        _ => 0,
    };

    private enum PaddingKind : byte
    {
        None,
        All,
        Sides,
    }
}

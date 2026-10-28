using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Blazix.Primitives;

// ADR 05 §4 (hand-written [Union] structs; Duration is the model); ADR 08 Amendments 2026-10-27 (the offset union, suffix
// Value; its name settled by the orchestrator 2026-10-27 as the positioning track's design point 2). Base UI's
// `sideOffset`/`alignOffset: number | OffsetFunction`, default 0 (internals/useAnchorPositioning.ts:688-742). Value
// equality without boxing (ADR 16 Amendments 2026-10-27).
/// <summary>Represents a positioning offset: a number of CSS pixels, or an <see cref="Offset"/> computed on every positioning pass.</summary>
/// <remarks>
/// <para>
/// This is a hand-written union, marked <see cref="UnionAttribute"/>, with the non-boxing access pattern:
/// <see cref="HasValue"/>, <see cref="TryGetValue(out double)"/> and <see cref="TryGetValue(out Offset)"/>.
/// <see cref="Value"/> exists because <see cref="IUnion"/> requires it.
/// </para>
/// <para>In Razor, a number is written as <c>SideOffset="8"</c>. The default instance and a <see langword="null"/> offset hold no value.</para>
/// </remarks>
[Union]
public readonly struct OffsetValue : IUnion, IEquatable<OffsetValue>
{
    private readonly Offset? offset;
    private readonly double pixels;
    private readonly OffsetKind kind;

    /// <summary>Initializes a new instance of the <see cref="OffsetValue"/> struct that holds a number of CSS pixels.</summary>
    /// <param name="value">The offset, in CSS pixels.</param>
    public OffsetValue(double value)
    {
        pixels = value;
        kind = OffsetKind.Pixels;
    }

    /// <summary>Initializes a new instance of the <see cref="OffsetValue"/> struct that holds an offset computed on every pass.</summary>
    /// <param name="value">The offset; <see langword="null"/> for no value.</param>
    public OffsetValue(Offset? value)
    {
        offset = value;
        kind = value is null ? OffsetKind.None : OffsetKind.Computed;
    }

    /// <summary>Gets a value indicating whether a value is held.</summary>
    /// <value><see langword="true"/> if a value is held; otherwise, <see langword="false"/>. The default instance holds no value.</value>
    public bool HasValue => kind != OffsetKind.None;

    // BLZX2005: IUnion requires an object-typed Value, so reading it boxes the number case; the library reads the cases
    // through TryGetValue instead.
#pragma warning disable BLZX2005
    /// <inheritdoc />
    /// <remarks>Reading it boxes a number.</remarks>
    public object? Value => kind switch
    {
        OffsetKind.Pixels => pixels,
        OffsetKind.Computed => offset,
        _ => null,
    };
#pragma warning restore BLZX2005

    /// <summary>Converts a number of CSS pixels to an <see cref="OffsetValue"/> that holds it.</summary>
    /// <param name="value">The offset, in CSS pixels.</param>
    /// <returns>An <see cref="OffsetValue"/> that holds <paramref name="value"/>.</returns>
    public static implicit operator OffsetValue(double value) => new(value);

    /// <summary>Converts an <see cref="Offset"/> to an <see cref="OffsetValue"/> that holds it.</summary>
    /// <param name="value">The offset; <see langword="null"/> for no value.</param>
    /// <returns>An <see cref="OffsetValue"/> that holds <paramref name="value"/>.</returns>
    public static implicit operator OffsetValue(Offset? value) => new(value);

    /// <summary>Determines whether two offsets hold the same case and value.</summary>
    /// <param name="left">The first offset.</param>
    /// <param name="right">The second offset.</param>
    /// <returns><see langword="true"/> if the offsets are equal; otherwise, <see langword="false"/>.</returns>
    public static bool operator ==(OffsetValue left, OffsetValue right) => left.Equals(right);

    /// <summary>Determines whether two offsets differ in case or value.</summary>
    /// <param name="left">The first offset.</param>
    /// <param name="right">The second offset.</param>
    /// <returns><see langword="true"/> if the offsets differ; otherwise, <see langword="false"/>.</returns>
    public static bool operator !=(OffsetValue left, OffsetValue right) => !left.Equals(right);

    /// <summary>Gets the number of CSS pixels, if this instance holds one.</summary>
    /// <param name="value">When this method returns, the number if one is held; otherwise, 0.</param>
    /// <returns><see langword="true"/> if a number is held; otherwise, <see langword="false"/>.</returns>
    public bool TryGetValue(out double value)
    {
        value = pixels;
        return kind == OffsetKind.Pixels;
    }

    /// <summary>Gets the offset computed on every pass, if this instance holds one.</summary>
    /// <param name="value">When this method returns, the offset if one is held; otherwise, <see langword="null"/>.</param>
    /// <returns><see langword="true"/> if an <see cref="Offset"/> is held; otherwise, <see langword="false"/>.</returns>
    public bool TryGetValue([NotNullWhen(true)] out Offset? value)
    {
        value = offset;
        return kind == OffsetKind.Computed;
    }

    /// <inheritdoc />
    public bool Equals(OffsetValue other) => kind == other.kind && kind switch
    {
        OffsetKind.Pixels => pixels.Equals(other.pixels),
        OffsetKind.Computed => offset!.Equals(other.offset),
        _ => true,
    };

    /// <inheritdoc />
    public override bool Equals([NotNullWhen(true)] object? obj) => obj is OffsetValue other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => kind switch
    {
        OffsetKind.Pixels => HashCode.Combine((int)kind, pixels),
        OffsetKind.Computed => HashCode.Combine((int)kind, offset!.GetHashCode()),
        _ => 0,
    };

    private enum OffsetKind : byte
    {
        None,
        Pixels,
        Computed,
    }
}

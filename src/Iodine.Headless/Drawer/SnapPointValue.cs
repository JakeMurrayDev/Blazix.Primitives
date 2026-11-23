using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Iodine.Headless.Store;

namespace Iodine.Headless;

// DRW-API-02..04, §3.18 (drawer/root/DrawerRootContext.ts:7, `Drawer.Root.SnapPoint = number | string`); ADR 08 Amendments
// 2026-11-18 (decision 20 (a) of the M6 plan, chosen by the user 2026-11-18, and the probe's outcome): a hand-written union
// with ModalValue's non-boxing access. Equality is Object.is on the number (NaN equals itself, 0 and -0 differ) and ordinal
// on the string, never across the cases, as Base UI's `Object.is(snapPoint, activeSnapPoint)` (DRW-STA-02); Expanded is
// the number 1 only (DRW-STA-16, §16 Q6).
/// <summary>
/// Represents a drawer's snap point: a number or a string such as <c>"200px"</c> or <c>"10rem"</c>.
/// </summary>
/// <remarks>
/// <para>
/// A number from 0 to 1 is a fraction of the viewport's height; a number greater than 1 is a height in pixels. A string
/// ending in <c>px</c> is a height in pixels and one ending in <c>rem</c> is relative to the root font size. Any other
/// value is ignored.
/// </para>
/// <para>
/// In Razor, write a number as a literal, such as <c>SnapPoint="0.5"</c>, and a string as an expression, such as
/// <c>SnapPoint="@("200px")"</c>: a bare string does not compile. Two snap points are equal when both are the same number
/// (<see cref="double.NaN"/> equals itself, and 0 and -0 differ) or both are the same string, compared ordinally; the
/// string <c>"1"</c> never equals the number 1.
/// </para>
/// <para>
/// This is a hand-written union, marked <see cref="UnionAttribute"/>, with the non-boxing access pattern:
/// <see cref="HasValue"/>, <see cref="TryGetValue(out double)"/> and <see cref="TryGetValue(out string)"/>.
/// <see cref="Value"/> exists because <see cref="IUnion"/> requires it. The default instance has no value.
/// </para>
/// </remarks>
[Union]
public readonly struct SnapPointValue : IUnion, IEquatable<SnapPointValue>
{
    private readonly double number;
    private readonly string? text;
    private readonly SnapPointKind kind;

    /// <summary>Initializes a new instance of the <see cref="SnapPointValue"/> struct that holds a number.</summary>
    /// <param name="value">A fraction of the viewport's height from 0 to 1, or a height in pixels.</param>
    public SnapPointValue(double value)
    {
        number = value;
        kind = SnapPointKind.Number;
    }

    /// <summary>Initializes a new instance of the <see cref="SnapPointValue"/> struct that holds a string.</summary>
    /// <param name="value">A height such as <c>"200px"</c> or <c>"10rem"</c>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is <see langword="null"/>.</exception>
    public SnapPointValue(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        text = value;
        kind = SnapPointKind.String;
    }

    /// <summary>Gets a value indicating whether a value is held.</summary>
    /// <value><see langword="true"/> if a value is held; otherwise, <see langword="false"/>. The default instance holds no value.</value>
    public bool HasValue => kind != SnapPointKind.None;

    // IOD2005: IUnion requires an object-typed Value, so reading it boxes the number; the library reads the cases through
    // TryGetValue instead.
#pragma warning disable IOD2005
    /// <inheritdoc />
    /// <remarks>Reading it boxes a number.</remarks>
    public object? Value => kind switch
    {
        SnapPointKind.Number => number,
        SnapPointKind.String => text,
        _ => null,
    };
#pragma warning restore IOD2005

    // DRW-STA-16 (DrawerPopup.tsx:314): only the number 1 is the full height.
    internal bool IsFullHeight => kind == SnapPointKind.Number && number == 1;

    /// <summary>Converts a number to a <see cref="SnapPointValue"/> that holds it.</summary>
    /// <param name="value">A fraction of the viewport's height from 0 to 1, or a height in pixels.</param>
    /// <returns>A <see cref="SnapPointValue"/> that holds <paramref name="value"/>.</returns>
    public static implicit operator SnapPointValue(double value) => new(value);

    /// <summary>Converts a string to a <see cref="SnapPointValue"/> that holds it.</summary>
    /// <param name="value">A height such as <c>"200px"</c> or <c>"10rem"</c>.</param>
    /// <returns>A <see cref="SnapPointValue"/> that holds <paramref name="value"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is <see langword="null"/>.</exception>
    public static implicit operator SnapPointValue(string value) => new(value);

    /// <summary>Determines whether two snap points are equal.</summary>
    /// <param name="left">The first snap point.</param>
    /// <param name="right">The second snap point.</param>
    /// <returns><see langword="true"/> if they hold the same number or the same string; otherwise, <see langword="false"/>.</returns>
    public static bool operator ==(SnapPointValue left, SnapPointValue right) => left.Equals(right);

    /// <summary>Determines whether two snap points differ.</summary>
    /// <param name="left">The first snap point.</param>
    /// <param name="right">The second snap point.</param>
    /// <returns><see langword="true"/> if they do not hold the same number or the same string; otherwise, <see langword="false"/>.</returns>
    public static bool operator !=(SnapPointValue left, SnapPointValue right) => !left.Equals(right);

    /// <summary>Gets the number, if this instance holds one.</summary>
    /// <param name="value">When this method returns, the number if one is held; otherwise, 0.</param>
    /// <returns><see langword="true"/> if a number is held; otherwise, <see langword="false"/>.</returns>
    public bool TryGetValue(out double value)
    {
        value = number;
        return kind == SnapPointKind.Number;
    }

    /// <summary>Gets the string, if this instance holds one.</summary>
    /// <param name="value">When this method returns, the string if one is held; otherwise, <see langword="null"/>.</param>
    /// <returns><see langword="true"/> if a string is held; otherwise, <see langword="false"/>.</returns>
    public bool TryGetValue([NotNullWhen(true)] out string? value)
    {
        value = text;
        return kind == SnapPointKind.String;
    }

    /// <inheritdoc />
    public bool Equals(SnapPointValue other) => kind == other.kind && kind switch
    {
        SnapPointKind.Number => Equality.Is(number, other.number),
        SnapPointKind.String => string.Equals(text, other.text, StringComparison.Ordinal),
        _ => true,
    };

    /// <inheritdoc />
    public override bool Equals([NotNullWhen(true)] object? obj) => obj is SnapPointValue other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => kind switch
    {
        // Object.is: every NaN is one value, and 0 and -0 differ, as their bit patterns do.
        SnapPointKind.Number => HashCode.Combine(kind, double.IsNaN(number) ? double.NaN.GetHashCode() : BitConverter.DoubleToInt64Bits(number).GetHashCode()),
        SnapPointKind.String => HashCode.Combine(kind, StringComparer.Ordinal.GetHashCode(text!)),
        _ => 0,
    };

    private enum SnapPointKind : byte
    {
        None,
        Number,
        String,
    }
}

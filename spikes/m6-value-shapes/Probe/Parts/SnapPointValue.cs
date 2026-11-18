using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Probe.Parts;

// Decision 20 (a), ADR 08 Amendments 2026-11-18 (the Drawer line): a hand-written [Union] over `double` and `string`; the
// string "1" never equals the number 1.
[Union]
public readonly struct SnapPointValue : IUnion, IEquatable<SnapPointValue>
{
    private readonly string? text;
    private readonly double number;
    private readonly byte kind; // 0 none, 1 number, 2 string

    public SnapPointValue(double value)
    {
        number = value;
        kind = 1;
    }

    public SnapPointValue(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        text = value;
        kind = 2;
    }

    public bool HasValue => kind != 0;

    public object? Value => kind switch { 1 => number, 2 => text, _ => null };

    public static implicit operator SnapPointValue(double value) => new(value);

    public static implicit operator SnapPointValue(string value) => new(value);

    public static bool operator ==(SnapPointValue left, SnapPointValue right) => left.Equals(right);

    public static bool operator !=(SnapPointValue left, SnapPointValue right) => !left.Equals(right);

    public bool TryGetValue(out double value)
    {
        value = number;
        return kind == 1;
    }

    public bool TryGetValue([NotNullWhen(true)] out string? value)
    {
        value = text;
        return kind == 2;
    }

    public bool Equals(SnapPointValue other) => kind == other.kind && kind switch
    {
        1 => number.Equals(other.number),
        2 => string.Equals(text, other.text, StringComparison.Ordinal),
        _ => true,
    };

    public override bool Equals([NotNullWhen(true)] object? obj) => obj is SnapPointValue other && Equals(other);

    public override int GetHashCode() => kind switch
    {
        1 => HashCode.Combine(1, number),
        2 => HashCode.Combine(2, StringComparer.Ordinal.GetHashCode(text!)),
        _ => 0,
    };

    public override string ToString() => kind switch
    {
        1 => "number:" + Show.Number(number),
        2 => "string:" + text,
        _ => "none",
    };
}

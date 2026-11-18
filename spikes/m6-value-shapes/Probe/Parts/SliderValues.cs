using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Probe.Parts;

// Decision 12 (a), ADR 08 Amendments 2026-11-18 (the Slider line): a hand-written [Union] over `double` and
// `IReadOnlyList<double>` with element-wise equality that allocates nothing (ADR 08 Amendments 2026-10-27's union rule:
// IEquatable<T>, ==, !=). C# allows no user-defined conversion from an interface (CS0552), so the list case converts
// from `double[]` and `List<double>`; whether the [Union] constructor alone converts an `IReadOnlyList<double>` is a case.
[Union]
public readonly struct SliderValues : IUnion, IEquatable<SliderValues>
{
    private readonly IReadOnlyList<double>? values;
    private readonly double value;
    private readonly byte kind; // 0 none (the default instance), 1 number, 2 list

    public SliderValues(double value)
    {
        this.value = value;
        kind = 1;
    }

    public SliderValues(IReadOnlyList<double> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        this.values = values;
        kind = 2;
    }

    public bool HasValue => kind != 0;

    public object? Value => kind switch { 1 => value, 2 => values, _ => null };

    public bool IsList => kind == 2;

    public static implicit operator SliderValues(double value) => new(value);

    public static implicit operator SliderValues(double[] values) => new(values);

    public static implicit operator SliderValues(List<double> values) => new(values);

    public static bool operator ==(SliderValues left, SliderValues right) => left.Equals(right);

    public static bool operator !=(SliderValues left, SliderValues right) => !left.Equals(right);

    public bool TryGetValue(out double result)
    {
        result = value;
        return kind == 1;
    }

    public bool TryGetValue([NotNullWhen(true)] out IReadOnlyList<double>? result)
    {
        result = values;
        return IsList;
    }

    // Element-wise, by index: no enumerator, no allocation.
    public bool Equals(SliderValues other)
    {
        if (kind != other.kind)
        {
            return false;
        }

        if (kind != 2)
        {
            return value.Equals(other.value);
        }

        if (ReferenceEquals(values, other.values))
        {
            return true;
        }

        int count = values!.Count;
        if (count != other.values!.Count)
        {
            return false;
        }

        for (int index = 0; index < count; index++)
        {
            if (!values[index].Equals(other.values[index]))
            {
                return false;
            }
        }

        return true;
    }

    public override bool Equals([NotNullWhen(true)] object? obj) => obj is SliderValues other && Equals(other);

    public override int GetHashCode()
    {
        if (!IsList)
        {
            return value.GetHashCode();
        }

        HashCode hash = default;
        for (int index = 0; index < values!.Count; index++)
        {
            hash.Add(values[index]);
        }

        return hash.ToHashCode();
    }

    public override string ToString() => IsList ? "list:" + Show.List(values) : "number:" + Show.Number(value);
}

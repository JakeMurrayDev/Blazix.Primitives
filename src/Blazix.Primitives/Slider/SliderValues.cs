using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Blazix.Primitives.Store;

namespace Blazix.Primitives;

// SLD-API-01/02, SLD-STA-04/09; slider/root/SliderRoot.tsx:39-47 (`number | readonly number[]`). ADR 05 §4 and Amendments
// 2026-11-18 item 4, ADR 08 Amendments 2026-11-18 (the Slider value shape, chosen by the user 2026-11-18): the hand-written
// union is the Root's internal raw value, never a parameter type; the parameters are the two triples. C# allows no conversion from an interface (CS0552), so the list case converts from `double[]`
// and `List<double>`, and an `IReadOnlyList<double>` through the constructor. Equality is element-wise by Equality.Is
// (Object.is per element, areArraysEqual), with no enumerator and no boxing; a list never equals a number.
/// <summary>
/// Represents the value of a slider: one number, which makes a single-thumb slider, or a list of numbers, which makes a
/// range slider with one thumb per element.
/// </summary>
/// <remarks>
/// <para>
/// This is a hand-written union, marked <see cref="UnionAttribute"/>, with the non-boxing access pattern:
/// <see cref="HasValue"/>, <see cref="IsRange"/>, <see cref="TryGetValue(out double)"/> and
/// <see cref="TryGetValue(out IReadOnlyList{double})"/>. <see cref="Value"/> exists because <see cref="IUnion"/> requires it.
/// </para>
/// <para>
/// In Razor, write <c>DefaultValue="25"</c> for one thumb, or <c>DefaultValue="@(new double[] { 25, 45 })"</c> for two. A list
/// of one element still makes a range slider. Two values are equal when both are numbers that are the same, or both are
/// lists of the same length whose elements are the same, by <c>Object.is</c>: <c>NaN</c> equals <c>NaN</c> and <c>0</c>
/// differs from <c>-0</c>. The default instance holds no value.
/// </para>
/// </remarks>
[Union]
internal readonly struct SliderValues : IUnion, IEquatable<SliderValues>
{
    private readonly IReadOnlyList<double>? values;
    private readonly double value;
    private readonly SliderValuesKind kind;

    /// <summary>Initializes a new instance of the <see cref="SliderValues"/> struct that holds one number.</summary>
    /// <param name="value">The value of a single-thumb slider.</param>
    public SliderValues(double value)
    {
        this.value = value;
        kind = SliderValuesKind.Number;
    }

    /// <summary>Initializes a new instance of the <see cref="SliderValues"/> struct that holds a list of numbers.</summary>
    /// <param name="values">The values of a range slider, one per thumb; the list is held, not copied.</param>
    /// <exception cref="ArgumentNullException"><paramref name="values"/> is <see langword="null"/>.</exception>
    public SliderValues(IReadOnlyList<double> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        this.values = values;
        kind = SliderValuesKind.List;
    }

    /// <summary>Gets a value indicating whether a value is held.</summary>
    /// <value><see langword="true"/> if a number or a list is held; otherwise, <see langword="false"/>. The default instance holds no value.</value>
    public bool HasValue => kind != SliderValuesKind.None;

    /// <summary>Gets a value indicating whether a list is held, which makes a range slider.</summary>
    /// <value><see langword="true"/> if a list is held, even of one element; otherwise, <see langword="false"/>.</value>
    public bool IsRange => kind == SliderValuesKind.List;

    // BLZX2005: IUnion requires an object-typed Value, so reading it boxes the number; the library reads the cases through
    // TryGetValue instead.
#pragma warning disable BLZX2005
    /// <inheritdoc />
    /// <remarks>Reading it boxes a number.</remarks>
    public object? Value => kind switch
    {
        SliderValuesKind.Number => value,
        SliderValuesKind.List => values,
        _ => null,
    };
#pragma warning restore BLZX2005

    /// <summary>Converts a number to a <see cref="SliderValues"/> that holds it.</summary>
    /// <param name="value">The value of a single-thumb slider.</param>
    /// <returns>A <see cref="SliderValues"/> that holds <paramref name="value"/>.</returns>
    public static implicit operator SliderValues(double value) => new(value);

    /// <summary>Converts an array to a <see cref="SliderValues"/> that holds it.</summary>
    /// <param name="values">The values of a range slider.</param>
    /// <returns>A <see cref="SliderValues"/> that holds <paramref name="values"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="values"/> is <see langword="null"/>.</exception>
    public static implicit operator SliderValues(double[] values) => new(values);

    /// <summary>Converts a list to a <see cref="SliderValues"/> that holds it.</summary>
    /// <param name="values">The values of a range slider.</param>
    /// <returns>A <see cref="SliderValues"/> that holds <paramref name="values"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="values"/> is <see langword="null"/>.</exception>
    public static implicit operator SliderValues(List<double> values) => new(values);

    /// <summary>Determines whether two values are equal.</summary>
    /// <param name="left">The first value.</param>
    /// <param name="right">The second value.</param>
    /// <returns><see langword="true"/> if the values are equal; otherwise, <see langword="false"/>.</returns>
    public static bool operator ==(SliderValues left, SliderValues right) => left.Equals(right);

    /// <summary>Determines whether two values are not equal.</summary>
    /// <param name="left">The first value.</param>
    /// <param name="right">The second value.</param>
    /// <returns><see langword="true"/> if the values are not equal; otherwise, <see langword="false"/>.</returns>
    public static bool operator !=(SliderValues left, SliderValues right) => !left.Equals(right);

    /// <summary>Gets the number, if this instance holds one.</summary>
    /// <param name="result">When this method returns, the number if one is held; otherwise, 0.</param>
    /// <returns><see langword="true"/> if a number is held; otherwise, <see langword="false"/>.</returns>
    public bool TryGetValue(out double result)
    {
        result = value;
        return kind == SliderValuesKind.Number;
    }

    /// <summary>Gets the list, if this instance holds one.</summary>
    /// <param name="result">When this method returns, the list if one is held; otherwise, <see langword="null"/>.</param>
    /// <returns><see langword="true"/> if a list is held; otherwise, <see langword="false"/>.</returns>
    public bool TryGetValue([NotNullWhen(true)] out IReadOnlyList<double>? result)
    {
        result = values;
        return kind == SliderValuesKind.List;
    }

    /// <inheritdoc />
    public bool Equals(SliderValues other)
    {
        if (kind != other.kind)
        {
            return false;
        }

        if (kind != SliderValuesKind.List)
        {
            return Equality.Is(value, other.value);
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
            if (!Equality.Is(values[index], other.values[index]))
            {
                return false;
            }
        }

        return true;
    }

    /// <inheritdoc />
    public override bool Equals([NotNullWhen(true)] object? obj) => obj is SliderValues other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode()
    {
        if (kind != SliderValuesKind.List)
        {
            return HashCode.Combine(kind, value);
        }

        HashCode hash = default;
        for (int index = 0; index < values!.Count; index++)
        {
            hash.Add(values[index]);
        }

        return hash.ToHashCode();
    }

    /// <inheritdoc />
    public override string ToString() => kind switch
    {
        SliderValuesKind.Number => value.ToString(System.Globalization.CultureInfo.InvariantCulture),
        SliderValuesKind.List => "[" + string.Join(", ", ToInvariantStrings(values!)) + "]",
        _ => string.Empty,
    };

    private static string[] ToInvariantStrings(IReadOnlyList<double> list)
    {
        string[] texts = new string[list.Count];
        for (int index = 0; index < texts.Length; index++)
        {
            texts[index] = list[index].ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        return texts;
    }

    private enum SliderValuesKind : byte
    {
        None,
        Number,
        List,
    }
}

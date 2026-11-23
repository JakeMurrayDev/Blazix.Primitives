using System.Runtime.CompilerServices;
using Iodine.Headless.Utilities;

namespace Iodine.Headless;

// NUM-API-06: Base UI's `step?: number | 'any'`. ADR 05 §4 and ADR 08 §3's union row (a hand-written [Union], suffix
// `Value`); the brief of issue 277 leaves the name to the track. The case type of the literal follows AutoHighlightValue's
// AutoHighlightAlways. A numeric literal binds through the implicit conversion from double (the Razor probe's line of
// ADR 08 Amendments 2026-11-03).
/// <summary>
/// Represents the step of a number field: a number, or <see cref="Any"/>, which steps by 1 and turns off the browser's
/// step validation.
/// </summary>
/// <remarks>
/// <para>
/// This is a hand-written union, marked <see cref="UnionAttribute"/>, with the non-boxing access pattern:
/// <see cref="HasValue"/>, <see cref="TryGetValue(out double)"/> and <see cref="TryGetValue(out StepAny)"/>.
/// <see cref="Value"/> exists because <see cref="IUnion"/> requires it.
/// </para>
/// <para>
/// In Razor, write <c>Step="5"</c>, <c>Step="0.25"</c> or <c>Step="@StepValue.Any"</c>. The default instance has no value.
/// </para>
/// </remarks>
[Union]
public readonly struct StepValue : IUnion, IEquatable<StepValue>
{
    private readonly double number;
    private readonly StepKind kind;

    /// <summary>Initializes a new instance of the <see cref="StepValue"/> struct that holds a number.</summary>
    /// <param name="value">The step.</param>
    public StepValue(double value)
    {
        number = value;
        kind = StepKind.Number;
    }

    /// <summary>Initializes a new instance of the <see cref="StepValue"/> struct that holds the <c>any</c> literal.</summary>
    /// <param name="value">The literal.</param>
    public StepValue(StepAny value)
    {
        _ = value;
        number = 0;
        kind = StepKind.Any;
    }

    /// <summary>Gets the step that moves by 1 and lets the browser accept any value, Base UI's <c>'any'</c>.</summary>
    /// <value>A <see cref="StepValue"/> that holds <see cref="StepAny"/>.</value>
    public static StepValue Any => new(default(StepAny));

    /// <summary>Gets a value indicating whether a value is held.</summary>
    /// <value><see langword="true"/> if a value is held; otherwise, <see langword="false"/>. The default instance holds no value.</value>
    public bool HasValue => kind != StepKind.None;

    // IOD2005: IUnion requires an object-typed Value, so reading it boxes either case; the library reads the cases through
    // TryGetValue and Amount instead.
#pragma warning disable IOD2005
    /// <inheritdoc />
    /// <remarks>Reading it boxes the value.</remarks>
    public object? Value => kind switch
    {
        StepKind.Number => number,
        StepKind.Any => default(StepAny),
        _ => null,
    };
#pragma warning restore IOD2005

    /// <summary>Gets a value indicating whether this instance holds the <c>any</c> literal.</summary>
    /// <value><see langword="true"/> if this instance is <see cref="Any"/>; otherwise, <see langword="false"/>.</value>
    public bool IsAny => kind == StepKind.Any;

    // NUM-STA (derived values): base step = step === 'any' ? 1 : step.
    /// <summary>Gets the amount a step moves by.</summary>
    /// <value>The number held, or 1 for <see cref="Any"/> and for the default instance.</value>
    internal double Amount => kind == StepKind.Number ? number : 1;

    // NUM-DOM-06: the hidden input's step attribute, the raw prop.
    /// <summary>Gets the text of the hidden input's <c>step</c> attribute.</summary>
    /// <value><c>any</c>, the number serialized as a browser writes it, or <c>1</c> for the default instance.</value>
    internal string AttributeText => kind switch
    {
        StepKind.Any => "any",
        StepKind.Number => NumberSerializer.Serialize(number),
        _ => "1",
    };

    /// <summary>Converts a number to a <see cref="StepValue"/> that holds it.</summary>
    /// <param name="value">The step.</param>
    /// <returns>A <see cref="StepValue"/> that holds <paramref name="value"/>.</returns>
    public static implicit operator StepValue(double value) => new(value);

    /// <summary>Converts the <c>any</c> literal to a <see cref="StepValue"/> that holds it.</summary>
    /// <param name="value">The literal.</param>
    /// <returns><see cref="Any"/>.</returns>
    public static implicit operator StepValue(StepAny value) => new(value);

    /// <summary>Determines whether two steps are equal.</summary>
    /// <param name="left">The first step.</param>
    /// <param name="right">The second step.</param>
    /// <returns><see langword="true"/> if both hold the same case and value; otherwise, <see langword="false"/>.</returns>
    public static bool operator ==(StepValue left, StepValue right) => left.Equals(right);

    /// <summary>Determines whether two steps differ.</summary>
    /// <param name="left">The first step.</param>
    /// <param name="right">The second step.</param>
    /// <returns><see langword="true"/> if the steps are not equal; otherwise, <see langword="false"/>.</returns>
    public static bool operator !=(StepValue left, StepValue right) => !left.Equals(right);

    /// <summary>Gets the number, if this instance holds one.</summary>
    /// <param name="value">When this method returns, the number if one is held; otherwise, 0.</param>
    /// <returns><see langword="true"/> if a number is held; otherwise, <see langword="false"/>.</returns>
    public bool TryGetValue(out double value)
    {
        value = number;
        return kind == StepKind.Number;
    }

    /// <summary>Gets the <c>any</c> literal, if this instance holds it.</summary>
    /// <param name="value">When this method returns, the literal.</param>
    /// <returns><see langword="true"/> if the literal is held; otherwise, <see langword="false"/>.</returns>
    public bool TryGetValue(out StepAny value)
    {
        value = default;
        return kind == StepKind.Any;
    }

    /// <inheritdoc />
    public bool Equals(StepValue other) => kind == other.kind && number.Equals(other.number);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is StepValue other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine((int)kind, number);

    private enum StepKind : byte
    {
        None,
        Number,
        Any,
    }
}

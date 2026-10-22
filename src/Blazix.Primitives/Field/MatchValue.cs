using System.Runtime.CompilerServices;

namespace Blazix.Primitives;

// ADR 05 §4 (value-typed alternatives are hand-written [Union] structs with the non-boxing access pattern; Duration is
// the model); ADR 08 §3. Base UI's `match?: boolean | keyof ValidityState` (FLD-API-30, field/error/FieldError.tsx:153):
// `true` always renders, `false` is the default slot, a flag renders while it is set.
/// <summary>Represents when a <see cref="FieldError"/> renders: always, as the default slot, or while a validity flag is set.</summary>
/// <remarks>
/// <para>
/// This is a hand-written union, marked <see cref="UnionAttribute"/>, with the non-boxing access pattern:
/// <see cref="HasValue"/>, <see cref="TryGetValue(out bool)"/> and <see cref="TryGetValue(out ValidityFlag)"/>.
/// <see cref="Value"/> exists because <see cref="IUnion"/> requires it.
/// </para>
/// <para>
/// In Razor, a flag is written with its enum name, such as <c>Match="ValidityFlag.ValueMissing"</c>, and the boolean
/// cases as <c>Match="true"</c> and <c>Match="false"</c>. The default instance has no value.
/// </para>
/// </remarks>
[Union]
public readonly struct MatchValue : IUnion
{
    private readonly ValidityFlag flag;
    private readonly bool always;
    private readonly MatchKind kind;

    /// <summary>Initializes a new instance of the <see cref="MatchValue"/> struct that holds a boolean.</summary>
    /// <param name="value"><see langword="true"/> to render the error always; <see langword="false"/> for the default slot.</param>
    public MatchValue(bool value)
    {
        always = value;
        kind = MatchKind.Boolean;
    }

    /// <summary>Initializes a new instance of the <see cref="MatchValue"/> struct that holds a validity flag.</summary>
    /// <param name="value">The flag that the field's validity must have set for the error to render.</param>
    public MatchValue(ValidityFlag value)
    {
        flag = value;
        kind = MatchKind.Flag;
    }

    /// <summary>Gets a value indicating whether a value is held.</summary>
    /// <value><see langword="true"/> if a value is held; otherwise, <see langword="false"/>. The default instance holds no value.</value>
    public bool HasValue => kind != MatchKind.None;

    // BLZX2005: IUnion requires an object-typed Value, so reading it boxes either case; the library reads the cases
    // through TryGetValue instead.
#pragma warning disable BLZX2005
    /// <inheritdoc />
    /// <remarks>Reading it boxes the value.</remarks>
    public object? Value => kind switch
    {
        MatchKind.Boolean => always,
        MatchKind.Flag => flag,
        _ => null,
    };
#pragma warning restore BLZX2005

    /// <summary>Converts a boolean to a <see cref="MatchValue"/> that holds it.</summary>
    /// <param name="value"><see langword="true"/> to render the error always; <see langword="false"/> for the default slot.</param>
    /// <returns>A <see cref="MatchValue"/> that holds <paramref name="value"/>.</returns>
    public static implicit operator MatchValue(bool value) => new(value);

    /// <summary>Converts a validity flag to a <see cref="MatchValue"/> that holds it.</summary>
    /// <param name="value">The flag that the field's validity must have set for the error to render.</param>
    /// <returns>A <see cref="MatchValue"/> that holds <paramref name="value"/>.</returns>
    public static implicit operator MatchValue(ValidityFlag value) => new(value);

    /// <summary>Gets the boolean, if this instance holds one.</summary>
    /// <param name="value">When this method returns, the boolean if one is held; otherwise, <see langword="false"/>.</param>
    /// <returns><see langword="true"/> if a boolean is held; otherwise, <see langword="false"/>.</returns>
    public bool TryGetValue(out bool value)
    {
        value = always;
        return kind == MatchKind.Boolean;
    }

    /// <summary>Gets the validity flag, if this instance holds one.</summary>
    /// <param name="value">When this method returns, the flag if one is held; otherwise, <see cref="ValidityFlag.BadInput"/>.</param>
    /// <returns><see langword="true"/> if a flag is held; otherwise, <see langword="false"/>.</returns>
    public bool TryGetValue(out ValidityFlag value)
    {
        value = flag;
        return kind == MatchKind.Flag;
    }

    private enum MatchKind : byte
    {
        None,
        Boolean,
        Flag,
    }
}

using System.Runtime.CompilerServices;

namespace Blazix.Primitives;

// ADR 05 §4 (value-typed alternatives are hand-written [Union] structs with the non-boxing access pattern;
// AttributeValue is the model); ADR 08 §3. Base UI's millisecond `number` props (Avatar.Fallback `delay`).
/// <summary>Represents a length of time given either as a number of milliseconds or as a <see cref="TimeSpan"/>.</summary>
/// <remarks>
/// <para>
/// This is a hand-written union, marked <see cref="UnionAttribute"/>, with the non-boxing access pattern:
/// <see cref="HasValue"/>, <see cref="TryGetValue(out int)"/> and <see cref="TryGetValue(out TimeSpan)"/>.
/// <see cref="Value"/> exists because <see cref="IUnion"/> requires it.
/// </para>
/// <para>The default instance has no value.</para>
/// </remarks>
[Union]
public readonly struct Duration : IUnion
{
    private readonly TimeSpan timeSpan;
    private readonly int milliseconds;
    private readonly DurationKind kind;

    /// <summary>Initializes a new instance of the <see cref="Duration"/> struct that holds a number of milliseconds.</summary>
    /// <param name="value">The number of milliseconds.</param>
    public Duration(int value)
    {
        milliseconds = value;
        kind = DurationKind.Milliseconds;
    }

    /// <summary>Initializes a new instance of the <see cref="Duration"/> struct that holds a <see cref="TimeSpan"/>.</summary>
    /// <param name="value">The time span.</param>
    public Duration(TimeSpan value)
    {
        timeSpan = value;
        kind = DurationKind.TimeSpan;
    }

    /// <summary>Gets a value indicating whether a value is held.</summary>
    /// <value><see langword="true"/> if a value is held; otherwise, <see langword="false"/>. The default instance holds no value.</value>
    public bool HasValue => kind != DurationKind.None;

    // BLZX2005: IUnion requires an object-typed Value, so reading it boxes either case; the library reads the cases
    // through TryGetValue instead.
#pragma warning disable BLZX2005
    /// <inheritdoc />
    /// <remarks>Reading it boxes the value.</remarks>
    public object? Value => kind switch
    {
        DurationKind.Milliseconds => milliseconds,
        DurationKind.TimeSpan => timeSpan,
        _ => null,
    };
#pragma warning restore BLZX2005

    /// <summary>Converts a number of milliseconds to a <see cref="Duration"/> that holds it.</summary>
    /// <param name="value">The number of milliseconds.</param>
    /// <returns>A <see cref="Duration"/> that holds <paramref name="value"/> milliseconds.</returns>
    public static implicit operator Duration(int value) => new(value);

    /// <summary>Converts a <see cref="TimeSpan"/> to a <see cref="Duration"/> that holds it.</summary>
    /// <param name="value">The time span.</param>
    /// <returns>A <see cref="Duration"/> that holds <paramref name="value"/>.</returns>
    public static implicit operator Duration(TimeSpan value) => new(value);

    /// <summary>Gets the number of milliseconds, if this instance holds one.</summary>
    /// <param name="value">When this method returns, the number of milliseconds if one is held; otherwise, 0.</param>
    /// <returns><see langword="true"/> if a number of milliseconds is held; otherwise, <see langword="false"/>.</returns>
    public bool TryGetValue(out int value)
    {
        value = milliseconds;
        return kind == DurationKind.Milliseconds;
    }

    /// <summary>Gets the time span, if this instance holds one.</summary>
    /// <param name="value">When this method returns, the time span if one is held; otherwise, <see cref="TimeSpan.Zero"/>.</param>
    /// <returns><see langword="true"/> if a time span is held; otherwise, <see langword="false"/>.</returns>
    public bool TryGetValue(out TimeSpan value)
    {
        value = timeSpan;
        return kind == DurationKind.TimeSpan;
    }

    /// <summary>Returns the length of time in milliseconds.</summary>
    /// <returns>The milliseconds held, a time span's total milliseconds, or 0 for the default instance.</returns>
    internal double ToMilliseconds() => kind switch
    {
        DurationKind.Milliseconds => milliseconds,
        DurationKind.TimeSpan => timeSpan.TotalMilliseconds,
        _ => 0,
    };

    private enum DurationKind : byte
    {
        None,
        Milliseconds,
        TimeSpan,
    }
}

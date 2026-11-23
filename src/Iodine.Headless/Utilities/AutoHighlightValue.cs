using System.Runtime.CompilerServices;

namespace Blazix.Primitives;

// UTL (shared by Menu's filter, MNU-API-55, and Autocomplete): Base UI's `autoHighlight?: boolean | 'always'`. Track 10's
// design point 4 (a) (settled by the orchestrator 2026-11-04): ModalValue's union shape with a case type on ModalTrapFocus's
// pattern; declared by whichever of M5 tracks 11 and 12 lands first. ADR 05 §4, ADR 08 §3's boolean-or-literal row.
/// <summary>
/// Represents whether the first matching item is highlighted automatically: <see langword="true"/>,
/// <see langword="false"/>, or <see cref="Always"/>, which highlights it even when the query is empty.
/// </summary>
/// <remarks>
/// <para>
/// This is a hand-written union, marked <see cref="UnionAttribute"/>, with the non-boxing access pattern:
/// <see cref="HasValue"/>, <see cref="TryGetValue(out bool)"/> and <see cref="TryGetValue(out AutoHighlightAlways)"/>.
/// <see cref="Value"/> exists because <see cref="IUnion"/> requires it.
/// </para>
/// <para>
/// In Razor, write <c>AutoHighlight="true"</c>, <c>AutoHighlight="false"</c> or
/// <c>AutoHighlight="@AutoHighlightValue.Always"</c>. A part's <c>AutoHighlight</c> parameter is nullable, and
/// <see langword="null"/> means <see langword="false"/>. The default instance has no value.
/// </para>
/// </remarks>
[Union]
public readonly struct AutoHighlightValue : IUnion
{
    private readonly bool enabled;
    private readonly AutoHighlightKind kind;

    /// <summary>Initializes a new instance of the <see cref="AutoHighlightValue"/> struct that holds a boolean.</summary>
    /// <param name="value"><see langword="true"/> to highlight the first match while the query is not empty; <see langword="false"/> for no automatic highlight.</param>
    public AutoHighlightValue(bool value)
    {
        enabled = value;
        kind = AutoHighlightKind.Boolean;
    }

    /// <summary>Initializes a new instance of the <see cref="AutoHighlightValue"/> struct that holds the <c>always</c> literal.</summary>
    /// <param name="value">The literal.</param>
    public AutoHighlightValue(AutoHighlightAlways value)
    {
        _ = value;
        kind = AutoHighlightKind.Always;
    }

    /// <summary>Gets the value that highlights the first item even when the query is empty, Base UI's <c>always</c>.</summary>
    /// <value>An <see cref="AutoHighlightValue"/> that holds <see cref="AutoHighlightAlways"/>.</value>
    public static AutoHighlightValue Always => new(default(AutoHighlightAlways));

    /// <summary>Gets a value indicating whether a value is held.</summary>
    /// <value><see langword="true"/> if a value is held; otherwise, <see langword="false"/>. The default instance holds no value.</value>
    public bool HasValue => kind != AutoHighlightKind.None;

    // BLZX2005: IUnion requires an object-typed Value, so reading it boxes either case; the library reads the cases
    // through TryGetValue, IsAlways and IsEnabled instead.
#pragma warning disable BLZX2005
    /// <inheritdoc />
    /// <remarks>Reading it boxes the value.</remarks>
    public object? Value => kind switch
    {
        AutoHighlightKind.Boolean => enabled,
        AutoHighlightKind.Always => default(AutoHighlightAlways),
        _ => null,
    };
#pragma warning restore BLZX2005

    /// <summary>Gets a value indicating whether this instance holds the <c>always</c> literal.</summary>
    /// <value><see langword="true"/> if this instance is <see cref="Always"/>; otherwise, <see langword="false"/>.</value>
    public bool IsAlways => kind == AutoHighlightKind.Always;

    /// <summary>Gets a value indicating whether the automatic highlight is on: <see langword="true"/> or <see cref="Always"/>.</summary>
    /// <value><see langword="false"/> for <see langword="false"/> and for the default instance.</value>
    public bool IsEnabled => kind == AutoHighlightKind.Always || (kind == AutoHighlightKind.Boolean && enabled);

    /// <summary>Converts a boolean to an <see cref="AutoHighlightValue"/> that holds it.</summary>
    /// <param name="value"><see langword="true"/> to highlight the first match while the query is not empty; <see langword="false"/> for no automatic highlight.</param>
    /// <returns>An <see cref="AutoHighlightValue"/> that holds <paramref name="value"/>.</returns>
    public static implicit operator AutoHighlightValue(bool value) => new(value);

    /// <summary>Gets the boolean, if this instance holds one.</summary>
    /// <param name="value">When this method returns, the boolean if one is held; otherwise, <see langword="false"/>.</param>
    /// <returns><see langword="true"/> if a boolean is held; otherwise, <see langword="false"/>.</returns>
    public bool TryGetValue(out bool value)
    {
        value = enabled;
        return kind == AutoHighlightKind.Boolean;
    }

    /// <summary>Gets the <c>always</c> literal, if this instance holds it.</summary>
    /// <param name="value">When this method returns, the literal.</param>
    /// <returns><see langword="true"/> if the literal is held; otherwise, <see langword="false"/>.</returns>
    public bool TryGetValue(out AutoHighlightAlways value)
    {
        value = default;
        return kind == AutoHighlightKind.Always;
    }

    private enum AutoHighlightKind : byte
    {
        None,
        Boolean,
        Always,
    }
}


using System.Runtime.CompilerServices;

namespace Blazix.Primitives;

// ADR 05 §4 (value-typed alternatives are hand-written [Union] structs with the non-boxing access pattern; Duration is
// the model); ADR 08 §3's boolean-or-literal row and verification item 1 (issue 10), Amendments 2026-10-27 (written by M4
// track 1 in Popup/, the first shared type Popover and Dialog both need). Base UI's `modal?: boolean | 'trap-focus'`.
/// <summary>
/// Represents whether a popup is modal: <see langword="true"/>, <see langword="false"/>, or <see cref="TrapFocus"/>,
/// which traps focus while page scroll and outside pointer interaction stay free.
/// </summary>
/// <remarks>
/// <para>
/// This is a hand-written union, marked <see cref="UnionAttribute"/>, with the non-boxing access pattern:
/// <see cref="HasValue"/>, <see cref="TryGetValue(out bool)"/> and <see cref="TryGetValue(out ModalTrapFocus)"/>.
/// <see cref="Value"/> exists because <see cref="IUnion"/> requires it.
/// </para>
/// <para>
/// In Razor, write <c>Modal="true"</c>, <c>Modal="false"</c> or <c>Modal="@ModalValue.TrapFocus"</c>. A part's
/// <c>Modal</c> parameter is nullable, and <see langword="null"/> means the part's default. The default instance has no
/// value.
/// </para>
/// </remarks>
[Union]
public readonly struct ModalValue : IUnion
{
    private readonly bool modal;
    private readonly ModalKind kind;

    /// <summary>Initializes a new instance of the <see cref="ModalValue"/> struct that holds a boolean.</summary>
    /// <param name="value"><see langword="true"/> for a modal popup; <see langword="false"/> for a non-modal one.</param>
    public ModalValue(bool value)
    {
        modal = value;
        kind = ModalKind.Boolean;
    }

    /// <summary>Initializes a new instance of the <see cref="ModalValue"/> struct that holds the <c>trap-focus</c> literal.</summary>
    /// <param name="value">The literal.</param>
    public ModalValue(ModalTrapFocus value)
    {
        _ = value;
        kind = ModalKind.TrapFocus;
    }

    /// <summary>Gets the value that traps focus while page scroll and outside pointer interaction stay free, Base UI's <c>trap-focus</c>.</summary>
    /// <value>A <see cref="ModalValue"/> that holds <see cref="ModalTrapFocus"/>.</value>
    public static ModalValue TrapFocus => new(default(ModalTrapFocus));

    /// <summary>Gets a value indicating whether a value is held.</summary>
    /// <value><see langword="true"/> if a value is held; otherwise, <see langword="false"/>. The default instance holds no value.</value>
    public bool HasValue => kind != ModalKind.None;

    // BLZX2005: IUnion requires an object-typed Value, so reading it boxes either case; the library reads the cases
    // through TryGetValue and IsTrapFocus instead.
#pragma warning disable BLZX2005
    /// <inheritdoc />
    /// <remarks>Reading it boxes the value.</remarks>
    public object? Value => kind switch
    {
        ModalKind.Boolean => modal,
        ModalKind.TrapFocus => default(ModalTrapFocus),
        _ => null,
    };
#pragma warning restore BLZX2005

    /// <summary>Gets a value indicating whether this instance holds the <c>trap-focus</c> literal.</summary>
    /// <value><see langword="true"/> if this instance is <see cref="TrapFocus"/>; otherwise, <see langword="false"/>.</value>
    public bool IsTrapFocus => kind == ModalKind.TrapFocus;

    /// <summary>Converts a boolean to a <see cref="ModalValue"/> that holds it.</summary>
    /// <param name="value"><see langword="true"/> for a modal popup; <see langword="false"/> for a non-modal one.</param>
    /// <returns>A <see cref="ModalValue"/> that holds <paramref name="value"/>.</returns>
    public static implicit operator ModalValue(bool value) => new(value);

    /// <summary>Gets the boolean, if this instance holds one.</summary>
    /// <param name="value">When this method returns, the boolean if one is held; otherwise, <see langword="false"/>.</param>
    /// <returns><see langword="true"/> if a boolean is held; otherwise, <see langword="false"/>.</returns>
    public bool TryGetValue(out bool value)
    {
        value = modal;
        return kind == ModalKind.Boolean;
    }

    /// <summary>Gets the <c>trap-focus</c> literal, if this instance holds it.</summary>
    /// <param name="value">When this method returns, the literal.</param>
    /// <returns><see langword="true"/> if the literal is held; otherwise, <see langword="false"/>.</returns>
    public bool TryGetValue(out ModalTrapFocus value)
    {
        value = default;
        return kind == ModalKind.TrapFocus;
    }

    private enum ModalKind : byte
    {
        None,
        Boolean,
        TrapFocus,
    }
}

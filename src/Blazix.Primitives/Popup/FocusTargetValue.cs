using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Components;

namespace Blazix.Primitives;

// ADR 08 Amendments 2026-10-27 (written by M4 track 1 in Popup/, settled by the orchestrator 2026-10-27; ADR 04
// Amendments 2026-10-27, focus targets): the value form of Base UI's `initialFocus` and `finalFocus` (PSH-FOC-02/04):
// `true` (the default target), `false` (focus is not moved), an element, or an element's id. Base UI's `null` is the
// same as `true` and has no case. The resolution against the interaction type, and the delegate form
// Func<InteractionType?, FocusTargetValue?>, are each family's (tracks 5 and 8).
/// <summary>
/// Represents where focus moves when a popup opens or closes: <see langword="true"/> for the default target,
/// <see langword="false"/> to leave focus where it is, an element, or the id of an element.
/// </summary>
/// <remarks>
/// <para>
/// This is a hand-written union, marked <see cref="UnionAttribute"/>, with the non-boxing access pattern:
/// <see cref="HasValue"/> and one <c>TryGetValue</c> overload per case. <see cref="Value"/> exists because
/// <see cref="IUnion"/> requires it.
/// </para>
/// <para>The default instance has no value.</para>
/// </remarks>
[Union]
public readonly struct FocusTargetValue : IUnion
{
    private readonly ElementReference element;
    private readonly string? id;
    private readonly bool focus;
    private readonly TargetKind kind;

    /// <summary>Initializes a new instance of the <see cref="FocusTargetValue"/> struct that holds a boolean.</summary>
    /// <param name="value"><see langword="true"/> for the default target; <see langword="false"/> to leave focus where it is.</param>
    public FocusTargetValue(bool value)
    {
        focus = value;
        kind = TargetKind.Boolean;
    }

    /// <summary>Initializes a new instance of the <see cref="FocusTargetValue"/> struct that holds an element.</summary>
    /// <param name="value">The element to focus.</param>
    public FocusTargetValue(ElementReference value)
    {
        element = value;
        kind = TargetKind.Element;
    }

    /// <summary>Initializes a new instance of the <see cref="FocusTargetValue"/> struct that holds an element's id.</summary>
    /// <param name="value">The id of the element to focus.</param>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is <see langword="null"/>.</exception>
    public FocusTargetValue(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        id = value;
        kind = TargetKind.Id;
    }

    /// <summary>Gets a value indicating whether a value is held.</summary>
    /// <value><see langword="true"/> if a value is held; otherwise, <see langword="false"/>. The default instance holds no value.</value>
    public bool HasValue => kind != TargetKind.None;

    // BLZX2005: IUnion requires an object-typed Value, so reading it boxes the struct cases; the library reads the cases
    // through TryGetValue instead.
#pragma warning disable BLZX2005
    /// <inheritdoc />
    /// <remarks>Reading it boxes a boolean or an element.</remarks>
    public object? Value => kind switch
    {
        TargetKind.Boolean => focus,
        TargetKind.Element => element,
        TargetKind.Id => id,
        _ => null,
    };
#pragma warning restore BLZX2005

    /// <summary>Converts a boolean to a <see cref="FocusTargetValue"/> that holds it.</summary>
    /// <param name="value"><see langword="true"/> for the default target; <see langword="false"/> to leave focus where it is.</param>
    /// <returns>A <see cref="FocusTargetValue"/> that holds <paramref name="value"/>.</returns>
    public static implicit operator FocusTargetValue(bool value) => new(value);

    /// <summary>Converts an element to a <see cref="FocusTargetValue"/> that holds it.</summary>
    /// <param name="value">The element.</param>
    /// <returns>A <see cref="FocusTargetValue"/> that holds <paramref name="value"/>.</returns>
    public static implicit operator FocusTargetValue(ElementReference value) => new(value);

    /// <summary>Converts an element's id to a <see cref="FocusTargetValue"/> that holds it.</summary>
    /// <param name="value">The id.</param>
    /// <returns>A <see cref="FocusTargetValue"/> that holds <paramref name="value"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is <see langword="null"/>.</exception>
    public static implicit operator FocusTargetValue(string value) => new(value);

    /// <summary>Gets the boolean, if this instance holds one.</summary>
    /// <param name="value">When this method returns, the boolean if one is held; otherwise, <see langword="false"/>.</param>
    /// <returns><see langword="true"/> if a boolean is held; otherwise, <see langword="false"/>.</returns>
    public bool TryGetValue(out bool value)
    {
        value = focus;
        return kind == TargetKind.Boolean;
    }

    /// <summary>Gets the element, if this instance holds one.</summary>
    /// <param name="value">When this method returns, the element if one is held; otherwise, the default reference.</param>
    /// <returns><see langword="true"/> if an element is held; otherwise, <see langword="false"/>.</returns>
    public bool TryGetValue(out ElementReference value)
    {
        value = element;
        return kind == TargetKind.Element;
    }

    /// <summary>Gets the element's id, if this instance holds one.</summary>
    /// <param name="value">When this method returns, the id if one is held; otherwise, <see langword="null"/>.</param>
    /// <returns><see langword="true"/> if an id is held; otherwise, <see langword="false"/>.</returns>
    public bool TryGetValue([System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out string? value)
    {
        value = id;
        return kind == TargetKind.Id;
    }

    private enum TargetKind : byte
    {
        None,
        Boolean,
        Element,
        Id,
    }
}

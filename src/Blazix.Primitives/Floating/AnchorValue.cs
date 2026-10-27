using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Components;

namespace Blazix.Primitives;

// ADR 05 §4 (hand-written [Union] structs with the non-boxing access pattern; Duration is the model); ADR 08 §3 (Union
// types row, which names AnchorValue) and Amendments 2026-10-27: an element, an element id and a virtual rect, no function
// case (ADR 04 Amendments 2026-10-27: Tooltip's cursor and PreviewCard's hovered line are JavaScript's end to end). Base
// UI's `anchor` (internals/useAnchorPositioning.ts:666-680); FLT-API-11, FLT-STA-07, FLT-POS-13. Value equality without
// boxing (ADR 16 Amendments 2026-10-27), so an equal re-render sends no update.
/// <summary>Represents the anchor a popup is positioned against: an element, the id of an element, or a virtual rectangle.</summary>
/// <remarks>
/// <para>
/// This is a hand-written union, marked <see cref="UnionAttribute"/>, with the non-boxing access pattern:
/// <see cref="HasValue"/>, <see cref="TryGetValue(out ElementReference)"/>, <see cref="TryGetValue(out string)"/> and
/// <see cref="TryGetValue(out VirtualAnchor)"/>. <see cref="Value"/> exists because <see cref="IUnion"/> requires it.
/// </para>
/// <para>
/// In Razor, an element id is written as plain text, such as <c>Anchor="my-anchor"</c>; an element as
/// <c>Anchor="@anchorElement"</c>. The default instance, a <see langword="null"/> id and a <see langword="null"/> virtual
/// anchor have no value. Two instances are equal when they hold the same case and value; elements compare by
/// <see cref="ElementReference.Id"/>.
/// </para>
/// </remarks>
[Union]
public readonly struct AnchorValue : IUnion, IEquatable<AnchorValue>
{
    private readonly ElementReference element;
    private readonly object? reference;
    private readonly AnchorKind kind;

    /// <summary>Initializes a new instance of the <see cref="AnchorValue"/> struct that holds an element.</summary>
    /// <param name="value">The element.</param>
    public AnchorValue(ElementReference value)
    {
        element = value;
        kind = AnchorKind.Element;
    }

    /// <summary>Initializes a new instance of the <see cref="AnchorValue"/> struct that holds the id of an element.</summary>
    /// <param name="value">The id of the element, looked up in the popup's document; <see langword="null"/> for no value.</param>
    public AnchorValue(string? value)
    {
        reference = value;
        kind = value is null ? AnchorKind.None : AnchorKind.Id;
    }

    /// <summary>Initializes a new instance of the <see cref="AnchorValue"/> struct that holds a virtual rectangle.</summary>
    /// <param name="value">The rectangle; <see langword="null"/> for no value.</param>
    public AnchorValue(VirtualAnchor? value)
    {
        reference = value;
        kind = value is null ? AnchorKind.None : AnchorKind.Virtual;
    }

    /// <summary>Gets a value indicating whether a value is held.</summary>
    /// <value><see langword="true"/> if a value is held; otherwise, <see langword="false"/>. The default instance holds no value.</value>
    public bool HasValue => kind != AnchorKind.None;

    // BLZX2005: IUnion requires an object-typed Value, so reading it boxes the element case; the library reads the cases
    // through TryGetValue instead.
#pragma warning disable BLZX2005
    /// <inheritdoc />
    /// <remarks>Reading it boxes an element.</remarks>
    public object? Value => kind switch
    {
        AnchorKind.Element => element,
        AnchorKind.Id or AnchorKind.Virtual => reference,
        _ => null,
    };
#pragma warning restore BLZX2005

    /// <summary>Converts an element to an <see cref="AnchorValue"/> that holds it.</summary>
    /// <param name="value">The element.</param>
    /// <returns>An <see cref="AnchorValue"/> that holds <paramref name="value"/>.</returns>
    public static implicit operator AnchorValue(ElementReference value) => new(value);

    /// <summary>Converts the id of an element to an <see cref="AnchorValue"/> that holds it.</summary>
    /// <param name="value">The id of the element; <see langword="null"/> for no value.</param>
    /// <returns>An <see cref="AnchorValue"/> that holds <paramref name="value"/>.</returns>
    public static implicit operator AnchorValue(string? value) => new(value);

    /// <summary>Converts a virtual rectangle to an <see cref="AnchorValue"/> that holds it.</summary>
    /// <param name="value">The rectangle; <see langword="null"/> for no value.</param>
    /// <returns>An <see cref="AnchorValue"/> that holds <paramref name="value"/>.</returns>
    public static implicit operator AnchorValue(VirtualAnchor? value) => new(value);

    /// <summary>Determines whether two anchors hold the same case and value.</summary>
    /// <param name="left">The first anchor.</param>
    /// <param name="right">The second anchor.</param>
    /// <returns><see langword="true"/> if the anchors are equal; otherwise, <see langword="false"/>.</returns>
    public static bool operator ==(AnchorValue left, AnchorValue right) => left.Equals(right);

    /// <summary>Determines whether two anchors differ in case or value.</summary>
    /// <param name="left">The first anchor.</param>
    /// <param name="right">The second anchor.</param>
    /// <returns><see langword="true"/> if the anchors differ; otherwise, <see langword="false"/>.</returns>
    public static bool operator !=(AnchorValue left, AnchorValue right) => !left.Equals(right);

    /// <summary>Gets the element, if this instance holds one.</summary>
    /// <param name="value">When this method returns, the element if one is held; otherwise, the default element reference.</param>
    /// <returns><see langword="true"/> if an element is held; otherwise, <see langword="false"/>.</returns>
    public bool TryGetValue(out ElementReference value)
    {
        value = element;
        return kind == AnchorKind.Element;
    }

    /// <summary>Gets the id of an element, if this instance holds one.</summary>
    /// <param name="value">When this method returns, the id if one is held; otherwise, <see langword="null"/>.</param>
    /// <returns><see langword="true"/> if an id is held; otherwise, <see langword="false"/>.</returns>
    public bool TryGetValue([NotNullWhen(true)] out string? value)
    {
        value = kind == AnchorKind.Id ? (string)reference! : null;
        return value is not null;
    }

    /// <summary>Gets the virtual rectangle, if this instance holds one.</summary>
    /// <param name="value">When this method returns, the rectangle if one is held; otherwise, <see langword="null"/>.</param>
    /// <returns><see langword="true"/> if a rectangle is held; otherwise, <see langword="false"/>.</returns>
    public bool TryGetValue([NotNullWhen(true)] out VirtualAnchor? value)
    {
        value = kind == AnchorKind.Virtual ? (VirtualAnchor)reference! : null;
        return value is not null;
    }

    /// <inheritdoc />
    public bool Equals(AnchorValue other) => kind == other.kind && kind switch
    {
        AnchorKind.Element => string.Equals(element.Id, other.element.Id, StringComparison.Ordinal),
        AnchorKind.Id => string.Equals((string)reference!, (string)other.reference!, StringComparison.Ordinal),
        AnchorKind.Virtual => ((VirtualAnchor)reference!).Equals((VirtualAnchor)other.reference!),
        _ => true,
    };

    /// <inheritdoc />
    public override bool Equals([NotNullWhen(true)] object? obj) => obj is AnchorValue other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => kind switch
    {
        AnchorKind.Element => HashCode.Combine((int)kind, StringComparer.Ordinal.GetHashCode(element.Id ?? string.Empty)),
        AnchorKind.Id => HashCode.Combine((int)kind, StringComparer.Ordinal.GetHashCode((string)reference!)),
        AnchorKind.Virtual => HashCode.Combine((int)kind, reference!.GetHashCode()),
        _ => 0,
    };

    private enum AnchorKind : byte
    {
        None,
        Element,
        Id,
        Virtual,
    }
}

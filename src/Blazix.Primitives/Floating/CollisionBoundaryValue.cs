using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Components;

namespace Blazix.Primitives;

// ADR 05 §4 (hand-written [Union] structs; Duration is the model); ADR 08 Amendments 2026-10-27: the clipping
// ancestors, an element, a list of elements and a rect. Base UI's `collisionBoundary: 'clipping-ancestors' | Element |
// Element[] | Rect` (internals/useAnchorPositioning.ts:747), the literal mapped to the engine's 'clippingAncestors'
// (FLT-API-09). Value equality without boxing (ADR 16 Amendments 2026-10-27); a list compares item by item.
/// <summary>
/// Represents the area a positioned popup avoids overflowing: the clipping ancestors of the popup, an element, a list of
/// elements, or a rectangle.
/// </summary>
/// <remarks>
/// <para>
/// This is a hand-written union, marked <see cref="UnionAttribute"/>, with the non-boxing access pattern:
/// <see cref="HasValue"/>, <see cref="IsClippingAncestors"/>, <see cref="TryGetValue(out ElementReference)"/>,
/// <see cref="TryGetValue(out IReadOnlyList{ElementReference})"/> and <see cref="TryGetValue(out Rect)"/>.
/// <see cref="Value"/> exists because <see cref="IUnion"/> requires it.
/// </para>
/// <para>
/// The default instance has no value. Two instances are equal when they hold the same case and value; elements compare by
/// <see cref="ElementReference.Id"/>, and lists item by item in order.
/// </para>
/// </remarks>
[Union]
public readonly struct CollisionBoundaryValue : IUnion, IEquatable<CollisionBoundaryValue>
{
    private const string ClippingAncestorsLiteral = "clipping-ancestors";

    private readonly ElementReference element;
    private readonly object? reference;
    private readonly BoundaryKind kind;

    /// <summary>Initializes a new instance of the <see cref="CollisionBoundaryValue"/> struct that holds an element.</summary>
    /// <param name="value">The element whose bounds the popup stays within.</param>
    public CollisionBoundaryValue(ElementReference value)
    {
        element = value;
        kind = BoundaryKind.Element;
    }

    /// <summary>Initializes a new instance of the <see cref="CollisionBoundaryValue"/> struct that holds a list of elements.</summary>
    /// <param name="value">The elements whose shared bounds the popup stays within; <see langword="null"/> for no value.</param>
    public CollisionBoundaryValue(IReadOnlyList<ElementReference>? value)
    {
        reference = value;
        kind = value is null ? BoundaryKind.None : BoundaryKind.Elements;
    }

    /// <summary>Initializes a new instance of the <see cref="CollisionBoundaryValue"/> struct that holds a rectangle.</summary>
    /// <param name="value">The rectangle, in viewport coordinates, that the popup stays within; <see langword="null"/> for no value.</param>
    public CollisionBoundaryValue(Rect? value)
    {
        reference = value;
        kind = value is null ? BoundaryKind.None : BoundaryKind.Rect;
    }

    private CollisionBoundaryValue(BoundaryKind kind)
    {
        this.kind = kind;
    }

    /// <summary>Gets the boundary formed by the popup's clipping ancestors, which is the default.</summary>
    /// <value>An instance for which <see cref="IsClippingAncestors"/> is <see langword="true"/>.</value>
    public static CollisionBoundaryValue ClippingAncestors => new(BoundaryKind.ClippingAncestors);

    /// <summary>Gets a value indicating whether a value is held.</summary>
    /// <value><see langword="true"/> if a value is held; otherwise, <see langword="false"/>. The default instance holds no value.</value>
    public bool HasValue => kind != BoundaryKind.None;

    /// <summary>Gets a value indicating whether this instance is <see cref="ClippingAncestors"/>.</summary>
    /// <value><see langword="true"/> for the clipping ancestors; otherwise, <see langword="false"/>.</value>
    public bool IsClippingAncestors => kind == BoundaryKind.ClippingAncestors;

    // BLZX2005: IUnion requires an object-typed Value, so reading it boxes the element case; the library reads the cases
    // through TryGetValue instead.
#pragma warning disable BLZX2005
    /// <inheritdoc />
    /// <remarks>Reading it boxes an element. For <see cref="ClippingAncestors"/> it is the string <c>clipping-ancestors</c>.</remarks>
    public object? Value => kind switch
    {
        BoundaryKind.ClippingAncestors => ClippingAncestorsLiteral,
        BoundaryKind.Element => element,
        BoundaryKind.Elements or BoundaryKind.Rect => reference,
        _ => null,
    };
#pragma warning restore BLZX2005

    /// <summary>Converts an element to a <see cref="CollisionBoundaryValue"/> that holds it.</summary>
    /// <param name="value">The element.</param>
    /// <returns>A <see cref="CollisionBoundaryValue"/> that holds <paramref name="value"/>.</returns>
    public static implicit operator CollisionBoundaryValue(ElementReference value) => new(value);

    /// <summary>Converts an array of elements to a <see cref="CollisionBoundaryValue"/> that holds it.</summary>
    /// <param name="value">The elements; <see langword="null"/> for no value.</param>
    /// <returns>A <see cref="CollisionBoundaryValue"/> that holds <paramref name="value"/>.</returns>
    public static implicit operator CollisionBoundaryValue(ElementReference[]? value) => new(value);

    /// <summary>Converts a list of elements to a <see cref="CollisionBoundaryValue"/> that holds it.</summary>
    /// <param name="value">The elements; <see langword="null"/> for no value.</param>
    /// <returns>A <see cref="CollisionBoundaryValue"/> that holds <paramref name="value"/>.</returns>
    public static implicit operator CollisionBoundaryValue(List<ElementReference>? value) => new(value);

    /// <summary>Converts a rectangle to a <see cref="CollisionBoundaryValue"/> that holds it.</summary>
    /// <param name="value">The rectangle; <see langword="null"/> for no value.</param>
    /// <returns>A <see cref="CollisionBoundaryValue"/> that holds <paramref name="value"/>.</returns>
    public static implicit operator CollisionBoundaryValue(Rect? value) => new(value);

    /// <summary>Determines whether two boundaries hold the same case and value.</summary>
    /// <param name="left">The first boundary.</param>
    /// <param name="right">The second boundary.</param>
    /// <returns><see langword="true"/> if the boundaries are equal; otherwise, <see langword="false"/>.</returns>
    public static bool operator ==(CollisionBoundaryValue left, CollisionBoundaryValue right) => left.Equals(right);

    /// <summary>Determines whether two boundaries differ in case or value.</summary>
    /// <param name="left">The first boundary.</param>
    /// <param name="right">The second boundary.</param>
    /// <returns><see langword="true"/> if the boundaries differ; otherwise, <see langword="false"/>.</returns>
    public static bool operator !=(CollisionBoundaryValue left, CollisionBoundaryValue right) => !left.Equals(right);

    /// <summary>Gets the element, if this instance holds one.</summary>
    /// <param name="value">When this method returns, the element if one is held; otherwise, the default element reference.</param>
    /// <returns><see langword="true"/> if an element is held; otherwise, <see langword="false"/>.</returns>
    public bool TryGetValue(out ElementReference value)
    {
        value = element;
        return kind == BoundaryKind.Element;
    }

    /// <summary>Gets the list of elements, if this instance holds one.</summary>
    /// <param name="value">When this method returns, the list if one is held; otherwise, <see langword="null"/>.</param>
    /// <returns><see langword="true"/> if a list is held; otherwise, <see langword="false"/>.</returns>
    public bool TryGetValue([NotNullWhen(true)] out IReadOnlyList<ElementReference>? value)
    {
        value = kind == BoundaryKind.Elements ? (IReadOnlyList<ElementReference>)reference! : null;
        return value is not null;
    }

    /// <summary>Gets the rectangle, if this instance holds one.</summary>
    /// <param name="value">When this method returns, the rectangle if one is held; otherwise, <see langword="null"/>.</param>
    /// <returns><see langword="true"/> if a rectangle is held; otherwise, <see langword="false"/>.</returns>
    public bool TryGetValue([NotNullWhen(true)] out Rect? value)
    {
        value = kind == BoundaryKind.Rect ? (Rect)reference! : null;
        return value is not null;
    }

    /// <inheritdoc />
    public bool Equals(CollisionBoundaryValue other) => kind == other.kind && kind switch
    {
        BoundaryKind.Element => string.Equals(element.Id, other.element.Id, StringComparison.Ordinal),
        BoundaryKind.Elements => SameElements((IReadOnlyList<ElementReference>)reference!, (IReadOnlyList<ElementReference>)other.reference!),
        BoundaryKind.Rect => ((Rect)reference!).Equals((Rect)other.reference!),
        _ => true,
    };

    /// <inheritdoc />
    public override bool Equals([NotNullWhen(true)] object? obj) => obj is CollisionBoundaryValue other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode()
    {
        switch (kind)
        {
            case BoundaryKind.Element:
                return HashCode.Combine((int)kind, StringComparer.Ordinal.GetHashCode(element.Id ?? string.Empty));
            case BoundaryKind.Elements:
                var hash = new HashCode();
                hash.Add((int)kind);
                var elements = (IReadOnlyList<ElementReference>)reference!;
                // An indexed loop: a foreach over the interface would allocate its enumerator.
                for (int i = 0; i < elements.Count; i++)
                {
                    hash.Add(StringComparer.Ordinal.GetHashCode(elements[i].Id ?? string.Empty));
                }

                return hash.ToHashCode();
            case BoundaryKind.Rect:
                return HashCode.Combine((int)kind, reference!.GetHashCode());
            default:
                return (int)kind;
        }
    }

    private static bool SameElements(IReadOnlyList<ElementReference> left, IReadOnlyList<ElementReference> right)
    {
        if (ReferenceEquals(left, right))
        {
            return true;
        }

        if (left.Count != right.Count)
        {
            return false;
        }

        for (int i = 0; i < left.Count; i++)
        {
            if (!string.Equals(left[i].Id, right[i].Id, StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    private enum BoundaryKind : byte
    {
        None,
        ClippingAncestors,
        Element,
        Elements,
        Rect,
    }
}

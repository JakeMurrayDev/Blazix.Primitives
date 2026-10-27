using Microsoft.AspNetCore.Components;

namespace Blazix.Primitives;

// PSH §2 "trigger registry"; settled by the orchestrator 2026-10-27 under ADR 01 Amendments 2026-10-22 (registration by
// id at parameter set): Base UI keys the registry by DOM element identity; the port registers at parameter set, before an
// element exists, so a registration's identity is the trigger part that registered plus its element once it has one.
// Two registrations are the same element when they come from the same part with the same element reference.
/// <summary>Identifies one registered trigger element: the part that registered it and the element it renders, once known.</summary>
internal readonly struct PopupTriggerElement : IEquatable<PopupTriggerElement>
{
    /// <summary>Initializes a new instance of the <see cref="PopupTriggerElement"/> struct.</summary>
    /// <param name="owner">The trigger part, or its behavior, that registered the element.</param>
    /// <param name="element">The element the trigger renders, or <see langword="null"/> before its first render or under a substitute callback.</param>
    /// <exception cref="ArgumentNullException"><paramref name="owner"/> is <see langword="null"/>.</exception>
    public PopupTriggerElement(object owner, ElementReference? element = null)
    {
        ArgumentNullException.ThrowIfNull(owner);
        Owner = owner;
        Element = element;
    }

    /// <summary>Gets the part, or its behavior, that registered the element.</summary>
    public object Owner { get; }

    /// <summary>Gets the element the trigger renders, or <see langword="null"/> when it has none yet.</summary>
    public ElementReference? Element { get; }

    /// <summary>Determines whether two registrations are the same element.</summary>
    /// <param name="left">The first registration.</param>
    /// <param name="right">The second registration.</param>
    /// <returns><see langword="true"/> if both come from the same part with the same element.</returns>
    public static bool operator ==(PopupTriggerElement left, PopupTriggerElement right) => left.Equals(right);

    /// <summary>Determines whether two registrations are different elements.</summary>
    /// <param name="left">The first registration.</param>
    /// <param name="right">The second registration.</param>
    /// <returns><see langword="true"/> if they come from different parts or carry different elements.</returns>
    public static bool operator !=(PopupTriggerElement left, PopupTriggerElement right) => !left.Equals(right);

    /// <inheritdoc />
    public bool Equals(PopupTriggerElement other)
    {
        if (!ReferenceEquals(Owner, other.Owner))
        {
            return false;
        }

        return Element is { } element
            ? other.Element is { } otherElement && string.Equals(element.Id, otherElement.Id, StringComparison.Ordinal) && ReferenceEquals(element.Context, otherElement.Context)
            : other.Element is null;
    }

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is PopupTriggerElement other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(Owner is null ? 0 : System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(Owner), Element?.Id);
}

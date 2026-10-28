using Blazix.Primitives.Interop;
using Microsoft.AspNetCore.Components;

namespace Blazix.Primitives;

// What a Popup part hands its FocusManagerBehavior on every parameter set (design point 6 of the focus-manager track,
// settled by the orchestrator 2026-10-27): FLT §6.10's props as the part resolved them. The targets arrive resolved, the
// delegate form called by the family with the open or close interaction type (PSH-FOC-03, PSH-FOC-05). Equality is by
// value and allocates nothing (ADR 16 rule 7): the targets compare by case, elements by id and renderer, and the lists by
// item, so a parameter set that changes nothing builds and sends nothing.
/// <summary>The inputs of a popup's focus manager for one parameter set.</summary>
internal sealed record FocusManagerInputs
{
    /// <summary>Gets a value indicating whether the popup is open.</summary>
    /// <value><see langword="true"/> while open; the default is <see langword="false"/>.</value>
    public bool Open { get; init; }

    /// <summary>Gets a value indicating whether focus is trapped and the outside hidden from assistive technology.</summary>
    /// <value><see langword="true"/>, the default, for a modal focus manager.</value>
    public bool Modal { get; init; } = true;

    /// <summary>Gets a value indicating whether the focus manager does nothing at all.</summary>
    /// <value><see langword="true"/> while the popup is not mounted, or opened by hover; the default is <see langword="false"/>.</value>
    public bool Disabled { get; init; }

    /// <summary>Gets where focus moves when the popup opens, as resolved for this open.</summary>
    /// <value>
    /// <see langword="false"/> to move no focus; <see langword="true"/>, an element or an element's id; <see langword="null"/>,
    /// the default, for the popup's first tabbable element, else the popup.
    /// </value>
    public FocusTargetValue? InitialFocus { get; init; }

    /// <summary>Gets where focus returns when the popup closes, as resolved for this close.</summary>
    /// <value>
    /// <see langword="false"/> to move no focus; <see langword="true"/>, an element or an element's id; <see langword="null"/>,
    /// the default, for the trigger, else the element focused before the popup opened.
    /// </value>
    public FocusTargetValue? ReturnFocus { get; init; }

    /// <summary>Gets a value indicating whether return focus moves focus even when focus has already left the popup.</summary>
    /// <value><see langword="null"/>, the default, for whether <see cref="ReturnFocus"/> names a target.</value>
    public bool? ExplicitReturnFocus { get; init; }

    /// <summary>Gets where focus goes when the focused element inside the popup is removed or hidden.</summary>
    /// <value>One of <see cref="FocusManagerTwin.RestoreNone"/>, the default, <see cref="FocusManagerTwin.RestoreTabbable"/> and <see cref="FocusManagerTwin.RestorePopup"/>.</value>
    public string RestoreFocus { get; init; } = FocusManagerTwin.RestoreNone;

    /// <summary>Gets a value indicating whether a non-modal popup closes when focus leaves it.</summary>
    /// <value><see langword="true"/>, the default.</value>
    public bool CloseOnFocusOut { get; init; } = true;

    /// <summary>Gets how the user opened the popup.</summary>
    /// <value>The interaction type; <see langword="null"/> for a programmatic open; the default is <see cref="InteractionType.Unknown"/>.</value>
    public InteractionType? OpenInteractionType { get; init; } = InteractionType.Unknown;

    /// <summary>Gets the snapshot of the event that caused the last committed close.</summary>
    /// <value>The snapshot, the placeholder for a close no event caused, or <see langword="null"/>, the default, before any close.</value>
    public EventSnapshot? CloseEvent { get; init; }

    /// <summary>Gets the selector the popup element matches while it is rendered open.</summary>
    /// <value>The selector, or <see langword="null"/>, the default, for <c>[data-open]</c>.</value>
    public string? OpenSelector { get; init; }

    /// <summary>Gets a value indicating whether the popup element is the floating element rather than its parent.</summary>
    /// <value><see langword="true"/> for a popup with no positioner (Dialog); the default is <see langword="false"/>.</value>
    public bool TreatPopupAsFloatingElement { get; init; }

    /// <summary>Gets the id of the anchor element, the active trigger.</summary>
    /// <value>The id, or <see langword="null"/>, the default, for none.</value>
    public string? DomReferenceId { get; init; }

    /// <summary>Gets a value indicating whether the anchor carries the trigger focus guards (Popover).</summary>
    /// <value><see langword="true"/> to treat the guards around the anchor as Tab's way out of the popup; the default is <see langword="false"/>.</value>
    public bool TriggerFocusGuards { get; init; }

    /// <summary>Gets the element focused when Tab leaves the popup backward (Menu).</summary>
    /// <value>The element, or <see langword="null"/>, the default, for none.</value>
    public ElementReference? PreviousFocusableElement { get; init; }

    /// <summary>Gets the element focused when Tab leaves the popup forward (Menu).</summary>
    /// <value>The element, or <see langword="null"/>, the default, for none.</value>
    public ElementReference? NextFocusableElement { get; init; }

    /// <summary>Gets elements outside the popup that count as inside it.</summary>
    /// <value>The elements, or <see langword="null"/>, the default, for none.</value>
    public IReadOnlyList<ElementReference>? InsideElements { get; init; }

    /// <summary>Gets the ids of elements outside the popup that count as inside it.</summary>
    /// <value>The ids, or <see langword="null"/>, the default, for none.</value>
    public IReadOnlyList<string>? InsideElementIds { get; init; }

    /// <summary>Gets the last open change the popup committed.</summary>
    /// <value>The relay, or <see langword="null"/>, the default, before the first committed change.</value>
    public OpenChangeRelay? Relay { get; init; }

    /// <summary>Gets the token of the popup's interactions session (the popup store's <c>SessionToken</c>).</summary>
    /// <value>The token, or <see langword="null"/>, the default, for none.</value>
    public string? SessionToken { get; init; }

    /// <inheritdoc />
    public bool Equals(FocusManagerInputs? other)
    {
        return other is not null
            && Open == other.Open
            && Modal == other.Modal
            && Disabled == other.Disabled
            && SameTarget(InitialFocus, other.InitialFocus)
            && SameTarget(ReturnFocus, other.ReturnFocus)
            && ExplicitReturnFocus == other.ExplicitReturnFocus
            && string.Equals(RestoreFocus, other.RestoreFocus, StringComparison.Ordinal)
            && CloseOnFocusOut == other.CloseOnFocusOut
            && OpenInteractionType == other.OpenInteractionType
            && Equals(CloseEvent, other.CloseEvent)
            && string.Equals(OpenSelector, other.OpenSelector, StringComparison.Ordinal)
            && TreatPopupAsFloatingElement == other.TreatPopupAsFloatingElement
            && string.Equals(DomReferenceId, other.DomReferenceId, StringComparison.Ordinal)
            && TriggerFocusGuards == other.TriggerFocusGuards
            && SameElement(PreviousFocusableElement, other.PreviousFocusableElement)
            && SameElement(NextFocusableElement, other.NextFocusableElement)
            && SameElements(InsideElements, other.InsideElements)
            && SameIds(InsideElementIds, other.InsideElementIds)
            && Equals(Relay, other.Relay)
            && string.Equals(SessionToken, other.SessionToken, StringComparison.Ordinal);
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        HashCode hash = default;
        hash.Add(Open);
        hash.Add(Modal);
        hash.Add(Disabled);
        hash.Add(InitialFocus?.HasValue);
        hash.Add(ReturnFocus?.HasValue);
        hash.Add(ExplicitReturnFocus);
        hash.Add(RestoreFocus, StringComparer.Ordinal);
        hash.Add(CloseOnFocusOut);
        hash.Add(OpenInteractionType);
        hash.Add(CloseEvent);
        hash.Add(OpenSelector, StringComparer.Ordinal);
        hash.Add(TreatPopupAsFloatingElement);
        hash.Add(DomReferenceId, StringComparer.Ordinal);
        hash.Add(TriggerFocusGuards);
        hash.Add(PreviousFocusableElement?.Id, StringComparer.Ordinal);
        hash.Add(NextFocusableElement?.Id, StringComparer.Ordinal);
        hash.Add(InsideElements?.Count ?? -1);
        hash.Add(InsideElementIds?.Count ?? -1);
        hash.Add(Relay);
        hash.Add(SessionToken, StringComparer.Ordinal);
        return hash.ToHashCode();
    }

    /// <summary>Determines whether two element references name the same element of the same renderer.</summary>
    /// <param name="left">The first reference.</param>
    /// <param name="right">The second reference.</param>
    /// <returns><see langword="true"/> if their ids and contexts are the same; otherwise, <see langword="false"/>.</returns>
    internal static bool SameElement(ElementReference left, ElementReference right) =>
        string.Equals(left.Id, right.Id, StringComparison.Ordinal) && ReferenceEquals(left.Context, right.Context);

    private static bool SameElement(ElementReference? left, ElementReference? right) =>
        left is { } l ? right is { } r && SameElement(l, r) : right is null;

    private static bool SameTarget(FocusTargetValue? left, FocusTargetValue? right)
    {
        if (left is not { } l || right is not { } r)
        {
            return left is null && right is null;
        }

        if (l.TryGetValue(out bool leftFocus))
        {
            return r.TryGetValue(out bool rightFocus) && leftFocus == rightFocus;
        }

        if (l.TryGetValue(out ElementReference leftElement))
        {
            return r.TryGetValue(out ElementReference rightElement) && SameElement(leftElement, rightElement);
        }

        if (l.TryGetValue(out string? leftId))
        {
            return r.TryGetValue(out string? rightId) && string.Equals(leftId, rightId, StringComparison.Ordinal);
        }

        return !r.HasValue;
    }

    private static bool SameElements(IReadOnlyList<ElementReference>? left, IReadOnlyList<ElementReference>? right)
    {
        if (ReferenceEquals(left, right))
        {
            return true;
        }

        if (left is null || right is null || left.Count != right.Count)
        {
            return false;
        }

        for (int i = 0; i < left.Count; i++)
        {
            if (!SameElement(left[i], right[i]))
            {
                return false;
            }
        }

        return true;
    }

    private static bool SameIds(IReadOnlyList<string>? left, IReadOnlyList<string>? right)
    {
        if (ReferenceEquals(left, right))
        {
            return true;
        }

        if (left is null || right is null || left.Count != right.Count)
        {
            return false;
        }

        for (int i = 0; i < left.Count; i++)
        {
            if (!string.Equals(left[i], right[i], StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }
}

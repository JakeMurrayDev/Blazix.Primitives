using System.Text.Json.Serialization;

namespace Blazix.Primitives.Interop;

// ADR 04 §1 (the `focus-manager` row) and Amendments 2026-10-27 (chosen by the user 2026-10-27): the module reads the
// open state from the applied batch through the element matching OpenSelector, never from these options after attach;
// InitialFocus and ReturnFocus arrive resolved (ADR 04 Amendments 2026-10-27, "Focus targets"; ADR 08 Amendments
// 2026-10-27, "Unions"): a boolean plus an element, by the index of a reference passed with the options (the popup
// core's references array, ADR 04 Amendments 2026-10-27; open until it is on `main`), or an element id. The names are
// FLT §6.10's props (design point 8 of the focus-manager track, settled by the orchestrator 2026-10-27); FinalFocus maps to
// ReturnFocus. OpenInteractionType and CloseInteractionType are the InteractionType literals until the enum is on
// `main` (Interop/CONTEXT.md). Answered is the answered-requests counter and PreviousFocusableElement and
// NextFocusableElement are Menu's indices beside TriggerFocusGuards (ADR 04 Amendments 2026-10-27, the verification of
// PR 128, settled by the orchestrator). FLT-API-05, FLT-FOC-06, FOC-07, FOC-11, FOC-12, FOC-19..21, ARIA-01..03, DOM-17, KEY-06.
/// <summary>
/// The options of the <c>focus-manager</c> module, sent on <c>attach</c> and on every <c>update</c>: what the focus manager
/// of a popup does when the popup opens, while it is open and when it closes.
/// </summary>
internal sealed record FocusManagerOptions : TwinOptions
{
    /// <summary>Gets a value indicating whether focus is trapped inside the popup and the outside is hidden from assistive technology.</summary>
    /// <value><see langword="true"/>, the default, for a modal focus manager; <see langword="false"/> to mark the outside with the inert marker only.</value>
    public bool Modal { get; init; } = true;

    /// <summary>Gets a value indicating whether the focus manager does nothing at all.</summary>
    /// <value><see langword="true"/> while the popup is not mounted, or opened by hover; the default is <see langword="false"/>.</value>
    public bool Disabled { get; init; }

    /// <summary>Gets a value indicating whether focus moves into the popup when it opens.</summary>
    /// <value>
    /// <see langword="true"/>, the default, to focus the target named by <see cref="InitialFocusElement"/> or
    /// <see cref="InitialFocusId"/>, else the popup's first tabbable element, else the popup; <see langword="false"/> to move
    /// no focus.
    /// </value>
    public bool InitialFocus { get; init; } = true;

    /// <summary>Gets the element to focus when the popup opens, as the index of its reference among the references sent with the options.</summary>
    /// <value>The index, or <see langword="null"/>, the default, for no element target.</value>
    public int? InitialFocusElement { get; init; }

    /// <summary>Gets the id of the element to focus when the popup opens.</summary>
    /// <value>The id, or <see langword="null"/>, the default, for no id target. An id that names no element falls back to the default target.</value>
    public string? InitialFocusId { get; init; }

    /// <summary>Gets a value indicating whether focus returns when the popup closes.</summary>
    /// <value>
    /// <see langword="true"/>, the default, to focus the target named by <see cref="ReturnFocusElement"/> or
    /// <see cref="ReturnFocusId"/>, else the trigger or the element focused before the popup opened; <see langword="false"/>
    /// to move no focus.
    /// </value>
    public bool ReturnFocus { get; init; } = true;

    /// <summary>Gets the element to focus when the popup closes, as the index of its reference among the references sent with the options.</summary>
    /// <value>The index, or <see langword="null"/>, the default, for no element target.</value>
    public int? ReturnFocusElement { get; init; }

    /// <summary>Gets the id of the element to focus when the popup closes.</summary>
    /// <value>The id, or <see langword="null"/>, the default, for no id target.</value>
    public string? ReturnFocusId { get; init; }

    /// <summary>Gets a value indicating whether return focus moves focus even when focus has already left the popup.</summary>
    /// <value>
    /// <see langword="true"/> or <see langword="false"/> to decide it; <see langword="null"/>, the default, for
    /// <see langword="true"/> exactly when an element or id target is set.
    /// </value>
    public bool? ExplicitReturnFocus { get; init; }

    /// <summary>Gets the interaction type of the close, as C# classified it.</summary>
    /// <value>
    /// <c>mouse</c>, <c>touch</c>, <c>pen</c>, <c>keyboard</c> or the empty string; <see langword="null"/>, the default,
    /// when no close has been classified. Return focus after a <c>keyboard</c> close asks for a visible focus indicator.
    /// </value>
    public string? CloseInteractionType { get; init; }

    /// <summary>Gets where focus goes when the focused element inside the popup is removed or hidden.</summary>
    /// <value>
    /// <see cref="FocusManagerTwin.RestoreNone"/>, the default, to restore nothing; <see cref="FocusManagerTwin.RestoreTabbable"/>
    /// for the nearest tabbable element inside; <see cref="FocusManagerTwin.RestorePopup"/> for the popup itself.
    /// </value>
    public string RestoreFocus { get; init; } = FocusManagerTwin.RestoreNone;

    /// <summary>Gets a value indicating whether a non-modal popup closes when focus leaves it.</summary>
    /// <value><see langword="true"/>, the default, to close it with a <c>focus-out</c> request and to restore focus inside.</value>
    public bool CloseOnFocusOut { get; init; } = true;

    /// <summary>Gets the interaction type that opened the popup.</summary>
    /// <value>
    /// <c>mouse</c>, <c>touch</c>, <c>pen</c>, <c>keyboard</c> or the empty string, the default; <see langword="null"/>
    /// for a programmatic open, after which return focus prefers the element focused before the open over the trigger.
    /// </value>
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public string? OpenInteractionType { get; init; } = "";

    /// <summary>Gets the selector the popup element matches while C# renders it open.</summary>
    /// <value>The selector, or <see langword="null"/>, the default, for <c>[data-open]</c>.</value>
    public string? OpenSelector { get; init; }

    /// <summary>Gets a value indicating whether the popup element is itself the floating element.</summary>
    /// <value>
    /// <see langword="true"/> for a popup with no positioner; <see langword="false"/>, the default, when the popup's parent
    /// element, its positioner, is the floating element.
    /// </value>
    public bool TreatPopupAsFloatingElement { get; init; }

    /// <summary>Gets the id of the popup's anchor element, its active trigger.</summary>
    /// <value>The id, or <see langword="null"/>, the default, for none.</value>
    public string? DomReferenceId { get; init; }

    /// <summary>Gets a value indicating whether the anchor element carries the trigger focus guards rendered beside it.</summary>
    /// <value>
    /// <see langword="true"/> to treat the guard before the anchor as inside the popup, the anchor as the element before the
    /// popup in focus order and the guard after it as the element after; the default is <see langword="false"/>.
    /// </value>
    public bool TriggerFocusGuards { get; init; }

    /// <summary>Gets the element focused when Tab leaves the popup backward, as the index of its reference among the references sent with the options.</summary>
    /// <value>The index, or <see langword="null"/>, the default, for none; <see cref="TriggerFocusGuards"/> supplies the anchor instead.</value>
    public int? PreviousFocusableElement { get; init; }

    /// <summary>Gets the element focused when Tab leaves the popup forward, as the index of its reference among the references sent with the options.</summary>
    /// <value>The index, or <see langword="null"/>, the default, for none; <see cref="TriggerFocusGuards"/> supplies the guard after the anchor instead.</value>
    public int? NextFocusableElement { get; init; }

    /// <summary>Gets elements outside the popup that count as inside it, as indices of their references among the references sent with the options.</summary>
    /// <value>The indices, or <see langword="null"/>, the default, for none.</value>
    public IReadOnlyList<int>? InsideElements { get; init; }

    /// <summary>Gets the ids of elements outside the popup that count as inside it.</summary>
    /// <value>The ids, or <see langword="null"/>, the default, for none.</value>
    public IReadOnlyList<string>? InsideElementIds { get; init; }

    /// <summary>Gets how many of the module's requests C# has answered.</summary>
    /// <value>
    /// The count, raised for every focus-out close request C# routed, canceled or not; the default is 0. The module asks
    /// again for a focus-out close once the count moves.
    /// </value>
    public int Answered { get; init; }

    /// <inheritdoc />
    /// <remarks>The lists are compared by their items, so an unchanged render compares equal and sends no <c>update</c>.</remarks>
    public bool Equals(FocusManagerOptions? other)
    {
        return other is not null
            && string.Equals(Style, other.Style, StringComparison.Ordinal)
            && Modal == other.Modal
            && Disabled == other.Disabled
            && InitialFocus == other.InitialFocus
            && InitialFocusElement == other.InitialFocusElement
            && string.Equals(InitialFocusId, other.InitialFocusId, StringComparison.Ordinal)
            && ReturnFocus == other.ReturnFocus
            && ReturnFocusElement == other.ReturnFocusElement
            && string.Equals(ReturnFocusId, other.ReturnFocusId, StringComparison.Ordinal)
            && ExplicitReturnFocus == other.ExplicitReturnFocus
            && string.Equals(CloseInteractionType, other.CloseInteractionType, StringComparison.Ordinal)
            && string.Equals(RestoreFocus, other.RestoreFocus, StringComparison.Ordinal)
            && CloseOnFocusOut == other.CloseOnFocusOut
            && string.Equals(OpenInteractionType, other.OpenInteractionType, StringComparison.Ordinal)
            && string.Equals(OpenSelector, other.OpenSelector, StringComparison.Ordinal)
            && TreatPopupAsFloatingElement == other.TreatPopupAsFloatingElement
            && string.Equals(DomReferenceId, other.DomReferenceId, StringComparison.Ordinal)
            && TriggerFocusGuards == other.TriggerFocusGuards
            && PreviousFocusableElement == other.PreviousFocusableElement
            && NextFocusableElement == other.NextFocusableElement
            && SameItems(InsideElements, other.InsideElements)
            && SameItems(InsideElementIds, other.InsideElementIds)
            && Answered == other.Answered;
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        HashCode hash = default;
        hash.Add(Style, StringComparer.Ordinal);
        hash.Add(Modal);
        hash.Add(Disabled);
        hash.Add(InitialFocus);
        hash.Add(InitialFocusElement);
        hash.Add(InitialFocusId, StringComparer.Ordinal);
        hash.Add(ReturnFocus);
        hash.Add(ReturnFocusElement);
        hash.Add(ReturnFocusId, StringComparer.Ordinal);
        hash.Add(ExplicitReturnFocus);
        hash.Add(CloseInteractionType, StringComparer.Ordinal);
        hash.Add(RestoreFocus, StringComparer.Ordinal);
        hash.Add(CloseOnFocusOut);
        hash.Add(OpenInteractionType, StringComparer.Ordinal);
        hash.Add(OpenSelector, StringComparer.Ordinal);
        hash.Add(TreatPopupAsFloatingElement);
        hash.Add(DomReferenceId, StringComparer.Ordinal);
        hash.Add(TriggerFocusGuards);
        hash.Add(PreviousFocusableElement);
        hash.Add(NextFocusableElement);
        hash.Add(Answered);
        hash.Add(InsideElements?.Count ?? -1);
        hash.Add(InsideElementIds?.Count ?? -1);
        return hash.ToHashCode();
    }

    private static bool SameItems<T>(IReadOnlyList<T>? a, IReadOnlyList<T>? b)
    {
        if (a is null || b is null)
        {
            return a is null && b is null;
        }

        if (a.Count != b.Count)
        {
            return false;
        }

        EqualityComparer<T> comparer = EqualityComparer<T>.Default;
        for (int i = 0; i < a.Count; i++)
        {
            if (!comparer.Equals(a[i], b[i]))
            {
                return false;
            }
        }

        return true;
    }
}

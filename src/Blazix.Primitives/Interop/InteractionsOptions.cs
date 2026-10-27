using System.Text.Json.Serialization;

namespace Blazix.Primitives.Interop;

// ADR 04 §1 (the interactions row) and Amendments 2026-10-27 (decision 3: the token form and the session; the relay;
// press; Prevent; the hover timers and the delay group; Dialog's outsidePress predicates; chosen by the user
// 2026-10-27). FLT §6.3-§6.9 name the options; design point 6 of M4 track 3 (the naming table, accepted by the
// orchestrator 2026-10-27) maps them. One record per twin with one sub-record per attaching role (the FormOptions
// precedent; design point 2, settled by the orchestrator 2026-10-27). Every option is sent only when set; a role's hook sub-record left null turns that hook off. Records compare by
// value, so an unchanged render sends nothing (ADR 16 rule 7). The relay member arrives with track 1's record.
/// <summary>
/// The options of the <c>interactions</c> module, sent on <c>attach</c> and on every <c>update</c>. Exactly one role is set
/// per handle: a popup root's session, a trigger, the floating element or a tooltip provider.
/// </summary>
/// <remarks>The module owns no style property, so a part leaves <see cref="TwinOptions.Style"/> unset.</remarks>
internal sealed record InteractionsOptions : TwinOptions
{
    /// <summary>Gets the token of the popup session the handle joins.</summary>
    /// <value>The token the popup root drew and cascades; it is never rendered. <see langword="null"/> for a provider.</value>
    public string? SessionToken { get; init; }

    /// <summary>Gets the options of a popup root's session handle, which has no element.</summary>
    /// <value>The session options, or <see langword="null"/> for another role.</value>
    public InteractionsSessionOptions? Session { get; init; }

    /// <summary>Gets the options of a trigger's handle.</summary>
    /// <value>The trigger options, or <see langword="null"/> for another role.</value>
    public InteractionsTriggerOptions? Trigger { get; init; }

    /// <summary>Gets the options of the floating element's handle.</summary>
    /// <value>The floating options, or <see langword="null"/> for another role.</value>
    public InteractionsFloatingOptions? Floating { get; init; }

    /// <summary>Gets the options of a tooltip provider's handle, which has no element.</summary>
    /// <value>The provider options, or <see langword="null"/> for another role.</value>
    public InteractionsProviderOptions? Provider { get; init; }
}

/// <summary>The options of a popup root's session: the committed open state and the dismissal configuration.</summary>
internal sealed record InteractionsSessionOptions
{
    /// <summary>Gets a value indicating whether the popup is open.</summary>
    /// <value>The open state the popup committed.</value>
    public bool Open { get; init; }

    /// <summary>Gets the id of the trigger the popup is anchored to.</summary>
    /// <value>The trigger's id in the popup's trigger map, or <see langword="null"/> while no trigger is active.</value>
    public string? ActiveTriggerId { get; init; }

    /// <summary>Gets the session token of the enclosing popup.</summary>
    /// <value>The token the enclosing popup cascades, or <see langword="null"/> at the top level.</value>
    public string? ParentToken { get; init; }

    /// <summary>Gets the id of the popup element.</summary>
    /// <value>The id, or <see langword="null"/> while the popup renders no element.</value>
    public string? FloatingId { get; init; }

    /// <summary>Gets a value indicating whether the popup is in its closing transition.</summary>
    /// <value><see langword="true"/> while the exit animation runs; the default <see langword="false"/> is not sent.</value>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool Ending { get; init; }

    /// <summary>Gets the dismissal configuration.</summary>
    /// <value>The configuration, or <see langword="null"/> when nothing dismisses the popup.</value>
    public InteractionsDismissOptions? Dismiss { get; init; }
}

/// <summary>The dismissal configuration of a popup: Escape and outside presses.</summary>
internal sealed record InteractionsDismissOptions
{
    /// <summary>Gets a value indicating whether Escape closes the popup.</summary>
    /// <value><see langword="false"/> to ignore Escape; <see langword="null"/>, the default, closes on Escape.</value>
    public bool? EscapeKey { get; init; }

    /// <summary>Gets a value indicating whether a press outside the popup and its triggers closes it.</summary>
    /// <value><see langword="false"/> to ignore outside presses; <see langword="null"/>, the default, closes on them.</value>
    public bool? OutsidePress { get; init; }

    /// <summary>Gets the press type an outside press is read as, per pointer type.</summary>
    /// <value>The press types, or <see langword="null"/> for <c>sloppy</c> on both.</value>
    public InteractionsPressEvents? OutsidePressEvent { get; init; }

    /// <summary>Gets a value indicating whether Escape also reaches the enclosing popups.</summary>
    /// <value><see langword="true"/> to let it bubble; <see langword="null"/>, the default, does not.</value>
    public bool? BubblesEscapeKey { get; init; }

    /// <summary>Gets a value indicating whether an outside press also reaches the enclosing popups.</summary>
    /// <value><see langword="false"/> to keep it; <see langword="null"/>, the default, lets it bubble.</value>
    public bool? BubblesOutsidePress { get; init; }

    /// <summary>Gets a value indicating whether only a primary-button press outside dismisses.</summary>
    /// <value><see langword="true"/> for a dialog; the default <see langword="false"/> is not sent.</value>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool PrimaryButtonOnly { get; init; }

    /// <summary>Gets a value indicating whether a press with more than one touch point does not dismiss.</summary>
    /// <value><see langword="true"/> for a dialog; the default <see langword="false"/> is not sent.</value>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool SingleTouch { get; init; }

    /// <summary>Gets the ids of the popup's own backdrops, a press on which dismisses it.</summary>
    /// <value>The ids, or <see langword="null"/> for none.</value>
    public IReadOnlyList<string>? OwnBackdropIds { get; init; }

    /// <summary>Gets a value indicating whether only the topmost open popup dismisses on an outside press.</summary>
    /// <value><see langword="true"/> for a dialog; the default <see langword="false"/> is not sent.</value>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool Topmost { get; init; }

    /// <inheritdoc />
    public bool Equals(InteractionsDismissOptions? other) =>
        other is not null
        && EscapeKey == other.EscapeKey
        && OutsidePress == other.OutsidePress
        && Equals(OutsidePressEvent, other.OutsidePressEvent)
        && BubblesEscapeKey == other.BubblesEscapeKey
        && BubblesOutsidePress == other.BubblesOutsidePress
        && PrimaryButtonOnly == other.PrimaryButtonOnly
        && SingleTouch == other.SingleTouch
        && Topmost == other.Topmost
        && (ReferenceEquals(OwnBackdropIds, other.OwnBackdropIds) || (OwnBackdropIds is not null && other.OwnBackdropIds is not null && OwnBackdropIds.SequenceEqual(other.OwnBackdropIds, StringComparer.Ordinal)));

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(EscapeKey, OutsidePress, OutsidePressEvent, BubblesEscapeKey, BubblesOutsidePress, PrimaryButtonOnly, SingleTouch, Topmost);
}

/// <summary>The press type an outside press is read as: <c>intentional</c> (a click whose press began inside the open popup's lifetime) or <c>sloppy</c> (the press itself).</summary>
/// <param name="Mouse">The press type for mouse and pen presses, or <see langword="null"/> for <c>sloppy</c>.</param>
/// <param name="Touch">The press type for touch presses, or <see langword="null"/> for <c>sloppy</c>.</param>
internal sealed record InteractionsPressEvents(string? Mouse, string? Touch);

/// <summary>The options of a trigger: its id and the hooks that run on it.</summary>
internal sealed record InteractionsTriggerOptions
{
    /// <summary>Gets the trigger's id in the popup's trigger map.</summary>
    /// <value>The id, or <see langword="null"/> for the element's <c>id</c>.</value>
    public string? TriggerId { get; init; }

    /// <summary>Gets the press-to-toggle configuration.</summary>
    /// <value>The configuration, or <see langword="null"/> when a press does not toggle.</value>
    public InteractionsClickOptions? Click { get; init; }

    /// <summary>Gets a value indicating whether a press on the trigger closes the open popup.</summary>
    /// <value><see langword="true"/> for a tooltip that closes on click; the default <see langword="false"/> is not sent.</value>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool ReferencePress { get; init; }

    /// <summary>Gets a value indicating whether a press on a closed popup's trigger cancels a pending hover open.</summary>
    /// <value><see langword="true"/> for a tooltip that closes on click; the default <see langword="false"/> is not sent.</value>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool CancelPendingOpenOnPress { get; init; }

    /// <summary>Gets the hover configuration.</summary>
    /// <value>The configuration, or <see langword="null"/> when hovering does nothing.</value>
    public InteractionsHoverOptions? Hover { get; init; }

    /// <summary>Gets the focus configuration.</summary>
    /// <value>The configuration, or <see langword="null"/> when focusing does nothing.</value>
    public InteractionsFocusOptions? Focus { get; init; }

    /// <summary>Gets the cursor-anchoring configuration.</summary>
    /// <value>The configuration, or <see langword="null"/> when the popup is anchored to the trigger.</value>
    public InteractionsClientPointOptions? ClientPoint { get; init; }

    /// <summary>Gets a value indicating whether the trigger records the hovered line of its text for the inline anchor.</summary>
    /// <value><see langword="true"/> for a preview card trigger; the default <see langword="false"/> is not sent.</value>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool HoveredLine { get; init; }
}

/// <summary>The press-to-toggle configuration of a trigger.</summary>
internal sealed record InteractionsClickOptions
{
    /// <summary>Gets the pointer event that toggles.</summary>
    /// <value><c>click</c>, <c>mousedown</c> or <c>mousedown-only</c>; <see langword="null"/>, the default, for <c>click</c>.</value>
    public string? Event { get; init; }

    /// <summary>Gets a value indicating whether a repeated press closes the popup.</summary>
    /// <value><see langword="false"/> to keep it open; <see langword="null"/>, the default, toggles.</value>
    public bool? Toggle { get; init; }

    /// <summary>Gets a value indicating whether strictly mouse-like presses are ignored.</summary>
    /// <value><see langword="true"/> to ignore them; the default <see langword="false"/> is not sent.</value>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool IgnoreMouse { get; init; }

    /// <summary>Gets a value indicating whether a press keeps a popup open that hover or focus opened.</summary>
    /// <value><see langword="false"/> to close it; <see langword="null"/>, the default, keeps it open.</value>
    public bool? StickIfOpen { get; init; }

    /// <summary>Gets a value indicating whether a press keeps a hover-opened popup open only within half a second of the hover change.</summary>
    /// <value><see langword="true"/> for a popover; the default <see langword="false"/> is not sent.</value>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool PatientClick { get; init; }

    /// <summary>Gets the delay before a touch press opens the popup.</summary>
    /// <value>The delay in milliseconds; the default 0 is not sent.</value>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int TouchOpenDelay { get; init; }

    /// <summary>Gets the reason of the request a press makes.</summary>
    /// <value><c>trigger-press</c> or <c>input-press</c>; <see langword="null"/>, the default, for <c>trigger-press</c>.</value>
    public string? Reason { get; init; }
}

/// <summary>The hover configuration of a trigger.</summary>
internal sealed record InteractionsHoverOptions
{
    /// <summary>Gets the delay before a hover opens the popup.</summary>
    /// <value>The delay in milliseconds, or <see langword="null"/> for none.</value>
    public int? DelayOpen { get; init; }

    /// <summary>Gets the delay before leaving closes the popup.</summary>
    /// <value>The delay in milliseconds, or <see langword="null"/> for none.</value>
    public int? DelayClose { get; init; }

    /// <summary>Gets the time the pointer must rest on the trigger before a hover opens the popup.</summary>
    /// <value>The time in milliseconds; the default 0 is not sent.</value>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int RestMs { get; init; }

    /// <summary>Gets a value indicating whether the first pointer move over the trigger also opens the popup.</summary>
    /// <value><see langword="false"/> to open on entering only; <see langword="null"/>, the default, also opens on the first move.</value>
    public bool? Move { get; init; }

    /// <summary>Gets a value indicating whether only mouse-like pointers hover.</summary>
    /// <value><see langword="true"/> to ignore touch; the default <see langword="false"/> is not sent.</value>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool MouseOnly { get; init; }

    /// <summary>Gets a value indicating whether leaving the trigger toward the popup keeps it open along a safe triangle.</summary>
    /// <value><see langword="true"/> for a hoverable popup; the default <see langword="false"/> is not sent.</value>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool SafePolygon { get; init; }

    /// <summary>Gets a value indicating whether the safe triangle blocks pointer events on the rest of the page.</summary>
    /// <value><see langword="true"/> to block them; the default <see langword="false"/> is not sent.</value>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool BlockPointerEvents { get; init; }

    /// <summary>Gets a value indicating whether leaving the trigger's subtree cancels a pending open.</summary>
    /// <value><see langword="true"/> to cancel it; the default <see langword="false"/> is not sent.</value>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool GuardStaleOpen { get; init; }

    /// <summary>Gets a value indicating whether a nested tooltip trigger inside this trigger takes the hover over.</summary>
    /// <value><see langword="true"/> for a tooltip trigger; the default <see langword="false"/> is not sent.</value>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool NestedTooltipTriggers { get; init; }

    /// <summary>Gets the token of the tooltip provider whose delay group the trigger belongs to.</summary>
    /// <value>The provider's token, or <see langword="null"/> outside a provider.</value>
    public string? DelayGroupToken { get; init; }
}

/// <summary>The focus configuration of a trigger.</summary>
/// <param name="Delay">The delay in milliseconds before a focus opens the popup, or <see langword="null"/> for none.</param>
internal sealed record InteractionsFocusOptions(int? Delay = null);

/// <summary>The cursor-anchoring configuration of a trigger.</summary>
/// <param name="Axis">The tracked axis: <c>x</c>, <c>y</c> or <c>both</c>; <see langword="null"/> for <c>both</c>.</param>
internal sealed record InteractionsClientPointOptions(string? Axis = null);

/// <summary>The options of the floating element: its tree node and its hover close.</summary>
internal sealed record InteractionsFloatingOptions
{
    /// <summary>Gets a value indicating whether the popup takes part in the popup tree.</summary>
    /// <value><see langword="false"/> to leave it out; <see langword="null"/>, the default, takes part.</value>
    public bool? Node { get; init; }

    /// <summary>Gets the hover configuration of the floating element.</summary>
    /// <value>The configuration, or <see langword="null"/> when leaving the floating element does nothing.</value>
    public InteractionsFloatingHoverOptions? Hover { get; init; }
}

/// <summary>The hover configuration of the floating element.</summary>
/// <param name="CloseDelay">The delay in milliseconds before leaving closes the popup, or <see langword="null"/> for none.</param>
internal sealed record InteractionsFloatingHoverOptions(int? CloseDelay = null);

/// <summary>The options of a tooltip provider: its token and the group's delays.</summary>
internal sealed record InteractionsProviderOptions
{
    /// <summary>Gets the provider's token, which its tooltips' triggers carry.</summary>
    /// <value>The token the provider drew; it is never rendered.</value>
    public string Token { get; init; } = string.Empty;

    /// <summary>Gets the group's open delay.</summary>
    /// <value>The delay in milliseconds, or <see langword="null"/> for none.</value>
    public int? DelayOpen { get; init; }

    /// <summary>Gets the group's close delay.</summary>
    /// <value>The delay in milliseconds, or <see langword="null"/> for none.</value>
    public int? DelayClose { get; init; }

    /// <summary>Gets how long the group stays in its instant phase after its last tooltip closed.</summary>
    /// <value>The time in milliseconds; the default 0 is not sent.</value>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int Timeout { get; init; }
}

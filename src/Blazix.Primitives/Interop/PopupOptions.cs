namespace Blazix.Primitives.Interop;

// ADR 04 Amendments 2026-10-27 (the `popup` row, chosen by the user 2026-10-27); design point 5 of M4 track 5 (one role
// sub-record per handle, the InteractionsOptions precedent; settled by the orchestrator 2026-10-28, ADR 04 Amendments
// 2026-10-28, which records ContentVersion). Records compare by value, so an unchanged
// render sends nothing (ADR 16 rule 7). The module owns the parts' styles through the dom style writer, so a part leaves
// TwinOptions.Style unset.
/// <summary>
/// The options of the <c>popup</c> module, sent on <c>attach</c> and on every <c>update</c>. Exactly one role is set per
/// handle: a viewport, a positioner or a popup.
/// </summary>
internal sealed record PopupOptions : TwinOptions
{
    /// <summary>Gets the token of the popup session the handle belongs to.</summary>
    /// <value>The token the popup root drew; it is never rendered.</value>
    public string? SessionToken { get; init; }

    /// <summary>Gets the options of a viewport's handle.</summary>
    /// <value>The viewport options, or <see langword="null"/> for another role.</value>
    public PopupViewportOptions? Viewport { get; init; }

    /// <summary>Gets the options of a positioner's handle.</summary>
    /// <value>The positioner options, or <see langword="null"/> for another role.</value>
    public PopupPositionerOptions? Positioner { get; init; }

    /// <summary>Gets the options of a trigger's handle.</summary>
    /// <value>The trigger options, or <see langword="null"/> for another role.</value>
    public PopupTriggerOptions? Trigger { get; init; }

    /// <summary>Gets the options of a popup element's handle.</summary>
    /// <value>The popup options, or <see langword="null"/> for another role.</value>
    public PopupElementOptions? Popup { get; init; }
}

// PSH-ALG-13..15 (usePopupViewport, usePopupAutoResize). ContentVersion is the implementing pull request's addition to the
// settled record: Base UI re-measures on every payload change, not only on the content's remount.
/// <summary>The options of a viewport's handle: what the snapshot, the direction and the auto-resize read.</summary>
internal sealed record PopupViewportOptions
{
    /// <summary>Gets a value indicating whether the popup is open.</summary>
    /// <value>The effective open state; a closed popup resets the measurement.</value>
    public bool Open { get; init; }

    /// <summary>Gets the popup's rendered id, which the active trigger names.</summary>
    /// <value>The id, or <see langword="null"/> before the popup rendered.</value>
    public string? PopupId { get; init; }

    /// <summary>Gets the payload's version, whose change re-measures the popup.</summary>
    /// <value>The store's payload version.</value>
    public int ContentVersion { get; init; }
}

// PSH-ALG-17, PSH-ALG-21, POP-ANI-07.
/// <summary>The options of a positioner's handle: the cut-out, the anchored lock and the trigger-change watch.</summary>
internal sealed record PopupPositionerOptions
{
    /// <summary>Gets a value indicating whether a move between triggers is watched.</summary>
    /// <value><see langword="true"/> while the popup is open.</value>
    public bool WatchTriggerChange { get; init; }

    /// <summary>Gets a value indicating whether the internal backdrop is rendered, so the active trigger is cut out of it.</summary>
    /// <value><see langword="true"/> while the backdrop renders.</value>
    public bool Cutout { get; init; }

    /// <summary>Gets a value indicating whether the anchored scroll lock is enabled.</summary>
    /// <value><see langword="true"/> while open, modal and not opened by hover.</value>
    public bool ScrollLock { get; init; }

    /// <summary>Gets a value indicating whether a touch opened the popup.</summary>
    /// <value><see langword="true"/> locks only a popup that spans the viewport's width.</value>
    public bool TouchOpen { get; init; }

    /// <summary>Gets the id of the active trigger: the cut-out, the lock's reference and the watched move.</summary>
    /// <value>The id, or <see langword="null"/>.</value>
    public string? ActiveTriggerId { get; init; }
}

// PSH-ALG-18, POP-STR-14 (useTriggerFocusGuards.ts); design point 5 of M4 track 5, settled by the orchestrator 2026-10-28.
/// <summary>The options of a trigger's handle: whether its focus guards render, and the popup they lead into.</summary>
internal sealed record PopupTriggerOptions
{
    /// <summary>Gets a value indicating whether the trigger's focus guards render beside it.</summary>
    /// <value><see langword="true"/> while the trigger owns the open popup outside focus-manager-modal mode.</value>
    public bool Guards { get; init; }

    /// <summary>Gets the popup's id, whose parent is the positioner the guards' destinations skip.</summary>
    /// <value>The id, or <see langword="null"/>.</value>
    public string? PopupId { get; init; }
}

// POP-KEY-12, DLG-KEY-09.
/// <summary>The options of a popup element's handle.</summary>
internal sealed record PopupElementOptions
{
    /// <summary>Gets a value indicating whether the arrow keys, Home and End stop at the popup.</summary>
    /// <value><see langword="true"/> inside a toolbar (Popover) or always (Dialog).</value>
    public bool StopCompositeKeys { get; init; }
}

namespace Blazix.Primitives.Interop;

// ADR 04 Amendments 2026-10-27 (decision 3: every request a handle makes; the decision point press; chosen by the user
// 2026-10-27); the payload names are design point 6 of M4 track 3 (pending the user).
/// <summary>The payload of an open-change request the module made: a hover, focus, outside press, reference press, Escape or group close.</summary>
/// <param name="Open">The open state requested.</param>
/// <param name="Reason">The reason's literal, for example <c>trigger-hover</c> or <c>outside-press</c>.</param>
/// <param name="Event">The snapshot of the event that caused the request, or <see langword="null"/> when none did.</param>
/// <param name="TriggerId">The id of the trigger that opens the popup, or <see langword="null"/> for a close.</param>
/// <param name="Instant">
/// <see langword="true"/> when the request belongs to a tooltip group's instant phase, so the popup renders its instant
/// state in the same render as its open state; the default is <see langword="false"/>.
/// </param>
internal sealed record InteractionsRequest(bool Open, string Reason, EventSnapshot? Event, string? TriggerId, bool Instant = false);

/// <summary>The payload of a press reported after its dispatch: the open state it computed and how it happened.</summary>
/// <param name="Open">The open state the press requests.</param>
/// <param name="Reason">The reason's literal: <c>trigger-press</c> or <c>input-press</c>.</param>
/// <param name="TriggerId">The id of the pressed trigger.</param>
/// <param name="InteractionType"><c>mouse</c>, <c>touch</c>, <c>pen</c>, <c>keyboard</c>, or the empty string when unknown.</param>
/// <param name="Event">The snapshot of the click or mousedown.</param>
internal sealed record InteractionsPress(bool Open, string Reason, string? TriggerId, string InteractionType, EventSnapshot Event);

/// <summary>The press handed over inside the dispatch just before the press decision point asks about it.</summary>
/// <param name="Open">The open state the press requests.</param>
/// <param name="Reason">The reason's literal: <c>trigger-press</c> or <c>input-press</c>.</param>
/// <param name="TriggerId">The id of the pressed trigger.</param>
/// <param name="InteractionType"><c>mouse</c>, <c>touch</c>, <c>pen</c>, <c>keyboard</c>, or the empty string when unknown.</param>
internal sealed record InteractionsPressing(bool Open, string Reason, string? TriggerId, string InteractionType);

// FLT-EVT-01: the reasons the module requests with. Events/ has no parser (it is not this track's to edit), so the
// mapping of the module's literals lives here.
/// <summary>Conversions of the literals the <c>interactions</c> module reports.</summary>
internal static class InteractionsLiterals
{
    /// <summary>Parses the literal of a reason the module requests with.</summary>
    /// <param name="literal">The literal, for example <c>escape-key</c>.</param>
    /// <returns>The reason.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="literal"/> is not a reason the module reports.</exception>
    public static EventReason ParseReason(string literal)
    {
        return literal switch
        {
            "none" => EventReason.None,
            "trigger-press" => EventReason.TriggerPress,
            "trigger-hover" => EventReason.TriggerHover,
            "trigger-focus" => EventReason.TriggerFocus,
            "outside-press" => EventReason.OutsidePress,
            "input-press" => EventReason.InputPress,
            "focus-out" => EventReason.FocusOut,
            "escape-key" => EventReason.EscapeKey,
            _ => throw new ArgumentOutOfRangeException(nameof(literal), $"Blazix: `{literal}` is not a reason the `interactions` module reports."),
        };
    }
}

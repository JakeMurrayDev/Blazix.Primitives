using System.Text.Json.Serialization;

namespace Blazix.Primitives.Interop;

// FLT-FOC-11 (the focus-out close, reason focus-out, a request C# commits), FLT-FOC-10 (the close type C# classifies
// from the request's snapshot and the type the module recorded; ADR 04 Amendments 2026-10-27, "Focus targets"). A
// module-owned event reaches C# as a twin report carrying JavaScript's snapshot (ADR 04 Amendments 2026-10-18 item 1).
/// <summary>The payload of a focus-out close: the module asks to close a non-modal popup that focus left.</summary>
/// <param name="Event">The snapshot of the <c>focusout</c>.</param>
/// <param name="InteractionType">The interaction type the module last recorded: <c>mouse</c>, <c>touch</c>, <c>pen</c>, <c>keyboard</c> or the empty string.</param>
internal sealed record FocusOutCloseRequest(EventSnapshot Event, string InteractionType)
{
    /// <summary>Gets the reason of the close request.</summary>
    /// <value>Always <see cref="EventReason.FocusOut"/>.</value>
    [JsonIgnore]
    public EventReason Reason => EventReason.FocusOut;
}

// FLT-FOC-10's recording half (document pointerdown and keydown while open and enabled), reset to the empty string on
// each open (FLT-EDGE-08); once per change (ADR 16 rule 6).
/// <summary>The payload of a change of the interaction type the module records while the popup is open.</summary>
/// <param name="Type">The new literal: <c>mouse</c>, <c>touch</c>, <c>pen</c>, <c>keyboard</c> or the empty string.</param>
internal sealed record InteractionTypeChange(string Type);

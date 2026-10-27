namespace Blazix.Primitives;

// ADR 04 Amendments 2026-10-27 (decision 3: every request the interactions module makes, press included; the delay
// group's instant phase on the open request; chosen by the user 2026-10-27). The family's root routes it into its popup
// store's open-change sequence (ADR 05 §2): the consumer's callback first, a cancel honored, then the write and the relay.
/// <summary>An open-state change the <c>interactions</c> module requested, as a popup store receives it.</summary>
/// <param name="Open">The open state requested.</param>
/// <param name="Details">The details the store hands the consumer's callback: the reason, the event and the trigger.</param>
/// <param name="Instant">
/// <see langword="true"/> when the request belongs to a tooltip group's instant phase, so the popup renders its instant
/// state in the same render as its open state.
/// </param>
/// <param name="InteractionType">
/// For a press, how it happened: <c>mouse</c>, <c>touch</c>, <c>pen</c>, <c>keyboard</c> or the empty string; otherwise
/// <see langword="null"/>.
/// </param>
internal readonly record struct OpenChangeRequest(bool Open, OpenChangeEventDetails Details, bool Instant, string? InteractionType);

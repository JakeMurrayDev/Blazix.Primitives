namespace Blazix.Primitives.Interop;

// ADR 04 Amendments 2026-10-27 (the open-change relay; the record is track 1's, settled by the orchestrator 2026-10-27).
// Base UI's root store emits `openchange` on its event bus with { open, reason, nativeEvent, nested, triggerElement }
// (FLT-EVT-02, FloatingRootStore.ts:106-135); the port's popup store commits in C# (the consumer's callback first, ADR 05
// §2) and relays the committed change in `update`, and each module emits its own `openchange` when `Revision` moves. The
// revision counter lives in the store's non-reactive context; `nested` is the module's own (its session tree), and the open
// state travels in each module's own options (the shared names settled with the interactions track, ADR 04 Amendments
// 2026-10-27, issue 119). The `interactions` and `focus-manager` options carry it as a nullable member (tracks 3 and 4).
/// <summary>Describes the last open change a popup committed, as its browser-side behaviors receive it.</summary>
/// <param name="Revision">The commit's number, which moves by one on every committed open change of the popup.</param>
/// <param name="Reason">The change's reason as its kebab-case literal, such as <c>trigger-press</c>.</param>
/// <param name="EventKind">The interface of the event that caused the change; <see cref="EventKind.Event"/> for none.</param>
/// <param name="EventType">The type of the event that caused the change, such as <c>click</c>; <c>base-ui</c> for none.</param>
/// <param name="PointerType">The pointer type of the event, or <see langword="null"/> for an event that is not a pointer event.</param>
/// <param name="TriggerId">The id of the trigger the change is associated with, or <see langword="null"/> for none.</param>
internal sealed record OpenChangeRelay(
    int Revision,
    string Reason,
    EventKind EventKind,
    string EventType,
    string? PointerType,
    string? TriggerId)
{
    /// <summary>Creates the relay of a committed open change.</summary>
    /// <param name="revision">The commit's number.</param>
    /// <param name="reason">The change's reason.</param>
    /// <param name="snapshot">The snapshot of the event that caused the change, or the placeholder for none.</param>
    /// <param name="triggerId">The id of the trigger the change is associated with, or <see langword="null"/>.</param>
    /// <returns>The relay.</returns>
    public static OpenChangeRelay From(int revision, EventReason reason, EventSnapshot snapshot, string? triggerId)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return new(revision, reason.ToLiteral(), snapshot.Kind, snapshot.Type, snapshot.PointerType, triggerId);
    }
}

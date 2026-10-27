using System.Text.Json;
using Blazix.Primitives.Interop;

namespace Blazix.Primitives.Tests.Interop;

/// <summary>
/// L1: the relay of a committed open change (ADR 04 Amendments 2026-10-27, the open-change relay; the record is track 1's,
/// settled by the orchestrator 2026-10-27). The popup store builds it at commit and the <c>interactions</c> and
/// <c>focus-manager</c> options carry it, so its JSON is the contract those modules read to emit their internal
/// <c>openchange</c> (FLT-EVT-02's details: open, reason, native event, trigger element).
/// </summary>
public sealed class OpenChangeRelayTests
{
    [Fact]
    public void A_relay_is_built_from_the_committed_change_and_serializes_with_the_field_names_the_modules_read()
    {
        EventSnapshot click = new() { Kind = EventKind.PointerEvent, Type = "click", PointerType = "mouse" };

        OpenChangeRelay relay = OpenChangeRelay.From(3, open: true, EventReason.TriggerPress, click, "base-ui-5");

        Assert.Equal(
            """{"revision":3,"open":true,"reason":"trigger-press","eventKind":"PointerEvent","eventType":"click","pointerType":"mouse","triggerId":"base-ui-5"}""",
            JsonSerializer.Serialize(relay, BlazixJsonContext.Default.OpenChangeRelay));
    }

    [Fact]
    public void A_relay_of_an_imperative_close_carries_the_placeholder_event_and_omits_the_pointer_type_and_the_trigger()
    {
        OpenChangeRelay relay = OpenChangeRelay.From(4, open: false, EventReason.ImperativeAction, EventSnapshot.CreatePlaceholder(), triggerId: null);

        Assert.Equal(
            """{"revision":4,"open":false,"reason":"imperative-action","eventKind":"Event","eventType":"base-ui"}""",
            JsonSerializer.Serialize(relay, BlazixJsonContext.Default.OpenChangeRelay));
    }
}

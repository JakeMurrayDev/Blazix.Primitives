using System.Text.Json;

namespace Blazix.Primitives.Tests.Events;

/// <summary>
/// The snapshot record of ADR 04 §3 and the JSON shape Track 3's JavaScript writes (documented on the type and in
/// <c>Events/CONTEXT.md</c>). EVD-API-05's <c>instanceof</c> becomes <see cref="EventSnapshot.Kind"/> plus
/// <see cref="EventSnapshot.IsInstanceOf"/>.
/// </summary>
public sealed class EventSnapshotTests
{
    private const string PointerClickJson = """
        {"kind":"PointerEvent","type":"click","key":null,"code":null,"altKey":false,"ctrlKey":true,"metaKey":false,"shiftKey":true,"pointerType":"mouse","detail":1,"buttons":0,"target":{"id":"trigger","tagName":"BUTTON"},"defaultPrevented":false,"timeStamp":1234.5}
        """;

    [Fact(DisplayName = "reads the JSON JavaScript writes")]
    [Trait("Requirement", "EVD-API-05")]
    public void Reads_the_JSON_JavaScript_writes()
    {
        EventSnapshot? snapshot = JsonSerializer.Deserialize<EventSnapshot>(PointerClickJson);

        Assert.NotNull(snapshot);
        Assert.Equal(EventKind.PointerEvent, snapshot.Kind);
        Assert.Equal("click", snapshot.Type);
        Assert.True(snapshot.CtrlKey);
        Assert.True(snapshot.ShiftKey);
        Assert.False(snapshot.AltKey);
        Assert.Equal("mouse", snapshot.PointerType);
        Assert.Equal(1, snapshot.Detail);
        Assert.Equal(new EventTargetInfo { Id = "trigger", TagName = "BUTTON" }, snapshot.Target);
        Assert.Equal(1234.5, snapshot.TimeStamp);
    }

    [Fact(DisplayName = "writes the documented property names")]
    [Trait("Requirement", "EVD-API-05")]
    public void Writes_the_documented_property_names()
    {
        EventSnapshot snapshot = new() { Kind = EventKind.KeyboardEvent, Type = "keydown", Key = "Escape", Code = "Escape" };

        using JsonDocument json = JsonDocument.Parse(JsonSerializer.Serialize(snapshot));

        Assert.Equal(
            ["kind", "type", "key", "code", "altKey", "ctrlKey", "metaKey", "shiftKey", "pointerType", "detail", "buttons", "target", "defaultPrevented", "timeStamp"],
            json.RootElement.EnumerateObject().Select(property => property.Name).ToArray());
        Assert.Equal("KeyboardEvent", json.RootElement.GetProperty("kind").GetString());
    }

    [Fact(DisplayName = "the placeholder has type base-ui and kind Event")]
    [Trait("Requirement", "EVD-API-04")]
    public void The_placeholder_has_type_base_ui_and_kind_Event()
    {
        EventSnapshot placeholder = EventSnapshot.CreatePlaceholder();

        Assert.Equal(EventSnapshot.PlaceholderType, placeholder.Type);
        Assert.Equal("base-ui", placeholder.Type);
        Assert.Equal(EventKind.Event, placeholder.Kind);
        Assert.NotSame(placeholder, EventSnapshot.CreatePlaceholder());
    }

    [Theory(DisplayName = "instanceof follows the DOM's inheritance")]
    [Trait("Requirement", "EVD-API-05")]
    [InlineData(EventKind.PointerEvent, EventKind.MouseEvent, true)]
    [InlineData(EventKind.PointerEvent, EventKind.UIEvent, true)]
    [InlineData(EventKind.PointerEvent, EventKind.Event, true)]
    [InlineData(EventKind.WheelEvent, EventKind.MouseEvent, true)]
    [InlineData(EventKind.MouseEvent, EventKind.PointerEvent, false)]
    [InlineData(EventKind.KeyboardEvent, EventKind.UIEvent, true)]
    [InlineData(EventKind.KeyboardEvent, EventKind.MouseEvent, false)]
    [InlineData(EventKind.FocusEvent, EventKind.UIEvent, true)]
    [InlineData(EventKind.TouchEvent, EventKind.UIEvent, true)]
    [InlineData(EventKind.InputEvent, EventKind.UIEvent, true)]
    [InlineData(EventKind.ClipboardEvent, EventKind.UIEvent, false)]
    [InlineData(EventKind.ClipboardEvent, EventKind.Event, true)]
    [InlineData(EventKind.UIEvent, EventKind.UIEvent, true)]
    [InlineData(EventKind.Event, EventKind.UIEvent, false)]
    public void Instanceof_follows_the_DOM_inheritance(EventKind kind, EventKind tested, bool expected)
    {
        EventSnapshot snapshot = new() { Kind = kind, Type = "x" };

        Assert.Equal(expected, snapshot.IsInstanceOf(tested));
    }
}

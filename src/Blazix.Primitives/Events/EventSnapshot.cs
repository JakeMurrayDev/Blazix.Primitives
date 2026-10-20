using System.Text.Json.Serialization;

namespace Blazix.Primitives;

/// <summary>
/// <c>details.event</c> in C#: a snapshot of the native event that caused a request (ADR 04 §3, EVD-API-04/05). JavaScript
/// takes the snapshot inside the dispatch and serialises it with exactly these JSON property names:
/// <list type="table">
/// <item><term><c>kind</c></term><description><see cref="Kind"/>, the DOM interface name as a string (<c>"PointerEvent"</c>).</description></item>
/// <item><term><c>type</c></term><description><see cref="Type"/>, <c>event.type</c>.</description></item>
/// <item><term><c>key</c>, <c>code</c></term><description><see cref="Key"/>, <see cref="Code"/>; <c>null</c> on non-keyboard events.</description></item>
/// <item><term><c>altKey</c>, <c>ctrlKey</c>, <c>metaKey</c>, <c>shiftKey</c></term><description>the modifier flags; <c>false</c> where the event has none.</description></item>
/// <item><term><c>pointerType</c></term><description><see cref="PointerType"/>; <c>null</c> on non-pointer events.</description></item>
/// <item><term><c>detail</c></term><description><see cref="Detail"/>, <c>UIEvent.detail</c> (0 for a keyboard-generated click).</description></item>
/// <item><term><c>buttons</c></term><description><see cref="Buttons"/>, <c>MouseEvent.buttons</c>.</description></item>
/// <item><term><c>target</c></term><description><see cref="Target"/>, an object with <c>id</c> and <c>tagName</c>, or <c>null</c>.</description></item>
/// <item><term><c>defaultPrevented</c></term><description><see cref="DefaultPrevented"/>.</description></item>
/// <item><term><c>timeStamp</c></term><description><see cref="TimeStamp"/>, milliseconds as <c>event.timeStamp</c>.</description></item>
/// </list>
/// The placeholder for a request with no native event (EVD-API-04) is a snapshot with <see cref="Type"/> <c>"base-ui"</c>
/// and <see cref="Kind"/> <see cref="EventKind.Event"/>, made fresh per request by <see cref="CreatePlaceholder"/>.
/// </summary>
public sealed record EventSnapshot
{
    /// <summary>The <see cref="Type"/> of the placeholder snapshot (EVD-API-04).</summary>
    public const string PlaceholderType = "base-ui";

    /// <summary>The most derived DOM interface of the native event.</summary>
    [JsonPropertyName("kind")]
    public EventKind Kind { get; init; }

    /// <summary><c>event.type</c>, e.g. <c>click</c>; <c>base-ui</c> for the placeholder.</summary>
    [JsonPropertyName("type")]
    public string Type { get; init; } = "";

    /// <summary><c>KeyboardEvent.key</c>.</summary>
    [JsonPropertyName("key")]
    public string? Key { get; init; }

    /// <summary><c>KeyboardEvent.code</c>.</summary>
    [JsonPropertyName("code")]
    public string? Code { get; init; }

    /// <summary>The Alt modifier.</summary>
    [JsonPropertyName("altKey")]
    public bool AltKey { get; init; }

    /// <summary>The Control modifier.</summary>
    [JsonPropertyName("ctrlKey")]
    public bool CtrlKey { get; init; }

    /// <summary>The Meta modifier.</summary>
    [JsonPropertyName("metaKey")]
    public bool MetaKey { get; init; }

    /// <summary>The Shift modifier.</summary>
    [JsonPropertyName("shiftKey")]
    public bool ShiftKey { get; init; }

    /// <summary><c>PointerEvent.pointerType</c>: <c>mouse</c>, <c>touch</c> or <c>pen</c>.</summary>
    [JsonPropertyName("pointerType")]
    public string? PointerType { get; init; }

    /// <summary><c>UIEvent.detail</c>; a keyboard-generated click has 0 (EVD-EVT-27).</summary>
    [JsonPropertyName("detail")]
    public int Detail { get; init; }

    /// <summary><c>MouseEvent.buttons</c>.</summary>
    [JsonPropertyName("buttons")]
    public int Buttons { get; init; }

    /// <summary>The event target, or <c>null</c>.</summary>
    [JsonPropertyName("target")]
    public EventTargetInfo? Target { get; init; }

    /// <summary><c>event.defaultPrevented</c> when the snapshot was taken.</summary>
    [JsonPropertyName("defaultPrevented")]
    public bool DefaultPrevented { get; init; }

    /// <summary><c>event.timeStamp</c> in milliseconds.</summary>
    [JsonPropertyName("timeStamp")]
    public double TimeStamp { get; init; }

    /// <summary>A fresh placeholder snapshot, <see cref="Type"/> <c>base-ui</c> (EVD-API-04).</summary>
    public static EventSnapshot CreatePlaceholder()
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// <c>event instanceof kind</c> under the DOM's inheritance: every snapshot is an <see cref="EventKind.Event"/>;
    /// focus, mouse, touch, keyboard and input events are <see cref="EventKind.UIEvent"/>s; pointer and wheel events are
    /// <see cref="EventKind.MouseEvent"/>s.
    /// </summary>
    public bool IsInstanceOf(EventKind kind)
    {
        throw new NotImplementedException();
    }
}

/// <summary>The target of an <see cref="EventSnapshot"/>: JSON <c>{ "id": …, "tagName": … }</c>.</summary>
public sealed record EventTargetInfo
{
    /// <summary>The target element's <c>id</c>, or <c>null</c> when it has none.</summary>
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    /// <summary>The target element's <c>tagName</c> as the DOM reports it (upper case for HTML).</summary>
    [JsonPropertyName("tagName")]
    public string? TagName { get; init; }
}

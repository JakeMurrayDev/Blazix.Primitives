using System.Text.Json.Serialization;

namespace Blazix.Primitives;

// ADR 04 §3, EVD-API-04/05; EVD-API-04
/// <summary>
/// Represents a snapshot of the native event that caused a request, taken in JavaScript inside the dispatch.
/// </summary>
/// <remarks>
/// <para>JavaScript serializes the snapshot with exactly these JSON property names:</para>
/// <list type="table">
/// <item><term><c>kind</c></term><description><see cref="Kind"/>, the DOM interface name as a string (<c>"PointerEvent"</c>).</description></item>
/// <item><term><c>type</c></term><description><see cref="Type"/>, <c>event.type</c>.</description></item>
/// <item><term><c>key</c>, <c>code</c></term><description><see cref="Key"/> and <see cref="Code"/>; <see langword="null"/> on non-keyboard events.</description></item>
/// <item><term><c>altKey</c>, <c>ctrlKey</c>, <c>metaKey</c>, <c>shiftKey</c></term><description>The modifier flags; <see langword="false"/> where the event has none.</description></item>
/// <item><term><c>pointerType</c></term><description><see cref="PointerType"/>; <see langword="null"/> on non-pointer events.</description></item>
/// <item><term><c>detail</c></term><description><see cref="Detail"/>, <c>UIEvent.detail</c> (0 for a keyboard-generated click).</description></item>
/// <item><term><c>buttons</c></term><description><see cref="Buttons"/>, <c>MouseEvent.buttons</c>.</description></item>
/// <item><term><c>target</c></term><description><see cref="Target"/>, an object with <c>id</c> and <c>tagName</c>, or <see langword="null"/>.</description></item>
/// <item><term><c>defaultPrevented</c></term><description><see cref="DefaultPrevented"/>.</description></item>
/// <item><term><c>timeStamp</c></term><description><see cref="TimeStamp"/>, in milliseconds as <c>event.timeStamp</c>.</description></item>
/// </list>
/// <para>
/// A request with no native event carries a placeholder: a snapshot whose <see cref="Type"/> is <c>"base-ui"</c> and whose
/// <see cref="Kind"/> is <see cref="EventKind.Event"/>, made fresh per request by <see cref="CreatePlaceholder"/>.
/// </para>
/// </remarks>
public sealed record EventSnapshot
{
    // EVD-API-04
    /// <summary>
    /// The <see cref="Type"/> of the placeholder snapshot, <c>base-ui</c>.
    /// </summary>
    public const string PlaceholderType = "base-ui";

    /// <summary>
    /// Gets the most derived DOM interface of the native event.
    /// </summary>
    /// <value>The event's interface; <see cref="EventKind.Event"/> for the placeholder.</value>
    [JsonPropertyName("kind")]
    public EventKind Kind { get; init; }

    /// <summary>
    /// Gets the type of the native event, <c>event.type</c>.
    /// </summary>
    /// <value>The event type, such as <c>click</c>; <c>base-ui</c> for the placeholder. The default is the empty string.</value>
    [JsonPropertyName("type")]
    public string Type { get; init; } = "";

    /// <summary>
    /// Gets the key value of a keyboard event, <c>KeyboardEvent.key</c>.
    /// </summary>
    /// <value>The key value, or <see langword="null"/> on non-keyboard events.</value>
    [JsonPropertyName("key")]
    public string? Key { get; init; }

    /// <summary>
    /// Gets the physical key code of a keyboard event, <c>KeyboardEvent.code</c>.
    /// </summary>
    /// <value>The key code, or <see langword="null"/> on non-keyboard events.</value>
    [JsonPropertyName("code")]
    public string? Code { get; init; }

    /// <summary>
    /// Gets a value that indicates whether the Alt key was down.
    /// </summary>
    /// <value><see langword="true"/> if the Alt modifier was active; otherwise, <see langword="false"/>.</value>
    [JsonPropertyName("altKey")]
    public bool AltKey { get; init; }

    /// <summary>
    /// Gets a value that indicates whether the Control key was down.
    /// </summary>
    /// <value><see langword="true"/> if the Control modifier was active; otherwise, <see langword="false"/>.</value>
    [JsonPropertyName("ctrlKey")]
    public bool CtrlKey { get; init; }

    /// <summary>
    /// Gets a value that indicates whether the Meta key was down.
    /// </summary>
    /// <value><see langword="true"/> if the Meta modifier was active; otherwise, <see langword="false"/>.</value>
    [JsonPropertyName("metaKey")]
    public bool MetaKey { get; init; }

    /// <summary>
    /// Gets a value that indicates whether the Shift key was down.
    /// </summary>
    /// <value><see langword="true"/> if the Shift modifier was active; otherwise, <see langword="false"/>.</value>
    [JsonPropertyName("shiftKey")]
    public bool ShiftKey { get; init; }

    /// <summary>
    /// Gets the kind of pointer, <c>PointerEvent.pointerType</c>.
    /// </summary>
    /// <value><c>mouse</c>, <c>touch</c> or <c>pen</c>, or <see langword="null"/> on non-pointer events.</value>
    [JsonPropertyName("pointerType")]
    public string? PointerType { get; init; }

    // EVD-EVT-27
    /// <summary>
    /// Gets the event detail, <c>UIEvent.detail</c>.
    /// </summary>
    /// <value>The click count for a click; 0 for a keyboard-generated click.</value>
    [JsonPropertyName("detail")]
    public int Detail { get; init; }

    /// <summary>
    /// Gets the mouse buttons that were down, <c>MouseEvent.buttons</c>.
    /// </summary>
    /// <value>A bit mask of the pressed buttons.</value>
    [JsonPropertyName("buttons")]
    public int Buttons { get; init; }

    /// <summary>
    /// Gets the event target.
    /// </summary>
    /// <value>The target's id and tag name, or <see langword="null"/> when the event had no element target.</value>
    [JsonPropertyName("target")]
    public EventTargetInfo? Target { get; init; }

    /// <summary>
    /// Gets a value that indicates whether the native event's default action had been prevented when the snapshot was taken.
    /// </summary>
    /// <value><see langword="true"/> if <c>event.defaultPrevented</c> was set; otherwise, <see langword="false"/>.</value>
    [JsonPropertyName("defaultPrevented")]
    public bool DefaultPrevented { get; init; }

    /// <summary>
    /// Gets the time at which the native event was created, <c>event.timeStamp</c>.
    /// </summary>
    /// <value>The time stamp in milliseconds.</value>
    [JsonPropertyName("timeStamp")]
    public double TimeStamp { get; init; }

    // EVD-API-04
    /// <summary>
    /// Creates a placeholder snapshot for a request that has no native event.
    /// </summary>
    /// <returns>A new snapshot whose <see cref="Type"/> is <c>base-ui</c> and whose <see cref="Kind"/> is <see cref="EventKind.Event"/>.</returns>
    public static EventSnapshot CreatePlaceholder() => new() { Kind = EventKind.Event, Type = PlaceholderType };

    /// <summary>
    /// Determines whether the native event is an instance of a DOM interface, following the DOM's inheritance.
    /// </summary>
    /// <param name="kind">The interface to test.</param>
    /// <returns><see langword="true"/> if the event is an instance of <paramref name="kind"/>; otherwise, <see langword="false"/>.</returns>
    /// <remarks>
    /// Every snapshot is an <see cref="EventKind.Event"/>; focus, mouse, pointer, wheel, touch, keyboard and input events are
    /// <see cref="EventKind.UIEvent"/>s; pointer and wheel events are <see cref="EventKind.MouseEvent"/>s.
    /// </remarks>
    public bool IsInstanceOf(EventKind kind)
    {
        return kind switch
        {
            EventKind.Event => true,
            EventKind.UIEvent => Kind is EventKind.UIEvent or EventKind.FocusEvent or EventKind.MouseEvent or EventKind.PointerEvent
                or EventKind.WheelEvent or EventKind.TouchEvent or EventKind.KeyboardEvent or EventKind.InputEvent,
            EventKind.MouseEvent => Kind is EventKind.MouseEvent or EventKind.PointerEvent or EventKind.WheelEvent,
            _ => Kind == kind,
        };
    }
}

/// <summary>
/// Represents the target of an <see cref="EventSnapshot"/>, serialized as <c>{ "id": …, "tagName": … }</c>.
/// </summary>
public sealed record EventTargetInfo
{
    /// <summary>
    /// Gets the target element's <c>id</c>.
    /// </summary>
    /// <value>The id, or <see langword="null"/> when the element has none.</value>
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    /// <summary>
    /// Gets the target element's tag name as the DOM reports it.
    /// </summary>
    /// <value>The tag name, upper case for HTML elements, or <see langword="null"/>.</value>
    [JsonPropertyName("tagName")]
    public string? TagName { get; init; }
}

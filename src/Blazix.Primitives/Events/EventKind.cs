using System.Text.Json.Serialization;

namespace Blazix.Primitives;

// ADR 04 §3; EVD §3.5, EVD-API-05
/// <summary>
/// Specifies the DOM interface of the native event an <see cref="EventSnapshot"/> was taken from.
/// </summary>
/// <remarks>
/// The value is the most derived of the interfaces listed. It stands in for JavaScript's <c>instanceof</c> test on the
/// event; <see cref="EventSnapshot.IsInstanceOf"/> answers <c>instanceof</c> with the DOM's inheritance (a
/// <c>PointerEvent</c> is a <c>MouseEvent</c>). The value is serialized as the interface name, for example <c>"PointerEvent"</c>.
/// </remarks>
[JsonConverter(typeof(JsonStringEnumConverter<EventKind>))]
public enum EventKind
{
    /// <summary>The event has no more specific interface; this includes the <c>base-ui</c> placeholder (<c>Event</c>).</summary>
    Event,

    /// <summary>The event is a user-interface event, such as a window resize (<c>UIEvent</c>).</summary>
    UIEvent,

    /// <summary>The event is a focus event (<c>FocusEvent</c>).</summary>
    FocusEvent,

    /// <summary>The event is a mouse event (<c>MouseEvent</c>).</summary>
    MouseEvent,

    /// <summary>The event is a pointer event, which is also a mouse event (<c>PointerEvent</c>).</summary>
    PointerEvent,

    /// <summary>The event is a wheel event, which is also a mouse event (<c>WheelEvent</c>).</summary>
    WheelEvent,

    /// <summary>The event is a touch event (<c>TouchEvent</c>).</summary>
    TouchEvent,

    /// <summary>The event is a keyboard event (<c>KeyboardEvent</c>).</summary>
    KeyboardEvent,

    /// <summary>The event is an input event (<c>InputEvent</c>).</summary>
    InputEvent,

    /// <summary>The event is a clipboard event (<c>ClipboardEvent</c>).</summary>
    ClipboardEvent,
}

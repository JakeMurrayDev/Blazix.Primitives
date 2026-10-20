using System.Text.Json.Serialization;

namespace Blazix.Primitives;

/// <summary>
/// The DOM interface of the native event an <see cref="EventSnapshot"/> was taken from: the most derived of the interfaces
/// EVD §3.5 names. It replaces EVD-API-05's <c>instanceof</c> test (ADR 04 §3); <see cref="EventSnapshot.IsInstanceOf"/>
/// answers <c>instanceof</c> with the DOM's inheritance (a <c>PointerEvent</c> is a <c>MouseEvent</c>). Serialised as the
/// interface name, e.g. <c>"PointerEvent"</c>.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<EventKind>))]
public enum EventKind
{
    /// <summary><c>Event</c>: any event with no more specific interface, and the <c>base-ui</c> placeholder.</summary>
    Event,

    /// <summary><c>UIEvent</c> (window resize).</summary>
    UIEvent,

    /// <summary><c>FocusEvent</c>.</summary>
    FocusEvent,

    /// <summary><c>MouseEvent</c>.</summary>
    MouseEvent,

    /// <summary><c>PointerEvent</c>, a <c>MouseEvent</c>.</summary>
    PointerEvent,

    /// <summary><c>WheelEvent</c>, a <c>MouseEvent</c>.</summary>
    WheelEvent,

    /// <summary><c>TouchEvent</c>.</summary>
    TouchEvent,

    /// <summary><c>KeyboardEvent</c>.</summary>
    KeyboardEvent,

    /// <summary><c>InputEvent</c>.</summary>
    InputEvent,

    /// <summary><c>ClipboardEvent</c>.</summary>
    ClipboardEvent,
}

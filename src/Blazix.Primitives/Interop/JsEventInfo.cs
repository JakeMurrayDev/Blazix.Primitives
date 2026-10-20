namespace Blazix.Primitives.Interop;

/// <summary>
/// The snapshot of a browser event as JavaScript serialises it for a decision point or a callback (ADR 04 §3).
/// The JSON property names are the camel-cased member names, mirrored by <c>js/event-info.ts</c>; this record is
/// the one place that defines them. Track 2's event snapshot record (EVD, <c>Events/</c>) is built from it.
/// </summary>
internal sealed record JsEventInfo
{
    /// <summary>The event class: <c>keyboard</c>, <c>pointer</c>, <c>mouse</c>, <c>touch</c>, <c>focus</c>, <c>generic</c>, or <c>base-ui</c> for the placeholder (EVD-API-05).</summary>
    public string Kind { get; init; } = "base-ui";

    /// <summary>The DOM event type, or <c>base-ui</c> for the placeholder.</summary>
    public string Type { get; init; } = "base-ui";

    public string? Key { get; init; }

    public string? Code { get; init; }

    public bool AltKey { get; init; }

    public bool CtrlKey { get; init; }

    public bool MetaKey { get; init; }

    public bool ShiftKey { get; init; }

    /// <summary><c>mouse</c>, <c>pen</c> or <c>touch</c> for pointer events; otherwise <see langword="null"/>.</summary>
    public string? PointerType { get; init; }

    public int Detail { get; init; }

    public int Buttons { get; init; }

    /// <summary>The mouse button of a mouse or pointer event; <c>-1</c> otherwise.</summary>
    public int Button { get; init; } = -1;

    public JsEventTarget? Target { get; init; }

    public bool DefaultPrevented { get; init; }

    public double TimeStamp { get; init; }
}

/// <summary>What the module reports about the event target: enough to identify a part's element.</summary>
internal sealed record JsEventTarget(string TagName, string? Id);

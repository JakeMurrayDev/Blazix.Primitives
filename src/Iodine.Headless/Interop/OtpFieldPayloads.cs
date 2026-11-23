namespace Blazix.Primitives.Interop;

// ADR 04 §3 (one message per edit), Amendments 2026-11-18 and 2026-11-19 (design points 1–7, chosen by the user 2026-11-19);
// issue 278. The module reports each edit once: typed or composed text, a paste, Backspace, Delete or Ctrl+Backspace.
/// <summary>The payload of an edit report: what the user did to a slot, which C# turns into a request.</summary>
internal sealed record OtpFieldEditReport
{
    /// <summary>Gets the slot's index.</summary>
    public int Index { get; init; }

    /// <summary>Gets the kind of edit: <c>input</c>, <c>paste</c>, <c>backspace</c>, <c>delete</c> or <c>clear</c>.</summary>
    public string Kind { get; init; } = string.Empty;

    /// <summary>Gets the slot's text for an input, the clipboard's text for a paste, or an empty string for a key.</summary>
    public string Raw { get; init; } = string.Empty;

    // Variant (ii): the module wrote the character and moved focus before C# answered.
    /// <summary>Gets a value indicating whether the module already wrote the character and advanced focus.</summary>
    public bool Advanced { get; init; }

    /// <summary>Gets the report's revision, which C# echoes once it applied the edit.</summary>
    public int Revision { get; init; }

    /// <summary>Gets the event that made the edit, as JavaScript saw it.</summary>
    public EventSnapshot Event { get; init; } = EventSnapshot.CreatePlaceholder();
}

// OTP-FOC-03, OTP-FOC-04, OTP-FOC-07: a focus no listener or decision vetoed.
/// <summary>The payload of a focus report.</summary>
internal sealed record OtpFieldFocusReport
{
    /// <summary>Gets the focused slot's index.</summary>
    public int Index { get; init; }

    /// <summary>Gets the focus event, as JavaScript saw it.</summary>
    public EventSnapshot Event { get; init; } = EventSnapshot.CreatePlaceholder();
}

// OTP-FOC-07, OTP-FRM-09, OTP-FRM-10: a blur no listener or decision vetoed, with the boundary of the root.
/// <summary>The payload of a blur report.</summary>
internal sealed record OtpFieldBlurReport
{
    /// <summary>Gets the slot's index.</summary>
    public int Index { get; init; }

    /// <summary>Gets a value indicating whether focus left the root (the related target is outside it or absent).</summary>
    public bool Outside { get; init; }

    /// <summary>Gets the blur event, as JavaScript saw it.</summary>
    public EventSnapshot Event { get; init; } = EventSnapshot.CreatePlaceholder();
}

// OTP-KEY-20, OTP-EDGE-05: reading a paste's clipboard text threw.
/// <summary>The payload of an unreadable-paste report.</summary>
internal sealed record OtpFieldPasteUnreadableReport
{
    /// <summary>Gets the slot's index.</summary>
    public int Index { get; init; }
}

namespace Blazix.Primitives.Interop;

// ADR 04 Amendments 2026-11-03 (4) and (6) (chosen by the user 2026-11-03): the `combobox` component module under the
// placement rule. One handle per part element (input, trigger, clear, input group, chips, chip remove, list, popup); values
// never cross, only ids, flags, the committed text and its revision. Handlers read the options of the latest update. The
// names are track 10's, published in its interface commit and refined at L3 by a dated line in the PR.
/// <summary>
/// The options of the <c>combobox</c> module, sent on <c>attach</c> and on every <c>update</c>: one combobox-family part's
/// keyboard, pointer and text handling that the composite, interactions and focus-manager modules do not own.
/// </summary>
internal sealed record ComboboxOptions : TwinOptions
{
    /// <summary>Gets the part the handle serves.</summary>
    /// <value>One of the part literals of <see cref="ComboboxTwin"/>; the default is <see cref="ComboboxTwin.InputPart"/>.</value>
    public string Part { get; init; } = ComboboxTwin.InputPart;

    /// <summary>Gets the interactions session's token, which joins the handles of one root.</summary>
    public string? SessionToken { get; init; }

    /// <summary>Gets a value indicating whether the input renders inside the popup.</summary>
    public bool InsidePopup { get; init; }

    /// <summary>Gets a value indicating whether the list renders without a popup.</summary>
    public bool Inline { get; init; }

    /// <summary>Gets a value indicating whether the combobox is disabled.</summary>
    public bool Disabled { get; init; }

    /// <summary>Gets a value indicating whether the value cannot be changed.</summary>
    public bool ReadOnly { get; init; }

    /// <summary>Gets a value indicating whether the root keeps several values.</summary>
    public bool Multiple { get; init; }

    /// <summary>Gets a value indicating whether there is something to clear: a value or text; the closed Escape's fallback reads it.</summary>
    public bool HasValue { get; init; }

    /// <summary>Gets a value indicating whether the list is a grid.</summary>
    public bool Grid { get; init; }

    /// <summary>Gets a value indicating whether the list renders a window of its items, which owns its scroll.</summary>
    public bool Virtualized { get; init; }

    /// <summary>Gets the id of the chips container, or <see langword="null"/> while none renders.</summary>
    public string? ChipsId { get; init; }

    /// <summary>Gets the id of the list, or <see langword="null"/> while none renders.</summary>
    public string? ListId { get; init; }

    /// <summary>Gets the id of the popup, or <see langword="null"/> while none renders.</summary>
    public string? PopupId { get; init; }

    /// <summary>Gets the committed input text the module writes when it differs from the live text since its last write.</summary>
    /// <value>The text, or <see langword="null"/> on handles other than the input.</value>
    public string? Value { get; init; }

    /// <summary>Gets the revision of <see cref="Value"/>, raised by every committed write.</summary>
    public int ValueRevision { get; init; }

    /// <summary>Gets a value indicating whether the consumer owns the input text, so the module re-asserts it.</summary>
    public bool Controlled { get; init; }

    /// <summary>Gets a value indicating whether the module asks the <c>clear</c> and <c>remove</c> points inside the dispatch.</summary>
    public bool Decide { get; init; }
}

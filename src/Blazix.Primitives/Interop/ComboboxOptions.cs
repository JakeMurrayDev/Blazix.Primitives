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

    /// <summary>Gets a value indicating whether a value is selected, Base UI's <c>isClear</c> condition; the closed Escape's fallback and Backspace into the chips read it.</summary>
    public bool HasValue { get; init; }

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

    /// <summary>Gets a value indicating whether the popup is open, for Enter's activation.</summary>
    public bool Open { get; init; }

    /// <summary>Gets a value indicating whether the popup is mounted; the Escape of a closed, unmounted popup clears.</summary>
    public bool Mounted { get; init; }

    /// <summary>Gets a value indicating whether the reading direction is right to left, for Home and End.</summary>
    public bool Rtl { get; init; }

    /// <summary>Gets the id of the input, which a press elsewhere keeps the focus on.</summary>
    public string? InputId { get; init; }

    /// <summary>Gets a value indicating whether the popup was opened by touch, so the focus is not redirected to the input.</summary>
    public bool TouchOpened { get; init; }

    /// <summary>Gets the ids of the chips in order, which a chips container's handle keeps for the chip keys and the input.</summary>
    /// <value>The ids, or <see langword="null"/> for a part other than the chips container.</value>
    public IReadOnlyList<string>? ChipIds { get; init; }

    /// <summary>Gets the index of a chip remove button's chip, which its handle reports with a removal.</summary>
    /// <value>The index, or <see langword="null"/> for a part other than a chip remove button.</value>
    public int? Index { get; init; }

    /// <summary>Gets the id of the internal dismiss button beside the handle's element, whose press closes the popup.</summary>
    /// <value>The id, or <see langword="null"/> while none renders.</value>
    public string? DismissId { get; init; }

    /// <summary>Gets the revision of the form submission an item press asked for; the input's handle submits its form when it moves.</summary>
    /// <value>The revision, or <see langword="null"/> before the first request and on handles other than the input.</value>
    public int? SubmitRevision { get; init; }
}

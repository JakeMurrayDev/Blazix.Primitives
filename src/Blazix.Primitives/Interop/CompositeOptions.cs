using System.Text.Json.Serialization;

namespace Blazix.Primitives.Interop;

// ADR 04 §1 (composite row) and §3; CMP §6.1 (the root part's options and defaults) and §6.2; the RadioGroup options of
// ADR 04 Amendments 2026-10-20 (the pending-navigation flag and the focus-driven selection, RDG-STA-06, RAD-FOC-05) and
// the reports of RDG-FOC-04/05 and CMP-EVT-01; the items' ids and consumer tabindex of ADR 04 Amendments 2026-10-22
// (RAD-DOM-16); the M3 extensions of ADR 04 Amendments 2026-10-26 (chosen by the user): the items' flags and the
// derived indices (CMP-STA-08, CMP-KEY-08, TLB-STA-02), the items report, onMapChange as ids (CMP-EVT-03), the
// registration-only mode of a root-less list (CMP-STR-03), and TabsList's highlight that follows the item marked active
// (CMP-STA-22, CMP-EDGE-09), sent only when set. Not carried (CMP §10 Q1, M5): grid, onLoop, a highlighted index from
// C#, metadata or an explicit index from C#: values stay in C#, joined to the reported ids.
/// <summary>
/// The options of the <c>composite</c> module: the roving group's configuration and its items, sent on <c>attach</c> and
/// on every <c>update</c>. The module resolves the items, keeps their order, writes their <c>tabindex</c> and handles the
/// navigation keys on the root.
/// </summary>
/// <remarks>The module owns no style property, so a part leaves <see cref="TwinOptions.Style"/> unset.</remarks>
internal sealed record CompositeOptions : TwinOptions
{
    /// <summary>Gets the arrow keys that move the highlight.</summary>
    /// <value><c>horizontal</c>, <c>vertical</c> or <c>both</c>, the default.</value>
    public string Orientation { get; init; } = "both";

    /// <summary>Gets a value indicating whether a step past either end wraps to the other end.</summary>
    /// <value><see langword="true"/>, the default, to wrap; <see langword="false"/> to stay at the end.</value>
    public bool LoopFocus { get; init; } = true;

    /// <summary>Gets a value indicating whether Home and End move the highlight to the first and last item.</summary>
    /// <value><see langword="true"/> to handle Home and End; the default is <see langword="false"/>.</value>
    public bool EnableHomeAndEndKeys { get; init; }

    /// <summary>Gets a value indicating whether the propagation of a key the module handles is stopped.</summary>
    /// <value><see langword="true"/>, the default, to stop it.</value>
    public bool StopEventPropagation { get; init; } = true;

    /// <summary>Gets a value indicating whether moving the pointer over an item focuses it.</summary>
    /// <value><see langword="true"/> to focus a hovered item; the default is <see langword="false"/>.</value>
    public bool HighlightItemOnHover { get; init; }

    /// <summary>Gets the modifier keys that do not block navigation.</summary>
    /// <value>A subset of <c>Shift</c>, <c>Control</c>, <c>Alt</c> and <c>Meta</c>; empty by default, so every modifier blocks it.</value>
    public IReadOnlyList<string> ModifierKeys { get; init; } = [];

    /// <summary>Gets the indices of the items navigation skips.</summary>
    /// <value>
    /// The indices, which also make every other item navigable even when it is <c>aria-disabled</c>; or
    /// <see langword="null"/>, the default, to read each item's disabled state from the document.
    /// </value>
    public IReadOnlyList<int>? DisabledIndices { get; init; }

    /// <summary>Gets a value indicating whether the module derives the indices navigation skips from the items' flags.</summary>
    /// <value>
    /// <see langword="true"/> to skip the items that are <see cref="CompositeItemRegistration.Disabled"/> and not
    /// <see cref="CompositeItemRegistration.FocusableWhenDisabled"/>, in place of <see cref="DisabledIndices"/>; the
    /// default is <see langword="false"/>, which is not sent.
    /// </value>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool DeriveDisabledIndices { get; init; }

    /// <summary>Gets the reading direction, which swaps the horizontal arrow keys.</summary>
    /// <value><c>ltr</c>, the default, or <c>rtl</c>.</value>
    public string Direction { get; init; } = "ltr";

    /// <summary>Gets the items of the group.</summary>
    /// <value>One registration per item, by element id; empty by default.</value>
    public IReadOnlyList<CompositeItemRegistration> Items { get; init; } = [];

    /// <summary>Gets a value indicating whether an item that an arrow key focuses is selected by clicking the input that follows it.</summary>
    /// <value>
    /// <see langword="true"/> to select on keyboard navigation, as a radio group does; the default is
    /// <see langword="false"/>.
    /// </value>
    public bool SelectOnNavigation { get; init; }

    /// <summary>Gets a value indicating whether the module reports focus leaving the root.</summary>
    /// <value><see langword="true"/> to receive <see cref="CompositeTwin.FocusLeft"/>; the default is <see langword="false"/>.</value>
    public bool ReportFocusLeave { get; init; }

    /// <summary>Gets a value indicating whether the module reports each change of the highlighted index.</summary>
    /// <value><see langword="true"/> to receive <see cref="CompositeTwin.HighlightChanged"/>; the default is <see langword="false"/>.</value>
    public bool ReportHighlight { get; init; }

    // Option (i) of ADR 04's note of 2026-10-22, chosen by the user 2026-10-23.
    /// <summary>Gets a value indicating whether the module reports a key it handled and stopped without running the C# chain inside the dispatch.</summary>
    /// <value>
    /// <see langword="true"/> to receive <see cref="CompositeTwin.KeyHandled"/>, so the consumer's key handlers run for a key
    /// that never reaches Blazor's dispatcher; the default is <see langword="false"/>.
    /// </value>
    public bool ReportHandledKeys { get; init; }

    /// <summary>Gets a value indicating whether the module reports the items' ids in index order on each publication that changed them.</summary>
    /// <value>
    /// <see langword="true"/> to receive <see cref="CompositeTwin.ItemsChanged"/>; the default is <see langword="false"/>,
    /// which is not sent.
    /// </value>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool ReportItems { get; init; }

    /// <summary>Gets a value indicating whether the root is a list that only orders and reports its items.</summary>
    /// <value>
    /// <see langword="true"/> for a list without a roving group, read once on <c>attach</c>: the module keeps the items'
    /// order and reports it to <see cref="CompositeTwin.ItemsChanged"/>, handles no key, writes no <c>tabindex</c> and
    /// keeps no highlight, so every other option is ignored. The default is <see langword="false"/>, which is not sent.
    /// </value>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool RegistrationOnly { get; init; }

    /// <summary>Gets a value indicating whether the highlight follows the item marked active.</summary>
    /// <value>
    /// <see langword="true"/> to move the highlight to the item that carries <c>data-composite-item-active</c> whenever it
    /// changes while focus is outside the root and the item is not <c>aria-disabled="true"</c>, except once after a keydown
    /// on that item; the highlight is then not moved off an item that <see cref="DisabledIndices"/> disables. The default
    /// is <see langword="false"/>, which is not sent.
    /// </value>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool FollowActiveItem { get; init; }

    // ADR 04 Amendments 2026-11-03 (1), chosen by the user: a list handle joins the popup's interactions session by token
    // and carries one role record (CMP-STR-06); the roving group's fields above are ignored on it.
    /// <summary>Gets the token of the popup session a list handle joins.</summary>
    /// <value>The token the popup root drew and cascades; it is never rendered. <see langword="null"/>, the default, for a roving group.</value>
    public string? SessionToken { get; init; }

    /// <summary>Gets the list navigation options of a list handle.</summary>
    /// <value>The options, the same on every handle of one list; or <see langword="null"/>, the default, for a roving group.</value>
    public CompositeListOptions? List { get; init; }

    /// <summary>Gets the options of a list's reference handle.</summary>
    /// <value>The reference role, or <see langword="null"/> for another role.</value>
    public CompositeReferenceOptions? Reference { get; init; }

    /// <summary>Gets the options of a list's trigger handle.</summary>
    /// <value>The trigger role, or <see langword="null"/> for another role.</value>
    public CompositeTriggerOptions? Trigger { get; init; }

    /// <summary>Gets the options of a list's floating handle.</summary>
    /// <value>The floating role, whose items are <see cref="Items"/>, or <see langword="null"/> for another role.</value>
    public CompositeFloatingOptions? Floating { get; init; }
}

// ADR 04 Amendments 2026-10-22 (the items by element id; a consumer tabindex wins, RAD-DOM-16); the flags of ADR 04
// Amendments 2026-10-26 (chosen by the user), Base UI's Toolbar item metadata (ToolbarRoot.tsx:79-82).
/// <summary>One item of a composite group as the module receives it.</summary>
/// <param name="Id">The id of the item's element, which the module resolves from the root's document or shadow root.</param>
/// <param name="TabIndex">
/// The consumer's <c>tabindex</c> on the item, which the module never writes over; <see langword="null"/> when the module
/// writes the roving value.
/// </param>
/// <param name="Disabled">Whether the item is disabled; <see langword="false"/>, the default, is not sent.</param>
/// <param name="FocusableWhenDisabled">
/// Whether the item stays navigable and can hold the tab stop while disabled; <see langword="false"/>, the default, is not
/// sent.
/// </param>
internal sealed record CompositeItemRegistration(
    string Id,
    string? TabIndex,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] bool Disabled = false,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] bool FocusableWhenDisabled = false)
{
    // CMP-STA-06 (ADR 04 Amendments 2026-11-03 (1), chosen by the user): the label is resolved in JavaScript, explicit first.
    /// <summary>Gets the item's explicit label, which typeahead matches.</summary>
    /// <value>
    /// The label; the empty string for an item that has no label and must not fall back to its text, which never matches;
    /// or <see langword="null"/>, the default, to read the text of <see cref="TextId"/> or of the item's element.
    /// </value>
    public string? Label { get; init; }

    /// <summary>Gets the id of the element whose text is the item's label when it has no explicit label.</summary>
    /// <value>The id, resolved as the item's is; or <see langword="null"/>, the default, to read the item's element.</value>
    public string? TextId { get; init; }

    // CMP-STA-04, CMP-STA-17: an explicit index from C#, such as a windowed list's.
    /// <summary>Gets the item's explicit index.</summary>
    /// <value>
    /// The index the item occupies, which a negative value takes out of the list; or <see langword="null"/>, the default,
    /// for an automatic index in document order.
    /// </value>
    public int? Index { get; init; }

    // NAV-DOM-21 and NAV-FOC-03 (sheet F Q15 (a)): a navigation menu link takes no tabindex.
    /// <summary>Gets a value indicating whether the item renders no <c>tabindex</c> and the module writes none.</summary>
    /// <value>
    /// <see langword="true"/> for an item that keeps its native focusability and stays navigable by the arrow keys; the
    /// default is <see langword="false"/>, which is not sent.
    /// </value>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool OmitTabIndex { get; init; }
}

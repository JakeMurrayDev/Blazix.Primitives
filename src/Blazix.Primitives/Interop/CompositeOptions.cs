using System.Text.Json.Serialization;

namespace Blazix.Primitives.Interop;

// ADR 04 §1 (composite row) and §3; CMP §6.1 (the root part's options and defaults) and §6.2; the RadioGroup options of
// ADR 04 Amendments 2026-10-20 (the pending-navigation flag and the focus-driven selection, RDG-STA-06, RAD-FOC-05) and
// the reports of RDG-FOC-04/05 and CMP-EVT-01; the items' ids and consumer tabindex of ADR 04 Amendments 2026-10-22
// (RAD-DOM-16); the M3 extensions of ADR 04 Amendments 2026-10-26 (chosen by the user): the items' flags and the
// derived indices (CMP-STA-08, CMP-KEY-08, TLB-STA-02) and the items report, onMapChange as ids (CMP-EVT-03), sent only
// when set. Not carried (CMP §10 Q1, M3, M5): grid, onLoop, a consumer-owned highlight, metadata or an explicit index
// from C#: values stay in C#, joined to the reported ids.
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
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] bool FocusableWhenDisabled = false);

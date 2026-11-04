using System.Text.Json.Serialization;

namespace Blazix.Primitives.Interop;

// ADR 04 §1's `select` row (M5 track 9): one options record per handle, its Part naming the role (the TabsOptions shape).
// Elements cross as indices into the references each attach and update carries (ADR 04 Amendments 2026-10-27); the
// options are compared by value, so an unchanged render sends nothing (ADR 16 rule 7).
/// <summary>The options of the <c>select</c> module, sent on <c>attach</c> and on every <c>update</c>.</summary>
internal sealed record SelectOptions : TwinOptions
{
    /// <summary>Gets the part the handle serves.</summary>
    /// <value><see cref="SelectTwin.PositionerPart"/>, the default, <see cref="SelectTwin.ListPart"/>, <see cref="SelectTwin.ScrollArrowPart"/> or <see cref="SelectTwin.TriggerPart"/>.</value>
    public string Part { get; init; } = SelectTwin.PositionerPart;

    /// <summary>Gets a value indicating whether the popup is open.</summary>
    public bool Open { get; init; }

    /// <summary>Gets a value indicating whether the popup is mounted.</summary>
    public bool Mounted { get; init; }

    /// <summary>Gets a value indicating whether the popup's exit transition is running.</summary>
    /// <value><see langword="true"/> while the transition status is ending; the default <see langword="false"/> is not sent.</value>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool Ending { get; init; }

    /// <summary>Gets a value indicating whether the positioner asks to place the popup over the trigger.</summary>
    /// <value>The positioner's latched request; the default <see langword="false"/> is not sent.</value>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool AlignItemWithTrigger { get; init; }

    /// <summary>Gets a value indicating whether a touch opened the popup, which is then never placed over the trigger.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool TouchOpened { get; init; }

    /// <summary>Gets a value indicating whether pointer movement over items highlights them.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool HighlightItemOnHover { get; init; }

    /// <summary>Gets a value indicating whether the reading direction is right to left.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool Rtl { get; init; }

    /// <summary>Gets a value indicating whether a value is selected.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool HasSelectedValue { get; init; }

    /// <summary>Gets the id of the selected item's text element, or <see langword="null"/>.</summary>
    public string? SelectedTextId { get; init; }

    /// <summary>Gets the id of the first item's text element, or <see langword="null"/>.</summary>
    public string? FirstTextId { get; init; }

    /// <summary>Gets a counter moved when the items changed, which re-runs a placement from cleared heights.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int ItemsRevision { get; init; }

    /// <summary>Gets the index of the trigger's element in the references.</summary>
    public int? Trigger { get; init; }

    /// <summary>Gets the index of the popup's element in the references.</summary>
    public int? Popup { get; init; }

    /// <summary>Gets the index of the list's element in the references.</summary>
    public int? List { get; init; }

    /// <summary>Gets the index of the value part's element in the references.</summary>
    public int? Value { get; init; }

    /// <summary>Gets the index of the positioner's element in the references, for a scroll arrow or a trigger.</summary>
    public int? Positioner { get; init; }

    /// <summary>Gets the direction a scroll arrow scrolls the list in.</summary>
    /// <value><c>up</c> or <c>down</c>, or <see langword="null"/> for another part.</value>
    public string? Direction { get; init; }

    /// <summary>Gets a value indicating whether the module asks the activation decision point inside the click's dispatch.</summary>
    /// <value><see langword="true"/> while an item has a consumer <c>onclick</c>; the default <see langword="false"/> is not sent.</value>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool Decides { get; init; }
}

/// <summary>An item activation the module classified.</summary>
/// <param name="ItemId">The activated item's id.</param>
internal sealed record SelectActivation(string ItemId)
{
    /// <summary>Gets the click that activated the item, or <see langword="null"/> in the hand-over before a decision.</summary>
    public EventSnapshot? Event { get; init; }
}

/// <summary>A change of whether aligned placement is active.</summary>
/// <param name="Aligned">Whether the popup is placed over the trigger.</param>
internal sealed record SelectAlignedChange(bool Aligned);

/// <summary>The visibility of the scroll arrows.</summary>
/// <param name="Up">Whether the scroll-up arrow is visible.</param>
/// <param name="Down">Whether the scroll-down arrow is visible.</param>
internal sealed record SelectArrowsVisibility(bool Up, bool Down);

/// <summary>A blur of the trigger.</summary>
/// <param name="IntoPopup">Whether focus moved into the popup.</param>
internal sealed record SelectTriggerBlur(bool IntoPopup);

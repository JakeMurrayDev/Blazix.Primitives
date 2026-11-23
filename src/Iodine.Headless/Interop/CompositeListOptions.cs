using System.Text.Json.Serialization;

namespace Blazix.Primitives.Interop;

// ADR 04 Amendments 2026-11-03 (1), (2) and (3) (chosen by the user 2026-11-03): list navigation in the `composite`
// module, a list handle with role records on `attach`, one CompositeTwin per handle joined through the interactions
// session token; typeahead wholly in the module; the highlight's writer; `navigate` reused and `leave` new. CMP §6.8 and
// §6.9 name the options; the union is fact sheet A §3's tables (M5 track 1, issue 159). The names are the track's,
// published in its first commit (design points 1-5 of 2026-11-04). Every option is sent only when it differs from
// Base UI's default (useListNavigation.ts:244-267, useTypeahead.ts:73-83), so a Base UI default of `true` is a `bool?`
// whose `null` stands for it.
/// <summary>
/// The list navigation options of a list handle: how the arrow keys, Home and End, the pointer and typeahead move the
/// highlight of a popup list, and when they open or close it. Every handle of one list carries the same options.
/// </summary>
internal sealed record CompositeListOptions
{
    /// <summary>Gets a value indicating whether list navigation is enabled.</summary>
    /// <value><see langword="false"/> to turn the handle's navigation off; <see langword="null"/>, the default, enables it.</value>
    public bool? Enabled { get; init; }

    /// <summary>Gets a value indicating whether a step past either end wraps to the other end.</summary>
    /// <value><see langword="true"/> to wrap; the default is <see langword="false"/>, which is not sent.</value>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool LoopFocus { get; init; }

    /// <summary>Gets the arrow keys that move the highlight inside the list.</summary>
    /// <value><c>horizontal</c> or <c>both</c>; <see langword="null"/>, the default, for <c>vertical</c>.</value>
    public string? Orientation { get; init; }

    /// <summary>Gets the arrow keys that open the list from its trigger.</summary>
    /// <value><c>horizontal</c>, <c>vertical</c> or <c>both</c>; <see langword="null"/>, the default, for the list's orientation.</value>
    public string? TriggerOrientation { get; init; }

    /// <summary>Gets a value indicating whether the reading direction is right to left, which swaps the horizontal arrow keys.</summary>
    /// <value><see langword="true"/> for right to left; the default is <see langword="false"/>, which is not sent.</value>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool Rtl { get; init; }

    /// <summary>Gets a value indicating whether the highlight moves without moving the focus.</summary>
    /// <value>
    /// <see langword="true"/> for virtual focus: the focus stays on the reference, and the module writes
    /// <c>aria-activedescendant</c>; the default is <see langword="false"/>, which is not sent.
    /// </value>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool Virtual { get; init; }

    /// <summary>Gets a value indicating whether the list is nested in another list, such as a submenu.</summary>
    /// <value><see langword="true"/> for a nested list; the default is <see langword="false"/>, which is not sent.</value>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool Nested { get; init; }

    /// <summary>Gets the orientation of the parent list, which decides the keys that open and close a nested list.</summary>
    /// <value>
    /// <c>horizontal</c>, <c>vertical</c> or <c>both</c>; <see langword="null"/>, the default, reads the orientation the
    /// parent popup's list published.
    /// </value>
    public string? ParentOrientation { get; init; }

    /// <summary>Gets a value indicating whether an item is highlighted when the list opens.</summary>
    /// <value>
    /// <see langword="true"/> or <see langword="false"/>; <see langword="null"/>, the default, for <c>auto</c>: an item is
    /// highlighted on a keyboard open only.
    /// </value>
    public bool? FocusItemOnOpen { get; init; }

    // MNU-STA-20 (M5 track 12): a filterable menu's list sends a value.
    /// <summary>Gets what an item change does to a highlight that another item would take: <c>first</c>, <c>clear</c> or <c>auto</c>.</summary>
    /// <value><see langword="null"/>, the default, to keep the index.</value>
    public string? ItemsChangeHighlight { get; init; }

    // MNU-ARIA-06 (M5 track 12, the orchestrator's 2026-11-12 grant): a filterable menu's list sends false.
    /// <summary>Gets a value indicating whether the floating element carries <c>aria-activedescendant</c> under virtual focus.</summary>
    /// <value><see langword="false"/> to leave it out; <see langword="null"/>, the default, writes it.</value>
    public bool? FloatingActiveDescendant { get; init; }

    // SEL-STA-07 (Select's finding; queued by the orchestrator 2026-11-10): Base UI's Select ignores the close's reset.
    /// <summary>Gets a value indicating whether the highlight survives the close's reset while the popup animates out.</summary>
    /// <value><see langword="true"/> for Select's list; the default <see langword="false"/> is not sent.</value>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool RetainHighlightWhileClosing { get; init; }

    /// <summary>Gets a value indicating whether a step past either end clears the highlight under virtual focus.</summary>
    /// <value><see langword="true"/> to allow it; the default is <see langword="false"/>, which is not sent.</value>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool AllowEscape { get; init; }

    /// <summary>Gets a value indicating whether an arrow key on the closed trigger opens the list.</summary>
    /// <value><see langword="false"/> to keep it closed; <see langword="null"/>, the default, opens it.</value>
    public bool? OpenOnArrowKeyDown { get; init; }

    /// <summary>Gets a value indicating whether moving the pointer over an item highlights it.</summary>
    /// <value><see langword="false"/> to ignore the pointer; <see langword="null"/>, the default, highlights it.</value>
    public bool? FocusItemOnHover { get; init; }

    /// <summary>Gets a value indicating whether the pointer leaving an item clears the highlight.</summary>
    /// <value><see langword="false"/> to keep the highlight; <see langword="null"/>, the default, clears it.</value>
    public bool? ResetOnPointerLeave { get; init; }

    /// <summary>Gets the indices of the items navigation skips.</summary>
    /// <value>
    /// The indices, which make every other item navigable; an empty list for none; or <see langword="null"/>, the
    /// default, to read each item's disabled state from the document.
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

    /// <summary>Gets the index of the selected item, which the list opens on.</summary>
    /// <value>The index, or <see langword="null"/>, the default, when no item is selected.</value>
    public int? SelectedIndex { get; init; }

    /// <summary>Gets a value indicating whether the items are a grid, navigated in two dimensions.</summary>
    /// <value>
    /// <see langword="true"/> for a grid of two columns, or of the rows the items' <c>role="row"</c> ancestors form; the
    /// default is <see langword="false"/>, which is not sent.
    /// </value>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool Grid { get; init; }

    /// <summary>Gets a value indicating whether a nested list that is the root of its menu stays open on the key that closes a nested list.</summary>
    /// <value><see langword="true"/> to stay open, as a context menu's root menu does; the default is <see langword="false"/>, which is not sent.</value>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool KeepOpenOnCrossOrientationKey { get; init; }

    /// <summary>Gets the element that takes the focus back when a nested list closes.</summary>
    /// <value>
    /// The index of the element in the references sent with <c>attach</c>, such as the parent menu's input; or
    /// <see langword="null"/>, the default, for the reference.
    /// </value>
    public int? NestedReturnFocusElement { get; init; }

    /// <summary>Gets the composite root a keydown that the list does not stop is relayed to.</summary>
    /// <value>
    /// The index of the root's element in the references sent with <c>attach</c>, such as a menubar's; or
    /// <see langword="null"/>, the default, to relay nothing.
    /// </value>
    public int? RelayElement { get; init; }

    /// <summary>Gets the highlight C# asks for.</summary>
    /// <value>The latest request, which the module applies once; or <see langword="null"/>, the default, before any request.</value>
    public CompositeHighlightRequest? Highlight { get; init; }

    /// <summary>Gets the typeahead of the list.</summary>
    /// <value>The typeahead options, or <see langword="null"/>, the default, for no typeahead.</value>
    public CompositeTypeaheadOptions? Typeahead { get; init; }

    // CMP-ALG-13: Base UI's grid navigator takes the column count of the wrapper a consumer gives it (2 by default).
    /// <summary>Gets the number of columns of a grid without rows.</summary>
    /// <value>The count, or <see langword="null"/>, the default, for 2.</value>
    public int? Cols { get; init; }

    // A windowed list (Combobox's `virtualized`, CMB-API-22) registers only the items it renders, by explicit index.
    /// <summary>Gets the number of items of a windowed list, of which only some are rendered and registered.</summary>
    /// <value>The count, or <see langword="null"/>, the default, for the registered items.</value>
    public int? ItemCount { get; init; }
}

// ADR 04 Amendments 2026-11-03 (2): a C#-origin highlight goes through `update` with a revision, so a stale echo is
// ignored (CMP-STA-32).
/// <summary>A highlight C# asks the list for, such as an automatic highlight, a clear or the selected item.</summary>
/// <param name="Index">The index to highlight, or <see langword="null"/> to clear the highlight.</param>
/// <param name="Revision">The request's number, which grows with each request; the module applies each revision once.</param>
internal sealed record CompositeHighlightRequest(int? Index, int Revision);

// CMP §6.9 (useTypeahead): Menu passes its elements, so invisible and natively disabled items are skipped; Select passes
// the `isElementDisabled` predicate, so force-mounted hidden items still match; Combobox's trigger passes neither and,
// with `items`, labels from C#-side data (fact sheet A §3).
/// <summary>The typeahead options of a list: the reset time, the items it skips and where its labels come from.</summary>
internal sealed record CompositeTypeaheadOptions
{
    /// <summary>Gets the time after the last key at which a new search starts.</summary>
    /// <value>The time in milliseconds; 750 by default.</value>
    public int ResetMs { get; init; } = 750;

    /// <summary>Gets the items a search skips.</summary>
    /// <value>
    /// <c>element</c> to skip the items whose element is hidden or natively disabled; <c>attribute</c> to skip the items
    /// whose element carries <c>disabled</c> or <c>aria-disabled="true"</c>, or has none; or
    /// <see langword="null"/>, the default, to skip none.
    /// </value>
    public string? Skip { get; init; }

    /// <summary>Gets the labels the search matches, by index.</summary>
    /// <value>
    /// One label per item, <see langword="null"/> for an item without one; or <see langword="null"/>, the default, to match
    /// the labels of the registered items.
    /// </value>
    public IReadOnlyList<string?>? Labels { get; init; }
}

// The reference role of CMP-STR-06: the trigger's handlers and, under virtual focus, `aria-activedescendant`.
/// <summary>The role of the element that holds the focus outside the list: the active trigger, or the input under virtual focus.</summary>
internal sealed record CompositeReferenceOptions;

// The trigger role of CMP-STR-06: an inactive or detached trigger, or the trigger whose list is navigated from an input.
/// <summary>The role of a trigger that opens the list but is not its reference.</summary>
internal sealed record CompositeTriggerOptions;

// The floating role of CMP-STR-06 (design point 1's C# refinement (r1) of 2026-11-04): the items, their order report and
// the handled-key report travel in the handle's own `CompositeOptions.Items`, `ReportItems` and `ReportHandledKeys`.
/// <summary>The role of the list's floating element, which holds the items and writes their highlight.</summary>
internal sealed record CompositeFloatingOptions;

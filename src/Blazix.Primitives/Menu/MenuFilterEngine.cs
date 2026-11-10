using Microsoft.AspNetCore.Components;

namespace Blazix.Primitives;

// MNU §4.4, STA-15..21, STA-23, STA-24 (filter-dropdown/root/FilterDropdownRoot.tsx:86-196, filter-dropdown/store.ts:9-17,
// menu/filter-root/MenuFilterList.tsx:101-123): the C# filter of one filterable menu (plan decision 10, ADR 01 :199). The
// visible set is computed at the render batch's boundary (MenuRootContext.AtBatchBoundaryAsync), once per batch, over the
// collator or the consumer's Filter; a filtered-out item renders nothing and unregisters from composite.

/// <summary>Represents what the filter does to the highlight after a change.</summary>
internal enum MenuFilterHighlight
{
    /// <summary>The highlight stays where it is.</summary>
    Keep,

    /// <summary>The first visible item is highlighted.</summary>
    First,

    /// <summary>The highlight is cleared.</summary>
    Clear,
}

// STA-15, STA-18, STA-19, STA-21: the inputs of one visible-set pass, read at the batch boundary. Query is what the menu
// shows: the committed value, or the closing query while the popup animates out (useFilterDropdownCloseQuery.ts:37).
/// <summary>Represents the inputs of one pass of the visible-set computation.</summary>
/// <param name="Open">A value indicating whether the menu is open.</param>
/// <param name="Query">The query the menu shows, untrimmed.</param>
/// <param name="Match">The matcher, or <see langword="null"/> when matching is off.</param>
/// <param name="AutoHighlight">The automatic highlight mode.</param>
/// <param name="HasHighlight">A value indicating whether an item is highlighted.</param>
internal readonly record struct MenuFilterPass(bool Open, string Query, Func<string, string, bool>? Match, AutoHighlightValue AutoHighlight, bool HasHighlight);

// STA-16, STA-17: one item's registration. Text is the menu-filter module's report of the element's textContent (design
// point 2 (b′), settled by the orchestrator 2026-11-11); Label, when supplied, wins and never crosses the boundary.
/// <summary>Represents one item registered with a menu's filter.</summary>
internal sealed class MenuFilterItem
{
    private readonly Action changed;
    private bool visible = true;

    /// <summary>Initializes a new instance of the <see cref="MenuFilterItem"/> class.</summary>
    /// <param name="changed">The callback run when the item's visibility changes, which renders the item.</param>
    public MenuFilterItem(Action changed) => this.changed = changed ?? throw new ArgumentNullException(nameof(changed));

    /// <summary>Gets or sets the item's explicit label, or <see langword="null"/>.</summary>
    public string? Label { get; set; }

    /// <summary>Gets or sets the item's rendered text as last reported, or <see langword="null"/> before the first report.</summary>
    public string? Text { get; set; }

    /// <summary>Gets the text the filter matches: the label, else the reported text, else <see langword="null"/>.</summary>
    public string? FilterText => Label ?? Text;

    /// <summary>Gets a value indicating whether the item matches the query; a hidden item renders nothing.</summary>
    public bool Visible => visible;

    // Only a change notifies, so a keystroke that keeps an item visible never renders it (MNU-STA-16).
    internal void SetVisible(bool value)
    {
        if (visible != value)
        {
            visible = value;
            changed();
        }
    }
}

/// <summary>Represents the filter of one filterable menu: the query's registration of items, the visible set and the highlight rules.</summary>
internal sealed class MenuFilterEngine
{
    private readonly List<MenuFilterItem> items = [];
    private HashSet<MenuFilterItem>? visibleItems;
    private HashSet<MenuFilterItem>? spare;
    private string? lastFilterQuery;

    /// <summary>Gets the number of registered items, updated as items register and unregister.</summary>
    public int RegisteredCount => items.Count;

    /// <summary>Gets a value indicating whether a visible set exists: <see langword="false"/> while the query is empty or matching is off.</summary>
    public bool HasVisibleSet => visibleItems is not null;

    /// <summary>Gets a value indicating whether the menu shows no item: no item registered and no visible set, or an empty visible set.</summary>
    public bool IsEmpty => visibleItems is null ? items.Count == 0 : visibleItems.Count == 0;

    // STA-19: open and (Always, or true with a non-empty trimmed query).
    /// <summary>Determines whether the automatic highlight applies to the current query.</summary>
    /// <param name="open">A value indicating whether the menu is open.</param>
    /// <param name="autoHighlight">The automatic highlight mode.</param>
    /// <param name="filterQuery">The trimmed query.</param>
    /// <returns><see langword="true"/> when the first visible item is highlighted automatically.</returns>
    public static bool AutoHighlightEnabled(bool open, AutoHighlightValue autoHighlight, string filterQuery) =>
        open && (autoHighlight.IsAlways || (autoHighlight.IsEnabled && filterQuery.Length > 0));

    /// <summary>Registers an item with the filter.</summary>
    /// <param name="item">The item.</param>
    /// <returns>The registration; disposing it unregisters the item.</returns>
    public IDisposable Register(MenuFilterItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        items.Add(item);
        return new Registration(this, item);
    }

    // STA-15, STA-16, STA-18, STA-19: FilterDropdownRoot.tsx:131-184, run once per render batch.
    /// <summary>Recomputes the visible set and decides the highlight.</summary>
    /// <param name="pass">The inputs of the pass.</param>
    /// <returns>What happens to the highlight.</returns>
    public MenuFilterHighlight Recompute(in MenuFilterPass pass)
    {
        string filterQuery = pass.Query.Trim();
        // An unused filter must not re-run the automatic highlight (FilterDropdownRoot.tsx:86-88).
        Func<string, string, bool>? match = filterQuery.Length == 0 ? null : pass.Match;
        bool autoHighlightEnabled = AutoHighlightEnabled(pass.Open, pass.AutoHighlight, filterQuery);
        bool queryChanged = lastFilterQuery is not null && !string.Equals(lastFilterQuery, filterQuery, StringComparison.Ordinal);
        lastFilterQuery = filterQuery;

        // :137-151: with no query or external filtering every registered item is visible; external filtering still follows
        // the automatic highlight, and a registry update keeps the current highlight.
        if (match is null)
        {
            Publish(null);
            if (autoHighlightEnabled && items.Count > 0)
            {
                return queryChanged || !pass.HasHighlight ? MenuFilterHighlight.First : MenuFilterHighlight.Keep;
            }

            return filterQuery.Length == 0 && queryChanged ? MenuFilterHighlight.Clear : MenuFilterHighlight.Keep;
        }

        // :155-159 (STA-16): publishing an empty result before anything registered would hide every item as it arrives.
        if (visibleItems is null && items.Count == 0)
        {
            return MenuFilterHighlight.Keep;
        }

        HashSet<MenuFilterItem> next = spare ?? [];
        next.Clear();
        bool hasNewMatch = visibleItems is null;
        foreach (MenuFilterItem item in items)
        {
            // Design point 2 (b'): an item whose text has not been reported stays visible until it is.
            string? text = item.FilterText;
            if (text is null || match(text, filterQuery))
            {
                next.Add(item);
                hasNewMatch |= visibleItems?.Contains(item) != true;
            }
        }

        // :172-184: new matches or a smaller result set invalidate the previous item identities; the first filtered result
        // has none to invalidate unless the query itself changed.
        if (hasNewMatch || visibleItems!.Count != next.Count)
        {
            MenuFilterHighlight highlight = autoHighlightEnabled && next.Count > 0
                ? MenuFilterHighlight.First
                : visibleItems is not null || queryChanged ? MenuFilterHighlight.Clear : MenuFilterHighlight.Keep;
            Publish(next);
            return highlight;
        }

        spare = next;
        return autoHighlightEnabled && queryChanged && next.Count > 0 ? MenuFilterHighlight.First : MenuFilterHighlight.Keep;
    }

    // STA-18: FilterDropdownRoot.tsx:98-107, indexes are positional.
    /// <summary>Decides the highlight after a query change was requested.</summary>
    /// <param name="canceled">A value indicating whether the consumer canceled the change.</param>
    /// <param name="autoHighlight">The automatic highlight mode.</param>
    /// <returns>What happens to the highlight.</returns>
    public static MenuFilterHighlight AfterValueChange(bool canceled, AutoHighlightValue autoHighlight) =>
        !canceled && !autoHighlight.IsEnabled ? MenuFilterHighlight.Clear : MenuFilterHighlight.Keep;

    // STA-20: FilterDropdownRoot.tsx:109-125, MenuFilterList.tsx:101-123.
    /// <summary>Decides the highlight after the ordered item list changed while open.</summary>
    /// <param name="sameItemAtHighlight">A value indicating whether the element at the highlighted index is the one that was there before.</param>
    /// <param name="autoHighlightEnabled">A value indicating whether the automatic highlight applies to the current query.</param>
    /// <param name="itemCount">The number of items in the list.</param>
    /// <returns>What happens to the highlight.</returns>
    public static MenuFilterHighlight AfterItemsChange(bool sameItemAtHighlight, bool autoHighlightEnabled, int itemCount) =>
        sameItemAtHighlight ? MenuFilterHighlight.Keep : autoHighlightEnabled && itemCount > 0 ? MenuFilterHighlight.First : MenuFilterHighlight.Clear;

    // Swaps the published set (the previous one becomes the next pass's scratch) and notifies the items whose visibility
    // changed; null publishes "every item visible".
    private void Publish(HashSet<MenuFilterItem>? next)
    {
        if (next is null && visibleItems is null)
        {
            return;
        }

        HashSet<MenuFilterItem>? previous = visibleItems;
        visibleItems = next;
        spare = previous;
        foreach (MenuFilterItem item in items)
        {
            item.SetVisible(next is null || next.Contains(item));
        }
    }

    private sealed class Registration(MenuFilterEngine engine, MenuFilterItem item) : IDisposable
    {
        private bool disposed;

        /// <inheritdoc />
        public void Dispose()
        {
            if (!disposed)
            {
                disposed = true;
                engine.items.Remove(item);
            }
        }
    }
}

// MNU §4.4: what a filterable root shares with its filter parts (the Input, Clear, Empty, List, groups, items and submenu
// triggers), reached through MenuRootContext.Filter.
/// <summary>Represents the filter state a filterable menu root shares with the parts in its subtree.</summary>
internal sealed class MenuFilterRootContext
{
    private readonly MenuFilterProviderContext provider;

    /// <summary>Initializes a new instance of the <see cref="MenuFilterRootContext"/> class.</summary>
    /// <param name="provider">The provider the root consumed.</param>
    public MenuFilterRootContext(MenuFilterProviderContext provider) => this.provider = provider;

    /// <summary>Gets the filter engine.</summary>
    public MenuFilterEngine Engine { get; } = new();

    /// <summary>Gets the committed query.</summary>
    public string Query => provider.Value;

    /// <summary>Gets a value indicating whether the Clear button renders: the query is not empty.</summary>
    public bool ShowsClear => Query.Length > 0;

    /// <summary>Gets the id of the filter list, which the input's <c>aria-controls</c> names, or <see langword="null"/>.</summary>
    public string? ListId { get; private set; }

    /// <summary>Gets the input's element, the list's reference and the nested menus' return target, or <see langword="null"/>.</summary>
    public ElementReference? InputElement { get; private set; }

    /// <summary>Gets a value indicating whether the input shows its focus ring, the menu-filter module's mirror.</summary>
    public bool InputHighlighted { get; private set; }

    /// <summary>Registers the menu's input, its focus owner.</summary>
    /// <param name="element">The input's element.</param>
    /// <returns>The registration; disposing it unregisters the input.</returns>
    public IDisposable RegisterInput(ElementReference element) => throw new NotImplementedException();

    /// <summary>Records the filter list's rendered id.</summary>
    /// <param name="id">The id, or <see langword="null"/> when the list renders none.</param>
    public void SetListId(string? id) => ListId = string.IsNullOrEmpty(id) ? null : id;

    /// <summary>Requests a query change through the provider's <c>OnValueChange</c>.</summary>
    /// <param name="value">The new query.</param>
    /// <param name="reason">The reason: input change, input clear, clear press or popup close.</param>
    /// <param name="snapshot">The event the change came from, or <see langword="null"/>.</param>
    /// <returns>A task that completes when the request and its highlight follow-up ran.</returns>
    public Task RequestQueryAsync(string value, EventReason reason, EventSnapshot? snapshot) => throw new NotImplementedException();
}

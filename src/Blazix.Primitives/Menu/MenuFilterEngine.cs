using System.Globalization;
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

    /// <summary>Gets or sets the item's rendered id, by which the List's handle reports its text (design point 2 (b′)).</summary>
    public string? Id { get; set; }

    /// <summary>Gets the text the filter matches: the label, else the reported text, else <see langword="null"/>.</summary>
    public string? FilterText => Label ?? Text;

    /// <summary>Gets a value indicating whether the item matches the query; a hidden item renders nothing.</summary>
    public bool Visible => visible;

    // An item's visibility before its first render, set at registration without a notification.
    internal void Seed(bool value) => visible = value;

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
    // Design point 3 (a), settled by the orchestrator 2026-11-11 (CMP-STA-19..21): the items, keyed by themselves, in a
    // KeyedItemRegistry; the engine reads the live items, as FilterDropdownRoot.tsx:131-135 reads the live registry, and its
    // owner publishes the snapshot at its batch boundary.
    private readonly KeyedItemRegistry<MenuFilterItem, MenuFilterItem> registry;
    private HashSet<MenuFilterItem>? visibleItems;
    private HashSet<MenuFilterItem>? spare;
    private string? lastFilterQuery;

    /// <summary>Initializes a new instance of the <see cref="MenuFilterEngine"/> class whose registrations ask for nothing.</summary>
    public MenuFilterEngine()
        : this(static () => { })
    {
    }

    /// <summary>Initializes a new instance of the <see cref="MenuFilterEngine"/> class.</summary>
    /// <param name="requestPublish">The callback a registration change runs, at most once per batch, to ask for <see cref="PublishItems"/>.</param>
    public MenuFilterEngine(Action requestPublish) => registry = new KeyedItemRegistry<MenuFilterItem, MenuFilterItem>(requestPublish);

    /// <summary>Gets the number of registered items, updated as items register and unregister.</summary>
    public int RegisteredCount => registry.LiveItems.Count;

    /// <summary>Gets the registered items, in registration order, including those registered since the last snapshot.</summary>
    public IReadOnlyList<MenuFilterItem> Items => registry.LiveValues;

    /// <summary>Gets the items of the snapshot published at the latest batch boundary.</summary>
    public IEnumerable<MenuFilterItem> PublishedItems => registry.Items.Values;

    /// <summary>Gets a value indicating whether a visible set exists: <see langword="false"/> while the query is empty or matching is off.</summary>
    public bool HasVisibleSet => visibleItems is not null;

    /// <summary>Gets a value indicating whether the menu shows no item: no item registered and no visible set, or an empty visible set.</summary>
    public bool IsEmpty => visibleItems is null ? registry.LiveItems.Count == 0 : visibleItems.Count == 0;

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
        return registry.Register(item, item);
    }

    /// <summary>Publishes the registry's snapshot when a registration changed since the last one; the owner calls it at its batch boundary.</summary>
    /// <returns><see langword="true"/> when a new snapshot was published.</returns>
    public bool PublishItems() => registry.Publish();

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
            if (autoHighlightEnabled && registry.LiveItems.Count > 0)
            {
                return queryChanged || !pass.HasHighlight ? MenuFilterHighlight.First : MenuFilterHighlight.Keep;
            }

            return filterQuery.Length == 0 && queryChanged ? MenuFilterHighlight.Clear : MenuFilterHighlight.Keep;
        }

        // :155-159 (STA-16): publishing an empty result before anything registered would hide every item as it arrives.
        if (visibleItems is null && registry.LiveItems.Count == 0)
        {
            return MenuFilterHighlight.Keep;
        }

        HashSet<MenuFilterItem> next = spare ?? [];
        next.Clear();
        bool hasNewMatch = visibleItems is null;
        foreach (MenuFilterItem item in registry.LiveValues)
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
        foreach (MenuFilterItem item in registry.LiveValues)
        {
            item.SetVisible(next is null || next.Contains(item));
        }
    }

}

// MNU §4.4: what a filterable root shares with its filter parts (the Input, Clear, Empty, List, groups, items and submenu
// triggers), reached through MenuRootContext.Filter. The visible set is recomputed by Flush, which the parts call after
// their render (registrations of a batch all precede it) and which a query change runs at once; an item's own visibility
// is also set when it registers or relabels, so an item that does not match never renders (FilterDropdownRoot.tsx's
// layout effect, which runs before paint).
/// <summary>Represents the filter state a filterable menu root shares with the parts in its subtree.</summary>
internal sealed class MenuFilterRootContext
{
    private readonly MenuFilterProviderContext provider;
    private readonly MenuRootContext root;
    private readonly List<Action> subscribers = [];
    private readonly List<Action> partSubscribers = [];
    private IReadOnlyList<string> textIds = [];
    private bool textIdsDirty;
    private Action? textIdsChanged;
    private Action? inputIdChanged;
    private string inputText;
    private string? flushedQuery;
    private Func<string, string, bool>? flushedMatch;
    private bool dirty;
    private CultureInfo? defaultCulture;
    private Func<string, string, bool>? defaultMatch;
    private readonly MenuSubscription rootSubscription;
    private readonly MenuSelection<bool> open;
    private readonly MenuSelection<bool> mounted;
    private bool wasOpen;
    private string? closeQuery;
    private int inputs;
    private bool inputCheckPending;

    /// <summary>Initializes a new instance of the <see cref="MenuFilterRootContext"/> class.</summary>
    /// <param name="provider">The provider the root consumed.</param>
    /// <param name="root">The filterable root.</param>
    public MenuFilterRootContext(MenuFilterProviderContext provider, MenuRootContext root)
    {
        this.provider = provider;
        this.root = root;

        // A registration change asks for the next flush, the filter's batch boundary, which publishes the snapshot.
        Engine = new MenuFilterEngine(() => dirty = true);
        inputText = provider.Value;
        rootSubscription = root.Subscribe(OnRootChanged);
        open = rootSubscription.Select(MenuSelectors.Open);
        mounted = rootSubscription.Select(MenuSelectors.Mounted);
        wasOpen = open.Value;
        inputCheckPending = wasOpen;
    }

    // MNU-STA-16, STA-23: the match the provider sets, or the collator's contains under its culture (MNU-API-54, API-56).
    private Func<string, string, bool>? Match
    {
        get
        {
            if (provider.FilterSupplied)
            {
                return provider.Filter;
            }

            if (defaultMatch is null || !Equals(defaultCulture, provider.Culture))
            {
                defaultCulture = provider.Culture;
                defaultMatch = MenuFilter.Create(provider.Culture).Contains;
            }

            return defaultMatch;
        }
    }

    /// <summary>Gets the filter engine.</summary>
    public MenuFilterEngine Engine { get; }

    // MNU-EVT-18 (useFilterDropdownCloseQuery.ts:19-38): while the popup animates out, the menu keeps showing the query it
    // closed with, after the reset committed ''.
    /// <summary>Gets the query the menu shows: the committed query, or the closing query until the menu reopens or unmounts.</summary>
    public string Query => !open.Value && closeQuery is not null ? closeQuery : provider.Value;

    /// <summary>
    /// Gets the count of times the committed query was sent for the input to write: it moves only when the query differs
    /// from the input's text, so a committed keystroke never overwrites a later one still on its way.
    /// </summary>
    public int QueryRevision { get; private set; }

    /// <summary>Gets the provider's automatic highlight mode.</summary>
    public AutoHighlightValue AutoHighlight => provider.AutoHighlight;

    /// <summary>Gets a value indicating whether the Clear button renders: the query is not empty.</summary>
    public bool ShowsClear => Query.Length > 0;

    /// <summary>Gets the id of the filter list, which the input's <c>aria-controls</c> names, or <see langword="null"/>.</summary>
    public string? ListId { get; private set; }

    /// <summary>Gets the id of the menu's input, the focus owner the popup focuses on open, or <see langword="null"/>.</summary>
    public string? InputId { get; private set; }

    /// <summary>Gets the input's element, the list's reference and the nested menus' return target, or <see langword="null"/>.</summary>
    public ElementReference? InputElement { get; private set; }

    // Design point 2 (b′): the ids of the registered items without a label, whose rendered text the List's handle reports;
    // the same instance while the set is unchanged, so an unchanged options record sends nothing.
    /// <summary>Gets the ids of the registered items whose rendered text the filter needs, in registration order.</summary>
    public IReadOnlyList<string> TextIds
    {
        get
        {
            if (textIdsDirty)
            {
                textIdsDirty = false;
                List<string> next = [];
                foreach (MenuFilterItem item in Engine.Items)
                {
                    if (item.Label is null && item.Id is { Length: > 0 } id)
                    {
                        next.Add(id);
                    }
                }

                if (!next.SequenceEqual(textIds))
                {
                    textIds = next;
                }
            }

            return textIds;
        }
    }

    /// <summary>Gets the highlight the last recompute asked for, which the list's virtual focus applies.</summary>
    public MenuFilterHighlight PendingHighlight { get; private set; }

    /// <summary>Registers the menu's input, its focus owner.</summary>
    /// <returns>The registration; disposing it unregisters the input.</returns>
    public IDisposable RegisterInput()
    {
        inputs++;
        return new InputRegistration(this);
    }

    // MNU-API-62 (FilterDropdownList.tsx:23-27): the input's aria-controls follows the list's id; an empty id is none.
    /// <summary>Records the filter list's rendered id, and tells the input when it changed.</summary>
    /// <param name="id">The id, or <see langword="null"/> when the list renders none.</param>
    public void SetListId(string? id)
    {
        string? next = string.IsNullOrEmpty(id) ? null : id;
        if (!string.Equals(ListId, next, StringComparison.Ordinal))
        {
            ListId = next;
            for (int i = 0; i < partSubscribers.Count; i++)
            {
                partSubscribers[i]();
            }
        }
    }

    // MenuFilterPopup.tsx:38-50 (M5 track 12): the popup's initial focus is the input, which it names by id.
    /// <summary>Records the input's rendered id, and tells the popup when it changed.</summary>
    /// <param name="id">The id, or <see langword="null"/> when no input is rendered.</param>
    public void SetInputId(string? id)
    {
        string? next = string.IsNullOrEmpty(id) ? null : id;
        if (!string.Equals(InputId, next, StringComparison.Ordinal))
        {
            InputId = next;
            inputIdChanged?.Invoke();
        }
    }

    /// <summary>Subscribes to changes of the query, the provider's options and the list's id: the Input's and the Clear's.</summary>
    /// <param name="changed">The callback run after a change, once <see cref="QueryRevision"/> and the visible set are current.</param>
    /// <returns>The subscription; disposing it unsubscribes.</returns>
    public IDisposable Subscribe(Action changed)
    {
        partSubscribers.Add(changed);
        IDisposable query = provider.Subscribe(() =>
        {
            Track();
            Flush();
            changed();
        });
        return new Subscription(partSubscribers, changed, query);
    }

    /// <summary>Subscribes to the visible set becoming empty or not, and to changes of the registry: the Empty's.</summary>
    /// <param name="changed">The callback run after such a change; a registry change asks for a render whose flush decides.</param>
    /// <returns>The subscription; disposing it unsubscribes.</returns>
    public IDisposable SubscribeVisibleSet(Action changed)
    {
        subscribers.Add(changed);
        IDisposable query = provider.Subscribe(() =>
        {
            Track();
            Flush();
        });
        return new Subscription(subscribers, changed, query);
    }

    /// <summary>Subscribes the List to changes of <see cref="TextIds"/>.</summary>
    /// <param name="changed">The callback run when the set of ids may have changed.</param>
    /// <returns>The subscription; disposing it unsubscribes.</returns>
    public IDisposable SubscribeTextIds(Action changed)
    {
        textIdsChanged = changed;
        return new TextIdsSubscription(this, changed);
    }

    /// <summary>Subscribes to changes of the input's id: the Popup's.</summary>
    /// <param name="changed">The callback run after the id changed.</param>
    /// <returns>The subscription; disposing it unsubscribes.</returns>
    public IDisposable SubscribeInputId(Action changed)
    {
        inputIdChanged = changed;
        return new InputIdSubscription(this, changed);
    }

    /// <summary>Registers an item with the engine and sets its visibility for its first render.</summary>
    /// <param name="item">The item, its label and id already set.</param>
    /// <returns>The registration; disposing it unregisters the item.</returns>
    public IDisposable RegisterItem(MenuFilterItem item)
    {
        item.Seed(Matches(item));
        dirty = true;
        IDisposable registration = Engine.Register(item);
        TextIdsChanged();
        return new ItemRegistration(this, registration);
    }

    /// <summary>Records that an item's label or id changed, which may change <see cref="TextIds"/>.</summary>
    public void TextIdsChanged()
    {
        textIdsDirty = true;
        textIdsChanged?.Invoke();
    }

    // Design point 2 (b′): the List's report of what items render; a changed text sets the item's visibility at once and
    // the visible set is recomputed (useFilterDropdownItem.ts:150-158, re-registering on a text change).
    /// <summary>Applies the rendered texts the List's handle reported.</summary>
    /// <param name="ids">The items' ids.</param>
    /// <param name="texts">Their texts, in the same order.</param>
    public void SetTexts(IReadOnlyList<string> ids, IReadOnlyList<string> texts)
    {
        IReadOnlyList<MenuFilterItem> items = Engine.Items;
        for (int i = 0; i < ids.Count && i < texts.Count; i++)
        {
            for (int j = 0; j < items.Count; j++)
            {
                MenuFilterItem item = items[j];
                if (string.Equals(item.Id, ids[i], StringComparison.Ordinal))
                {
                    if (!string.Equals(item.Text, texts[i], StringComparison.Ordinal))
                    {
                        item.Text = texts[i];
                        ItemChanged(item);
                    }

                    break;
                }
            }
        }

        Flush();
    }

    /// <summary>Records that an item's label or text changed, and sets its visibility at once.</summary>
    /// <param name="item">The item.</param>
    public void ItemChanged(MenuFilterItem item)
    {
        dirty = true;
        item.SetVisible(Matches(item));
    }

    // MNU-STA-15..20 (FilterDropdownRoot.tsx:131-196): the visible set and the highlight it asks for, once per change of the
    // query, the filter or the registry; the visible-set subscribers (Empty) hear of it only when its emptiness changed, and an item
    // hears of its own visibility (ADR 16: no render for an unchanged result).
    /// <summary>Recomputes the visible set when the query or the registry changed since the last recompute.</summary>
    public void Flush()
    {
        CheckInput();
        string query = Query;
        Func<string, string, bool>? match = Match;
        if (!dirty && string.Equals(flushedQuery, query, StringComparison.Ordinal) && ReferenceEquals(flushedMatch, match))
        {
            return;
        }

        // MNU-STA-15: a new filter (a Filter or Culture parameter) re-filters every item at once, as the query does.
        if (!ReferenceEquals(flushedMatch, match) && flushedQuery is not null)
        {
            IReadOnlyList<MenuFilterItem> items = Engine.Items;
            for (int i = 0; i < items.Count; i++)
            {
                items[i].SetVisible(Matches(items[i]));
            }
        }

        dirty = false;
        flushedQuery = query;
        flushedMatch = match;
        bool wasEmpty = Engine.IsEmpty;
        Engine.PublishItems();
        PendingHighlight = Engine.Recompute(new MenuFilterPass(root.State.Open, query, match, provider.AutoHighlight, root.HighlightedItemId is not null));
        ApplyHighlight(PendingHighlight);
        if (Engine.IsEmpty != wasEmpty)
        {
            Notify();
        }
    }

    // MNU-EVT-16 (FilterDropdownInput.tsx:96-121, FilterDropdownRoot.tsx:155-178): the provider runs onValueChange and commits
    // unless canceled. An input edit is the input's text already, so a commit of it sends nothing to write; a canceled or
    // rewritten one moves the revision and the module writes the committed query back, React's re-render of the controlled
    // input. The highlight that follows a change lands with the list's virtual focus.
    /// <summary>Requests a query change through the provider's <c>OnValueChange</c>.</summary>
    /// <param name="value">The new query.</param>
    /// <param name="reason">The reason: input change, input clear, clear press or popup close.</param>
    /// <param name="snapshot">The event the change came from, or <see langword="null"/>.</param>
    /// <returns>A task that completes when the request ran.</returns>
    public async Task RequestQueryAsync(string value, EventReason reason, EventSnapshot? snapshot)
    {
        if (reason is EventReason.InputChange or EventReason.InputClear)
        {
            inputText = value;
        }

        await provider.RequestValueAsync(value, reason, snapshot);
        Track();
        Flush();
    }

    // MNU-EVT-18 (useFilterDropdownCloseQuery.ts:28-35): a close with a non-empty query requests '' with popup-close once
    // the close has committed; reopening or unmounting drops the closing query, which writes the reset into a kept input.
    private void OnRootChanged()
    {
        bool isOpen = open.Value;
        bool closed = wasOpen && !isOpen;
        inputCheckPending |= !wasOpen && isOpen;
        wasOpen = isOpen;
        if (closed && provider.Value.Length > 0)
        {
            closeQuery = provider.Value;
            _ = ResetOnCloseAsync();
        }
        else if ((isOpen || !mounted.Value) && closeQuery is not null)
        {
            closeQuery = null;
            Track();
            Flush();
            for (int i = 0; i < partSubscribers.Count; i++)
            {
                partSubscribers[i]();
            }
        }
    }

    // FilterDropdownRoot.tsx:131-196 (M5 track 12): the recompute's highlight reaches the list's virtual focus, the first
    // visible item or none; the module keeps the index over the filtered render.
    private void ApplyHighlight(MenuFilterHighlight highlight)
    {
        if (highlight == MenuFilterHighlight.First)
        {
            _ = root.RequestHighlightAsync(0);
        }
        else if (highlight == MenuFilterHighlight.Clear)
        {
            _ = root.RequestHighlightAsync(null);
        }
    }

    private async Task ResetOnCloseAsync()
    {
        // After the close's own commit, not inside the store's notification.
        await Task.Yield();
        await provider.RequestValueAsync("", EventReason.PopupClose, null);
        Track();
        Flush();
    }

    // MNU-EDGE-05 (useMenuFilterPopup.ts:41-52): once per open, after the popup's content rendered (the first flush after
    // the open, run by the parts' after-render), a filterable menu without a Menu.Input warns in development.
    private void CheckInput()
    {
        if (!inputCheckPending || !open.Value)
        {
            return;
        }

        inputCheckPending = false;
        if (inputs == 0 && root.Logger is { } logger && DevelopmentWarnings.IsEnabled)
        {
            DevelopmentWarnings.Write(
                logger,
                "Blazix: A filterable menu opened without a `<MenuInput>`. Render the input inside `<MenuPopup>`, or drop `<MenuFilterProvider>` for a menu that does not filter.");
        }
    }

    private bool Matches(MenuFilterItem item)
    {
        string query = Query.Trim();
        return query.Length == 0 || Match is not { } match || item.FilterText is not { } text || match(text, query);
    }

    private void Track()
    {
        string query = Query;
        if (!string.Equals(query, inputText, StringComparison.Ordinal))
        {
            inputText = query;
            QueryRevision++;
        }
    }

    private void Notify()
    {
        for (int i = 0; i < subscribers.Count; i++)
        {
            subscribers[i]();
        }
    }

    private sealed class Subscription(List<Action> list, Action changed, IDisposable query) : IDisposable
    {
        /// <inheritdoc />
        public void Dispose()
        {
            list.Remove(changed);
            query.Dispose();
        }
    }

    private sealed class InputRegistration(MenuFilterRootContext owner) : IDisposable
    {
        private bool disposed;

        /// <inheritdoc />
        public void Dispose()
        {
            if (!disposed)
            {
                disposed = true;
                owner.inputs--;
            }
        }
    }

    private sealed class InputIdSubscription(MenuFilterRootContext owner, Action changed) : IDisposable
    {
        /// <inheritdoc />
        public void Dispose()
        {
            if (owner.inputIdChanged == changed)
            {
                owner.inputIdChanged = null;
            }
        }
    }

    private sealed class TextIdsSubscription(MenuFilterRootContext owner, Action changed) : IDisposable
    {
        /// <inheritdoc />
        public void Dispose()
        {
            if (owner.textIdsChanged == changed)
            {
                owner.textIdsChanged = null;
            }
        }
    }

    // An unregistered item changes the registry; the subscribers re-render and their next render's Flush recomputes once.
    private sealed class ItemRegistration(MenuFilterRootContext owner, IDisposable registration) : IDisposable
    {
        private bool disposed;

        /// <inheritdoc />
        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            registration.Dispose();
            owner.dirty = true;
            owner.TextIdsChanged();
            owner.Notify();
        }
    }
}

// The filter half of an item part's behaviour (MenuItemBehavior, MenuSubmenuTriggerBehavior): its registration with the
// menu's filter by Label (design point 2 (b′); the rendered text comes through the List's handle) and its visibility.
/// <summary>Represents one item's registration with its menu's filter.</summary>
internal sealed class MenuFilterItemSlot : IDisposable
{
    private readonly MenuFilterRootContext filter;
    private readonly MenuFilterGroupContext? group;
    private readonly MenuFilterItem item;
    private IDisposable? registration;
    private MenuFilterGroupContext.Member? member;

    /// <summary>Initializes a new instance of the <see cref="MenuFilterItemSlot"/> class.</summary>
    /// <param name="filter">The menu's filter.</param>
    /// <param name="changed">The callback run when the item's visibility changes.</param>
    /// <param name="group">The enclosing group's filter context, which counts the item as a member, or <see langword="null"/>.</param>
    public MenuFilterItemSlot(MenuFilterRootContext filter, Action changed, MenuFilterGroupContext? group = null)
    {
        this.filter = filter;
        this.group = group;
        item = new MenuFilterItem(() =>
        {
            member?.Changed();
            changed();
        });
    }

    /// <summary>Gets a value indicating whether the query filters the item out, so that it renders nothing.</summary>
    public bool FilteredOut => !item.Visible;

    /// <inheritdoc />
    public void Dispose()
    {
        member?.Dispose();
        registration?.Dispose();
    }

    /// <summary>Registers the item on the first call, and applies a changed label or id afterwards.</summary>
    /// <param name="label">The item's <c>Label</c>.</param>
    /// <param name="id">The item's rendered id.</param>
    public void Apply(string? label, string? id)
    {
        if (registration is null)
        {
            item.Label = label;
            item.Id = id;
            registration = filter.RegisterItem(item);
            member = group?.Register(() => item.Visible);
            return;
        }

        bool labelChanged = !string.Equals(item.Label, label, StringComparison.Ordinal);
        if (labelChanged || !string.Equals(item.Id, id, StringComparison.Ordinal))
        {
            item.Label = label;
            item.Id = id;
            filter.TextIdsChanged();
            if (labelChanged)
            {
                filter.ItemChanged(item);
            }
        }
    }

    /// <summary>Recomputes the menu's visible set when it changed; called after the item's render.</summary>
    public void Flush() => filter.Flush();
}

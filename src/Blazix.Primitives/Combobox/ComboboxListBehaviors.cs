using Blazix.Primitives.Interop;
using Blazix.Primitives.Rendering;
using Blazix.Primitives.Store;
using Blazix.Primitives.Utilities;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;

namespace Blazix.Primitives;

// combobox/list/ComboboxList.tsx (CMB §3.19, STR-05, STR-06): tabindex="-1", id = the floating id, role listbox or grid,
// aria-multiselectable, aria-readonly (not grid), data-empty. The `composite` floating list handle (the registry, the items'
// listeners by delegation, the highlight's writes), attached after PopupPortalContext.WhenMoved (M4 track 1's design point
// 3). Without Items the module reports the rendered order, which the store joins to the items' values. Its `combobox`
// handle: the items' pointerdown/mousedown default, the drag-select release, the scroll reset (CMB-PTR-03..05, PTR-12).
/// <summary>The behavior of a combobox-family list.</summary>
/// <typeparam name="TValue">The type of the items' values.</typeparam>
internal sealed class ComboboxListBehavior<TValue> : IAsyncDisposable
{
    private readonly ComboboxPartServices services;

    private readonly CompositeRootBehavior composite;

    private readonly Action onOpenChanged;

    private ComboboxStore<TValue>? store;

    private PopupPortalContext? portal;

    private ComboboxTwin? twin;

    private readonly ComboboxHandleSync<ComboboxTwin, ComboboxOptions> sync = new();

    private RenderFragment? cascadedContent;

    private RenderFragment? cascade;

    private string? id;

    private SelectorSubscription<ComboboxStoreState>? openSubscription;

    private ElementReference? lastElement;

    private bool disposed;

    /// <summary>Initializes a new instance of the <see cref="ComboboxListBehavior{TValue}"/> class.</summary>
    /// <param name="services">The part's services.</param>
    /// <param name="onKeyDown">The part's own <c>onkeydown</c> handler.</param>
    public ComboboxListBehavior(ComboboxPartServices services, EventCallback onKeyDown)
    {
        this.services = services;
        composite = new CompositeRootBehavior(services.JSRuntime, services.Ids, services.RequestRender, services.DispatchException, onKeyDown);
        composite.ItemsChanged += OnItemsChanged;
        onOpenChanged = () => _ = Observe(SyncTwinAsync(lastElement));
    }

    /// <summary>Gets the root's store.</summary>
    public ComboboxStore<TValue> Store => store ?? throw new InvalidOperationException($"Blazix: `{services.Family}List` is used before its first render.");

    /// <summary>Gets the composite context the items register in.</summary>
    public CompositeRootContext Composite => composite.Context;

    /// <summary>Gets a value indicating whether the filtered list is empty.</summary>
    public bool Empty => Store.State.FilteredCount == 0;

    /// <summary>Gets the id the list renders.</summary>
    public string? Id => id;

    /// <summary>Gets a task that completes once the list's handles have attached.</summary>
    public Task TwinAttached => composite.TwinAttached;

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        openSubscription?.Dispose();
        composite.ItemsChanged -= OnItemsChanged;
        composite.Dispose();
        if (store is not null && id is not null)
        {
            string own = id;
            store.SetElementIds(ids => ids.List == own ? ids with { List = null } : ids);
        }

        if (twin is not null)
        {
            await twin.DisposeAsync();
        }
    }

    /// <summary>Reads the root and the portal and subscribes the part.</summary>
    /// <param name="cascaded">The cascaded store, or <see langword="null"/> outside a root.</param>
    /// <param name="portalContext">The cascaded portal context, or <see langword="null"/>.</param>
    /// <param name="subscribe">The part's subscription factory over the store.</param>
    public void Subscribe(ComboboxStore? cascaded, PopupPortalContext? portalContext, Func<ComboboxStore, SelectorSubscription<ComboboxStoreState>> subscribe)
    {
        store = ComboboxParts.RequireRoot<TValue>(cascaded, services.Family, "List");
        portal = portalContext;
        SelectorSubscription<ComboboxStoreState> subscription = subscribe(store);

        // Decision 11 (CMB-API-55, EDGE-10): the membership, not the query, renders the list's items.
        subscription.Select(ComboboxStoreState.Membership);
        subscription.Select(ComboboxStoreState.ListEmpty);
        subscription.Select(static state => state.SelectionMode);
        subscription.Select(static state => state.ReadOnly);
        subscription.Select(static state => state.Grid);

        // The open state reaches the list's sync.Handle with no render of the list (decision 11: its items render only on a
        // membership change).
        openSubscription = new SelectorSubscription<ComboboxStoreState>(store, onOpenChanged);
        openSubscription.Select(ComboboxStoreState.Open);
        composite.SetList(store.ListNavigation, CompositeListRole.Floating);
        composite.ReportItems = !store.State.HasItems;
        twin = new ComboboxTwin(services.JSRuntime);
        ComboboxStore<TValue> root = store;
        twin.ItemLinkPressed += root.SetLinkPress;
    }

    /// <summary>Applies the id the part renders and the consumer's attributes.</summary>
    /// <param name="consumerId">The consumer's id, or <see langword="null"/> for the floating id.</param>
    /// <param name="consumerAttributes">The consumer's attributes.</param>
    public void Apply(string? consumerId, IReadOnlyDictionary<string, object>? consumerAttributes)
    {
        ComboboxStore<TValue> root = Store;

        // CMB-DOM-08 (ComboboxList.tsx:94): the floating id, which the input's aria-controls names.
        id = consumerId ?? root.Lifecycle.FloatingId;
        string? own = id;
        root.SetElementIds(ids => ids with { List = own });
        composite.ReportItems = !root.State.HasItems;
        composite.SetConsumerAttributes(consumerAttributes);
    }

    /// <summary>Adds the list's attributes.</summary>
    /// <param name="attributes">The part's attributes.</param>
    public void AddPartAttributes(AttributeList attributes)
    {
        ComboboxStoreState state = Store.State;

        // CMB-DOM-08, ARIA-08 (ComboboxList.tsx:94-111): no aria-orientation, aria-readonly only as a listbox.
        attributes.Set("tabindex", "-1");
        attributes.Set("id", id);
        attributes.Set("role", state.Grid ? "grid" : "listbox");
        if (state.SelectionMode == ComboboxSelectionMode.Multiple)
        {
            attributes.Set("aria-multiselectable", "true");
        }

        if (state.ReadOnly && !state.Grid)
        {
            attributes.Set("aria-readonly", "true");
        }

        composite.AddAttributes(attributes, id);
    }

    /// <summary>Determines whether the consumer's handler for an event renders on the element.</summary>
    /// <param name="eventName">The event's attribute name.</param>
    /// <returns><see langword="false"/> for a handler the part declines and runs itself.</returns>
    public bool ForwardsConsumerHandler(string eventName) => composite.ForwardsConsumerHandler(eventName);

    /// <summary>Wraps the list's content in the composite context the items read.</summary>
    /// <param name="content">The list's content.</param>
    /// <returns>The content inside the cascade, cached while the content is the same.</returns>
    public RenderFragment Cascade(RenderFragment? content)
    {
        if (cascade is null || !ReferenceEquals(cascadedContent, content))
        {
            cascadedContent = content;
            cascade = composite.Context.Cascade(content);
        }

        return cascade;
    }

    // The implicit Collection (ComboboxList.tsx:52-61): one template call per filtered item or group, keyed by position. Here
    // rather than in the part, so Autocomplete's List shell renders the same (review B1 of the final review).
    /// <summary>Renders the group template once per filtered group, else the item template once per filtered item.</summary>
    /// <param name="builder">The part's render tree builder.</param>
    /// <param name="itemContent">The part's <c>ItemContent</c>.</param>
    /// <param name="groupContent">The part's <c>GroupContent</c>.</param>
    public void BuildTemplate(RenderTreeBuilder builder, RenderFragment<ComboboxListItem<TValue>>? itemContent, RenderFragment<ComboboxItemGroup<TValue>>? groupContent)
    {
        ComboboxStore<TValue> root = Store;
        if (groupContent is not null && root.FilteredGroups is { } groups)
        {
            for (int i = 0; i < groups.Count; i++)
            {
                builder.AddContent(0, groupContent(groups[i]));
            }

            return;
        }

        if (itemContent is not null)
        {
            IReadOnlyList<TValue> values = root.FilteredValues;
            for (int i = 0; i < values.Count; i++)
            {
                builder.AddContent(1, itemContent(new ComboboxListItem<TValue>(values[i], i)));
            }
        }
    }

    /// <summary>Attaches the handles after the portal moved, and sends their changed options.</summary>
    /// <param name="element">The list's element.</param>
    /// <returns>A task that completes when the handles have their options.</returns>
    public async Task AfterRenderAsync(ElementReference? element)
    {
        if (disposed)
        {
            return;
        }

        // A portaled part's sync.Handle attaches once the portal has moved its node (M4 track 1's design point 3): in place the
        // node is `display: none`, where every item is invisible.
        if (!composite.TwinAttached.IsCompleted)
        {
            await PopupPortalContext.WhenMoved(portal);
        }

        Task compositeSync = composite.AfterRenderAsync(true, element, id);
        lastElement = element;
        Task twinSync = SyncTwinAsync(element);
        await Task.WhenAll(compositeSync, twinSync);
    }

    private async Task SyncTwinAsync(ElementReference? element)
    {
        if (twin is null || element is not { } target)
        {
            return;
        }

        ComboboxStoreState state = Store.State;
        ComboboxOptions options = new()
        {
            Part = ComboboxTwin.ListPart,
            Inline = state.Inline,
            Disabled = state.Disabled,
            ReadOnly = state.ReadOnly,
            Virtualized = state.Virtualized,
            InsidePopup = state.InputInsidePopup,
            Open = state.Lifecycle.EffectiveOpen,
        };
        await sync.SyncAsync(twin, target, options);
    }

    // CMB-STA-25 (ComboboxItem.tsx:92-121): without Items the rendered items' values, in the reported order, are the registry
    // the selected index and the highlight read.
    private void OnItemsChanged()
    {
        if (store is null)
        {
            return;
        }

        IReadOnlyList<CompositeItemBehavior> ordered = composite.OrderedItems;
        ItemSlot<TValue>[] slots = new ItemSlot<TValue>[ordered.Count];
        for (int i = 0; i < slots.Length; i++)
        {
            slots[i] = store.RenderedValueOf(ordered[i]);
        }

        store.SetRenderedValues(slots, ordered);
    }

    private async Task Observe(Task task)
    {
        try
        {
            await task;
        }
        catch (Exception exception) when (!task.IsCanceled)
        {
            await services.DispatchException(exception);
        }
    }
}

// combobox/collection/ComboboxCollection.tsx (CMB §3.20, STR-06): maps the enclosing Group's items, else the root's filtered
// items, subscribed to the membership only (decision 11; CMB-API-55, EDGE-10: an unchanged membership renders no item).
/// <summary>The behavior of a combobox-family collection, and of a list's item template.</summary>
/// <typeparam name="TValue">The type of the items' values.</typeparam>
internal sealed class ComboboxCollectionBehavior<TValue> : IDisposable
{
    private readonly string family;

    private readonly Action requestRender;

    private ComboboxStore<TValue>? store;

    private ComboboxGroupContext<TValue>? group;

    private SelectorSubscription<ComboboxStoreState>? subscription;

    /// <summary>Initializes a new instance of the <see cref="ComboboxCollectionBehavior{TValue}"/> class.</summary>
    /// <param name="family">The family whose parts the diagnostics name.</param>
    /// <param name="requestRender">Asks the part to render.</param>
    public ComboboxCollectionBehavior(string family, Action requestRender)
    {
        this.family = family;
        this.requestRender = requestRender;
    }

    /// <summary>Gets the items the collection maps, or <see langword="null"/> for none.</summary>
    /// <remarks>A group without items maps nothing.</remarks>
    public IReadOnlyList<TValue>? Items => group is not null ? group.Items : store?.FilteredValues;

    /// <summary>Gets the root's filtered groups, for a list's group template.</summary>
    public IReadOnlyList<ComboboxItemGroup<TValue>>? Groups => store?.FilteredGroups;

    /// <inheritdoc />
    public void Dispose()
    {
        subscription?.Dispose();
        subscription = null;
    }

    /// <summary>Reads the root and the enclosing group and subscribes to the membership.</summary>
    /// <param name="cascaded">The cascaded store, or <see langword="null"/> outside a root.</param>
    /// <param name="groupContext">The cascaded group context, or <see langword="null"/>.</param>
    public void Subscribe(ComboboxStore? cascaded, ComboboxGroupContext<TValue>? groupContext)
    {
        store = ComboboxParts.RequireRoot<TValue>(cascaded, family, "Collection");
        group = groupContext;
        subscription = new SelectorSubscription<ComboboxStoreState>(store, requestRender);
        subscription.Select(ComboboxStoreState.Membership);
    }

    // ComboboxCollection.tsx: one template call per item, keyed by position. Here rather than in the part, so Autocomplete's
    // Collection shell renders the same (review B1 of the final review).
    /// <summary>Renders the item template once per item of the group, else of the filtered items.</summary>
    /// <param name="builder">The part's render tree builder.</param>
    /// <param name="itemContent">The part's <c>ItemContent</c>.</param>
    public void BuildItems(RenderTreeBuilder builder, RenderFragment<ComboboxListItem<TValue>>? itemContent)
    {
        if (itemContent is null || Items is not { } items)
        {
            return;
        }

        for (int i = 0; i < items.Count; i++)
        {
            builder.AddContent(0, itemContent(new ComboboxListItem<TValue>(items[i], i)));
        }
    }
}

// combobox/group/ComboboxGroup.tsx (CMB §3.21): role group (rowgroup in a grid), aria-labelledby its label's id (a
// LabelIdSlot), and its items for a Collection inside.
/// <summary>The behavior of a combobox-family group.</summary>
/// <typeparam name="TValue">The type of the items' values.</typeparam>
internal sealed class ComboboxGroupBehavior<TValue> : IDisposable
{
    private readonly ComboboxPartServices services;

    private readonly ComboboxGroupContext<TValue> context;

    private ComboboxStore? store;

    private RenderFragment? cascadedContent;

    private RenderFragment? cascade;

    /// <summary>Initializes a new instance of the <see cref="ComboboxGroupBehavior{TValue}"/> class.</summary>
    /// <param name="services">The part's services.</param>
    public ComboboxGroupBehavior(ComboboxPartServices services)
    {
        this.services = services;
        context = new ComboboxGroupContext<TValue>(services.RequestRender);
    }

    /// <summary>Gets the group context the label and a collection read.</summary>
    public ComboboxGroupContext<TValue> Context => context;

    /// <inheritdoc />
    public void Dispose()
    {
    }

    /// <summary>Reads the root and subscribes the part.</summary>
    /// <param name="cascaded">The cascaded store, or <see langword="null"/> outside a root.</param>
    /// <param name="subscribe">The part's subscription factory over the store.</param>
    public void Subscribe(ComboboxStore? cascaded, Func<ComboboxStore, SelectorSubscription<ComboboxStoreState>> subscribe)
    {
        store = ComboboxParts.RequireRoot(cascaded, services.Family);
        subscribe(store).Select(static state => state.Grid);
    }

    /// <summary>Applies the group's items.</summary>
    /// <param name="items">The part's <c>Items</c>, or <see langword="null"/>.</param>
    public void Apply(IReadOnlyList<TValue>? items) => context.Items = items;

    /// <summary>Adds the group's attributes.</summary>
    /// <param name="attributes">The part's attributes.</param>
    public void AddPartAttributes(AttributeList attributes)
    {
        attributes.Set("role", store!.State.Grid ? "rowgroup" : "group");
        if (context.LabelId.Id is { } labelId)
        {
            attributes.Set("aria-labelledby", labelId);
        }
    }

    /// <summary>Wraps the group's content in the group context.</summary>
    /// <param name="content">The group's content.</param>
    /// <returns>The content inside the cascade, cached while the content is the same.</returns>
    public RenderFragment Cascade(RenderFragment? content)
    {
        if (cascade is null || !ReferenceEquals(cascadedContent, content))
        {
            cascadedContent = content;
            RenderFragment? inner = content;
            ComboboxGroupContext<TValue> value = context;
            cascade = builder =>
            {
                builder.OpenComponent<CascadingValue<ComboboxGroupContext<TValue>>>(0);
                builder.AddComponentParameter(1, nameof(CascadingValue<ComboboxGroupContext<TValue>>.Value), value);
                builder.AddComponentParameter(2, nameof(CascadingValue<ComboboxGroupContext<TValue>>.IsFixed), true);
                builder.AddComponentParameter(3, nameof(CascadingValue<ComboboxGroupContext<TValue>>.ChildContent), inner);
                builder.CloseComponent();
            };
        }

        return cascade;
    }
}

// combobox/group-label/ComboboxGroupLabel.tsx (CMB §3.22): aria-hidden, its id written into the group's slot.
/// <summary>The behavior of a combobox-family group label.</summary>
internal sealed class ComboboxGroupLabelBehavior : IDisposable
{
    private readonly string family;

    private IComboboxGroupContext? group;

    private string? id;

    /// <summary>Initializes a new instance of the <see cref="ComboboxGroupLabelBehavior"/> class.</summary>
    /// <param name="family">The family whose parts the diagnostics name.</param>
    public ComboboxGroupLabelBehavior(string family)
    {
        this.family = family;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (group is not null && id is not null)
        {
            group.LabelId.Withdraw(id);
        }
    }

    /// <summary>Reads the enclosing group, throwing the missing-group error outside one.</summary>
    /// <param name="cascaded">The cascaded group context, or <see langword="null"/>.</param>
    public void Subscribe(IComboboxGroupContext? cascaded) => group = cascaded ?? throw ComboboxParts.MissingGroup(family);

    /// <summary>Writes the label's id into the group's slot.</summary>
    /// <param name="labelId">The id the label renders.</param>
    public void Apply(string? labelId)
    {
        if (id is not null && !string.Equals(id, labelId, StringComparison.Ordinal))
        {
            group!.LabelId.Withdraw(id);
        }

        id = labelId;
        if (labelId is not null)
        {
            group!.LabelId.Publish(labelId);
        }
    }

    /// <summary>Adds the label's attributes.</summary>
    /// <param name="attributes">The part's attributes.</param>
    public void AddPartAttributes(AttributeList attributes)
    {
        attributes.Set("id", id);
        attributes.Set("aria-hidden", "true");
    }
}

// combobox/item/ComboboxItem.tsx (CMB §3.24): role option (gridcell), a stable generated id (the {rootId}-{index} deviation
// of ADR 04 Amendments 2026-11-03 (2)), no tabindex (SetRegistration(…, omitTabIndex: true)), aria-selected, aria-disabled;
// ButtonBehavior non-native, composite, focusable when disabled; the `item-press` request after the consumer's onclick, which
// PreventPartHandler() skips (CMB-API-60), reached by a press and the module's synthetic click (issue 146's deviation).
/// <summary>The behavior of a combobox-family item.</summary>
/// <typeparam name="TValue">The type of the item's value.</typeparam>
internal sealed class ComboboxItemBehavior<TValue> : IAsyncDisposable
{
    private const string ClickEvent = "onclick";

    private readonly ComboboxPartServices services;

    private readonly ButtonBehavior button;

    private readonly FieldConsumerHandlers clickHandlers = new(ClickEvent);

    private readonly EventCallback onKeyDown;

    private readonly ComboboxItemContext context;

    private CompositeItemBehavior? item;

    private ComboboxStore<TValue>? store;

    private StoreSelection<ComboboxStoreState, (ComboboxItemBehavior<TValue> Owner, TValue Value, bool NullItem), bool>? selected;

    private RenderFragment? cascadedContent;

    private RenderFragment? cascade;

    private TValue value = default!;

    private bool valueSupplied;

    private bool disabledParameter;

    private bool isRow;

    private string? id;

    private bool disposed;

    /// <summary>Initializes a new instance of the <see cref="ComboboxItemBehavior{TValue}"/> class.</summary>
    /// <param name="services">The part's services.</param>
    /// <param name="developmentLog">The button mechanism's development log.</param>
    /// <param name="onKeyDown">The part's own <c>onkeydown</c> handler.</param>
    /// <param name="onKeyUp">The part's own <c>onkeyup</c> handler.</param>
    public ComboboxItemBehavior(ComboboxPartServices services, ButtonDevelopmentLog developmentLog, EventCallback onKeyDown, EventCallback onKeyUp)
    {
        this.services = services;
        this.onKeyDown = onKeyDown;
        button = new ButtonBehavior(services.JSRuntime, services.Logger, developmentLog, services.Ids, services.RequestRender, services.DispatchException, onKeyDown, onKeyUp);
        context = new ComboboxItemContext();
    }

    // Base UI's `null` item: no value, or a null one.
    private bool NullItem => !valueSupplied || ValueNull.IsNull(value);

    /// <summary>Gets the item context the indicator reads.</summary>
    public ComboboxItemContext Context => context;

    /// <summary>Gets a value indicating whether the item is selected.</summary>
    public bool Selected => selected?.Value ?? false;

    /// <summary>Gets a value indicating whether the item is disabled, by the part or the root.</summary>
    public bool Disabled => disabledParameter || (store?.State.Disabled ?? false);

    /// <summary>Gets the mirrored highlight.</summary>
    public bool Highlighted => item?.Highlighted ?? false;

    /// <summary>Gets a task that completes once the item's button handle has attached.</summary>
    public Task TwinAttached => button.TwinAttached;

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        if (disposed)
        {
            return ValueTask.CompletedTask;
        }

        disposed = true;
        if (item is not null)
        {
            store?.UnregisterRenderedValue(item);
            item.Dispose();
        }

        button.Dispose();
        return ValueTask.CompletedTask;
    }

    /// <summary>Reads the root and the list's composite context and subscribes the part.</summary>
    /// <param name="cascaded">The cascaded store, or <see langword="null"/> outside a root.</param>
    /// <param name="composite">The list's composite context, or <see langword="null"/>.</param>
    /// <param name="row">Whether the item is inside a row.</param>
    /// <param name="subscribe">The part's subscription factory over the store.</param>
    public void Subscribe(ComboboxStore? cascaded, CompositeRootContext? composite, bool row, Func<ComboboxStore, SelectorSubscription<ComboboxStoreState>> subscribe)
    {
        store = ComboboxParts.RequireRoot<TValue>(cascaded, services.Family, "Item");
        isRow = row;
        item = new CompositeItemBehavior(composite, services.Family + "Item", onKeyDown, services.RequestRender);
        SelectorSubscription<ComboboxStoreState> subscription = subscribe(store);
        subscription.Select(static state => state.Disabled);
        subscription.Select(static state => state.SelectionMode);

        // Base UI's `isSelected` selector with the item's value as its argument: re-evaluated on a selection change and when
        // the value changes (the argument compares by value, so the owner alone would never re-evaluate).
        selected = subscription.Select(static (state, arg) => arg.Owner.EvaluateSelected(state.SelectionRevision, arg.Value, arg.NullItem), (Owner: this, Value: value, NullItem: NullItem));
    }

    /// <summary>Applies the part's parameters.</summary>
    /// <param name="itemValue">The part's <c>Value</c>.</param>
    /// <param name="valueSupplied">Whether <paramref name="itemValue"/> was supplied.</param>
    /// <param name="index">The part's <c>Index</c>.</param>
    /// <param name="disabled">The part's <c>Disabled</c>.</param>
    /// <param name="nativeButton">The part's <c>NativeButton</c>.</param>
    /// <param name="itemId">The id the part renders.</param>
    /// <param name="consumerAttributes">The consumer's attributes.</param>
    public void Apply(TValue itemValue, bool valueSupplied, int? index, bool disabled, bool nativeButton, string? itemId, IReadOnlyDictionary<string, object>? consumerAttributes)
    {
        ComboboxStore<TValue> root = store!;

        // ADR 08 Amendments 2026-11-03 (2): an omitted Value is Base UI's `null` (ComboboxItem.tsx:44), told by the
        // supplied flag, so a value-type root never reads it as default(TValue). A multiple root of a non-nullable value
        // type cannot hold it (the NavigationMenuItem precedent of Amendments 2026-11-04).
        if (!valueSupplied && !ComboboxStore<TValue>.ValueAcceptsNull && root.State.SelectionMode == ComboboxSelectionMode.Multiple)
        {
            throw new InvalidOperationException(
                $"Blazix: A `{services.Family}Item` without a `Value` stands for `null`, which a multiple `{services.Family}Root` of a non-nullable value type cannot hold. "
                + "Give the item a `Value`, or use a nullable `TValue` such as `int?`.");
        }

        bool valueChanged = !Equality.Is(value, itemValue) || this.valueSupplied != valueSupplied;
        value = itemValue;
        this.valueSupplied = valueSupplied;
        disabledParameter = disabled;
        id = itemId;
        if (valueChanged && selected is not null)
        {
            selected.Arg = (Owner: this, Value: itemValue, NullItem: NullItem);
        }

        button.Disabled = Disabled;
        button.Native = nativeButton;
        button.FocusableWhenDisabled = true;
        button.SetConsumerAttributes(consumerAttributes);
        clickHandlers.Set(consumerAttributes);
        item!.SetRegistration(itemId, consumerAttributes, Disabled, focusableWhenDisabled: true, omitTabIndex: true, label: root.LabelOf(itemValue), index: index);
        item.SetConsumerAttributes(Disabled ? null : consumerAttributes);
        root.RegisterRenderedValue(item, itemValue, valueSupplied || ComboboxStore<TValue>.ValueAcceptsNull);
        context.Set(Selected, index ?? -1);
    }

    /// <summary>Adds the item's attributes.</summary>
    /// <param name="attributes">The part's attributes.</param>
    public void AddPartAttributes(AttributeList attributes)
    {
        button.AddAttributes(attributes, id, composite: true);
        attributes.Set("id", id);
        attributes.Set("role", isRow ? "gridcell" : "option");
        if (store!.State.SelectionMode != ComboboxSelectionMode.None)
        {
            attributes.Set("aria-selected", Selected ? "true" : "false");
        }

        item!.AddAttributes(attributes);
    }

    /// <summary>Determines whether the consumer's handler for an event renders on the element.</summary>
    /// <param name="eventName">The event's attribute name.</param>
    /// <returns><see langword="false"/> for a handler the part declines and runs itself.</returns>
    public bool ForwardsConsumerHandler(string eventName) => button.ForwardsConsumerHandler(eventName) && item!.ForwardsConsumerHandler(eventName) && !clickHandlers.Declines(eventName);

    /// <summary>Runs the selection after the consumer's <c>onclick</c>, unless it called <c>PreventPartHandler()</c>.</summary>
    /// <param name="args">The click.</param>
    /// <returns>A task that completes when the selection has run.</returns>
    public async Task HandleClickAsync(MouseEventArgs args)
    {
        // CMB-EVT-12 (ComboboxItem.tsx:159-165): a disabled item's click never reaches here (the button behavior suppresses
        // it); readOnly runs the consumer's onclick and selects nothing. The part declines the consumer's onclick and runs it
        // here, after taking Enter's attribution and a link press, whose end reports can arrive while it yields on Server
        // (final review N1).
        ComboboxStore<TValue> root = store!;
        if (Disabled)
        {
            return;
        }

        ComboboxClickContext context = root.TakeClickContext();
        if (await clickHandlers.RunAsync(ClickEvent, args) || root.State.ReadOnly)
        {
            return;
        }

        await SelectAsync(root, EventSnapshot.FromEventArgs(args), context);
    }

    /// <summary>Forwards a key to the button behavior.</summary>
    /// <param name="args">The key event.</param>
    /// <returns>A task that completes when the handler has run.</returns>
    public Task HandleKeyDownAsync(KeyboardEventArgs args) => button.HandleKeyDownAsync(args);

    /// <summary>Forwards a key release to the button behavior.</summary>
    /// <param name="args">The key event.</param>
    /// <returns>A task that completes when the handler has run.</returns>
    public Task HandleKeyUpAsync(KeyboardEventArgs args) => button.HandleKeyUpAsync(args);

    /// <summary>Wraps the item's content in the item context the indicator reads.</summary>
    /// <param name="content">The item's content.</param>
    /// <returns>The content inside the cascade, cached while the content is the same.</returns>
    public RenderFragment Cascade(RenderFragment? content)
    {
        if (cascade is null || !ReferenceEquals(cascadedContent, content))
        {
            cascadedContent = content;
            RenderFragment? inner = content;
            ComboboxItemContext itemContext = context;
            cascade = builder =>
            {
                builder.OpenComponent<CascadingValue<ComboboxItemContext>>(0);
                builder.AddComponentParameter(1, nameof(CascadingValue<ComboboxItemContext>.Value), itemContext);
                builder.AddComponentParameter(2, nameof(CascadingValue<ComboboxItemContext>.IsFixed), true);
                builder.AddComponentParameter(3, nameof(CascadingValue<ComboboxItemContext>.ChildContent), inner);
                builder.CloseComponent();
            };
        }

        return cascade;
    }

    /// <summary>Attaches the button handle and sends the registration.</summary>
    /// <param name="element">The item's element.</param>
    /// <returns>A task that completes when the handle has its options.</returns>
    public async Task AfterRenderAsync(ElementReference? element)
    {
        if (disposed)
        {
            return;
        }

        context.Set(Selected, context.Index);
        await button.AfterRenderAsync(true, element, id, composite: true);
        await item!.AfterRenderAsync(element);
    }

    // ComboboxItem.tsx:150-161 (commitSelection): with submitOnItemClick the selection is flushed, then the form submitted,
    // whatever the selection did.
    private async Task SelectAsync(ComboboxStore<TValue> root, EventSnapshot @event, ComboboxClickContext context)
    {
        await root.SelectAsync(value, @event, present: !NullItem, context);
        if (root.Inputs.SubmitOnItemClick)
        {
            root.RequestSubmit();
        }
    }

    private bool EvaluateSelected(int revision, TValue candidate, bool nullItem)
    {
        _ = revision;
        return store is not null && store.State.SelectionMode != ComboboxSelectionMode.None && store.IsSelected(candidate, nullItem);
    }
}

// combobox/item-indicator/ComboboxItemIndicator.tsx (CMB §3.25) over ItemIndicatorBehavior (Utilities/, the
// TestItemIndicator composition). Combobox only.
/// <summary>The behavior of a combobox item indicator.</summary>
internal sealed class ComboboxItemIndicatorBehavior : IAsyncDisposable
{
    private readonly ComboboxPartServices services;

    private readonly ItemIndicatorBehavior indicator;

    private readonly Action changed;

    private ComboboxItemContext? item;

    private bool keepMounted;

    /// <summary>Initializes a new instance of the <see cref="ComboboxItemIndicatorBehavior"/> class.</summary>
    /// <param name="services">The part's services.</param>
    public ComboboxItemIndicatorBehavior(ComboboxPartServices services)
    {
        this.services = services;
        indicator = new ItemIndicatorBehavior(services.JSRuntime, action => services.InvokeAsync(() => { action(); return Task.CompletedTask; }), services.RequestRender);
        changed = OnItemChanged;
    }

    /// <summary>Gets a value indicating whether the item is selected.</summary>
    public bool Selected => item?.Selected ?? false;

    /// <summary>Gets a value indicating whether the indicator renders.</summary>
    public bool Shown => indicator.Shown;

    /// <summary>Gets the mirrored transition status.</summary>
    public TransitionStatus? TransitionStatus => indicator.TransitionStatus;

    /// <summary>Gets the indicator's default content, <c>✔️</c>.</summary>
    public static RenderFragment DefaultContent => ItemIndicatorBehavior.DefaultContent;

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        item?.RemoveListener(changed);
        indicator.Dispose();
        return ValueTask.CompletedTask;
    }

    /// <summary>Reads the enclosing item, throwing the missing-item error outside one.</summary>
    /// <param name="cascaded">The cascaded item context, or <see langword="null"/>.</param>
    public void Subscribe(ComboboxItemContext? cascaded)
    {
        item = cascaded ?? throw ComboboxParts.MissingItem(services.Family);
        item.AddListener(changed);
    }

    /// <summary>Applies the part's <c>KeepMounted</c>, at the first parameter set and later.</summary>
    /// <param name="mounted">Whether the indicator stays mounted while not selected.</param>
    /// <param name="first">Whether this is the first parameter set.</param>
    public void Apply(bool mounted, bool first)
    {
        keepMounted = mounted;
        if (first)
        {
            indicator.Initialize(Selected, mounted);
        }
        else
        {
            indicator.Update(Selected, mounted);
        }
    }

    /// <summary>Adds the indicator's attributes.</summary>
    /// <param name="attributes">The part's attributes.</param>
    public void AddPartAttributes(AttributeList attributes) => indicator.AddPartAttributes(attributes);

    /// <summary>Attaches the transitions handle.</summary>
    /// <param name="element">The indicator's element.</param>
    /// <returns>A task that completes when the handle has its options.</returns>
    public Task AfterRenderAsync(ElementReference? element) => indicator.AfterRenderAsync(element);

    private void OnItemChanged()
    {
        if (indicator.Update(Selected, keepMounted))
        {
            services.RequestRender();
        }
    }
}

// combobox/value/ComboboxValue.tsx (CMB §3.10, API-46/47): the selected value's label, or a template, or the placeholder.
// Combobox only (Autocomplete has its own Value). A null item's label in an item collection (ComboboxItems.Create, design
// point 2) is the label of no selection and wins over the placeholder.
// Base UI's Value renders again with its parent, so a label the consumer's accessor now computes differently, or that an
// external window no longer supplies, shows at once (CMB-STA-20, EDGE-12). A part without parameters does not render with
// the root here: after the root's parameters change, the part renders again only when the text it shows changed.
/// <summary>The behavior of a combobox value.</summary>
/// <typeparam name="TValue">The type of the items' values.</typeparam>
internal sealed class ComboboxValueBehavior<TValue> : IDisposable
{
    private readonly Action requestRender;

    private ComboboxStore<TValue>? store;

    private SelectorSubscription<ComboboxStoreState>? subscription;

    private string family = string.Empty;

    private bool showsText;

    private bool placeholderGiven;

    private string? shown;

    /// <summary>Initializes a new instance of the <see cref="ComboboxValueBehavior{TValue}"/> class.</summary>
    /// <param name="requestRender">Asks the part to render.</param>
    public ComboboxValueBehavior(Action requestRender)
    {
        this.requestRender = requestRender;
    }

    /// <summary>Gets the root's store.</summary>
    public ComboboxStore<TValue> Store => store ?? throw new InvalidOperationException($"Blazix: `{family}Value` is used before its first render.");

    /// <summary>Gets a value indicating whether a value is selected.</summary>
    public bool HasValue => Store.State.HasSelectedValue;

    /// <summary>Gets a value indicating whether the root keeps several values.</summary>
    public bool Multiple => Store.State.SelectionMode == ComboboxSelectionMode.Multiple;

    /// <summary>Gets a value indicating whether an item collection labels the <see langword="null"/> value.</summary>
    public bool HasNullItemLabel => Store.HasNullItemLabel;

    /// <summary>Gets the single selected value, or the default value when none is selected.</summary>
    public TValue Value => Store.State.HasSelectedValue ? Store.SelectedValue : default!;

    /// <summary>Gets the selected values, an empty list when none are selected.</summary>
    public IReadOnlyList<TValue> Values => Store.SelectedValues;

    /// <summary>Gets the selected value's label, or the labels joined with <c>", "</c> in multiple mode.</summary>
    public string Label
    {
        get
        {
            ComboboxStore<TValue> root = Store;
            if (root.State.SelectionMode != ComboboxSelectionMode.Multiple)
            {
                return root.State.HasSelectedValue ? root.LabelOf(root.SelectedValue) : root.HasNullItemLabel ? root.LabelOf(default) : string.Empty;
            }

            IReadOnlyList<TValue> values = root.SelectedValues;
            switch (values.Count)
            {
                case 0:
                    return string.Empty;
                case 1:
                    return root.LabelOf(values[0]);
                default:
                    string[] labels = new string[values.Count];
                    for (int i = 0; i < labels.Length; i++)
                    {
                        labels[i] = root.LabelOf(values[i]);
                    }

                    return string.Join(", ", labels);
            }
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        subscription?.Dispose();
        subscription = null;
        if (store is not null)
        {
            store.InputsApplied -= OnInputsApplied;
        }
    }

    /// <summary>Gets the text the part shows when it renders neither a template nor content: the label, or <see langword="null"/> for the placeholder.</summary>
    /// <param name="placeholderGiven">Whether the part has a placeholder.</param>
    /// <returns>The label, or <see langword="null"/> while nothing is selected, no item labels the null value and a placeholder is given.</returns>
    public string? ShowText(bool placeholderGiven)
    {
        showsText = true;
        this.placeholderGiven = placeholderGiven;
        shown = Text();
        return shown;
    }

    /// <summary>Records that the part renders a template or content, which render again with the root.</summary>
    public void ShowContent() => showsText = false;

    /// <summary>Reads the root and subscribes to the selection.</summary>
    /// <param name="cascaded">The cascaded store, or <see langword="null"/> outside a root.</param>
    /// <param name="family">The family whose parts the diagnostics name.</param>
    public void Subscribe(ComboboxStore? cascaded, string family)
    {
        this.family = family;
        store = ComboboxParts.RequireRoot<TValue>(cascaded, family, "Value");
        subscription = new SelectorSubscription<ComboboxStoreState>(store, requestRender);
        subscription.Select(ComboboxStoreState.Selection);
        store.InputsApplied += OnInputsApplied;
    }

    /// <summary>Throws when the template given is the other selection mode's.</summary>
    /// <param name="hasValueContent">Whether the single mode's template is given.</param>
    /// <param name="hasValuesContent">Whether the multiple mode's template is given.</param>
    /// <exception cref="InvalidOperationException">The template does not fit the root's selection mode.</exception>
    public void CheckTemplates(bool hasValueContent, bool hasValuesContent)
    {
        bool multiple = Multiple;
        if (multiple && hasValueContent)
        {
            throw new InvalidOperationException($"Blazix: `<{family}Value>` was given `ValueContent`, but its `<{family}Root>` is multiple. Use `ValuesContent`, which receives the selected values.");
        }

        if (!multiple && hasValuesContent)
        {
            throw new InvalidOperationException($"Blazix: `<{family}Value>` was given `ValuesContent`, but its `<{family}Root>` is not multiple. Use `ValueContent`, which receives the selected value.");
        }
    }

    private string? Text() => !HasValue && placeholderGiven && !HasNullItemLabel ? null : Label;

    private void OnInputsApplied()
    {
        if (showsText && !string.Equals(shown, Text(), StringComparison.Ordinal))
        {
            requestRender();
        }
    }
}

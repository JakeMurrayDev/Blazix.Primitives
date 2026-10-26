using Blazix.Primitives.Interop;
using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace Blazix.Primitives;

// The composite root as a part uses it (composite-and-list-navigation.md, CMP; ADR 09 Amendments 2026-10-22: the C# half
// of a mechanism lives in a folder named after it; composed into the part as ButtonBehavior is, never a nested component,
// ADR 16 rule 10). Base UI's CompositeRoot, useCompositeRoot and CompositeList are internal (CMP §3); RadioGroup is the
// first public root (M2), ToggleGroup, Toolbar and Tabs follow (M3).
//
// JavaScript owns the registry's order, the highlighted index and, from attach, every item's tabindex (ADR 01 decision 2,
// ADR 04 Amendments 2026-10-22). C# keeps the items by id (each item registers in the batch that creates it), sends one
// `update` per render batch that changed the id set, an item's consumer tabindex or flags, an item's element or the
// options (CMP-STA-10; Server: after paint plus a round trip, ADR 01's row "EVD-STA-07, CMP-STA-10, FFI registration"),
// and mirrors the module's reports: the highlighted index (CMP-EVT-01), focus leaving the root (RDG-FOC-04/05) and, when
// asked, the items' order as ids, joined to its registrations (CMP-EVT-03; M3 track 1, ADR 04 Amendments 2026-10-26). A batch
// that only removes items runs no item's after-render, so an item's removal makes the root render, unless it already
// rendered in that batch, and the root's after-render sends the change.
//
// Two items on one element (CMP-STR-04; ADR 04 Amendments 2026-10-26, chosen by the user): an outer item whose element is
// an inner item, as a Toolbar.Button substituting a Toggle. Base UI's outer item attaches its ref last and the last
// registration wins (CompositeItem.tsx:28-31). Blazor sets parameters parent before child, so the outer item registers
// first and the root sends the first registration per id: the inner item's registration, its flags and the tabindex
// the outer item put in its bag are never sent for that id, and a change to the inner item alone changes nothing sent.
// When the outer item is removed, the inner item's registration becomes the first. FindItem returns the first item too,
// so the key path runs the outer item's consumer onkeydown.
//
// The consumer's onkeydown (CMP-EVT-12): Base UI merges it before the root's handler, so a PreventPartHandler() there
// vetoes the key handling (EVD-EVT-26). The part declines it (ForwardsConsumerHandler, ADR 02 Amendments 2026-10-21) and
// runs it from its own handler; on WebAssembly the decision point `navigate` runs it inside the dispatch, and Blazor's
// later dispatch of that key skips it (the one-shot marker, ButtonBehavior's model). A key the module handles with
// stopEventPropagation never reaches Blazor's document-level dispatcher, so the marker is left only when Blazor will
// dispatch the key (a veto, or no stopping). On Server the decision falls back to `run` (ADR 01 relaxation table).
// Option (i) of ADR 04's note of 2026-10-22, chosen by the user 2026-10-23: a handled key that the module stops never reaches Blazor's
// dispatcher, so the root runs the focused item's consumer onkeydown and then its own consumer's, on one shared event as
// Base UI's bubbling does: inside `navigate` on WebAssembly, where a PreventPartHandler() in either stops the key, and from
// the module's handled-key report on Server, a round trip late. The report is asked for only while a handler exists.
/// <summary>
/// The composite root mechanism of one part: its <see cref="CompositeTwin"/>, the registry its items join through
/// <see cref="Context"/>, the reading direction, the consumer's <c>onkeydown</c> gate and the mirrors of the module's
/// reports.
/// </summary>
internal sealed class CompositeRootBehavior : IDisposable
{
    private static readonly IReadOnlyList<string> s_noModifierKeys = [];

    private readonly IJSRuntime jsRuntime;
    private readonly PartIdGenerator idGenerator;
    private readonly Action stateChanged;
    private readonly Func<Exception, Task> dispatchException;
    private readonly EventCallback onKeyDown;
    private readonly TaskCompletionSource attachedSource = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly List<CompositeItemBehavior> items = [];
    private readonly List<CompositeItemBehavior> orderedItems = [];
    private AttributeValue consumerKeyDown;
    private DecidedKey? decidedKeyDown;
    private DirectionProviderContext? directionContext;
    private CompositeTwin? twin;
    private TwinHandle<CompositeOptions>? handle;
    private CompositeOptions? sentOptions;
    private int itemsVersion;
    private int sentItemsVersion;
    private bool elementReplaced;
    private bool attaching;
    private bool renderPending;
    private bool disposed;
    private string? lateId;

    /// <summary>Occurs when the highlighted index the module reports changed, once per change.</summary>
    public event Action<int>? HighlightChanged;

    /// <summary>Occurs when the module reports that focus left the root, with the <c>focusout</c> event.</summary>
    public event Action<EventSnapshot>? FocusLeft;

    /// <summary>Occurs once per report of the items' order, after <see cref="OrderedItems"/> holds it.</summary>
    public event Action? ItemsChanged;

    /// <summary>Initializes a new instance of the <see cref="CompositeRootBehavior"/> class.</summary>
    /// <param name="jsRuntime">The runtime the twin imports its module through.</param>
    /// <param name="idGenerator">The source of the late id of a component root.</param>
    /// <param name="stateChanged">Renders the part again; called to render a late id, a new direction or a removal.</param>
    /// <param name="dispatchException">Routes an exception of a consumer handler that ran in a decision to the part.</param>
    /// <param name="onKeyDown">The part's own <c>onkeydown</c> handler, which calls <see cref="HandleKeyDownAsync"/>.</param>
    public CompositeRootBehavior(
        IJSRuntime jsRuntime,
        PartIdGenerator idGenerator,
        Action stateChanged,
        Func<Exception, Task> dispatchException,
        EventCallback onKeyDown)
    {
        this.jsRuntime = jsRuntime;
        this.idGenerator = idGenerator;
        this.stateChanged = stateChanged;
        this.dispatchException = dispatchException;
        this.onKeyDown = onKeyDown;
        Context = new CompositeRootContext(this);
    }

    /// <summary>Gets or sets the arrow keys that move the highlight.</summary>
    /// <value><c>horizontal</c>, <c>vertical</c> or <c>both</c>, the default.</value>
    public string Orientation { get; set; } = "both";

    /// <summary>Gets or sets a value indicating whether a step past either end wraps to the other end.</summary>
    /// <value><see langword="true"/>, the default, to wrap.</value>
    public bool LoopFocus { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether Home and End move the highlight to the first and last item.</summary>
    /// <value><see langword="true"/> to handle Home and End; the default is <see langword="false"/>.</value>
    public bool EnableHomeAndEndKeys { get; set; }

    /// <summary>Gets or sets a value indicating whether the propagation of a key the module handles is stopped.</summary>
    /// <value><see langword="true"/>, the default, to stop it.</value>
    public bool StopEventPropagation { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether moving the pointer over an item focuses it.</summary>
    /// <value><see langword="true"/> to focus a hovered item; the default is <see langword="false"/>.</value>
    public bool HighlightItemOnHover { get; set; }

    /// <summary>Gets or sets the modifier keys that do not block navigation.</summary>
    /// <value>A subset of <c>Shift</c>, <c>Control</c>, <c>Alt</c> and <c>Meta</c>; empty by default.</value>
    public IReadOnlyList<string> ModifierKeys { get; set; } = s_noModifierKeys;

    /// <summary>Gets or sets the indices of the items navigation skips.</summary>
    /// <value>The indices, or <see langword="null"/>, the default, to read each item's disabled state from the document.</value>
    public IReadOnlyList<int>? DisabledIndices { get; set; }

    /// <summary>Gets or sets a value indicating whether the indices navigation skips are derived from the items' flags.</summary>
    /// <value>
    /// <see langword="true"/> to skip the items that are disabled and not focusable when disabled, in place of
    /// <see cref="DisabledIndices"/>; the default is <see langword="false"/>.
    /// </value>
    public bool DeriveDisabledIndices { get; set; }

    /// <summary>Gets or sets a value indicating whether an item that an arrow key focuses is selected by clicking the input that follows it.</summary>
    /// <value><see langword="true"/> to select on keyboard navigation; the default is <see langword="false"/>.</value>
    public bool SelectOnNavigation { get; set; }

    /// <summary>Gets or sets a value indicating whether the module reports focus leaving the root.</summary>
    /// <value><see langword="true"/> to raise <see cref="FocusLeft"/>; the default is <see langword="false"/>.</value>
    public bool ReportFocusLeave { get; set; }

    /// <summary>Gets or sets a value indicating whether the module reports each change of the highlighted index.</summary>
    /// <value><see langword="true"/> to raise <see cref="HighlightChanged"/>; the default is <see langword="false"/>.</value>
    public bool ReportHighlight { get; set; }

    /// <summary>Gets or sets a value indicating whether the module reports the items' order, which <see cref="OrderedItems"/> exposes.</summary>
    /// <value><see langword="true"/> to raise <see cref="ItemsChanged"/>; the default is <see langword="false"/>.</value>
    public bool ReportItems { get; set; }

    /// <summary>Gets or sets a value indicating whether the highlight follows the item marked active.</summary>
    /// <value>
    /// <see langword="true"/> to move the highlight to the item that carries <see cref="CompositeAttributes.ActiveItem"/>
    /// whenever it changes while focus is outside the root and the item is not <c>aria-disabled="true"</c>; the default is
    /// <see langword="false"/>.
    /// </value>
    public bool FollowActiveItem { get; set; }

    /// <summary>Gets or sets the part's own chain of the navigation decision point, run before the consumer's <c>onkeydown</c>.</summary>
    /// <value>
    /// A function that returns <see cref="DecisionPolicy.Stop"/> to veto the key, or <see langword="null"/>. It runs only on
    /// WebAssembly, synchronously inside the dispatch, and must not await.
    /// </value>
    public Func<EventSnapshot, DecisionPolicy>? NavigationDecider { get; set; }

    /// <summary>Gets or sets the part's chain of the selection decision point, such as an item's consumer focus handler.</summary>
    /// <value>
    /// A function that receives the focus event of the item about to be selected and returns
    /// <see cref="DecisionPolicy.Stop"/> to veto the selection, or <see langword="null"/>. It runs only on WebAssembly,
    /// synchronously inside the dispatch, and must not await.
    /// </value>
    public Func<EventSnapshot, DecisionPolicy>? SelectionDecider { get; set; }

    /// <summary>Gets the registry the part cascades to its items.</summary>
    /// <value>The context, cascaded with <c>IsFixed</c> inside the root's element.</value>
    public CompositeRootContext Context { get; }

    /// <summary>Gets the reading direction the root sends.</summary>
    /// <value>The direction of the nearest <see cref="DirectionProvider"/>, or <see cref="TextDirection.Ltr"/> without one.</value>
    public TextDirection Direction => DirectionProviderContext.Read(directionContext);

    /// <summary>Gets the registered items in the order the module last reported.</summary>
    /// <value>
    /// The items whose ids the last report named, in index order, without an item removed since; empty until the first
    /// report, and always empty without <see cref="ReportItems"/>.
    /// </value>
    public IReadOnlyList<CompositeItemBehavior> OrderedItems => orderedItems;

    /// <summary>Gets the highlighted index the module last reported.</summary>
    /// <value>The index of the item that holds the tab stop; 0 until a report arrives.</value>
    public int HighlightedIndex { get; private set; }

    /// <summary>Gets the id the behavior drew for a root element that a component root rendered.</summary>
    /// <value>The late id, or <see langword="null"/> while none was needed.</value>
    public string? LateId => lateId;

    /// <summary>Gets a task that completes when the behavior is attached to the root element.</summary>
    /// <value>A task that completes after the first interactive render has attached the behavior.</value>
    public Task TwinAttached => attachedSource.Task;

    /// <inheritdoc />
    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        directionContext?.RemoveListener(stateChanged);
        directionContext = null;
        if (twin is not null)
        {
            twin.HighlightChanged -= OnHighlightChange;
            twin.FocusLeft -= OnFocusLeave;
            twin.KeyHandled -= OnKeyHandled;
            twin.ItemsChanged -= OnItemsChange;
            twin.NavigationDecider = null;
            twin.SelectionDecider = null;
            _ = twin.DisposeAsync().AsTask();
            twin = null;
            handle = null;
        }
    }

    /// <summary>Subscribes to the nearest direction provider, so a change of direction renders the part and is sent.</summary>
    /// <param name="context">The cascaded context, or <see langword="null"/> without a provider.</param>
    public void SetDirectionContext(DirectionProviderContext? context)
    {
        if (ReferenceEquals(context, directionContext) || disposed)
        {
            return;
        }

        directionContext?.RemoveListener(stateChanged);
        directionContext = context;
        directionContext?.AddListener(stateChanged);
    }

    /// <summary>Finds the consumer's key handler, which the part declines and runs itself.</summary>
    /// <param name="additionalAttributes">The part's unmatched attributes.</param>
    public void SetConsumerAttributes(IReadOnlyDictionary<string, object>? additionalAttributes)
    {
        consumerKeyDown = default;
        if (additionalAttributes is null)
        {
            return;
        }

        foreach (KeyValuePair<string, object> pair in additionalAttributes)
        {
            if (!IsKeyDown(pair.Key))
            {
                continue;
            }

            AttributeValue value = AttributeValue.FromObject(pair.Value);
            if (MergeProps.IsHandler(pair.Key, value))
            {
                consumerKeyDown = value;
            }
        }
    }

    /// <summary>Determines whether the consumer's handler for an event is rendered on the root element.</summary>
    /// <param name="eventName">The event's attribute name in lowercase.</param>
    /// <returns><see langword="false"/> for the key handler, which the part runs itself.</returns>
    public bool ForwardsConsumerHandler(string eventName) => !IsKeyDown(eventName);

    /// <summary>Adds the root's attributes to a render: only the part's key handler, and a late id when one was drawn.</summary>
    /// <param name="attributes">The part-supplied attributes.</param>
    /// <param name="consumerId">The part's id when the consumer supplied one or a generated one was drawn.</param>
    public void AddAttributes(AttributeList attributes, string? consumerId)
    {
        // The root rendered in this batch, so its after-render sends whatever the batch changed.
        renderPending = true;

        if (lateId is not null && consumerId is null)
        {
            attributes.Set("id", lateId);
        }

        if (consumerKeyDown.IsHandler)
        {
            attributes.Set("onkeydown", onKeyDown);
        }
    }

    /// <summary>Attaches the twin after the first render that showed the root element, then sends options that changed.</summary>
    /// <param name="enabled">Whether the part rendered its element.</param>
    /// <param name="element">The element, or <see langword="null"/> under a substitute callback or component root.</param>
    /// <param name="id">The part's id (the consumer's or the generated one), or <see langword="null"/>.</param>
    /// <returns>A task that completes when the module has the options.</returns>
    public async Task AfterRenderAsync(bool enabled, ElementReference? element, string? id)
    {
        renderPending = false;
        if (disposed)
        {
            return;
        }

        if (!enabled)
        {
            // Nothing is rendered: the element the module listened on is gone.
            if (handle is { } released)
            {
                handle = null;
                sentOptions = null;
                await released.DisposeAsync();
            }

            return;
        }

        if (handle is not null)
        {
            await FlushAsync();
            return;
        }

        if (attaching)
        {
            return;
        }

        string? elementId = null;
        if (element is null)
        {
            elementId = id ?? lateId;
            if (elementId is null)
            {
                // A substitute whose root is a component leaves no element reference: render an id, attach next time.
                lateId = idGenerator.Next();
                stateChanged();
                return;
            }
        }

        twin ??= CreateTwin();
        CompositeOptions options = BuildOptions();
        attaching = true;
        try
        {
            handle = elementId is null
                ? await twin.AttachAsync(element!.Value, options)
                : await twin.AttachAsync(elementId, options);
        }
        finally
        {
            attaching = false;
        }

        attachedSource.TrySetResult();

        // Registrations that arrived while the attach was in flight.
        await FlushAsync();
    }

    /// <summary>Runs the consumer's <c>onkeydown</c> handler from the part's own, unless a decision already ran it.</summary>
    /// <param name="e">The event arguments.</param>
    /// <returns>The consumer handler's task.</returns>
    public Task HandleKeyDownAsync(KeyboardEventArgs e)
    {
        // The decision already ran the consumer's handler for the event Blazor dispatches now.
        DecidedKey? pending = decidedKeyDown;
        decidedKeyDown = null;
        if (!consumerKeyDown.IsHandler || (pending is { } key && key == DecidedKey.From(e)))
        {
            return Task.CompletedTask;
        }

        return consumerKeyDown.InvokeAsync(e);
    }

    /// <summary>Adds an item to the registry.</summary>
    /// <param name="item">The item, whose registration is set.</param>
    internal void Register(CompositeItemBehavior item)
    {
        items.Add(item);
        if (IsFirstWithId(item, item.Registration.Id))
        {
            itemsVersion++;
        }
    }

    /// <summary>Removes an item from the registry; the root renders unless it already rendered in this batch.</summary>
    /// <param name="item">The item.</param>
    internal void Unregister(CompositeItemBehavior item)
    {
        // An item behind another registration of its id was never sent, so its removal changes nothing sent.
        bool sent = IsFirstWithId(item, item.Registration.Id);
        if (!items.Remove(item))
        {
            return;
        }

        orderedItems.Remove(item);
        if (!sent)
        {
            return;
        }

        itemsVersion++;
        if (!disposed && !renderPending && (handle is not null || attaching))
        {
            renderPending = true;
            stateChanged();
        }
    }

    /// <summary>Records that a registered item changed its id, its consumer <c>tabindex</c> or its flags.</summary>
    /// <param name="item">The item, whose registration is already the new one.</param>
    /// <param name="previousId">The id of the item's previous registration.</param>
    internal void RegistrationChanged(CompositeItemBehavior item, string previousId)
    {
        if (IsFirstWithId(item, previousId) || IsFirstWithId(item, item.Registration.Id))
        {
            itemsVersion++;
        }
    }

    /// <summary>Records that a registered item rendered a new element under the same id, which the module must resolve again.</summary>
    internal void ElementReplaced() => elementReplaced = true;

    /// <summary>Sends the options if they changed since the last send.</summary>
    /// <returns>A task that completes when the module has the options.</returns>
    internal Task FlushAsync()
    {
        if (handle is null || disposed)
        {
            return Task.CompletedTask;
        }

        CompositeOptions? next = NextOptions();
        return next is null ? Task.CompletedTask : handle.UpdateAsync(next).AsTask();
    }

    private static bool IsKeyDown(string name) => string.Equals(name, "onkeydown", StringComparison.OrdinalIgnoreCase);

    private static bool SameStrings(IReadOnlyList<string> sent, IReadOnlyList<string> current)
    {
        if (sent.Count != current.Count)
        {
            return false;
        }

        for (int i = 0; i < sent.Count; i++)
        {
            if (!string.Equals(sent[i], current[i], StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    private static bool SameIndices(IReadOnlyList<int>? sent, IReadOnlyList<int>? current)
    {
        if (sent is null || current is null)
        {
            return sent is null && current is null;
        }

        if (sent.Count != current.Count)
        {
            return false;
        }

        for (int i = 0; i < sent.Count; i++)
        {
            if (sent[i] != current[i])
            {
                return false;
            }
        }

        return true;
    }

    // Option (i), chosen by the user 2026-10-23: the report costs a Server message per handled key, so it is asked for only while the
    // root or an item has a consumer key handler.
    private bool ReportHandledKeys()
    {
        if (consumerKeyDown.IsHandler)
        {
            return true;
        }

        for (int i = 0; i < items.Count; i++)
        {
            if (items[i].HasKeyHandler)
            {
                return true;
            }
        }

        return false;
    }

    // Whether no item registered before this one carries the id, so the root sends this item's registration for it.
    private bool IsFirstWithId(CompositeItemBehavior item, string id)
    {
        for (int i = 0; i < items.Count; i++)
        {
            if (ReferenceEquals(items[i], item))
            {
                return true;
            }

            if (string.Equals(items[i].Registration.Id, id, StringComparison.Ordinal))
            {
                return false;
            }
        }

        return false;
    }

    private CompositeItemBehavior? FindItem(string? id)
    {
        if (string.IsNullOrEmpty(id))
        {
            return null;
        }

        for (int i = 0; i < items.Count; i++)
        {
            if (string.Equals(items[i].Registration.Id, id, StringComparison.Ordinal))
            {
                return items[i];
            }
        }

        return null;
    }

    private CompositeTwin CreateTwin()
    {
        CompositeTwin created = new(jsRuntime)
        {
            NavigationDecider = DecideNavigation,
            SelectionDecider = DecideSelection,
        };
        created.HighlightChanged += OnHighlightChange;
        created.FocusLeft += OnFocusLeave;
        created.KeyHandled += OnKeyHandled;
        created.ItemsChanged += OnItemsChange;
        return created;
    }

    // The lists are copied, so a part that mutates its list in place still sends the change. One registration per id, the
    // first (the outer item's).
    private CompositeOptions BuildOptions()
    {
        int count = 0;
        for (int i = 0; i < items.Count; i++)
        {
            if (IsFirstWithId(items[i], items[i].Registration.Id))
            {
                count++;
            }
        }

        CompositeItemRegistration[] registrations = new CompositeItemRegistration[count];
        int next = 0;
        for (int i = 0; i < items.Count; i++)
        {
            if (IsFirstWithId(items[i], items[i].Registration.Id))
            {
                registrations[next++] = items[i].Registration;
            }
        }

        CompositeOptions options = new()
        {
            Orientation = Orientation,
            LoopFocus = LoopFocus,
            EnableHomeAndEndKeys = EnableHomeAndEndKeys,
            StopEventPropagation = StopEventPropagation,
            HighlightItemOnHover = HighlightItemOnHover,
            ModifierKeys = ModifierKeys.Count == 0 ? s_noModifierKeys : [.. ModifierKeys],
            DisabledIndices = DisabledIndices is null ? null : [.. DisabledIndices],
            DeriveDisabledIndices = DeriveDisabledIndices,
            Direction = CompositeLiterals.ToLiteral(Direction),
            Items = registrations,
            SelectOnNavigation = SelectOnNavigation,
            ReportFocusLeave = ReportFocusLeave,
            ReportHighlight = ReportHighlight,
            ReportHandledKeys = ReportHandledKeys(),
            ReportItems = ReportItems,
            FollowActiveItem = FollowActiveItem,
        };
        sentOptions = options;
        sentItemsVersion = itemsVersion;
        elementReplaced = false;
        return options;
    }

    // Compares before constructing, so an unchanged render allocates no options record (ADR 16 rule 7).
    private CompositeOptions? NextOptions()
    {
        if (sentOptions is { } sent
            && !elementReplaced
            && sentItemsVersion == itemsVersion
            && string.Equals(sent.Orientation, Orientation, StringComparison.Ordinal)
            && sent.LoopFocus == LoopFocus
            && sent.EnableHomeAndEndKeys == EnableHomeAndEndKeys
            && sent.StopEventPropagation == StopEventPropagation
            && sent.HighlightItemOnHover == HighlightItemOnHover
            && SameStrings(sent.ModifierKeys, ModifierKeys)
            && SameIndices(sent.DisabledIndices, DisabledIndices)
            && sent.DeriveDisabledIndices == DeriveDisabledIndices
            && string.Equals(sent.Direction, CompositeLiterals.ToLiteral(Direction), StringComparison.Ordinal)
            && sent.SelectOnNavigation == SelectOnNavigation
            && sent.ReportFocusLeave == ReportFocusLeave
            && sent.ReportHighlight == ReportHighlight
            && sent.ReportHandledKeys == ReportHandledKeys()
            && sent.ReportItems == ReportItems
            && sent.FollowActiveItem == FollowActiveItem)
        {
            return null;
        }

        return BuildOptions();
    }

    // CMP-EVT-01: the mirror changes once per report of a new index.
    private void OnHighlightChange(int index)
    {
        if (index == HighlightedIndex)
        {
            return;
        }

        HighlightedIndex = index;
        HighlightChanged?.Invoke(index);
    }

    private void OnFocusLeave(EventSnapshot snapshot) => FocusLeft?.Invoke(snapshot);

    // CMP-EVT-03 (M3 track 1, E2; design points 1 and 3, option (a), settled by the orchestrator 2026-10-26): each report is the whole sequence,
    // joined when it arrives, before or after the handle exists. An id no registration carries is ignored (an item C# has
    // already removed, on Server); the first registration of an id is the one joined, as the one sent.
    private void OnItemsChange(IReadOnlyList<string> ids)
    {
        orderedItems.Clear();
        for (int i = 0; i < ids.Count; i++)
        {
            if (FindItem(ids[i]) is { } item)
            {
                orderedItems.Add(item);
            }
        }

        ItemsChanged?.Invoke();
    }

    // The decision point `navigate`, on WebAssembly only: the part's chain, then the focused item's and the root's consumer
    // key handlers on one event, run inside the dispatch, so a PreventPartHandler() in either stops the key handling
    // (EVD-EVT-26, CMP-EVT-12; the item's under option (i), chosen by the user 2026-10-23).
    private DecisionPolicy DecideNavigation(EventSnapshot snapshot)
    {
        if (NavigationDecider is { } chain && chain(snapshot) == DecisionPolicy.Stop)
        {
            return DecisionPolicy.Stop;
        }

        CompositeItemBehavior? item = FindItem(snapshot.Target?.Id);
        bool itemHandles = item is not null && item.HasKeyHandler;
        if (!itemHandles && !consumerKeyDown.IsHandler)
        {
            return DecisionPolicy.Run;
        }

        KeyboardEventArgs args = snapshot.ToKeyboardEventArgs();
        if (itemHandles)
        {
            RunConsumerHandler(item!.InvokeKeyHandler, args);
        }

        if (consumerKeyDown.IsHandler)
        {
            RunConsumerHandler(e => consumerKeyDown.InvokeAsync(e), args);
        }

        bool vetoed = args.PartHandlerPrevented();
        // Blazor dispatches the key after the module only when the module does not stop its propagation.
        if (vetoed || !(sentOptions?.StopEventPropagation ?? StopEventPropagation))
        {
            if (itemHandles)
            {
                item!.MarkDecided(args);
            }

            if (consumerKeyDown.IsHandler)
            {
                decidedKeyDown = DecidedKey.From(args);
            }
        }

        return vetoed ? DecisionPolicy.Stop : DecisionPolicy.Run;
    }

    // Option (i), chosen by the user 2026-10-23: the module handled and stopped a key without running the chain inside the dispatch
    // (Server). The focused item's and the root's consumer key handlers run now, in Base UI's order, and cannot veto.
    private void OnKeyHandled(EventSnapshot snapshot) => _ = RunHandledKeyAsync(snapshot);

    private async Task RunHandledKeyAsync(EventSnapshot snapshot)
    {
        KeyboardEventArgs args = snapshot.ToKeyboardEventArgs();
        try
        {
            if (FindItem(snapshot.Target?.Id) is { HasKeyHandler: true } item)
            {
                await item.InvokeKeyHandler(args);
            }

            if (consumerKeyDown.IsHandler)
            {
                await consumerKeyDown.InvokeAsync(args);
            }
        }
        catch (Exception exception)
        {
            await dispatchException(exception);
        }
    }

    // Only the synchronous part of a handler can veto; an asynchronous rest runs on (ADR 04 §3).
    private void RunConsumerHandler(Func<KeyboardEventArgs, Task> handler, KeyboardEventArgs args)
    {
        try
        {
            Task task = handler(args);
            if (!task.IsCompleted)
            {
                _ = ObserveAsync(task);
            }
            else if (task.IsFaulted)
            {
                _ = dispatchException(task.Exception!.GetBaseException());
            }
        }
        catch (Exception exception)
        {
            _ = dispatchException(exception);
        }
    }

    // The decision point `select`, on WebAssembly only: the part's chain (an item's consumer focus handler).
    private DecisionPolicy DecideSelection(EventSnapshot snapshot)
    {
        return SelectionDecider is { } chain ? chain(snapshot) : DecisionPolicy.Run;
    }

    private async Task ObserveAsync(Task task)
    {
        try
        {
            await task;
        }
        catch (Exception exception) when (!task.IsCanceled)
        {
            await dispatchException(exception);
        }
    }

    /// <summary>The key event a decision ran the consumer's handler for, matched against Blazor's dispatch of it.</summary>
    private readonly record struct DecidedKey(string Key, string Code, bool AltKey, bool CtrlKey, bool MetaKey, bool ShiftKey)
    {
        public static DecidedKey From(KeyboardEventArgs e) => new(e.Key, e.Code, e.AltKey, e.CtrlKey, e.MetaKey, e.ShiftKey);
    }
}

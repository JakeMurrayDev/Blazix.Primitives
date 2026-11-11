using System.Globalization;
using Blazix.Primitives.Interop;
using Blazix.Primitives.Store;
using Blazix.Primitives.Utilities;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace Blazix.Primitives;

// CMB §4 (combobox/store.ts; AriaCombobox.tsx's commands setOpen, setInputValue, setSelectedValue, setIndices,
// handleSelection, forceMount). ADR 05 Amendments 2026-11-03: the store derives from the open lifecycle; RunConsumerStepAsync
// carries setOpen's extras (AriaCombobox.tsx:759-852) and the relay follows the commit (FLT-EVT-02's note). It implements
// IFloatingNodeContext (M5 track 2's design point 5, settled by the orchestrator 2026-11-04), so the store's fixed cascade
// carries the floating node to a popup nested inside. The non-generic base is what every part that does not handle a value
// reads (the TabsRootContext / RadioGroup R7 precedent, ADR 08 Amendments 2026-10-26); CascadingValue<ComboboxStore<TValue>>
// supplies it by assignability.
/// <summary>The store of a combobox-family root, independent of the type of its values.</summary>
internal abstract class ComboboxStore : PopupLifecycleStore<ComboboxStoreState>, IFloatingNodeContext
{
    private int emptyParts;

    private bool inputFocusPending;

    /// <summary>Occurs when a part that the session's options read registered or left, such as an empty state.</summary>
    public event Action? SessionInputsChanged;

    /// <summary>Occurs when the committed input text changed, for the input's <c>value</c> writer.</summary>
    public event Action? InputValueCommitted;

    /// <summary>Occurs after a close by focus-out or an outside press while the input is inside the popup, for the field's touched state.</summary>
    public event Action? ClosedFromInsidePopup;

    /// <summary>Initializes a new instance of the <see cref="ComboboxStore"/> class.</summary>
    /// <param name="state">The initial state.</param>
    /// <param name="family">The family whose parts the diagnostics name.</param>
    /// <param name="sessionToken">The interactions session's token.</param>
    /// <param name="logger">The root's logger.</param>
    protected ComboboxStore(ComboboxStoreState state, string family, string sessionToken, ILogger? logger)
        : base(state, family, sessionToken, inert: false, logger)
    {
        Family = family;
    }

    /// <summary>Gets the family whose parts the diagnostics name: <c>Combobox</c> or <c>Autocomplete</c>.</summary>
    public string Family { get; }

    /// <summary>Gets the list navigation context the list's handles join.</summary>
    public abstract ListNavigationContext ListNavigation { get; }

    /// <summary>Gets the labels of the filtered items, for the closed trigger's typeahead.</summary>
    public abstract IReadOnlyList<string?> FilteredLabels { get; }

    /// <summary>Gets the typeahead the trigger runs on the closed list, or <see langword="null"/> while it is off.</summary>
    public abstract CompositeTypeaheadOptions? TriggerTypeahead { get; }

    /// <summary>Gets a value indicating whether pressing the input opens the popup.</summary>
    public abstract bool OpenOnInputClick { get; }

    /// <summary>Gets the engine's ARIA autocomplete mode.</summary>
    public abstract ComboboxAriaAutoComplete AriaAutoComplete { get; }

    /// <summary>Gets a value indicating whether the consumer owns the input text.</summary>
    public abstract bool InputControlled { get; }

    /// <summary>Gets when the engine highlights the first item by itself.</summary>
    public abstract ComboboxAutoHighlightMode AutoHighlight { get; }

    /// <summary>Gets the name the value is submitted under, the root's or its field's.</summary>
    public abstract string? Name { get; }

    /// <summary>Gets or sets the root's reaction to a focus loss of its input or trigger: touched, and a commit in onBlur mode.</summary>
    public Action? ControlBlurred { get; set; }

    /// <summary>Gets or sets the route of an exception of work the store starts outside a request, to the root.</summary>
    public Func<Exception, Task>? DispatchException { get; set; }

    /// <summary>Gets or sets the root's hidden validation input, whose form an item press of a root that submits on item click submits first.</summary>
    public ElementReference? HiddenInput { get; set; }

    /// <summary>Gets or sets a value indicating whether the list sits inside another popup, such as a Dialog's, that hides it on close.</summary>
    public bool ListInsideOtherPopup { get; set; }
    /// <summary>Gets the id of the form that owns the hidden input.</summary>
    public abstract string? Form { get; }

    /// <summary>Gets a value indicating whether a value is required.</summary>
    public bool Required => State.Required;

    /// <summary>Gets a value indicating whether an empty-state part is mounted.</summary>
    public bool EmptyMounted => emptyParts > 0;

    /// <summary>Gets the id of the trigger a press made the floating root's DOM reference, or <see langword="null"/>.</summary>
    public string? DomReferenceTriggerId { get; private set; }

    /// <summary>Runs the input's change: the text, then the clears, the open and the highlight it implies.</summary>
    /// <param name="text">The input's text after the change.</param>
    /// <param name="typed">Whether the change is typed input.</param>
    /// <param name="composing">Whether a composition is in progress, so the text is not committed yet.</param>
    /// <param name="event">The <c>input</c> or <c>compositionend</c> event.</param>
    /// <returns>A task that completes when the change and its follow-ups have run.</returns>
    public abstract Task HandleInputChangeAsync(string text, bool typed, bool composing, EventSnapshot @event);

    /// <summary>Records the keydown the next item press is attributed to, as Enter's synthetic click is, or ends the attribution.</summary>
    /// <param name="event">The keydown, or <see langword="null"/> once the synthetic click was dispatched.</param>
    public abstract void SetSelectionEvent(EventSnapshot? @event);

    /// <summary>Clears the selection and the input text, as the clear button and the closed Escape do.</summary>
    /// <param name="reason">The request's reason: <see cref="EventReason.ClearPress"/> or <see cref="EventReason.EscapeKey"/>.</param>
    /// <param name="event">The native event, or <see langword="null"/>.</param>
    /// <param name="shareDetails">
    /// <see langword="true"/> for one details object for the text and the value (the closed Escape); <see langword="false"/>
    /// for one each (the clear button), so canceling the text's change leaves the value's.
    /// </param>
    /// <returns>A task that completes with the value's details, whose <see cref="ChangeEventDetails.IsPropagationAllowed"/> the module reads.</returns>
    public abstract Task<ChangeEventDetails> ClearAsync(EventReason reason, EventSnapshot? @event, bool shareDetails = true);

    /// <summary>Takes the text a browser autofilled into the hidden input.</summary>
    /// <param name="text">The autofilled text.</param>
    /// <param name="event">The input's <c>change</c>.</param>
    /// <returns>A task that completes when the matching value was requested.</returns>
    public abstract Task HandleAutofillAsync(string text, EventSnapshot @event);

    /// <summary>Removes the last selected value, as Backspace in an empty input does.</summary>
    /// <param name="renderedChips">The number of rendered chips.</param>
    /// <param name="event">The native event.</param>
    /// <returns>A task that completes when the request has run.</returns>
    public abstract Task RemoveLastAsync(int renderedChips, EventSnapshot @event);

    /// <summary>Removes the selected value at <paramref name="index"/>, as a chip's remove button or key does.</summary>
    /// <param name="index">The chip's index.</param>
    /// <param name="reason">The request's reason.</param>
    /// <param name="event">The native event.</param>
    /// <returns>A task that completes with the request's details.</returns>
    public abstract Task<ChangeEventDetails?> RemoveAtAsync(int index, EventReason reason, EventSnapshot @event);

    /// <summary>Selects the item the closed trigger's typeahead matched, as Base UI's <c>onMatch</c> does.</summary>
    /// <param name="index">The matched index.</param>
    /// <param name="event">The keydown that matched.</param>
    /// <returns>A task that completes when the selection and its callbacks have run.</returns>
    public abstract Task SelectTypeaheadMatchAsync(int index, EventSnapshot @event);

    /// <summary>Takes the highlight the list handle reported.</summary>
    /// <param name="index">The highlighted index, or <see langword="null"/> for none.</param>
    /// <param name="event">The event that moved the highlight, or <see langword="null"/>.</param>
    /// <returns>A task that completes when the highlight callback has run.</returns>
    public abstract Task OnHighlightReportedAsync(int? index, EventSnapshot? @event);

    /// <summary>Clears the highlight, as typing and the clear button do.</summary>
    /// <param name="event">The event that cleared it.</param>
    /// <returns>A task that completes when the request and the callback have run.</returns>
    public abstract Task ClearHighlightAsync(EventSnapshot? @event);

    /// <summary>Clears the text, the selection and the highlight for a press of the clear button.</summary>
    /// <param name="event">The click.</param>
    /// <returns>A task that completes when the clear and its callbacks have run.</returns>
    public abstract Task ClearByPressAsync(EventSnapshot @event);

    /// <summary>Clears an inline list's highlight as its input loses the focus, and restores it as the input regains the focus within the same open cycle.</summary>
    /// <param name="focused">Whether the input received or lost the focus.</param>
    /// <returns>A task that completes when the highlight was handled.</returns>
    public abstract Task OnInlineInputFocusAsync(bool focused);

    /// <summary>Asks for the input text to change, as typing, a clear or a fill does.</summary>
    /// <param name="text">The requested text.</param>
    /// <param name="details">The request's details, which the consumer can cancel.</param>
    /// <param name="typed">Whether the change is typed input rather than autofill-like or programmatic.</param>
    /// <returns>A task that completes with <see langword="true"/> when the text was committed and its follow-ups ran.</returns>
    public abstract Task<bool> SetInputValueAsync(string text, ChangeEventDetails details, bool typed = false);

    /// <summary>Records the side the positioner settled on.</summary>
    /// <param name="side">The side, or <see langword="null"/> when no positioner renders.</param>
    public void SetPopupSide(Side? side)
    {
        if (State.PopupSide != side)
        {
            Write(State with { PopupSide = side });
        }
    }

    /// <summary>Records the rendered id of a part the modules or the dismissal read.</summary>
    /// <param name="update">Returns the ids with the part's id written.</param>
    public void SetElementIds(Func<ComboboxElementIds, ComboboxElementIds> update)
    {
        ArgumentNullException.ThrowIfNull(update);
        ComboboxElementIds next = update(State.ElementIds);
        if (next != State.ElementIds)
        {
            Write(State with { ElementIds = next });
            SessionInputsChanged?.Invoke();
        }
    }

    /// <summary>Records where the input renders.</summary>
    /// <param name="insidePopup">Whether the input has a positioner ancestor, or the root is inline.</param>
    public abstract void SetInputInsidePopup(bool insidePopup);

    /// <summary>Registers a mounted empty-state part.</summary>
    /// <returns>A registration whose disposal records the part's removal.</returns>
    public IDisposable RegisterEmpty()
    {
        emptyParts++;
        Write(State with { EmptyMounted = true });
        SessionInputsChanged?.Invoke();
        return new EmptyRegistration(this);
    }

    /// <summary>Mounts the list while closed, for typeahead or autofill.</summary>
    public abstract void ForceMount();

    // ComboboxChip.tsx: a chip's ArrowDown or ArrowUp opens the popup and focuses the input, which a conditional input mounts
    // with the open first; the input takes the focus as its handle attaches.
    /// <summary>Asks the next input that mounts to take the focus.</summary>
    public void RequestInputFocus() => inputFocusPending = true;

    /// <summary>Returns whether an input focus was asked for, and clears the request.</summary>
    /// <returns><see langword="true"/> once after <see cref="RequestInputFocus"/>.</returns>
    public bool TakeInputFocus()
    {
        bool pending = inputFocusPending;
        inputFocusPending = false;
        return pending;
    }

    // ComboboxTrigger.tsx:196-199: with the input outside the popup, a trigger press makes the trigger the floating root's DOM
    // reference: its next press toggles the popup (useClick's active trigger) and a popup with no anchor follows it.
    /// <summary>Makes a trigger the floating root's DOM reference, which the interactions session reads as its active trigger.</summary>
    /// <param name="triggerId">The trigger's id.</param>
    public void SetDomReference(string triggerId)
    {
        if (!string.Equals(DomReferenceTriggerId, triggerId, StringComparison.Ordinal))
        {
            DomReferenceTriggerId = triggerId;
            NotifySessionInputsChanged();
        }
    }

    /// <summary>Raises <see cref="InputValueCommitted"/>.</summary>
    protected void NotifyInputValueCommitted() => InputValueCommitted?.Invoke();

    /// <summary>Raises <see cref="SessionInputsChanged"/>.</summary>
    protected void NotifySessionInputsChanged() => SessionInputsChanged?.Invoke();

    /// <summary>Raises <see cref="ClosedFromInsidePopup"/>.</summary>
    protected void NotifyClosedFromInsidePopup() => ClosedFromInsidePopup?.Invoke();

    private sealed class EmptyRegistration(ComboboxStore store) : IDisposable
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
            store.emptyParts--;
            if (store.emptyParts == 0)
            {
                store.Write(store.State with { EmptyMounted = false });
            }

            store.NotifySessionInputsChanged();
        }
}
}

// The value half (store.ts's selectedValue, items, isItemEqualToValue, itemToStringLabel; AriaCombobox.tsx's derivations):
// values never cross to JavaScript, only ids, indices and labels (Composite/CONTEXT.md). Items are kept in rendered order as
// ItemSlot<TValue> (UTL's equality over spans; never a HashSet<T>, the lesson of track 4 on the WebAssembly interpreter).
/// <summary>The store of a combobox-family root, with its values and items.</summary>
/// <typeparam name="TValue">The type of the items' values.</typeparam>
internal sealed class ComboboxStore<TValue> : ComboboxStore
{
    // AriaCombobox.tsx's INITIAL_LAST_HIGHLIGHT: nothing emitted yet.
    private static readonly bool s_nullable = default(TValue) is null || Nullable.GetUnderlyingType(typeof(TValue)) is not null;

    // The mounted items' labels: the module matches the labels the force-mounted list registers.
    private static readonly CompositeTypeaheadOptions s_renderedTypeahead = new();

    private readonly Controlled<TValue> valueState;

    private readonly Controlled<IReadOnlyList<TValue>> valuesState;

    private readonly Controlled<string> inputValueState;

    private ComboboxRootInputs<TValue> inputs = new();

    private ListNavigationContext? listNavigation;

    private ListNavigationBehavior? listBehavior;

    private Func<int?, Task>? requestHighlight;

    private CollatorFilter collator;

    private CultureInfo? collatorCulture;

    private bool collatorCreated;

    private bool singlePresent;

    private ComboboxFilteredItems<TValue> filtered = new([], null);

    private ItemSlot<TValue>[] filteredSlots = [];

    private string?[] filteredLabels = [];

    private CompositeTypeaheadOptions? derivedTypeahead;

    private readonly Dictionary<CompositeItemBehavior, TValue> itemValues = new(ReferenceEqualityComparer.Instance);

    private readonly Dictionary<CompositeItemBehavior, int> renderedPositions = new(ReferenceEqualityComparer.Instance);

    private ItemSlot<TValue>[] renderedValues = [];

    private string? closeQuery;

    private bool queryChangedAfterOpen;

    private string initialDefaultInputValue = string.Empty;

    private bool observing;

    private string observedQuery = string.Empty;

    private bool observedOpen;

    private string? derivedQuery;

    private DerivationKey? derivedKey;

    private bool derivedQueryChanged;

    private PendingHighlight? pendingHighlight;

    private HighlightDependencies? resolvedFor;

    private string? linkPress;

    private object? duplicatesReportedFor;

    private bool hadInputClear;

    private bool handlingEvent;

    private string? handlerQuery;

    private int? savedActiveIndex;

    private (string Text, ChangeEventDetails Details)? pendingAutofill;

    private TValue? observedValue;

    private bool observedPresent;

    private bool highlightDeferred;

    private (bool Emitted, TValue? Value, int Index) lastHighlight;

    private string syncedLabel = string.Empty;

    private bool labelSynced;

    private bool labelSyncPending;

    private EventSnapshot? selectionEvent;

    private IReadOnlyList<TValue>? lastItems;

    private IReadOnlyList<ComboboxItemGroup<TValue>>? lastGroups;

    private string lastSelectedLabel = string.Empty;

    private bool initialized;

    private Func<TValue, string>? collectionLabel;

    private bool inputNullControlled;

    /// <summary>Occurs when the selected value or values changed, for the hidden inputs and the field.</summary>
    public event Action? SelectionCommitted;

    /// <summary>Occurs after the root's parameters changed, for the parts that show a label the consumer's accessors compute.</summary>
    public event Action? InputsApplied;

    /// <summary>Initializes a new instance of the <see cref="ComboboxStore{TValue}"/> class.</summary>
    /// <param name="state">The initial state.</param>
    /// <param name="family">The family whose parts the diagnostics name.</param>
    /// <param name="sessionToken">The interactions session's token.</param>
    /// <param name="logger">The root's logger.</param>
    public ComboboxStore(ComboboxStoreState state, string family, string sessionToken, ILogger? logger)
        : base(state, family, sessionToken, logger)
    {
        // CMB-STA-18: the three controlled values under the name `Combobox` (UTL-STA-01..04).
        valueState = new Controlled<TValue>("Combobox", "selectedValue", logger);
        valuesState = new Controlled<IReadOnlyList<TValue>>("Combobox", "selectedValue", logger);
        inputValueState = new Controlled<string>("Combobox", "inputValue", logger);
    }

    private bool Single => State.SelectionMode == ComboboxSelectionMode.Single;

    private bool Multiple => State.SelectionMode == ComboboxSelectionMode.Multiple;

    // AriaCombobox.tsx:285 (hasFilteredItemsProp): filtered items in either shape.
    private bool HasFilteredItemsInput => inputs.FilteredItems is not null || inputs.FilteredItemGroups is not null;

    // AriaCombobox.tsx:231-239: with an item collection the filter reads the items' collection labels; otherwise the converter.
    private Func<TValue, string>? FilterItemToString =>
        inputs.Items is IComboboxItemLabels<TValue> || inputs.ItemGroups is IComboboxItemLabels<TValue> ? collectionLabel ??= value => LabelOf(value) : inputs.ItemToStringLabel;

    private bool HasInputValueParameter { get; set; }

    // The candidates are the filtered collection with items or filtered items given, else the rendered items.
    private bool UsesCollection => State.HasItems || inputs.FilteredItems is not null || inputs.FilteredItemGroups is not null;

    /// <inheritdoc />
    public override ListNavigationContext ListNavigation => listNavigation ?? throw new InvalidOperationException($"Blazix: the list navigation of `<{Family}Root>` is created at its first parameter set.");

    /// <inheritdoc />
    public override IReadOnlyList<string?> FilteredLabels => filteredLabels;

    // ComboboxTrigger.tsx:107-115 (CMB-KEY-12): enabled while single, not read-only and not disabled; the module checks the
    // closed state. With items the labels are the filtered values' (AriaCombobox.tsx:593-600, forceMount), else the mounted
    // items' own.
    /// <inheritdoc />
    public override CompositeTypeaheadOptions? TriggerTypeahead
    {
        get
        {
            if (!Single || State.ReadOnly || State.Disabled)
            {
                return null;
            }

            if (!UsesCollection)
            {
                return s_renderedTypeahead;
            }

            if (derivedTypeahead is null || !ReferenceEquals(derivedTypeahead.Labels, filteredLabels))
            {
                derivedTypeahead = new CompositeTypeaheadOptions { Labels = filteredLabels };
            }

            return derivedTypeahead;
        }
    }

    /// <inheritdoc />
    public override bool OpenOnInputClick => inputs.OpenOnInputClick;

    /// <inheritdoc />
    public override ComboboxAriaAutoComplete AriaAutoComplete => inputs.AriaAutoComplete;

    /// <inheritdoc />
    public override ComboboxAutoHighlightMode AutoHighlight => inputs.AutoHighlight;

    /// <inheritdoc />
    public override bool InputControlled => inputValueState.IsControlled;

    /// <inheritdoc />
    public override string? Name => NameResolver is { } resolve ? resolve(inputs.Name) : inputs.Name;

    /// <summary>Gets or sets the resolution of the submitted name from the root's own: the field's name wins.</summary>
    public Func<string?, string?>? NameResolver { get; set; }

    /// <inheritdoc />
    public override string? Form => inputs.Form;

    /// <summary>Gets the single-mode value.</summary>
    public Controlled<TValue> ValueState => valueState;

    /// <summary>Gets the multiple-mode values.</summary>
    public Controlled<IReadOnlyList<TValue>> ValuesState => valuesState;

    /// <summary>Gets the input text.</summary>
    public Controlled<string> InputValueState => inputValueState;

    /// <summary>Gets the filtered items, flat, in rendered order.</summary>
    public IReadOnlyList<TValue> FilteredValues => filtered.Flat;

    /// <summary>Gets the filtered groups, or <see langword="null"/> when the items are flat.</summary>
    public IReadOnlyList<ComboboxItemGroup<TValue>>? FilteredGroups => filtered.Groups;

    /// <summary>Gets a value indicating whether an item collection labels the <see langword="null"/> value, which then wins over a placeholder.</summary>
    public bool HasNullItemLabel => TryCollectionLabel(default!, out _);

    /// <summary>Gets the inputs of the root's latest parameter set.</summary>
    public ComboboxRootInputs<TValue> Inputs => inputs;

    /// <summary>Gets the single selected value; meaningful while <see cref="ComboboxStoreState.HasSelectedValue"/> in single mode.</summary>
    public TValue SelectedValue => valueState.Value;

    /// <summary>Gets the selected values in multiple mode, never <see langword="null"/>.</summary>
    public IReadOnlyList<TValue> SelectedValues => valuesState.Value ?? [];

    /// <summary>Gets the query the filter would use: the trimmed input text, or the frozen close query while closed.</summary>
    public string Query => !IsOpen && closeQuery is not null ? closeQuery : State.InputValue.Trim();

    /// <summary>Gets a value indicating whether a query was typed since the popup opened.</summary>
    public bool QueryChangedAfterOpen => queryChangedAfterOpen;

    /// <summary>Gets a value indicating whether the root was given an input value or a default input value at its first render.</summary>
    public bool HasInputValue => HasInputValueParameter;

    /// <summary>Gets a value indicating whether the values can be <see langword="null"/>: a reference or nullable value type.</summary>
    internal static bool ValueAcceptsNull => s_nullable;

    /// <inheritdoc />
    public override void SetSelectionEvent(EventSnapshot? @event) => selectionEvent = @event;

    /// <summary>Asks the input's handle to submit its form after this render, as an item press of a root that submits on item click does.</summary>
    public void RequestSubmit() => Write(State with { SubmitRevision = State.SubmitRevision + 1 });

    // Final review N1: an item takes these before its consumer's onclick, which may yield on Server while their end reports
    // arrive.
    /// <summary>Takes the keydown Enter's click is attributed to and the <c>href</c> of a link press, clearing both.</summary>
    /// <returns>The values the click's selection reads.</returns>
    public ComboboxClickContext TakeClickContext()
    {
        ComboboxClickContext taken = new(selectionEvent, linkPress);
        selectionEvent = null;
        linkPress = null;
        return taken;
    }

    /// <summary>Records the <c>href</c> of a press inside a link of an item, before its click, and clears it after.</summary>
    /// <param name="href">The link's <c>href</c>, or <see langword="null"/> once the click was dispatched.</param>
    public void SetLinkPress(string? href) => linkPress = href;

    /// <summary>Selects or toggles an item, as an item press does.</summary>
    /// <param name="value">The item's value.</param>
    /// <param name="event">The press, or the keydown that started it.</param>
    /// <param name="present"><see langword="false"/> for the null item, whose press selects nothing.</param>
    /// <param name="context">The values taken before the consumer's <c>onclick</c>, or <see langword="null"/> to take them now.</param>
    /// <returns>A task that completes when the selection and its follow-ups have run.</returns>
    public async Task SelectAsync(TValue value, EventSnapshot @event, bool present = true, ComboboxClickContext? context = null)
    {
        // AriaCombobox.tsx:880-950 (handleSelection): Enter's synthetic click is attributed to its keydown.
        ComboboxClickContext taken = context ?? TakeClickContext();
        EventSnapshot source = taken.SelectionEvent ?? @event;
        ChangeEventDetails details = CreateDetails(EventReason.ItemPress, source);

        // CMB-PTR-05 (AriaCombobox.tsx:885-892): a press inside an `<a href>` is the link's; a `#` link closes the popup.
        if (taken.LinkPress is { } href)
        {
            if (href.StartsWith('#'))
            {
                await RequestOpenAsync(false, ToOpenDetails(details));
            }

            return;
        }

        if (Multiple)
        {
            IReadOnlyList<TValue> current = SelectedValues;
            bool wasSelected = ItemEquality.Includes(current, value, inputs.IsItemEqualToValue);
            IReadOnlyList<TValue> next = wasSelected ? ItemEquality.RemoveItem(current, value, inputs.IsItemEqualToValue) : [.. current, value];
            if (!await SetSelectedValuesAsync(next, details))
            {
                return;
            }

            bool wasFiltering = State.InputValue.Trim().Length != 0;
            if (!wasFiltering)
            {
                return;
            }

            if (State.InputInsidePopup)
            {
                await SetInputValueAsync(string.Empty, CreateDetails(EventReason.InputClear, details.Event, isItemPress: true));
                if (pendingHighlight is { } pending && !wasSelected)
                {
                    pendingHighlight = pending with { ToggledValue = value, HasToggledValue = true };
                }
            }
            else
            {
                await RequestOpenAsync(false, ToOpenDetails(details));
            }

            return;
        }

        // AriaCombobox.tsx:940: setOpen reads the query the press's render had, not the label the selection filled since, so a
        // deferred controlled close keeps filtering by it while it exits (CMB-ANI-02).
        handlerQuery = Query;
        try
        {
            if (!await SetSelectedValueAsync(value, details, present))
            {
                return;
            }

            await RequestOpenAsync(false, ToOpenDetails(details));
        }
        finally
        {
            handlerQuery = null;
        }
    }

    /// <inheritdoc />
    public override async Task HandleInputChangeAsync(string text, bool typed, bool composing, EventSnapshot @event)
    {
        // Base UI's post-commit effects run after the whole handler, its highlight clear included (ComboboxInput.tsx:311-316).
        await RunHandlerAsync(() => HandleInputChangeCoreAsync(text, typed, composing, @event));
    }

    // ComboboxClear.tsx:100-128: the text and the value, each with its own details, then no highlight with the click, all in
    // the handler, whose post-commit effects follow it.
    /// <inheritdoc />
    public override Task ClearByPressAsync(EventSnapshot @event) => RunHandlerAsync(async () =>
    {
        await ClearAsync(EventReason.ClearPress, @event, shareDetails: false);
        await ClearHighlightAsync(@event);
    });

    // CMB-EVT-11 (AriaCombobox.tsx:1627-1689): multiple mode ignores the text; no-selection mode writes it; single mode
    // requests the first value whose serialized text or label matches it case-insensitively, with the reason none (a
    // canceled request leaves the field untouched, the field's reaction following the committed value only).
    /// <inheritdoc />
    public override async Task HandleAutofillAsync(string text, EventSnapshot @event)
    {
        ComboboxStoreState state = State;
        if (state.Disabled || state.ReadOnly || Multiple)
        {
            return;
        }

        ChangeEventDetails details = CreateDetails(EventReason.None, @event);
        if (state.SelectionMode == ComboboxSelectionMode.None)
        {
            await SetInputValueAsync(text, details);
            return;
        }

        ForceMount();
        if (!state.HasItems && renderedValues.Length == 0)
        {
            // AriaCombobox.tsx:1677-1689: the match runs once the forced mount has committed (a microtask there); here once
            // the mounted items have reported their order (SetRenderedValues asks for the boundary).
            pendingAutofill = (text, details);
            return;
        }

        await MatchAutofillAsync(text, details);
    }

    /// <inheritdoc />
    public override async Task<ChangeEventDetails> ClearAsync(EventReason reason, EventSnapshot? @event, bool shareDetails = true)
    {
        // ComboboxInput.tsx:347-362 (the closed Escape, one details object) and ComboboxClear.tsx:108-117 (the clear button,
        // one for the text and one for the value): the text, then the value.
        ChangeEventDetails textDetails = CreateDetails(reason, @event);
        await SetInputValueAsync(string.Empty, textDetails);
        ChangeEventDetails details = shareDetails ? textDetails : CreateDetails(reason, @event);
        if (Multiple)
        {
            await SetSelectedValuesAsync([], details);
        }
        else if (Single)
        {
            await SetSelectedValueAsync(default!, details, present: false);
        }

        return details;
    }

    /// <inheritdoc />
    public override async Task RemoveLastAsync(int renderedChips, EventSnapshot @event)
    {
        // ComboboxInput.tsx:364-385: Backspace into an empty input removes the last rendered chip's value.
        IReadOnlyList<TValue> current = SelectedValues;
        if (!Multiple || current.Count == 0)
        {
            return;
        }

        int removal = renderedChips > 0 ? renderedChips - 1 : current.Count - 1;
        TValue[] next = new TValue[current.Count - 1];
        for (int i = 0, j = 0; i < current.Count; i++)
        {
            if (i != removal)
            {
                next[j++] = current[i];
            }
        }

        await ClearHighlightAsync(@event);
        await SetSelectedValuesAsync(next, CreateDetails(EventReason.None, @event));
    }

    /// <inheritdoc />
    public override async Task<ChangeEventDetails?> RemoveAtAsync(int index, EventReason reason, EventSnapshot @event)
    {
        IReadOnlyList<TValue> current = SelectedValues;
        if (!Multiple)
        {
            return null;
        }

        ChangeEventDetails details = CreateDetails(reason, @event);

        // ComboboxChipRemove.tsx:76-90: the selection is filtered by the chip's index and set whatever the index holds, so a chip
        // whose value is already gone reports the unchanged selection (and its allowPropagation() still counts).
        if (index < 0 || index >= current.Count)
        {
            await SetSelectedValuesAsync([.. current], details);
            return details;
        }

        TValue removed = current[index];
        IReadOnlyList<TValue> next = ItemEquality.RemoveItem(current, removed, inputs.IsItemEqualToValue);

        // ComboboxChipRemove.tsx: the highlight clears when the removed value was the highlighted item.
        if (State.ActiveIndex is int active && active < filtered.Flat.Count && ItemEquality.Compare(filtered.Flat[active], removed, inputs.IsItemEqualToValue))
        {
            await ClearHighlightAsync(@event);
        }

        await SetSelectedValuesAsync(next, details);
        return details;
    }

    // ComboboxTrigger.tsx:116-121: valuesRef[index] with the reason none; an index without a value selects nothing.
    /// <inheritdoc />
    public override Task SelectTypeaheadMatchAsync(int index, EventSnapshot @event)
    {
        if (TriggerTypeahead is null || IsOpen || !TryCandidateAt(index, out TValue value))
        {
            return Task.CompletedTask;
        }

        return RunHandlerAsync(() => SetSelectedValueAsync(value, CreateDetails(EventReason.None, @event)));
    }

    /// <inheritdoc />
    public override async Task OnHighlightReportedAsync(int? index, EventSnapshot? @event)
    {
        // AriaCombobox.tsx:1433-1448 (onNavigate): automatic navigation while closed is ignored, except an inline clear, and
        // nothing is taken while the popup is exiting.
        if ((@event is null && !IsOpen && !(State.Inline && index is null)) || Lifecycle.DerivedTransitionStatus == TransitionStatus.Ending)
        {
            return;
        }

        // Base UI's onNavigate cannot run inside a DOM event handler: an eventless report that reaches C# while the store runs one
        // (the module's reset after the handler's own text change, received at one of its awaits) yields to the handler's highlight.
        if (@event is null && handlingEvent)
        {
            return;
        }

        // The module's answer to the store's own request, an eventless report of the index the store holds and has reported,
        // changes nothing: Base UI's list navigation does not call onNavigate for an index set from outside, so the post-commit
        // highlight is emitted once (AriaCombobox.tsx:1210-1225).
        if (@event is null && index is int at && State.ActiveIndex == at && lastHighlight.Emitted && lastHighlight.Index == at)
        {
            return;
        }

        await SetIndicesAsync(index, @event, fromModule: true);
    }

    /// <inheritdoc />
    public override Task ClearHighlightAsync(EventSnapshot? @event) => SetIndicesAsync(null, @event, fromModule: false, clearSelectedIndex: true);

    // ComboboxInput.tsx:188-216: unless the highlight is always on, an inline input clears the highlight it loses with the
    // focus and restores it on refocus, while its slot still exists; the saved index lasts one open cycle (:104-109).
    /// <inheritdoc />
    public override async Task OnInlineInputFocusAsync(bool focused)
    {
        if (!State.Inline)
        {
            return;
        }

        if (focused)
        {
            int? saved = savedActiveIndex;
            savedActiveIndex = null;
            if (saved is int at && at < CandidateValues().Count)
            {
                await SetIndicesAsync(at, null, fromModule: false);
            }

            return;
        }

        if (State.ActiveIndex is int active && inputs.AutoHighlight != ComboboxAutoHighlightMode.Always)
        {
            savedActiveIndex = active;
            await SetIndicesAsync(null, null, fromModule: false);
        }
    }

    /// <summary>Wires the store to the root's list navigation.</summary>
    /// <param name="behavior">The list navigation behavior.</param>
    /// <param name="request">Sends a C#-origin highlight to the list's handles.</param>
    public void AttachListNavigation(ListNavigationBehavior behavior, Func<int?, Task> request)
    {
        listBehavior = behavior;
        listNavigation = behavior.Context;
        requestHighlight = request;
    }

    /// <summary>Reads the three controlled values at a parameter set.</summary>
    /// <param name="parameters">The root's parameters.</param>
    /// <param name="names">The parameter names.</param>
    public void ReadControlled(Microsoft.AspNetCore.Components.ParameterView parameters, ComboboxValueNames names)
    {
        bool firstSet = !initialized;
        if (names.Value is { } valueName && names.DefaultValue is { } defaultName)
        {
            bool valueSupplied = parameters.TryGetValue(valueName, out TValue? value);
            bool defaultSupplied = parameters.TryGetValue(defaultName, out TValue? defaultValue);
            valueState.Update(valueSupplied, value!, defaultSupplied, defaultValue!);
            if (firstSet)
            {
                singlePresent = valueSupplied ? !ValueNull.IsNull(value) || !s_nullable : defaultSupplied && (!ValueNull.IsNull(defaultValue) || !s_nullable);
            }
            else if (valueState.IsControlled)
            {
                singlePresent = !ValueNull.IsNull(value) || !s_nullable;
            }
        }

        if (names.Values is { } valuesName && names.DefaultValues is { } defaultValuesName)
        {
            // CMB-EDGE-11: a controlled null is [] for every read.
            bool valuesSupplied = parameters.TryGetValue(valuesName, out IReadOnlyList<TValue>? values);
            bool defaultsSupplied = parameters.TryGetValue(defaultValuesName, out IReadOnlyList<TValue>? defaults);
            valuesState.Update(valuesSupplied, values ?? [], true, defaults ?? []);
        }

        bool inputSupplied = parameters.TryGetValue(names.InputValue, out string? inputValue);
        bool defaultInputSupplied = parameters.TryGetValue(names.DefaultInputValue, out string? defaultInputValue);

        // CMB-STA-19 (AriaCombobox.tsx:283, read at every render): hasInputValue is a given text, InputValue or
        // DefaultInputValue. InputValue is controlled iff supplied at the first parameter set, and a supplied null shows the
        // text derived from the selection, so `@bind-InputValue` on a field that starts null both derives and binds.
        HasInputValueParameter = (inputSupplied && inputValue is not null) || (defaultInputSupplied && defaultInputValue is not null);
        inputNullControlled = inputSupplied && inputValue is null;
        // The default is passed only when supplied: Controlled<T> compares it with the first set's, which it stored as default.
        bool defaultInputGiven = defaultInputSupplied && defaultInputValue is not null;
        inputValueState.Update(inputSupplied, inputValue ?? string.Empty, defaultInputGiven, defaultInputGiven ? defaultInputValue! : default!);
    }

    /// <summary>Applies the root's inputs: the flags, the filtered items, the labels and the input's derived label.</summary>
    /// <param name="next">The inputs of the parameter set.</param>
    /// <param name="fieldDisabled">Whether the enclosing field is disabled.</param>
    /// <param name="fieldControlId">The enclosing field's control id, the root's id when it has none of its own.</param>
    public void ApplyInputs(ComboboxRootInputs<TValue> next, bool fieldDisabled, string? fieldControlId = null)
    {
        ArgumentNullException.ThrowIfNull(next);
        bool firstSet = !initialized;
        ComboboxRootInputs<TValue> previous = inputs;
        inputs = next;
        if (!collatorCreated || !Equals(collatorCulture, next.Culture))
        {
            collatorCreated = true;
            collatorCulture = next.Culture;
            collator = CollatorFilter.Create(next.Culture);
        }

        ComboboxStoreState state = State;
        bool textChanged = false;
        bool disabled = fieldDisabled || next.Disabled;
        bool hasItems = next.Items is not null || next.ItemGroups is not null;
        string id = next.Id ?? fieldControlId ?? state.Id;
        state = state with
        {
            Id = id,
            SelectionMode = next.SelectionMode,
            Inline = next.Inline,
            Disabled = disabled,
            ReadOnly = next.ReadOnly,
            Required = next.Required,
            Grid = next.Grid,
            Virtualized = next.Virtualized,
            Modal = next.Modal,
            HasItems = hasItems,
        };

        if (firstSet)
        {
            initialized = true;

            // CMB-STA-19 (AriaCombobox.tsx:313-330): the uncontrolled initial text.
            if (HasInputValueParameter)
            {
                initialDefaultInputValue = next.DefaultInputValue ?? string.Empty;
            }
            else if (next.SelectionMode == ComboboxSelectionMode.Single)
            {
                initialDefaultInputValue = singlePresent ? LabelOf(valueState.Value) : string.Empty;
            }

            if (!inputValueState.IsControlled)
            {
                inputValueState.SetValueIfUncontrolled(initialDefaultInputValue);
            }

            state = state with
            {
                InputValue = inputNullControlled ? initialDefaultInputValue : inputValueState.Value,
                HasSelectedValue = ComputeHasSelectedValue(next.SelectionMode),
            };
        }
        else
        {
            // ADR 04 Amendments 2026-11-03 (6): a controlled text the consumer changed reaches the input's handle with a new
            // revision, as a commit does.
            string text = !inputValueState.IsControlled ? state.InputValue : inputNullControlled ? DerivedInputText() : inputValueState.Value;
            textChanged = inputValueState.IsControlled && !string.Equals(text, state.InputValue, StringComparison.Ordinal);
            state = state with
            {
                InputValue = text,
                InputRevision = textChanged ? state.InputRevision + 1 : state.InputRevision,
                HasSelectedValue = ComputeHasSelectedValue(next.SelectionMode),
            };
        }

        Write(state);
        RefreshDerivation(State);
        if (textChanged)
        {
            NotifyInputValueCommitted();
        }

        // CMB-STA-28 (AriaCombobox.tsx:454-470): an inline list open on the first render never runs the closed sync, so the
        // selection's index is seeded once, for the list navigation to highlight and scroll to on mount.
        if (firstSet && next.Inline && OpenState.Value && hasItems && next.SelectionMode != ComboboxSelectionMode.None)
        {
            Write(State with { SelectedIndex = FindSelectedIndex() });
        }

        // AriaCombobox.tsx:1295-1308 (handleSelectedValueChanged): a controlled value the consumer changed outside a commit
        // resyncs the input to its label, whether or not the query changed since the open.
        if (ObserveSelectedValue() && !firstSet && Single && !HasInputValueParameter && !State.InputInsidePopup)
        {
            labelSynced = false;
            labelSyncPending = true;
        }

        // CMB-STA-20 (AriaCombobox.tsx:1280-1331): a selection or label change resyncs the input's derived label.
        string selectedLabel = Single && singlePresent ? LabelOf(valueState.Value) : string.Empty;
        bool itemsChanged = !ReferenceEquals(lastItems, next.Items) || !ReferenceEquals(lastGroups, next.ItemGroups);
        bool labelChanged = !string.Equals(lastSelectedLabel, selectedLabel, StringComparison.Ordinal);
        lastItems = next.Items;
        lastGroups = next.ItemGroups;
        lastSelectedLabel = selectedLabel;
        if (!firstSet && (itemsChanged || labelChanged) && !queryChangedAfterOpen)
        {
            labelSynced = false;
            labelSyncPending = true;
        }

        if (!ReferenceEquals(previous.ItemToStringLabel, next.ItemToStringLabel) && !firstSet)
        {
            labelSynced = false;
        }

        if (!firstSet)
        {
            InputsApplied?.Invoke();
        }
    }

    /// <summary>Records the value an item part renders, which the list joins to the reported order.</summary>
    /// <param name="item">The item's composite behavior.</param>
    /// <param name="value">The item's value.</param>
    /// <param name="present"><see langword="false"/> for an item without a value under a root of a non-nullable value type.</param>
    public void RegisterRenderedValue(CompositeItemBehavior item, TValue value, bool present = true)
    {
        // ComboboxItem.tsx:98-109: an item writes its value at its index whenever the value changes, so a positional
        // template that hands an item another value under the same id keeps the registry current. An item without a value
        // of a value-type root holds an absent slot, which matches no value.
        bool had = itemValues.TryGetValue(item, out TValue? previous);
        bool changed = present ? !had || !Equality.Is(previous, value) : had;
        if (present)
        {
            itemValues[item] = value;
        }
        else
        {
            itemValues.Remove(item);
        }

        if (changed && renderedPositions.TryGetValue(item, out int position))
        {
            renderedValues[position] = new ItemSlot<TValue>(present ? value : default!, present);
            ReassertSelectedIndex();
        }
    }

    /// <summary>Removes an item part's value.</summary>
    /// <param name="item">The item's composite behavior.</param>
    public void UnregisterRenderedValue(CompositeItemBehavior item)
    {
        itemValues.Remove(item);
        renderedPositions.Remove(item);
    }

    /// <summary>Gets an item part's value as a slot of the rendered registry.</summary>
    /// <param name="item">The item's composite behavior.</param>
    /// <returns>The value, present, or an absent slot for an item without one.</returns>
    public ItemSlot<TValue> RenderedValueOf(CompositeItemBehavior item) =>
        itemValues.TryGetValue(item, out TValue? value) ? new ItemSlot<TValue>(value, true) : new ItemSlot<TValue>(default!, false);

    /// <summary>Records the values of the items that rendered, in order, for a list without <c>Items</c>.</summary>
    /// <param name="values">The rendered items' values joined to the reported order.</param>
    /// <param name="order">The items, in the reported order.</param>
    public void SetRenderedValues(ItemSlot<TValue>[] values, IReadOnlyList<CompositeItemBehavior> order)
    {
        renderedValues = values;
        renderedPositions.Clear();
        for (int i = 0; i < order.Count; i++)
        {
            renderedPositions[order[i]] = i;
        }

        SyncSelectedIndex();
        ReassertSelectedIndex();
        if (pendingAutofill is not null)
        {
            RequestBoundary();
        }
    }

    /// <summary>Determines whether <paramref name="value"/> is selected, Base UI's <c>isSelected</c> selector.</summary>
    /// <param name="value">An item's value.</param>
    /// <param name="nullItem">Whether the item stands for <c>null</c>: it has no value, or a <see langword="null"/> one.</param>
    /// <returns><see langword="true"/> if the item is selected.</returns>
    public bool IsSelected(TValue value, bool nullItem)
    {
        // itemEquality.ts:17-19: a null operand matches only a null one, so the null item is selected while nothing is.
        return State.SelectionMode switch
        {
            ComboboxSelectionMode.Single => singlePresent ? !nullItem && ItemEquality.Compare(value, valueState.Value, inputs.IsItemEqualToValue) : nullItem,
            ComboboxSelectionMode.Multiple => ItemEquality.Includes(SelectedValues, value, inputs.IsItemEqualToValue),
            _ => false,
        };
    }

    /// <summary>Gets the label of a value, by the root's label converter.</summary>
    /// <param name="value">The value.</param>
    /// <returns>The value's label.</returns>
    public string LabelOf(TValue? value) =>
        TryCollectionLabel(value!, out string label) ? label : ValueLabels.StringifyAsLabel<TValue>(value!, inputs.ItemToStringLabel);


    /// <summary>Gets the form value of a value, by the root's value converter.</summary>
    /// <param name="value">The value.</param>
    /// <returns>The value's serialized text.</returns>
    public string ValueTextOf(TValue? value) => ValueLabels.StringifyAsValue<TValue>(value!, inputs.ItemToStringValue);

    /// <summary>Gets the index of the first selected item among the rendered items, or <see langword="null"/>.</summary>
    /// <returns>The index.</returns>
    public int? FindSelectedIndex()
    {
        ReadOnlySpan<ItemSlot<TValue>> registry = State.HasItems ? filteredSlots : renderedValues;
        return State.SelectionMode switch
        {
            ComboboxSelectionMode.Single when singlePresent => ItemEquality.FindSelectionIndex(registry, valueState.Value, inputs.IsItemEqualToValue),
            ComboboxSelectionMode.Multiple => ItemEquality.FindSelectionIndex(registry, SelectedValues, inputs.IsItemEqualToValue),
            _ => null,
        };
    }

    /// <inheritdoc />
    public override async Task<bool> SetInputValueAsync(string text, ChangeEventDetails details, bool typed = false)
    {
        // AriaCombobox.tsx:655-733 (setInputValue).
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(details);
        bool applied = await inputValueState.NotifyAsync(text, details, inputs.OnInputValueChange);
        if (!applied)
        {
            // ADR 05 Amendments 2026-10-18 item 2: an uncontrolled typed edit the browser already shows is committed; its
            // follow-ups are skipped (CMB-EVT-07's note).
            if (typed && !inputValueState.IsControlled)
            {
                await CommitInputValueAsync(text);
            }
            else if (typed)
            {
                // A controlled text the consumer kept: the input's handle writes it over the typed edit.
                Write(State with { InputRevision = State.InputRevision + 1 });
                NotifyInputValueCommitted();
            }

            return false;
        }

        hadInputClear = details.Reason == EventReason.InputClear;
        int membership = State.MembershipRevision;
        if (details.Reason == EventReason.InputChange)
        {
            if (IsOpen && closeQuery is not null)
            {
                closeQuery = null;
            }

            if (typed)
            {
                bool hasQuery = text.Trim().Length != 0;
                if (hasQuery)
                {
                    queryChangedAfterOpen = true;
                }

                pendingHighlight = new PendingHighlight(hasQuery, false);
                if (hasQuery && inputs.AutoHighlight != ComboboxAutoHighlightMode.Off && State.ActiveIndex is null && (IsOpen || State.Inline))
                {
                    Write(State with { ActiveIndex = 0 });
                }
            }
        }
        else if (details.Reason == EventReason.InputClear && text.Length == 0 && State.InputInsidePopup)
        {
            pendingHighlight = new PendingHighlight(false, true);
        }

        await CommitInputValueAsync(text);

        // AriaCombobox.tsx:1087-1242 run after every commit. A change that leaves the store's filtered membership as it was
        // while the list is navigable renders no part, and its candidates are already the rendered items, so the pending
        // highlight resolves here: a Root render would render all of its content again (CMB-EDGE-10). Any other change asks
        // for the boundary, after the items it mounts have rendered.
        if (pendingHighlight is not null)
        {
            bool storeCandidates = State.HasItems || inputs.FilteredItems is not null || inputs.FilteredItemGroups is not null;
            if (storeCandidates && State.MembershipRevision == membership && (IsOpen || State.Inline))
            {
                await ResolvePendingHighlightAsync();
            }
            else
            {
                RequestBoundary();
            }
        }

        return true;
    }

    /// <inheritdoc />
    public override void SetInputInsidePopup(bool insidePopup)
    {
        bool inside = insidePopup || State.Inline;
        if (State.InputInsidePopup != inside)
        {
            Write(State with { InputInsidePopup = inside });
        }

        // CMB-STA-19: an input inside the popup starts blank when no input value was given.
        if (inside && !HasInputValueParameter && State.InputValue.Length != 0)
        {
            _ = ObserveAsync(SetInputValueAsync(string.Empty, CreateDetails(EventReason.None, null)));
        }
    }

    /// <inheritdoc />
    public override void ForceMount()
    {
        if (!State.HasItems && !State.ForceMounted)
        {
            Write(State with { ForceMounted = true });
        }
    }

    /// <inheritdoc />
    public override async Task<bool> RequestOpenAsync(bool open, OpenChangeEventDetails details)
    {
        ArgumentNullException.ThrowIfNull(details);

        // CMB-STA-18: setOpen returns at once when the requested state is the current one.
        if (IsOpen == open)
        {
            return false;
        }

        // CMB-DIS-02 (AriaCombobox.tsx:765-775): with items, nothing filtered and no empty state, Escape may close a parent.
        if (details.Reason == EventReason.EscapeKey && State.HasItems && filtered.Flat.Count == 0 && !EmptyMounted)
        {
            details.AllowPropagation();
        }

        bool applied = await base.RequestOpenAsync(open, details);
        if (applied && !open && State.InputInsidePopup && (details.Reason == EventReason.FocusOut || details.Reason == EventReason.OutsidePress))
        {
            NotifyClosedFromInsidePopup();
        }

        return applied;
    }

    /// <inheritdoc />
    public override async Task AtBatchBoundaryAsync()
    {
        await base.AtBatchBoundaryAsync();
        await ObserveQueryAndOpenAsync();
        if (pendingAutofill is { } autofill && renderedValues.Length != 0)
        {
            pendingAutofill = null;
            await MatchAutofillAsync(autofill.Text, autofill.Details);
        }

        if (labelSyncPending)
        {
            labelSyncPending = false;
            await SyncInputToSelectedLabelAsync();
        }

        SyncSelectedIndex();
        await ResolvePendingHighlightAsync();
    }

    /// <inheritdoc />
    protected override async Task<bool> RunConsumerStepAsync(bool open, OpenChangeEventDetails details)
    {
        bool applied = await base.RunConsumerStepAsync(open, details);

        // AriaCombobox.tsx:779-788: a typed request must not highlight a later open.
        if (pendingHighlight is { HasQuery: true } && (details.Reason == EventReason.InputChange) == details.IsCanceled)
        {
            pendingHighlight = null;
        }

        if (!applied)
        {
            return false;
        }

        if (open && closeQuery is not null)
        {
            await HandleInterruptedReopenAsync(details.Reason == EventReason.InputChange);
        }

        if (!open && queryChangedAfterOpen)
        {
            string query = handlerQuery ?? Query;
            if (Single)
            {
                if (!State.Inline)
                {
                    closeQuery = query;
                }

                if (query.Length == 0)
                {
                    queryChangedAfterOpen = false;
                }
            }
            else if (Multiple)
            {
                if (!State.Inline)
                {
                    closeQuery = query;
                }

                if (State.InputInsidePopup)
                {
                    await SetIndicesAsync(null, null, fromModule: false);
                }

                if (!State.InputInsidePopup || State.Inline)
                {
                    await SetInputValueAsync(string.Empty, CreateDetails(EventReason.InputClear, details.Event, isItemPress: details.Reason == EventReason.ItemPress));
                }
            }
        }

        await ObserveQueryAndOpenAsync();
        return true;
    }

    /// <inheritdoc />
    protected override void OnUnmount()
    {
        base.OnUnmount();
        _ = ObserveAsync(HandleUnmountCleanupAsync());
    }

    /// <summary>Applies a controlled value the consumer changed between parameter sets.</summary>
    public void OnControlledSelectionChanged() => OnSelectionCommitted();

    private async Task MaybeOpenOnInputAsync(string trimmed, bool opens, bool autoHighlight, EventSnapshot @event)
    {
        if (State.ReadOnly || State.Disabled || trimmed.Length == 0 || !opens)
        {
            return;
        }

        await RequestOpenAsync(true, new OpenChangeEventDetails(EventReason.InputChange, @event));
        if (!autoHighlight)
        {
            await ClearHighlightAsync(@event);
        }
    }

    private async Task SetIndicesAsync(int? activeIndex, EventSnapshot? @event, bool fromModule, bool clearSelectedIndex = false)
    {
        ComboboxStoreState state = State;
        if (state.ActiveIndex != activeIndex || (clearSelectedIndex && state.SelectedIndex is not null))
        {
            Write(state with { ActiveIndex = activeIndex, SelectedIndex = clearSelectedIndex ? null : state.SelectedIndex });
        }

        // A request equal to the module's mirror is not sent: on Server a typed change's clear, answered a round trip late,
        // would otherwise overwrite a highlight the user moved since (the mirror already shows none for the typing).
        if (!fromModule && requestHighlight is not null && listBehavior?.HighlightedIndex != activeIndex)
        {
            await requestHighlight(activeIndex);
        }

        await EmitHighlightAsync(activeIndex, @event);
    }

    private async Task EmitHighlightAsync(int? index, EventSnapshot? @event)
    {
        // AriaCombobox.tsx:606-624 (emitHighlight): a clear of a clear highlight is not reported.
        if (index is not int at || at < 0)
        {
            if (!lastHighlight.Emitted)
            {
                return;
            }

            lastHighlight = default;
            lastHighlight = default;
            await InvokeHighlightedAsync(default, -1, @event);
            return;
        }

        // The candidate at the index, read without copying the rendered registry (final review N2).
        TValue value = CandidateAt(at);
        lastHighlight = (true, value, at);
        await InvokeHighlightedAsync(value, at, @event);
    }

    private Task InvokeHighlightedAsync(TValue? value, int index, EventSnapshot? @event)
    {
        if (inputs.ItemHighlighted is not { } callback)
        {
            return Task.CompletedTask;
        }

        return callback(value, new HighlightEventDetails(HighlightReason.From(@event), index, @event));
    }

    private ChangeEventDetails CreateDetails(EventReason reason, EventSnapshot? @event, bool? isItemPress = null) =>
        inputs.CreateChangeDetails(reason, @event, null, isItemPress);

    private async Task HandleUnmountCleanupAsync()
    {
        // AriaCombobox.tsx:952-992 (handleUnmountCleanup); onOpenChangeComplete(false) is the lifecycle's.
        queryChangedAfterOpen = false;
        closeQuery = null;
        await SetIndicesAsync(null, null, fromModule: false, clearSelectedIndex: State.SelectionMode == ComboboxSelectionMode.None);
        if (Multiple && State.InputValue.Length != 0 && !hadInputClear)
        {
            await SetInputValueAsync(string.Empty, CreateDetails(EventReason.InputClear, null));
        }

        if (Single)
        {
            if (State.InputInsidePopup)
            {
                if (State.InputValue.Length != 0)
                {
                    await SetInputValueAsync(string.Empty, CreateDetails(EventReason.InputClear, null));
                }
            }
            else
            {
                string label = singlePresent ? LabelOf(valueState.Value) : string.Empty;
                if (!string.Equals(State.InputValue, label, StringComparison.Ordinal))
                {
                    await SetInputValueAsync(label, CreateDetails(label.Length == 0 ? EventReason.InputClear : EventReason.None, null));
                }
            }
        }

        RefreshDerivation(State);
    }

    private async Task HandleInterruptedReopenAsync(bool isInputChange)
    {
        // AriaCombobox.tsx:735-755.
        string inputValue = State.InputValue;
        string selectedLabel = Single && singlePresent ? LabelOf(valueState.Value) : string.Empty;
        bool inputMatchesSelected = Single && !State.InputInsidePopup && string.Equals(inputValue, selectedLabel, StringComparison.Ordinal);
        bool clearsPendingInput = !isInputChange
            && State.InputInsidePopup
            && !State.Inline
            && inputValue.Length != 0
            && (string.Equals(inputValue.Trim(), closeQuery, StringComparison.Ordinal) || string.Equals(inputValue, selectedLabel, StringComparison.Ordinal));
        if (!isInputChange && (clearsPendingInput || inputValue.Length == 0 || inputMatchesSelected))
        {
            queryChangedAfterOpen = false;
        }

        closeQuery = null;
        if (clearsPendingInput)
        {
            await SetInputValueAsync(string.Empty, CreateDetails(EventReason.InputClear, null));
        }
    }

    private async Task<bool> SetSelectedValueAsync(TValue value, ChangeEventDetails details, bool present = true)
    {
        // AriaCombobox.tsx:854-877 (setSelectedValue, single).
        if (!await valueState.NotifyAsync(value, details, inputs.OnValueChange))
        {
            return false;
        }

        await valueState.CommitAsync(value, inputs.ValueChanged);
        if (!valueState.IsControlled)
        {
            singlePresent = present && (!ValueNull.IsNull(value) || !s_nullable);
        }

        // A controlled value the consumer has not taken yet leaves the selection as it was: its label sync waits for the
        // consumer's change (useValueChanged(selectedValue) runs on a change only).
        OnSelectionCommitted(ObserveSelectedValue());

        bool fillInput = (State.SelectionMode == ComboboxSelectionMode.None && inputs.FillInputOnItemPress) || (Single && !State.InputInsidePopup);
        if (fillInput)
        {
            await SetInputValueAsync(present ? LabelOf(value) : string.Empty, CreateDetails(details.Reason, details.Event));
        }

        return true;
    }

    private async Task<bool> SetSelectedValuesAsync(IReadOnlyList<TValue> values, ChangeEventDetails details)
    {
        if (!await valuesState.NotifyAsync(values, details, inputs.OnValuesChange))
        {
            return false;
        }

        await valuesState.CommitAsync(values, inputs.ValuesChanged);
        OnSelectionCommitted();
        return true;
    }

    private void OnSelectionCommitted(bool valueChanged = true)
    {
        Write(State with { HasSelectedValue = ComputeHasSelectedValue(State.SelectionMode), SelectionRevision = State.SelectionRevision + 1 });
        SelectionCommitted?.Invoke();

        // CMB-STA-20: in single mode with the input outside the popup and no input value given, the input follows the label,
        // after the commit (Base UI's useValueChanged, a layout effect: after the selection's own fill).
        if (valueChanged && Single && !HasInputValueParameter && !State.InputInsidePopup)
        {
            labelSynced = false;
            labelSyncPending = true;
        }

        RefreshDerivation(State);
    }

    private async Task SyncInputToSelectedLabelAsync()
    {
        if (!Single || HasInputValueParameter || State.InputInsidePopup)
        {
            return;
        }

        string label = singlePresent ? LabelOf(valueState.Value) : string.Empty;
        if (labelSynced && string.Equals(syncedLabel, label, StringComparison.Ordinal))
        {
            return;
        }

        if (!string.Equals(State.InputValue, label, StringComparison.Ordinal))
        {
            labelSynced = true;
            syncedLabel = label;
            if (!await SetInputValueAsync(label, CreateDetails(EventReason.None, null)))
            {
                // A canceled sync is retried by the next trigger.
                labelSynced = false;
            }
        }
    }

    private async Task CommitInputValueAsync(string text)
    {
        await inputValueState.CommitAsync(text, inputs.InputValueChanged);
        string committed = inputValueState.Value;
        if (!string.Equals(committed, State.InputValue, StringComparison.Ordinal) || inputValueState.IsControlled)
        {
            Write(State with { InputValue = committed, InputRevision = State.InputRevision + 1 });
            RefreshDerivation(State);
        }

        NotifyInputValueCommitted();
        await ObserveQueryAndOpenAsync();
    }

    // AriaCombobox.tsx:1262-1278, 1326-1327 (useValueChanged(query) and useValueChanged(open), in that order): run after the
    // commits that can move the query or the open state, an input commit, an open change and the batch boundary (a
    // controlled Open or InputValue). A query that changes while open, by the consumer or an autofill as well as by
    // typing, counts as changed since the open unless it is the initial text or mirrors the selection; a controlled open
    // that interrupts the close runs the interrupted reopen, which no setOpen call ran. The list is derived again when the
    // query or the flag it reads moved since the last derivation.
    private async Task ObserveQueryAndOpenAsync()
    {
        string query = Query;
        bool open = IsOpen;
        if (!open)
        {
            savedActiveIndex = null;
        }

        if (!observing)
        {
            observing = true;
            observedQuery = query;
            observedOpen = open;
            return;
        }

        if (!string.Equals(query, observedQuery, StringComparison.Ordinal))
        {
            observedQuery = query;
            bool mirrorsSelection = Single
                && !State.InputInsidePopup
                && string.Equals(State.InputValue, singlePresent ? LabelOf(valueState.Value) : string.Empty, StringComparison.Ordinal);
            if (open && query.Length != 0 && !string.Equals(query, initialDefaultInputValue, StringComparison.Ordinal) && !mirrorsSelection)
            {
                queryChangedAfterOpen = true;
            }
        }

        if (open != observedOpen)
        {
            observedOpen = open;
            if (open && closeQuery is not null)
            {
                await HandleInterruptedReopenAsync(false);
            }
        }

        if (!string.Equals(Query, derivedQuery, StringComparison.Ordinal) || queryChangedAfterOpen != derivedQueryChanged)
        {
            RefreshDerivation(State);
        }
    }

    // createItems.ts:116-124: a value two items derive resolves to the first; the development error names it, once per collection.
    private void ReportDuplicates(IComboboxItemLabels<TValue> collection)
    {
        if (Logger is not { } logger || !DevelopmentWarnings.IsEnabled || ReferenceEquals(duplicatesReportedFor, collection))
        {
            return;
        }

        duplicatesReportedFor = collection;
        foreach (TValue duplicate in collection.DuplicateValues)
        {
            DevelopmentWarnings.Write(logger, string.Create(
                CultureInfo.InvariantCulture,
                $"Blazix: Two items passed to `ComboboxItems.Create()` derived the value {duplicate}, so selection and label resolution cannot tell them apart: the first item wins the label and every item carrying the value renders as selected. Return a unique value from `getValue`."));
        }
    }

    private TValue CandidateAt(int index)
    {
        if (State.HasItems || inputs.FilteredItems is not null || inputs.FilteredItemGroups is not null)
        {
            return index < filtered.Flat.Count ? filtered.Flat[index] : default!;
        }

        return index < renderedValues.Length ? renderedValues[index].Value : default!;
    }

    private bool TryCandidateAt(int index, out TValue value)
    {
        if (UsesCollection ? index >= 0 && index < filtered.Flat.Count : index >= 0 && index < renderedValues.Length && renderedValues[index].Present)
        {
            value = UsesCollection ? filtered.Flat[index] : renderedValues[index].Value;
            return true;
        }

        value = default!;
        return false;
    }

    private IReadOnlyList<TValue> CandidateValues()
    {
        if (State.HasItems || inputs.FilteredItems is not null || inputs.FilteredItemGroups is not null)
        {
            return filtered.Flat;
        }

        TValue[] values = new TValue[renderedValues.Length];
        for (int i = 0; i < values.Length; i++)
        {
            values[i] = renderedValues[i].Value;
        }

        return values;
    }

    // A DOM event handler's own writes, then Base UI's post-commit effects: the selection's label sync (AriaCombobox.tsx:
    // 1295-1308), then the highlight (:1089-1235). A commit here renders synchronously, so the highlight waits for the handler,
    // and the sync runs before it returns, not at a later event's boundary.
    private async Task RunHandlerAsync(Func<Task> handler)
    {
        handlingEvent = true;
        try
        {
            await handler();
        }
        finally
        {
            handlingEvent = false;
        }

        if (labelSyncPending)
        {
            labelSyncPending = false;
            await SyncInputToSelectedLabelAsync();
        }

        if (highlightDeferred)
        {
            highlightDeferred = false;
            await ResolvePendingHighlightAsync();
        }
    }

    private async Task HandleInputChangeCoreAsync(string text, bool typed, bool composing, EventSnapshot @event)
    {
        // ComboboxInput.tsx:240-317 (onChange; the composition branch keeps the text uncommitted).
        bool autoHighlight = inputs.AutoHighlight != ComboboxAutoHighlightMode.Off;
        if (composing)
        {
            if (text.Length == 0 && !inputs.OpenOnInputClick && !State.InputInsidePopup)
            {
                await RequestOpenAsync(false, new OpenChangeEventDetails(EventReason.InputClear, @event));
            }

            string trimmedComposing = text.Trim();
            await MaybeOpenOnInputAsync(trimmedComposing, opens: true, autoHighlight, @event);
            if (IsOpen && State.ActiveIndex is not null && !(autoHighlight && trimmedComposing.Length != 0))
            {
                await ClearHighlightAsync(@event);
            }

            return;
        }

        ChangeEventDetails changeDetails = CreateDetails(EventReason.InputChange, @event);
        await SetInputValueAsync(text, changeDetails, typed);
        if (changeDetails.IsCanceled)
        {
            return;
        }

        bool empty = text.Length == 0;
        if (empty && !State.InputInsidePopup)
        {
            ChangeEventDetails clearDetails = CreateDetails(EventReason.InputClear, @event);
            if (Single)
            {
                await SetSelectedValueAsync(default!, clearDetails, present: false);
            }

            if (!inputs.OpenOnInputClick)
            {
                await RequestOpenAsync(false, ToOpenDetails(clearDetails));
            }
        }

        await MaybeOpenOnInputAsync(text.Trim(), typed, autoHighlight, @event);
        if (IsOpen && State.ActiveIndex is not null && !autoHighlight)
        {
            await ClearHighlightAsync(@event);
        }
    }

    private async Task ResolvePendingHighlightAsync()
    {
        if (handlingEvent)
        {
            highlightDeferred = true;
            return;
        }

        // AriaCombobox.tsx:1088-1093: a kept-mounted dialog hides its inline list on close, so a pending query-clear restore is
        // discarded before it can overwrite the cleared highlight or report an item of the unfiltered list.
        if (!IsOpen && State.Inline && ListInsideOtherPopup)
        {
            pendingHighlight = null;
            return;
        }

        // AriaCombobox.tsx:1089-1235, the post-commit highlight: the typed query's first item, the clear's restore, and the
        // reconciliation of an index the filtered list no longer holds. The microtask of :1138 maps to this boundary (ADR 01
        // decision 3: no C# microtasks; its purpose is "after every write of this commit"). Base UI's effect runs when its
        // dependencies change (:1229-1241), so this runs when they moved since the last run; the rendered registry stands
        // for the items that register in Base UI's commit and reach C# with the module's order report here.
        HighlightDependencies dependencies = new(
            State.ActiveIndex,
            inputs.AutoHighlight,
            State.HasItems,
            HasFilteredItemsInput,
            State.MembershipRevision,
            State.Inline,
            IsOpen,
            State.InputValue,
            renderedValues,
            pendingHighlight is not null);
        if (resolvedFor == dependencies)
        {
            return;
        }

        resolvedFor = dependencies;
        IReadOnlyList<TValue> candidates = CandidateValues();
        if (pendingHighlight is { } pending)
        {
            bool navigable = IsOpen || State.Inline;
            if (pending.HasQuery)
            {
                if (inputs.AutoHighlight == ComboboxAutoHighlightMode.Off || State.InputValue.Trim().Length == 0)
                {
                    pendingHighlight = null;
                }
                else if (navigable && (candidates.Count > 0 || (!State.HasItems && !HasFilteredItemsInput)))
                {
                    pendingHighlight = null;
                    await SetIndicesAsync(0, null, fromModule: false);
                }
            }
            else if (State.InputValue.Trim().Length == 0)
            {
                pendingHighlight = null;
                if (navigable)
                {
                    if (inputs.AutoHighlight == ComboboxAutoHighlightMode.Always && !pending.Selection && State.SelectionMode == ComboboxSelectionMode.None)
                    {
                        await SetIndicesAsync(0, null, fromModule: false);
                    }

                    bool hasSelection = Multiple ? SelectedValues.Count > 0 : Single && singlePresent;
                    if (hasSelection)
                    {
                        int toggled = pending.HasToggledValue ? ItemEquality.FindItemIndex(Slots(candidates), pending.ToggledValue!, inputs.IsItemEqualToValue) : -1;
                        int? next = toggled != -1 ? toggled : FindSelectedIndexIn(candidates);
                        await SetIndicesAsync(next, null, fromModule: false);
                    }
                    else if (pending.Selection)
                    {
                        await SetIndicesAsync(null, null, fromModule: false);
                    }
                    else if (inputs.AutoHighlight == ComboboxAutoHighlightMode.Always)
                    {
                        await SetIndicesAsync(0, null, fromModule: false);
                    }
                }
            }
        }

        if (!IsOpen && !State.Inline)
        {
            return;
        }

        if (State.ActiveIndex is not int active)
        {
            if (inputs.AutoHighlight == ComboboxAutoHighlightMode.Always && candidates.Count > 0)
            {
                await SetIndicesAsync(0, null, fromModule: false);
            }

            return;
        }

        if (active >= candidates.Count && (State.HasItems || HasFilteredItemsInput))
        {
            await SetIndicesAsync(null, null, fromModule: false);
            return;
        }

        if (active < candidates.Count && (!lastHighlight.Emitted || lastHighlight.Index != active || !ItemEquality.Compare(candidates[active], lastHighlight.Value!, inputs.IsItemEqualToValue)))
        {
            await EmitHighlightAsync(active, null);
        }
    }

    private int? FindSelectedIndexIn(IReadOnlyList<TValue> candidates)
    {
        ItemSlot<TValue>[] slots = Slots(candidates);
        return Multiple
            ? ItemEquality.FindSelectionIndex(slots, SelectedValues, inputs.IsItemEqualToValue)
            : ItemEquality.FindSelectionIndex(slots, valueState.Value, inputs.IsItemEqualToValue);
    }

    private static ItemSlot<TValue>[] Slots(IReadOnlyList<TValue> values)
    {
        ItemSlot<TValue>[] slots = new ItemSlot<TValue>[values.Count];
        for (int i = 0; i < slots.Length; i++)
        {
            slots[i] = new ItemSlot<TValue>(values[i], true);
        }

        return slots;
    }

    private void SyncSelectedIndex()
    {
        // AriaCombobox.tsx:994-1040 (syncSelectedIndex): while closed, the selection's index in the rendered list.
        if (IsOpen || State.SelectionMode == ComboboxSelectionMode.None)
        {
            return;
        }

        int? index = FindSelectedIndex();
        if (State.SelectedIndex != index)
        {
            Write(State with { SelectedIndex = index });
        }

        if (listBehavior is not null)
        {
            listBehavior.SelectedIndex = index;
        }
    }

    // ComboboxItem.tsx:111-135: without items, a rendered item re-asserts the selection's index as it registers or its value
    // changes, open as well as closed; a selection that changes while open moves it only at the close (syncSelectedIndex).
    // The items' effects in rendered order leave the first matching index (resolveSelectedIndex anchors the first selected
    // item); with no match a single root keeps its index.
    private void ReassertSelectedIndex()
    {
        if (!IsOpen || State.HasItems || State.SelectionMode == ComboboxSelectionMode.None || FindSelectedIndex() is not int index)
        {
            return;
        }

        if (State.SelectedIndex != index)
        {
            Write(State with { SelectedIndex = index });
        }

        if (listBehavior is not null)
        {
            listBehavior.SelectedIndex = index;
        }
    }

    // useValueChanged(selectedValue): whether the single selection moved since it was last observed, which records it.
    private bool ObserveSelectedValue()
    {
        TValue? value = singlePresent ? valueState.Value : default;
        if (observedPresent == singlePresent && Equality.Is(observedValue, value))
        {
            return false;
        }

        observedPresent = singlePresent;
        observedValue = value;
        return true;
    }

    private async Task MatchAutofillAsync(string text, ChangeEventDetails details)
    {
        ReadOnlySpan<ItemSlot<TValue>> registry = State.HasItems ? filteredSlots : renderedValues;
        foreach (ItemSlot<TValue> slot in registry)
        {
            if (slot.Present
                && (string.Equals(ValueTextOf(slot.Value), text, StringComparison.OrdinalIgnoreCase) || string.Equals(LabelOf(slot.Value), text, StringComparison.OrdinalIgnoreCase))
                && !ValueNull.IsNull(slot.Value))
            {
                await SetSelectedValueAsync(slot.Value, details);
                return;
            }
        }
    }

    private async Task ObserveAsync(Task task)
    {
        try
        {
            await task;
        }
        catch (Exception exception) when (!task.IsCanceled && DispatchException is not null)
        {
            await DispatchException(exception);
        }
    }

    // STA-19's derived text, which a supplied null InputValue shows: the selected label in single mode, else empty.
    private string DerivedInputText() => Single && singlePresent ? LabelOf(valueState.Value) : string.Empty;

    // CMB-API-32, EDGE-12 (AriaCombobox.tsx:216-229): the collection given as the items first, then a collection given as the
    // current filtered items (the external window); nothing from a past window is remembered.
    private bool TryCollectionLabel(TValue value, out string label)
    {
        Func<TValue, TValue, bool>? comparer = inputs.IsItemEqualToValue;
        if ((inputs.Items as IComboboxItemLabels<TValue> ?? inputs.ItemGroups as IComboboxItemLabels<TValue>) is { } items)
        {
            ReportDuplicates(items);
            if (items.TryGetLabel(value, comparer, out label))
            {
                return true;
            }
        }

        if ((inputs.FilteredItems as IComboboxItemLabels<TValue> ?? inputs.FilteredItemGroups as IComboboxItemLabels<TValue>) is { } window && window.TryGetLabel(value, comparer, out label))
        {
            return true;
        }

        label = string.Empty;
        return false;
    }

    private void RefreshDerivation(ComboboxStoreState state)
    {
        // CMB-STA-21..23: the query, the bypass and the filtered items; the membership revision only when it changed.
        string query = !state.Lifecycle.EffectiveOpen && closeQuery is not null ? closeQuery : state.InputValue.Trim();
        derivedQuery = query;
        derivedQueryChanged = queryChangedAfterOpen;
        string selectedLabel = Single && singlePresent ? LabelOf(valueState.Value) : string.Empty;
        bool bypass = ComboboxItemFiltering.ShouldBypassFiltering(Single, queryChangedAfterOpen, query, selectedLabel, collator);
        string filterQuery = bypass ? string.Empty : inputs.FilterQuery ?? query;
        bool hasItems = inputs.Items is not null || inputs.ItemGroups is not null;

        // AriaCombobox.tsx:352-356: the bypass keeps the external result while the known data lacks the selected item.
        bool ignoreExternal = hasItems && (inputs.FilteredItems is not null || inputs.FilteredItemGroups is not null) && bypass
            && ((inputs.Items as IComboboxItemLabels<TValue> ?? inputs.ItemGroups as IComboboxItemLabels<TValue>) is not { } known || known.HasValue(valueState.Value, inputs.IsItemEqualToValue));

        // AriaCombobox.tsx:358-438 memoizes the filtered items on their inputs (ADR 16 decision 11): unchanged inputs, by
        // reference for the lists and the delegates, filter nothing again.
        DerivationKey key = new(
            inputs.Items,
            inputs.ItemGroups,
            inputs.FilteredItems,
            inputs.FilteredItemGroups,
            ignoreExternal,
            filterQuery,
            inputs.Limit,
            inputs.FilterSupplied,
            inputs.Filter,
            inputs.ItemToStringLabel,
            collator);
        if (derivedKey is { } previous && previous.Matches(key))
        {
            return;
        }

        derivedKey = key;
        ComboboxItemPredicate<TValue> predicate = inputs.FilterSupplied
            ? inputs.Filter is { } custom ? ComboboxItemPredicate<TValue>.Custom(custom, FilterItemToString) : ComboboxItemPredicate<TValue>.Off
            : ComboboxItemPredicate<TValue>.Collator(collator, FilterItemToString);
        ComboboxFilteredItems<TValue> next = ComboboxItemFiltering.Derive(new ComboboxFilterInputs<TValue>(
            inputs.Items,
            inputs.ItemGroups,
            inputs.FilteredItems,
            inputs.FilteredItemGroups,
            ignoreExternal,
            filterQuery,
            inputs.Limit,
            predicate));
        if (SameMembership(filtered, next))
        {
            return;
        }

        filtered = next;
        filteredSlots = Slots(next.Flat);
        filteredLabels = new string?[next.Flat.Count];
        for (int i = 0; i < filteredLabels.Length; i++)
        {
            filteredLabels[i] = LabelOf(next.Flat[i]);
        }

        Write(State with { FilteredCount = next.Flat.Count, MembershipRevision = State.MembershipRevision + 1 });
        NotifySessionInputsChanged();
    }

    private bool SameMembership(ComboboxFilteredItems<TValue> current, ComboboxFilteredItems<TValue> next)
    {
        if (ReferenceEquals(current.Flat, next.Flat) && ReferenceEquals(current.Groups, next.Groups))
        {
            return true;
        }

        if (current.Flat.Count != next.Flat.Count || (current.Groups is null) != (next.Groups is null))
        {
            return false;
        }

        for (int i = 0; i < next.Flat.Count; i++)
        {
            if (!Equality.Is(current.Flat[i], next.Flat[i]))
            {
                return false;
            }
        }

        if (next.Groups is { } groups)
        {
            if (groups.Count != current.Groups!.Count)
            {
                return false;
            }

            for (int g = 0; g < groups.Count; g++)
            {
                if (!string.Equals(groups[g].Value, current.Groups[g].Value, StringComparison.Ordinal) || groups[g].Items.Count != current.Groups[g].Items.Count)
                {
                    return false;
                }
            }
        }

        return true;
    }

    private bool ComputeHasSelectedValue(ComboboxSelectionMode mode) => mode switch
    {
        ComboboxSelectionMode.Single => singlePresent,
        ComboboxSelectionMode.Multiple => SelectedValues.Count > 0,
        _ => false,
    };

    private static OpenChangeEventDetails ToOpenDetails(ChangeEventDetails details) => new(details.Reason, details.Event, details.Trigger);

    // Compared by Matches: the lists and the delegates by reference, as Base UI's memo compares its dependencies.
    private readonly record struct DerivationKey(
        object? Items,
        object? ItemGroups,
        object? FilteredItems,
        object? FilteredItemGroups,
        bool IgnoreExternal,
        string FilterQuery,
        int Limit,
        bool FilterSupplied,
        object? Filter,
        object? ItemToStringLabel,
        CollatorFilter Collator)
    {
        public bool Matches(in DerivationKey other) =>
            ReferenceEquals(Items, other.Items)
            && ReferenceEquals(ItemGroups, other.ItemGroups)
            && ReferenceEquals(FilteredItems, other.FilteredItems)
            && ReferenceEquals(FilteredItemGroups, other.FilteredItemGroups)
            && IgnoreExternal == other.IgnoreExternal
            && string.Equals(FilterQuery, other.FilterQuery, StringComparison.Ordinal)
            && Limit == other.Limit
            && FilterSupplied == other.FilterSupplied
            && ReferenceEquals(Filter, other.Filter)
            && ReferenceEquals(ItemToStringLabel, other.ItemToStringLabel)
            && Collator == other.Collator;
    }

    private readonly record struct HighlightDependencies(
        int? ActiveIndex,
        ComboboxAutoHighlightMode AutoHighlight,
        bool HasItems,
        bool HasFilteredItems,
        int MembershipRevision,
        bool Inline,
        bool Open,
        string InputValue,
        ItemSlot<TValue>[] Rendered,
        bool Pending);

    private readonly record struct PendingHighlight(bool HasQuery, bool Selection)
    {
        public bool HasToggledValue { get; init; }

        public TValue? ToggledValue { get; init; }
}
}

/// <summary>What an item press reads that a report set before its click: Enter's keydown and a link press.</summary>
/// <param name="SelectionEvent">The keydown Enter's synthetic click is attributed to, or <see langword="null"/>.</param>
/// <param name="LinkPress">The <c>href</c> of a press inside a link of the item, or <see langword="null"/>.</param>
internal readonly record struct ComboboxClickContext(EventSnapshot? SelectionEvent, string? LinkPress);

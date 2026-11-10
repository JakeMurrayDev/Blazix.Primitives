using System.Globalization;
using Blazix.Primitives.Store;
using Blazix.Primitives.Utilities;
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

    /// <summary>Occurs when a part that the session's options read registered or left, such as an empty state.</summary>
    public event Action? SessionInputsChanged;

    /// <summary>Occurs when the committed input text changed, for the input's <c>value</c> writer.</summary>
    public event Action? InputValueCommitted;

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
    /// <summary>Gets the id of the form that owns the hidden input.</summary>
    public abstract string? Form { get; }

    /// <summary>Gets a value indicating whether a value is required.</summary>
    public bool Required => State.Required;

    /// <summary>Gets a value indicating whether an empty-state part is mounted.</summary>
    public bool EmptyMounted => emptyParts > 0;

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

    /// <summary>Takes the highlight the list handle reported.</summary>
    /// <param name="index">The highlighted index, or <see langword="null"/> for none.</param>
    /// <param name="event">The event that moved the highlight, or <see langword="null"/>.</param>
    /// <returns>A task that completes when the highlight callback has run.</returns>
    public abstract Task OnHighlightReportedAsync(int? index, EventSnapshot? @event);

    /// <summary>Clears the highlight, as typing and the clear button do.</summary>
    /// <param name="event">The event that cleared it.</param>
    /// <returns>A task that completes when the request and the callback have run.</returns>
    public abstract Task ClearHighlightAsync(EventSnapshot? @event);

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

    /// <summary>Raises <see cref="InputValueCommitted"/>.</summary>
    protected void NotifyInputValueCommitted() => InputValueCommitted?.Invoke();

    /// <summary>Raises <see cref="SessionInputsChanged"/>.</summary>
    protected void NotifySessionInputsChanged() => SessionInputsChanged?.Invoke();

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

    private readonly Dictionary<CompositeItemBehavior, TValue> itemValues = new(ReferenceEqualityComparer.Instance);

    private ItemSlot<TValue>[] renderedValues = [];

    private string? closeQuery;

    private bool queryChangedAfterOpen;

    private string initialDefaultInputValue = string.Empty;

    private PendingHighlight? pendingHighlight;

    private bool hadInputClear;

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

    /// <summary>Occurs after a close by focus-out or an outside press while the input is inside the popup, for the field's touched state.</summary>
    public event Action? ClosedFromInsidePopup;

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

    /// <inheritdoc />
    public override ListNavigationContext ListNavigation => listNavigation ?? throw new InvalidOperationException("Blazix: the combobox's list navigation is created at its root's first parameter set.");

    /// <inheritdoc />
    public override IReadOnlyList<string?> FilteredLabels => filteredLabels;

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

    /// <inheritdoc />
    public override void SetSelectionEvent(EventSnapshot? @event) => selectionEvent = @event;

    /// <summary>Selects or toggles an item, as an item press does.</summary>
    /// <param name="value">The item's value.</param>
    /// <param name="event">The press, or the keydown that started it.</param>
    /// <returns>A task that completes when the selection and its follow-ups have run.</returns>
    public async Task SelectAsync(TValue value, EventSnapshot @event)
    {
        // AriaCombobox.tsx:880-950 (handleSelection): Enter's synthetic click is attributed to its keydown.
        EventSnapshot source = selectionEvent ?? @event;
        selectionEvent = null;
        ChangeEventDetails details = CreateDetails(EventReason.ItemPress, source);
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

        if (!await SetSelectedValueAsync(value, details))
        {
            return;
        }

        await RequestOpenAsync(false, ToOpenDetails(details));
    }

    /// <inheritdoc />
    public override async Task HandleInputChangeAsync(string text, bool typed, bool composing, EventSnapshot @event)
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
        ReadOnlySpan<ItemSlot<TValue>> registry = state.HasItems ? filteredSlots : renderedValues;
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
        if (!Multiple || index < 0 || index >= current.Count)
        {
            return null;
        }

        TValue removed = current[index];
        IReadOnlyList<TValue> next = ItemEquality.RemoveItem(current, removed, inputs.IsItemEqualToValue);
        ChangeEventDetails details = CreateDetails(reason, @event);

        // ComboboxChipRemove.tsx: the highlight clears when the removed value was the highlighted item.
        if (State.ActiveIndex is int active && active < filtered.Flat.Count && ItemEquality.Compare(filtered.Flat[active], removed, inputs.IsItemEqualToValue))
        {
            await ClearHighlightAsync(@event);
        }

        await SetSelectedValuesAsync(next, details);
        return details;
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

        await SetIndicesAsync(index, @event, fromModule: true);
    }

    /// <inheritdoc />
    public override Task ClearHighlightAsync(EventSnapshot? @event) => SetIndicesAsync(null, @event, fromModule: false, clearSelectedIndex: true);

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
        inputValueState.Update(inputSupplied, inputValue ?? string.Empty, defaultInputSupplied && defaultInputValue is not null, defaultInputValue ?? string.Empty);
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
            Lifecycle = state.Lifecycle with { Disabled = disabled },
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
    }

    /// <summary>Records the value an item part renders, which the list joins to the reported order.</summary>
    /// <param name="item">The item's composite behavior.</param>
    /// <param name="value">The item's value.</param>
    public void RegisterRenderedValue(CompositeItemBehavior item, TValue value) => itemValues[item] = value;

    /// <summary>Removes an item part's value.</summary>
    /// <param name="item">The item's composite behavior.</param>
    public void UnregisterRenderedValue(CompositeItemBehavior item) => itemValues.Remove(item);

    /// <summary>Gets an item part's value as a slot of the rendered registry.</summary>
    /// <param name="item">The item's composite behavior.</param>
    /// <returns>The value, present, or an absent slot for an item without one.</returns>
    public ItemSlot<TValue> RenderedValueOf(CompositeItemBehavior item) =>
        itemValues.TryGetValue(item, out TValue? value) ? new ItemSlot<TValue>(value, true) : new ItemSlot<TValue>(default!, false);

    /// <summary>Records the values of the items that rendered, in order, for a list without <c>Items</c>.</summary>
    /// <param name="values">The rendered items' values joined to the reported order.</param>
    public void SetRenderedValues(ItemSlot<TValue>[] values)
    {
        renderedValues = values;
        SyncSelectedIndex();
    }

    /// <summary>Determines whether <paramref name="value"/> is selected, Base UI's <c>isSelected</c> selector.</summary>
    /// <param name="value">An item's value.</param>
    /// <returns><see langword="true"/> if the item is selected.</returns>
    public bool IsSelected(TValue value)
    {
        return State.SelectionMode switch
        {
            ComboboxSelectionMode.Single => singlePresent && ItemEquality.Compare(value, valueState.Value, inputs.IsItemEqualToValue),
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

        // AriaCombobox.tsx:1087-1242 run after every commit: a change that leaves the filtered membership as it was renders
        // no list part, so the pending highlight asks for the boundary itself.
        if (pendingHighlight is not null)
        {
            RequestBoundary();
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
            ClosedFromInsidePopup?.Invoke();
        }

        return applied;
    }

    /// <inheritdoc />
    public override async Task AtBatchBoundaryAsync()
    {
        await base.AtBatchBoundaryAsync();
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
            string query = Query;
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
            await InvokeHighlightedAsync(default, -1, @event);
            return;
        }

        IReadOnlyList<TValue> values = CandidateValues();
        TValue value = at < values.Count ? values[at] : default!;
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

        OnSelectionCommitted();

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

    private void OnSelectionCommitted()
    {
        Write(State with { HasSelectedValue = ComputeHasSelectedValue(State.SelectionMode), SelectionRevision = State.SelectionRevision + 1 });
        SelectionCommitted?.Invoke();

        // CMB-STA-20: in single mode with the input outside the popup and no input value given, the input follows the label,
        // after the commit (Base UI's useValueChanged, a layout effect: after the selection's own fill).
        if (Single && !HasInputValueParameter && !State.InputInsidePopup)
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

    private async Task ResolvePendingHighlightAsync()
    {
        // AriaCombobox.tsx:1089-1235, the post-commit highlight: the typed query's first item, the clear's restore, and the
        // reconciliation of an index the filtered list no longer holds. The microtask of :1138 maps to this boundary (ADR 01
        // decision 3: no C# microtasks; its purpose is "after every write of this commit").
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
        if ((inputs.Items as IComboboxItemLabels<TValue> ?? inputs.ItemGroups as IComboboxItemLabels<TValue>) is { } items && items.TryGetLabel(value, comparer, out label))
        {
            return true;
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
        string selectedLabel = Single && singlePresent ? LabelOf(valueState.Value) : string.Empty;
        bool bypass = ComboboxItemFiltering.ShouldBypassFiltering(Single, queryChangedAfterOpen, query, selectedLabel, collator);
        string filterQuery = bypass ? string.Empty : inputs.FilterQuery ?? query;
        bool hasItems = inputs.Items is not null || inputs.ItemGroups is not null;
        bool ignoreExternal = hasItems && (inputs.FilteredItems is not null || inputs.FilteredItemGroups is not null) && bypass;
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

    private readonly record struct PendingHighlight(bool HasQuery, bool Selection)
    {
        public bool HasToggledValue { get; init; }

        public TValue? ToggledValue { get; init; }
}
}

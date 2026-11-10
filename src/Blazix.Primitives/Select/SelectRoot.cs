using System.Linq.Expressions;
using Blazix.Primitives.Interop;
using Blazix.Primitives.Rendering;
using Blazix.Primitives.Store;
using Blazix.Primitives.Utilities;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace Blazix.Primitives;

// Base UI's Select.Root (select/root/SelectRoot.tsx); the contract is docs/components/select.md §3.1, §4, §7, §8.
// - An elementless ComponentBase that composes PopupLifecycleRootBehavior over SelectStore<TValue> (ADR 05 Amendments
//   2026-11-03, 2026-11-04; track 2), cascades the store fixed (the non-generic parts read it as SelectStore), and renders
//   its children, then the hidden inputs (SEL-STR-01), which are a child component so a value change renders them alone.
// - The value: the probe's two triples (ADR 08 Amendments 2026-11-03 (1) and its 2026-11-04 line), ListValueParameters.Check
//   first; ownership fixed at the first parameter set (Controlled<T>, diagnostics `Select`/`value`, SEL-EDGE-02).
// - The `interactions` session by the store's token, every dismissal default (SEL-DIS-02: outside press sloppy, Escape not
//   bubbling, outside press bubbling), its parent the enclosing popup's (IFloatingNodeContext; ADR 04 Amendments
//   2026-11-03 (5)); the commit path (ADR 01 Amendments 2026-10-27): a committed change reaches the session and the list
//   without a render of the Root.
// - List navigation (ADR 04 Amendments 2026-11-03 (1), track 1): vertical, no loop, `auto` focus on open, the hover
//   highlight from HighlightItemOnHover, an empty DisabledIndices, the selected index, and the typeahead with Base UI's
//   750 ms default skipping attribute-disabled items (SEL §6 preamble; SEL-KEY-04/12/20).
/// <summary>
/// Groups all parts of a select and owns its value and open state, Base UI's <c>Select.Root</c>. It renders no element of
/// its own: its content, then the hidden input that carries the value into a form.
/// </summary>
/// <typeparam name="TValue">The type of the select's values; for a multiple select, the type of each value.</typeparam>
/// <remarks>
/// <para>
/// A select holds one value (<see cref="Value"/>, <see cref="ValueChanged"/> or <c>@bind-Value</c>, <see cref="DefaultValue"/>,
/// <see cref="OnValueChange"/>) or, with <see cref="Multiple"/>, a list of values (<see cref="Values"/>,
/// <see cref="ValuesChanged"/> or <c>@bind-Values</c>, <see cref="DefaultValues"/>, <see cref="OnValuesChange"/>). Supplying a
/// parameter of the other mode throws.
/// </para>
/// <para>
/// <typeparamref name="TValue"/> is inferred from a value parameter or from <see cref="Items"/>; write
/// <c>TValue="…"</c> when the root sets none of them, or when <see cref="OnValueChange"/> is a method group. Parts inside a
/// separate component or a <see cref="RenderFragment"/> field write <c>TValue</c> or their own <c>Value</c> of exactly the
/// root's type. <c>@bind-Values</c> needs a field typed <see cref="IReadOnlyList{T}"/>.
/// </para>
/// <para>
/// <see cref="OnOpenChange"/> receives a <see cref="ValueChangeEventArgs{TValue}"/> whose <c>Details</c> is an
/// <see cref="OpenChangeEventDetails"/> at run time: cast it to call <c>PreventUnmountOnClose()</c>.
/// </para>
/// </remarks>
[CascadingTypeParameter(nameof(TValue))]
public sealed class SelectRoot<TValue> : ComponentBase, IAsyncDisposable
{
    private const string TypeaheadSkip = "attribute";
    private const int TypeaheadResetMs = 750;

    private static readonly InteractionsDismissOptions s_dismiss = new();
    private static readonly IReadOnlyList<int> s_noDisabledIndices = [];

    private readonly PopupLifecycleRootBehavior<SelectStore<TValue>, SelectStoreState> behavior;
    private readonly RenderFragment content;
    private readonly RenderFragment listContent;
    private InteractionsSessionBehavior? session;
    private ListNavigationBehavior? list;
    private SelectField? fieldHalf;
    private SelectorSubscription<SelectStoreState>? committed;
    private RenderFragment? cascade;
    private bool rendered;
    private bool renderedWhileClosed;
    private bool openDeferred;
    private bool firstSet = true;
    private object? labelsSource;
    private Func<TValue, string>? labelFunction;
    private TValue appliedValue = default!;
    private bool appliedHasValue;
    private IReadOnlyList<TValue>? appliedValues;
    private Func<TValue, TValue, bool>? appliedComparer;
    private bool appliedMultiple;
    private object? boxedFor;
    private bool boxed;
    private object? boxedValue;

    /// <summary>Initializes a new instance of the <see cref="SelectRoot{TValue}"/> class.</summary>
    public SelectRoot()
    {
        behavior = new((popupId, token, logger) => new SelectStore<TValue>(popupId, token, logger), StateHasChanged);
        content = behavior.BuildRenderTree;
        listContent = RenderListContent;
    }

    [Inject]
    private PartIdGenerator Ids { get; set; } = null!;

    [Inject]
    private ILogger<SelectRoot<TValue>> Logger { get; set; } = null!;

    [Inject]
    private IJSRuntime JSRuntime { get; set; } = null!;

    [Inject]
    private DefaultFormScope Formless { get; set; } = null!;

    [CascadingParameter]
    private IFloatingNodeContext? FloatingParent { get; set; }

    [CascadingParameter]
    private FieldRootContext? FieldContext { get; set; }

    [CascadingParameter]
    private IFormScope? FormScope { get; set; }

    [CascadingParameter]
    private EditContext? CascadedEditContext { get; set; }

    // SEL-API-01
    /// <summary>Gets or sets the selected value, when the consumer owns it.</summary>
    /// <value>
    /// The value the consumer owns, when supplied at the first render; then a selection only reports through
    /// <see cref="OnValueChange"/> and <see cref="ValueChanged"/>. Use <c>@bind-Value</c> to follow them.
    /// </value>
    [Parameter]
    public TValue Value { get; set; } = default!;

    /// <summary>Gets or sets the callback that receives the selected value, for <c>@bind-Value</c>.</summary>
    /// <value>Invoked after <see cref="OnValueChange"/> when the change is not canceled.</value>
    [Parameter]
    public EventCallback<TValue> ValueChanged { get; set; }

    // SEL-API-02
    /// <summary>Gets or sets the value selected at the first render, while <see cref="Value"/> is not supplied.</summary>
    /// <value>Nothing is selected by default.</value>
    [Parameter]
    public TValue DefaultValue { get; set; } = default!;

    // SEL-API-03
    /// <summary>Gets or sets the callback invoked before the selected value changes.</summary>
    /// <value>
    /// Receives the requested value and its <see cref="ChangeEventDetails"/>; calling <c>Details.Cancel()</c> keeps the
    /// current value.
    /// </value>
    [Parameter]
    public EventCallback<ValueChangeEventArgs<TValue>> OnValueChange { get; set; }

    /// <summary>Gets or sets the expression of the bound value, which <c>@bind-Value</c> supplies.</summary>
    /// <value>The expression that identifies the value to an <see cref="EditContext"/>, or <see langword="null"/>.</value>
    [Parameter]
    public Expression<Func<TValue>>? ValueExpression { get; set; }

    // SEL-API-01, SEL-API-14
    /// <summary>Gets or sets the selected values of a multiple select, when the consumer owns them.</summary>
    /// <value>
    /// The values the consumer owns, when supplied at the first render; <see langword="null"/> selects nothing. Use
    /// <c>@bind-Values</c> to follow the changes.
    /// </value>
    [Parameter]
    public IReadOnlyList<TValue>? Values { get; set; }

    /// <summary>Gets or sets the callback that receives the selected values, for <c>@bind-Values</c>.</summary>
    /// <value>Invoked after <see cref="OnValuesChange"/> when the change is not canceled.</value>
    [Parameter]
    public EventCallback<IReadOnlyList<TValue>> ValuesChanged { get; set; }

    // SEL-API-02
    /// <summary>Gets or sets the values a multiple select holds at its first render, while <see cref="Values"/> is not supplied.</summary>
    /// <value>An empty selection by default.</value>
    [Parameter]
    public IReadOnlyList<TValue>? DefaultValues { get; set; }

    /// <summary>Gets or sets the callback invoked before the selected values of a multiple select change.</summary>
    /// <value>Receives the requested values, a new list, and their <see cref="ChangeEventDetails"/>; calling <c>Details.Cancel()</c> keeps them.</value>
    [Parameter]
    public EventCallback<ValueChangeEventArgs<IReadOnlyList<TValue>>> OnValuesChange { get; set; }

    /// <summary>Gets or sets the expression of the bound values, which <c>@bind-Values</c> supplies.</summary>
    /// <value>The expression that identifies the values to an <see cref="EditContext"/>, or <see langword="null"/>.</value>
    [Parameter]
    public Expression<Func<IReadOnlyList<TValue>>>? ValuesExpression { get; set; }

    // SEL-API-14
    /// <summary>Gets or sets a value indicating whether several values may be selected.</summary>
    /// <value>
    /// <see langword="false"/> by default. It must not change after the first render; in multiple mode use the
    /// <see cref="Values"/> parameters.
    /// </value>
    [Parameter]
    public bool Multiple { get; set; }

    // SEL-API-04
    /// <summary>Gets or sets a value indicating whether the popup is open, when the consumer owns it.</summary>
    /// <value>
    /// The open state the consumer owns, when supplied at the first render; use <c>@bind-Open</c> to follow the requests.
    /// </value>
    [Parameter]
    public bool Open { get; set; }

    /// <summary>Gets or sets the callback that receives the requested open state, for <c>@bind-Open</c>.</summary>
    /// <value>Invoked after <see cref="OnOpenChange"/> when the change is not canceled.</value>
    [Parameter]
    public EventCallback<bool> OpenChanged { get; set; }

    /// <summary>Gets or sets a value indicating whether the popup is open at the first render, while <see cref="Open"/> is not supplied.</summary>
    /// <value><see langword="false"/> by default.</value>
    [Parameter]
    public bool DefaultOpen { get; set; }

    // SEL-API-05
    /// <summary>Gets or sets the callback invoked before the popup opens or closes.</summary>
    /// <value>
    /// Receives the requested state and an <see cref="OpenChangeEventDetails"/> as its <c>Details</c>; calling
    /// <c>Details.Cancel()</c> keeps the current state.
    /// </value>
    [Parameter]
    public EventCallback<ValueChangeEventArgs<bool>> OnOpenChange { get; set; }

    // SEL-API-06
    /// <summary>Gets or sets the callback invoked after the open or close transition finishes.</summary>
    /// <value>Receives <see langword="true"/> after opening and <see langword="false"/> after closing.</value>
    [Parameter]
    public EventCallback<bool> OnOpenChangeComplete { get; set; }

    // SEL-API-10
    /// <summary>Gets or sets the id of the trigger, which the label and the hidden input derive their ids from.</summary>
    /// <value>An id, or <see langword="null"/>, the default, for a generated one.</value>
    [Parameter]
    public string? Id { get; set; }

    // SEL-API-07
    /// <summary>Gets or sets the name the value is submitted under in a form.</summary>
    /// <value>A name, or <see langword="null"/>; an enclosing field's name wins.</value>
    [Parameter]
    public string? Name { get; set; }

    // SEL-API-08
    /// <summary>Gets or sets the id of the form the value is submitted with, when the select is outside it.</summary>
    /// <value>A form's id, or <see langword="null"/>.</value>
    [Parameter]
    public string? Form { get; set; }

    // SEL-API-09
    /// <summary>Gets or sets the browser autofill hint of the hidden input.</summary>
    /// <value>An <c>autocomplete</c> token, or <see langword="null"/>.</value>
    [Parameter]
    public string? AutoComplete { get; set; }

    // SEL-API-13
    /// <summary>Gets or sets a value indicating whether the select ignores user interaction.</summary>
    /// <value><see langword="false"/> by default; an enclosing field's disabled state also disables it.</value>
    [Parameter]
    public bool Disabled { get; set; }

    // SEL-API-12
    /// <summary>Gets or sets a value indicating whether the value is locked; the popup still opens and can be browsed.</summary>
    /// <value><see langword="false"/> by default.</value>
    [Parameter]
    public bool ReadOnly { get; set; }

    // SEL-API-11
    /// <summary>Gets or sets a value indicating whether a value must be chosen before a form submits.</summary>
    /// <value><see langword="false"/> by default.</value>
    [Parameter]
    public bool Required { get; set; }

    // SEL-API-16
    /// <summary>Gets or sets a value indicating whether the popup blocks outside pointer interaction and locks page scroll while open.</summary>
    /// <value><see langword="true"/> by default. Focus is never trapped.</value>
    [Parameter]
    public bool Modal { get; set; } = true;

    // SEL-API-15
    /// <summary>Gets or sets a value indicating whether pointer movement over items highlights them.</summary>
    /// <value><see langword="true"/> by default.</value>
    [Parameter]
    public bool HighlightItemOnHover { get; set; } = true;

    // SEL-API-20
    /// <summary>Gets or sets the function that converts a value to the text the value part shows.</summary>
    /// <value>A function, or <see langword="null"/>, the default, for the item labels, else the value's text.</value>
    [Parameter]
    public Func<TValue, string>? ItemToStringLabel { get; set; }

    // SEL-API-21
    /// <summary>Gets or sets the function that converts a value to the text submitted with a form.</summary>
    /// <value>A function, or <see langword="null"/>, the default, for the value's invariant text.</value>
    [Parameter]
    public Func<TValue, string>? ItemToStringValue { get; set; }

    // SEL-API-22
    /// <summary>Gets or sets the function that decides whether an item's value is the selected value.</summary>
    /// <value>
    /// A function called with the item's value first, never with <see langword="null"/>; or <see langword="null"/>, the
    /// default, which compares strings by value and other objects by reference.
    /// </value>
    [Parameter]
    public Func<TValue, TValue, bool>? IsItemEqualToValue { get; set; }

    // SEL-API-19 (design point N1, settled by the orchestrator 2026-11-04)
    /// <summary>Gets or sets the labels of the values the select can hold, which the value part shows.</summary>
    /// <value>A list of values with their labels, or <see langword="null"/>. Supply at most one of <see cref="Items"/>, <see cref="ItemGroups"/> and <see cref="ItemLabels"/>.</value>
    [Parameter]
    public IReadOnlyList<LabeledValue<TValue>>? Items { get; set; }

    /// <summary>Gets or sets the labels of the values the select can hold, in groups.</summary>
    /// <value>A list of groups, or <see langword="null"/>.</value>
    [Parameter]
    public IReadOnlyList<LabeledValueGroup<TValue>>? ItemGroups { get; set; }

    /// <summary>Gets or sets the labels of the values the select can hold, keyed by each value's text.</summary>
    /// <value>A dictionary from each value's invariant text (<c>"null"</c> for <see langword="null"/>) to its label, or <see langword="null"/>.</value>
    [Parameter]
    public IReadOnlyDictionary<string, string?>? ItemLabels { get; set; }

    /// <summary>Gets or sets the content of the select.</summary>
    /// <value>The trigger, the portal and the other parts, or <see langword="null"/>.</value>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>Gets the root's store.</summary>
    internal SelectStore<TValue> Store => behavior.Store;

    /// <summary>Gets a task that completes when the select's session is attached in the browser.</summary>
    internal Task SessionAttached => session?.TwinAttached ?? Task.CompletedTask;

    /// <summary>Gets the root's field half.</summary>
    internal SelectField Field => fieldHalf!;

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">A parameter of the multiple mode is supplied while <see cref="Multiple"/> is false, or the other way round; or more than one of the item label parameters is supplied.</exception>
    public override Task SetParametersAsync(ParameterView parameters)
    {
        ListValueParameters.Check(parameters, "SelectRoot");
        CheckItems(parameters);
        behavior.EnsureStore(Ids, Logger);
        SelectStore<TValue> store = behavior.Store;
        if (firstSet)
        {
            store.SetDefaultSupplied(parameters.TryGetValue<TValue>(nameof(DefaultValue), out _));
        }

        store.ValueState.SetParameters(parameters, nameof(Value), nameof(DefaultValue));
        store.ValuesState.SetParameters(parameters, nameof(Values), nameof(DefaultValues));
        store.OpenState.SetParameters(parameters, nameof(Open), nameof(DefaultOpen));
        if (session is null)
        {
            session = new InteractionsSessionBehavior(JSRuntime, store.SessionToken!, RouteAsync, DispatchExceptionAsync) { Dismiss = s_dismiss };
            list = new ListNavigationBehavior(store.SessionToken!, session.TwinAttached, RouteAsync, DispatchExceptionAsync);
            list.Matched += match => _ = InvokeAsync(() => store.MatchItemAsync(match.Index, match.Event));
            store.List = list;
            fieldHalf = new SelectField(Formless, JSRuntime, store.FieldChanged, InvokeAsync, DispatchExceptionAsync);
            fieldHalf.Autofilled += report => _ = AutofillAsync(report);
            store.Field = fieldHalf;
            store.BoxedValue = BoxedValue;
            store.Dismissed += OnDismissed;
            store.PositionerRendered = OnPositionerRendered;
            committed = new SelectorSubscription<SelectStoreState>(store, OnCommitted);
            committed.Select(SelectStoreState.Open);
            committed.Select(SelectStoreState.Mounted);
            committed.Select(SelectStoreState.Rendered);
            committed.Select(SelectStoreState.OpenChange);
            committed.Select(SelectStoreState.TransitionStatus);
            committed.Select(SelectStoreState.SelectedIndexSelector);
            committed.Select(SelectStoreState.DisabledSelector);
        }

        return base.SetParametersAsync(parameters);
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        SelectStore<TValue> store = behavior.Store;
        store.OnOpenChange = OnOpenChange;
        store.OpenChanged = OpenChanged;
        store.OnOpenChangeComplete = OnOpenChangeComplete;
        store.OnValueChange = OnValueChange;
        store.ValueChanged = ValueChanged;
        store.OnValuesChange = OnValuesChange;
        store.ValuesChanged = ValuesChanged;
        store.Comparer = IsItemEqualToValue;
        store.ItemToStringLabel = ItemToStringLabel;
        store.ItemToStringValue = ItemToStringValue;
        store.ApplyOpenParameters(store.OpenState.IsControlled ? store.OpenState.Value : null, DefaultOpen);

        // The cascaded field is assigned by the base parameter set, so the field half joins it here, once.
        if (firstSet)
        {
            fieldHalf!.Initialize(FieldContext);
        }

        if (Multiple)
        {
            fieldHalf!.SetParameters(Name, Disabled, FormScope, CascadedEditContext, ValuesExpression);
        }
        else
        {
            fieldHalf!.SetParameters(Name, Disabled, FormScope, CascadedEditContext, ValueExpression);
        }

        bool first = firstSet;
        if (firstSet)
        {
            firstSet = false;
            store.CaptureInitial();
            appliedValue = store.Value;
            appliedHasValue = store.HasValue;
            appliedValues = store.ValuesState.Value;
            appliedComparer = IsItemEqualToValue;
            appliedMultiple = Multiple;
        }

        bool labelsChanged = ApplyLabels(store);
        SelectStoreState state = store.State;
        SelectStoreState next = state with
        {
            RootId = Id ?? state.Lifecycle.FloatingId,
            Disabled = fieldHalf.Disabled,
            ReadOnly = ReadOnly,
            Required = Required,
            Modal = Modal,
            Multiple = Multiple,
            HighlightItemOnHover = HighlightItemOnHover,
            LabelsVersion = labelsChanged ? state.LabelsVersion + 1 : state.LabelsVersion,
        };
        if (!SelectStoreState.ShallowEquals(state, next))
        {
            store.Write(next);
        }

        // The first render's value is the applied one, so its emptiness is computed here, once the mode is in the record.
        if (first && store.ComputeHasSelectedValue())
        {
            store.Write(store.State with { HasSelectedValue = true });
        }

        // SEL-EVT-06: a controlled value the root received renders the label, the selection and the field effects.
        if (appliedHasValue != store.HasValue || !Equality.Is(appliedValue, store.Value) || !ReferenceEquals(appliedValues, store.ValuesState.Value))
        {
            appliedValue = store.Value;
            appliedHasValue = store.HasValue;
            appliedValues = store.ValuesState.Value;
            store.ValueApplied();
        }
        else if (!ReferenceEquals(appliedComparer, IsItemEqualToValue) || appliedMultiple != Multiple || labelsChanged)
        {
            store.Write(store.State with { HasSelectedValue = store.ComputeHasSelectedValue() });
            store.SyncSelectedIndex();
        }

        appliedComparer = IsItemEqualToValue;
        appliedMultiple = Multiple;
        session!.ParentToken = FloatingParent?.SessionToken;
        SetListOptions();
        cascade ??= list!.Context.Cascade(listContent);
        behavior.ChildContent = cascade;
    }

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder) => content(builder);

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        rendered = true;
        CopySessionInputs();
        Task sync = session!.AfterRenderAsync();
        Task listSync = list!.FlushAsync();
        await behavior.OnAfterRenderAsync();
        await sync;
        await listSync;
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        behavior.Dispose();
        committed?.Dispose();
        list?.Dispose();
        fieldHalf?.Dispose();
        if (session is not null)
        {
            await session.DisposeAsync();
        }
    }

    // SEL-API-17, SEL-DIS-08 (actionsRef.close): only while open; closed, it asks nothing.
    /// <summary>Closes the popup, with the reason <see cref="EventReason.ImperativeAction"/>; while closed it does nothing.</summary>
    /// <returns>A task that completes when the close request has run.</returns>
    public Task CloseAsync() => InvokeAsync(() => behavior.Store.IsOpen ? behavior.Store.CloseAsync() : Task.CompletedTask);

    // SEL-API-17, SEL-ANI-08 (actionsRef.unmount).
    /// <summary>Ends the closing phase of a select whose close called <c>PreventUnmountOnClose()</c>.</summary>
    public void Unmount()
    {
        Task task = InvokeAsync(() => behavior.Store.UnmountAsync());
        if (!task.IsCompletedSuccessfully)
        {
            _ = task.ContinueWith(t => DispatchExceptionAsync(t.Exception!.GetBaseException()), CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default);
        }
    }

    /// <summary>Gets the value an onBlur commit and the registration take, boxed once per value.</summary>
    /// <returns>The raw value: the value, <see langword="null"/>, or the list of values.</returns>
    internal object? BoxedValue()
    {
        SelectStore<TValue> store = behavior.Store;
        if (store.State.Multiple)
        {
            IReadOnlyList<TValue> values = store.Values;
            if (!boxed || !ReferenceEquals(boxedFor, values))
            {
                boxed = true;
                boxedFor = values;
                boxedValue = values;
            }

            return boxedValue;
        }

        if (!store.HasValue || ValueNull.IsNull(store.Value))
        {
            return null;
        }

        TValue value = store.Value;
        if (!boxed || boxedFor is not TValue previous || !Equality.Is(previous, value))
        {
            boxed = true;
            boxedValue = value;
            boxedFor = boxedValue;
        }

        return boxedValue;
    }

    // SEL-API-19: at most one of the three label forms (design point N1).
    private static void CheckItems(in ParameterView parameters)
    {
        int supplied = 0;
        foreach (ParameterValue parameter in parameters)
        {
            if (parameter.Name is nameof(Items) or nameof(ItemGroups) or nameof(ItemLabels) && parameter.Value is not null)
            {
                supplied++;
            }
        }

        if (supplied > 1)
        {
            throw new InvalidOperationException("Blazix: `SelectRoot` received more than one of `Items`, `ItemGroups` and `ItemLabels`. Supply the labels in one form.");
        }
    }

    // SEL-API-19 (design point N1): the public forms adapted to track 4's view once per change, never per render.
    private bool ApplyLabels(SelectStore<TValue> store)
    {
        object? source = (object?)Items ?? (object?)ItemGroups ?? ItemLabels;
        bool changed = !ReferenceEquals(source, labelsSource) || !ReferenceEquals(labelFunction, ItemToStringLabel);
        if (!ReferenceEquals(source, labelsSource))
        {
            labelsSource = source;
            store.Labels = Items is { } flat
                ? LabeledItems<TValue>.Flat(new LabeledValueList<TValue>(flat))
                : ItemGroups is { } groups
                    ? LabeledItems<TValue>.Grouped(new LabeledValueGroupList<TValue>(groups))
                    : ItemLabels is { } keyed ? LabeledItems<TValue>.Keyed(keyed) : default;
        }

        labelFunction = ItemToStringLabel;
        return changed;
    }

    private void SetListOptions()
    {
        SelectStoreState state = behavior.Store.State;
        ListNavigationBehavior navigation = list!;
        navigation.Enabled = !state.Disabled;
        navigation.FocusItemOnHover = HighlightItemOnHover;
        // SEL-STA-07, SEL-ANI-14: the highlight stays on its item while the popup animates out.
        navigation.RetainHighlightWhileClosing = true;
        navigation.DisabledIndices = s_noDisabledIndices;
        navigation.SelectedIndex = state.SelectedIndex;

        // SEL-KEY-04, SEL-KEY-12 (SelectRoot.tsx:391): on while open, or closed in single mode while not read-only.
        navigation.SetTypeahead(!state.Disabled && (state.Lifecycle.EffectiveOpen || (!state.ReadOnly && !state.Multiple)), TypeaheadResetMs, TypeaheadSkip);
    }

    private void RenderListContent(RenderTreeBuilder builder)
    {
        builder.AddContent(0, ChildContent);
        builder.OpenComponent<SelectHiddenInputs<TValue>>(1);
        builder.AddComponentParameter(2, nameof(SelectHiddenInputs<TValue>.Root), this);
        builder.AddComponentParameter(3, nameof(SelectHiddenInputs<TValue>.Name), Name);
        builder.AddComponentParameter(4, nameof(SelectHiddenInputs<TValue>.Form), Form);
        builder.AddComponentParameter(5, nameof(SelectHiddenInputs<TValue>.AutoComplete), AutoComplete);
        builder.CloseComponent();
    }

    // An interactions or list request: a press's interaction type is the open method (PSH-PTR-01/02).
    private Task RouteAsync(OpenChangeRequest request)
    {
        SelectStore<TValue> store = behavior.Store;
        if (request.InteractionType is { } type)
        {
            store.SetOpenInteractionType(type);
        }

        return store.RequestOpenAsync(request.Open, request.Details);
    }

    // SEL-FOC-10, SEL-FRM-03, SEL-FRM-07 (SelectRoot.tsx:303-313).
    private void OnDismissed() => _ = fieldHalf!.HandleDismissAsync(BoxedValue());

    // SEL-FRM-18, SEL-FRM-19 (SelectRoot.tsx:525-568): force-mount, then the match by value or label text; the
    // force-mount's render runs inside this dispatch, so the items have registered when the match runs.
    private async Task AutofillAsync(FormAutofillReport report)
    {
        await InvokeAsync(async () =>
        {
            SelectStore<TValue> store = behavior.Store;
            SelectStoreState state = store.State;
            if (state.Disabled || state.ReadOnly || state.Multiple || report.DefaultPrevented)
            {
                return;
            }

            store.ForceMount();
            if (store.MatchAutofill(report.Text) is { } match)
            {
                if (await store.RequestValueAsync(match.Value, new ChangeEventDetails(EventReason.None, report.Event)))
                {
                    fieldHalf!.SendValue(ValueLabels.StringifyAsValue(store.Value, ItemToStringValue));
                }
            }
        });
    }

    // The commit path sends the session and the list their options without a render of the Root (ADR 01 Amendments
    // 2026-10-27). A popup kept mounted while closed is hidden until the positioner's render of the open commit, and the
    // list reads its items' visibility when it learns of the open (CMP-ALG-03, CMP-STA-37), so that open reaches the
    // session and the list from the positioner's next after-render, as Base UI's effects follow the commit's render. Only
    // while the popup's list handle is already attached: a handle that attaches in that window would sync with the
    // session still closed and drop the key the open highlights by (CMP-STA-38), so an open that races the trigger's
    // force-mount goes out at once and the handle attaches to an open session.
    private void OnCommitted()
    {
        if (!rendered)
        {
            return;
        }

        SelectStore<TValue> store = behavior.Store;
        SelectStoreState state = store.State;
        bool open = state.Lifecycle.EffectiveOpen;
        bool keptMounted = renderedWhileClosed;
        renderedWhileClosed = !open && SelectStoreState.Rendered(state);
        if (open && (openDeferred || (keptMounted && !session!.Open && store.PositionerElement is not null && store.PopupListAttached)))
        {
            openDeferred = true;
            return;
        }

        openDeferred = false;
        FlushSession();
    }

    private void OnPositionerRendered()
    {
        if (openDeferred)
        {
            openDeferred = false;
            FlushSession();
        }
    }

    private void FlushSession()
    {
        CopySessionInputs();
        _ = session!.AfterRenderAsync();
        SetListOptions();
        _ = list!.FlushAsync();
    }

    private void CopySessionInputs()
    {
        PopupLifecycleState lifecycle = behavior.Store.Lifecycle;
        session!.Open = lifecycle.EffectiveOpen;

        // The trigger is the floating root's reference whether open or not (SelectRoot.tsx's one trigger), so a second
        // press on it is a press on the active trigger, which toggles (FLT-ALG-16).
        session.ActiveTriggerId = behavior.Store.State.TriggerId;
        session.FloatingId = lifecycle.FloatingId;
        session.Mounted = lifecycle.Mounted;
        session.Ending = lifecycle.DerivedTransitionStatus == TransitionStatus.Ending;
        session.Relay = lifecycle.OpenChange;
    }
}

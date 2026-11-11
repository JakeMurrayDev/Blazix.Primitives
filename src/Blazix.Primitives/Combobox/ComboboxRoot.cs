using System.Globalization;
using System.Linq.Expressions;
using Blazix.Primitives.Rendering;
using Blazix.Primitives.Utilities;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace Blazix.Primitives;

// Base UI's Combobox.Root (combobox/root/ComboboxRoot.tsx; CMB §3.1): a sealed thin part over ComboboxRootBehavior, the
// engine in single or multiple mode (ADR 08 Amendments 2026-11-03 (1)-(3)). The value triples are the probe's (ADR 08
// Amendments 2026-11-03 (1), its line of 2026-11-04); ListValueParameters.Check runs first in SetParametersAsync. The engine
// props Combobox hides (§16 Q4: the ARIA autoComplete mode, fillInputOnItemPress, submitOnItemClick, keepHighlight,
// autoHighlight 'always') keep their engine defaults. It renders no element (CMB-STR-01); CMB-API-36's actions are @ref
// methods (ADR 08 Amendments 2026-10-27).
/// <summary>
/// Groups all parts of a combobox, owns its selected value, its input text and its open state. It renders no element of its
/// own, only its parts and the hidden input that carries the value to a form.
/// </summary>
/// <typeparam name="TValue">The type of the items' values; inferred from <see cref="Value"/>, <see cref="Values"/>, <see cref="Items"/> or their defaults.</typeparam>
/// <remarks>
/// <para>
/// One value: <c>@bind-Value</c>, or <see cref="Value"/>/<see cref="DefaultValue"/> with <see cref="OnValueChange"/>. Several
/// values: <c>Multiple="true"</c> with <c>@bind-Values</c> (an <see cref="IReadOnlyList{T}"/> field), or
/// <see cref="Values"/>/<see cref="DefaultValues"/> with <see cref="OnValuesChange"/>. Supplying a parameter of the other mode
/// is an error. A combobox whose root sets none of these, or only a method-group <see cref="OnValueChange"/>, writes
/// <c>TValue="…"</c>; items in another component or in a <c>RenderFragment</c> field write <c>TValue</c> or their own value,
/// of the root's exact type.
/// </para>
/// <para>
/// <see langword="null"/> selects nothing; use a nullable <typeparamref name="TValue"/> for a value type that can be cleared.
/// <see cref="OnValueChange"/>, <see cref="OnValuesChange"/> and <see cref="OnInputValueChange"/> receive a
/// <see cref="ComboboxChangeEventDetails"/> as their arguments' <c>Details</c>.
/// </para>
/// </remarks>
[CascadingTypeParameter(nameof(TValue))]
public sealed class ComboboxRoot<TValue> : ComponentBase, IAsyncDisposable
{
    private static readonly Func<EventReason, EventSnapshot?, EventTrigger?, bool?, ChangeEventDetails> s_details =
        static (reason, @event, trigger, isItemPress) => new ComboboxChangeEventDetails(reason, @event, trigger) { IsItemPress = isItemPress };

    private readonly ComboboxRootInputs<TValue> inputs = new() { CreateChangeDetails = s_details };

    private ComboboxRootBehavior<TValue>? behavior;

    private Func<TValue?, HighlightEventDetails, Task>? highlighted;

    private bool filterSupplied;

    [Inject]
    private PartIdGenerator IdGenerator { get; set; } = null!;

    [Inject]
    private ILogger<ComboboxRoot<TValue>> Logger { get; set; } = null!;

    [Inject]
    private IJSRuntime JSRuntime { get; set; } = null!;

    // Created at the first parameter set, once the services are injected.
    private ComboboxRootBehavior<TValue> Behavior => behavior ?? throw new InvalidOperationException("Blazix: `ComboboxRoot` is used before its first parameter set.");

    [CascadingParameter]
    private IFloatingNodeContext? FloatingParent { get; set; }

    [CascadingParameter]
    private FieldRootContext? Field { get; set; }

    [CascadingParameter]
    private EditContext? EditContext { get; set; }

    [CascadingParameter]
    private IFormScope? FormScope { get; set; }

    [CascadingParameter]
    private DirectionProviderContext? Direction { get; set; }

    // CMB-API-01
    /// <summary>Gets or sets the selected value, for <c>@bind-Value</c>.</summary>
    /// <value>Leave it unset for an uncontrolled combobox; once set at the first render, the combobox follows it.</value>
    [Parameter]
    public TValue Value { get; set; } = default!;

    /// <summary>Gets or sets the callback <c>@bind-Value</c> uses.</summary>
    [Parameter]
    public EventCallback<TValue> ValueChanged { get; set; }

    // CMB-API-02
    /// <summary>Gets or sets the value selected initially in an uncontrolled combobox.</summary>
    /// <value>Leave it unset to select nothing.</value>
    [Parameter]
    public TValue DefaultValue { get; set; } = default!;

    // CMB-API-03
    /// <summary>Gets or sets the callback called when the selected value is about to change, before the change is applied.</summary>
    /// <value>The arguments' <c>Value</c> is the requested value; their <c>Details</c>, a <see cref="ComboboxChangeEventDetails"/>, carry the reason and the event, and can cancel the change.</value>
    [Parameter]
    public EventCallback<ValueChangeEventArgs<TValue>> OnValueChange { get; set; }

    /// <summary>Gets or sets the expression <c>@bind-Value</c> supplies, which an edit context reads.</summary>
    [Parameter]
    public Expression<Func<TValue>>? ValueExpression { get; set; }

    /// <summary>Gets or sets the selected values while <see cref="Multiple"/>, for <c>@bind-Values</c>.</summary>
    /// <value>Leave it unset for uncontrolled values; once set at the first render, the combobox follows it. <see langword="null"/> is no values.</value>
    [Parameter]
    public IReadOnlyList<TValue>? Values { get; set; }

    /// <summary>Gets or sets the callback <c>@bind-Values</c> uses.</summary>
    [Parameter]
    public EventCallback<IReadOnlyList<TValue>> ValuesChanged { get; set; }

    /// <summary>Gets or sets the values selected initially while <see cref="Multiple"/>.</summary>
    [Parameter]
    public IReadOnlyList<TValue>? DefaultValues { get; set; }

    /// <summary>Gets or sets the callback called when the selected values are about to change, with the new list.</summary>
    [Parameter]
    public EventCallback<ValueChangeEventArgs<IReadOnlyList<TValue>>> OnValuesChange { get; set; }

    /// <summary>Gets or sets the expression <c>@bind-Values</c> supplies, which an edit context reads.</summary>
    [Parameter]
    public Expression<Func<IReadOnlyList<TValue>>>? ValuesExpression { get; set; }

    // CMB-API-04
    /// <summary>Gets or sets a value indicating whether several values can be selected.</summary>
    /// <value>The default is <see langword="false"/>. It does not change for the combobox's lifetime.</value>
    [Parameter]
    public bool Multiple { get; set; }

    // CMB-API-05..08
    /// <summary>Gets or sets a value indicating whether the popup is open, for <c>@bind-Open</c>.</summary>
    [Parameter]
    public bool Open { get; set; }

    /// <summary>Gets or sets the callback <c>@bind-Open</c> uses.</summary>
    [Parameter]
    public EventCallback<bool> OpenChanged { get; set; }

    /// <summary>Gets or sets a value indicating whether an uncontrolled popup is open initially.</summary>
    [Parameter]
    public bool DefaultOpen { get; set; }

    /// <summary>Gets or sets the callback called when the popup is about to open or close, before the change is applied.</summary>
    [Parameter]
    public EventCallback<ValueChangeEventArgs<bool>> OnOpenChange { get; set; }

    /// <summary>Gets or sets the callback called after the popup's open or close animations finish.</summary>
    [Parameter]
    public EventCallback<bool> OnOpenChangeComplete { get; set; }

    // CMB-API-09..11
    /// <summary>Gets or sets the input's text, for <c>@bind-InputValue</c>.</summary>
    /// <value>
    /// Supplied at the first parameter set, the text is controlled; <see langword="null"/> shows the label derived from the
    /// selection. Leave it unset for an uncontrolled text.
    /// </value>
    [Parameter]
    public string? InputValue { get; set; }

    /// <summary>Gets or sets the callback <c>@bind-InputValue</c> uses.</summary>
    [Parameter]
    public EventCallback<string> InputValueChanged { get; set; }

    /// <summary>Gets or sets the input's initial text in an uncontrolled combobox.</summary>
    /// <value><see langword="null"/>, the default, for the selected value's label in single mode and the empty string otherwise.</value>
    [Parameter]
    public string? DefaultInputValue { get; set; }

    /// <summary>Gets or sets the callback called when the input's text is about to change, before the change is applied.</summary>
    [Parameter]
    public EventCallback<ValueChangeEventArgs<string>> OnInputValueChange { get; set; }

    // CMB-API-12, 13 (design point 2 (a), settled by the orchestrator 2026-11-04)
    /// <summary>Gets or sets the items the list shows and filters.</summary>
    [Parameter]
    public IReadOnlyList<TValue>? Items { get; set; }

    /// <summary>Gets or sets the items as groups, instead of <see cref="Items"/>.</summary>
    [Parameter]
    public IReadOnlyList<ComboboxItemGroup<TValue>>? ItemGroups { get; set; }

    /// <summary>Gets or sets items filtered outside the combobox, which the list shows instead of its own filtering.</summary>
    [Parameter]
    public IReadOnlyList<TValue>? FilteredItems { get; set; }

    /// <summary>Gets or sets groups filtered outside the combobox, instead of <see cref="FilteredItems"/>.</summary>
    [Parameter]
    public IReadOnlyList<ComboboxItemGroup<TValue>>? FilteredItemGroups { get; set; }

    // CMB-API-14 (design point 3 (d), settled by the orchestrator 2026-11-04)
    /// <summary>Gets or sets the filter that decides which items match the query: the item, the query and the item's label converter.</summary>
    /// <value>
    /// Leave it unset for the default, which matches the query anywhere in an item's label, ignoring case, marks, symbols,
    /// kana type and width under <see cref="Culture"/>. Set it to <see langword="null"/> to show every item.
    /// </value>
    [Parameter]
    public Func<TValue, string, Func<TValue, string>?, bool>? Filter { get; set; }

    // CMB-API-15
    /// <summary>Gets or sets the culture of the default filter.</summary>
    /// <value><see langword="null"/>, the default, for <see cref="CultureInfo.CurrentCulture"/>.</value>
    [Parameter]
    public CultureInfo? Culture { get; set; }

    // CMB-API-16
    /// <summary>Gets or sets the most items the filter keeps.</summary>
    /// <value>The default is <c>-1</c>, which keeps all.</value>
    [Parameter]
    public int Limit { get; set; } = -1;

    // CMB-API-17..20
    /// <summary>Gets or sets a value indicating whether the first item is highlighted after the user types.</summary>
    [Parameter]
    public bool AutoHighlight { get; set; }

    /// <summary>Gets or sets a value indicating whether moving the pointer over an item highlights it.</summary>
    /// <value>The default is <see langword="true"/>.</value>
    [Parameter]
    public bool HighlightItemOnHover { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether the keyboard highlight wraps around the list.</summary>
    /// <value>The default is <see langword="true"/>.</value>
    [Parameter]
    public bool LoopFocus { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether pressing the input opens the popup.</summary>
    /// <value>The default is <see langword="true"/>.</value>
    [Parameter]
    public bool OpenOnInputClick { get; set; } = true;

    // CMB-API-21..24
    /// <summary>Gets or sets a value indicating whether the items are laid out in rows, navigated in two dimensions.</summary>
    [Parameter]
    public bool Grid { get; set; }

    /// <summary>Gets or sets a value indicating whether the list renders only a window of its items.</summary>
    [Parameter]
    public bool Virtualized { get; set; }

    /// <summary>Gets or sets a value indicating whether the list renders without a popup, always expanded.</summary>
    [Parameter]
    public bool Inline { get; set; }

    /// <summary>Gets or sets a value indicating whether the popup locks page scroll and outside pointer interaction while open.</summary>
    [Parameter]
    public bool Modal { get; set; }

    // CMB-API-25..31
    /// <summary>Gets or sets the name the value is submitted under.</summary>
    [Parameter]
    public string? Name { get; set; }

    /// <summary>Gets or sets the id of the form the hidden input belongs to.</summary>
    [Parameter]
    public string? Form { get; set; }

    /// <summary>Gets or sets the combobox's id, rendered on the input (or the trigger when the input is inside the popup).</summary>
    [Parameter]
    public string? Id { get; set; }

    /// <summary>Gets or sets a value indicating whether a value is required.</summary>
    [Parameter]
    public bool Required { get; set; }

    /// <summary>Gets or sets a value indicating whether the value cannot be changed; the popup still opens and can be browsed.</summary>
    [Parameter]
    public bool ReadOnly { get; set; }

    /// <summary>Gets or sets a value indicating whether the combobox is disabled.</summary>
    [Parameter]
    public bool Disabled { get; set; }

    /// <summary>Gets or sets the hidden input's <c>autocomplete</c>.</summary>
    [Parameter]
    public string? AutoComplete { get; set; }

    // CMB-API-32..34
    /// <summary>Gets or sets the label an item's value shows, filters by and fills the input with.</summary>
    /// <value><see langword="null"/>, the default, for the value's invariant text.</value>
    [Parameter]
    public Func<TValue, string>? ItemToStringLabel { get; set; }

    /// <summary>Gets or sets the text a value submits with a form.</summary>
    /// <value><see langword="null"/>, the default, for the value's invariant text.</value>
    [Parameter]
    public Func<TValue, string>? ItemToStringValue { get; set; }

    /// <summary>Gets or sets the equality of an item's value and a selected value, called for values that are not <see langword="null"/>.</summary>
    /// <value><see langword="null"/>, the default, for reference equality, and equality of value types.</value>
    [Parameter]
    public Func<TValue, TValue, bool>? IsItemEqualToValue { get; set; }

    // CMB-API-37 (design point 8, settled by the orchestrator 2026-11-04)
    /// <summary>Gets or sets the callback called when the highlighted item changes.</summary>
    [Parameter]
    public EventCallback<ComboboxItemHighlightedEventArgs<TValue>> OnItemHighlighted { get; set; }

    /// <summary>Gets or sets the combobox's parts.</summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    // For the fixtures: completes once the session handle is attached.
    internal Task SessionAttached => behavior?.SessionAttached ?? Task.CompletedTask;

    // For the tests and fixtures.
    internal ComboboxStore<TValue> Store => Behavior.Store;

    /// <inheritdoc />
    public override Task SetParametersAsync(ParameterView parameters)
    {
        // ADR 08 Amendments 2026-11-03 (1): the mode mismatch is an always-on error, checked before any assignment.
        ListValueParameters.Check(parameters, "ComboboxRoot");
        behavior ??= new ComboboxRootBehavior<TValue>("Combobox", JSRuntime, StateHasChanged, InvokeAsync, DispatchExceptionAsync);
        behavior.SetParameters(parameters, ComboboxValueNames.Combobox, IdGenerator, Logger);
        filterSupplied = parameters.TryGetValue<Func<TValue, string, Func<TValue, string>?, bool>?>(nameof(Filter), out _);
        return base.SetParametersAsync(parameters);
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        inputs.SelectionMode = Multiple ? ComboboxSelectionMode.Multiple : ComboboxSelectionMode.Single;
        inputs.AutoHighlight = AutoHighlight ? ComboboxAutoHighlightMode.InputChange : ComboboxAutoHighlightMode.Off;
        inputs.DefaultOpen = DefaultOpen;
        inputs.OnOpenChange = OnOpenChange;
        inputs.OpenChanged = OpenChanged;
        inputs.OnOpenChangeComplete = OnOpenChangeComplete;
        inputs.DefaultValue = DefaultValue;
        inputs.OnValueChange = OnValueChange;
        inputs.ValueChanged = ValueChanged;
        inputs.ValueExpression = ValueExpression;
        inputs.DefaultValues = DefaultValues;
        inputs.OnValuesChange = OnValuesChange;
        inputs.ValuesChanged = ValuesChanged;
        inputs.ValuesExpression = ValuesExpression;
        inputs.DefaultInputValue = DefaultInputValue;
        inputs.OnInputValueChange = OnInputValueChange;
        inputs.InputValueChanged = InputValueChanged;
        inputs.Items = Items;
        inputs.ItemGroups = ItemGroups;
        inputs.FilteredItems = FilteredItems;
        inputs.FilteredItemGroups = FilteredItemGroups;
        inputs.Filter = Filter;
        inputs.FilterSupplied = filterSupplied;
        inputs.Culture = Culture;
        inputs.Limit = Limit;
        inputs.HighlightItemOnHover = HighlightItemOnHover;
        inputs.LoopFocus = LoopFocus;
        inputs.OpenOnInputClick = OpenOnInputClick;
        inputs.Grid = Grid;
        inputs.Virtualized = Virtualized;
        inputs.Inline = Inline;
        inputs.Modal = Modal;
        inputs.Required = Required;
        inputs.ReadOnly = ReadOnly;
        inputs.Disabled = Disabled;
        inputs.Name = Field?.Name ?? Name;
        inputs.Form = Form;
        inputs.Id = Id;
        inputs.FormAutoComplete = AutoComplete;
        inputs.ItemToStringLabel = ItemToStringLabel;
        inputs.ItemToStringValue = ItemToStringValue;
        inputs.IsItemEqualToValue = IsItemEqualToValue;
        inputs.ItemHighlighted = OnItemHighlighted.HasDelegate ? highlighted ??= InvokeHighlightedAsync : null;
        inputs.ChildContent = ChildContent;
        Behavior.ApplyParameters(inputs, FloatingParent, Field, FormScope, EditContext, Direction?.Direction == TextDirection.Rtl);
    }

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder) => Behavior.BuildRenderTree(builder);

    /// <inheritdoc />
    protected override Task OnAfterRenderAsync(bool firstRender) => Behavior.OnAfterRenderAsync();

    /// <inheritdoc />
    public ValueTask DisposeAsync() => behavior?.DisposeAsync() ?? ValueTask.CompletedTask;

    // CMB-API-36: actionsRef.close().
    /// <summary>Closes the popup, with the reason <see cref="EventReason.ImperativeAction"/>.</summary>
    /// <returns>A task that completes when the close request has run.</returns>
    public Task CloseAsync() => Behavior.CloseAsync();

    // CMB-API-36: actionsRef.unmount().
    /// <summary>
    /// Removes the popup at once, ending its close animation or a close kept mounted by
    /// <see cref="OpenChangeEventDetails.PreventUnmountOnClose"/>. While the popup is open it does nothing until it closes.
    /// </summary>
    public void Unmount()
    {
        Task unmounting = Behavior.UnmountAsync();
        if (!unmounting.IsCompleted)
        {
            _ = ObserveAsync(unmounting);
        }
    }

    private Task InvokeHighlightedAsync(TValue? value, HighlightEventDetails details) =>
        OnItemHighlighted.InvokeAsync(new ComboboxItemHighlightedEventArgs<TValue>(value, details));

    private async Task ObserveAsync(Task task)
    {
        try
        {
            await task;
        }
        catch (Exception exception) when (!task.IsCanceled)
        {
            await DispatchExceptionAsync(exception);
        }
    }
}

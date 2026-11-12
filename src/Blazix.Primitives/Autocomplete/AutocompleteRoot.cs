using System.Globalization;
using System.Linq.Expressions;
using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace Blazix.Primitives;

// AUT-API-02 (autocomplete/root/AutocompleteRoot.tsx:181-196): the `mode` prop, beside its first consumer (M5 track 11's
// dated ADR 08 §3 line). It is the engine's ARIA autoComplete mode (AUT-STR-03).
/// <summary>Specifies how an autocomplete filters its list and whether the highlighted item completes the text.</summary>
public enum AutocompleteMode
{
    /// <summary>The list is filtered by the text; the text does not follow the highlighted item.</summary>
    List,

    /// <summary>The list is filtered by the text, which shows the highlighted item's text while it is highlighted.</summary>
    Both,

    /// <summary>The list is not filtered; the text shows the highlighted item's text while it is highlighted.</summary>
    Inline,

    /// <summary>The list is not filtered, and the text does not follow the highlighted item.</summary>
    None,
}

// Base UI's Autocomplete.Root (autocomplete/root/AutocompleteRoot.tsx; AUT §3.1): its own part over ComboboxRootBehavior, the
// engine in None mode (AUT-STR-03; decision 12, ADR 08 Amendments 2026-11-03 (3)). The text is the value (AUT-API-01..03):
// Value/ValueChanged/DefaultValue/OnValueChange/ValueExpression, controlled iff Value is supplied at the first parameter set.
// The engine inputs are rebuilt at every set, so a Mode change applies at run time. Not accepted (autocomplete.md:198):
// Multiple, the Values triple, the InputValue triple, ItemToStringLabel, IsItemEqualToValue, AutoComplete. It renders no
// element (AUT-STR-03); the actions are @ref methods (ADR 08 Amendments 2026-10-27).
/// <summary>
/// Groups all parts of an autocomplete and owns its text and its open state. It renders no element of its own, only its
/// parts and, when the input is inside the popup, the hidden input that carries the text to a form.
/// </summary>
/// <typeparam name="TValue">The type of the items; inferred from <see cref="Items"/> or the items' values.</typeparam>
/// <remarks>
/// The text is the value: <c>@bind-Value</c> on a <see cref="string"/> field, or <see cref="Value"/>/<see cref="DefaultValue"/>
/// with <see cref="OnValueChange"/>. Items are suggestions; choosing one fills the text with its label.
/// </remarks>
[CascadingTypeParameter(nameof(TValue))]
public sealed class AutocompleteRoot<TValue> : ComponentBase, IAsyncDisposable
{
    // The text names the engine reads while the root's own text path is the engine's (design point 1 of M5 track 11).
    private static readonly ComboboxValueNames s_names = new(null, null, null, null, nameof(Value), nameof(DefaultValue));

    // AUT-STA-07 (AutocompleteRoot.tsx:76-80): the static modes and a null filter show every item.
    private static readonly Func<TValue, string, Func<TValue, string>?, bool> s_every = static (_, _, _) => true;

    private readonly ComboboxRootInputs<TValue> inputs = new() { SelectionMode = ComboboxSelectionMode.None };

    private ComboboxRootBehavior<TValue>? behavior;

    private Func<TValue?, HighlightEventDetails, Task>? highlighted;

    private bool filterSupplied;

    [Inject]
    private PartIdGenerator IdGenerator { get; set; } = null!;

    [Inject]
    private ILogger<AutocompleteRoot<TValue>> Logger { get; set; } = null!;

    [Inject]
    private IJSRuntime JSRuntime { get; set; } = null!;

    // Created at the first parameter set, once the services are injected.
    private ComboboxRootBehavior<TValue> Behavior => behavior ?? throw new InvalidOperationException("Blazix: `AutocompleteRoot` is used before its first parameter set.");

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

    // AUT-API-01, EDGE-01, EDGE-02
    /// <summary>Gets or sets the text; supplying it makes the autocomplete controlled, and <see langword="null"/> reads as an empty text.</summary>
    [Parameter]
    public string? Value { get; set; }

    /// <summary>Gets or sets the callback that receives the new text, for <c>@bind-Value</c>.</summary>
    [Parameter]
    public EventCallback<string> ValueChanged { get; set; }

    /// <summary>Gets or sets the initial text of an uncontrolled autocomplete.</summary>
    [Parameter]
    public string? DefaultValue { get; set; }

    // AUT-API-03, EVT-02
    /// <summary>Gets or sets the callback invoked when the text changes, with the new text and its details.</summary>
    [Parameter]
    public EventCallback<ValueChangeEventArgs<string>> OnValueChange { get; set; }

    /// <summary>Gets or sets the expression that identifies the bound text, for an enclosing <c>EditForm</c>'s validation.</summary>
    [Parameter]
    public Expression<Func<string>>? ValueExpression { get; set; }

    // AUT-API-02
    /// <summary>Gets or sets how the list is filtered and whether the highlighted item completes the text.</summary>
    /// <value>The default is <see cref="AutocompleteMode.List"/>.</value>
    [Parameter]
    public AutocompleteMode Mode { get; set; }

    // AUT-API-04
    /// <summary>Gets or sets a value indicating whether the highlight is kept when the pointer leaves the list.</summary>
    [Parameter]
    public bool KeepHighlight { get; set; }

    /// <summary>Gets or sets a value indicating whether moving the pointer over an item highlights it.</summary>
    /// <value>The default is <see langword="true"/>.</value>
    [Parameter]
    public bool HighlightItemOnHover { get; set; } = true;

    // AUT-API-06
    /// <summary>Gets or sets a value indicating whether a click on the input opens the popup.</summary>
    /// <value>The default is <see langword="false"/>.</value>
    [Parameter]
    public bool OpenOnInputClick { get; set; }

    // AUT-API-08
    /// <summary>Gets or sets the converter from an item to its text, which fills the input and is filtered.</summary>
    /// <value><see langword="null"/>, the default, for the item's invariant text.</value>
    [Parameter]
    public Func<TValue, string>? ItemToStringValue { get; set; }

    // AUT-API-11
    /// <summary>Gets or sets the suggestions the list renders and filters.</summary>
    [Parameter]
    public IReadOnlyList<TValue>? Items { get; set; }

    /// <summary>Gets or sets the suggestions in groups.</summary>
    [Parameter]
    public IReadOnlyList<ComboboxItemGroup<TValue>>? ItemGroups { get; set; }

    /// <summary>Gets or sets suggestions filtered by the consumer, which the list renders in place of its own filtering.</summary>
    [Parameter]
    public IReadOnlyList<TValue>? FilteredItems { get; set; }

    /// <summary>Gets or sets grouped suggestions filtered by the consumer.</summary>
    [Parameter]
    public IReadOnlyList<ComboboxItemGroup<TValue>>? FilteredItemGroups { get; set; }

    // AUT-API-09
    /// <summary>Gets or sets the filter that decides which items match the text: the item, the query and <see cref="ItemToStringValue"/>.</summary>
    /// <value>
    /// Leave it unset for the default, which matches the text anywhere in an item's text, ignoring case, marks, symbols, kana
    /// type and width under <see cref="Culture"/>. Set it to <see langword="null"/> to show every item. The
    /// <see cref="AutocompleteMode.Inline"/> and <see cref="AutocompleteMode.None"/> modes never filter.
    /// </value>
    [Parameter]
    public Func<TValue, string, Func<TValue, string>?, bool>? Filter { get; set; }

    // AUT-API-10
    /// <summary>Gets or sets the culture of the default filter.</summary>
    /// <value><see langword="null"/>, the default, for <see cref="CultureInfo.CurrentCulture"/>.</value>
    [Parameter]
    public CultureInfo? Culture { get; set; }

    /// <summary>Gets or sets the most items the list renders from <see cref="Items"/>.</summary>
    /// <value>The default is <c>-1</c>, which keeps all.</value>
    [Parameter]
    public int Limit { get; set; } = -1;

    // AUT-API-12
    /// <summary>Gets or sets a value indicating whether choosing an item submits the form that owns the autocomplete.</summary>
    [Parameter]
    public bool SubmitOnItemClick { get; set; }

    // AUT-API-15
    /// <summary>Gets or sets the callback invoked when the highlighted item changes, before the text follows it.</summary>
    [Parameter]
    public EventCallback<AutocompleteItemHighlightedEventArgs<TValue>> OnItemHighlighted { get; set; }

    /// <summary>Gets or sets a value indicating whether the popup is open; supplying it makes the open state controlled.</summary>
    [Parameter]
    public bool Open { get; set; }

    /// <summary>Gets or sets the callback that receives the new open state, for <c>@bind-Open</c>.</summary>
    [Parameter]
    public EventCallback<bool> OpenChanged { get; set; }

    /// <summary>Gets or sets a value indicating whether the popup is initially open.</summary>
    [Parameter]
    public bool DefaultOpen { get; set; }

    /// <summary>Gets or sets the callback invoked when the popup opens or closes, with its details.</summary>
    [Parameter]
    public EventCallback<ValueChangeEventArgs<bool>> OnOpenChange { get; set; }

    /// <summary>Gets or sets the callback invoked when the popup's open or close animation completes.</summary>
    [Parameter]
    public EventCallback<bool> OnOpenChangeComplete { get; set; }

    /// <summary>Gets or sets a value indicating whether keyboard navigation wraps from the last item to the first.</summary>
    /// <value>The default is <see langword="true"/>.</value>
    [Parameter]
    public bool LoopFocus { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether the items are laid out in rows navigated in two dimensions.</summary>
    [Parameter]
    public bool Grid { get; set; }

    /// <summary>Gets or sets a value indicating whether the list renders only a window of its items.</summary>
    [Parameter]
    public bool Virtualized { get; set; }

    // AUT-API-16
    /// <summary>Gets or sets a value indicating whether the list is rendered inline, without the popup; set <see cref="Open"/> with it.</summary>
    [Parameter]
    public bool Inline { get; set; }

    /// <summary>Gets or sets a value indicating whether the popup is modal.</summary>
    [Parameter]
    public bool Modal { get; set; }

    /// <summary>Gets or sets the name of the text's form field; an enclosing field's name wins.</summary>
    [Parameter]
    public string? Name { get; set; }

    /// <summary>Gets or sets the id of the form that owns the text when the autocomplete is outside it.</summary>
    [Parameter]
    public string? Form { get; set; }

    /// <summary>Gets or sets the id of the autocomplete's control.</summary>
    [Parameter]
    public string? Id { get; set; }

    /// <summary>Gets or sets a value indicating whether a text is required to submit the form.</summary>
    [Parameter]
    public bool Required { get; set; }

    // AUT-API-17
    /// <summary>Gets or sets a value indicating whether the text cannot be changed; the list can still be browsed.</summary>
    [Parameter]
    public bool ReadOnly { get; set; }

    /// <summary>Gets or sets a value indicating whether the autocomplete is disabled.</summary>
    [Parameter]
    public bool Disabled { get; set; }

    /// <summary>Gets or sets the autocomplete's parts.</summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    // For the fixtures: completes once the session handle is attached.
    internal Task SessionAttached => behavior?.SessionAttached ?? Task.CompletedTask;

    // For the tests and fixtures.
    internal ComboboxStore<TValue> Store => Behavior.Store;

    /// <inheritdoc />
    public override Task SetParametersAsync(ParameterView parameters)
    {
        behavior ??= new ComboboxRootBehavior<TValue>("Autocomplete", JSRuntime, StateHasChanged, InvokeAsync, DispatchExceptionAsync);
        behavior.SetParameters(parameters, s_names, IdGenerator, Logger);
        filterSupplied = parameters.TryGetValue<Func<TValue, string, Func<TValue, string>?, bool>?>(nameof(Filter), out _);
        return base.SetParametersAsync(parameters);
    }

    // AUT-STR-03 (AutocompleteRoot.tsx:107-125): the engine inputs, rebuilt at every set.
    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        bool staticItems = Mode is AutocompleteMode.Inline or AutocompleteMode.None;
        inputs.AriaAutoComplete = Mode switch
        {
            AutocompleteMode.Both => ComboboxAriaAutoComplete.Both,
            AutocompleteMode.Inline => ComboboxAriaAutoComplete.Inline,
            AutocompleteMode.None => ComboboxAriaAutoComplete.None,
            _ => ComboboxAriaAutoComplete.List,
        };
        inputs.FillInputOnItemPress = true;
        inputs.SubmitOnItemClick = SubmitOnItemClick;
        inputs.KeepHighlight = KeepHighlight;
        inputs.DefaultOpen = DefaultOpen;
        inputs.OnOpenChange = OnOpenChange;
        inputs.OpenChanged = OpenChanged;
        inputs.OnOpenChangeComplete = OnOpenChangeComplete;
        inputs.DefaultInputValue = DefaultValue;
        inputs.OnInputValueChange = OnValueChange;
        inputs.InputValueChanged = ValueChanged;
        inputs.InputValueExpression = ValueExpression;
        inputs.Items = Items;
        inputs.ItemGroups = ItemGroups;
        inputs.FilteredItems = FilteredItems;
        inputs.FilteredItemGroups = FilteredItemGroups;
        bool every = staticItems || (filterSupplied && Filter is null);
        inputs.Filter = every ? s_every : Filter;
        inputs.FilterSupplied = every || filterSupplied;
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
        inputs.ItemToStringLabel = ItemToStringValue;
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

    // AUT-API-05: actionsRef.close().
    /// <summary>Closes the popup, with the reason <see cref="EventReason.ImperativeAction"/>.</summary>
    /// <returns>A task that completes when the close request has run.</returns>
    public Task CloseAsync() => Behavior.CloseAsync();

    // AUT-API-05: actionsRef.unmount().
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
        OnItemHighlighted.InvokeAsync(new AutocompleteItemHighlightedEventArgs<TValue>(value, details));

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

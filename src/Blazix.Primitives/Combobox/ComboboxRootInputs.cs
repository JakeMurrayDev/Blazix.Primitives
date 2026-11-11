using System.Globalization;
using System.Linq.Expressions;
using Microsoft.AspNetCore.Components;

namespace Blazix.Primitives;

// Every input of the shared engine (AriaCombobox.tsx:103-146, the props ComboboxRoot.tsx and AutocompleteRoot.tsx pass),
// set by the family's root at every parameter set: Autocomplete's `mode` changes the ARIA autocomplete mode at run time, so
// nothing here is fixed at construction (M5 track 10 design-points note, "Facts that differ"). The callbacks are delegates
// so each family keeps its own details and args types (ComboboxChangeEventDetails; Autocomplete's own).
/// <summary>The inputs of a combobox-family root, read by <see cref="ComboboxRootBehavior{TValue}"/> at every parameter set.</summary>
/// <typeparam name="TValue">The type of the items' values.</typeparam>
internal sealed class ComboboxRootInputs<TValue>
{
    /// <summary>Gets or sets what the engine remembers of an item press.</summary>
    public ComboboxSelectionMode SelectionMode { get; set; }

    /// <summary>Gets or sets the ARIA autocomplete mode; Combobox's is <see cref="ComboboxAriaAutoComplete.List"/>.</summary>
    public ComboboxAriaAutoComplete AriaAutoComplete { get; set; }

    /// <summary>Gets or sets a value indicating whether an item press in <see cref="ComboboxSelectionMode.None"/> fills the input.</summary>
    public bool FillInputOnItemPress { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether an item press submits the owning form.</summary>
    public bool SubmitOnItemClick { get; set; }

    /// <summary>Gets or sets a value indicating whether the highlight survives the pointer leaving the list.</summary>
    public bool KeepHighlight { get; set; }

    /// <summary>Gets or sets when the engine highlights the first item by itself.</summary>
    public ComboboxAutoHighlightMode AutoHighlight { get; set; }

    /// <summary>Gets or sets the query the filter uses instead of the input text, or <see langword="null"/> for the input text.</summary>
    public string? FilterQuery { get; set; }

    /// <summary>Gets or sets a value indicating whether an uncontrolled popup is open initially.</summary>
    public bool DefaultOpen { get; set; }

    /// <summary>Gets or sets the callback called before an open change is applied.</summary>
    public EventCallback<ValueChangeEventArgs<bool>> OnOpenChange { get; set; }

    /// <summary>Gets or sets the callback <c>@bind-Open</c> uses.</summary>
    public EventCallback<bool> OpenChanged { get; set; }

    /// <summary>Gets or sets the callback called after the open or close animations finish.</summary>
    public EventCallback<bool> OnOpenChangeComplete { get; set; }

    /// <summary>Gets or sets the single-mode default value.</summary>
    public TValue? DefaultValue { get; set; }

    /// <summary>Gets or sets the single-mode change callback.</summary>
    public EventCallback<ValueChangeEventArgs<TValue>> OnValueChange { get; set; }

    /// <summary>Gets or sets the callback <c>@bind-Value</c> uses.</summary>
    public EventCallback<TValue> ValueChanged { get; set; }

    /// <summary>Gets or sets the single-mode value's expression, for <c>EditContext</c> participation.</summary>
    public Expression<Func<TValue>>? ValueExpression { get; set; }

    /// <summary>Gets or sets the multiple-mode default values.</summary>
    public IReadOnlyList<TValue>? DefaultValues { get; set; }

    /// <summary>Gets or sets the multiple-mode change callback.</summary>
    public EventCallback<ValueChangeEventArgs<IReadOnlyList<TValue>>> OnValuesChange { get; set; }

    /// <summary>Gets or sets the callback <c>@bind-Values</c> uses.</summary>
    public EventCallback<IReadOnlyList<TValue>> ValuesChanged { get; set; }

    /// <summary>Gets or sets the multiple-mode values' expression, for <c>EditContext</c> participation.</summary>
    public Expression<Func<IReadOnlyList<TValue>>>? ValuesExpression { get; set; }

    /// <summary>Gets or sets the input value's default; <see langword="null"/> derives it.</summary>
    public string? DefaultInputValue { get; set; }

    /// <summary>Gets or sets the input value's change callback.</summary>
    public EventCallback<ValueChangeEventArgs<string>> OnInputValueChange { get; set; }

    /// <summary>Gets or sets the callback <c>@bind-InputValue</c> uses.</summary>
    public EventCallback<string> InputValueChanged { get; set; }

    /// <summary>Gets or sets the input value's expression, for <c>EditContext</c> participation in <see cref="ComboboxSelectionMode.None"/>.</summary>
    public Expression<Func<string>>? InputValueExpression { get; set; }

    /// <summary>Gets or sets the flat items, or <see langword="null"/> when none are given.</summary>
    public IReadOnlyList<TValue>? Items { get; set; }

    /// <summary>Gets or sets the grouped items, or <see langword="null"/> when none are given.</summary>
    public IReadOnlyList<ComboboxItemGroup<TValue>>? ItemGroups { get; set; }

    /// <summary>Gets or sets the externally filtered flat items.</summary>
    public IReadOnlyList<TValue>? FilteredItems { get; set; }

    /// <summary>Gets or sets the externally filtered grouped items.</summary>
    public IReadOnlyList<ComboboxItemGroup<TValue>>? FilteredItemGroups { get; set; }

    /// <summary>Gets or sets the filter: the item, the query and the item's string conversion.</summary>
    public Func<TValue, string, Func<TValue, string>?, bool>? Filter { get; set; }

    /// <summary>Gets or sets a value indicating whether <see cref="Filter"/> was supplied; a supplied <see langword="null"/> turns filtering off.</summary>
    public bool FilterSupplied { get; set; }

    /// <summary>Gets or sets the culture of the default filter, or <see langword="null"/> for the current culture.</summary>
    public CultureInfo? Culture { get; set; }

    /// <summary>Gets or sets the most items the filter keeps; <c>-1</c> keeps all.</summary>
    public int Limit { get; set; } = -1;

    /// <summary>Gets or sets a value indicating whether hovering an item highlights it.</summary>
    public bool HighlightItemOnHover { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether the keyboard highlight wraps.</summary>
    public bool LoopFocus { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether pressing the input opens the popup.</summary>
    public bool OpenOnInputClick { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether the list is a two-dimensional grid.</summary>
    public bool Grid { get; set; }

    /// <summary>Gets or sets a value indicating whether the list renders a window of its items.</summary>
    public bool Virtualized { get; set; }

    /// <summary>Gets or sets a value indicating whether the list renders without a popup.</summary>
    public bool Inline { get; set; }

    /// <summary>Gets or sets a value indicating whether the popup locks page scroll and outside pointer interaction.</summary>
    public bool Modal { get; set; }

    /// <summary>Gets or sets a value indicating whether a value is required.</summary>
    public bool Required { get; set; }

    /// <summary>Gets or sets a value indicating whether the value cannot be changed.</summary>
    public bool ReadOnly { get; set; }

    /// <summary>Gets or sets a value indicating whether the combobox is disabled.</summary>
    public bool Disabled { get; set; }

    /// <summary>Gets or sets the name the value is submitted under.</summary>
    public string? Name { get; set; }

    /// <summary>Gets or sets the id of the form that owns the hidden input.</summary>
    public string? Form { get; set; }

    /// <summary>Gets or sets the root's id.</summary>
    public string? Id { get; set; }

    /// <summary>Gets or sets the hidden input's <c>autocomplete</c>.</summary>
    public string? FormAutoComplete { get; set; }

    /// <summary>Gets or sets the label of a value.</summary>
    public Func<TValue, string>? ItemToStringLabel { get; set; }

    /// <summary>Gets or sets the form value of a value.</summary>
    public Func<TValue, string>? ItemToStringValue { get; set; }

    /// <summary>Gets or sets the equality of an item and a selected value, consulted for non-null operands.</summary>
    public Func<TValue, TValue, bool>? IsItemEqualToValue { get; set; }

    /// <summary>Gets or sets the highlight callback: the highlighted value (or <see langword="default"/>) and the details.</summary>
    public Func<TValue?, HighlightEventDetails, Task>? ItemHighlighted { get; set; }

    /// <summary>Gets or sets the factory of the family's change details: the reason, the event, the trigger and <c>IsItemPress</c>.</summary>
    public Func<EventReason, EventSnapshot?, EventTrigger?, bool?, ChangeEventDetails> CreateChangeDetails { get; set; } = static (reason, @event, trigger, _) => new ChangeEventDetails(reason, @event, trigger);

    /// <summary>Gets or sets the root's parts.</summary>
    public RenderFragment? ChildContent { get; set; }
}

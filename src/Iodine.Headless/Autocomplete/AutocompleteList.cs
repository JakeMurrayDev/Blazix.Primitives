using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace Blazix.Primitives;

// AUT-STR-02, STR-05 (M5 track 11; decision 12, ADR 08 Amendments 2026-11-03 (3)): the Autocomplete parts of this file
// are Combobox's, as sealed shells over the same internal behaviours with the family `Autocomplete`; the comments below
// cite the Combobox rows the Autocomplete document adopts (autocomplete.md §2.1).

// CMB §3.19, STA-12 (combobox/list/AutocompleteList.tsx).
/// <summary>Represents the state of an autocomplete list, which the part exposes as data attributes and passes to its class and style functions.</summary>
/// <param name="Empty">A value indicating whether the filtered list is empty; projected as <c>data-empty</c>.</param>
[PartState]
public sealed partial record AutocompleteListState(
    bool Empty);

// Base UI's Combobox.List (combobox/list/AutocompleteList.tsx; CMB §3.19, STR-06): a sealed thin part over
// ComboboxListBehavior. Its ItemContent and GroupContent are the item template (design point 2 (a), settled by the
// orchestrator 2026-11-04), named after Virtualize.ItemContent; with either, the list maps the root's filtered items as
// Base UI's function children do through the implicit Collection.
/// <summary>Represents the list of an autocomplete's items, Base UI's <c>Autocomplete.List</c>, rendered as a <c>div</c>.</summary>
/// <typeparam name="TValue">The type of the items' values; inferred from the enclosing root.</typeparam>
/// <remarks>
/// Give it <see cref="ItemContent"/> to render one item per filtered item of the root's <c>Items</c>, or
/// <see cref="GroupContent"/> for the root's <c>ItemGroups</c>; or put the items in it directly.
/// </remarks>
[CascadingTypeParameter(nameof(TValue))]
public sealed class AutocompleteList<TValue> : PartBase<AutocompleteListState>
{
    private readonly EventCallback onKeyDown;
    private ComboboxListBehavior<TValue>? behavior;
    private AutocompleteListState state = new(true);
    private RenderFragment? template;

    /// <summary>Initializes a new instance of the <see cref="AutocompleteList{TValue}"/> class.</summary>
    public AutocompleteList()
    {
        onKeyDown = CreateHandler<KeyboardEventArgs>("onkeydown", _ => Task.CompletedTask);
    }

    [Inject]
    private IJSRuntime JSRuntime { get; set; } = null!;

    [Inject]
    private ILogger<AutocompleteList<TValue>> Logger { get; set; } = null!;

    [Inject]
    private PartIdGenerator IdGenerator { get; set; } = null!;

    [CascadingParameter]
    private ComboboxStore? Root { get; set; }

    [CascadingParameter]
    private PopupPortalContext? Portal { get; set; }

    // Base UI resolves an inline list's popup as its closest `[role="dialog"]` (AriaCombobox.tsx:994-1002); the port's stand-in
    // is the nearest floating node: another popup's store, such as a Dialog's, cascaded between the root and the list.
    [CascadingParameter]
    private IFloatingNodeContext? FloatingNode { get; set; }

    /// <summary>Gets or sets the template of an item, rendered once per filtered item with its value and index.</summary>
    [Parameter]
    public RenderFragment<ComboboxListItem<TValue>>? ItemContent { get; set; }

    /// <summary>Gets or sets the template of a group, rendered once per filtered group of the root's <c>ItemGroups</c>.</summary>
    [Parameter]
    public RenderFragment<ComboboxItemGroup<TValue>>? GroupContent { get; set; }

    /// <inheritdoc />
    protected override string DefaultTagName => "div";

    /// <inheritdoc />
    protected override AutocompleteListState State
    {
        get
        {
            bool empty = behavior!.Empty;
            if (state.Empty != empty)
            {
                state = new AutocompleteListState(empty);
            }

            return state;
        }
    }

    // For the fixtures: completes once the list's handles are attached.
    internal Task TwinAttached => behavior?.TwinAttached ?? Task.CompletedTask;

    /// <inheritdoc />
    public override Task SetParametersAsync(ParameterView parameters)
    {
        // The part replaces ChildContent with its own cascade, so a parameter set without content must not keep it.
        ChildContent = null;
        return base.SetParametersAsync(parameters);
    }

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        behavior = new ComboboxListBehavior<TValue>(new ComboboxPartServices("Autocomplete", JSRuntime, Logger, IdGenerator, StateHasChanged, InvokeAsync, DispatchExceptionAsync), onKeyDown);
        template = RenderTemplate;
        base.OnInitialized();
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        ChildContent = behavior!.Apply(AdditionalAttributes, FloatingNode, ChildContent, template!, ItemContent is not null || GroupContent is not null);
    }

    /// <inheritdoc />
    protected override Task OnAfterRenderAsync(bool firstRender) => behavior!.AfterRenderAsync(Element);

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing && behavior is not null)
        {
            _ = behavior.DisposeAsync().AsTask();
        }

        base.Dispose(disposing);
    }

    /// <inheritdoc />
    protected override void SubscribeSelectors() => behavior!.Subscribe(Root, Portal, SubscribeTo);

    /// <inheritdoc />
    protected override bool ForwardsConsumerHandler(string eventName) => behavior!.ForwardsConsumerHandler(eventName);

    /// <inheritdoc />
    protected override void AddPartAttributes(AttributeList attributes) => behavior!.AddPartAttributes(attributes);

    private void RenderTemplate(RenderTreeBuilder builder) => behavior!.BuildTemplate(builder, ItemContent, GroupContent);
}


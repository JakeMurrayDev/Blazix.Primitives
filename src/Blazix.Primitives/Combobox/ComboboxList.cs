using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace Blazix.Primitives;

// CMB §3.19, STA-12 (combobox/list/ComboboxList.tsx).
/// <summary>Represents the state of a combobox list, which the part exposes as data attributes and passes to its class and style functions.</summary>
/// <param name="Empty">A value indicating whether the filtered list is empty; projected as <c>data-empty</c>.</param>
[PartState]
public sealed partial record ComboboxListState(
    bool Empty);

// Base UI's Combobox.List (combobox/list/ComboboxList.tsx; CMB §3.19, STR-06): a sealed thin part over
// ComboboxListBehavior. Its ItemContent and GroupContent are the item template (design point 2 (a), settled by the
// orchestrator 2026-11-04), named after Virtualize.ItemContent; with either, the list maps the root's filtered items as
// Base UI's function children do through the implicit Collection.
/// <summary>Represents the list of a combobox's items, Base UI's <c>Combobox.List</c>, rendered as a <c>div</c>.</summary>
/// <typeparam name="TValue">The type of the items' values; inferred from the enclosing root.</typeparam>
/// <remarks>
/// Give it <see cref="ItemContent"/> to render one item per filtered item of the root's <c>Items</c>, or
/// <see cref="GroupContent"/> for the root's <c>ItemGroups</c>; or put the items in it directly.
/// </remarks>
[CascadingTypeParameter(nameof(TValue))]
public sealed class ComboboxList<TValue> : PartBase<ComboboxListState>
{
    private readonly EventCallback onKeyDown;
    private ComboboxListBehavior<TValue>? behavior;
    private ComboboxListState state = new(true);
    private RenderFragment? template;

    /// <summary>Initializes a new instance of the <see cref="ComboboxList{TValue}"/> class.</summary>
    public ComboboxList()
    {
        onKeyDown = CreateHandler<KeyboardEventArgs>("onkeydown", _ => Task.CompletedTask);
    }

    [Inject]
    private IJSRuntime JSRuntime { get; set; } = null!;

    [Inject]
    private ILogger<ComboboxList<TValue>> Logger { get; set; } = null!;

    [Inject]
    private PartIdGenerator IdGenerator { get; set; } = null!;

    [CascadingParameter]
    private ComboboxStore? Root { get; set; }

    [CascadingParameter]
    private PopupPortalContext? Portal { get; set; }

    /// <summary>Gets or sets the template of an item, rendered once per filtered item with its value and index.</summary>
    [Parameter]
    public RenderFragment<ComboboxListItem<TValue>>? ItemContent { get; set; }

    /// <summary>Gets or sets the template of a group, rendered once per filtered group of the root's <c>ItemGroups</c>.</summary>
    [Parameter]
    public RenderFragment<ComboboxItemGroup<TValue>>? GroupContent { get; set; }

    /// <inheritdoc />
    protected override string DefaultTagName => "div";

    /// <inheritdoc />
    protected override ComboboxListState State
    {
        get
        {
            bool empty = behavior!.Empty;
            if (state.Empty != empty)
            {
                state = new ComboboxListState(empty);
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
        behavior = new ComboboxListBehavior<TValue>(new ComboboxPartServices("Combobox", JSRuntime, Logger, IdGenerator, StateHasChanged, InvokeAsync, DispatchExceptionAsync), onKeyDown);
        template = RenderTemplate;
        base.OnInitialized();
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        string? consumerId = AdditionalAttributes is not null && AdditionalAttributes.TryGetValue("id", out object? supplied) ? supplied as string : null;
        behavior!.Apply(consumerId, AdditionalAttributes);
        ChildContent = behavior.Cascade(ItemContent is not null || GroupContent is not null ? template : ChildContent);
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

// Design point 2 (a), settled by the orchestrator 2026-11-04: the template's argument, the item and its index (Base UI's
// `children(item, index)`).
/// <summary>Represents an item a combobox list's template renders: its value and its index among the filtered items.</summary>
/// <typeparam name="TValue">The type of the items' values.</typeparam>
/// <param name="Value">The item's value.</param>
/// <param name="Index">The item's index among the root's filtered items.</param>
public readonly record struct ComboboxListItem<TValue>(TValue Value, int Index);

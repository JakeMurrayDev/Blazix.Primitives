using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace Blazix.Primitives;

// CMB §3.24, STA-13 (combobox/item/ComboboxItem.tsx): Highlighted is the list handle's data-highlighted from attach (ADR 04 Amendments 2026-11-03 (2)): mirrored.
/// <summary>Represents the state of a combobox item, which the part exposes as data attributes and passes to its class and style functions.</summary>
/// <param name="Disabled">A value indicating whether the item is disabled, by the part or the root; projected as <c>data-disabled</c>.</param>
/// <param name="Selected">A value indicating whether the item's value is selected; projected as <c>data-selected</c>.</param>
/// <param name="Highlighted">A value indicating whether the item is highlighted; its <c>data-highlighted</c> is written by the browser-side behavior.</param>
[PartState]
public sealed partial record ComboboxItemState(
    bool Disabled,
    bool Selected,
    [property: MirroredField] bool Highlighted);

// combobox/item/ComboboxItemDataAttributes.ts (CMB-DOM-09).
/// <summary>Provides the names of the data attributes a combobox item renders.</summary>
public static class ComboboxItemDataAttributes
{
    /// <summary>Represents the attribute present while the item is selected: <c>data-selected</c>.</summary>
    public const string Selected = "data-selected";

    /// <summary>Represents the attribute present while the item is highlighted: <c>data-highlighted</c>.</summary>
    public const string Highlighted = "data-highlighted";

    /// <summary>Represents the attribute present while the item is disabled: <c>data-disabled</c>.</summary>
    public const string Disabled = "data-disabled";
}

// Base UI's Combobox.Item (combobox/item/ComboboxItem.tsx; CMB §3.24): a sealed thin part over ComboboxItemBehavior, which
// Autocomplete's Item shells. An omitted Value is told from default(TValue) by whether it was supplied (ADR 08 Amendments
// 2026-11-03 (2)).
/// <summary>Represents an item of a combobox's list, Base UI's <c>Combobox.Item</c>, rendered as a <c>div</c>.</summary>
/// <typeparam name="TValue">The type of the item's value; inferred from the enclosing root.</typeparam>
/// <remarks>
/// Its <c>id</c> is stable for its lifetime. The highlight follows the keyboard and the pointer while the focus stays in
/// the input. A consumer <c>onclick</c> runs first and can call <c>PreventPartHandler()</c> to skip the selection.
/// </remarks>
[CascadingTypeParameter(nameof(TValue))]
public sealed class ComboboxItem<TValue> : PartBase<ComboboxItemState>
{
    private readonly EventCallback onKeyDown;
    private readonly EventCallback onKeyUp;
    private readonly EventCallback onClick;
    private ComboboxItemBehavior<TValue>? behavior;
    private ComboboxItemState state = new(false, false, false);
    private bool valueSupplied;

    /// <summary>Initializes a new instance of the <see cref="ComboboxItem{TValue}"/> class.</summary>
    public ComboboxItem()
    {
        onKeyDown = CreateHandler<KeyboardEventArgs>("onkeydown", e => behavior!.HandleKeyDownAsync(e));
        onKeyUp = CreateHandler<KeyboardEventArgs>("onkeyup", e => behavior!.HandleKeyUpAsync(e));
        onClick = CreateHandler<MouseEventArgs>("onclick", e => behavior!.HandleClickAsync(e));
    }

    [Inject]
    private IJSRuntime JSRuntime { get; set; } = null!;

    [Inject]
    private ILogger<ComboboxItem<TValue>> Logger { get; set; } = null!;

    [Inject]
    private ButtonDevelopmentLog DevelopmentLog { get; set; } = null!;

    [Inject]
    private PartIdGenerator IdGenerator { get; set; } = null!;

    [CascadingParameter]
    private ComboboxStore? Root { get; set; }

    [CascadingParameter]
    private CompositeRootContext? Composite { get; set; }

    [CascadingParameter]
    private ComboboxRowContext? Row { get; set; }

    // CMB-API-56
    /// <summary>Gets or sets the item's value, which a press selects.</summary>
    [Parameter]
    public TValue Value { get; set; } = default!;

    // CMB-API-57
    /// <summary>Gets or sets the item's index among the filtered items, when the list renders a window of them.</summary>
    /// <value><see langword="null"/>, the default, for the item's position among the rendered items.</value>
    [Parameter]
    public int? Index { get; set; }

    // CMB-API-58
    /// <summary>Gets or sets a value indicating whether the item is disabled; it stays reachable by the keyboard.</summary>
    [Parameter]
    public bool Disabled { get; set; }

    // CMB-API-59
    /// <summary>Gets or sets a value indicating whether the element substituted with <c>Render</c> is a native <c>button</c>.</summary>
    /// <value>The default is <see langword="false"/>.</value>
    [Parameter]
    public bool NativeButton { get; set; }

    /// <inheritdoc />
    protected override string DefaultTagName => "div";

    /// <inheritdoc />
    protected override ComboboxItemState State
    {
        get
        {
            ComboboxItemBehavior<TValue> item = behavior!;
            bool disabled = item.Disabled;
            bool selected = item.Selected;
            bool highlighted = item.Highlighted;
            if (state.Disabled != disabled || state.Selected != selected || state.Highlighted != highlighted)
            {
                state = new ComboboxItemState(disabled, selected, highlighted);
            }

            return state;
        }
    }

    /// <inheritdoc />
    protected override bool UsesGeneratedId => true;

    // For the fixtures and the tests.
    internal Task TwinAttached => behavior?.TwinAttached ?? Task.CompletedTask;

    /// <inheritdoc />
    public override Task SetParametersAsync(ParameterView parameters)
    {
        valueSupplied = parameters.TryGetValue<TValue>(nameof(Value), out _);

        // The part replaces ChildContent with its own cascade, so a parameter set without content must not keep it.
        ChildContent = null;
        return base.SetParametersAsync(parameters);
    }

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        behavior = new ComboboxItemBehavior<TValue>(new ComboboxPartServices("Combobox", JSRuntime, Logger, IdGenerator, StateHasChanged, InvokeAsync, DispatchExceptionAsync), DevelopmentLog, onKeyDown, onKeyUp);
        base.OnInitialized();
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        behavior!.Apply(Value, valueSupplied, Index, Disabled, NativeButton, Id, AdditionalAttributes);
        ChildContent = behavior.Cascade(ChildContent);
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
    protected override void SubscribeSelectors() => behavior!.Subscribe(Root, Composite, Row is not null, SubscribeTo);

    /// <inheritdoc />
    protected override bool ForwardsConsumerHandler(string eventName) => behavior!.ForwardsConsumerHandler(eventName);

    /// <inheritdoc />
    protected override void AddPartAttributes(AttributeList attributes)
    {
        behavior!.AddPartAttributes(attributes);
        attributes.Set("onclick", onClick);
    }
}

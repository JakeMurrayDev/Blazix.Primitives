using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace Blazix.Primitives;

// AUT-STR-02, STR-05 (M5 track 11; decision 12, ADR 08 Amendments 2026-11-03 (3)): the Autocomplete parts of this file
// are Combobox's, as sealed shells over the same internal behaviours with the family `Autocomplete`; the comments below
// cite the Combobox rows the Autocomplete document adopts (autocomplete.md §2.1).

// CMB §3.24, STA-13 (combobox/item/AutocompleteItem.tsx): Highlighted is the list handle's data-highlighted from attach (ADR 04 Amendments 2026-11-03 (2)): mirrored.
/// <summary>Represents the state of an autocomplete item, which the part exposes as data attributes and passes to its class and style functions.</summary>
/// <param name="Disabled">A value indicating whether the item is disabled, by the part or the root; projected as <c>data-disabled</c>.</param>
/// <param name="Highlighted">A value indicating whether the item is highlighted; its <c>data-highlighted</c> is written by the browser-side behavior.</param>
[PartState]
public sealed partial record AutocompleteItemState(
    bool Disabled,
    [property: MirroredField] bool Highlighted);

// combobox/item/AutocompleteItemDataAttributes.ts (CMB-DOM-09).
/// <summary>Provides the names of the data attributes an autocomplete item renders.</summary>
public static class AutocompleteItemDataAttributes
{
    /// <summary>Represents the attribute present while the item is highlighted: <c>data-highlighted</c>.</summary>
    public const string Highlighted = "data-highlighted";

    /// <summary>Represents the attribute present while the item is disabled: <c>data-disabled</c>.</summary>
    public const string Disabled = "data-disabled";
}

// Base UI's Combobox.Item (combobox/item/AutocompleteItem.tsx; CMB §3.24): a sealed thin part over ComboboxItemBehavior, which
// Autocomplete's Item shells. An omitted Value is told from default(TValue) by whether it was supplied (ADR 08 Amendments
// 2026-11-03 (2)).
/// <summary>Represents an item of an autocomplete's list, Base UI's <c>Autocomplete.Item</c>, rendered as a <c>div</c>.</summary>
/// <typeparam name="TValue">The type of the item's value; inferred from the enclosing root.</typeparam>
/// <remarks>
/// Its <c>id</c> is stable for its lifetime. The highlight follows the keyboard and the pointer while the focus stays in
/// the input. A consumer <c>onclick</c> runs first and can call <c>PreventPartHandler()</c> to skip the selection.
/// </remarks>
[CascadingTypeParameter(nameof(TValue))]
public sealed class AutocompleteItem<TValue> : PartBase<AutocompleteItemState>
{
    private readonly EventCallback onKeyDown;
    private readonly EventCallback onKeyUp;
    private readonly EventCallback onClick;
    private ComboboxItemBehavior<TValue>? behavior;
    private AutocompleteItemState state = new(false, false);
    private bool valueSupplied;

    /// <summary>Initializes a new instance of the <see cref="AutocompleteItem{TValue}"/> class.</summary>
    public AutocompleteItem()
    {
        onKeyDown = CreateHandler<KeyboardEventArgs>("onkeydown", e => behavior!.HandleKeyDownAsync(e));
        onKeyUp = CreateHandler<KeyboardEventArgs>("onkeyup", e => behavior!.HandleKeyUpAsync(e));
        onClick = CreateHandler<MouseEventArgs>("onclick", e => behavior!.HandleClickAsync(e));
    }

    [Inject]
    private IJSRuntime JSRuntime { get; set; } = null!;

    [Inject]
    private ILogger<AutocompleteItem<TValue>> Logger { get; set; } = null!;

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
    protected override AutocompleteItemState State
    {
        get
        {
            ComboboxItemBehavior<TValue> item = behavior!;
            bool disabled = item.Disabled;
            bool highlighted = item.Highlighted;
            if (state.Disabled != disabled || state.Highlighted != highlighted)
            {
                state = new AutocompleteItemState(disabled, highlighted);
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
        behavior = new ComboboxItemBehavior<TValue>(new ComboboxPartServices("Autocomplete", JSRuntime, Logger, IdGenerator, StateHasChanged, InvokeAsync, DispatchExceptionAsync), DevelopmentLog, onKeyDown, onKeyUp);
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

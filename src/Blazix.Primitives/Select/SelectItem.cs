using Blazix.Primitives.Rendering;
using Blazix.Primitives.Store;
using Blazix.Primitives.Utilities;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace Blazix.Primitives;

// SEL-STA-23 (select/item/SelectItem.tsx:127-131, 259-272): Highlighted is list navigation's from attach (ADR 04 Amendments
// 2026-11-03 (2), the item row of 2026-11-04): mirrored, so C# never projects `data-highlighted`.
/// <summary>Represents the state of a select item, which the part exposes as data attributes and passes to its class and style functions.</summary>
/// <param name="Disabled">A value indicating whether the item is disabled, its own flag or the root's; projected as <c>data-disabled</c>.</param>
/// <param name="Selected">A value indicating whether the item's value is selected; projected as <c>data-selected</c>.</param>
/// <param name="Highlighted">A value indicating whether the item is highlighted; its <c>data-highlighted</c> is written by the browser-side behavior.</param>
[PartState]
public sealed partial record SelectItemState(bool Disabled, bool Selected, [property: MirroredField] bool Highlighted);

// select/item/SelectItemDataAttributes.ts (SEL-DOM-35).
/// <summary>Provides the names of the data attributes a select item renders.</summary>
public static class SelectItemDataAttributes
{
    /// <summary>Represents the attribute present while the item's value is selected: <c>data-selected</c>.</summary>
    public const string Selected = "data-selected";

    /// <summary>Represents the attribute present while the item is highlighted: <c>data-highlighted</c>.</summary>
    public const string Highlighted = "data-highlighted";

    /// <summary>Represents the attribute present while the item is disabled: <c>data-disabled</c>.</summary>
    public const string Disabled = "data-disabled";
}

// SEL §11 (SelectItemContext); select/item/SelectItemContext.ts: what an item shares with its text and indicator,
// independent of the value type.
/// <summary>The state a select item shares with its text and indicator.</summary>
internal sealed class SelectItemContext
{
    private readonly List<Action> listeners = [];
    private readonly Action<string?> textChanged;
    private bool notifyPending;

    /// <summary>Initializes a new instance of the <see cref="SelectItemContext"/> class.</summary>
    /// <param name="textChanged">Called with the id of the item's text element when it changes.</param>
    public SelectItemContext(Action<string?> textChanged)
    {
        this.textChanged = textChanged;
    }

    /// <summary>Gets a value indicating whether the item's value is selected.</summary>
    public bool Selected { get; private set; }

    /// <summary>Throws the error of an item part rendered outside a select item.</summary>
    /// <param name="context">The cascaded context, or <see langword="null"/>.</param>
    /// <returns>The context.</returns>
    /// <exception cref="InvalidOperationException"><paramref name="context"/> is <see langword="null"/>.</exception>
    public static SelectItemContext Require(SelectItemContext? context)
    {
        // SEL-EDGE-01, SEL-STR-04.
        return context ?? throw new InvalidOperationException(
            "Blazix: `SelectItemContext` is missing. SelectItem parts must be placed within `<SelectItem>`.");
    }

    /// <summary>Adds a listener that runs when <see cref="Selected"/> changes.</summary>
    /// <param name="listener">The listener.</param>
    public void AddListener(Action listener) => listeners.Add(listener);

    /// <summary>Removes a listener added with <see cref="AddListener"/>.</summary>
    /// <param name="listener">The listener.</param>
    public void RemoveListener(Action listener) => listeners.Remove(listener);

    /// <summary>Records the item's text element.</summary>
    /// <param name="textId">The text element's id, or <see langword="null"/> when it goes away.</param>
    public void SetText(string? textId) => textChanged(textId);

    /// <summary>Records whether the item's value is selected; the listeners run at the next <see cref="Publish"/>.</summary>
    /// <param name="selected">Whether the item's value is selected.</param>
    public void SetSelected(bool selected)
    {
        if (Selected == selected)
        {
            return;
        }

        Selected = selected;
        notifyPending = true;
    }

    /// <summary>Runs the listeners once after <see cref="Selected"/> changed.</summary>
    public void Publish()
    {
        if (!notifyPending)
        {
            return;
        }

        notifyPending = false;
        for (int i = 0; i < listeners.Count; i++)
        {
            listeners[i]();
        }
    }
}

// Base UI's Select.Item (select/item/SelectItem.tsx); the contract is docs/components/select.md §3.12, §5.10, §6.
// - `button` (ButtonBehavior { Native = NativeButton, Disabled, FocusableWhenDisabled }, composite; SEL-API-41/43): Enter
//   on keydown and Space in composite mode as a synthetic click, a disabled item's press suppressed.
// - `composite`'s item registration with the Popup's floating handle (focusable while disabled, the explicit label, the
//   text's id as the label source; CMP-STA-06); the highlight's `tabindex` and `data-highlighted` are the module's
//   (`tabindex="-1"` rendered, SEL-DOM-36), `Highlighted` the mirror.
// - The value registration with the store by the rendered id (ADR 04 Amendments 2026-11-03 (1)); items keep stable
//   generated ids (ADR 04 (1)), a dated port note on SEL-API-44.
// - `Selected` through a per-item selector over the value counter (SEL-STA-03), so a value change renders only the items
//   whose `Selected` changed (ADR 16 rules 8, 9; the brief's render-count claim).
// - No C# `onclick`: the `select` module classifies each click and activates through `activate` (Deliverable 7), where the
//   item's declined consumer `onclick` runs (SEL-PTR-11).
/// <summary>Represents an item of a select's list, Base UI's <c>Select.Item</c>, rendered as a <c>div</c> with <c>role="option"</c>.</summary>
/// <typeparam name="TValue">The type of the select's values.</typeparam>
public sealed class SelectItem<TValue> : PartBase<SelectItemState>
{
    private const string ArrowKeyPrefix = "Arrow";

    private readonly EventCallback onKeyDown;
    private readonly EventCallback onKeyUp;
    private readonly SelectItemContext itemContext;
    private readonly RenderFragment cascadedContent;
    private readonly RenderFragment publishedContent;
    private readonly Action highlightChanged;
    private SelectStore<TValue>? store;
    private ButtonBehavior? button;
    private CompositeItemBehavior? item;
    private SelectItemRegistration<TValue>? registration;
    private StoreSelection<SelectStoreState, SelectItem<TValue>, bool>? selected;
    private StoreSelection<SelectStoreState, bool>? rootDisabled;
    private SelectItemState? state;
    private RenderFragment? consumerContent;
    private bool valueSupplied;
    private string? textId;
    private string? registeredId;
    private int selectedVersion = -1;
    private bool selectedValue;
    private EventCallback<MouseEventArgs> consumerClick;

    /// <summary>Initializes a new instance of the <see cref="SelectItem{TValue}"/> class.</summary>
    public SelectItem()
    {
        onKeyDown = CreateHandler<KeyboardEventArgs>("onkeydown", e => IsItemKey(e.Key) && item is not null ? item.HandleKeyDownAsync(e) : button!.HandleKeyDownAsync(e));
        onKeyUp = CreateHandler<KeyboardEventArgs>("onkeyup", e => button!.HandleKeyUpAsync(e));
        itemContext = new SelectItemContext(OnTextChanged);
        cascadedContent = RenderCascade;

        // RND §10's indicator claim: the listeners run from inside the cascade's child content, after the item's diff
        // queued the cascade, so an indicator's listener render and its parameter render coalesce into one.
        publishedContent = builder =>
        {
            itemContext.Publish();
            consumerContent?.Invoke(builder);
        };
        highlightChanged = OnHighlightChanged;
    }

    [Inject]
    private IJSRuntime JSRuntime { get; set; } = null!;

    [Inject]
    private ILogger<SelectItem<TValue>> Logger { get; set; } = null!;

    [Inject]
    private ButtonDevelopmentLog DevelopmentLog { get; set; } = null!;

    [Inject]
    private PartIdGenerator IdGenerator { get; set; } = null!;

    private bool EffectiveDisabled => Disabled || rootDisabled!.Value;

    [CascadingParameter]
    private SelectStore<TValue>? RootStore { get; set; }

    [CascadingParameter]
    private CompositeRootContext? ListContext { get; set; }

    // SEL-API-40
    /// <summary>Gets or sets the item's value.</summary>
    /// <value>The value the item selects; an omitted value is <see langword="null"/> for a nullable type and matches nothing otherwise.</value>
    [Parameter]
    public TValue Value { get; set; } = default!;

    // SEL-API-41
    /// <summary>Gets or sets a value indicating whether the item ignores activation; it stays focusable and navigable.</summary>
    /// <value><see langword="false"/> by default; a disabled root disables every item.</value>
    [Parameter]
    public bool Disabled { get; set; }

    // SEL-API-42
    /// <summary>Gets or sets the text typeahead matches for the item.</summary>
    /// <value>The label, or <see langword="null"/>, the default, for the text of its <see cref="SelectItemText"/>, else the item's own.</value>
    [Parameter]
    public string? Label { get; set; }

    // SEL-API-43
    /// <summary>Gets or sets a value indicating whether the rendered element is a native <c>button</c>.</summary>
    /// <value><see langword="false"/> by default.</value>
    [Parameter]
    public bool NativeButton { get; set; }

    /// <inheritdoc />
    protected override string DefaultTagName => "div";

    /// <inheritdoc />
    protected override SelectItemState State
    {
        get
        {
            bool disabled = EffectiveDisabled;
            bool isSelected = IsSelected(store!.State);
            bool highlighted = item?.Highlighted ?? false;
            if (state is null || state.Disabled != disabled || state.Selected != isSelected || state.Highlighted != highlighted)
            {
                state = new SelectItemState(disabled, isSelected, highlighted);
            }

            return state;
        }
    }

    // ADR 04 Amendments 2026-11-03 (1): registered by the id it renders, a stable generated one (SEL-API-44's port note).
    /// <inheritdoc />
    protected override bool UsesGeneratedId => true;

    /// <summary>Gets a task that completes when the item's behaviors are attached.</summary>
    internal Task TwinsAttached => Task.WhenAll(button!.TwinAttached);

    /// <summary>Gets the consumer's <c>onclick</c>, which the activation runs.</summary>
    internal EventCallback<MouseEventArgs> ConsumerClick => consumerClick;

    /// <inheritdoc />
    public override Task SetParametersAsync(ParameterView parameters)
    {
        valueSupplied = parameters.TryGetValue<TValue>(nameof(Value), out _);
        ChildContent = null;
        return base.SetParametersAsync(parameters);
    }

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">The item is not inside a <see cref="SelectRoot{TValue}"/> of the same value type.</exception>
    protected override void OnInitialized()
    {
        store = (SelectStore<TValue>)SelectStore.Require(RootStore);
        button = new ButtonBehavior(JSRuntime, Logger, DevelopmentLog, IdGenerator, StateHasChanged, DispatchExceptionAsync, onKeyDown, onKeyUp);
        // SEL-STR-08 (Base UI's Positioner hosts the item registry): an item directly under the positioner, without a
        // popup's list, has no composite registration and is never highlighted; it still selects by its activation.
        item = ListContext is null ? null : new CompositeItemBehavior(ListContext, nameof(SelectItem<TValue>), onKeyDown, highlightChanged);
        base.OnInitialized();
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        SelectStore<TValue> current = store!;
        bool disabled = EffectiveDisabled;
        string id = Id!;
        ItemSlot<TValue> slot = valueSupplied || ValueNull.IsNull(default(TValue)) ? new ItemSlot<TValue>(Value, true) : default;
        if (registration is null || !string.Equals(registeredId, id, StringComparison.Ordinal))
        {
            if (registration is not null)
            {
                current.Unregister(registration);
            }

            registration = new SelectItemRegistration<TValue>(id) { Slot = slot, Label = Label, Disabled = disabled, TextId = textId, Activate = ActivateAsync };
            registeredId = id;
            current.Register(registration);
        }
        else
        {
            bool valueChanged = registration.Slot.Present != slot.Present || !Equality.Is(registration.Slot.Value, slot.Value);
            registration.Slot = slot;
            registration.Label = Label;
            registration.Disabled = disabled;
            if (valueChanged)
            {
                current.ItemValueChanged(registration);
            }
        }

        selectedVersion = -1;
        consumerClick = ConsumerHandler(AdditionalAttributes);
        button!.Native = NativeButton;
        button.Disabled = disabled;
        button.FocusableWhenDisabled = true;
        button.SetConsumerAttributes(AdditionalAttributes);
        item?.SetRegistration(id, AdditionalAttributes, disabled, focusableWhenDisabled: true, label: Label, textId: textId);
        item?.SetConsumerAttributes(disabled ? null : AdditionalAttributes);
        itemContext.SetSelected(IsSelected(current.State));
        consumerContent = ChildContent;
        ChildContent = cascadedContent;
    }

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        await button!.AfterRenderAsync(Enabled, Element, Id, composite: true);
        if (item is not null)
        {
            await item.AfterRenderAsync(Element);
        }
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            if (registration is not null)
            {
                store?.Unregister(registration);
            }

            button?.Dispose();
            item?.Dispose();
        }

        base.Dispose(disposing);
    }

    /// <inheritdoc />
    protected override void SubscribeSelectors()
    {
        SelectorSubscription<SelectStoreState> subscription = SubscribeTo<SelectStoreState>(store!);
        selected = subscription.Select(static (state, owner) => owner.IsSelected(state), this);
        rootDisabled = subscription.Select(SelectStoreState.DisabledSelector);
    }

    /// <inheritdoc />
    protected override void AddPartAttributes(AttributeList attributes)
    {
        button!.AddAttributes(attributes, Id, composite: true);
        attributes.Set("role", "option");
        attributes.Set("aria-selected", IsSelected(store!.State) ? "true" : "false");
        item?.AddAttributes(attributes);
    }

    // SEL-PTR-11: the item's consumer onclick runs inside the activation, never as Blazor's own handler.
    /// <inheritdoc />
    protected override bool ForwardsConsumerHandler(string eventName) =>
        !string.Equals(eventName, "onclick", StringComparison.Ordinal) && button!.ForwardsConsumerHandler(eventName) && (item?.ForwardsConsumerHandler(eventName) ?? true);

    /// <summary>Runs the item's activation: the consumer's <c>onclick</c>, then the commit unless it called <c>PreventPartHandler()</c>.</summary>
    /// <param name="snapshot">The click that activated the item.</param>
    /// <returns><see langword="true"/> when the consumer vetoed the activation.</returns>
    internal async Task<bool> ActivateAsync(EventSnapshot snapshot)
    {
        if (consumerClick.HasDelegate)
        {
            MouseEventArgs args = FieldConsumerHandlers.ToMouseEventArgs(snapshot);
            await consumerClick.InvokeAsync(args);
            if (args.PartHandlerPrevented())
            {
                return true;
            }
        }

        await store!.CommitSelectionAsync(registration!, snapshot);
        return false;
    }

    private static bool IsItemKey(string? key) =>
        key is not null && (key.StartsWith(ArrowKeyPrefix, StringComparison.Ordinal) || key is "Home" or "End");

    private static EventCallback<MouseEventArgs> ConsumerHandler(IReadOnlyDictionary<string, object>? attributes) =>
        attributes is not null && attributes.TryGetValue("onclick", out object? handler) && handler is EventCallback<MouseEventArgs> callback ? callback : default;

    // SEL-STA-03 through the value counter: computed once per value change.
    private bool IsSelected(SelectStoreState state)
    {
        if (state.ValueVersion == selectedVersion || registration is null)
        {
            return selectedValue;
        }

        selectedVersion = state.ValueVersion;
        selectedValue = store!.IsSelected(registration.Slot);
        return selectedValue;
    }

    private void OnTextChanged(string? next)
    {
        if (string.Equals(textId, next, StringComparison.Ordinal))
        {
            return;
        }

        textId = next;
        if (registration is not null)
        {
            store!.SetItemText(registration.Id, next);
        }

        item?.SetRegistration(Id, AdditionalAttributes, EffectiveDisabled, focusableWhenDisabled: true, label: Label, textId: textId);
    }

    private void OnHighlightChanged()
    {
        SelectItemState? previous = state;
        if (previous is null || previous != State)
        {
            StateHasChanged();
        }
    }

    private void RenderCascade(RenderTreeBuilder builder)
    {
        itemContext.SetSelected(IsSelected(store!.State));
        builder.OpenComponent<CascadingValue<SelectItemContext>>(0);
        builder.AddComponentParameter(1, nameof(CascadingValue<SelectItemContext>.Value), itemContext);
        builder.AddComponentParameter(2, nameof(CascadingValue<SelectItemContext>.IsFixed), true);
        builder.AddComponentParameter(3, nameof(CascadingValue<SelectItemContext>.ChildContent), publishedContent);
        builder.CloseComponent();
    }
}

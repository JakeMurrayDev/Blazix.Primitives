using Blazix.Primitives.Rendering;
using Blazix.Primitives.Store;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace Blazix.Primitives;

// ACC-STA-02: the item's state, unchanged (accordion/trigger/AccordionTrigger.tsx:49,77), in the trigger's own record (ADR 08
// §3). The trigger's own Disabled is never in it (ACC-API-14).
/// <summary>Represents the state of an accordion trigger, which the part exposes as data attributes and passes to its class and style functions.</summary>
/// <typeparam name="TValue">The type of the items' values.</typeparam>
/// <param name="Value">The values of the root's open items; never projected as an attribute.</param>
/// <param name="Disabled">
/// A value indicating whether the item ignores user interaction, its own or its root's; projected as <c>data-disabled</c>. The
/// trigger's own <c>Disabled</c> changes how its button behaves, not this value.
/// </param>
/// <param name="Orientation">The root's orientation; projected as <c>data-orientation</c>.</param>
/// <param name="Hidden">A value indicating whether the item's panel is closed and its exit has ended; projected as <c>data-hidden</c>.</param>
/// <param name="Index">The item's position among its root's items, or -1 until it is known; projected as <c>data-index</c>.</param>
/// <param name="Open">A value indicating whether the item is open; projected as <c>data-panel-open</c> while open.</param>
[PartState]
public sealed partial record AccordionTriggerState<TValue>([property: NotProjected] IReadOnlyList<TValue> Value, bool Disabled, Orientation Orientation, bool Hidden, int Index, bool Open);

// accordion/trigger/AccordionTriggerDataAttributes.ts, in Base UI's order.
/// <summary>Provides the names of the data attributes an accordion trigger renders.</summary>
public static class AccordionTriggerDataAttributes
{
    /// <summary>Represents the attribute that carries the item's index: <c>data-index</c>.</summary>
    public const string Index = "data-index";

    /// <summary>Represents the attribute present while the item's panel is open: <c>data-panel-open</c>.</summary>
    public const string PanelOpen = "data-panel-open";

    /// <summary>Represents the attribute present while the item is disabled: <c>data-disabled</c>.</summary>
    public const string Disabled = "data-disabled";
}

// Base UI's Accordion.Trigger (accordion/trigger/AccordionTrigger.tsx); the contract is docs/components/accordion.md (§3.4,
// §5.3, §6).
// - The button mechanism is ButtonBehavior with composite false (ADR 04 Amendments 2026-10-26, chosen by the user; ACC-DOM-12,
//   UBT-STA-01): native = NativeButton, disabled = own || item (ACC-API-14), focusableWhenDisabled unset; one ButtonTwin. No key
//   handler of its own (ACC-KEY-04); the consumer's key handlers are declined and run by the part (ADR 02 Amendments 2026-10-21).
// - The id is the bag's when non-empty, registered with the item (ACC-STA-11); otherwise the item's generated trigger id, which
//   FinishAttributes also puts in place of an empty one (ACC-API-15, ACC-DOM-13, ACC-EDGE-13).
// - aria-expanded always; aria-controls while the item is open and a panel id resolves (ACC-ARIA-01/02, ACC-STR-06).
// - The click requests !open with reason trigger-press and the click's snapshot (EventSnapshot.FromEventArgs, ADR 04 Amendments
//   2026-10-21; ACC-PTR-01, ACC-EVT-01/05), through the item's collapsible state and its chain.
// - ACC-EDGE-03: the collapsible state is looked up first (COL-EDGE-01's message), then the item's (ACC-EDGE-02's).
/// <summary>
/// Represents the button that opens and closes an accordion item's panel, Base UI's <c>Accordion.Trigger</c>, rendered as a
/// <c>button</c>.
/// </summary>
/// <typeparam name="TValue">The type of the items' values, which the trigger takes from its item.</typeparam>
public sealed class AccordionTrigger<TValue> : PartBase<AccordionTriggerState<TValue>>
{
    private const string DefaultTag = "button";

    private static readonly StateAttributesMapping<AccordionTriggerState<TValue>> s_mapping = AccordionStateMapping.CreateTrigger<AccordionTriggerState<TValue>>();

    private readonly EventCallback onClick;
    private readonly EventCallback onKeyDown;
    private readonly EventCallback onKeyUp;
    private readonly Action itemChanged;
    private ButtonBehavior? button;
    private CollapsibleRootContext? collapsible;
    private AccordionItemContext<TValue>? item;
    private StoreSelection<CollapsibleRootStoreState, string?>? panelId;
    private AccordionItemState<TValue>? renderedItemState;
    private AccordionTriggerState<TValue>? state;
    private string? registeredId;
    private bool registered;

    /// <summary>Initializes a new instance of the <see cref="AccordionTrigger{TValue}"/> class.</summary>
    public AccordionTrigger()
    {
        onClick = CreateHandler<MouseEventArgs>("onclick", HandleClickAsync);
        onKeyDown = CreateHandler<KeyboardEventArgs>("onkeydown", e => button!.HandleKeyDownAsync(e));
        onKeyUp = CreateHandler<KeyboardEventArgs>("onkeyup", e => button!.HandleKeyUpAsync(e));
        itemChanged = OnItemChanged;
    }

    [Inject]
    private IJSRuntime JSRuntime { get; set; } = null!;

    [Inject]
    private ILogger<AccordionTrigger<TValue>> Logger { get; set; } = null!;

    [Inject]
    private ButtonDevelopmentLog DevelopmentLog { get; set; } = null!;

    [Inject]
    private PartIdGenerator IdGenerator { get; set; } = null!;

    private bool EffectiveDisabled => Disabled || item!.State.Disabled;

    // The id the element renders: the consumer's non-empty id, else the item's generated one (AccordionTrigger.tsx:50-51).
    private string RenderedId => registeredId ?? item!.DefaultTriggerId;

    [CascadingParameter]
    private CollapsibleRootContext? CollapsibleContext { get; set; }

    [CascadingParameter]
    private AccordionItemContext<TValue>? ItemContext { get; set; }

    [CascadingParameter]
    private AccordionItemContext? AnyItemContext { get; set; }

    // ACC-API-13
    /// <summary>Gets or sets a value indicating whether the rendered element is a native <c>button</c>.</summary>
    /// <value>
    /// <see langword="true"/>, the default, for a <c>button</c> element; <see langword="false"/> when
    /// <see cref="PartBase{TState}.Render"/> supplies another element, which then gets <c>role="button"</c>, a
    /// <c>tabindex</c>, and clicks from Enter and Space.
    /// </value>
    [Parameter]
    public bool NativeButton { get; set; } = true;

    // ACC-API-14
    /// <summary>Gets or sets a value indicating whether this trigger ignores user interaction.</summary>
    /// <value>
    /// <see langword="true"/> to disable the trigger; the default is <see langword="false"/>, which cannot enable it inside a
    /// disabled item or root.
    /// </value>
    [Parameter]
    public bool Disabled { get; set; }

    /// <inheritdoc />
    protected override string DefaultTagName => DefaultTag;

    /// <inheritdoc />
    protected override AccordionTriggerState<TValue> State
    {
        get
        {
            AccordionItemState<TValue> current = item!.State;
            if (state is null || !ReferenceEquals(current, renderedItemState))
            {
                renderedItemState = current;
                state = new AccordionTriggerState<TValue>(current.Value, current.Disabled, current.Orientation, current.Hidden, current.Index, current.Open);
            }

            return state;
        }
    }

    /// <inheritdoc />
    protected override StateAttributesMapping<AccordionTriggerState<TValue>>? StateAttributesMapping => s_mapping;

    // For the fixtures: completes once the button behavior is attached to the trigger's element.
    /// <summary>Gets a task that completes when the trigger's behavior is attached to its element.</summary>
    /// <value>A task that completes after the first interactive render has attached it.</value>
    internal Task TwinAttached => button!.TwinAttached;

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">The trigger is not inside an <see cref="AccordionItem{TValue}"/> of its value type.</exception>
    protected override void OnInitialized()
    {
        collapsible = CollapsibleContext ?? throw CollapsiblePanel.MissingRoot();
        item = ItemContext ?? throw (AnyItemContext is null ? AccordionItem<TValue>.MissingItem() : AccordionRoot<TValue>.TypeMismatch("AccordionTrigger"));
        item.AddListener(itemChanged);
        button = new ButtonBehavior(JSRuntime, Logger, DevelopmentLog, IdGenerator, StateHasChanged, DispatchExceptionAsync, onKeyDown, onKeyUp);
        base.OnInitialized();
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        button!.Native = NativeButton;
        button.FocusableWhenDisabled = null;
        button.SetConsumerAttributes(AdditionalAttributes);

        // ACC-STA-11: a non-empty id registers; a change re-registers; removing it restores the generated id.
        string? next = AdditionalAttributes is not null && AdditionalAttributes.TryGetValue("id", out object? id) && id is string { Length: > 0 } text ? text : null;
        if (registered && string.Equals(next, registeredId, StringComparison.Ordinal))
        {
            return;
        }

        if (registered)
        {
            item!.UnregisterTrigger(registeredId);
        }

        registeredId = next;
        registered = true;
        item!.RegisterTrigger(next);
    }

    /// <inheritdoc />
    protected override Task OnAfterRenderAsync(bool firstRender)
    {
        button!.Disabled = EffectiveDisabled;
        return button.AfterRenderAsync(Enabled, Element, RenderedId, composite: false);
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            button?.Dispose();
            if (item is not null)
            {
                item.RemoveListener(itemChanged);
                if (registered)
                {
                    registered = false;
                    item.UnregisterTrigger(registeredId);
                }
            }
        }

        base.Dispose(disposing);
    }

    /// <inheritdoc />
    protected override void SubscribeSelectors()
    {
        panelId = SubscribeTo(collapsible!).Select(CollapsibleRootStoreState.PanelIdSelector);
    }

    /// <inheritdoc />
    protected override void AddPartAttributes(AttributeList attributes)
    {
        // The gate reads the effective state of this render (the item's Disabled can change without a parameter set).
        button!.Disabled = EffectiveDisabled;
        bool isOpen = item!.State.Open;
        string id = RenderedId;
        attributes.Set("id", id);
        if (isOpen && panelId!.Value is { } controls)
        {
            attributes.Set("aria-controls", controls);
        }

        attributes.Set("aria-expanded", isOpen ? "true" : "false");
        attributes.Set("onclick", onClick);
        button.AddAttributes(attributes, id, composite: false);
    }

    /// <inheritdoc />
    protected override bool ForwardsConsumerHandler(string eventName)
    {
        return button!.ForwardsConsumerHandler(eventName);
    }

    // ACC-API-15: an empty id is not the trigger's id (registeredId = idProp || undefined; id = registeredId ?? default).
    /// <inheritdoc />
    protected override void FinishAttributes(AttributeList attributes)
    {
        if (attributes.TryGetValue("id", out AttributeValue id) && id.TryGetValue(out string text) && text.Length == 0)
        {
            attributes.Set("id", item!.DefaultTriggerId);
        }
    }

    private void OnItemChanged()
    {
        if (!ReferenceEquals(item!.State, renderedItemState))
        {
            StateHasChanged();
        }
    }

    // ACC-PTR-01/02, ACC-EVT-01/05/08: a disabled trigger requests nothing; the request carries no trigger element.
    private Task HandleClickAsync(MouseEventArgs e)
    {
        if (EffectiveDisabled)
        {
            return Task.CompletedTask;
        }

        CollapsibleRootContext root = collapsible!;
        return root.RequestOpenAsync(!root.State.Open, new ChangeEventDetails(EventReason.TriggerPress, EventSnapshot.FromEventArgs(e)));
    }
}

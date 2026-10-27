using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.Logging;

namespace Blazix.Primitives;

// ACC-STA-02, §3.2 state object; accordion/item/AccordionItem.tsx:87-96. The header and trigger render the same fields in their
// own records (ADR 08 §3: one record per part). The value is the Root's, never projected (ADR 02 Amendments 2026-10-26).
/// <summary>Represents the state of an accordion item, which the part exposes as data attributes and passes to its class and style functions.</summary>
/// <typeparam name="TValue">The type of the items' values.</typeparam>
/// <param name="Value">The values of the root's open items; never projected as an attribute.</param>
/// <param name="Disabled">A value indicating whether the item ignores user interaction, its own or its root's; projected as <c>data-disabled</c>.</param>
/// <param name="Orientation">The root's orientation; projected as <c>data-orientation</c>.</param>
/// <param name="Hidden">A value indicating whether the item's panel is closed and its exit has ended; projected as <c>data-hidden</c>.</param>
/// <param name="Index">The item's position among its root's items, or -1 until it is known; projected as <c>data-index</c>.</param>
/// <param name="Open">A value indicating whether the item is open; projected as <c>data-open</c> or <c>data-closed</c>.</param>
[PartState]
public sealed partial record AccordionItemState<TValue>([property: NotProjected] IReadOnlyList<TValue> Value, bool Disabled, Orientation Orientation, bool Hidden, int Index, bool Open);

// accordion/item/AccordionItemDataAttributes.ts (ACC-DOM-04..06), in Base UI's order.
/// <summary>Provides the names of the data attributes an accordion item renders.</summary>
public static class AccordionItemDataAttributes
{
    /// <summary>Represents the attribute that carries the item's index: <c>data-index</c>.</summary>
    public const string Index = "data-index";

    /// <summary>Represents the attribute present while the item is disabled: <c>data-disabled</c>.</summary>
    public const string Disabled = "data-disabled";

    /// <summary>Represents the attribute present while the item is open: <c>data-open</c>.</summary>
    public const string Open = "data-open";
}

// Base UI's Accordion.Item (accordion/item/AccordionItem.tsx); the contract is docs/components/accordion.md (§3.2, §4).
// - Its identity is Value, read with TryGetValue (the RadioGroup precedent), unless absent or null; then the generated
//   fallback under string, and an always-on error under any other TValue (ACC-API-10, ACC-EDGE-09; ADR 08 Amendments
//   2026-10-26).
// - Its ids are drawn in Base UI's order: the fallback value, the panel id (inside useCollapsibleRoot), the trigger id
//   (ACC-ARIA-06: prerender equals the interactive render); then its element id (D3), which Base UI does not render.
// - Two fixed cascades (settled; ACC-EDGE-14): CollapsibleRootContext, so real Collapsible parts bind to the item, and
//   AccordionItemContext<TValue>.
// - ACC-EDGE-01: outside a root it throws in every build.
// - ACC-STA-13, ACC-DOM-05: it is an item of its root's registration-only composite list (CompositeListItemBehavior, track 1's
//   E1, ADR 04 Amendments 2026-10-26), registered by its element id; its index is the reported position, -1 until then.
/// <summary>
/// Represents one section of an accordion, Base UI's <c>Accordion.Item</c>, rendered as a <c>div</c>: it groups an
/// <see cref="AccordionHeader{TValue}"/> and its <see cref="AccordionTrigger{TValue}"/> with the
/// <see cref="AccordionPanel{TValue}"/> they show and hide.
/// </summary>
/// <typeparam name="TValue">The type of the items' values, which the item takes from its root.</typeparam>
[CascadingTypeParameter(nameof(TValue))]
public sealed class AccordionItem<TValue> : PartBase<AccordionItemState<TValue>>
{
    private const string DefaultTag = "div";

    private static readonly StateAttributesMapping<AccordionItemState<TValue>> s_mapping = AccordionStateMapping.CreateItem<AccordionItemState<TValue>>();

    private readonly RenderFragment renderPart;
    private readonly RenderFragment renderItemCascade;
    private readonly Action itemChanged;
    private readonly Action indexChanged;
    private AccordionItemContext<TValue>? context;
    private CompositeListItemBehavior? listItem;
    private string? fallbackValue;
    private bool valueSupplied;

    /// <summary>Initializes a new instance of the <see cref="AccordionItem{TValue}"/> class.</summary>
    public AccordionItem()
    {
        renderPart = base.BuildRenderTree;
        renderItemCascade = BuildItemCascade;
        itemChanged = StateHasChanged;
        indexChanged = () => context!.SetIndex(listItem!.Index);
    }

    [Inject]
    private ILogger<AccordionItem<TValue>> Logger { get; set; } = null!;

    [Inject]
    private PartIdGenerator IdGenerator { get; set; } = null!;

    [CascadingParameter]
    private AccordionRootContext<TValue>? RootContext { get; set; }

    [CascadingParameter]
    private AccordionRootContext? AnyRootContext { get; set; }

    [CascadingParameter]
    private CompositeListContext? ListContext { get; set; }

    // ACC-API-10
    /// <summary>Gets or sets the value that identifies the item in its root's value.</summary>
    /// <value>
    /// The item's value, compared by identity. Without it, or with <see langword="null"/>, the item is identified by a
    /// generated id, which needs a root whose <c>TValue</c> is <see cref="string"/>; such an item can be opened by the user
    /// but not initially.
    /// </value>
    [Parameter]
    public TValue? Value { get; set; }

    // ACC-API-11
    /// <summary>Gets or sets a value indicating whether the item ignores user interaction.</summary>
    /// <value>
    /// <see langword="true"/> to disable the item; the default is <see langword="false"/>. A disabled root disables every
    /// item whatever this value.
    /// </value>
    [Parameter]
    public bool Disabled { get; set; }

    // ACC-API-12, ACC-EVT-01/02
    /// <summary>Gets or sets the callback invoked when the item asks to open or close, before its root is asked.</summary>
    /// <value>
    /// A callback that receives the requested open state and the request's details, whose reason is
    /// <see cref="EventReason.TriggerPress"/> for a trigger press and <see cref="EventReason.None"/> for a find-in-page
    /// reveal. Calling <see cref="ChangeEventDetails.Cancel"/> keeps the item as it is and asks the root nothing.
    /// </value>
    [Parameter]
    public EventCallback<ValueChangeEventArgs<bool>> OnOpenChange { get; set; }

    /// <inheritdoc />
    protected override string DefaultTagName => DefaultTag;

    /// <inheritdoc />
    protected override AccordionItemState<TValue> State => context!.State;

    /// <inheritdoc />
    protected override StateAttributesMapping<AccordionItemState<TValue>>? StateAttributesMapping => s_mapping;

    // Design point D3 (option (a), settled by the orchestrator 2026-10-26; track 1's design point 2 (a)): the registration-only
    // list resolves its items by element id, so the item renders the consumer's id, else a generated one. The base draws it in
    // OnInitialized after the item's three Base UI ids (ACC-ARIA-06: prerender equals the interactive render).
    /// <inheritdoc />
    protected override bool UsesGeneratedId => true;

    /// <summary>Gets the shared state the item cascades to its parts.</summary>
    /// <value>The context; available from the first render.</value>
    internal AccordionItemContext<TValue> Context => context!;

    /// <inheritdoc />
    public override Task SetParametersAsync(ParameterView parameters)
    {
        valueSupplied = parameters.TryGetValue<TValue>(nameof(Value), out _);
        return base.SetParametersAsync(parameters);
    }

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">
    /// The item is not inside an <see cref="AccordionRoot{TValue}"/>, or its root's value type differs from
    /// <typeparamref name="TValue"/>.
    /// </exception>
    protected override void OnInitialized()
    {
        AccordionRootContext<TValue> root = RootContext ?? throw (AnyRootContext is null ? AccordionRoot<TValue>.MissingRoot() : AccordionRoot<TValue>.TypeMismatch("AccordionItem"));
        fallbackValue = IdGenerator.Next();
        CollapsibleRootContext collapsible = new(IdGenerator.Next(), Logger);
        context = new AccordionItemContext<TValue>(root, collapsible, IdGenerator.Next());
        context.AddListener(itemChanged);
        base.OnInitialized();
        listItem = new CompositeListItemBehavior(ListContext, indexChanged);
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        context!.OnOpenChange = OnOpenChange;
        context.ApplyParameters(AccordionValues.Identity(valueSupplied, Value!, fallbackValue!), Disabled);
        listItem!.SetRegistration(Id);
    }

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        // Base UI's two providers wrap the item's element; both cascades are fixed (ADR 16 rule 9).
        builder.OpenComponent<CascadingValue<CollapsibleRootContext>>(0);
        builder.AddComponentParameter(1, nameof(CascadingValue<CollapsibleRootContext>.Value), context!.Collapsible);
        builder.AddComponentParameter(2, nameof(CascadingValue<CollapsibleRootContext>.IsFixed), true);
        builder.AddComponentParameter(3, nameof(CascadingValue<CollapsibleRootContext>.ChildContent), renderItemCascade);
        builder.CloseComponent();
    }

    /// <inheritdoc />
    protected override Task OnAfterRenderAsync(bool firstRender)
    {
        return listItem!.AfterRenderAsync(Element);
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing && context is not null)
        {
            listItem?.Dispose();
            context.RemoveListener(itemChanged);
            context.Dispose();
        }

        base.Dispose(disposing);
    }

    // ACC-EDGE-02 (the PRG-EDGE-01 precedent: `Blazix: ` for `Base UI: `, the part names for the context names).
    internal static InvalidOperationException MissingItem() =>
        new("Blazix: The `AccordionItem` context is missing. Accordion parts must be placed within `<AccordionItem>`.");

    private void BuildItemCascade(RenderTreeBuilder builder)
    {
        builder.OpenComponent<CascadingValue<AccordionItemContext<TValue>>>(0);
        builder.AddComponentParameter(1, nameof(CascadingValue<AccordionItemContext<TValue>>.Value), context);
        builder.AddComponentParameter(2, nameof(CascadingValue<AccordionItemContext<TValue>>.IsFixed), true);
        builder.AddComponentParameter(3, nameof(CascadingValue<AccordionItemContext<TValue>>.ChildContent), renderPart);
        builder.CloseComponent();
    }
}

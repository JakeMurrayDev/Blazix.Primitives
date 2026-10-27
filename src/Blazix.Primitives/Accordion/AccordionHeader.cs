using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;

namespace Blazix.Primitives;

// ACC-STA-02: the item's state, unchanged (accordion/header/AccordionHeader.tsx:21,33), in the header's own record (ADR 08 §3).
/// <summary>Represents the state of an accordion header, which the part exposes as data attributes and passes to its class and style functions.</summary>
/// <typeparam name="TValue">The type of the items' values.</typeparam>
/// <param name="Value">The values of the root's open items; never projected as an attribute.</param>
/// <param name="Disabled">A value indicating whether the item ignores user interaction, its own or its root's; projected as <c>data-disabled</c>.</param>
/// <param name="Orientation">The root's orientation; projected as <c>data-orientation</c>.</param>
/// <param name="Hidden">A value indicating whether the item's panel is closed and its exit has ended; projected as <c>data-hidden</c>.</param>
/// <param name="Index">The item's position among its root's items, or -1 until it is known; projected as <c>data-index</c>.</param>
/// <param name="Open">A value indicating whether the item is open; projected as <c>data-open</c> or <c>data-closed</c>.</param>
[PartState]
public sealed partial record AccordionHeaderState<TValue>([property: NotProjected] IReadOnlyList<TValue> Value, bool Disabled, Orientation Orientation, bool Hidden, int Index, bool Open);

// accordion/header/AccordionHeaderDataAttributes.ts, in Base UI's order.
/// <summary>Provides the names of the data attributes an accordion header renders.</summary>
public static class AccordionHeaderDataAttributes
{
    /// <summary>Represents the attribute that carries the item's index: <c>data-index</c>.</summary>
    public const string Index = "data-index";

    /// <summary>Represents the attribute present while the item is disabled: <c>data-disabled</c>.</summary>
    public const string Disabled = "data-disabled";

    /// <summary>Represents the attribute present while the item is open: <c>data-open</c>.</summary>
    public const string Open = "data-open";
}

// Base UI's Accordion.Header (accordion/header/AccordionHeader.tsx); ACC-STR-03, ACC-ARIA-07: an `h3` with no behavior and no
// parameter of its own; it renders the item's state and re-renders when that changes. ACC-EDGE-02: it throws outside an item.
/// <summary>
/// Represents the heading of an accordion item, Base UI's <c>Accordion.Header</c>, rendered as an <c>h3</c> that holds the
/// item's <see cref="AccordionTrigger{TValue}"/>. Substitute another heading level through
/// <see cref="PartBase{TState}.Render"/> to fit the page's outline.
/// </summary>
/// <typeparam name="TValue">The type of the items' values, which the header takes from its item.</typeparam>
public sealed class AccordionHeader<TValue> : PartBase<AccordionHeaderState<TValue>>
{
    private const string DefaultTag = "h3";

    private static readonly StateAttributesMapping<AccordionHeaderState<TValue>> s_mapping = AccordionStateMapping.CreateItem<AccordionHeaderState<TValue>>();

    private readonly Action itemChanged;
    private AccordionItemContext<TValue>? item;
    private AccordionItemState<TValue>? renderedItemState;
    private AccordionHeaderState<TValue>? state;

    /// <summary>Initializes a new instance of the <see cref="AccordionHeader{TValue}"/> class.</summary>
    public AccordionHeader()
    {
        itemChanged = OnItemChanged;
    }

    [CascadingParameter]
    private AccordionItemContext<TValue>? ItemContext { get; set; }

    [CascadingParameter]
    private AccordionItemContext? AnyItemContext { get; set; }

    /// <inheritdoc />
    protected override string DefaultTagName => DefaultTag;

    /// <inheritdoc />
    protected override AccordionHeaderState<TValue> State
    {
        get
        {
            AccordionItemState<TValue> current = item!.State;
            if (state is null || !ReferenceEquals(current, renderedItemState))
            {
                renderedItemState = current;
                state = new AccordionHeaderState<TValue>(current.Value, current.Disabled, current.Orientation, current.Hidden, current.Index, current.Open);
            }

            return state;
        }
    }

    /// <inheritdoc />
    protected override StateAttributesMapping<AccordionHeaderState<TValue>>? StateAttributesMapping => s_mapping;

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">The header is not inside an <see cref="AccordionItem{TValue}"/> of its value type.</exception>
    protected override void OnInitialized()
    {
        item = ItemContext ?? throw (AnyItemContext is null ? AccordionItem<TValue>.MissingItem() : AccordionRoot<TValue>.TypeMismatch("AccordionHeader"));
        item.AddListener(itemChanged);
        base.OnInitialized();
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            item?.RemoveListener(itemChanged);
        }

        base.Dispose(disposing);
    }

    private void OnItemChanged()
    {
        if (!ReferenceEquals(item!.State, renderedItemState))
        {
            StateHasChanged();
        }
    }
}

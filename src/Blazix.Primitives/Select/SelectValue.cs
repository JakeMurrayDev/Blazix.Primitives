using Blazix.Primitives.Rendering;
using Blazix.Primitives.Store;
using Blazix.Primitives.Utilities;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Blazix.Primitives;

// SEL-STA-16 (select/value/SelectValue.tsx:42-45, 70-79); design point 4 (a), pending the user: the value is in the state,
// never projected (SEL-DOM-08; the Accordion precedent, ADR 08 Amendments 2026-10-26), as two fields, one per mode.
/// <summary>Represents the state of a select value, which the part exposes as data attributes and passes to its class and style functions.</summary>
/// <typeparam name="TValue">The type of the select's values.</typeparam>
/// <param name="Value">The selected value of a single select; not projected.</param>
/// <param name="Values">The selected values of a multiple select, empty otherwise; not projected.</param>
/// <param name="Placeholder">A value indicating whether no value is selected; projected as <c>data-placeholder</c>.</param>
[PartState]
public sealed partial record SelectValueState<TValue>([property: NotProjected] TValue Value, [property: NotProjected] IReadOnlyList<TValue> Values, bool Placeholder);

// select/value/SelectValueDataAttributes.ts (SEL-DOM-11).
/// <summary>Provides the names of the data attributes a select value renders.</summary>
public static class SelectValueDataAttributes
{
    /// <summary>Represents the attribute present while no value is selected: <c>data-placeholder</c>.</summary>
    public const string Placeholder = "data-placeholder";
}

// Base UI's Select.Value (select/value/SelectValue.tsx); the contract is docs/components/select.md §3.4, §5.2.
// - SEL-DOM-10's order: a template, ChildContent, the placeholder (SEL-API-30: unless a labeled null item exists), the
//   joined labels in multiple mode, the selected label (track 4's ValueLabels over the root's items).
// - It selects the value counter, the label counter and the placeholder flag, so a value change renders it once.
// - SEL-DOM-12: its element is the alignment's value reference; the last value part to render holds it (SEL-EDGE-17).
/// <summary>Represents the text of a select's selected value, Base UI's <c>Select.Value</c>, rendered as a <c>span</c>.</summary>
/// <typeparam name="TValue">The type of the select's values.</typeparam>
public sealed class SelectValue<TValue> : PartBase<SelectValueState<TValue>>
{
    private static readonly IReadOnlyList<TValue> s_empty = [];

    private SelectStore<TValue>? store;
    private StoreSelection<SelectStoreState, int>? valueVersion;
    private StoreSelection<SelectStoreState, int>? labelsVersion;
    private StoreSelection<SelectStoreState, bool>? hasSelectedValue;
    private StoreSelection<SelectStoreState, bool>? multiple;
    private SelectValueState<TValue>? state;
    private int stateVersion = -1;
    private RenderFragment? consumerContent;
    private readonly RenderFragment resolvedContent;

    /// <summary>Initializes a new instance of the <see cref="SelectValue{TValue}"/> class.</summary>
    public SelectValue()
    {
        resolvedContent = RenderContent;
    }

    [CascadingParameter]
    private SelectStore<TValue>? RootStore { get; set; }

    // SEL-API-29
    /// <summary>Gets or sets the content rendered with the selected value of a single select.</summary>
    /// <value>A template that receives the selected value, or <see langword="null"/>; it wins over every other content.</value>
    [Parameter]
    public RenderFragment<TValue>? ValueContent { get; set; }

    /// <summary>Gets or sets the content rendered with the selected values of a multiple select.</summary>
    /// <value>A template that receives the selected values, an empty list when none is selected; or <see langword="null"/>.</value>
    [Parameter]
    public RenderFragment<IReadOnlyList<TValue>>? ValuesContent { get; set; }

    // SEL-API-30
    /// <summary>Gets or sets the content shown while no value is selected.</summary>
    /// <value>
    /// The placeholder, or <see langword="null"/>. It is not shown when <see cref="PartBase{TState}.ChildContent"/> is given or
    /// when the root's items label a <see langword="null"/> value.
    /// </value>
    [Parameter]
    public RenderFragment? Placeholder { get; set; }

    /// <inheritdoc />
    protected override string DefaultTagName => "span";

    /// <inheritdoc />
    protected override SelectValueState<TValue> State
    {
        get
        {
            int version = valueVersion!.Value;
            bool placeholder = !hasSelectedValue!.Value;
            if (state is null || stateVersion != version || state.Placeholder != placeholder)
            {
                stateVersion = version;
                SelectStore<TValue> current = store!;
                state = new SelectValueState<TValue>(current.HasValue ? current.Value : default!, multiple!.Value ? current.Values : s_empty, placeholder);
            }

            return state;
        }
    }

    /// <inheritdoc />
    public override Task SetParametersAsync(ParameterView parameters)
    {
        ChildContent = null;
        return base.SetParametersAsync(parameters);
    }

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">The value is not inside a <see cref="SelectRoot{TValue}"/> of the same value type.</exception>
    protected override void OnInitialized()
    {
        store = (SelectStore<TValue>)SelectStore.Require(RootStore);
        base.OnInitialized();
    }

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">A template of the other mode is supplied.</exception>
    protected override void OnParametersSet()
    {
        consumerContent = ChildContent;
        ChildContent = resolvedContent;

        // Design point N2 (pending the user): the other mode's template is an always-on error, as the root's parameters are.
        if (multiple!.Value && ValueContent is not null)
        {
            throw new InvalidOperationException("Blazix: `SelectValue` received `ValueContent` while the root's `Multiple` is true. Use `ValuesContent` in multiple mode.");
        }

        if (!multiple.Value && ValuesContent is not null)
        {
            throw new InvalidOperationException("Blazix: `SelectValue` received `ValuesContent` while the root's `Multiple` is false. Use `ValueContent` for one value.");
        }
    }

    /// <inheritdoc />
    protected override Task OnAfterRenderAsync(bool firstRender)
    {
        store!.ValueElement = Element;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing && store is not null && Element is { } element && store.ValueElement is { } current && string.Equals(current.Id, element.Id, StringComparison.Ordinal))
        {
            store.ValueElement = null;
        }

        base.Dispose(disposing);
    }

    /// <inheritdoc />
    protected override void SubscribeSelectors()
    {
        SelectorSubscription<SelectStoreState> subscription = SubscribeTo<SelectStoreState>(store!);
        valueVersion = subscription.Select(SelectStoreState.ValueVersionSelector);
        labelsVersion = subscription.Select(SelectStoreState.LabelsVersionSelector);
        hasSelectedValue = subscription.Select(SelectStoreState.HasSelectedValueSelector);
        multiple = subscription.Select(SelectStoreState.MultipleSelector);
    }

    private void RenderContent(RenderTreeBuilder builder)
    {
        SelectStore<TValue> current = store!;
        bool isMultiple = multiple!.Value;
        if (isMultiple && ValuesContent is { } valuesTemplate)
        {
            builder.AddContent(0, valuesTemplate(current.Values));
            return;
        }

        if (!isMultiple && ValueContent is { } valueTemplate)
        {
            builder.AddContent(1, valueTemplate(current.HasValue ? current.Value : default!));
            return;
        }

        if (consumerContent is not null)
        {
            builder.AddContent(2, consumerContent);
            return;
        }

        if (!hasSelectedValue!.Value && Placeholder is not null && !ValueLabels.HasNullItemLabel(current.Labels))
        {
            builder.AddContent(3, Placeholder);
            return;
        }

        builder.AddContent(4, Label(current, isMultiple));
    }

    // SEL-DOM-10 steps 4 and 5; a value type with no value shows nothing (Base UI's `null` without a labeled null item).
    private static string Label(SelectStore<TValue> current, bool isMultiple)
    {
        if (isMultiple)
        {
            return ValueLabels.ResolveMultipleLabels(current.Values, current.Labels, current.ItemToStringLabel);
        }

        if (!current.HasValue && !ValueNull.IsNull(default(TValue)))
        {
            return string.Empty;
        }

        return ValueLabels.ResolveSelectedLabel(current.Value, current.Labels, current.ItemToStringLabel);
    }
}

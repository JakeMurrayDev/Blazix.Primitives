using Iodine.Headless.Rendering;
using Iodine.Headless.Store;
using Microsoft.AspNetCore.Components;

namespace Iodine.Headless;

// SLD §3.6: the Root's state object (slider/indicator/SliderIndicator.tsx:93).
/// <summary>Represents the state of a slider indicator, which the part exposes as data attributes and passes to its class and style functions.</summary>
/// <param name="ActiveThumbIndex">The index of the thumb whose input has focus, or -1.</param>
/// <param name="Disabled">Whether the slider is disabled; projected as <c>data-disabled</c>.</param>
/// <param name="Dragging">Whether a pointer or touch interaction is in progress; projected as <c>data-dragging</c>.</param>
/// <param name="Orientation">The axis of the slider; projected as <c>data-orientation</c>.</param>
/// <param name="Max">The upper bound.</param>
/// <param name="Min">The lower bound.</param>
/// <param name="MinStepsBetweenValues">The minimum number of steps between adjacent values.</param>
/// <param name="Step">The step.</param>
/// <param name="Values">The normalized values, one per thumb.</param>
/// <param name="Touched">Whether the field was touched; projected as <c>data-touched</c>.</param>
/// <param name="Dirty">Whether the value differs from the field's initial value; projected as <c>data-dirty</c>.</param>
/// <param name="Valid">The field's verdict: <c>data-valid</c> when <see langword="true"/>, <c>data-invalid</c> when <see langword="false"/>.</param>
/// <param name="Filled">Whether the field is filled; projected as <c>data-filled</c>, never set by the slider.</param>
/// <param name="Focused">Whether a thumb input has focus; projected as <c>data-focused</c>.</param>
[PartState]
public sealed partial record SliderIndicatorState(
    [property: NotProjected] int ActiveThumbIndex,
    bool Disabled,
    bool Dragging,
    Orientation Orientation,
    [property: NotProjected] double Max,
    [property: NotProjected] double Min,
    [property: NotProjected] double MinStepsBetweenValues,
    [property: NotProjected] double Step,
    [property: NotProjected] IReadOnlyList<double> Values,
    bool Touched,
    bool Dirty,
    bool? Valid,
    bool Filled,
    bool Focused);

// SLD §3.6; slider/indicator/SliderIndicatorDataAttributes.ts, in its order.
/// <summary>Provides the names of the data attributes a slider indicator renders.</summary>
public static class SliderIndicatorDataAttributes
{
    /// <summary>Represents the attribute present, with an empty value, while the user drags a thumb: <c>data-dragging</c>.</summary>
    public const string Dragging = "data-dragging";

    /// <summary>Represents the attribute whose value is the orientation, <c>horizontal</c> or <c>vertical</c>: <c>data-orientation</c>.</summary>
    public const string Orientation = "data-orientation";

    /// <summary>Represents the attribute present, with an empty value, while the slider is disabled: <c>data-disabled</c>.</summary>
    public const string Disabled = "data-disabled";

    /// <summary>Represents the attribute present, with an empty value, while the field is valid: <c>data-valid</c>.</summary>
    public const string Valid = "data-valid";

    /// <summary>Represents the attribute present, with an empty value, while the field is invalid: <c>data-invalid</c>.</summary>
    public const string Invalid = "data-invalid";

    /// <summary>Represents the attribute present, with an empty value, after the field was touched: <c>data-touched</c>.</summary>
    public const string Touched = "data-touched";

    /// <summary>Represents the attribute present, with an empty value, while the value differs from the initial one: <c>data-dirty</c>.</summary>
    public const string Dirty = "data-dirty";

    /// <summary>Represents the attribute present, with an empty value, while a thumb has focus: <c>data-focused</c>.</summary>
    public const string Focused = "data-focused";
}

// Base UI's Slider.Indicator (slider/indicator/SliderIndicator.tsx); the contract is docs/components/slider.md §3.6, §5.4.
// C# renders the initial inline style once (prerender and the first render; ADR 04 Amendments 2026-10-18): centre mode at its
// percentages at once (SLD-ANI-05), the inset modes hidden with zero variables (SLD-ANI-02/04). From the attach of the
// Root's `slider` handle the module writes the element's whole style in the `component-under` band, below the consumer's
// style, which travels to it in the options (ADR 04 Amendments 2026-11-18); C# keeps rendering the style it attached with.
// The marker attribute is withdrawn with the pre-hydration script (SLD-DOM-07; ADR 12 Amendments 2026-11-18 item 6).
/// <summary>
/// Represents the filled part of a slider's track: Base UI's <c>Slider.Indicator</c>, rendered as a <c>div</c> that spans from
/// the start of the track to the thumb, or between the first and the last thumb of a range slider.
/// </summary>
public sealed class SliderIndicator : PartBase<SliderIndicatorState>
{
    private const string DefaultTag = "div";

    private static readonly StateAttributesMapping<SliderIndicatorState> s_mapping = FieldValidityMapping.Create<SliderIndicatorState>();

    private SliderRootContext? context;
    private SliderIndicatorState? state;
    private SliderStateKey stateKey;
    private readonly SliderIndicatorEntry entry;
    private ElementReference? publishedElement;
    private bool frozen;
    private string? frozenStyle;
    private string? renderedStyle;
    private bool disposed;

    /// <summary>Initializes a new instance of the <see cref="SliderIndicator"/> class.</summary>
    public SliderIndicator()
    {
        entry = new SliderIndicatorEntry { Part = this };
    }

    private bool HasStateFunctions => ClassValue is not null || StyleValue is not null || RenderFunction is not null;

    [CascadingParameter]
    private SliderRootContext? RootContext { get; set; }

    /// <inheritdoc />
    protected override string DefaultTagName => DefaultTag;

    /// <inheritdoc />
    protected override SliderIndicatorState State
    {
        get
        {
            UpdateState();
            return state!;
        }
    }

    /// <inheritdoc />
    protected override StateAttributesMapping<SliderIndicatorState>? StateAttributesMapping => s_mapping;

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">The indicator is not placed within a <see cref="SliderRoot"/>.</exception>
    protected override void OnInitialized()
    {
        context = SliderRootContext.Require(RootContext);
        base.OnInitialized();
        context.AddIndicator(entry);
    }

    /// <inheritdoc />
    protected override void OnAfterRender(bool firstRender)
    {
        if (disposed)
        {
            return;
        }

        if (!Nullable.Equals(publishedElement, Element))
        {
            publishedElement = Element;
            context!.RequestSync();
        }
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing && !disposed)
        {
            disposed = true;
            context?.RemoveIndicator(entry);
        }

        base.Dispose(disposing);
    }

    /// <inheritdoc />
    protected override void SubscribeSelectors()
    {
        SelectorSubscription<SliderStoreState> subscription = SubscribeTo(context!.Store);
        subscription.Select(SliderStateKey.Projected);
        subscription.Select(s => HasStateFunctions ? SliderStateKey.From(s) : default);

        // The style C# renders before the module attaches follows the values; frozen from then on.
        // Read while not frozen only, so the attach itself renders nothing.
        subscription.Select(s => frozen ? default : (s.ValueVersion, s.Inset, s.Min, s.Max, s.Orientation));
    }

    // ADR 04 Amendments 2026-10-18 (the handover): the style rendered at attach stays; the consumer's later style travels to
    // the module, under which it writes its own.
    /// <inheritdoc />
    protected override void FinishAttributes(AttributeList attributes)
    {
        string? consumerStyle = attributes.TryGetValue(MergeProps.StyleAttribute, out AttributeValue style) && style.TryGetValue(out string text) ? text : null;
        if (!string.Equals(entry.ConsumerStyle, consumerStyle, StringComparison.Ordinal))
        {
            entry.ConsumerStyle = consumerStyle;
            context!.RequestSync();
        }

        // The handover: the style this element last rendered stays from the attach on.
        if (!frozen && context!.State.Attached)
        {
            frozen = true;
            frozenStyle = renderedStyle ?? MergeProps.MergeStyles(SliderStyles.Indicator(context.State), consumerStyle);
        }

        string? rendered = frozen ? frozenStyle : MergeProps.MergeStyles(SliderStyles.Indicator(context!.State), consumerStyle);
        renderedStyle = rendered;
        if (rendered is null)
        {
            attributes.Remove(MergeProps.StyleAttribute);
        }
        else
        {
            attributes.Set(MergeProps.StyleAttribute, rendered);
        }
    }

    // Returns whether the state changed; a new record is made only then (ADR 16 rule 7).
    private bool UpdateState()
    {
        SliderStateKey key = SliderStateKey.From(context!.State);
        if (state is not null && key == stateKey)
        {
            return false;
        }

        stateKey = key;
        state = new SliderIndicatorState(key.ActiveThumbIndex, key.Disabled, key.Dragging, key.Orientation, key.Max, key.Min, key.MinStepsBetweenValues, key.Step, key.Values, key.Field.Touched, key.Field.Dirty, key.Field.Valid, key.Field.Filled, key.Field.Focused);
        return true;
    }
}

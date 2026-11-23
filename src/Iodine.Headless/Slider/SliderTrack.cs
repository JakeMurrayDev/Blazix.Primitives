using Iodine.Headless.Rendering;
using Iodine.Headless.Store;
using Microsoft.AspNetCore.Components;

namespace Iodine.Headless;

// SLD §3.5: the Root's state object (slider/track/SliderTrack.tsx:40).
/// <summary>Represents the state of a slider track, which the part exposes as data attributes and passes to its class and style functions.</summary>
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
public sealed partial record SliderTrackState(
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

// SLD §3.5; slider/track/SliderTrackDataAttributes.ts, in its order.
/// <summary>Provides the names of the data attributes a slider track renders.</summary>
public static class SliderTrackDataAttributes
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

// Base UI's Slider.Track (slider/track/SliderTrack.tsx); the contract is docs/components/slider.md §3.5, §5.3. Structural: the
// inline `position: relative`, which the consumer's style overrides key by key (SLD-STR-04, SLD-DOM-04), and the state
// attributes; no handler, no ARIA attribute, no id.
/// <summary>
/// Represents the track of a slider: Base UI's <c>Slider.Track</c>, rendered as a <c>div</c> with <c>position: relative</c>,
/// the containing block of the indicator and the thumbs.
/// </summary>
public sealed class SliderTrack : PartBase<SliderTrackState>
{
    private const string DefaultTag = "div";

    private static readonly StateAttributesMapping<SliderTrackState> s_mapping = FieldValidityMapping.Create<SliderTrackState>();

    private SliderRootContext? context;
    private SliderTrackState? state;
    private SliderStateKey stateKey;

    private bool HasStateFunctions => ClassValue is not null || StyleValue is not null || RenderFunction is not null;

    [CascadingParameter]
    private SliderRootContext? RootContext { get; set; }

    /// <inheritdoc />
    protected override string DefaultTagName => DefaultTag;

    /// <inheritdoc />
    protected override SliderTrackState State
    {
        get
        {
            UpdateState();
            return state!;
        }
    }

    /// <inheritdoc />
    protected override StateAttributesMapping<SliderTrackState>? StateAttributesMapping => s_mapping;

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">The track is not placed within a <see cref="SliderRoot"/>.</exception>
    protected override void OnInitialized()
    {
        context = SliderRootContext.Require(RootContext);
        base.OnInitialized();
    }

    /// <inheritdoc />
    protected override void SubscribeSelectors()
    {
        SelectorSubscription<SliderStoreState> subscription = SubscribeTo(context!.Store);
        subscription.Select(SliderStateKey.Projected);
        subscription.Select(s => HasStateFunctions ? SliderStateKey.From(s) : default);
    }

    // SLD-DOM-04: the part's own declaration, before the consumer's style.
    /// <inheritdoc />
    protected override void AddPartAttributes(AttributeList attributes)
    {
        attributes.Set(MergeProps.StyleAttribute, "position: relative");
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
        state = new SliderTrackState(key.ActiveThumbIndex, key.Disabled, key.Dragging, key.Orientation, key.Max, key.Min, key.MinStepsBetweenValues, key.Step, key.Values, key.Field.Touched, key.Field.Dirty, key.Field.Valid, key.Field.Filled, key.Field.Focused);
        return true;
    }
}

using Iodine.Headless.Rendering;
using Iodine.Headless.Store;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Iodine.Headless;

// SLD §3.3: the Root's state object (slider/value/SliderValue.tsx:63).
/// <summary>Represents the state of a slider value, which the part exposes as data attributes and passes to its class and style functions.</summary>
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
public sealed partial record SliderValueState(
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

// SLD §3.3; slider/value/SliderValueDataAttributes.ts, in its order.
/// <summary>Provides the names of the data attributes a slider value renders.</summary>
public static class SliderValueDataAttributes
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

// Base UI's Slider.Value (slider/value/SliderValue.tsx); the contract is docs/components/slider.md §3.3, §5.7.
// - An `output` whose `for` is every registered thumb's input id in index order, absent until the thumb registry first
//   reports (SLD-DOM-17, SLD-ANI-06); `aria-live="off"` unless the consumer gives one (SLD-API-26).
// - Its text: ValueContent with the formatted values and the values (the MeterValue precedent), else the consumer's
//   ChildContent, else every value formatted and joined with " – " (SLD-DOM-18).
/// <summary>
/// Represents the text that shows a slider's values: Base UI's <c>Slider.Value</c>, rendered as an <c>output</c> associated
/// with the thumbs' inputs.
/// </summary>
/// <remarks>
/// The part shows each value formatted with the root's format string and culture, joined with an en dash for a range
/// slider. Use <see cref="ValueContent"/> to render something else.
/// </remarks>
public sealed class SliderValue : PartBase<SliderValueState>
{
    private const string DefaultTag = "output";
    private const string Separator = " \u2013 ";

    private static readonly StateAttributesMapping<SliderValueState> s_mapping = FieldValidityMapping.Create<SliderValueState>();

    private readonly RenderFragment content;
    private SliderRootContext? context;
    private SliderValueState? state;
    private SliderStateKey stateKey;
    private RenderFragment? consumerContent;

    /// <summary>Initializes a new instance of the <see cref="SliderValue"/> class.</summary>
    public SliderValue()
    {
        content = RenderContent;
    }

    private bool HasStateFunctions => ClassValue is not null || StyleValue is not null || RenderFunction is not null;

    [CascadingParameter]
    private SliderRootContext? RootContext { get; set; }

    // SLD-API-26; ADR 08 Amendments 2026-10-20: a template parameter is named for what it renders and used with Context.
    /// <summary>Gets or sets the template that renders the content of the element.</summary>
    /// <value>
    /// A template that receives the formatted values and the values, such as
    /// <c>&lt;ValueContent Context="value"&gt;@value.FormattedValues[0]&lt;/ValueContent&gt;</c>; or <see langword="null"/>
    /// to render the child content, or the formatted values joined with an en dash when there is none. It runs on every render.
    /// </value>
    [Parameter]
    public RenderFragment<SliderValueArguments>? ValueContent { get; set; }

    /// <inheritdoc />
    protected override string DefaultTagName => DefaultTag;

    /// <inheritdoc />
    protected override SliderValueState State
    {
        get
        {
            UpdateState();
            return state!;
        }
    }

    /// <inheritdoc />
    protected override StateAttributesMapping<SliderValueState>? StateAttributesMapping => s_mapping;

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">The value is not placed within a <see cref="SliderRoot"/>.</exception>
    protected override void OnInitialized()
    {
        context = SliderRootContext.Require(RootContext);
        base.OnInitialized();
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        if (!ReferenceEquals(ChildContent, content))
        {
            consumerContent = ChildContent;
        }

        ChildContent = content;
    }

    /// <inheritdoc />
    protected override void SubscribeSelectors()
    {
        SelectorSubscription<SliderStoreState> subscription = SubscribeTo(context!.Store);
        subscription.Select(SliderStateKey.Projected);
        subscription.Select(s => HasStateFunctions ? SliderStateKey.From(s) : default);
        subscription.Select(s => (s.ValueVersion, s.Format, s.Culture, s.ThumbsVersion));
    }

    // SLD-API-26, SLD-DOM-17: aria-live and `for` before the consumer's attributes.
    /// <inheritdoc />
    protected override void AddPartAttributes(AttributeList attributes)
    {
        attributes.Set("aria-live", "off");
        if (ForIds() is { } ids)
        {
            attributes.Set("for", ids);
        }
    }

    // SLD-DOM-17: the inputs of the thumbs the registry reported, in index order.
    private string? ForIds()
    {
        if (context!.State.ThumbsVersion == 0)
        {
            return null;
        }

        IReadOnlyList<SliderThumbEntry> thumbs = context.Thumbs;
        int highest = -1;
        for (int i = 0; i < thumbs.Count; i++)
        {
            highest = Math.Max(highest, thumbs[i].Index);
        }

        string? ids = null;
        for (int index = 0; index <= highest; index++)
        {
            if (context.FindThumb(index) is { } thumb)
            {
                ids = ids is null ? thumb.InputId : ids + " " + thumb.InputId;
            }
        }

        return ids;
    }

    private void RenderContent(RenderTreeBuilder builder)
    {
        SliderStoreState s = context!.State;
        IReadOnlyList<double> values = s.Values;
        if (ValueContent is { } template)
        {
            string[] formatted = new string[values.Count];
            for (int i = 0; i < formatted.Length; i++)
            {
                formatted[i] = SliderRoot.FormatValue(values[i], s.Format, s.Culture);
            }

            builder.AddContent(0, template(new SliderValueArguments(formatted, values)));
            return;
        }

        if (consumerContent is not null)
        {
            builder.AddContent(1, consumerContent);
            return;
        }

        // SLD-DOM-18: a single value renders without the separator.
        string text = values.Count > 0 ? SliderRoot.FormatValue(values[0], s.Format, s.Culture) : string.Empty;
        for (int i = 1; i < values.Count; i++)
        {
            text += Separator + SliderRoot.FormatValue(values[i], s.Format, s.Culture);
        }

        builder.AddContent(2, text);
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
        state = new SliderValueState(key.ActiveThumbIndex, key.Disabled, key.Dragging, key.Orientation, key.Max, key.Min, key.MinStepsBetweenValues, key.Step, key.Values, key.Field.Touched, key.Field.Dirty, key.Field.Valid, key.Field.Filled, key.Field.Focused);
        return true;
    }
}

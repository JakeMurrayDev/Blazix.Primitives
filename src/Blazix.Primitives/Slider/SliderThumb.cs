using Blazix.Primitives.Rendering;
using Blazix.Primitives.Store;
using Blazix.Primitives.Utilities;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Blazix.Primitives;

// SLD §3.7: the Root's state object, extended with nothing (slider/thumb/SliderThumb.tsx:505).
/// <summary>Represents the state of a slider thumb, which the part exposes as data attributes and passes to its class and style functions.</summary>
/// <param name="ActiveThumbIndex">The index of the thumb whose input has focus, or -1.</param>
/// <param name="Disabled">Whether the slider is disabled; projected as <c>data-disabled</c>. A thumb disabled on its own does not set it.</param>
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
public sealed partial record SliderThumbState(
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

// SLD §3.7; slider/thumb/SliderThumbDataAttributes.ts, in its order.
/// <summary>Provides the names of the data attributes a slider thumb renders.</summary>
public static class SliderThumbDataAttributes
{
    /// <summary>Represents the attribute whose value is the thumb's index as a decimal string: <c>data-index</c>.</summary>
    public const string Index = "data-index";

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

// Base UI's Slider.Thumb (slider/thumb/SliderThumb.tsx); the contract is docs/components/slider.md §3.7, §5.5, §5.6.
// - The host element carries the id (the consumer's or a generated one, SLD-API-25), data-index (SLD-DOM-08) and the
//   inline style C# renders once under the handover (SLD-DOM-09..12; ADR 04 Amendments 2026-10-18): from the attach of
//   the Root's `slider` handle the module writes the element's whole style, in the `component-under` band below the
//   consumer's style, which travels to it in the options (ADR 04 Amendments 2026-11-18).
// - Its children are the consumer's, then the nested `<input type="range">` (SLD-STR-02, SLD-API-24), for every
//   substitution form: the part replaces ChildContent. The input carries every ARIA attribute (SLD-ARIA-01..06, 08) and
//   its value; the module writes the value from attach (decision 14 (a): SLD-DOM-16's Writer cell becomes C# → JS).
// - The consumer's aria-label, aria-valuetext, aria-labelledby, aria-describedby and tabindex go on the input
//   (SLD-API-18..20, 23); its onfocus, onblur and onkeydown are declined on the host and run from the module's reports, or
//   inside the `key` decision on WebAssembly (SLD-API-22).
// - It registers with the Root's thumb list (SLD-API-17); a range thumb's index is its Index, else its registry index.
/// <summary>
/// Represents a draggable thumb of a slider: Base UI's <c>Slider.Thumb</c>, rendered as a <c>div</c> that holds a visually
/// hidden <c>input type="range"</c>, the thumb's focusable element.
/// </summary>
/// <remarks>
/// Render one thumb per value. The input carries the slider role, the ARIA attributes and the form value; the
/// <c>aria-label</c>, <c>aria-valuetext</c>, <c>aria-labelledby</c>, <c>aria-describedby</c>, <c>tabindex</c>,
/// <c>onfocus</c>, <c>onblur</c> and <c>onkeydown</c> given to the thumb apply to the input.
/// </remarks>
public sealed class SliderThumb : PartBase<SliderThumbState>
{
    private const string DefaultTag = "div";

    private static readonly StateAttributesMapping<SliderThumbState> s_mapping = FieldValidityMapping.Create<SliderThumbState>();

    private readonly RenderFragment content;
    private readonly SliderThumbEntry entry = new();
    private SliderRootContext? context;
    private CompositeListItemBehavior? listItem;
    private RenderFragment? consumerContent;
    private SliderThumbState? state;
    private SliderStateKey stateKey;
    private string? inputId;
    private string? consumerAriaLabel;
    private string? consumerAriaValueText;
    private string? consumerAriaLabelledBy;
    private string? consumerAriaDescribedBy;
    private string? consumerTabIndex;
    private bool frozen;
    private string? frozenStyle;
    private string? frozenValue;
    private string? renderedStyle;
    private string? renderedValue;
    private bool disposed;

    /// <summary>Initializes a new instance of the <see cref="SliderThumb"/> class.</summary>
    public SliderThumb()
    {
        content = RenderContent;
        entry.Owner = this;
    }

    [Inject]
    private PartIdGenerator IdGenerator { get; set; } = null!;

    private int ThumbIndex => context!.State.Range ? Index ?? listItem?.Index ?? -1 : 0;

    private bool HasStateFunctions => ClassValue is not null || StyleValue is not null || RenderFunction is not null;

    [CascadingParameter]
    private SliderRootContext? RootContext { get; set; }

    // SLD-API-17.
    /// <summary>Gets or sets the index of the value the thumb represents.</summary>
    /// <value>
    /// The index into the list value of a range slider; <see langword="null"/>, the default, for the thumb's position among
    /// the slider's thumbs. A single-thumb slider ignores it. Set it on each thumb of a range slider that is prerendered.
    /// </value>
    [Parameter]
    public int? Index { get; set; }

    // SLD-API-16.
    /// <summary>Gets or sets a value indicating whether this thumb ignores user interaction.</summary>
    /// <value><see langword="true"/> to disable the thumb alone; the slider's disabled state applies too. The default is <see langword="false"/>.</value>
    [Parameter]
    public bool Disabled { get; set; }

    // SLD-API-18.
    /// <summary>Gets or sets the function that returns the input's <c>aria-label</c>.</summary>
    /// <value>A function from the thumb's index to the label, which wins over an <c>aria-label</c> attribute; or <see langword="null"/>.</value>
    [Parameter]
    public Func<int, string?>? GetAriaLabel { get; set; }

    // SLD-API-19.
    /// <summary>Gets or sets the function that returns the input's <c>aria-valuetext</c>.</summary>
    /// <value>
    /// A function that receives the formatted value, the value and the thumb's index and returns the text announced by
    /// assistive technology, which wins over an <c>aria-valuetext</c> attribute; or <see langword="null"/>.
    /// </value>
    [Parameter]
    public Func<string, double, int, string?>? GetAriaValueText { get; set; }

    /// <inheritdoc />
    protected override string DefaultTagName => DefaultTag;

    // SLD-API-25: the host carries the id, the consumer's or a generated one, which the registry resolves.
    /// <inheritdoc />
    protected override bool UsesGeneratedId => true;

    /// <inheritdoc />
    protected override SliderThumbState State
    {
        get
        {
            UpdateState();
            return state!;
        }
    }

    /// <inheritdoc />
    protected override StateAttributesMapping<SliderThumbState>? StateAttributesMapping => s_mapping;

    /// <summary>Gets the id of the thumb's input.</summary>
    /// <value>The id the input renders.</value>
    internal string? InputId => inputId;

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">The thumb is not placed within a <see cref="SliderRoot"/>.</exception>
    protected override void OnInitialized()
    {
        context = SliderRootContext.Require(RootContext);
        base.OnInitialized();

        // SLD-DOM-15: a range thumb's input has its own generated id, drawn in render order.
        inputId = IdGenerator.Next();
        listItem = new CompositeListItemBehavior(context.ThumbList, OnRegistryIndexChanged);
        context.AddThumb(entry);
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        ReadConsumerAttributes();
        // SLD §16 Q7 (decision 23): a range thumb with Index registers with it.
        listItem!.SetRegistration(Id, context!.State.Range ? Index : null);
        entry.OwnDisabled = Disabled;
        entry.Index = ThumbIndex;
        entry.InputId = CurrentInputId();
        UpdateState();

        // SLD-STR-02, SLD-API-24: the consumer's children, then the input, for every substitution form.
        if (!ReferenceEquals(ChildContent, content))
        {
            consumerContent = ChildContent;
        }

        ChildContent = content;
    }

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (disposed)
        {
            return;
        }

        await listItem!.AfterRenderAsync(Element);
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing && !disposed)
        {
            disposed = true;
            listItem?.Dispose();
            if (context is not null)
            {
                context.RemoveThumb(entry);
                context.Field.Release(this);
            }
        }

        base.Dispose(disposing);
    }

    /// <inheritdoc />
    protected override void SubscribeSelectors()
    {
        SelectorSubscription<SliderStoreState> subscription = SubscribeTo(context!.Store);
        subscription.Select(SliderStateKey.Projected);
        subscription.Select(s => HasStateFunctions ? SliderStateKey.From(s) : default);

        // What the input renders: its value, its text and its attributes.
        subscription.Select(s => ThumbValue(s));
        subscription.Select(s => (s.Values.Count, s.Range, s.Min, s.Max, s.Step, s.Rtl, s.ThumbsVersion));
        subscription.Select(s => (s.Name, s.Form, s.LabelledBy, s.Format, s.Culture));
    }

    // SLD-API-22: the consumer's focus, blur and key handlers are the input's, run from the module's reports.
    /// <inheritdoc />
    protected override bool ForwardsConsumerHandler(string eventName) =>
        eventName is not ("onfocus" or "onblur" or "onkeydown");

    // SLD-DOM-08, SLD-DOM-13: data-index; no role, tabindex or ARIA attribute of the thumb's own.
    /// <inheritdoc />
    protected override void AddPartAttributes(AttributeList attributes)
    {
        attributes.Set(SliderThumbDataAttributes.Index, ThumbIndex.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    // SLD-API-18..20, 23: consumed attributes leave the host; ADR 04 Amendments 2026-10-18 (the handover): the style C#
    // rendered at attach stays, the consumer's later style travels to the module.
    /// <inheritdoc />
    protected override void FinishAttributes(AttributeList attributes)
    {
        attributes.Remove("aria-label");
        attributes.Remove("aria-valuetext");
        attributes.Remove("aria-labelledby");
        attributes.Remove("aria-describedby");
        attributes.Remove("tabindex");
        string? consumerStyle = attributes.TryGetValue(MergeProps.StyleAttribute, out AttributeValue style) && style.TryGetValue(out string text) ? text : null;
        if (!string.Equals(entry.ConsumerStyle, consumerStyle, StringComparison.Ordinal))
        {
            entry.ConsumerStyle = consumerStyle;
            context!.RequestSync();
        }

        // The handover: the style and the input value this element last rendered stay from the attach on.
        if (!frozen && context!.State.Attached)
        {
            frozen = true;
            frozenStyle = renderedStyle ?? MergeProps.MergeStyles(SliderStyles.Thumb(context.State, ThumbIndex), consumerStyle);
            frozenValue = renderedValue ?? InputValue();
        }

        string? rendered = frozen ? frozenStyle : MergeProps.MergeStyles(SliderStyles.Thumb(context!.State, ThumbIndex), consumerStyle);
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

    private static double ThumbValue(SliderStoreState s, int index) => index >= 0 && index < s.Values.Count ? s.Values[index] : double.NaN;

    private double ThumbValue(SliderStoreState s) => ThumbValue(s, ThumbIndex);

    // SLD-DOM-15: the field's control id for a single thumb, a generated id per thumb of a range slider.
    private string CurrentInputId() => !context!.State.Range && context.Field.InField && context.Field.ControlId is { } controlId ? controlId : inputId!;

    private void ReadConsumerAttributes()
    {
        bool hadKeyDown = entry.KeyDown.IsHandler;
        consumerAriaLabel = null;
        consumerAriaValueText = null;
        consumerAriaLabelledBy = null;
        consumerAriaDescribedBy = null;
        consumerTabIndex = null;
        entry.KeyDown = default;
        entry.Focus = default;
        entry.Blur = default;
        if (AdditionalAttributes is not null)
        {
            ReadAttributes(AdditionalAttributes);
        }

        if (hadKeyDown != entry.KeyDown.IsHandler)
        {
            context!.RequestSync();
        }
    }

    private void ReadAttributes(IReadOnlyDictionary<string, object> attributes)
    {
        foreach (KeyValuePair<string, object> pair in attributes)
        {
            switch (pair.Key.ToLowerInvariant())
            {
                case "aria-label":
                    consumerAriaLabel = pair.Value as string;
                    break;
                case "aria-valuetext":
                    consumerAriaValueText = pair.Value as string;
                    break;
                case "aria-labelledby":
                    consumerAriaLabelledBy = pair.Value as string;
                    break;
                case "aria-describedby":
                    consumerAriaDescribedBy = pair.Value as string;
                    break;
                case "tabindex":
                    consumerTabIndex = Convert.ToString(pair.Value, System.Globalization.CultureInfo.InvariantCulture);
                    break;
                case "onkeydown":
                    entry.KeyDown = Handler(pair.Key, pair.Value);
                    break;
                case "onfocus":
                    entry.Focus = Handler(pair.Key, pair.Value);
                    break;
                case "onblur":
                    entry.Blur = Handler(pair.Key, pair.Value);
                    break;
            }
        }
    }

    private static AttributeValue Handler(string name, object value)
    {
        AttributeValue attribute = AttributeValue.FromObject(value);
        return MergeProps.IsHandler(name, attribute) ? attribute : default;
    }

    private void OnRegistryIndexChanged()
    {
        if (disposed)
        {
            return;
        }

        // The index renders as data-index and decides the value the input shows.
        entry.Index = ThumbIndex;
        context!.RequestSync();
        StateHasChanged();
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
        state = new SliderThumbState(key.ActiveThumbIndex, key.Disabled, key.Dragging, key.Orientation, key.Max, key.Min, key.MinStepsBetweenValues, key.Step, key.Values, key.Field.Touched, key.Field.Dirty, key.Field.Valid, key.Field.Filled, key.Field.Focused);
        return true;
    }

    // SLD-DOM-16: the value, serialized per UTL-DOM-37; empty when the index has no value. Frozen from attach.
    private string InputValue()
    {
        double value = ThumbValue(context!.State);
        return double.IsNaN(value) ? string.Empty : NumberSerializer.Serialize(value);
    }

    // SLD-ARIA-04: two values announce "start range" and "end range"; otherwise the formatted value only with a Format.
    private string? DefaultValueText(SliderStoreState s, int index, double value)
    {
        if (index < 0 || double.IsNaN(value))
        {
            return null;
        }

        if (s.Values.Count == 2)
        {
            return SliderRoot.FormatValue(value, s.Format, s.Culture) + (index == 0 ? " start range" : " end range");
        }

        return s.Format is not null ? SliderRoot.FormatValue(value, s.Format, s.Culture) : null;
    }

    private void RenderContent(RenderTreeBuilder builder)
    {
        UpdateState();
        SliderStoreState s = context!.State;
        int index = ThumbIndex;
        double value = ThumbValue(s, index);
        bool disabled = Disabled || s.Disabled;
        bool hasValue = !double.IsNaN(value);
        string? ariaLabel = GetAriaLabel is { } getLabel ? getLabel(index) : consumerAriaLabel;
        string? labelledBy = consumerAriaLabelledBy ?? (ariaLabel is null ? s.LabelledBy : null);
        string? valueText = GetAriaValueText is { } getText
            ? getText(SliderRoot.FormatValue(value, s.Format, s.Culture), value, index)
            : consumerAriaValueText ?? DefaultValueText(s, index, value);
        string? describedBy = context.Field.DescribedBy(consumerAriaDescribedBy);
        entry.InputId = CurrentInputId();

        builder.AddContent(0, consumerContent);
        builder.OpenElement(1, "input");
        if (ariaLabel is not null)
        {
            builder.AddAttribute(2, "aria-label", ariaLabel);
        }

        if (labelledBy is not null)
        {
            builder.AddAttribute(3, "aria-labelledby", labelledBy);
        }

        if (describedBy is not null)
        {
            builder.AddAttribute(4, "aria-describedby", describedBy);
        }

        builder.AddAttribute(5, "aria-orientation", s.Orientation == Orientation.Vertical ? "vertical" : "horizontal");
        if (hasValue)
        {
            builder.AddAttribute(6, "aria-valuenow", NumberSerializer.Serialize(value));
        }

        if (valueText is not null)
        {
            builder.AddAttribute(7, "aria-valuetext", valueText);
        }

        if (disabled)
        {
            builder.AddAttribute(8, "disabled", true);
        }

        if (s.Form is not null)
        {
            builder.AddAttribute(9, "form", s.Form);
        }

        builder.AddAttribute(10, "id", entry.InputId);
        builder.AddAttribute(11, "max", NumberSerializer.Serialize(s.Max));
        builder.AddAttribute(12, "min", NumberSerializer.Serialize(s.Min));
        if (s.Name is not null)
        {
            builder.AddAttribute(13, "name", s.Name);
        }

        builder.AddAttribute(14, "step", NumberSerializer.Serialize(s.Step));
        builder.AddAttribute(15, "style", SliderStyles.Input(s.Orientation == Orientation.Vertical, s.Rtl));
        if (consumerTabIndex is not null)
        {
            builder.AddAttribute(16, "tabindex", consumerTabIndex);
        }

        builder.AddAttribute(17, "type", "range");
        renderedValue = frozen ? frozenValue : InputValue();
        builder.AddAttribute(18, "value", renderedValue);
        if (context.Field.AriaInvalid(disabled))
        {
            builder.AddAttribute(19, "aria-invalid", "true");
        }

        builder.CloseElement();
    }
}

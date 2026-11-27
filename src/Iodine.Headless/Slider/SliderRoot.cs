using System.Globalization;
using System.Linq.Expressions;
using Iodine.Headless.Interop;
using Iodine.Headless.Rendering;
using Iodine.Headless.Store;
using Iodine.Headless.Utilities;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace Iodine.Headless;

// SLD §3.1's state object (slider/root/SliderRoot.tsx:274-299,406-446): the field's flags with the slider's fields on top,
// the same fourteen fields on every part (SLD-STA-05). The six numeric and list fields are never projected (SLD-DOM-02).
/// <summary>Represents the state of a slider root, which the part exposes as data attributes and passes to its class and style functions.</summary>
/// <param name="ActiveThumbIndex">The index of the thumb whose input has focus, or -1.</param>
/// <param name="Disabled">Whether the slider is disabled; projected as <c>data-disabled</c>.</param>
/// <param name="Dragging">Whether a pointer or touch interaction is in progress; projected as <c>data-dragging</c>.</param>
/// <param name="Orientation">The axis of the slider; projected as <c>data-orientation</c>.</param>
/// <param name="Max">The upper bound.</param>
/// <param name="Min">The lower bound.</param>
/// <param name="MinStepsBetweenValues">The minimum number of steps between adjacent values.</param>
/// <param name="Step">The step.</param>
/// <param name="Values">The normalized values, one per thumb, also for a single-thumb slider.</param>
/// <param name="Touched">Whether the field was touched; projected as <c>data-touched</c>.</param>
/// <param name="Dirty">Whether the value differs from the field's initial value; projected as <c>data-dirty</c>.</param>
/// <param name="Valid">The field's verdict: <c>data-valid</c> when <see langword="true"/>, <c>data-invalid</c> when <see langword="false"/>.</param>
/// <param name="Filled">Whether the field is filled; projected as <c>data-filled</c>, never set by the slider.</param>
/// <param name="Focused">Whether a thumb input has focus; projected as <c>data-focused</c>.</param>
[PartState]
public sealed partial record SliderRootState(
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

// SLD §3.1; slider/root/SliderRootDataAttributes.ts, in its order.
/// <summary>Provides the names of the data attributes a slider root renders.</summary>
public static class SliderRootDataAttributes
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

// Base UI's Slider.Root (slider/root/SliderRoot.tsx); the contract is docs/components/slider.md.
// - One `div role="group"` with its id and aria-labelledby, the consumer's attributes, then the field's validation
//   attributes (SLD-DOM-01, SLD-ARIA-07..10); no tabindex and no handler.
// - The value is two typed triples (ADR 08 Amendments 2026-11-18, the Slider value shape, chosen by the user 2026-11-18):
//   Value for one thumb, Values for a range, each a Controlled<T> fixed at the first parameter set (SLD-STA-01; ADR 05
//   Amendments 2026-10-18); the triple supplied is the mode (SLD-API-02's "same kind"), and both are rejected. The store
//   holds the normalized list (SLD-STA-03), the Root the raw value, as the internal SliderValues, for SLD-STA-09's equality.
// - Every request comes from the `slider` module (SliderTwin, one handle on this element; design point 1 (a') of track 4):
//   OnValueChange with SliderChangeEventDetails before anything changes, a cancel stops it, then ValueChanged and the commit
//   (EVD-ALG-03, SLD-EVT-01..12). Requests run one at a time, in the order the module sent them. The module moves the
//   thumb before it reports (the optimistic move, ADR 04 Amendments 2026-11-18): the answering update carries the
//   report's revision, and a refused request rolls the module back.
// - The thumbs register in a registration-only composite list (SLD §11's CMP row), with their explicit index when given
//   (decision 23, the Composite/ grant; SLD-EDGE-14's dated deviation).
// - SliderField is the field half (ADR 05 Amendments 2026-11-18 item 2).
/// <summary>
/// Represents the container of a slider: Base UI's <c>Slider.Root</c>, rendered as a <c>div</c> with the <c>group</c> role.
/// It owns the value, one number or a list of numbers, and groups the other slider parts.
/// </summary>
/// <remarks>
/// A number makes a single-thumb slider; a list makes a range slider with one <see cref="SliderThumb"/> per element. Values
/// are limited to <see cref="Min"/> and <see cref="Max"/> and kept in ascending order for display; pointer input snaps to
/// the <see cref="Step"/> grid that starts at <see cref="Min"/>.
/// </remarks>
public sealed class SliderRoot : PartBase<SliderRootState>
{
    private const string DefaultTag = "div";
    private const string DefaultFormat = "#,##0.###";

    private static readonly StateAttributesMapping<SliderRootState> s_mapping = FieldValidityMapping.Create<SliderRootState>();
    private static readonly IReadOnlyList<double> s_empty = [];

    private readonly Action<SliderValueRequest> onValueRequest;
    private readonly Action<SliderGestureReport> onGesture;
    private readonly Action<SliderFocusReport> onFocus;
    private readonly Action<SliderKeyReport> onKey;
    private readonly Action directionChanged;
    private readonly Action labelChanged;
    private readonly TaskCompletionSource attachedSource = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private Controlled<double?>? valueState;
    private Controlled<IReadOnlyList<double>?>? valuesState;
    private bool range;
    private SliderRootContext? context;
    private SliderField? field;
    private CompositeListBehavior? thumbList;
    private SliderRootState? state;
    private RenderFragment? cascade;
    private RenderFragment? cascadedContent;
    private SliderTwin? twin;
    private TwinHandle<SliderOptions>? handle;
    private SliderOptions? sentOptions;
    private bool attaching;
    private bool syncPosted;
    private Task requests = Task.CompletedTask;
    private int revision;
    private bool rollbackPending;
    private bool gestureActive;
    private SliderValues? gestureValue;
    private EventReason lastChangeReason = EventReason.None;
    private object? boxedFieldValue;
    private int boxedVersion = -1;
    private bool warnedRange;
    private bool disposed;

    /// <summary>Initializes a new instance of the <see cref="SliderRoot"/> class.</summary>
    public SliderRoot()
    {
        onValueRequest = request => _ = InvokeAsync(() => Enqueue(() => HandleValueRequestAsync(request)));
        onGesture = report => _ = InvokeAsync(() => Enqueue(() => HandleGestureAsync(report)));
        onFocus = report => _ = InvokeAsync(() => Enqueue(() => HandleFocusAsync(report)));
        onKey = report => _ = InvokeAsync(() => Enqueue(() => HandleKeyAsync(report)));
        directionChanged = OnDirectionChanged;
        labelChanged = OnLabelChanged;
    }

    [Inject]
    private ILogger<SliderRoot> Logger { get; set; } = null!;

    [Inject]
    private IJSRuntime JSRuntime { get; set; } = null!;

    [Inject]
    private PartIdGenerator IdGenerator { get; set; } = null!;

    [Inject]
    private DefaultFormScope Formless { get; set; } = null!;

    // SLD-API-01: `defaultValue ?? min`; a range slider without a list starts with one thumb at the minimum.
    private SliderValues RawValue => range ? new SliderValues(valuesState!.Value ?? [Min]) : new SliderValues(valueState!.Value ?? Min);

    [CascadingParameter]
    private FieldRootContext? FieldContext { get; set; }

    [CascadingParameter]
    private IFormScope? FormScope { get; set; }

    [CascadingParameter]
    private EditContext? CascadedEditContext { get; set; }

    [CascadingParameter]
    private DirectionProviderContext? DirectionContext { get; set; }

    // SLD-API-01/02, SLD-STA-01; ADR 05 §2; ADR 08 Amendments 2026-11-18 (the two triples).
    /// <summary>Gets or sets the value of a single-thumb slider, when the consumer owns it.</summary>
    /// <value>
    /// The value. Supplying it at the first render makes the value the consumer's for the lifetime of the root: a change then
    /// only raises <see cref="OnValueChange"/> and <see cref="ValueChanged"/>, and the slider shows the value the consumer
    /// passes back. Bind a <c>double?</c> field with <c>@bind-Value</c>; a plain <c>double</c> does not bind. Use
    /// <see cref="Values"/> instead for a range slider; supplying both throws <see cref="InvalidOperationException"/>.
    /// </value>
    [Parameter]
    public double? Value { get; set; }

    // ADR 05 §2: the binding half of @bind-Value.
    /// <summary>Gets or sets the callback that receives the new value of a single-thumb slider after a change that was not canceled.</summary>
    /// <value>The callback <c>@bind-Value</c> supplies, invoked after <see cref="OnValueChange"/>.</value>
    [Parameter]
    public EventCallback<double?> ValueChanged { get; set; }

    // SLD-API-01, SLD-STA-02.
    /// <summary>Gets or sets the initial value of a single-thumb slider, when the root owns the value.</summary>
    /// <value>The value, read at the first render only; <see langword="null"/>, the default, starts at <see cref="Min"/>.</value>
    [Parameter]
    public double? DefaultValue { get; set; }

    // SLD-API-03, SLD-EVT-01..12; ADR 05 §3 and ADR 08 Amendments 2026-10-26 (the details are a subclass).
    /// <summary>Gets or sets the callback invoked before the value of a single-thumb slider changes.</summary>
    /// <value>
    /// The callback, which receives the new value and the change details. The details are a
    /// <see cref="SliderChangeEventDetails"/>: cast <c>args.Details</c> to read the index of the thumb that moved, as in
    /// <c>((SliderChangeEventDetails)args.Details).ActiveThumbIndex</c>. The reason is <see cref="EventReason.InputChange"/>,
    /// <see cref="EventReason.Keyboard"/>, <see cref="EventReason.TrackPress"/> or <see cref="EventReason.Drag"/>, and calling
    /// <see cref="ChangeEventDetails.Cancel"/> keeps the current value.
    /// </value>
    [Parameter]
    public EventCallback<ValueChangeEventArgs<double>> OnValueChange { get; set; }

    // SLD-API-04, SLD-EVT-05.
    /// <summary>Gets or sets the callback invoked when an interaction with a single-thumb slider settles on an applied value.</summary>
    /// <value>
    /// The callback, which receives the value and the details: right after a keyboard change or a change of the native input,
    /// and once when a pointer or touch interaction ends, with the last value <see cref="OnValueChange"/> received. It is not
    /// cancelable.
    /// </value>
    [Parameter]
    public EventCallback<ValueCommittedEventArgs<double>> OnValueCommitted { get; set; }

    // ADR 05 Amendments 2026-10-22 and 2026-11-18: EditContext participation.
    /// <summary>Gets or sets the expression that identifies the bound value of a single-thumb slider in a cascaded <see cref="EditContext"/>.</summary>
    /// <value>
    /// The expression <c>@bind-Value</c> supplies, or <see langword="null"/>. Inside an <see cref="EditContext"/> the slider
    /// then notifies the context of every change, takes the identifier's field name when it has no other name, and is
    /// invalid while the context holds messages for it.
    /// </value>
    [Parameter]
    public Expression<Func<double?>>? ValueExpression { get; set; }

    // SLD-API-01/02 for a range slider: one thumb per element, even for a list of one element (SLD-STA-04).
    /// <summary>Gets or sets the values of a range slider, when the consumer owns them.</summary>
    /// <value>
    /// The values, one per thumb; a list of one element is still a range slider. Supplying it at the first render makes the
    /// values the consumer's for the lifetime of the root, as <see cref="Value"/> does for one thumb. The slider shows them
    /// limited to the range and sorted, and a change always passes a new list.
    /// </value>
    [Parameter]
    public IReadOnlyList<double>? Values { get; set; }

    /// <summary>Gets or sets the callback that receives the new values of a range slider after a change that was not canceled.</summary>
    /// <value>The callback <c>@bind-Values</c> supplies, invoked after <see cref="OnValuesChange"/>.</value>
    [Parameter]
    public EventCallback<IReadOnlyList<double>?> ValuesChanged { get; set; }

    /// <summary>Gets or sets the initial values of a range slider, when the root owns the values.</summary>
    /// <value>The values, read at the first render only, one per thumb.</value>
    [Parameter]
    public IReadOnlyList<double>? DefaultValues { get; set; }

    /// <summary>Gets or sets the callback invoked before the values of a range slider change.</summary>
    /// <value>
    /// The callback, which receives the new values and the change details, a <see cref="SliderChangeEventDetails"/> as for
    /// <see cref="OnValueChange"/>. Calling <see cref="ChangeEventDetails.Cancel"/> keeps the current values.
    /// </value>
    [Parameter]
    public EventCallback<ValueChangeEventArgs<IReadOnlyList<double>>> OnValuesChange { get; set; }

    /// <summary>Gets or sets the callback invoked when an interaction with a range slider settles on applied values.</summary>
    /// <value>The callback, which receives the values and the details, as <see cref="OnValueCommitted"/> does for one thumb.</value>
    [Parameter]
    public EventCallback<ValueCommittedEventArgs<IReadOnlyList<double>>> OnValuesCommitted { get; set; }

    /// <summary>Gets or sets the expression that identifies the bound values of a range slider in a cascaded <see cref="EditContext"/>.</summary>
    /// <value>The expression <c>@bind-Values</c> supplies, or <see langword="null"/>, as <see cref="ValueExpression"/> is for one thumb.</value>
    [Parameter]
    public Expression<Func<IReadOnlyList<double>?>>? ValuesExpression { get; set; }

    // SLD-API-05.
    /// <summary>Gets or sets a value indicating whether the slider ignores user interaction.</summary>
    /// <value><see langword="true"/> to disable the slider; combined with the enclosing field's. The default is <see langword="false"/>.</value>
    [Parameter]
    public bool Disabled { get; set; }

    // SLD-API-06.
    /// <summary>Gets or sets the lower bound.</summary>
    /// <value>The minimum, also the start of the <see cref="Step"/> grid. The default is 0.</value>
    [Parameter]
    public double Min { get; set; }

    /// <summary>Gets or sets the upper bound.</summary>
    /// <value>The maximum, which must be greater than <see cref="Min"/>. The default is 100.</value>
    [Parameter]
    public double Max { get; set; } = 100;

    // SLD-API-07.
    /// <summary>Gets or sets the step of the arrow keys and the grid of pointer input.</summary>
    /// <value>The step; decimals are allowed. The default is 1.</value>
    [Parameter]
    public double Step { get; set; } = 1;

    /// <summary>Gets or sets the step of the Page Up and Page Down keys and of Shift with an arrow key.</summary>
    /// <value>The large step. The default is 10.</value>
    [Parameter]
    public double LargeStep { get; set; } = 10;

    /// <summary>Gets or sets the minimum number of steps between adjacent values of a range slider.</summary>
    /// <value>The minimum distance in steps; fractions are allowed. The default is 0.</value>
    [Parameter]
    public double MinStepsBetweenValues { get; set; }

    // SLD-API-08.
    /// <summary>Gets or sets the axis of the slider.</summary>
    /// <value><see cref="Headless.Orientation.Horizontal"/>, the default, or <see cref="Headless.Orientation.Vertical"/>.</value>
    [Parameter]
    public Orientation Orientation { get; set; } = Orientation.Horizontal;

    // SLD-API-09; ADR 08 Amendments 2026-10-21 and 2026-11-18 (decision 10: the `"#,##0.###"` default).
    /// <summary>Gets or sets the numeric format string of the values a <see cref="SliderValue"/> shows and the thumbs announce.</summary>
    /// <value>
    /// A .NET standard or custom numeric format string, such as <c>C2</c>; or <see langword="null"/>, the default, for grouped
    /// digits with at most three fraction digits. An empty string formats as the default and counts as supplied.
    /// </value>
    [Parameter]
    public string? Format { get; set; }

    /// <summary>Gets or sets the culture that formats the values.</summary>
    /// <value>The culture, or <see langword="null"/> for <see cref="CultureInfo.CurrentCulture"/>. The default is <see langword="null"/>.</value>
    [Parameter]
    public CultureInfo? Culture { get; set; }

    // SLD-API-10.
    /// <summary>Gets or sets the form name of the thumbs' inputs.</summary>
    /// <value>The name, which the enclosing field's name overrides; or <see langword="null"/> to submit nothing.</value>
    [Parameter]
    public string? Name { get; set; }

    /// <summary>Gets or sets the id of the form that owns the thumbs' inputs when the slider is rendered outside it.</summary>
    /// <value>The form's id, placed as the <c>form</c> attribute of every input; or <see langword="null"/>.</value>
    [Parameter]
    public string? Form { get; set; }

    // SLD-API-11.
    /// <summary>Gets or sets how a thumb at the minimum or the maximum aligns with the control's edge.</summary>
    /// <value><see cref="SliderThumbAlignment.Center"/>, the default, <see cref="SliderThumbAlignment.Edge"/> or <see cref="SliderThumbAlignment.EdgeClientOnly"/>.</value>
    [Parameter]
    public SliderThumbAlignment ThumbAlignment { get; set; } = SliderThumbAlignment.Center;

    // SLD-API-12.
    /// <summary>Gets or sets what happens when a dragged thumb of a range slider reaches a neighbor.</summary>
    /// <value><see cref="SliderThumbCollisionBehavior.Push"/>, the default, <see cref="SliderThumbCollisionBehavior.Swap"/> or <see cref="SliderThumbCollisionBehavior.None"/>. Keys always stop at a neighbor.</value>
    [Parameter]
    public SliderThumbCollisionBehavior ThumbCollisionBehavior { get; set; } = SliderThumbCollisionBehavior.Push;

    /// <inheritdoc />
    protected override string DefaultTagName => DefaultTag;

    // SLD-API-13: the root renders its id, the consumer's or a generated one.
    /// <inheritdoc />
    protected override bool UsesGeneratedId => true;

    /// <inheritdoc />
    protected override SliderRootState State => state!;

    /// <inheritdoc />
    protected override StateAttributesMapping<SliderRootState>? StateAttributesMapping => s_mapping;

    // For the fixtures (tests/Iodine.Headless.Fixtures.Client): a browser test waits for the module before it presses.
    /// <summary>Gets a task that completes when the slider's browser-side behavior is first attached.</summary>
    /// <value>A task that completes after the first interactive render that showed the element.</value>
    internal Task TwinAttached => attachedSource.Task;

    /// <inheritdoc />
    public override Task SetParametersAsync(ParameterView parameters)
    {
        // ADR 08 Amendments 2026-11-18: one triple or the other, never both (the shared exclusive check).
        ListValueParameters.CheckExclusive(parameters, nameof(SliderRoot));

        // SLD-STA-01, SLD-EDGE-03: ownership and the mode are fixed at the first parameter set, with Controlled<T>'s
        // diagnostics.
        if (valueState is null)
        {
            valueState = new Controlled<double?>("Slider", "value", Logger);
            valuesState = new Controlled<IReadOnlyList<double>?>("Slider", "value", Logger);
            range = SuppliesRangeTriple(parameters);
        }

        valueState.SetParameters(parameters, nameof(Value), nameof(DefaultValue));
        valuesState!.SetParameters(parameters, nameof(Values), nameof(DefaultValues));
        return base.SetParametersAsync(parameters);
    }

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        base.OnInitialized();
        field = new SliderField(FieldContext?.Form ?? FormScope ?? Formless, OnFieldChanged);
        field.Initialize(FieldContext);
        thumbList = new CompositeListBehavior(JSRuntime, IdGenerator, StateHasChanged);
        thumbList.ItemsChanged += OnThumbsChanged;
        context = new SliderRootContext(BuildStoreState(), thumbList.Context, field, RequestSync);
        context.Label.Changed = labelChanged;
        DirectionContext?.AddListener(directionChanged);
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        // SLD-EDGE-02 (UTL-EDGE-12: once per message), in development only.
        if (Min >= Max && !warnedRange && DevelopmentWarnings.IsEnabled)
        {
            warnedRange = true;
            DevelopmentWarnings.Write(Logger, "Iodine: Slider `max` must be greater than `min`.");
        }

        if (range)
        {
            field!.SetParameters(Name, Disabled, FormScope, CascadedEditContext, ValuesExpression);
        }
        else
        {
            field!.SetParameters(Name, Disabled, FormScope, CascadedEditContext, ValueExpression);
        }
        Publish();
        UpdateState();

        // The cascade wraps the consumer's content inside the root's element: the context, then the thumbs' registry; it is
        // rebuilt only when the consumer supplied other content (the Meter precedent).
        if (cascade is null || (!ReferenceEquals(ChildContent, cascade) && !ReferenceEquals(cascadedContent, ChildContent)))
        {
            cascadedContent = ChildContent;
            cascade = context!.Cascade(thumbList!.Context.Cascade(cascadedContent));
        }

        ChildContent = cascade;
    }

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (disposed)
        {
            return;
        }

        await thumbList!.AfterRenderAsync(Enabled, Element, Id);
        AfterRenderField();
        await SyncTwinAsync();
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing && !disposed)
        {
            disposed = true;
            DirectionContext?.RemoveListener(directionChanged);
            if (context is not null)
            {
                context.Label.Changed = null;
            }

            if (thumbList is not null)
            {
                thumbList.ItemsChanged -= OnThumbsChanged;
                thumbList.Dispose();
            }

            field?.Dispose();
            if (twin is not null)
            {
                DetachTwinEvents(twin);
                _ = twin.DisposeAsync().AsTask();
                twin = null;
                handle = null;
            }
        }

        base.Dispose(disposing);
    }

    // SLD-DOM-01, SLD-ARIA-07, SLD-ARIA-10: role and the resolved aria-labelledby before the consumer's attributes, so a
    // consumer role wins.
    /// <inheritdoc />
    protected override void AddPartAttributes(AttributeList attributes)
    {
        thumbList!.AddAttributes(attributes, Id);
        if (context!.State.LabelledBy is { } labelledBy)
        {
            attributes.Set("aria-labelledby", labelledBy);
        }

        attributes.Set("role", "group");
    }

    // SLD-ARIA-09: the field's validation attributes run last, after the consumer's.
    /// <inheritdoc />
    protected override void FinishAttributes(AttributeList attributes)
    {
        if (!field!.InField)
        {
            return;
        }

        string? consumer = attributes.TryGetValue("aria-describedby", out AttributeValue current) && current.TryGetValue(out string text) ? text : null;
        if (field.DescribedBy(consumer) is { } describedBy)
        {
            attributes.Set("aria-describedby", describedBy);
        }
        else
        {
            attributes.Remove("aria-describedby");
        }

        if (field.AriaInvalid(elementDisabled: false) && !field.Disabled)
        {
            attributes.Set("aria-invalid", "true");
        }
    }

    // SLD-STA-03: single mode `[clamp(raw)]`; range mode every element clamped, then sorted ascending.
    internal static IReadOnlyList<double> Normalize(SliderValues raw, double min, double max)
    {
        if (raw.TryGetValue(out IReadOnlyList<double>? list))
        {
            double[] values = new double[list.Count];
            for (int i = 0; i < values.Length; i++)
            {
                values[i] = RangeMath.Clamp(list[i], min, max);
            }

            Array.Sort(values);
            return values;
        }

        return [RangeMath.Clamp(raw.TryGetValue(out double number) ? number : min, min, max)];
    }

    // SLD-STA-09 (areValuesEqual): a number by `===`, a list by length and `===` per element; the kinds never match.
    internal static bool SameRaw(SliderValues candidate, SliderValues current)
    {
        if (candidate.TryGetValue(out double a))
        {
            return current.TryGetValue(out double b) && a == b;
        }

        if (!candidate.TryGetValue(out IReadOnlyList<double>? left) || !current.TryGetValue(out IReadOnlyList<double>? right))
        {
            return false;
        }

        if (ReferenceEquals(left, right))
        {
            return true;
        }

        if (left.Count != right.Count)
        {
            return false;
        }

        for (int i = 0; i < left.Count; i++)
        {
            if (left[i] != right[i])
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Formats one value as the value part and the thumbs show it.</summary>
    /// <param name="value">The value.</param>
    /// <param name="format">The root's format string, or <see langword="null"/>.</param>
    /// <param name="culture">The root's culture, or <see langword="null"/>.</param>
    /// <returns>The formatted value.</returns>
    internal static string FormatValue(double value, string? format, CultureInfo? culture) =>
        NumberFormatting.Format(value, string.IsNullOrEmpty(format) ? DefaultFormat : format, culture);

    private SliderStoreState BuildStoreState()
    {
        SliderValues raw = RawValue;
        string rootId = Id ?? string.Empty;
        return new SliderStoreState(
            Normalize(raw, Min, Max),
            0,
            raw.IsRange,
            Min,
            Max,
            Step,
            LargeStep,
            MinStepsBetweenValues,
            Orientation,
            DirectionProviderContext.Read(DirectionContext) == TextDirection.Rtl,
            field!.Disabled,
            Dragging: false,
            ActiveThumbIndex: -1,
            LastUsedThumbIndex: -1,
            field.Snapshot,
            rootId,
            ResolveLabelledBy(rootId),
            field.Name,
            Form,
            Format,
            Culture,
            ThumbAlignment,
            ThumbsVersion: 0,
            Attached: false);
    }

    // SLD-ARIA-07: the consumer's aria-labelledby, else the field's label, else the slider label once published.
    private string? ResolveLabelledBy(string rootId)
    {
        if (AdditionalAttributes is not null && AdditionalAttributes.TryGetValue("aria-labelledby", out object? supplied) && supplied is string consumer)
        {
            return consumer;
        }

        return field!.FieldLabelId ?? context?.Label.Id;
    }

    // Writes the store's record from the parameters, the field and the label; a new list only when the raw value, the bounds
    // or the range mode changed.
    private void Publish()
    {
        SliderStoreState current = context!.State;
        SliderValues raw = RawValue;
        IReadOnlyList<double> values = current.Values;
        int version = current.ValueVersion;
        bool range = raw.IsRange;
        if (range != current.Range || !Equality.Is(Min, current.Min) || !Equality.Is(Max, current.Max) || !SameNormalized(raw, current))
        {
            IReadOnlyList<double> next = Normalize(raw, Min, Max);
            if (!SameList(next, values))
            {
                values = next;
                version++;
            }
        }

        string rootId = Id ?? string.Empty;
        SliderStoreState next2 = current with
        {
            Values = values,
            ValueVersion = version,
            Range = range,
            Min = Min,
            Max = Max,
            Step = Step,
            LargeStep = LargeStep,
            MinStepsBetweenValues = MinStepsBetweenValues,
            Orientation = Orientation,
            Rtl = DirectionProviderContext.Read(DirectionContext) == TextDirection.Rtl,
            Disabled = field!.Disabled,
            Field = field.Snapshot,
            RootId = rootId,
            LabelledBy = ResolveLabelledBy(rootId),
            Name = field.Name,
            Form = Form,
            Format = Format,
            Culture = Culture,
            ThumbAlignment = ThumbAlignment,
        };

        // SLD-FOC-05: a slider that becomes disabled resets the active thumb (the module blurs the focused element).
        if (next2.Disabled && !current.Disabled)
        {
            next2 = next2 with { ActiveThumbIndex = -1 };
        }

        if (!SliderStoreState.ShallowEquals(next2, current))
        {
            context.Store.SetState(next2);
            RequestSync();
        }
    }

    // Whether the normalized list of the raw value is the one the store holds (a controlled re-render with an equal value).
    private static bool SameNormalized(SliderValues raw, SliderStoreState current)
    {
        IReadOnlyList<double> values = current.Values;
        if (raw.TryGetValue(out IReadOnlyList<double>? list))
        {
            if (list.Count != values.Count)
            {
                return false;
            }

            // Sorted input normalizes to itself element by element; anything else is recomputed.
            for (int i = 0; i < list.Count; i++)
            {
                if (!Equality.Is(RangeMath.Clamp(list[i], current.Min, current.Max), values[i]))
                {
                    return false;
                }
            }

            return true;
        }

        return values.Count == 1 && Equality.Is(RangeMath.Clamp(raw.TryGetValue(out double number) ? number : current.Min, current.Min, current.Max), values[0]);
    }

    private static bool SameList(IReadOnlyList<double> left, IReadOnlyList<double> right)
    {
        if (left.Count != right.Count)
        {
            return false;
        }

        for (int i = 0; i < left.Count; i++)
        {
            if (!Equality.Is(left[i], right[i]))
            {
                return false;
            }
        }

        return true;
    }

    // Returns whether the root's state record changed; a new record is made only then (ADR 16 rule 7).
    private bool UpdateState()
    {
        SliderStoreState s = context!.State;
        if (state is { } current
            && current.ActiveThumbIndex == s.ActiveThumbIndex
            && current.Disabled == s.Disabled
            && current.Dragging == s.Dragging
            && current.Orientation == s.Orientation
            && Equality.Is(current.Max, s.Max)
            && Equality.Is(current.Min, s.Min)
            && Equality.Is(current.MinStepsBetweenValues, s.MinStepsBetweenValues)
            && Equality.Is(current.Step, s.Step)
            && ReferenceEquals(current.Values, s.Values)
            && current.Touched == s.Field.Touched
            && current.Dirty == s.Field.Dirty
            && current.Valid == s.Field.Valid
            && current.Filled == s.Field.Filled
            && current.Focused == s.Field.Focused)
        {
            return false;
        }

        state = new SliderRootState(s.ActiveThumbIndex, s.Disabled, s.Dragging, s.Orientation, s.Max, s.Min, s.MinStepsBetweenValues, s.Step, s.Values, s.Field.Touched, s.Field.Dirty, s.Field.Valid, s.Field.Filled, s.Field.Focused);
        return true;
    }

    // The root renders when a projected field changed, or when a state function could read a changed field; rendering it
    // renders the whole subtree, so a value step alone never renders it without a state function.
    private void RenderIfChanged(SliderStoreState before)
    {
        SliderStoreState after = context!.State;
        bool projected = before.Disabled != after.Disabled
            || before.Dragging != after.Dragging
            || before.Orientation != after.Orientation
            || before.Field != after.Field
            || !string.Equals(before.LabelledBy, after.LabelledBy, StringComparison.Ordinal);
        bool stateFunctions = ClassValue is not null || StyleValue is not null || RenderFunction is not null;
        if (UpdateState() && (projected || stateFunctions))
        {
            StateHasChanged();
        }
        else if (projected)
        {
            StateHasChanged();
        }
    }

    private void SetStore(Func<SliderStoreState, SliderStoreState> change)
    {
        SliderStoreState before = context!.State;
        SliderStoreState after = change(before);
        if (SliderStoreState.ShallowEquals(after, before))
        {
            return;
        }

        context.Store.SetState(after);
        RenderIfChanged(before);
        RequestSync();
    }

    private void OnFieldChanged()
    {
        if (disposed)
        {
            return;
        }

        SliderStoreState before = context!.State;
        string rootId = Id ?? string.Empty;
        SetStore(s => s with { Field = field!.Snapshot, Disabled = field.Disabled, Name = field.Name, LabelledBy = ResolveLabelledBy(rootId) });
        if (before.Disabled != context.State.Disabled && context.State.Disabled)
        {
            SetStore(s => s with { ActiveThumbIndex = -1 });
        }
    }

    private void OnLabelChanged()
    {
        if (disposed)
        {
            return;
        }

        string rootId = Id ?? string.Empty;
        SetStore(s => s with { LabelledBy = ResolveLabelledBy(rootId) });
    }

    private void OnDirectionChanged()
    {
        if (!disposed)
        {
            SetStore(s => s with { Rtl = DirectionProviderContext.Read(DirectionContext) == TextDirection.Rtl });
        }
    }

    // SLD-API-17, SLD-DOM-17: each report re-indexes the thumbs; the value part's `for` follows.
    private void OnThumbsChanged()
    {
        if (!disposed)
        {
            SetStore(s => s with { ThumbsVersion = s.ThumbsVersion + 1 });
        }
    }

    private static bool SuppliesRangeTriple(in ParameterView parameters)
    {
        foreach (ParameterValue parameter in parameters)
        {
            if (parameter.Name is nameof(Values) or nameof(ValuesChanged) or nameof(DefaultValues) or nameof(OnValuesChange) or nameof(ValuesExpression) or nameof(OnValuesCommitted))
            {
                return true;
            }
        }

        return false;
    }

    // EVD-ALG-03 step 2: the change callback of the mode's triple; a cancel stops the request.
    private async Task<bool> NotifyAsync(SliderValues candidate, SliderChangeEventDetails details)
    {
        if (candidate.TryGetValue(out IReadOnlyList<double>? list))
        {
            await OnValuesChange.InvokeAsync(new ValueChangeEventArgs<IReadOnlyList<double>>(list, details));
        }
        else
        {
            candidate.TryGetValue(out double number);
            await OnValueChange.InvokeAsync(new ValueChangeEventArgs<double>(number, details));
        }

        return !details.IsCanceled;
    }

    // ValueChanged, then the commit when the root owns the value.
    private Task CommitAsync(SliderValues candidate)
    {
        if (candidate.TryGetValue(out IReadOnlyList<double>? list))
        {
            return valuesState!.CommitAsync(list, ValuesChanged);
        }

        candidate.TryGetValue(out double number);
        return valueState!.CommitAsync(number, ValueChanged);
    }

    private Task CommittedAsync(SliderValues value, GenericEventDetails details)
    {
        if (value.TryGetValue(out IReadOnlyList<double>? list))
        {
            return OnValuesCommitted.InvokeAsync(new ValueCommittedEventArgs<IReadOnlyList<double>>(list, details));
        }

        value.TryGetValue(out double number);
        return OnValueCommitted.InvokeAsync(new ValueCommittedEventArgs<double>(number, details));
    }

    // Requests run one at a time, in the order the module sent them, each after the previous one finished.
    private Task Enqueue(Func<Task> work)
    {
        requests = RunAfterAsync(requests, work);
        return requests;
    }

    private async Task RunAfterAsync(Task previous, Func<Task> work)
    {
        try
        {
            await previous;
        }
        catch (Exception)
        {
            // The previous request's exception was dispatched already; this one runs regardless.
        }

        if (disposed)
        {
            return;
        }

        try
        {
            await work();
        }
        catch (Exception exception)
        {
            await DispatchExceptionAsync(exception);
        }
    }

    // SLD-EVT-01..12, SLD-STA-09/10/11, SLD-FRM-05 (a), SLD-FRM-16; slider/root/SliderRoot.tsx:193-247.
    private async Task HandleValueRequestAsync(SliderValueRequest request)
    {
        revision = Math.Max(revision, request.Revision);
        EventReason reason = ParseReason(request.Reason);
        SliderStoreState current = context!.State;
        SliderValues raw = RawValue;
        SliderValues candidate = current.Range ? new SliderValues(CopyOf(request.Values)) : new SliderValues(request.Values.Count > 0 ? request.Values[0] : double.NaN);
        bool userInput = reason is EventReason.Keyboard or EventReason.InputChange;

        bool applied = false;
        bool dropped = (!current.Range && candidate.TryGetValue(out double number) && double.IsNaN(number)) || SameRaw(candidate, raw);
        if (!dropped)
        {
            SliderChangeEventDetails details = new(reason, request.ActiveThumbIndex, request.Event);
            if (await NotifyAsync(candidate, details))
            {
                await CommitAsync(candidate);
                lastChangeReason = reason;
                applied = true;
            }
        }

        // SLD-FRM-05 (a): after the request, applied or not.
        if (userInput)
        {
            field!.MarkTouched();
        }

        // A controlled value the consumer did not change, or a cancel, rolls the module back.
        Publish();
        RenderIfChanged(current);

        // SLD-FRM-02/04, SLD-STA-21: the field's reaction to the new value, which no render of the root runs on a step.
        AfterRenderField();
        SliderValues after = RawValue;
        bool shown = applied && SameRaw(candidate, after);
        if (!shown && !SameNormalizedList(request.Values, context.State.Values))
        {
            rollbackPending = true;
        }

        if (applied)
        {
            if (gestureActive)
            {
                gestureValue = candidate;
            }

            // SLD-EVT-05 (a), SLD-KEY-12: right after an applied keyboard or native-input change.
            if (userInput)
            {
                await CommittedAsync(candidate, new GenericEventDetails(reason, request.Event));
            }
        }

        // Issue 411: the answering update is sent, not awaited, so the next request does not wait a round trip on it.
        Flush();
    }

    private static bool SameNormalizedList(IReadOnlyList<double> sent, IReadOnlyList<double> values) => SameList(sent, values);

    private static double[] CopyOf(IReadOnlyList<double> values)
    {
        double[] copy = new double[values.Count];
        for (int i = 0; i < copy.Length; i++)
        {
            copy[i] = values[i];
        }

        return copy;
    }

    // SLD-STA-08, SLD-PTR-08/11/14/17, SLD-EVT-05 (b), SLD-EVT-12, SLD-PTR-19.
    private async Task HandleGestureAsync(SliderGestureReport report)
    {
        if (report.Active)
        {
            if (!gestureActive)
            {
                gestureActive = true;
                gestureValue = null;
            }

            SetStore(s => s with { Dragging = report.Dragging });
            return;
        }

        bool wasActive = gestureActive;
        gestureActive = false;
        SliderValues? committed = gestureValue;
        gestureValue = null;
        SetStore(s => s with { Dragging = false, ActiveThumbIndex = report.Commit ? -1 : s.ActiveThumbIndex });
        if (!wasActive || !report.Commit || committed is not { } value)
        {
            return;
        }

        // A cached list whose length no longer matches the thumbs is not committed (SLD-PTR-19).
        if (value.TryGetValue(out IReadOnlyList<double>? list) && list.Count != context!.State.Values.Count)
        {
            return;
        }

        await CommittedAsync(value, new GenericEventDetails(lastChangeReason, report.Event));
    }

    // SLD-STA-06/07, SLD-FOC-03/04, SLD-FRM-05 (b)/06, SLD-API-22 (the consumer's focus and blur handlers run first).
    private async Task HandleFocusAsync(SliderFocusReport report)
    {
        SliderThumbEntry? thumb = context!.FindThumb(report.Index);
        if (thumb is not null)
        {
            AttributeValue handler = report.Focused ? thumb.Focus : thumb.Blur;
            if (handler.IsHandler)
            {
                await handler.InvokeAsync(new FocusEventArgs { Type = report.Focused ? "focus" : "blur" });
            }
        }

        bool thumbDisabled = thumb is not null && (thumb.OwnDisabled || context.State.Disabled);
        if (report.Focused)
        {
            SetStore(s => s with { ActiveThumbIndex = report.Index, LastUsedThumbIndex = report.Index >= 0 ? report.Index : s.LastUsedThumbIndex });
            if (thumb is not null)
            {
                field!.SetFocused(thumb.Owner, true, thumbDisabled);
            }

            return;
        }

        SetStore(s => s with { ActiveThumbIndex = -1 });
        if (!report.Outside || thumb is null)
        {
            return;
        }

        // SLD-FRM-06: the current normalized value in the raw value's shape.
        await field!.HandleBlurOutsideAsync(thumb.Owner, thumbDisabled, BoxedFieldValue());
    }

    // SLD-API-22 on Server: the consumer's C# onkeydown runs a round trip after the module handled the key.
    private async Task HandleKeyAsync(SliderKeyReport report)
    {
        SliderThumbEntry? thumb = context!.FindThumb(report.Index);
        if (thumb is { KeyDown.IsHandler: true })
        {
            await thumb.KeyDown.InvokeAsync(report.Event.ToKeyboardEventArgs());
        }
    }

    // ADR 04 §3: the thumb keydown, on WebAssembly only, inside the dispatch.
    private DecisionPolicy DecideKey(EventSnapshot snapshot)
    {
        SliderThumbEntry? thumb = context!.FindThumbByInput(snapshot.Target?.Id);
        if (thumb is not { KeyDown.IsHandler: true })
        {
            return DecisionPolicy.Run;
        }

        KeyboardEventArgs args = snapshot.ToKeyboardEventArgs();
        RunSynchronousPart(thumb.KeyDown.InvokeAsync(args));
        return args.PartHandlerPrevented() ? DecisionPolicy.Stop : DecisionPolicy.Run;
    }

    private DecisionPolicy DecidePress(EventSnapshot snapshot) => context!.PressDecider is { } decider ? decider(snapshot) : DecisionPolicy.Run;

    private void RunSynchronousPart(Task task)
    {
        // Only the synchronous part of a handler can veto; an asynchronous rest runs on (ADR 04 §3).
        if (!task.IsCompleted)
        {
            _ = ObserveAsync(task);
        }
        else if (task.IsFaulted)
        {
            _ = DispatchExceptionAsync(task.Exception!.GetBaseException());
        }
    }

    private async Task ObserveAsync(Task task)
    {
        try
        {
            await task;
        }
        catch (Exception exception) when (!task.IsCanceled)
        {
            await DispatchExceptionAsync(exception);
        }
    }

    private static EventReason ParseReason(string literal) => literal switch
    {
        "input-change" => EventReason.InputChange,
        "keyboard" => EventReason.Keyboard,
        "track-press" => EventReason.TrackPress,
        "drag" => EventReason.Drag,
        _ => EventReason.InputChange,
    };

    // SLD-STA-12: the normalized list in range mode, the first value in single mode; boxed once per value (the RadioGroup
    // precedent, ADR 05's note of 2026-10-22).
    private object? BoxedFieldValue()
    {
        SliderStoreState s = context!.State;
        if (boxedVersion == s.ValueVersion && boxedFieldValue is not null)
        {
            return boxedFieldValue;
        }

        boxedVersion = s.ValueVersion;

        // IOD2005: the field's registration and validator take the value as an object; boxed once per committed value.
#pragma warning disable IOD2005
        boxedFieldValue = s.Range ? s.Values : s.Values.Count > 0 ? s.Values[0] : Min;
#pragma warning restore IOD2005
        return boxedFieldValue;
    }

    // SLD-STA-21: arrays by element, otherwise `!==`.
    private bool IsDirty(object? initial)
    {
        SliderStoreState s = context!.State;
        if (s.Range)
        {
            return initial is not IReadOnlyList<double> initialList || !SameList(s.Values, initialList);
        }

        return initial is not double initialNumber || !Equality.StrictEquals(s.Values.Count > 0 ? s.Values[0] : Min, initialNumber);
    }

    private void AfterRenderField()
    {
        SliderStoreState s = context!.State;
        IReadOnlyList<SliderThumbEntry> thumbs = context.Thumbs;
        string? controlId = thumbs.Count > 0 ? thumbs[^1].InputId : null;
        field!.AfterRender(s.RootId, controlId, BoxedFieldValue(), s.ValueVersion, IsDirty);
    }

    // Parts ask for a send after the batch that changed them; one post per batch.
    private void RequestSync()
    {
        if (syncPosted || disposed || (handle is null && !attaching))
        {
            return;
        }

        syncPosted = true;
        _ = InvokeAsync(async () =>
        {
            await Task.Yield();
            syncPosted = false;
            if (!disposed)
            {
                AfterRenderField();
                Flush();
            }
        });
    }

    private async Task SyncTwinAsync()
    {
        if (!Enabled)
        {
            if (handle is { } released)
            {
                handle = null;
                sentOptions = null;
                await released.DisposeAsync();
            }

            return;
        }

        if (handle is not null || attaching)
        {
            Flush();
            return;
        }

        string? elementId = Element is null ? Id : null;
        if (Element is null && elementId is null)
        {
            return;
        }

        if (twin is null)
        {
            twin = new SliderTwin(JSRuntime) { KeyDecider = DecideKey, PressDecider = DecidePress };
            twin.ValueRequested += onValueRequest;
            twin.GestureChanged += onGesture;
            twin.FocusChanged += onFocus;
            twin.KeyReported += onKey;
        }

        // The handover (ADR 04 Amendments 2026-10-18): the parts keep the style and the input value they last rendered from
        // the moment the attach is sent.
        SetStore(s => s with { Attached = true });
        (SliderOptions options, TwinReferences references) = BuildOptions();
        sentOptions = options;
        attaching = true;
        try
        {
            handle = Element is { } element ? await twin.AttachAsync(element, options, references) : await twin.AttachAsync(elementId!, options, references);
        }
        finally
        {
            attaching = false;
        }

        attachedSource.TrySetResult();

        // Whatever changed while the attach was in flight.
        Flush();
    }

    // Issue 411 (M6 track 10; orchestrator 5's grant, 2026-11-27): the update is sent, not awaited. Requests still run one at
    // a time in the module's order, and the updates reach the module in the order sent (the handle invokes at once, and the
    // circuit delivers JavaScript calls in order); what no longer waits is the next request, a round trip on Server. The
    // field behaviors' Observe pattern: a fault is dispatched, and its options count as unsent (PR 398's lesson).
    private void Flush()
    {
        if (handle is null || disposed)
        {
            return;
        }

        (SliderOptions options, TwinReferences references) = BuildOptions();
        if (options == sentOptions && !options.Rollback)
        {
            return;
        }

        sentOptions = options;
        rollbackPending = false;
        Task sending = handle.UpdateAsync(options, references).AsTask();
        if (!sending.IsCompletedSuccessfully)
        {
            _ = ObserveUpdateAsync(sending, options);
        }
    }

    // A disconnect, or a dispose that overtook the update, ends quietly in the handle.
    private async Task ObserveUpdateAsync(Task sending, SliderOptions options)
    {
        try
        {
            await sending;
        }
        catch (Exception exception) when (!sending.IsCanceled)
        {
            // The module never applied them: the next send carries them again, a rollback included.
            if (ReferenceEquals(sentOptions, options))
            {
                sentOptions = null;
                rollbackPending |= options.Rollback;
            }

            if (!disposed)
            {
                await DispatchExceptionAsync(exception);
            }
        }
    }

    private (SliderOptions Options, TwinReferences References) BuildOptions()
    {
        SliderStoreState s = context!.State;
        TwinReferences references = new();
        int? controlRef = context.ControlElement is { } control ? references.Add(control) : null;
        IReadOnlyList<SliderIndicatorEntry> indicatorEntries = context.Indicators;
        SliderIndicatorOptions[] indicators = new SliderIndicatorOptions[CountIndicators(indicatorEntries)];
        int next = 0;
        for (int i = 0; i < indicatorEntries.Count; i++)
        {
            if (indicatorEntries[i].Element is { } element)
            {
                indicators[next++] = new SliderIndicatorOptions { Ref = references.Add(element), Style = indicatorEntries[i].ConsumerStyle };
            }
        }

        return (new SliderOptions
        {
            Values = s.Values,
            Range = s.Range,
            Min = s.Min,
            Max = s.Max,
            Step = s.Step,
            LargeStep = s.LargeStep,
            MinStepsBetweenValues = s.MinStepsBetweenValues,
            Vertical = s.Orientation == Orientation.Vertical,
            Rtl = s.Rtl,
            Disabled = s.Disabled,
            Inset = s.Inset,
            Collision = ThumbCollisionBehavior switch
            {
                SliderThumbCollisionBehavior.Swap => "swap",
                SliderThumbCollisionBehavior.None => "none",
                _ => "push",
            },
            Thumbs = BuildThumbs(s),
            ControlRef = controlRef,
            Indicators = indicators,
            Revision = revision,
            Rollback = rollbackPending,
            RawDiffers = !SameRaw(RawValue, s.Range ? new SliderValues(s.Values) : new SliderValues(s.Values.Count > 0 ? s.Values[0] : Min)),
            KeyHandlers = AnyKeyHandler(),
            PressHandler = context.ControlPressHandler,
        }, references);
    }

    private static int CountIndicators(IReadOnlyList<SliderIndicatorEntry> entries)
    {
        int count = 0;
        for (int i = 0; i < entries.Count; i++)
        {
            if (entries[i].Element is not null)
            {
                count++;
            }
        }

        return count;
    }

    private SliderThumbOptions[] BuildThumbs(SliderStoreState s)
    {
        IReadOnlyList<SliderThumbEntry> entries = context!.Thumbs;
        int highest = -1;
        for (int i = 0; i < entries.Count; i++)
        {
            highest = Math.Max(highest, entries[i].Index);
        }

        SliderThumbOptions[] thumbs = new SliderThumbOptions[highest + 1];
        for (int index = 0; index < thumbs.Length; index++)
        {
            SliderThumbEntry? entry = context.FindThumb(index);
            thumbs[index] = entry is null
                ? new SliderThumbOptions()
                : new SliderThumbOptions { InputId = entry.InputId, Disabled = entry.OwnDisabled || s.Disabled, Style = entry.ConsumerStyle };
        }

        return thumbs;
    }

    private bool AnyKeyHandler()
    {
        IReadOnlyList<SliderThumbEntry> entries = context!.Thumbs;
        for (int i = 0; i < entries.Count; i++)
        {
            if (entries[i].KeyDown.IsHandler)
            {
                return true;
            }
        }

        return false;
    }

    private void DetachTwinEvents(SliderTwin attached)
    {
        attached.ValueRequested -= onValueRequest;
        attached.GestureChanged -= onGesture;
        attached.FocusChanged -= onFocus;
        attached.KeyReported -= onKey;
        attached.KeyDecider = null;
        attached.PressDecider = null;
    }
}

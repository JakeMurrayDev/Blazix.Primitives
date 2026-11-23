using Iodine.Headless.Interop;
using Iodine.Headless.Rendering;
using Iodine.Headless.Store;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace Iodine.Headless;

// SLD §3.2: the Root's state object (slider/label/SliderLabel.tsx:62).
/// <summary>Represents the state of a slider label, which the part exposes as data attributes and passes to its class and style functions.</summary>
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
public sealed partial record SliderLabelState(
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

// Base UI's Slider.Label (slider/label/SliderLabel.tsx); the contract is docs/components/slider.md §3.2, §5.8.
// - A `div` whose id is always `<root id>-label`; a consumer id is discarded (SLD-API-27, SLD-DOM-19). It publishes the id
//   after its render to the Root (SLD-ARIA-06/07) and to the enclosing field's labelable scope, and withdraws it only if it
//   is still the current one (FFI-ARIA-08); in server output nothing references it (SLD-ARIA-12).
// - Click-to-focus through the `form` module's label role, non-native (FFI-PTR-01/05; SLD-FOC-06): the field's control
//   id, else the lone thumb's input id when exactly one thumb is registered, else nothing (design point 4 (a) of track 4);
//   its pointerdown is default-prevented by the module. The consumer's click, mousedown and pointerdown run inside the
//   `label` decision on WebAssembly (the FieldLabel precedent).
// - Several labels under one Root share the id (SLD §16 Q11, replicated).
/// <summary>
/// Represents the visible label of a slider: Base UI's <c>Slider.Label</c>, rendered as a <c>div</c> whose id labels the
/// slider and its thumbs. Clicking it focuses the thumb of a single-thumb slider.
/// </summary>
public sealed class SliderLabel : PartBase<SliderLabelState>
{
    private const string DefaultTag = "div";
    private const string MouseDownEvent = "onmousedown";
    private const string ClickEvent = "onclick";
    private const string PointerDownEvent = "onpointerdown";

    private static readonly StateAttributesMapping<SliderLabelState> s_mapping = FieldValidityMapping.Create<SliderLabelState>();

    private readonly FieldConsumerHandlers consumerHandlers = new(MouseDownEvent, ClickEvent, PointerDownEvent);
    private readonly EventCallback onMouseDown;
    private readonly EventCallback onClick;
    private readonly EventCallback onPointerDown;
    private readonly TaskCompletionSource attachedSource = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private SliderRootContext? context;
    private SliderLabelState? state;
    private SliderStateKey stateKey;
    private string? publishedId;
    private LabelableScope? publishedScope;
    private FormTwin? twin;
    private TwinHandle<FormOptions>? handle;
    private Task? attaching;
    private FormOptions? sentOptions;
    private bool decidedPending;
    private bool disposed;

    /// <summary>Initializes a new instance of the <see cref="SliderLabel"/> class.</summary>
    public SliderLabel()
    {
        onMouseDown = CreateHandler<MouseEventArgs>(MouseDownEvent, e => RunConsumerAsync(MouseDownEvent, e));
        onClick = CreateHandler<MouseEventArgs>(ClickEvent, e => RunConsumerAsync(ClickEvent, e));
        onPointerDown = CreateHandler<PointerEventArgs>(PointerDownEvent, e => RunConsumerAsync(PointerDownEvent, e));
    }

    [Inject]
    private IJSRuntime JSRuntime { get; set; } = null!;

    private bool HasStateFunctions => ClassValue is not null || StyleValue is not null || RenderFunction is not null;

    private string LabelId => context!.State.RootId + "-label";

    [CascadingParameter]
    private SliderRootContext? RootContext { get; set; }

    [CascadingParameter]
    private FieldRootContext? FieldContext { get; set; }

    /// <inheritdoc />
    protected override string DefaultTagName => DefaultTag;

    /// <inheritdoc />
    protected override SliderLabelState State
    {
        get
        {
            UpdateState();
            return state!;
        }
    }

    /// <inheritdoc />
    protected override StateAttributesMapping<SliderLabelState>? StateAttributesMapping => s_mapping;

    // For the fixtures: completes once the form module is attached, so a browser test can wait for its listeners.
    /// <summary>Gets a task that completes when the label's browser-side behavior is attached to its element.</summary>
    /// <value>A task that completes after the first interactive render attached the behavior.</value>
    internal Task TwinAttached => attachedSource.Task;

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">The label is not placed within a <see cref="SliderRoot"/>.</exception>
    protected override void OnInitialized()
    {
        context = SliderRootContext.Require(RootContext);
        base.OnInitialized();
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        consumerHandlers.Set(AdditionalAttributes);
    }

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (disposed)
        {
            return;
        }

        // SLD-DOM-19 (post-commit): the id is published once the label is in the document, and follows the root's id.
        Publish(Enabled ? LabelId : null);
        if (!Enabled)
        {
            if (handle is { } released)
            {
                handle = null;
                sentOptions = null;
                await released.DisposeAsync().ConfigureAwait(true);
            }

            return;
        }

        if (handle is null)
        {
            twin ??= new FormTwin(JSRuntime) { LabelDecider = Decide };
            Task pending = attaching ??= AttachAsync(twin);
            await pending.ConfigureAwait(true);
            if (ReferenceEquals(attaching, pending))
            {
                attaching = null;
            }
        }

        if (handle is not null && !disposed && Options() is var options && options != sentOptions)
        {
            sentOptions = options;
            await handle.UpdateAsync(options).ConfigureAwait(true);
        }
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing && !disposed)
        {
            disposed = true;
            Publish(null);
            if (twin is not null)
            {
                twin.LabelDecider = null;
                _ = twin.DisposeAsync().AsTask();
                twin = null;
                handle = null;
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
        subscription.Select(s => (s.RootId, s.ThumbsVersion, s.Range));
    }

    /// <inheritdoc />
    protected override void AddPartAttributes(AttributeList attributes)
    {
        if (consumerHandlers.Has(MouseDownEvent))
        {
            attributes.Set(MouseDownEvent, onMouseDown);
        }

        if (consumerHandlers.Has(ClickEvent))
        {
            attributes.Set(ClickEvent, onClick);
        }

        if (consumerHandlers.Has(PointerDownEvent))
        {
            attributes.Set(PointerDownEvent, onPointerDown);
        }
    }

    // SLD-API-27: the id is the part's, whatever the consumer passed.
    /// <inheritdoc />
    protected override void FinishAttributes(AttributeList attributes)
    {
        attributes.Set("id", LabelId);
    }

    /// <inheritdoc />
    protected override bool ForwardsConsumerHandler(string eventName) => !consumerHandlers.Declines(eventName);

    private void Publish(string? id)
    {
        LabelableScope? scope = FieldContext?.Labelable;
        if (string.Equals(id, publishedId, StringComparison.Ordinal) && ReferenceEquals(scope, publishedScope))
        {
            return;
        }

        if (publishedId is not null)
        {
            context!.Label.Withdraw(publishedId);
            publishedScope?.Label.Withdraw(publishedId);
        }

        publishedId = id;
        publishedScope = id is null ? null : scope;
        if (id is not null)
        {
            context!.Label.Publish(id);
            scope?.Label.Publish(id);
        }
    }

    // Design point 4 (a) of track 4: the field's control, else the lone thumb's input.
    private string? FocusTarget()
    {
        if (FieldContext is { } field && field.Labelable.ControlId is { } controlId && !context!.State.Range)
        {
            return controlId;
        }

        IReadOnlyList<SliderThumbEntry> thumbs = context!.Thumbs;
        return thumbs.Count == 1 ? thumbs[0].InputId : null;
    }

    private FormOptions Options() => new()
    {
        Label = new FormLabelOptions
        {
            Native = false,
            ControlId = FocusTarget(),
            Decides = consumerHandlers.Any,
        },
    };

    private async Task AttachAsync(FormTwin attachingTwin)
    {
        FormOptions options = Options();
        sentOptions = options;
        if (Element is { } element)
        {
            handle = await attachingTwin.AttachAsync(element, options).ConfigureAwait(true);
        }
        else
        {
            try
            {
                handle = await attachingTwin.AttachAsync(LabelId, options).ConfigureAwait(true);
            }
            catch (JSException)
            {
                // A substitute that renders no element leaves nothing to attach to.
                sentOptions = null;
                return;
            }
        }

        attachedSource.TrySetResult();
    }

    // The label decision point, on WebAssembly only: the consumer's handler runs inside the dispatch.
    private DecisionPolicy Decide(EventSnapshot snapshot)
    {
        string eventName = "on" + snapshot.Type;
        if (!consumerHandlers.Has(eventName))
        {
            return DecisionPolicy.Run;
        }

        decidedPending = true;
        MouseEventArgs args = FieldConsumerHandlers.ToMouseEventArgs(snapshot);
        return consumerHandlers.RunSynchronously(eventName, args, DispatchExceptionAsync) ? DecisionPolicy.Stop : DecisionPolicy.Run;
    }

    private Task RunConsumerAsync(string eventName, MouseEventArgs e)
    {
        if (decidedPending)
        {
            decidedPending = false;
            return Task.CompletedTask;
        }

        return consumerHandlers.RunAsync(eventName, e);
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
        state = new SliderLabelState(key.ActiveThumbIndex, key.Disabled, key.Dragging, key.Orientation, key.Max, key.Min, key.MinStepsBetweenValues, key.Step, key.Values, key.Field.Touched, key.Field.Dirty, key.Field.Valid, key.Field.Filled, key.Field.Focused);
        return true;
    }
}

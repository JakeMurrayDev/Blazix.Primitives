using Iodine.Headless.Interop;
using Iodine.Headless.Rendering;
using Iodine.Headless.Store;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace Iodine.Headless;

// SLD §3.4: the Root's state object (slider/control/SliderControl.tsx:569).
/// <summary>Represents the state of a slider control, which the part exposes as data attributes and passes to its class and style functions.</summary>
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
public sealed partial record SliderControlState(
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

// SLD §3.4; slider/control/SliderControlDataAttributes.ts, in its order.
/// <summary>Provides the names of the data attributes a slider control renders.</summary>
public static class SliderControlDataAttributes
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

// Base UI's Slider.Control (slider/control/SliderControl.tsx); the contract is docs/components/slider.md §3.4, §5.2, §6.2.
// - The pointer surface: the Root's `slider` handle listens on this element (design point 1 (a')), which crosses as a
//   reference. No tabindex, no role (SLD-DOM-03); the marker attribute is withdrawn with the pre-hydration script (ADR 12
//   Amendments 2026-11-18 item 6).
// - The consumer's onpointerdown runs before the part (SLD-API-28, SLD-PTR-16): it is declined on the element and run by the
//   `press` decision on WebAssembly, whose PreventPartHandler() stops the gesture, and by the part's own handler otherwise,
//   a round trip after the gesture started on Server (ADR 01's relaxation table, fallback start). A JavaScript listener's
//   preventDefault() is honored on both modes, where the module checks it.
/// <summary>
/// Represents the interactive surface of a slider: Base UI's <c>Slider.Control</c>, rendered as a <c>div</c>. Pressing it
/// moves the nearest thumb to the press point, and dragging moves the pressed thumb.
/// </summary>
/// <remarks>Without a control the slider is operated by the keyboard only.</remarks>
public sealed class SliderControl : PartBase<SliderControlState>
{
    private const string DefaultTag = "div";
    private const string PointerDown = "onpointerdown";

    private static readonly StateAttributesMapping<SliderControlState> s_mapping = FieldValidityMapping.Create<SliderControlState>();

    private readonly EventCallback onPointerDown;
    private SliderRootContext? context;
    private SliderControlState? state;
    private SliderStateKey stateKey;
    private AttributeValue consumerPointerDown;
    private bool decidedPending;
    private ElementReference? publishedElement;
    private bool disposed;

    /// <summary>Initializes a new instance of the <see cref="SliderControl"/> class.</summary>
    public SliderControl()
    {
        onPointerDown = CreateHandler<PointerEventArgs>(PointerDown, OnPointerDownAsync);
    }

    private bool HasStateFunctions => ClassValue is not null || StyleValue is not null || RenderFunction is not null;

    [CascadingParameter]
    private SliderRootContext? RootContext { get; set; }

    /// <inheritdoc />
    protected override string DefaultTagName => DefaultTag;

    /// <inheritdoc />
    protected override SliderControlState State
    {
        get
        {
            UpdateState();
            return state!;
        }
    }

    /// <inheritdoc />
    protected override StateAttributesMapping<SliderControlState>? StateAttributesMapping => s_mapping;

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">The control is not placed within a <see cref="SliderRoot"/>.</exception>
    protected override void OnInitialized()
    {
        context = SliderRootContext.Require(RootContext);
        base.OnInitialized();
        context.PressDecider = Decide;
        context.Control = this;
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        consumerPointerDown = default;
        if (AdditionalAttributes is not null)
        {
            foreach (KeyValuePair<string, object> pair in AdditionalAttributes)
            {
                if (string.Equals(pair.Key, PointerDown, StringComparison.OrdinalIgnoreCase))
                {
                    AttributeValue value = AttributeValue.FromObject(pair.Value);
                    consumerPointerDown = MergeProps.IsHandler(pair.Key, value) ? value : default;
                }
            }
        }

        if (context!.ControlPressHandler != consumerPointerDown.IsHandler)
        {
            context.ControlPressHandler = consumerPointerDown.IsHandler;
            context.RequestSync();
        }

        UpdateState();
    }

    /// <inheritdoc />
    protected override void OnAfterRender(bool firstRender)
    {
        // SLD-EDGE-08: a substitute that leaves no element leaves the slider without a pointer surface.
        if (!disposed && !Nullable.Equals(publishedElement, Element))
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
            if (context is not null)
            {
                if (context.PressDecider == Decide)
                {
                    context.PressDecider = null;
                }

                if (ReferenceEquals(context.Control, this))
                {
                    context.Control = null;
                }

                context.ControlPressHandler = false;
                context.RequestSync();
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
    }

    /// <inheritdoc />
    protected override bool ForwardsConsumerHandler(string eventName) => !string.Equals(eventName, PointerDown, StringComparison.Ordinal);

    /// <inheritdoc />
    protected override void AddPartAttributes(AttributeList attributes)
    {
        if (consumerPointerDown.IsHandler)
        {
            attributes.Set(PointerDown, onPointerDown);
        }
    }

    // ADR 04 §3: the `press` decision, on WebAssembly only, inside the dispatch.
    private DecisionPolicy Decide(EventSnapshot snapshot)
    {
        if (!consumerPointerDown.IsHandler)
        {
            return DecisionPolicy.Run;
        }

        PointerEventArgs args = new()
        {
            Type = snapshot.Type,
            PointerType = snapshot.PointerType ?? string.Empty,
            Buttons = snapshot.Buttons,
            Detail = snapshot.Detail,
            AltKey = snapshot.AltKey,
            CtrlKey = snapshot.CtrlKey,
            MetaKey = snapshot.MetaKey,
            ShiftKey = snapshot.ShiftKey,
        };
        decidedPending = true;
        Task task = consumerPointerDown.InvokeAsync(args);
        if (!task.IsCompleted)
        {
            _ = ObserveAsync(task);
        }
        else if (task.IsFaulted)
        {
            _ = DispatchExceptionAsync(task.Exception!.GetBaseException());
        }

        return args.PartHandlerPrevented() ? DecisionPolicy.Stop : DecisionPolicy.Run;
    }

    // The part's own onpointerdown: the consumer's handler, unless the decision already ran it inside this dispatch.
    private Task OnPointerDownAsync(PointerEventArgs args)
    {
        if (decidedPending)
        {
            decidedPending = false;
            return Task.CompletedTask;
        }

        return consumerPointerDown.IsHandler ? consumerPointerDown.InvokeAsync(args) : Task.CompletedTask;
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

    private bool UpdateState()
    {
        SliderStateKey key = SliderStateKey.From(context!.State);
        if (state is not null && key == stateKey)
        {
            return false;
        }

        stateKey = key;
        state = new SliderControlState(key.ActiveThumbIndex, key.Disabled, key.Dragging, key.Orientation, key.Max, key.Min, key.MinStepsBetweenValues, key.Step, key.Values, key.Field.Touched, key.Field.Dirty, key.Field.Valid, key.Field.Filled, key.Field.Focused);
        return true;
    }

}

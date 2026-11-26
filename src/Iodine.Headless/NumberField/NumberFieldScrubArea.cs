using Iodine.Headless.Interop;
using Iodine.Headless.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace Iodine.Headless;

// Base UI's NumberField.ScrubArea (number-field/scrub-area/NumberFieldScrubArea.tsx); the contract is
// docs/components/number-field.md §3.2 and §5. A `span` with `role="presentation"` and the inline `touch-action: none;
// -webkit-user-select: none; user-select: none` under the consumer's style (NUM-DOM-12), the root's state attributes.
// - The `number-field` module's scrub role (6b; ADR 04 Amendments 2026-11-18) runs the pointerdown, the pointer lock, the
//   window move accumulator and the release in the browser; this part attaches its handle with the visible input as the
//   reference (the mouse press focuses it, NUM-FOC-04) and turns the reports into the root's requests: the start sets its own
//   and the root's scrubbing state (NUM-STA-15), a step steps the value (NUM-PTR-11), the end commits (NUM-PTR-13). An
//   unmount mid-scrub clears the root's scrubbing state without a commit (NUM-PTR-17).
// - Each scrub area keeps its own scrubbing state and cursor (NUM-STR-05), cascaded to its ScrubAreaCursor.
// - The consumer's `onpointerdown` is declined and run by the part: on WebAssembly inside the `scrub-start` decision, where
//   PreventPartHandler() stops the scrub; on Server from the part's own handler, after the module started it (design
//   point 1 (a); ADR 01's C#-origin veto row).
/// <summary>
/// Represents an area the user can press and drag to change a number field's value: Base UI's
/// <c>NumberField.ScrubArea</c>, rendered as a <c>span</c>. It must be placed within a <see cref="NumberFieldRoot"/>.
/// </summary>
public sealed class NumberFieldScrubArea : PartBase<NumberFieldScrubAreaState>
{
    private const string DefaultTag = "span";

    // NumberFieldScrubArea.tsx:21-25, in React's serialization order.
    private const string PartStyle = "touch-action: none; -webkit-user-select: none; user-select: none";

    private static readonly StateAttributesMapping<NumberFieldScrubAreaState> s_mapping = FieldValidityMapping.Create<NumberFieldScrubAreaState>();

    private readonly NumberFieldScrubAreaContext scrubContext = new();
    private readonly TaskCompletionSource attached = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly Action stateChanged;
    private readonly Action inputPublished;
    private readonly RenderFragment cascadedContent;
    private readonly Action<NumberFieldScrubStartReport> started;
    private readonly Action<NumberFieldScrubLockReport> lockSettled;
    private readonly Action<NumberFieldScrubStepReport> stepped;
    private readonly Action<NumberFieldScrubEndReport> ended;
    private NumberFieldRootContext? context;
    private NumberFieldScrubTwin? twin;
    private TwinHandle<NumberFieldScrubOptions>? handle;
    private Task? attaching;
    private NumberFieldScrubOptions? sentOptions;
    private ElementReference? sentInput;
    private RenderFragment? consumerContent;
    private AttributeValue consumerPointerDown;
    private bool pointerDownDecided;

    // Created the first time a consumer passes the handler: each handler slot costs every render (ADR 16 rule 7).
    private EventCallback onPointerDown;
    private NumberFieldScrubAreaState? state;
    private int stateVersion = -1;

    /// <summary>Initializes a new instance of the <see cref="NumberFieldScrubArea"/> class.</summary>
    public NumberFieldScrubArea()
    {
        stateChanged = StateHasChanged;
        inputPublished = OnInputPublished;
        cascadedContent = builder => scrubContext.RenderCascade(builder, consumerContent);
        started = report => _ = InvokeAsync(() => OnStarted(report));
        lockSettled = report => _ = InvokeAsync(() => scrubContext.SetPointerLockDenied(report.Denied));
        stepped = report => _ = InvokeAsync(() => context!.Root.HandleScrubStepAsync(report.Delta, report.Revision, report.Event));
        ended = report => _ = InvokeAsync(() => OnEndedAsync(report));
    }

    [Inject]
    private IJSRuntime JSRuntime { get; set; } = null!;

    [CascadingParameter]
    private NumberFieldRootContext? RootContext { get; set; }

    // NUM-API-24, NUM-PTR-11
    /// <summary>Gets or sets the axis of the pointer movement that changes the value.</summary>
    /// <value><see cref="Orientation.Horizontal"/> by default; with <see cref="Orientation.Vertical"/>, upward movement increases the value.</value>
    [Parameter]
    public Orientation Direction { get; set; } = Orientation.Horizontal;

    // NUM-API-24, NUM-PTR-11
    /// <summary>Gets or sets how many pixels the pointer must move before the value changes.</summary>
    /// <value>2 by default; a higher value makes scrubbing less sensitive.</value>
    [Parameter]
    public double PixelSensitivity { get; set; } = 2;

    // NUM-API-24, NUM-PTR-16
    /// <summary>Gets or sets the distance the cursor may move from the scrub area's edges before it wraps around.</summary>
    /// <value>
    /// The distance in pixels, which pads the scrub area by half of it on every side; <see langword="null"/>, the default,
    /// wraps at the edges of the visual viewport.
    /// </value>
    [Parameter]
    public double? TeleportDistance { get; set; }

    /// <inheritdoc />
    protected override string DefaultTagName => DefaultTag;

    /// <inheritdoc />
    protected override NumberFieldScrubAreaState State
    {
        get
        {
            // NUM-STA-02: the root's state, rebuilt only when the root made a new one.
            if (state is null || stateVersion != context!.StateVersion)
            {
                NumberFieldRootState root = context!.State;
                stateVersion = context.StateVersion;
                state = new NumberFieldScrubAreaState(root.Value, root.InputValue, root.Required, root.Disabled, root.ReadOnly, root.Scrubbing, root.Touched, root.Dirty, root.Valid, root.Filled, root.Focused);
            }

            return state;
        }
    }

    /// <inheritdoc />
    protected override StateAttributesMapping<NumberFieldScrubAreaState>? StateAttributesMapping => s_mapping;

    /// <summary>Gets a task that completes once the <c>number-field</c> module is attached (the browser fixtures' marker).</summary>
    internal Task TwinAttached => attached.Task;

    /// <inheritdoc />
    public override Task SetParametersAsync(ParameterView parameters)
    {
        // The part replaces ChildContent with its own cascade, so a parameter set without content must not keep it.
        ChildContent = null;
        return base.SetParametersAsync(parameters);
    }

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">The scrub area is not placed within a <see cref="NumberFieldRoot"/>.</exception>
    protected override void OnInitialized()
    {
        context = NumberFieldRootContext.Require(RootContext);
        context.AddListener(stateChanged);
        context.AddInputListener(inputPublished);
        base.OnInitialized();
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        consumerContent = ChildContent;
        ChildContent = cascadedContent;
        consumerPointerDown = default;
        if (AdditionalAttributes is not null
            && AdditionalAttributes.TryGetValue("onpointerdown", out object? supplied)
            && AttributeValue.FromObject(supplied) is var value
            && MergeProps.IsHandler("onpointerdown", value))
        {
            consumerPointerDown = value;
            if (!onPointerDown.HasDelegate)
            {
                onPointerDown = CreateHandler<PointerEventArgs>("onpointerdown", RunConsumerAsync);
            }
        }
    }

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!Enabled || Element is not { } element)
        {
            return;
        }

        scrubContext.Element = element;
        if (attaching is null)
        {
            attaching = AttachAsync(element);
            await attaching;
            return;
        }

        // Issue 370: one attach however many renders come before it completes; a render meanwhile, such as the one the
        // input's publication asks for, sends what changed once the attach has.
        await attaching;
        if (twin is null || handle is null)
        {
            return;
        }

        NumberFieldScrubOptions? options = ChangedOptions();
        ElementReference? input = context!.Root.InputElement;
        if (options is null && NumberFieldRootContext.SameElement(input, sentInput))
        {
            return;
        }

        sentOptions = options ?? sentOptions;
        sentInput = input;
        await handle.UpdateAsync(sentOptions!, References(input));
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            context?.RemoveListener(stateChanged);
            context?.RemoveInputListener(inputPublished);

            // NUM-PTR-17: an unmount mid-scrub clears the root's scrubbing state; no pointer release, so no commit.
            if (scrubContext.IsScrubbing)
            {
                scrubContext.SetScrubbing(false, scrubContext.CursorAllowed);
                context?.Root.SetScrubbing(false);
            }

            if (twin is not null)
            {
                twin.ScrubStarted -= started;
                twin.LockSettled -= lockSettled;
                twin.ScrubStepped -= stepped;
                twin.ScrubEnded -= ended;
                twin.StartDecider = null;
                _ = twin.DisposeAsync().AsTask();
                twin = null;
                handle = null;
            }
        }

        base.Dispose(disposing);
    }

    /// <inheritdoc />
    protected override bool ForwardsConsumerHandler(string eventName)
    {
        return !eventName.Equals("onpointerdown", StringComparison.OrdinalIgnoreCase);
    }

    /// <inheritdoc />
    protected override void AddPartAttributes(AttributeList attributes)
    {
        attributes.Set("role", "presentation");
        attributes.Set("style", PartStyle);
        if (consumerPointerDown.IsHandler)
        {
            attributes.Set("onpointerdown", onPointerDown);
        }
    }

    // The visible input is the module's only reference: the mouse press focuses it (NUM-FOC-04) and the release clicks
    // through only with it (NUM-PTR-14).
    private static TwinReferences? References(ElementReference? input)
    {
        if (input is not { } element)
        {
            return null;
        }

        TwinReferences references = new();
        references.Add(element);
        return references;
    }

    private async Task AttachAsync(ElementReference element)
    {
        sentOptions = ChangedOptions();
        sentInput = context!.Root.InputElement;
        twin ??= CreateTwin();
        handle = await twin.AttachAsync(element, sentOptions!, References(sentInput));
        attached.TrySetResult();
    }

    // The options to send when they differ from the last sent, otherwise null.
    private NumberFieldScrubOptions? ChangedOptions()
    {
        NumberFieldRoot root = context!.Root;
        bool? vertical = Direction == Orientation.Vertical ? true : null;
        double? pixelSensitivity = PixelSensitivity == 2 ? null : PixelSensitivity;
        bool? disabled = root.EffectiveDisabled ? true : null;
        bool? readOnly = root.ReadOnly ? true : null;
        if (sentOptions is { } sent
            && sent.Vertical == vertical
            && sent.PixelSensitivity == pixelSensitivity
            && sent.TeleportDistance == TeleportDistance
            && sent.Disabled == disabled
            && sent.ReadOnly == readOnly)
        {
            return null;
        }

        return new NumberFieldScrubOptions
        {
            Part = NumberFieldScrubTwin.ScrubPart,
            Vertical = vertical,
            PixelSensitivity = pixelSensitivity,
            TeleportDistance = TeleportDistance,
            Disabled = disabled,
            ReadOnly = readOnly,
        };
    }

    // Issue 370: an input published after this area began attaching is sent by a render; before that, the attach reads it.
    private void OnInputPublished()
    {
        if (attaching is not null && !NumberFieldRootContext.SameElement(context!.Root.InputElement, sentInput))
        {
            StateHasChanged();
        }
    }

    private NumberFieldScrubTwin CreateTwin()
    {
        NumberFieldScrubTwin created = new(JSRuntime) { StartDecider = Decide };
        created.ScrubStarted += started;
        created.LockSettled += lockSettled;
        created.ScrubStepped += stepped;
        created.ScrubEnded += ended;
        return created;
    }

    // NUM-STA-15, NUM-STR-06: the area's and the root's scrubbing state, and whether its cursor may render.
    private void OnStarted(NumberFieldScrubStartReport report)
    {
        scrubContext.SetScrubbing(true, report.Cursor);
        context!.Root.SetScrubbing(true);
    }

    private Task OnEndedAsync(NumberFieldScrubEndReport report)
    {
        scrubContext.SetScrubbing(false, scrubContext.CursorAllowed);
        return context!.Root.HandleScrubEndAsync(report.Event);
    }

    // The decision, on WebAssembly only: the consumer's handler runs inside the dispatch, so its PreventPartHandler() keeps
    // the scrub from starting; the marker keeps Blazor's own dispatch of the same event from running it twice.
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
            AltKey = snapshot.AltKey,
            CtrlKey = snapshot.CtrlKey,
            MetaKey = snapshot.MetaKey,
            ShiftKey = snapshot.ShiftKey,
        };
        pointerDownDecided = true;
        try
        {
            Task task = consumerPointerDown.InvokeAsync(args);
            if (task.IsFaulted)
            {
                _ = DispatchExceptionAsync(task.Exception!.GetBaseException());
            }
        }
        catch (Exception exception)
        {
            _ = DispatchExceptionAsync(exception);
        }

        return args.PartHandlerPrevented() ? DecisionPolicy.Stop : DecisionPolicy.Run;
    }

    private Task RunConsumerAsync(PointerEventArgs e)
    {
        bool pending = pointerDownDecided;
        pointerDownDecided = false;
        return !consumerPointerDown.IsHandler || pending ? Task.CompletedTask : consumerPointerDown.InvokeAsync(e);
    }
}

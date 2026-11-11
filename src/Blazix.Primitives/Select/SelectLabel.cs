using Blazix.Primitives.Interop;
using Blazix.Primitives.Rendering;
using Blazix.Primitives.Store;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace Blazix.Primitives;

// SEL-STA-18 (select/label/SelectLabel.tsx:27,46): the enclosing field's state, unmodified.
/// <summary>Represents the state of a select label, which the part exposes as data attributes and passes to its class and style functions.</summary>
/// <param name="Disabled">A value indicating whether the field is disabled; projected as <c>data-disabled</c>.</param>
/// <param name="Touched">A value indicating whether the field has been touched; projected as <c>data-touched</c>.</param>
/// <param name="Dirty">A value indicating whether the field's value differs from its initial value; projected as <c>data-dirty</c>.</param>
/// <param name="Valid">The field's verdict: projected as <c>data-valid</c> for <see langword="true"/>, <c>data-invalid</c> for <see langword="false"/>, and neither for <see langword="null"/>.</param>
/// <param name="Filled">A value indicating whether the field's control has a value; projected as <c>data-filled</c>.</param>
/// <param name="Focused">A value indicating whether the field's control has focus; projected as <c>data-focused</c>.</param>
[PartState]
public sealed partial record SelectLabelState(bool Disabled, bool Touched, bool Dirty, bool? Valid, bool Filled, bool Focused);

// Base UI's Select.Label (select/label/SelectLabel.tsx); the contract is docs/components/select.md §3.2, §5.4.
// - SEL-API-32, SEL-DOM-14: the id is always `<root id>-label`; a consumer id is dropped. It is published to the field's
//   labelable scope and to the store in OnParametersSet, in the batch that creates the label (ADR 01 Amendments
//   2026-10-22; the FieldLabel precedent), so the trigger's aria-labelledby is in the prerendered markup (dated notes on
//   SEL-DOM-14, SEL-FRM-15, SEL-EDGE-11).
// - SEL-DOM-15, FFI-PTR-01: the `form` module's non-native label role (pointerdown prevented, click focuses the control
//   with focusVisible, nested controls ignored, the double-click guard), with the consumer's pointer handlers inside the
//   `label` decision point on WebAssembly (the FieldLabel precedent).
/// <summary>Represents the visible label of a select, Base UI's <c>Select.Label</c>, rendered as a <c>div</c>.</summary>
/// <remarks>A click focuses the trigger without opening the popup. The label's id is derived from the root's and cannot be set.</remarks>
public sealed class SelectLabel : PartBase<SelectLabelState>
{
    private const string MouseDownEvent = "onmousedown";
    private const string ClickEvent = "onclick";
    private const string PointerDownEvent = "onpointerdown";

    private static readonly StateAttributesMapping<SelectLabelState> s_mapping = FieldValidityMapping.Create<SelectLabelState>();

    private readonly FieldConsumerHandlers consumerHandlers = new(MouseDownEvent, ClickEvent, PointerDownEvent);
    private readonly EventCallback onMouseDown;
    private readonly EventCallback onClick;
    private readonly EventCallback onPointerDown;
    private readonly Action fieldChanged;
    private readonly TaskCompletionSource attachedSource = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private SelectStore? store;
    private StoreSelection<SelectStoreState, string?>? rootId;
    private SelectorSubscription<SelectStoreState>? triggerWatch;
    private SelectLabelState? state;
    private FieldStateSnapshot renderedSnapshot;
    private string? publishedId;
    private FormTwin? twin;
    private TwinHandle<FormOptions>? handle;
    private FormOptions? sentOptions;
    private DecidedPointer? decided;

    /// <summary>Initializes a new instance of the <see cref="SelectLabel"/> class.</summary>
    public SelectLabel()
    {
        onMouseDown = CreateHandler<MouseEventArgs>(MouseDownEvent, e => RunConsumerAsync(MouseDownEvent, e));
        onClick = CreateHandler<MouseEventArgs>(ClickEvent, e => RunConsumerAsync(ClickEvent, e));
        onPointerDown = CreateHandler<PointerEventArgs>(PointerDownEvent, e => RunConsumerAsync(PointerDownEvent, e));
        fieldChanged = () => _ = InvokeAsync(OnFieldChanged);
    }

    [Inject]
    private IJSRuntime JSRuntime { get; set; } = null!;

    private string LabelId => (rootId!.Value ?? string.Empty) + "-label";

    [CascadingParameter]
    private SelectStore? RootStore { get; set; }

    [CascadingParameter]
    private FieldRootContext? FieldContext { get; set; }

    /// <inheritdoc />
    protected override string DefaultTagName => "div";

    /// <inheritdoc />
    protected override SelectLabelState State
    {
        get
        {
            FieldStateSnapshot snapshot = store!.Field?.FieldSnapshot ?? FieldStateSnapshot.Default;
            if (state is null || snapshot != renderedSnapshot)
            {
                renderedSnapshot = snapshot;
                state = new SelectLabelState(snapshot.Disabled, snapshot.Touched, snapshot.Dirty, snapshot.Valid, snapshot.Filled, snapshot.Focused);
            }

            return state;
        }
    }

    /// <inheritdoc />
    protected override StateAttributesMapping<SelectLabelState>? StateAttributesMapping => s_mapping;

    /// <summary>Gets a task that completes when the label's browser-side behavior is attached.</summary>
    internal Task TwinAttached => attachedSource.Task;

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">The label is not inside a <see cref="SelectRoot{TValue}"/>.</exception>
    protected override void OnInitialized()
    {
        store = SelectStore.Require(RootStore);
        store.FieldStateChanged += fieldChanged;

        // The trigger's id is only the label module's control id, which C# does not render: a change resends the options
        // without a render of the label.
        triggerWatch = new SelectorSubscription<SelectStoreState>(store, OnTriggerChanged);
        triggerWatch.Select(SelectStoreState.TriggerIdSelector);
        base.OnInitialized();
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        consumerHandlers.Set(AdditionalAttributes);
        Publish();
    }

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        // A root id that changed after the parameter set (the root renders first) moves the published id.
        Publish();
        if (!Enabled || Element is not { } element)
        {
            return;
        }

        FormOptions options = Options();
        if (handle is not null)
        {
            if (options != sentOptions)
            {
                sentOptions = options;
                await handle.UpdateAsync(options).ConfigureAwait(true);
            }

            return;
        }

        twin ??= new FormTwin(JSRuntime) { LabelDecider = Decide };
        sentOptions = options;
        handle = await twin.AttachAsync(element, options).ConfigureAwait(true);
        attachedSource.TrySetResult();
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            if (store is not null)
            {
                store.FieldStateChanged -= fieldChanged;
            }

            triggerWatch?.Dispose();

            Withdraw();
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
        SelectorSubscription<SelectStoreState> subscription = SubscribeTo<SelectStoreState>(store!);
        rootId = subscription.Select(SelectStoreState.RootIdSelector);
    }

    // SEL-API-32: the part's id wins over a consumer id (FinishAttributes changes only part-owned attributes).
    /// <inheritdoc />
    protected override void FinishAttributes(AttributeList attributes) => attributes.Set("id", LabelId);

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

    /// <inheritdoc />
    protected override bool ForwardsConsumerHandler(string eventName) => !consumerHandlers.Declines(eventName);

    // FFI-ALG-07's fallback (useLabel's fallbackControlId): the field's control id, else the trigger's, else the root's.
    private FormOptions Options() => new()
    {
        Label = new FormLabelOptions
        {
            Native = false,
            ControlId = FieldContext?.Labelable.ControlId ?? store!.State.TriggerId ?? rootId!.Value,
            Decides = consumerHandlers.Any,
        },
    };

    private void Publish()
    {
        string? id = Enabled && rootId!.Value is not null ? LabelId : null;
        if (string.Equals(id, publishedId, StringComparison.Ordinal))
        {
            return;
        }

        Withdraw();
        if (id is not null)
        {
            FieldContext?.Labelable.Label.Publish(id);
            store!.SetLabelId(id);
            publishedId = id;
        }
    }

    private void Withdraw()
    {
        if (publishedId is null)
        {
            return;
        }

        FieldContext?.Labelable.Label.Withdraw(publishedId);
        if (string.Equals(store!.State.LabelId, publishedId, StringComparison.Ordinal))
        {
            store.SetLabelId(null);
        }

        publishedId = null;
    }

    // The label decision point, on WebAssembly only: the consumer's handler runs inside the dispatch.
    private DecisionPolicy Decide(EventSnapshot snapshot)
    {
        string eventName = "on" + snapshot.Type;
        if (!consumerHandlers.Has(eventName))
        {
            return DecisionPolicy.Run;
        }

        MouseEventArgs args = FieldConsumerHandlers.ToMouseEventArgs(snapshot);
        decided = DecidedPointer.From(args);
        return consumerHandlers.RunSynchronously(eventName, args, DispatchExceptionAsync) ? DecisionPolicy.Stop : DecisionPolicy.Run;
    }

    private Task RunConsumerAsync(string eventName, MouseEventArgs e)
    {
        // The decision already ran the consumer's handler for the event Blazor dispatches now.
        DecidedPointer? pending = decided;
        decided = null;
        if (pending is { } previous && previous == DecidedPointer.From(e))
        {
            return Task.CompletedTask;
        }

        return consumerHandlers.RunAsync(eventName, e);
    }

    private void OnFieldChanged()
    {
        SelectLabelState? previous = state;
        if (previous is null || previous != State)
        {
            StateHasChanged();
        }
    }

    private void OnTriggerChanged() => _ = SendOptionsAsync();

    private async Task SendOptionsAsync()
    {
        if (handle is null)
        {
            return;
        }

        FormOptions options = Options();
        if (options != sentOptions)
        {
            sentOptions = options;
            await handle.UpdateAsync(options).ConfigureAwait(true);
        }
    }

    /// <summary>The pointer event a decision ran the consumer's handler for, matched against Blazor's dispatch of it.</summary>
    private readonly record struct DecidedPointer(string Type, long Detail, long Buttons, bool AltKey, bool CtrlKey, bool MetaKey, bool ShiftKey)
    {
        public static DecidedPointer From(MouseEventArgs e) => new(e.Type, e.Detail, e.Buttons, e.AltKey, e.CtrlKey, e.MetaKey, e.ShiftKey);
    }
}

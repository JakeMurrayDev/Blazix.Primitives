using Blazix.Primitives.Interop;
using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace Blazix.Primitives;

// FLD-STA-02; field/label/FieldLabel.tsx:37-40,88.
/// <summary>Represents the state of a field label, which the part exposes as data attributes and passes to its class and style functions.</summary>
/// <param name="Disabled">A value indicating whether the field, or the item the label is in, is disabled; projected as <c>data-disabled</c>.</param>
/// <param name="Touched">A value indicating whether the field has been touched; projected as <c>data-touched</c>.</param>
/// <param name="Dirty">A value indicating whether the field's value differs from its initial value; projected as <c>data-dirty</c>.</param>
/// <param name="Valid">The field's verdict: projected as <c>data-valid</c> for <see langword="true"/>, <c>data-invalid</c> for <see langword="false"/>, and neither for <see langword="null"/>.</param>
/// <param name="Filled">A value indicating whether the field's control has a value; projected as <c>data-filled</c>.</param>
/// <param name="Focused">A value indicating whether the field's control has focus; projected as <c>data-focused</c>.</param>
[PartState]
public sealed partial record FieldLabelState(bool Disabled, bool Touched, bool Dirty, bool? Valid, bool Filled, bool Focused);

// FLD-DOM-04; field/label/FieldLabelDataAttributes.ts.
/// <summary>Provides the names of the data attributes a field label renders.</summary>
public static class FieldLabelDataAttributes
{
    /// <summary>Represents the attribute present while the label is disabled: <c>data-disabled</c>.</summary>
    public const string Disabled = "data-disabled";

    /// <summary>Represents the attribute present while the field is valid: <c>data-valid</c>.</summary>
    public const string Valid = "data-valid";

    /// <summary>Represents the attribute present while the field is invalid: <c>data-invalid</c>.</summary>
    public const string Invalid = "data-invalid";

    /// <summary>Represents the attribute present once the field has been touched: <c>data-touched</c>.</summary>
    public const string Touched = "data-touched";

    /// <summary>Represents the attribute present while the field's value differs from its initial value: <c>data-dirty</c>.</summary>
    public const string Dirty = "data-dirty";

    /// <summary>Represents the attribute present while the field has a value: <c>data-filled</c>.</summary>
    public const string Filled = "data-filled";

    /// <summary>Represents the attribute present while the field's control has focus: <c>data-focused</c>.</summary>
    public const string Focused = "data-focused";
}

// Base UI's Field.Label (field/label/FieldLabel.tsx, internals/labelable-provider/useLabel.ts); the contract is
// docs/components/field.md. FLD-API-12, FFI-ARIA-08: the id (the consumer's or a generated one) is published to the scope
// while its parameters are set, in the render batch that creates the label (ADR 01 Amendments 2026-10-22); each label
// publishes its own id and the last publication wins (FLD §16 Q2, settled by the orchestrator 2026-10-22). FLD-API-11, FLD-DOM-06/07: a native
// label renders `for` from the scope's control id (none for a `null` claim); a non-native one renders none. The pointer
// handling (the double-click guard; for a non-native label the pointerdown prevention and the click-to-focus with
// focusVisible; nested interactive targets ignored, FFI-PTR-01..04) is the form module's, inside the dispatch. The
// consumer's mousedown, click and pointerdown handlers run first (FLD-PTR-04): inside the decision point `label` on
// WebAssembly, after the module on Server (ADR 01 "C#-origin consumer vetoes"). FLD-EDGE-02: the module reports a label
// element that contradicts NativeLabel, logged once per NativeLabel value through the development warnings switch.
/// <summary>
/// Represents the label of a field's control: Base UI's <c>Field.Label</c>, rendered as a <c>label</c>. It belongs inside a
/// <see cref="FieldRoot"/> and labels the field's control, or the control of the <see cref="FieldItem"/> it is in.
/// </summary>
/// <remarks>
/// A native label points at the control through <c>for</c>. With <see cref="NativeLabel"/> set to <see langword="false"/>,
/// for an element other than a <c>label</c>, the control references the label through <c>aria-labelledby</c> instead and a
/// click on the label moves focus to the control.
/// </remarks>
public sealed class FieldLabel : PartBase<FieldLabelState>
{
    private const string DefaultTag = "label";
    private const string MouseDownEvent = "onmousedown";
    private const string ClickEvent = "onclick";
    private const string PointerDownEvent = "onpointerdown";

    // FFI-EDGE-02 in the port's words.
    private const string NativeMismatchMessage =
        "Blazix: `<FieldLabel>` expected a `<label>` element because the `NativeLabel` parameter is `true`. "
        + "Rendering a non-`<label>` disables native label association, so `for` will not work. "
        + "Use a real `<label>` in the `Render` parameter, or set `NativeLabel` to `false`.";

    private const string NonNativeMismatchMessage =
        "Blazix: `<FieldLabel>` expected a non-`<label>` element because the `NativeLabel` parameter is `false`. "
        + "Rendering a `<label>` assumes native label behavior while the label is treated as non-native, which can cause unexpected pointer behavior. "
        + "Use a non-`<label>` in the `Render` parameter, or set `NativeLabel` to `true`.";

    private static readonly StateAttributesMapping<FieldLabelState> s_mapping = FieldValidityMapping.Create<FieldLabelState>();

    private readonly FieldConsumerHandlers consumerHandlers = new(MouseDownEvent, ClickEvent, PointerDownEvent);
    private readonly EventCallback onMouseDown;
    private readonly EventCallback onClick;
    private readonly EventCallback onPointerDown;
    private readonly Action fieldChanged;
    private readonly TaskCompletionSource attachedSource = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private FieldRootContext? field;
    private FieldItemContext? item;
    private LabelableScope? scope;
    private string? publishedId;
    private FieldLabelState? state;
    private FieldStateSnapshot renderedSnapshot;
    private string? renderedControlId;
    private FormTwin? twin;
    private TwinHandle<FormOptions>? handle;
    private FormOptions? sentOptions;
    private DecidedPointer? decided;
    private bool? reportedNativeLabel;

    /// <summary>Initializes a new instance of the <see cref="FieldLabel"/> class.</summary>
    public FieldLabel()
    {
        onMouseDown = CreateHandler<MouseEventArgs>(MouseDownEvent, e => RunConsumerAsync(MouseDownEvent, e));
        onClick = CreateHandler<MouseEventArgs>(ClickEvent, e => RunConsumerAsync(ClickEvent, e));
        onPointerDown = CreateHandler<PointerEventArgs>(PointerDownEvent, e => RunConsumerAsync(PointerDownEvent, e));
        fieldChanged = OnFieldChanged;
    }

    [Inject]
    private IJSRuntime JSRuntime { get; set; } = null!;

    [Inject]
    private ILogger<FieldLabel> Logger { get; set; } = null!;

    [CascadingParameter]
    private FieldRootContext? FieldContext { get; set; }

    [CascadingParameter]
    private FieldItemContext? ItemContext { get; set; }

    // FLD-API-11
    /// <summary>Gets or sets a value indicating whether the rendered element is a native <c>label</c>.</summary>
    /// <value>
    /// <see langword="true"/>, the default, to associate the label through <c>for</c>; <see langword="false"/> when
    /// <see cref="PartBase{TState}.Render"/> supplies another element, which the control then references through
    /// <c>aria-labelledby</c> and which moves focus to the control when clicked.
    /// </value>
    [Parameter]
    public bool NativeLabel { get; set; } = true;

    /// <inheritdoc />
    protected override string DefaultTagName => DefaultTag;

    /// <inheritdoc />
    protected override FieldLabelState State
    {
        get
        {
            FieldStateSnapshot snapshot = Snapshot();
            if (state is null || snapshot != renderedSnapshot)
            {
                renderedSnapshot = snapshot;
                state = new FieldLabelState(snapshot.Disabled, snapshot.Touched, snapshot.Dirty, snapshot.Valid, snapshot.Filled, snapshot.Focused);
            }

            return state;
        }
    }

    /// <inheritdoc />
    protected override StateAttributesMapping<FieldLabelState>? StateAttributesMapping => s_mapping;

    /// <inheritdoc />
    protected override bool UsesGeneratedId => true;

    // For the fixtures: completes once the form module is attached, so a browser test can wait for its listeners.
    /// <summary>Gets a task that completes when the label's browser-side behavior is attached to its element.</summary>
    /// <value>A task that completes after the first interactive render attached the behavior.</value>
    internal Task TwinAttached => attachedSource.Task;

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">The label is not inside a <see cref="FieldRoot"/>.</exception>
    protected override void OnInitialized()
    {
        field = FieldScopes.RequireField(FieldContext);
        item = ItemContext;
        scope = FieldItemContext.LabelableOf(field, item);
        field.AddListener(fieldChanged);
        base.OnInitialized();
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        consumerHandlers.Set(AdditionalAttributes);
        renderedControlId = scope!.ControlId;

        // A disabled label renders no element, so it publishes no id.
        string? id = Enabled ? Id : null;
        if (string.Equals(id, publishedId, StringComparison.Ordinal))
        {
            return;
        }

        if (id is not null)
        {
            scope.Label.Publish(id);
        }
        else if (publishedId is not null)
        {
            scope.Label.Withdraw(publishedId);
        }

        publishedId = id;
    }

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
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

        twin ??= CreateTwin();
        sentOptions = options;
        if (Element is { } element)
        {
            handle = await twin.AttachAsync(element, options).ConfigureAwait(true);
        }
        else
        {
            try
            {
                handle = await twin.AttachAsync(Id!, options).ConfigureAwait(true);
            }
            catch (JSException)
            {
                // FFI-EDGE-02: a substitute that renders no element leaves nothing to attach to and nothing to check.
                sentOptions = null;
                return;
            }
        }

        attachedSource.TrySetResult();
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            field?.RemoveListener(fieldChanged);
            if (publishedId is not null)
            {
                scope!.Label.Withdraw(publishedId);
                publishedId = null;
            }

            if (twin is not null)
            {
                twin.LabelDecider = null;
                twin.LabelMismatched -= ReportMismatch;
                _ = twin.DisposeAsync().AsTask();
                twin = null;
                handle = null;
            }
        }

        base.Dispose(disposing);
    }

    /// <inheritdoc />
    protected override void AddPartAttributes(AttributeList attributes)
    {
        // FLD-DOM-06/07: a consumer `for` replaces this one (the pipeline merges the consumer last).
        if (NativeLabel && scope!.ControlId is { } controlId)
        {
            attributes.Set("for", controlId);
        }

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

    private FieldStateSnapshot Snapshot() => field!.State with { Disabled = FieldItemContext.DisabledOf(field, item) };

    private FormOptions Options() => new()
    {
        Label = new FormLabelOptions
        {
            Native = NativeLabel,
            ControlId = scope!.ControlId,
            Decides = consumerHandlers.Any,
            DevelopmentChecks = DevelopmentWarnings.IsEnabled,
        },
    };

    private FormTwin CreateTwin()
    {
        FormTwin created = new(JSRuntime) { LabelDecider = Decide };
        created.LabelMismatched += ReportMismatch;
        return created;
    }

    // FLD-EDGE-02: once per NativeLabel value.
    private void ReportMismatch(bool expectedNative)
    {
        if (reportedNativeLabel == NativeLabel)
        {
            return;
        }

        reportedNativeLabel = NativeLabel;
        DevelopmentWarnings.Write(Logger, expectedNative ? NativeMismatchMessage : NonNativeMismatchMessage);
    }

    // The label decision point, on WebAssembly only: the consumer's handler runs inside the dispatch, so its
    // PreventPartHandler() stops the module's handling of the event.
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
        if (state is not null && Snapshot() == renderedSnapshot && string.Equals(renderedControlId, scope!.ControlId, StringComparison.Ordinal))
        {
            return;
        }

        renderedControlId = scope!.ControlId;
        StateHasChanged();
    }

    /// <summary>The pointer event a decision ran the consumer's handler for, matched against Blazor's dispatch of it.</summary>
    private readonly record struct DecidedPointer(string Type, long Detail, long Buttons, bool AltKey, bool CtrlKey, bool MetaKey, bool ShiftKey)
    {
        public static DecidedPointer From(MouseEventArgs e) => new(e.Type, e.Detail, e.Buttons, e.AltKey, e.CtrlKey, e.MetaKey, e.ShiftKey);
    }
}

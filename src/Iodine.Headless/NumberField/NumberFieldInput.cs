using Iodine.Headless.Interop;
using Iodine.Headless.Rendering;
using Iodine.Headless.Utilities;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace Iodine.Headless;

// Base UI's NumberField.Input (number-field/input/NumberFieldInput.tsx:114-130); the contract is docs/components/number-field.md
// §3.6 and §5.3. A text input (NUM-DOM-08, NUM-ARIA-01): the labelable control id (NUM-DOM-09), the display text, the
// inputmode of NUM-STA-19, autocomplete, autocorrect and spellcheck off, the root's required, disabled and read-only states,
// `aria-roledescription` (a consumer value wins at the merge, NUM-API-22), never `name` (NUM-DOM-18).
// - The `number-field` module (6a, design point 2 (b), settled by the orchestrator 2026-11-18) owns the input's `value` from
//   attach (C# → JS, design point 7 (a)): C# renders the first text, then sends the text with the echoed revision. Focus and
//   blur reach the root as the module's reports (ADR 04 Amendments 2026-10-18 item 1). The consumer's `onfocus` and `onblur`
//   are declined and run by the part: on WebAssembly inside the `input-focus` / `input-blur` decision, where
//   PreventPartHandler() stops the report; on Server from the part's own handler, a round trip after the module reported
//   (ADR 01's C#-origin veto row).
// - 6b's input role: the module's keydown gate runs over the allowed set and locale symbols this part sends (decision 7 (a),
//   NUM-KEY-07/09); its key steps, edits, pastes, wheel steps and the hidden input's autofill reach the root as reports
//   (NUM-KEY-01/02, NUM-EVT-15/16, NUM-PTR-07, NUM-FRM-05). The consumer's `onkeydown`, `onpaste` and `oninput` are declined
//   and run as `onfocus` is, through the `input-key`, `input-paste` and `input-change` points (design point 1 (a)).
/// <summary>
/// Represents the text input of a number field: Base UI's <c>NumberField.Input</c>, rendered as an <c>input type="text"</c>
/// that shows the formatted value. It must be placed within a <see cref="NumberFieldRoot"/>.
/// </summary>
public sealed class NumberFieldInput : PartBase<NumberFieldInputState>
{
    private const string DefaultTag = "input";
    private const string RoleDescription = "Number field";

    private static readonly StateAttributesMapping<NumberFieldInputState> s_mapping = FieldValidityMapping.Create<NumberFieldInputState>();

    private readonly TaskCompletionSource attached = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly Action stateChanged;
    private readonly EventCallback onFocus;
    private readonly EventCallback onBlur;
    private readonly Action<EventSnapshot> focused;
    private readonly Action<NumberFieldBlurReport> blurred;
    private readonly Action<NumberFieldKeyStepReport> keyStepped;
    private readonly Action<NumberFieldChangeReport> changed;
    private readonly Action<NumberFieldPasteReport> pasted;
    private readonly Action<NumberFieldWheelReport> wheelStepped;
    private readonly Action<NumberFieldAutofillReport> autofilled;
    private NumberFieldRootContext? context;
    private NumberFieldTwin? twin;
    private TwinHandle<NumberFieldOptions>? handle;
    private NumberFieldOptions? sentOptions;
    private NumberFieldInputOptions? sentInput;
    private string? renderedValue;
    private AttributeValue consumerFocus;
    private AttributeValue consumerBlur;
    private AttributeValue consumerKeyDown;
    private AttributeValue consumerPaste;
    private AttributeValue consumerInput;
    // One marker per declined handler: whichever of the decision and Blazor's own dispatch comes first runs the consumer.
    private readonly ConsumerMarker focusMarker = new(blazorFirst: true);
    private readonly ConsumerMarker blurMarker = new(blazorFirst: true);
    private readonly ConsumerMarker keyMarker = new(blazorFirst: true);
    private readonly ConsumerMarker pasteMarker = new(blazorFirst: false);
    private readonly ConsumerMarker inputMarker = new(blazorFirst: false);

    // Created the first time a consumer passes the handler: each handler slot costs every render (ADR 16 rule 7).
    private EventCallback onKeyDown;
    private EventCallback onPaste;
    private EventCallback onInput;
    private NumberFieldInputState? state;
    private int stateVersion = -1;

    /// <summary>Initializes a new instance of the <see cref="NumberFieldInput"/> class.</summary>
    public NumberFieldInput()
    {
        stateChanged = StateHasChanged;
        onFocus = CreateHandler<FocusEventArgs>("onfocus", e => RunConsumerAsync(e, consumerFocus, focusMarker));
        onBlur = CreateHandler<FocusEventArgs>("onblur", e => RunConsumerAsync(e, consumerBlur, blurMarker));
        focused = snapshot => _ = InvokeAsync(() => context!.Root.HandleInputFocus());
        blurred = report => _ = InvokeAsync(() => context!.Root.HandleInputBlurAsync(report.Text, report.Revision, report.Event));
        keyStepped = report => _ = InvokeAsync(() => context!.Root.HandleKeyStepAsync(report.Key, report.Text, report.Revision, report.Event));
        changed = report => _ = InvokeAsync(() => context!.Root.HandleInputChangeAsync(report.Text, report.Revision, report.Event));
        pasted = report => _ = InvokeAsync(() => context!.Root.HandleInputPasteAsync(report.Text, report.Revision, report.Event));
        wheelStepped = report => _ = InvokeAsync(() => context!.Root.HandleWheelStepAsync(report.Direction, report.Event));
        autofilled = report => _ = InvokeAsync(() => context!.Root.HandleAutofillAsync(report.Value, report.Event));
    }

    [Inject]
    private IJSRuntime JSRuntime { get; set; } = null!;

    [CascadingParameter]
    private NumberFieldRootContext? RootContext { get; set; }

    /// <inheritdoc />
    protected override string DefaultTagName => DefaultTag;

    /// <inheritdoc />
    protected override NumberFieldInputState State
    {
        get
        {
            // NUM-STA-02: the root's state, rebuilt only when the root made a new one.
            if (state is null || stateVersion != context!.StateVersion)
            {
                NumberFieldRootState root = context!.State;
                stateVersion = context.StateVersion;
                state = new NumberFieldInputState(root.Value, root.InputValue, root.Required, root.Disabled, root.ReadOnly, root.Scrubbing, root.Touched, root.Dirty, root.Valid, root.Filled, root.Focused);
            }

            return state;
        }
    }

    /// <inheritdoc />
    protected override StateAttributesMapping<NumberFieldInputState>? StateAttributesMapping => s_mapping;

    /// <summary>Gets a task that completes once the <c>number-field</c> module is attached (the browser fixtures' marker).</summary>
    internal Task TwinAttached => attached.Task;

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">The input is not placed within a <see cref="NumberFieldRoot"/>.</exception>
    protected override void OnInitialized()
    {
        context = NumberFieldRootContext.Require(RootContext);
        context.AddListener(stateChanged);
        context.Root.SetInputMounted(true);
        base.OnInitialized();
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        consumerFocus = default;
        consumerBlur = default;
        consumerKeyDown = default;
        consumerPaste = default;
        consumerInput = default;
        if (AdditionalAttributes is null)
        {
            return;
        }

        foreach (KeyValuePair<string, object> pair in AdditionalAttributes)
        {
            if (!IsDeclined(pair.Key))
            {
                continue;
            }

            AttributeValue value = AttributeValue.FromObject(pair.Value);
            if (!MergeProps.IsHandler(pair.Key, value))
            {
                continue;
            }

            switch (pair.Key.ToLowerInvariant())
            {
                case "onfocus":
                    consumerFocus = value;
                    break;
                case "onblur":
                    consumerBlur = value;
                    break;
                case "onkeydown":
                    consumerKeyDown = value;
                    if (!onKeyDown.HasDelegate)
                    {
                        onKeyDown = CreateHandler<KeyboardEventArgs>("onkeydown", e => RunConsumerAsync(e, consumerKeyDown, keyMarker));
                    }

                    break;
                case "onpaste":
                    consumerPaste = value;
                    if (!onPaste.HasDelegate)
                    {
                        onPaste = CreateHandler<ClipboardEventArgs>("onpaste", e => RunConsumerAsync(e, consumerPaste, pasteMarker));
                    }

                    break;
                default:
                    consumerInput = value;
                    if (!onInput.HasDelegate)
                    {
                        onInput = CreateHandler<ChangeEventArgs>("oninput", e => RunConsumerAsync(e, consumerInput, inputMarker));
                    }

                    break;
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

        NumberFieldRoot root = context!.Root;
        context.PublishInput(element);
        string text = root.InputValue;
        int revision = root.EchoedRevision;
        string allowed = root.AllowedCharacters();
        bool disabled = root.EffectiveDisabled;
        bool readOnly = root.ReadOnly;
        bool wheel = root.AllowWheelScrub && !disabled && !readOnly;
        bool hasMin = root.Min.HasValue;
        bool hasMax = root.Max.HasValue;
        bool gateChanged = sentInput is not { } gate
            || !ReferenceEquals(gate.Allowed, allowed)
            || gate.Min != hasMin
            || gate.Max != hasMax
            || gate.Wheel != wheel
            || gate.Disabled != disabled
            || gate.ReadOnly != readOnly;
        if (handle is null)
        {
            twin ??= CreateTwin();
            sentInput = InputOptions(root, allowed, hasMin, hasMax, wheel, disabled, readOnly);
            sentOptions = new NumberFieldOptions { Value = text, Revision = revision, Input = sentInput };
            TwinReferences? references = null;
            if (root.HiddenInput is { } hidden)
            {
                references = new TwinReferences();
                references.Add(hidden);
            }

            handle = await twin.AttachAsync(element, sentOptions, references);
            attached.TrySetResult();
            return;
        }

        // Compared before constructing, so an unchanged render allocates no options record.
        if (!gateChanged && sentOptions is { } sent && string.Equals(sent.Value, text, StringComparison.Ordinal) && sent.Revision == revision)
        {
            return;
        }

        // ADR 16 rule 7: the input part goes only with an update that changes it; the module keeps the last one.
        NumberFieldInputOptions? input = null;
        if (gateChanged)
        {
            sentInput = InputOptions(root, allowed, hasMin, hasMax, wheel, disabled, readOnly);
            input = sentInput;
        }

        sentOptions = new NumberFieldOptions { Value = text, Revision = revision, Input = input };
        await handle.UpdateAsync(sentOptions);
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            context?.RemoveListener(stateChanged);
            context?.Root.SetInputMounted(false);
            if (context is not null && NumberFieldRootContext.SameElement(context.Root.InputElement, Element))
            {
                context.PublishInput(null);
            }

            if (twin is not null)
            {
                twin.InputFocused -= focused;
                twin.InputBlurred -= blurred;
                twin.KeyStepped -= keyStepped;
                twin.InputChanged -= changed;
                twin.InputPasted -= pasted;
                twin.WheelStepped -= wheelStepped;
                twin.Autofilled -= autofilled;
                twin.FocusDecider = null;
                twin.BlurDecider = null;
                twin.KeyDecider = null;
                twin.PasteDecider = null;
                twin.ChangeDecider = null;
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
        return !IsDeclined(eventName);
    }

    /// <inheritdoc />
    protected override void AddPartAttributes(AttributeList attributes)
    {
        NumberFieldRoot root = context!.Root;
        NumberFieldRootState rootState = context.State;
        attributes.Set("id", root.InputId);
        attributes.Set("required", rootState.Required);
        attributes.Set("disabled", rootState.Disabled);
        attributes.Set("readonly", rootState.ReadOnly);
        attributes.Set("inputmode", root.InputMode);
        // The handover at attach: the first text is rendered, then the module writes it.
        if (handle is null)
        {
            renderedValue = rootState.InputValue;
        }

        attributes.Set("value", renderedValue);
        if (consumerFocus.IsHandler)
        {
            attributes.Set("onfocus", onFocus);
        }

        if (consumerBlur.IsHandler)
        {
            attributes.Set("onblur", onBlur);
        }

        if (consumerKeyDown.IsHandler)
        {
            attributes.Set("onkeydown", onKeyDown);
        }

        if (consumerPaste.IsHandler)
        {
            attributes.Set("onpaste", onPaste);
        }

        if (consumerInput.IsHandler)
        {
            attributes.Set("oninput", onInput);
        }
        attributes.Set("type", "text");
        attributes.Set("autocomplete", "off");
        attributes.Set("autocorrect", "off");
        attributes.Set("spellcheck", "false");
        attributes.Set("aria-roledescription", RoleDescription);

        // NUM-ARIA-03: the field label's id; a consumer aria-labelledby wins at the merge.
        if (root.Field.FieldLabelId is { } labelId)
        {
            attributes.Set("aria-labelledby", labelId);
        }
    }

    // ADR 02 Amendments 2026-10-21 (FinishAttributes); NUM-DOM-15: the field's validation props come last, so the composed
    // aria-describedby keeps the consumer's ids first and aria-invalid wins over a consumer value (NUM-ARIA-02/04).
    /// <inheritdoc />
    protected override void FinishAttributes(AttributeList attributes)
    {
        NumberFieldField field = context!.Root.Field;
        if (!field.InField)
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

        if (field.AriaInvalid)
        {
            attributes.Set("aria-invalid", "true");
        }
        else
        {
            attributes.Remove("aria-invalid");
        }
    }

    private NumberFieldTwin CreateTwin()
    {
        NumberFieldTwin created = new(JSRuntime)
        {
            FocusDecider = snapshot => Decide(snapshot, consumerFocus, focusMarker),
            BlurDecider = snapshot => Decide(snapshot, consumerBlur, blurMarker),
            KeyDecider = snapshot => Decide(snapshot, consumerKeyDown, keyMarker),
            PasteDecider = snapshot => Decide(snapshot, consumerPaste, pasteMarker),
            ChangeDecider = snapshot => Decide(snapshot, consumerInput, inputMarker),
        };
        created.InputFocused += focused;
        created.InputBlurred += blurred;
        created.KeyStepped += keyStepped;
        created.InputChanged += changed;
        created.InputPasted += pasted;
        created.WheelStepped += wheelStepped;
        created.Autofilled += autofilled;
        return created;
    }

    // The decision, on WebAssembly only: the consumer's handler runs inside the dispatch, so its PreventPartHandler() keeps
    // the report back; the marker keeps Blazor's own dispatch of the same event from running it twice. Blazor dispatches
    // onkeydown (document, bubble) and onfocus and onblur (document, capture) before the module decides (the keydown at the
    // input's root node, focus and blur in the target phase): its run left the verdict, which the decision takes. On
    // WebAssembly both happen inside the one browser dispatch, and Blazor's run of the next such event replaces the verdict.
    private DecisionPolicy Decide(EventSnapshot snapshot, AttributeValue consumer, ConsumerMarker marker)
    {
        if (!consumer.IsHandler)
        {
            return DecisionPolicy.Run;
        }

        if (marker.Ran)
        {
            marker.Ran = false;
            return marker.RanPrevented ? DecisionPolicy.Stop : DecisionPolicy.Run;
        }

        EventArgs args = snapshot.Type switch
        {
            "keydown" => new KeyboardEventArgs
            {
                Type = snapshot.Type,
                Key = snapshot.Key ?? string.Empty,
                Code = snapshot.Code ?? string.Empty,
                AltKey = snapshot.AltKey,
                CtrlKey = snapshot.CtrlKey,
                MetaKey = snapshot.MetaKey,
                ShiftKey = snapshot.ShiftKey,
            },
            "paste" => new ClipboardEventArgs { Type = snapshot.Type },
            "input" => new ChangeEventArgs(),
            _ => new FocusEventArgs { Type = snapshot.Type },
        };
        marker.Decided = true;
        try
        {
            Task task = consumer.InvokeAsync(args);
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

    // NUM-API-25 and design point 1 (a): the consumer handlers the part runs itself.
    private static bool IsDeclined(string eventName) =>
        eventName.Equals("onfocus", StringComparison.OrdinalIgnoreCase)
        || eventName.Equals("onblur", StringComparison.OrdinalIgnoreCase)
        || eventName.Equals("onkeydown", StringComparison.OrdinalIgnoreCase)
        || eventName.Equals("onpaste", StringComparison.OrdinalIgnoreCase)
        || eventName.Equals("oninput", StringComparison.OrdinalIgnoreCase);

    private static NumberFieldInputOptions InputOptions(NumberFieldRoot root, string allowed, bool hasMin, bool hasMax, bool wheel, bool disabled, bool readOnly)
    {
        NumberLocaleDetails details = root.Formatter.LocaleDetails();
        return new NumberFieldInputOptions
        {
            Allowed = allowed,
            Decimal = details.Decimal,
            Currency = details.Currency,
            Percent = details.PercentSign,
            Min = hasMin,
            Max = hasMax,
            Wheel = wheel,
            Disabled = disabled,
            ReadOnly = readOnly,
            DevelopmentChecks = DevelopmentWarnings.IsEnabled ? true : null,
        };
    }

    private Task RunConsumerAsync(EventArgs e, AttributeValue consumer, ConsumerMarker marker)
    {
        bool pending = marker.Decided;
        marker.Decided = false;
        if (!consumer.IsHandler || pending)
        {
            return Task.CompletedTask;
        }

        Task task = consumer.InvokeAsync(e);
        if (marker.BlazorFirst)
        {
            marker.Ran = true;
            marker.RanPrevented = e.PartHandlerPrevented();
        }

        return task;
    }

    // Whether the decision or Blazor's dispatch ran a declined consumer handler for the event in flight.
    private sealed class ConsumerMarker(bool blazorFirst)
    {
        public bool BlazorFirst { get; } = blazorFirst;

        public bool Decided { get; set; }

        public bool Ran { get; set; }

        public bool RanPrevented { get; set; }
    }
}

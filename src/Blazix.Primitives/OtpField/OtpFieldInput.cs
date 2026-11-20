using System.Globalization;
using Blazix.Primitives.Interop;
using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace Blazix.Primitives;

// Base UI's OTPField.Input (otp-field/input/OTPFieldInput.tsx); the contract is docs/components/otp-field.md §3.2 and §5.2
// (issue 278, M6 track 7).
// - One `<input>` per slot: its id derives from the root's control id and its index (OTP-DOM-07); the slot's character as its
//   value (OTP-DOM-08); type, inputmode, autocomplete, autocorrect, spellcheck, enterkeyhint, maxlength, the root's states and
//   form, the slot pattern and the roving tabindex (OTP-DOM-09..16, OTP-FOC-01); never `name` (OTP-FRM-02).
// - Its index is its position among the root's slots: the render-order guess of the root's list until the module's order
//   report (OTP-STR-03, OTP-EDGE-10/11; CMP-STA-16); there is no Index parameter (OTP-API-24).
// - The consumer's aria-label and aria-labelledby are consumed: aria-label applies from slot 1 on, aria-labelledby wins over
//   the inherited group label (OTP-API-21/22, OTP-ARIA-06..08).
// - The consumer's onkeydown, oninput, onpaste, onmousedown, onfocus and onblur are declined and run from the part's own
//   handler, and on WebAssembly inside the root's decision with a one-shot marker (ADR 02 Amendments 2026-10-21; design
//   point 1 (a), chosen by the user 2026-11-19; the NumberFieldInput precedent): Blazor dispatches each at the document
//   before the module, which listens at the root node, so its run leaves the verdict the decision takes.
// - The slot's `value` and `tabindex` are rendered until the root's module attaches, then the module owns them (OTP-DOM-08;
//   OTP-FOC-01, design point 5 (a)): the rendered attributes stay as handed over, so Blazor's diff never writes them again.
// - Slot 0 discovers its native label through the `form` module's label fallback (FFI-ALG-08, OTP-ARIA-04): the root
//   takes the label as the group's when neither the consumer nor a field labels it.
/// <summary>
/// Represents one character of a one-time code field: Base UI's <c>OTPField.Input</c>, rendered as an <c>input</c>. It
/// must be placed within an <see cref="OtpFieldRoot"/>.
/// </summary>
public sealed class OtpFieldInput : PartBase<OtpFieldInputState>
{
    private const string DefaultTag = "input";
    private const string AriaLabel = "aria-label";
    private const string AriaLabelledBy = "aria-labelledby";
    private const string LabelReport = "label";

    private static readonly StateAttributesMapping<OtpFieldInputState> s_mapping = FieldValidityMapping.Create<OtpFieldInputState>();

    private readonly Action stateChanged;
    private readonly Action indexChanged;
    private readonly Action<FormControlReport> labelReported;
    private OtpFieldRootContext? context;
    private CompositeListItemBehavior? listItem;
    private FormTwin? labelTwin;
    private TwinHandle<FormOptions>? labelHandle;
    private bool sentLabelFallback;
    private OtpFieldInputState? state;
    private int stateVersion = -1;
    private int stateIndex = -1;
    private string? renderedValue;
    private string? renderedTabIndex;

    // One declined consumer handler per event the module handles (design point 1 (a)).
    private readonly ConsumerHandler keyDown;
    private readonly ConsumerHandler input;
    private readonly ConsumerHandler paste;
    private readonly ConsumerHandler mouseDown;
    private readonly ConsumerHandler focus;
    private readonly ConsumerHandler blur;

    /// <summary>Initializes a new instance of the <see cref="OtpFieldInput"/> class.</summary>
    public OtpFieldInput()
    {
        stateChanged = StateHasChanged;
        indexChanged = OnIndexChanged;
        labelReported = report => _ = InvokeAsync(() => OnLabelReport(report));
        keyDown = new ConsumerHandler("onkeydown");
        keyDown.Handler = CreateHandler<KeyboardEventArgs>(keyDown.Name, keyDown.RunAsync);
        input = new ConsumerHandler("oninput");
        input.Handler = CreateHandler<ChangeEventArgs>(input.Name, input.RunAsync);
        paste = new ConsumerHandler("onpaste");
        paste.Handler = CreateHandler<ClipboardEventArgs>(paste.Name, paste.RunAsync);
        mouseDown = new ConsumerHandler("onmousedown");
        mouseDown.Handler = CreateHandler<MouseEventArgs>(mouseDown.Name, mouseDown.RunAsync);
        focus = new ConsumerHandler("onfocus");
        focus.Handler = CreateHandler<FocusEventArgs>(focus.Name, focus.RunAsync);
        blur = new ConsumerHandler("onblur");
        blur.Handler = CreateHandler<FocusEventArgs>(blur.Name, blur.RunAsync);
    }

    [Inject]
    private IJSRuntime JSRuntime { get; set; } = null!;

    [Inject]
    private ILogger<OtpFieldInput> Logger { get; set; } = null!;

    [Inject]
    private OtpFieldDevelopmentLog DevelopmentLog { get; set; } = null!;

    [CascadingParameter]
    private OtpFieldRootContext? RootContext { get; set; }

    /// <inheritdoc />
    protected override string DefaultTagName => DefaultTag;

    /// <inheritdoc />
    protected override OtpFieldInputState State
    {
        get
        {
            // OTP-STA-11: the root's state with the slot's character, filled and index, rebuilt only when either changed.
            int index = Index;
            if (state is null || stateVersion != context!.StateVersion || stateIndex != index)
            {
                OtpFieldRootState root = context!.State;
                stateVersion = context.StateVersion;
                stateIndex = index;
                string slot = SlotCharacter(root.Value, index);
                state = new OtpFieldInputState(slot, root.Length, root.Disabled, root.Touched, root.Dirty, root.Valid, slot.Length > 0, root.Focused, root.Complete, root.ReadOnly, root.Required, index);
            }

            return state;
        }
    }

    /// <inheritdoc />
    protected override StateAttributesMapping<OtpFieldInputState>? StateAttributesMapping => s_mapping;

    /// <summary>Gets the slot's index among the root's slots.</summary>
    internal int Index => listItem?.Index ?? -1;

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">The input is not placed within an <see cref="OtpFieldRoot"/>.</exception>
    protected override void OnInitialized()
    {
        context = OtpFieldRootContext.Require(RootContext);
        context.AddListener(stateChanged);
        context.Root.SetSlotMounted(true);
        context.Root.RegisterSlot(this);
        listItem = new CompositeListItemBehavior(context.Root.Slots, indexChanged, guess: true);
        base.OnInitialized();
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        // OTP-STR-03, OTP-DOM-07: the guess comes first, since the slot's registered id derives from its index.
        int index = listItem!.EnsureGuess();
        listItem.SetRegistration(context!.Root.SlotId(index));
        bool hadPoints = HasConsumer(OtpFieldTwin.KeyPoint) || HasConsumer(OtpFieldTwin.PressPoint) || HasConsumer(OtpFieldTwin.FocusPoint) || HasConsumer(OtpFieldTwin.BlurPoint);
        foreach (ConsumerHandler handler in (ReadOnlySpan<ConsumerHandler>)[keyDown, input, paste, mouseDown, focus, blur])
        {
            handler.Consumer = AdditionalAttributes is not null && AdditionalAttributes.TryGetValue(handler.Name, out object? supplied)
                ? AttributeValue.FromObject(supplied)
                : default;
        }

        bool hasPoints = HasConsumer(OtpFieldTwin.KeyPoint) || HasConsumer(OtpFieldTwin.PressPoint) || HasConsumer(OtpFieldTwin.FocusPoint) || HasConsumer(OtpFieldTwin.BlurPoint);
        if (hadPoints || hasPoints)
        {
            context.Root.Refresh();
        }
    }

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        await listItem!.AfterRenderAsync(Element);
        await SyncLabelHandleAsync();
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing && context is not null)
        {
            context.RemoveListener(stateChanged);
            context.Root.SetSlotMounted(false);
            context.Root.ReleaseSlot(this);
            context.Root.UnregisterSlot(this);
            listItem?.Dispose();
            if (labelTwin is not null)
            {
                labelTwin.ControlReported -= labelReported;
                _ = labelTwin.DisposeAsync().AsTask();
                labelTwin = null;
                labelHandle = null;
            }
        }

        base.Dispose(disposing);
    }

    /// <inheritdoc />
    protected override void AddPartAttributes(AttributeList attributes)
    {
        OtpFieldRoot root = context!.Root;
        OtpFieldRootState rootState = context.State;
        int index = Index;
        int length = root.Length;
        attributes.Set("id", root.SlotId(index));
        // The handover at attach (OTP-DOM-08): the first value is rendered, then the module writes it.
        if (!root.ModuleOwnsSlots)
        {
            renderedValue = SlotCharacter(rootState.Value, index);
            renderedTabIndex = root.ActiveIndex == index ? "0" : "-1";
        }

        attributes.Set("value", renderedValue);
        attributes.Set("type", root.Mask ? "password" : "text");
        attributes.Set("inputmode", root.InputModeLiteral);
        attributes.Set("autocomplete", index == 0 ? root.AutoComplete : "off");
        attributes.Set("autocorrect", "off");
        attributes.Set("spellcheck", "false");
        attributes.Set("enterkeyhint", index == length - 1 ? "done" : "next");
        if (index == 0)
        {
            attributes.Set("maxlength", length.ToString(CultureInfo.InvariantCulture));
        }

        attributes.Set("tabindex", renderedTabIndex);
        attributes.Set("disabled", rootState.Disabled);
        attributes.Set("form", root.Form);
        attributes.Set("pattern", root.SlotPattern);
        attributes.Set("readonly", rootState.ReadOnly);
        attributes.Set("required", rootState.Required);
        foreach (ConsumerHandler handler in (ReadOnlySpan<ConsumerHandler>)[keyDown, input, paste, mouseDown, focus, blur])
        {
            if (handler.Consumer.IsHandler)
            {
                attributes.Set(handler.Name, handler.Handler);
            }
        }
    }

    /// <inheritdoc />
    protected override bool ForwardsConsumerHandler(string eventName) => !IsDeclined(eventName);

    // OTP-API-21/22, OTP-ARIA-06..09 (OTPFieldInput.tsx:81-83,161-163): the consumer's aria-label applies from slot 1 on; a
    // slot with an aria-label renders no aria-labelledby; otherwise the consumer's aria-labelledby, else the inherited label.
    // aria-invalid follows the field's app-controlled flag while enabled.
    /// <inheritdoc />
    protected override void FinishAttributes(AttributeList attributes)
    {
        OtpFieldRoot root = context!.Root;
        string? ariaLabel = Index == 0 ? null : ConsumerAttribute(AriaLabel);
        if (ariaLabel is null)
        {
            attributes.Remove(AriaLabel);
            string? labelledBy = ConsumerAttribute(AriaLabelledBy) ?? root.InheritedLabel;
            if (labelledBy is null)
            {
                attributes.Remove(AriaLabelledBy);
            }
            else
            {
                attributes.Set(AriaLabelledBy, labelledBy);
            }
        }
        else
        {
            attributes.Set(AriaLabel, ariaLabel);
            attributes.Remove(AriaLabelledBy);
        }

        if (root.Field.SlotsInvalid)
        {
            attributes.Set("aria-invalid", "true");
        }
        else
        {
            attributes.Remove("aria-invalid");
        }
    }

    /// <summary>Gets a value indicating whether the consumer passed the handler a decision point runs.</summary>
    /// <param name="point">The decision point: <c>key</c>, <c>press</c>, <c>focus</c> or <c>blur</c>.</param>
    /// <returns><see langword="true"/> when the slot has the consumer's handler for that point.</returns>
    internal bool HasConsumer(string point) => point switch
    {
        OtpFieldTwin.KeyPoint => keyDown.Consumer.IsHandler,
        OtpFieldTwin.PressPoint => mouseDown.Consumer.IsHandler,
        OtpFieldTwin.FocusPoint => focus.Consumer.IsHandler,
        OtpFieldTwin.BlurPoint => blur.Consumer.IsHandler,
        _ => false,
    };

    // On WebAssembly, inside the root's decision: the consumer's handler for the event in flight, by the snapshot's type.
    /// <summary>Runs the consumer's handler for the event a decision point is asked about.</summary>
    /// <param name="snapshot">The event, as JavaScript saw it.</param>
    /// <returns><see cref="DecisionPolicy.Stop"/> when the consumer called <c>PreventPartHandler()</c>; otherwise <see cref="DecisionPolicy.Run"/>.</returns>
    internal DecisionPolicy DecideConsumer(EventSnapshot snapshot)
    {
        ConsumerHandler? handler = snapshot.Type switch
        {
            "keydown" => keyDown,
            "input" or "compositionend" => input,
            "paste" => paste,
            "mousedown" => mouseDown,
            "focusin" or "focus" => focus,
            "focusout" or "blur" => blur,
            _ => null,
        };
        if (handler is null || !handler.Consumer.IsHandler)
        {
            return DecisionPolicy.Run;
        }

        if (handler.Ran)
        {
            handler.Ran = false;
            return handler.RanPrevented ? DecisionPolicy.Stop : DecisionPolicy.Run;
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
            "mousedown" => new MouseEventArgs { Type = snapshot.Type, Detail = snapshot.Detail, Buttons = snapshot.Buttons },
            "input" or "compositionend" => new ChangeEventArgs(),
            _ => new FocusEventArgs { Type = snapshot.Type },
        };
        handler.Decided = true;
        try
        {
            Task task = handler.Consumer.InvokeAsync(args);
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

    private static bool IsDeclined(string eventName) =>
        eventName.Equals("onkeydown", StringComparison.OrdinalIgnoreCase)
        || eventName.Equals("oninput", StringComparison.OrdinalIgnoreCase)
        || eventName.Equals("onpaste", StringComparison.OrdinalIgnoreCase)
        || eventName.Equals("onmousedown", StringComparison.OrdinalIgnoreCase)
        || eventName.Equals("onfocus", StringComparison.OrdinalIgnoreCase)
        || eventName.Equals("onblur", StringComparison.OrdinalIgnoreCase);

    // OTP-STA-11, OTP-EDGE-12: indexed by UTF-16 code unit, as `value[index] ?? ''` is.
    private static string SlotCharacter(string value, int index) =>
        index >= 0 && index < value.Length ? value.Substring(index, 1) : string.Empty;

    private string? ConsumerAttribute(string name) =>
        AdditionalAttributes is not null && AdditionalAttributes.TryGetValue(name, out object? supplied) && supplied is string text ? text : null;

    // The registered id derives from the index, so a corrected index registers the new id before the slot renders it.
    private void OnIndexChanged()
    {
        listItem!.SetRegistration(context!.Root.SlotId(listItem.Index));
        StateHasChanged();
    }

    // OTP-ARIA-04 (useAriaLabelledBy's fallback, FFI-ALG-08): while slot 0's label may name the group, the `form` module looks
    // for the slot's native label and reports its id.
    private async Task SyncLabelHandleAsync()
    {
        OtpFieldRoot root = context!.Root;
        bool fallback = Index == 0 && root.Field.FieldLabelId is null && !HasRootLabelledBy(root);
        if (Element is not { } element || (labelHandle is null && !fallback))
        {
            return;
        }

        if (labelHandle is null)
        {
            labelTwin = new FormTwin(JSRuntime);
            labelTwin.ControlReported += labelReported;
            sentLabelFallback = fallback;
            labelHandle = await labelTwin.AttachAsync(element, LabelOptions(fallback));
            return;
        }

        if (sentLabelFallback != fallback)
        {
            sentLabelFallback = fallback;
            await labelHandle.UpdateAsync(LabelOptions(fallback));
            if (!fallback)
            {
                root.SetLabelFallback(null);
            }
        }
    }

    private static bool HasRootLabelledBy(OtpFieldRoot root) =>
        root.AdditionalAttributes is not null && root.AdditionalAttributes.ContainsKey(AriaLabelledBy);

    private static FormOptions LabelOptions(bool fallback) => new()
    {
        Control = new FormControlOptions { ReportEvents = false, LabelFallback = fallback },
    };

    private void OnLabelReport(FormControlReport report)
    {
        if (!string.Equals(report.Type, LabelReport, StringComparison.Ordinal) || context is null)
        {
            return;
        }

        context.Root.SetLabelFallback(report.Value);

        // OTP-EDGE-04 (OTPFieldInput.tsx:85-99): slot 0 ignores the consumer's aria-label; without an associated label the
        // field has no name, which the development warning says once.
        if (report.Value is null && Index == 0 && ConsumerAttribute(AriaLabel) is not null)
        {
            DevelopmentLog.Report(Logger, "Blazix: `<OtpFieldInput>` ignores `aria-label` on the first input. Use a `<label>` or `<FieldLabel>` to label the one-time code field.");
        }
    }

    // A declined consumer handler with its one-shot marker: Blazor's dispatch runs first and leaves its verdict for the
    // decision of the same event; a handler the decision ran is not run again by Blazor's dispatch.
    private sealed class ConsumerHandler(string name)
    {
        public string Name { get; } = name;

        public AttributeValue Consumer { get; set; }

        public EventCallback Handler { get; set; }

        public bool Decided { get; set; }

        public bool Ran { get; set; }

        public bool RanPrevented { get; set; }

        public Task RunAsync(EventArgs e)
        {
            bool pending = Decided;
            Decided = false;
            if (!Consumer.IsHandler || pending)
            {
                return Task.CompletedTask;
            }

            Task task = Consumer.InvokeAsync(e);
            Ran = true;
            RanPrevented = e.PartHandlerPrevented();
            return task;
        }
    }
}

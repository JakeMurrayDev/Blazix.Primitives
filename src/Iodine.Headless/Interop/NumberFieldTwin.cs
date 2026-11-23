using Microsoft.JSInterop;

namespace Iodine.Headless.Interop;

// ADR 04 §1 (the `number-field` component module) and §3 (decision points; Amendments 2026-11-18, the names chosen by the
// user 2026-11-18: `input-focus` and `input-blur`, fallback run); issue 277, design points 1 (a), 2 (b) and 7 (a), settled
// by the orchestrator 2026-11-18. One twin per part: NumberFieldInput's. 6a: the value handover and the focus and blur
// reports; 6b adds the gate, paste, wheel and scrub reports and the other points.
/// <summary>
/// The C# half of the <c>number-field</c> module: the visible input's text and the reports of its focus and blur, attached to
/// the input a <see cref="NumberFieldInput"/> renders.
/// </summary>
/// <param name="jsRuntime">The runtime of the part's circuit or WebAssembly application.</param>
internal sealed class NumberFieldTwin(IJSRuntime jsRuntime)
    : TwinBase<NumberFieldTwin, NumberFieldOptions>(jsRuntime, LibraryModule("number-field"), IodineJsonContext.Default.NumberFieldOptions)
{
    /// <summary>Represents the name of the decision point the module asks about before it reports a focus.</summary>
    public const string InputFocusPoint = "input-focus";

    /// <summary>Represents the name of the decision point the module asks about before it reports a blur.</summary>
    public const string InputBlurPoint = "input-blur";

    /// <summary>Represents the name of the decision point the module asks about before a keydown steps or is blocked.</summary>
    public const string InputKeyPoint = "input-key";

    /// <summary>Represents the name of the decision point the module asks about before it prevents a paste.</summary>
    public const string InputPastePoint = "input-paste";

    /// <summary>Represents the name of the decision point the module asks about before it reports an edit.</summary>
    public const string InputChangePoint = "input-change";

    /// <summary>Occurs when the visible input gains focus and neither a listener nor a decision vetoed it.</summary>
    public event Action<EventSnapshot>? InputFocused;

    /// <summary>Occurs when the visible input loses focus and neither a listener nor a decision vetoed it.</summary>
    public event Action<NumberFieldBlurReport>? InputBlurred;

    /// <summary>Occurs when a step key (ArrowUp, ArrowDown, or Home or End with its bound) was pressed in the input.</summary>
    public event Action<NumberFieldKeyStepReport>? KeyStepped;

    /// <summary>Occurs when an edit left the input with a text whose characters are all allowed.</summary>
    public event Action<NumberFieldChangeReport>? InputChanged;

    /// <summary>Occurs when a paste was prevented and its text spliced into the display text.</summary>
    public event Action<NumberFieldPasteReport>? InputPasted;

    /// <summary>Occurs when the wheel stepped the focused input.</summary>
    public event Action<NumberFieldWheelReport>? WheelStepped;

    /// <summary>Occurs when the browser autofilled the hidden input.</summary>
    public event Action<NumberFieldAutofillReport>? Autofilled;

    /// <summary>Gets or sets the C# handler chain of the <c>input-focus</c> point.</summary>
    /// <value>A function that returns <see cref="DecisionPolicy.Stop"/> when a consumer vetoed the focus; <see langword="null"/> runs.</value>
    public Func<EventSnapshot, DecisionPolicy>? FocusDecider { get; set; }

    /// <summary>Gets or sets the C# handler chain of the <c>input-blur</c> point.</summary>
    /// <value>A function that returns <see cref="DecisionPolicy.Stop"/> when a consumer vetoed the blur; <see langword="null"/> runs.</value>
    public Func<EventSnapshot, DecisionPolicy>? BlurDecider { get; set; }

    /// <summary>Gets or sets the C# handler chain of the <c>input-key</c> point.</summary>
    /// <value>A function that returns <see cref="DecisionPolicy.Stop"/> when a consumer vetoed the keydown; <see langword="null"/> runs.</value>
    public Func<EventSnapshot, DecisionPolicy>? KeyDecider { get; set; }

    /// <summary>Gets or sets the C# handler chain of the <c>input-paste</c> point.</summary>
    /// <value>A function that returns <see cref="DecisionPolicy.Stop"/> when a consumer vetoed the paste; <see langword="null"/> runs.</value>
    public Func<EventSnapshot, DecisionPolicy>? PasteDecider { get; set; }

    /// <summary>Gets or sets the C# handler chain of the <c>input-change</c> point.</summary>
    /// <value>A function that returns <see cref="DecisionPolicy.Stop"/> when a consumer vetoed the edit; <see langword="null"/> runs.</value>
    public Func<EventSnapshot, DecisionPolicy>? ChangeDecider { get; set; }

    /// <summary>Receives a focus of the visible input.</summary>
    /// <param name="payloadJson">The JSON of a <see cref="NumberFieldFocusReport"/>.</param>
    /// <exception cref="System.Text.Json.JsonException">The payload is not valid.</exception>
    [JSInvokable]
    public void OnInputFocus(string payloadJson)
    {
        NumberFieldFocusReport report = Deserialize(payloadJson, IodineJsonContext.Default.NumberFieldFocusReport);
        InputFocused?.Invoke(report.Event);
    }

    /// <summary>Receives a blur of the visible input.</summary>
    /// <param name="payloadJson">The JSON of a <see cref="NumberFieldBlurReport"/>.</param>
    /// <exception cref="System.Text.Json.JsonException">The payload is not valid.</exception>
    [JSInvokable]
    public void OnInputBlur(string payloadJson)
    {
        NumberFieldBlurReport report = Deserialize(payloadJson, IodineJsonContext.Default.NumberFieldBlurReport);
        InputBlurred?.Invoke(report);
    }

    /// <summary>Receives a step key pressed in the visible input.</summary>
    /// <param name="payloadJson">The JSON of a <see cref="NumberFieldKeyStepReport"/>.</param>
    /// <exception cref="System.Text.Json.JsonException">The payload is not valid.</exception>
    [JSInvokable]
    public void OnKeyStep(string payloadJson) => KeyStepped?.Invoke(Deserialize(payloadJson, IodineJsonContext.Default.NumberFieldKeyStepReport));

    /// <summary>Receives an allowed edit of the visible input.</summary>
    /// <param name="payloadJson">The JSON of a <see cref="NumberFieldChangeReport"/>.</param>
    /// <exception cref="System.Text.Json.JsonException">The payload is not valid.</exception>
    [JSInvokable]
    public void OnInputChange(string payloadJson) => InputChanged?.Invoke(Deserialize(payloadJson, IodineJsonContext.Default.NumberFieldChangeReport));

    /// <summary>Receives a paste into the visible input.</summary>
    /// <param name="payloadJson">The JSON of a <see cref="NumberFieldPasteReport"/>.</param>
    /// <exception cref="System.Text.Json.JsonException">The payload is not valid.</exception>
    [JSInvokable]
    public void OnInputPaste(string payloadJson) => InputPasted?.Invoke(Deserialize(payloadJson, IodineJsonContext.Default.NumberFieldPasteReport));

    /// <summary>Receives a wheel step of the focused input.</summary>
    /// <param name="payloadJson">The JSON of a <see cref="NumberFieldWheelReport"/>.</param>
    /// <exception cref="System.Text.Json.JsonException">The payload is not valid.</exception>
    [JSInvokable]
    public void OnWheelStep(string payloadJson) => WheelStepped?.Invoke(Deserialize(payloadJson, IodineJsonContext.Default.NumberFieldWheelReport));

    /// <summary>Receives a browser autofill of the hidden input.</summary>
    /// <param name="payloadJson">The JSON of a <see cref="NumberFieldAutofillReport"/>.</param>
    /// <exception cref="System.Text.Json.JsonException">The payload is not valid.</exception>
    [JSInvokable]
    public void OnAutofill(string payloadJson) => Autofilled?.Invoke(Deserialize(payloadJson, IodineJsonContext.Default.NumberFieldAutofillReport));

    /// <inheritdoc />
    protected override DecisionPolicy OnDecide(string point, EventSnapshot snapshot)
    {
        Func<EventSnapshot, DecisionPolicy>? decider = point switch
        {
            InputFocusPoint => FocusDecider,
            InputBlurPoint => BlurDecider,
            InputKeyPoint => KeyDecider,
            InputPastePoint => PasteDecider,
            InputChangePoint => ChangeDecider,
            _ => null,
        };
        return decider is null ? DecisionPolicy.Run : decider(snapshot);
    }
}

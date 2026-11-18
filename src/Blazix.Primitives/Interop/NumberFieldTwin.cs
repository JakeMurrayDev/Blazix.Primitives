using Microsoft.JSInterop;

namespace Blazix.Primitives.Interop;

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
    : TwinBase<NumberFieldTwin, NumberFieldOptions>(jsRuntime, LibraryModule("number-field"), BlazixJsonContext.Default.NumberFieldOptions)
{
    /// <summary>Represents the name of the decision point the module asks about before it reports a focus.</summary>
    public const string InputFocusPoint = "input-focus";

    /// <summary>Represents the name of the decision point the module asks about before it reports a blur.</summary>
    public const string InputBlurPoint = "input-blur";

    /// <summary>Occurs when the visible input gains focus and neither a listener nor a decision vetoed it.</summary>
    public event Action<EventSnapshot>? InputFocused;

    /// <summary>Occurs when the visible input loses focus and neither a listener nor a decision vetoed it.</summary>
    public event Action<NumberFieldBlurReport>? InputBlurred;

    /// <summary>Gets or sets the C# handler chain of the <c>input-focus</c> point.</summary>
    /// <value>A function that returns <see cref="DecisionPolicy.Stop"/> when a consumer vetoed the focus; <see langword="null"/> runs.</value>
    public Func<EventSnapshot, DecisionPolicy>? FocusDecider { get; set; }

    /// <summary>Gets or sets the C# handler chain of the <c>input-blur</c> point.</summary>
    /// <value>A function that returns <see cref="DecisionPolicy.Stop"/> when a consumer vetoed the blur; <see langword="null"/> runs.</value>
    public Func<EventSnapshot, DecisionPolicy>? BlurDecider { get; set; }

    /// <summary>Receives a focus of the visible input.</summary>
    /// <param name="payloadJson">The JSON of a <see cref="NumberFieldFocusReport"/>.</param>
    /// <exception cref="System.Text.Json.JsonException">The payload is not valid.</exception>
    [JSInvokable]
    public void OnInputFocus(string payloadJson)
    {
        NumberFieldFocusReport report = Deserialize(payloadJson, BlazixJsonContext.Default.NumberFieldFocusReport);
        InputFocused?.Invoke(report.Event);
    }

    /// <summary>Receives a blur of the visible input.</summary>
    /// <param name="payloadJson">The JSON of a <see cref="NumberFieldBlurReport"/>.</param>
    /// <exception cref="System.Text.Json.JsonException">The payload is not valid.</exception>
    [JSInvokable]
    public void OnInputBlur(string payloadJson)
    {
        NumberFieldBlurReport report = Deserialize(payloadJson, BlazixJsonContext.Default.NumberFieldBlurReport);
        InputBlurred?.Invoke(report);
    }

    /// <inheritdoc />
    protected override DecisionPolicy OnDecide(string point, EventSnapshot snapshot)
    {
        Func<EventSnapshot, DecisionPolicy>? decider = point switch
        {
            InputFocusPoint => FocusDecider,
            InputBlurPoint => BlurDecider,
            _ => null,
        };
        return decider is null ? DecisionPolicy.Run : decider(snapshot);
    }
}

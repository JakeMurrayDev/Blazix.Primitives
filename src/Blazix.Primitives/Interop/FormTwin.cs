using Microsoft.JSInterop;

namespace Blazix.Primitives.Interop;

// ADR 04 §1 (form row, Amendments 2026-10-18), §3 (one message per event; the decision point `label`, fallback run, for a
// consumer's C# pointer handler on a label: ADR 01's "C#-origin consumer vetoes" row); ADR 08 §3 (`<Module>Twin`).
// FFI-ALG-02/04/08/10/16, FFI-FRM-09/10, FFI-PTR-01..04, FFI-FOC-05, FFI-STA-09. One twin per part.
/// <summary>
/// The C# half of the <c>form</c> module: a control's events with its value and native validity, its custom validity and
/// its value as the field and the consumer decide them, and a field label's pointer behavior.
/// </summary>
/// <param name="jsRuntime">The runtime of the part's circuit or WebAssembly application.</param>
internal sealed class FormTwin(IJSRuntime jsRuntime)
    : TwinBase<FormTwin, FormOptions>(jsRuntime, LibraryModule("form"), BlazixJsonContext.Default.FormOptions)
{
    /// <summary>Represents the name of the decision point a label's pointer handling asks about.</summary>
    public const string LabelPoint = "label";

    /// <summary>Occurs once per event of an attached control.</summary>
    public event Action<FormControlReport>? ControlReported;

    /// <summary>Occurs when a label's element contradicts its declared mode.</summary>
    /// <remarks>The argument is <see langword="true"/> when a native label's element is not a <c>label</c>.</remarks>
    public event Action<bool>? LabelMismatched;

    /// <summary>Gets or sets the handler of a form element's submissions.</summary>
    /// <value>
    /// A function that runs the submission pipeline and returns the module's next step; <see langword="null"/> answers
    /// <see cref="FormSubmitAnswer.HandledVerdict"/>.
    /// </value>
    public Func<FormSubmitRequest, Task<FormSubmitAnswer>>? SubmitHandler { get; set; }

    /// <summary>Gets or sets the C# handler chain of the label decision point.</summary>
    /// <value>
    /// A function that runs the consumer's pointer handler for the event and returns <see cref="DecisionPolicy.Stop"/>
    /// when it called <c>PreventPartHandler()</c>; <see langword="null"/> always lets the label proceed. It runs only on
    /// WebAssembly, synchronously inside the dispatch, and must not await.
    /// </value>
    public Func<EventSnapshot, DecisionPolicy>? LabelDecider { get; set; }

    // FRM-EVT-01 (design point F1 of M2 track 2: an invocation answered with a result, the OnRevealRequest precedent of ADR
    // 04 Amendments 2026-10-21). The answer is JSON from the source-generated context, so nothing is serialized by reflection.
    /// <summary>Receives a native submission of an attached form element, which the module prevented.</summary>
    /// <param name="payloadJson">The JSON of a <see cref="FormSubmitRequest"/>.</param>
    /// <returns>The JSON of the <see cref="FormSubmitAnswer"/>.</returns>
    /// <exception cref="System.Text.Json.JsonException">The payload is not valid.</exception>
    [JSInvokable]
    public async Task<string> OnSubmitRequest(string payloadJson)
    {
        FormSubmitRequest request = Deserialize(payloadJson, BlazixJsonContext.Default.FormSubmitRequest);
        FormSubmitAnswer answer = SubmitHandler is { } handler ? await handler(request).ConfigureAwait(true) : new FormSubmitAnswer();
        return System.Text.Json.JsonSerializer.Serialize(answer, BlazixJsonContext.Default.FormSubmitAnswer);
    }

    /// <summary>Receives one event of an attached control.</summary>
    /// <param name="payloadJson">The JSON of a <see cref="FormControlReport"/>.</param>
    /// <exception cref="System.Text.Json.JsonException">The payload is not valid.</exception>
    [JSInvokable]
    public void OnControlReport(string payloadJson)
    {
        FormControlReport report = Deserialize(payloadJson, BlazixJsonContext.Default.FormControlReport);
        ControlReported?.Invoke(report);
    }

    /// <summary>Receives the report of a label element that contradicts its declared mode.</summary>
    /// <param name="payloadJson">The JSON of a <see cref="LabelMismatch"/>.</param>
    /// <exception cref="System.Text.Json.JsonException">The payload is not valid.</exception>
    [JSInvokable]
    public void OnLabelMismatch(string payloadJson)
    {
        LabelMismatch mismatch = Deserialize(payloadJson, BlazixJsonContext.Default.LabelMismatch);
        LabelMismatched?.Invoke(mismatch.ExpectedNative);
    }

    /// <inheritdoc />
    /// <remarks>The label point runs <see cref="LabelDecider"/>; every other point answers <see cref="DecisionPolicy.Run"/>.</remarks>
    protected override DecisionPolicy OnDecide(string point, EventSnapshot snapshot)
    {
        return string.Equals(point, LabelPoint, StringComparison.Ordinal) && LabelDecider is { } decider ? decider(snapshot) : DecisionPolicy.Run;
    }
}

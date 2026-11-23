using Microsoft.JSInterop;

namespace Blazix.Primitives.Interop;

// ADR 04 §1 (the `slider` component module), §2 (the Slider.Thumb and Slider.Indicator row), §3 (the two decision points of
// Slider) and Amendments 2026-10-18 (placement; the handover), 2026-10-20 (the blur classification) and 2026-11-18 (the
// bands, the revision rule). The module owns the drag loop, the thumb keys and the seven value algorithms (SLD §4.1), the
// thumbs' and indicators' whole style and the inputs' value after attach; C# owns the values, runs every request through
// OnValueChange and commits. One twin on the Root (design point 1 (a') of track 4). ADR 01's relaxation table: the two
// vetoes (SLD-PTR-01/16, SLD-KEY-01, SLD-EVT-09), RND-STA-01 for the values C# renders from reports.
/// <summary>
/// The C# half of the <c>slider</c> module: the pointer and touch gestures on the control, the keys and native changes of the
/// thumb inputs, and the positions of the thumbs and indicators.
/// </summary>
/// <param name="jsRuntime">The runtime of the part's circuit or WebAssembly application.</param>
internal sealed class SliderTwin(IJSRuntime jsRuntime)
    : TwinBase<SliderTwin, SliderOptions>(jsRuntime, LibraryModule("slider"), BlazixJsonContext.Default.SliderOptions)
{
    // ADR 04 §3: the thumb `keydown`, fallback run (SLD-KEY-01, SLD-API-22, SLD-EVT-09).
    /// <summary>Represents the name of the decision point the module asks before it handles a key on a thumb input.</summary>
    public const string KeyPoint = "key";

    // ADR 04 §3: the control `pointerdown`, fallback start (SLD-PTR-01, SLD-PTR-16, SLD-EVT-09).
    /// <summary>Represents the name of the decision point the module asks before a press on the control starts a gesture.</summary>
    public const string PressPoint = "press";

    /// <summary>Occurs when the module reports a candidate value it applied optimistically.</summary>
    public event Action<SliderValueRequest>? ValueRequested;

    /// <summary>Occurs when a pointer or touch gesture starts, shows as a drag, ends or is dropped.</summary>
    public event Action<SliderGestureReport>? GestureChanged;

    /// <summary>Occurs when a thumb input receives or loses focus, outside the module's own focus-visible restoration.</summary>
    public event Action<SliderFocusReport>? FocusChanged;

    /// <summary>Occurs when a key reaches a thumb input and no C# chain ran inside the dispatch.</summary>
    public event Action<SliderKeyReport>? KeyReported;

    /// <summary>Gets or sets the C# chain of the key decision point.</summary>
    /// <value>
    /// A function that receives the <c>keydown</c>, runs the consumer's handler of the thumb whose input the event's target
    /// names, and returns <see cref="DecisionPolicy.Stop"/> when the consumer called <c>PreventPartHandler()</c>;
    /// <see langword="null"/> always lets the module handle the key. It runs only on WebAssembly, synchronously inside the
    /// dispatch, and must not await.
    /// </value>
    public Func<EventSnapshot, DecisionPolicy>? KeyDecider { get; set; }

    /// <summary>Gets or sets the C# chain of the press decision point.</summary>
    /// <value>
    /// A function that receives the control's <c>pointerdown</c>, runs the consumer's handler and returns
    /// <see cref="DecisionPolicy.Stop"/> when the consumer called <c>PreventPartHandler()</c>; <see langword="null"/> always
    /// starts the gesture. It runs only on WebAssembly, synchronously inside the dispatch, and must not await.
    /// </value>
    public Func<EventSnapshot, DecisionPolicy>? PressDecider { get; set; }

    /// <summary>Receives a candidate value the module applied optimistically.</summary>
    /// <param name="payloadJson">The JSON of a <see cref="SliderValueRequest"/>.</param>
    /// <exception cref="System.Text.Json.JsonException">The payload is not valid.</exception>
    [JSInvokable]
    public void OnValueRequest(string payloadJson)
    {
        SliderValueRequest request = Deserialize(payloadJson, BlazixJsonContext.Default.SliderValueRequest);
        ValueRequested?.Invoke(request);
    }

    /// <summary>Receives a change of the pointer or touch gesture.</summary>
    /// <param name="payloadJson">The JSON of a <see cref="SliderGestureReport"/>.</param>
    /// <exception cref="System.Text.Json.JsonException">The payload is not valid.</exception>
    [JSInvokable]
    public void OnGesture(string payloadJson)
    {
        SliderGestureReport report = Deserialize(payloadJson, BlazixJsonContext.Default.SliderGestureReport);
        GestureChanged?.Invoke(report);
    }

    /// <summary>Receives a thumb input's focus or blur.</summary>
    /// <param name="payloadJson">The JSON of a <see cref="SliderFocusReport"/>.</param>
    /// <exception cref="System.Text.Json.JsonException">The payload is not valid.</exception>
    [JSInvokable]
    public void OnFocusChange(string payloadJson)
    {
        SliderFocusReport report = Deserialize(payloadJson, BlazixJsonContext.Default.SliderFocusReport);
        FocusChanged?.Invoke(report);
    }

    /// <summary>Receives a key that reached a thumb input while no C# chain ran inside the dispatch.</summary>
    /// <param name="payloadJson">The JSON of a <see cref="SliderKeyReport"/>.</param>
    /// <exception cref="System.Text.Json.JsonException">The payload is not valid.</exception>
    [JSInvokable]
    public void OnKeyDown(string payloadJson)
    {
        SliderKeyReport report = Deserialize(payloadJson, BlazixJsonContext.Default.SliderKeyReport);
        KeyReported?.Invoke(report);
    }

    /// <inheritdoc />
    /// <remarks>
    /// The module has two decision points: <see cref="KeyPoint"/>, answered by <see cref="KeyDecider"/>, and
    /// <see cref="PressPoint"/>, answered by <see cref="PressDecider"/>. Any other point, or a point without a decider,
    /// answers <see cref="DecisionPolicy.Run"/>.
    /// </remarks>
    protected override DecisionPolicy OnDecide(string point, EventSnapshot snapshot)
    {
        if (string.Equals(point, KeyPoint, StringComparison.Ordinal))
        {
            return KeyDecider is { } keyDecider ? keyDecider(snapshot) : DecisionPolicy.Run;
        }

        if (string.Equals(point, PressPoint, StringComparison.Ordinal))
        {
            return PressDecider is { } pressDecider ? pressDecider(snapshot) : DecisionPolicy.Run;
        }

        return DecisionPolicy.Run;
    }
}

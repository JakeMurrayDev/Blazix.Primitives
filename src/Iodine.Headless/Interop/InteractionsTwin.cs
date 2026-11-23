using Microsoft.JSInterop;

namespace Blazix.Primitives.Interop;

// ADR 04 §1 (the interactions row), §3 (decision points) and Amendments 2026-10-27 (decision 3: the token form, the
// session, press and Prevent at escape; chosen by the user 2026-10-27). One twin per attaching part (Interop/CONTEXT.md, a
// part owns its twin): a popup root's session (token form), a trigger, the floating element, a tooltip provider (token
// form). ADR 01 relaxation table: "C#-origin consumer vetoes" (press), "EVD-EVT-11, EVD-DIS-01, FLT-KEY-01" (escape),
// "EVD-EVT-18, PSH-ANI-03" (the hover timers' requests).
/// <summary>
/// The C# half of the <c>interactions</c> module: press-to-toggle, dismissal, focus-to-open, cursor anchoring, hover and
/// the tooltip delay group, attached to a popup's root (without an element), its triggers and its floating element.
/// </summary>
/// <param name="jsRuntime">The runtime of the part's circuit or WebAssembly application.</param>
internal sealed class InteractionsTwin(IJSRuntime jsRuntime)
    : TwinBase<InteractionsTwin, InteractionsOptions>(jsRuntime, LibraryModule("interactions"), BlazixJsonContext.Default.InteractionsOptions)
{
    /// <summary>Represents the name of the decision point a trigger asks about before it requests the open state a press computed.</summary>
    public const string PressPoint = "press";

    /// <summary>Represents the name of the decision point a popup's session asks about before it closes on Escape.</summary>
    public const string EscapePoint = "escape";

    private InteractionsPressing? pendingPress;

    /// <summary>Occurs when the module requests an open-state change it decided without a decision point.</summary>
    /// <remarks>The argument carries the requested state, the reason's literal, the event and the trigger's id.</remarks>
    public event Action<InteractionsRequest>? RequestReported;

    /// <summary>Occurs when a press was not decided inside its dispatch.</summary>
    /// <remarks>The argument carries the open state the press computed, the trigger's id, how it happened and the event.</remarks>
    public event Action<InteractionsPress>? PressReported;

    /// <summary>Gets or sets the C# chain of the press decision point.</summary>
    /// <value>
    /// A function that receives the click about to request the state named by <see cref="PendingPress"/>, runs the
    /// consumer's handler and then the request, and returns <see cref="DecisionPolicy.Stop"/> when the consumer called
    /// <c>PreventPartHandler()</c>; <see langword="null"/> lets every press. It runs only on WebAssembly, synchronously
    /// inside the dispatch, and must not await.
    /// </value>
    public Func<EventSnapshot, DecisionPolicy>? PressDecider { get; set; }

    /// <summary>Gets or sets the C# chain of the Escape decision point.</summary>
    /// <value>
    /// A function that receives the Escape <c>keydown</c>, runs the popup's open-change request with the reason
    /// <see cref="EventReason.EscapeKey"/>, and returns <see cref="DecisionPolicy.PreventAndStop"/> when the close was
    /// accepted, <see cref="DecisionPolicy.Prevent"/> when it was accepted and propagation allowed,
    /// <see cref="DecisionPolicy.Stop"/> when it was canceled, or <see cref="DecisionPolicy.Run"/> when it was canceled and
    /// propagation allowed; <see langword="null"/> answers <see cref="DecisionPolicy.PreventAndStop"/>. It runs only on
    /// WebAssembly, synchronously inside the dispatch, and must not await.
    /// </value>
    public Func<EventSnapshot, DecisionPolicy>? EscapeDecider { get; set; }

    /// <summary>Gets the press the module handed over for the press decision in progress.</summary>
    /// <value>The press, or <see langword="null"/> outside a press decision.</value>
    public InteractionsPressing? PendingPress => pendingPress;

    /// <summary>Receives an open-state change the module requests.</summary>
    /// <param name="payloadJson">The JSON of an <see cref="InteractionsRequest"/>.</param>
    /// <exception cref="System.Text.Json.JsonException">The payload is not valid.</exception>
    [JSInvokable]
    public void OnRequest(string payloadJson)
    {
        InteractionsRequest request = Deserialize(payloadJson, BlazixJsonContext.Default.InteractionsRequest);
        RequestReported?.Invoke(request);
    }

    /// <summary>Receives a press that was not decided inside its dispatch.</summary>
    /// <param name="payloadJson">The JSON of an <see cref="InteractionsPress"/>.</param>
    /// <exception cref="System.Text.Json.JsonException">The payload is not valid.</exception>
    [JSInvokable]
    public void OnPress(string payloadJson)
    {
        InteractionsPress press = Deserialize(payloadJson, BlazixJsonContext.Default.InteractionsPress);
        PressReported?.Invoke(press);
    }

    /// <summary>Receives the press the next press decision is about, inside the dispatch, on WebAssembly only.</summary>
    /// <param name="payloadJson">The JSON of an <see cref="InteractionsPressing"/>.</param>
    /// <exception cref="System.Text.Json.JsonException">The payload is not valid.</exception>
    [JSInvokable]
    public void OnPressing(string payloadJson)
    {
        pendingPress = Deserialize(payloadJson, BlazixJsonContext.Default.InteractionsPressing);
    }

    /// <inheritdoc />
    /// <remarks>
    /// The module has two decision points. <see cref="PressPoint"/> is answered by <see cref="PressDecider"/> while a
    /// press was handed over, and the hand-over is consumed; without a decider or a hand-over it answers
    /// <see cref="DecisionPolicy.Run"/>. <see cref="EscapePoint"/> is answered by <see cref="EscapeDecider"/>, and without
    /// one by <see cref="DecisionPolicy.PreventAndStop"/>, the module's fallback. Any other point answers
    /// <see cref="DecisionPolicy.Run"/>.
    /// </remarks>
    protected override DecisionPolicy OnDecide(string point, EventSnapshot snapshot)
    {
        switch (point)
        {
            case PressPoint:
                try
                {
                    return pendingPress is not null && PressDecider is { } press ? press(snapshot) : DecisionPolicy.Run;
                }
                finally
                {
                    pendingPress = null;
                }

            case EscapePoint:
                return EscapeDecider is { } escape ? escape(snapshot) : DecisionPolicy.PreventAndStop;

            default:
                return DecisionPolicy.Run;
        }
    }
}

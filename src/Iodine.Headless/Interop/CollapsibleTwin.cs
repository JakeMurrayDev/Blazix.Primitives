using Microsoft.JSInterop;

namespace Blazix.Primitives.Interop;

// ADR 04 §1 (the collapsible component module, Amendments 2026-10-21), §2 (the panel's JS-owned attributes and style;
// the convergent hidden), §3 (one message per state change; the reveal decision point, fallback run, and its Server
// answer: Amendments 2026-10-21); ADR 01 relaxation table (TRN-ANI-03/09/10, the reveal row). One twin per part.
/// <summary>
/// The C# half of the <c>collapsible</c> module: the measurement passes and transition flips of a collapsible panel's
/// element. The module reports what the part's state needs from the element: the forced idle of a motion-skipping
/// open, the end of an exit, the detected animation type, a reveal by find-in-page, and, when asked, the panel's
/// transition status.
/// </summary>
/// <param name="jsRuntime">The runtime of the part's circuit or WebAssembly application.</param>
internal sealed class CollapsibleTwin(IJSRuntime jsRuntime)
    : TwinBase<CollapsibleTwin, CollapsibleOptions>(jsRuntime, LibraryModule("collapsible"), BlazixJsonContext.Default.CollapsibleOptions)
{
    /// <summary>Represents the name of the decision point the module asks about when <c>beforematch</c> reveals the panel.</summary>
    public const string RevealPoint = "reveal";

    /// <summary>Occurs when the panel starts or stops showing itself settled during a motion-skipping open.</summary>
    public event Action<bool>? ForcedIdleChanged;

    /// <summary>Occurs when the panel's exit has finished, once per exit.</summary>
    /// <remarks>The module has already hidden an element that is about to be removed or hidden.</remarks>
    public event Action? ExitCompleted;

    /// <summary>Occurs when the detected animation type changed, or when both a transition and an animation were found.</summary>
    public event Action<PanelAnimationTypeChange>? AnimationTypeChanged;

    /// <summary>Occurs when the panel's transition status changed, once per change, while <see cref="CollapsibleOptions.ReportStatus"/> is set.</summary>
    /// <remarks>The unset status is never reported: <see cref="ExitCompleted"/> implies it.</remarks>
    public event Action<TransitionStatus?>? StatusChanged;

    /// <summary>Gets or sets the C# handler chain of the reveal decision point.</summary>
    /// <value>
    /// A function that receives the <c>beforematch</c> event, requests the open, and returns
    /// <see cref="DecisionPolicy.Run"/> to let the reveal proceed or <see cref="DecisionPolicy.Stop"/> when a consumer
    /// canceled the request; <see langword="null"/> always lets it proceed. It runs only on WebAssembly, synchronously
    /// inside the dispatch, and must not await.
    /// </value>
    public Func<EventSnapshot, DecisionPolicy>? RevealDecider { get; set; }

    /// <summary>Gets or sets the C# handler of a reveal that no C# handler could decide inside the dispatch.</summary>
    /// <value>
    /// A function that receives the <c>beforematch</c> event, requests the open it stands for, and returns whether the
    /// panel is open once the request has run; <see langword="null"/> answers that it is not.
    /// </value>
    public Func<EventSnapshot, ValueTask<bool>>? RevealRequestHandler { get; set; }

    /// <summary>Receives a change of the forced idle.</summary>
    /// <param name="payloadJson">The JSON of a <see cref="PanelForcedIdleChange"/>.</param>
    /// <exception cref="System.Text.Json.JsonException">The payload is not valid.</exception>
    [JSInvokable]
    public void OnForcedIdleChange(string payloadJson)
    {
        PanelForcedIdleChange change = Deserialize(payloadJson, BlazixJsonContext.Default.PanelForcedIdleChange);
        ForcedIdleChanged?.Invoke(change.ForcedIdle);
    }

    /// <summary>Receives the end of the panel's exit.</summary>
    /// <param name="payloadJson">The JSON of a <see cref="TransitionCompletion"/>; only the exit is reported.</param>
    /// <exception cref="System.Text.Json.JsonException">The payload is not valid.</exception>
    [JSInvokable]
    public void OnTransitionComplete(string payloadJson)
    {
        TransitionCompletion completion = Deserialize(payloadJson, BlazixJsonContext.Default.TransitionCompletion);
        if (!completion.Open)
        {
            ExitCompleted?.Invoke();
        }
    }

    /// <summary>Receives the detected animation type.</summary>
    /// <param name="payloadJson">The JSON of a <see cref="PanelAnimationTypeChange"/>.</param>
    /// <exception cref="System.Text.Json.JsonException">The payload is not valid.</exception>
    [JSInvokable]
    public void OnAnimationTypeChange(string payloadJson)
    {
        PanelAnimationTypeChange change = Deserialize(payloadJson, BlazixJsonContext.Default.PanelAnimationTypeChange);
        AnimationTypeChanged?.Invoke(change);
    }

    /// <summary>Receives a change of the panel's transition status.</summary>
    /// <param name="payloadJson">The JSON of a <see cref="PanelStatusChange"/>.</param>
    /// <exception cref="System.Text.Json.JsonException">The payload is not valid.</exception>
    [JSInvokable]
    public void OnPanelStatusChange(string payloadJson)
    {
        PanelStatusChange change = Deserialize(payloadJson, BlazixJsonContext.Default.PanelStatusChange);
        StatusChanged?.Invoke(change.Status);
    }

    /// <summary>Receives a reveal that no C# handler saw inside the dispatch and answers whether the panel is open after it.</summary>
    /// <param name="payloadJson">The JSON of a <see cref="RevealRequest"/>.</param>
    /// <returns>The answer of <see cref="RevealRequestHandler"/>, or <see langword="false"/> without one.</returns>
    /// <exception cref="System.Text.Json.JsonException">The payload is not valid.</exception>
    [JSInvokable]
    public async Task<bool> OnRevealRequest(string payloadJson)
    {
        RevealRequest request = Deserialize(payloadJson, BlazixJsonContext.Default.RevealRequest);
        return RevealRequestHandler is { } handler && await handler(request.Event).ConfigureAwait(false);
    }

    /// <inheritdoc />
    /// <remarks>The module has one decision point, <see cref="RevealPoint"/>; any other point answers <see cref="DecisionPolicy.Run"/>.</remarks>
    protected override DecisionPolicy OnDecide(string point, EventSnapshot snapshot)
    {
        return point == RevealPoint && RevealDecider is { } decider ? decider(snapshot) : DecisionPolicy.Run;
    }
}

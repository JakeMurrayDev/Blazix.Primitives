using Microsoft.JSInterop;

namespace Blazix.Primitives.Interop;

// ADR 04 §1 (transitions row), §2 (mirrored transitionStatus, the convergent hidden), §3 (one message per state
// change; the reveal decision point, open with the orchestrator: track 5 hand-over note); ADR 01 relaxation table
// (TRN-ANI-03/09/10, RND-STA-01 for mirrored fields, TRN-STA-04/05/07/09 met on both modes). One twin per part.
/// <summary>
/// The C# half of the <c>transitions</c> module: the enter and exit transition of one animated part's element. The
/// module writes the element's <c>data-starting-style</c> and <c>data-ending-style</c>, reports the status the part's
/// state mirrors, and reports when an exit (or, if asked, an enter) has finished.
/// </summary>
/// <param name="jsRuntime">The runtime of the part's circuit or WebAssembly application.</param>
internal sealed class TransitionsTwin(IJSRuntime jsRuntime)
    : TwinBase<TransitionsTwin, TransitionsOptions>(jsRuntime, LibraryModule("transitions"), BlazixJsonContext.Default.TransitionsOptions)
{
    /// <summary>Represents the name of the decision point the module asks about when <c>beforematch</c> reveals the element.</summary>
    public const string RevealPoint = "reveal";

    /// <summary>Occurs when the element's transition status or mounted flag changed, once per change.</summary>
    public event Action<TransitionStatusChange>? StatusChanged;

    /// <summary>Occurs when the animations of one side of the cycle have finished.</summary>
    /// <remarks>
    /// The argument is <see langword="false"/> for an exit: the element is no longer mounted, its status is unset, and
    /// the module has already hidden it if it is about to be removed or hidden. It is <see langword="true"/> for an
    /// enter, reported only when <see cref="TransitionsOptions.ReportOpenComplete"/> is set.
    /// </remarks>
    public event Action<bool>? TransitionCompleted;

    /// <summary>Occurs when <c>beforematch</c> revealed the element and no C# handler could decide inside the dispatch.</summary>
    /// <remarks>
    /// The argument is the snapshot of the <c>beforematch</c> event. The part requests the open it stands for; the module
    /// has already shown the element and puts it back under <c>hidden="until-found"</c> if the open does not arrive.
    /// </remarks>
    public event Action<EventSnapshot>? RevealRequested;

    /// <summary>Gets or sets the C# handler chain of the reveal decision point.</summary>
    /// <value>
    /// A function that receives the <c>beforematch</c> event, requests the open, and returns
    /// <see cref="DecisionPolicy.Run"/> to let the reveal proceed or <see cref="DecisionPolicy.Stop"/> when a consumer
    /// canceled the request; <see langword="null"/> always lets it proceed. It runs only on WebAssembly, synchronously
    /// inside the dispatch, and must not await.
    /// </value>
    public Func<EventSnapshot, DecisionPolicy>? RevealDecider { get; set; }

    /// <summary>Receives a change of the element's transition status or mounted flag.</summary>
    /// <param name="payloadJson">The JSON of a <see cref="TransitionStatusChange"/>.</param>
    /// <exception cref="System.Text.Json.JsonException">The payload is not valid.</exception>
    [JSInvokable]
    public void OnStatusChange(string payloadJson)
    {
        TransitionStatusChange change = Deserialize(payloadJson, BlazixJsonContext.Default.TransitionStatusChange);
        StatusChanged?.Invoke(change);
    }

    /// <summary>Receives the completion of one side of the cycle.</summary>
    /// <param name="payloadJson">The JSON of a <see cref="TransitionCompletion"/>.</param>
    /// <exception cref="System.Text.Json.JsonException">The payload is not valid.</exception>
    [JSInvokable]
    public void OnTransitionComplete(string payloadJson)
    {
        TransitionCompletion completion = Deserialize(payloadJson, BlazixJsonContext.Default.TransitionCompletion);
        TransitionCompleted?.Invoke(completion.Open);
    }

    /// <summary>Receives a reveal that no C# handler saw inside the dispatch.</summary>
    /// <param name="payloadJson">The JSON of a <see cref="RevealRequest"/>.</param>
    /// <exception cref="System.Text.Json.JsonException">The payload is not valid.</exception>
    [JSInvokable]
    public void OnRevealRequest(string payloadJson)
    {
        RevealRequest request = Deserialize(payloadJson, BlazixJsonContext.Default.RevealRequest);
        RevealRequested?.Invoke(request.Event);
    }

    /// <inheritdoc />
    /// <remarks>The module has one decision point, <see cref="RevealPoint"/>; any other point answers <see cref="DecisionPolicy.Run"/>.</remarks>
    protected override DecisionPolicy OnDecide(string point, EventSnapshot snapshot)
    {
        return point == RevealPoint && RevealDecider is { } decider ? decider(snapshot) : DecisionPolicy.Run;
    }
}

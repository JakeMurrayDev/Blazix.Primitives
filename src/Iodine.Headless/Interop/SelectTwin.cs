using Microsoft.JSInterop;

namespace Blazix.Primitives.Interop;

// ADR 04 §1 (the `select` component module, its row dated 2026-11-04 by M5 track 9), §2, §3 and Amendments 2026-11-03 (4)
// (`activate` on the `select` twin, chosen by the user 2026-11-03): the module places the popup over the trigger, grows it
// as the list scrolls, runs the scroll arrows, the trigger's release check and the items' press-and-release gates, and
// classifies each item click. One twin per part: the Positioner's handle (placement, the window resize, the items'
// activation delegated from it), each scroll arrow's, the Trigger's.
// - `activate` (fallback run): on WebAssembly the module hands the item over (OnActivating) and asks the point inside the
//   click's dispatch, so the item's declined consumer `onclick` runs there and its PreventPartHandler() stops the
//   activation, the drag release's synthetic click included (SEL-PTR-11); on Server it reports the activation and the
//   veto is a round trip late (ADR 01's "C#-origin consumer vetoes" row).
/// <summary>
/// The C# half of the <c>select</c> module: the aligned placement and the activation of the items of a select's positioner,
/// the visibility and auto-scroll of a scroll arrow, and the release check of a trigger.
/// </summary>
/// <param name="jsRuntime">The runtime of the part's circuit or WebAssembly application.</param>
internal sealed class SelectTwin(IJSRuntime jsRuntime)
    : TwinBase<SelectTwin, SelectOptions>(jsRuntime, LibraryModule("select"), BlazixJsonContext.Default.SelectOptions)
{
    /// <summary>Represents the name of the decision point the module asks about before an item click activates the item.</summary>
    public const string ActivatePoint = "activate";

    /// <summary>Represents the part literal of a positioner's handle.</summary>
    public const string PositionerPart = "positioner";

    /// <summary>Represents the part literal of a list's handle.</summary>
    public const string ListPart = "list";

    /// <summary>Represents the part literal of a scroll arrow's handle.</summary>
    public const string ScrollArrowPart = "scroll-arrow";

    /// <summary>Represents the part literal of a trigger's handle.</summary>
    public const string TriggerPart = "trigger";

    private SelectActivation? pendingActivation;

    /// <summary>Occurs when an item click activated an item and no C# chain ran inside the dispatch.</summary>
    public event Action<SelectActivation>? ActivationReported;

    /// <summary>Occurs when aligned placement became active or inactive, once per change.</summary>
    public event Action<bool>? AlignedChanged;

    /// <summary>Occurs when the visibility of the scroll arrows changed, once per change.</summary>
    public event Action<SelectArrowsVisibility>? ArrowsChanged;

    /// <summary>Occurs when aligned placement placed the popup.</summary>
    public event Action? Placed;

    /// <summary>Occurs when the pointer moved over a scroll arrow, which clears the highlight.</summary>
    public event Action? HighlightCleared;

    /// <summary>Occurs when the press that opened the popup was released away from the trigger and the popup.</summary>
    public event Action<EventSnapshot>? OpenCanceled;

    /// <summary>Occurs when the window was resized while aligned placement was active.</summary>
    public event Action<EventSnapshot>? WindowResized;

    /// <summary>Occurs when the trigger lost focus, with whether focus moved into the popup.</summary>
    public event Action<bool>? TriggerBlurred;

    /// <summary>Gets or sets the C# chain of the activation decision point.</summary>
    /// <value>
    /// A function that receives the click about to activate the item named by <see cref="PendingActivation"/>, runs the
    /// item's consumer <c>onclick</c> and the commit, and returns <see cref="DecisionPolicy.Stop"/> when the consumer called
    /// <c>PreventPartHandler()</c>; <see langword="null"/> always lets it. It runs only on WebAssembly, synchronously inside
    /// the dispatch, and must not await.
    /// </value>
    public Func<EventSnapshot, DecisionPolicy>? ActivationDecider { get; set; }

    /// <summary>Gets the activation the module handed over for the decision in progress.</summary>
    /// <value>The item's id, or <see langword="null"/> outside an activation decision.</value>
    public SelectActivation? PendingActivation => pendingActivation;

    /// <summary>Receives the item the next activation decision is about, inside the dispatch, on WebAssembly only.</summary>
    /// <param name="payloadJson">The JSON of a <see cref="SelectActivation"/> without its event.</param>
    /// <exception cref="System.Text.Json.JsonException">The payload is not valid.</exception>
    [JSInvokable]
    public void OnActivating(string payloadJson) => pendingActivation = Deserialize(payloadJson, BlazixJsonContext.Default.SelectActivation);

    /// <summary>Receives an activation that was not decided inside the dispatch.</summary>
    /// <param name="payloadJson">The JSON of a <see cref="SelectActivation"/>.</param>
    /// <exception cref="System.Text.Json.JsonException">The payload is not valid.</exception>
    [JSInvokable]
    public void OnActivate(string payloadJson) => ActivationReported?.Invoke(Deserialize(payloadJson, BlazixJsonContext.Default.SelectActivation));

    /// <summary>Receives a change of whether aligned placement is active.</summary>
    /// <param name="payloadJson">The JSON of a <see cref="SelectAlignedChange"/>.</param>
    /// <exception cref="System.Text.Json.JsonException">The payload is not valid.</exception>
    [JSInvokable]
    public void OnAlignedChange(string payloadJson) => AlignedChanged?.Invoke(Deserialize(payloadJson, BlazixJsonContext.Default.SelectAlignedChange).Aligned);

    /// <summary>Receives a change of the scroll arrows' visibility.</summary>
    /// <param name="payloadJson">The JSON of a <see cref="SelectArrowsVisibility"/>.</param>
    /// <exception cref="System.Text.Json.JsonException">The payload is not valid.</exception>
    [JSInvokable]
    public void OnArrowsChange(string payloadJson) => ArrowsChanged?.Invoke(Deserialize(payloadJson, BlazixJsonContext.Default.SelectArrowsVisibility));

    /// <summary>Receives the end of an aligned placement.</summary>
    [JSInvokable]
    public void OnPlaced() => Placed?.Invoke();

    /// <summary>Receives a pointer movement over a scroll arrow.</summary>
    [JSInvokable]
    public void OnClearHighlight() => HighlightCleared?.Invoke();

    /// <summary>Receives the release of the opening press away from the trigger and the popup.</summary>
    /// <param name="eventJson">The JSON of the <c>mouseup</c>'s <see cref="EventSnapshot"/>.</param>
    /// <exception cref="System.Text.Json.JsonException">The payload is not valid.</exception>
    [JSInvokable]
    public void OnCancelOpen(string eventJson) => OpenCanceled?.Invoke(Deserialize(eventJson, BlazixJsonContext.Default.EventSnapshot));

    /// <summary>Receives a resize of the window while aligned placement was active.</summary>
    /// <param name="eventJson">The JSON of the <c>resize</c>'s <see cref="EventSnapshot"/>.</param>
    /// <exception cref="System.Text.Json.JsonException">The payload is not valid.</exception>
    [JSInvokable]
    public void OnWindowResize(string eventJson) => WindowResized?.Invoke(Deserialize(eventJson, BlazixJsonContext.Default.EventSnapshot));

    /// <summary>Receives the trigger's blur.</summary>
    /// <param name="payloadJson">The JSON of a <see cref="SelectTriggerBlur"/>.</param>
    /// <exception cref="System.Text.Json.JsonException">The payload is not valid.</exception>
    [JSInvokable]
    public void OnTriggerBlur(string payloadJson) => TriggerBlurred?.Invoke(Deserialize(payloadJson, BlazixJsonContext.Default.SelectTriggerBlur).IntoPopup);

    /// <inheritdoc />
    /// <remarks>
    /// The module has one decision point, <see cref="ActivatePoint"/>; it is answered by <see cref="ActivationDecider"/>
    /// while an activation was handed over, and the hand-over is consumed. Any other point answers <see cref="DecisionPolicy.Run"/>.
    /// </remarks>
    protected override DecisionPolicy OnDecide(string point, EventSnapshot snapshot)
    {
        if (!string.Equals(point, ActivatePoint, StringComparison.Ordinal))
        {
            return DecisionPolicy.Run;
        }

        try
        {
            return pendingActivation is not null && ActivationDecider is { } decider ? decider(snapshot) : DecisionPolicy.Run;
        }
        finally
        {
            pendingActivation = null;
        }
    }
}

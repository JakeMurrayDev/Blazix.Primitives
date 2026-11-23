using Microsoft.JSInterop;

namespace Blazix.Primitives.Interop;

// ADR 04 §1 (the `tabs` component module), §2 (the Tabs.Indicator row), §3 (decision points) and Amendments 2026-10-26
// (chosen by the user 2026-10-26): the module listens for a Tab's click and focus, measures the two Tab rects and asks
// `select` on this twin, fallback run; it owns data-activation-direction on the five parts from attach and the
// Indicator's hidden and --active-tab-*. One twin per part: the List's handle beside its CompositeTwin, one per
// Indicator (the Switch and Avatar two-twin precedents). Design point 6 of the Tabs track (the stash invocation before
// `select`) was settled by the orchestrator 2026-10-26. ADR 01 relaxation table: "C#-origin consumer vetoes"
// (TAB-PTR-09), "RND-STA-01 for mirrored fields" (the direction and the Indicator's measurement).
/// <summary>
/// The C# half of the <c>tabs</c> module: on a tab list, the activation of its tabs by click and focus and the activation
/// direction it writes on every part of the tabs; on a tab indicator, the measurement of the active tab.
/// </summary>
/// <param name="jsRuntime">The runtime of the part's circuit or WebAssembly application.</param>
internal sealed class TabsTwin(IJSRuntime jsRuntime)
    : TwinBase<TabsTwin, TabsOptions>(jsRuntime, LibraryModule("tabs"), BlazixJsonContext.Default.TabsOptions)
{
    /// <summary>Represents the name of the decision point the module asks about before it activates the tab a gesture points at.</summary>
    public const string SelectPoint = "select";

    /// <summary>Represents the part literal of a tab list's handle.</summary>
    public const string ListPart = "list";

    /// <summary>Represents the part literal of a tab indicator's handle.</summary>
    public const string IndicatorPart = "indicator";

    private TabsSelection? pendingSelection;

    /// <summary>Occurs when a gesture activates a tab and no C# chain ran inside the dispatch.</summary>
    /// <remarks>The argument carries the tab's id, the measured direction and the event.</remarks>
    public event Action<TabsSelection>? SelectionReported;

    /// <summary>Occurs when a click or focus reaches a tab without activating it, if the options ask for it.</summary>
    public event Action<TabsTabEvent>? TabEventReported;

    /// <summary>Occurs when the module wrote a new activation direction on the parts, once per change.</summary>
    public event Action<TabsActivationDirection>? DirectionChanged;

    /// <summary>Occurs when an indicator's measurement changed, once per change.</summary>
    public event Action<TabsIndicatorMeasurement>? MeasurementChanged;

    /// <summary>Gets or sets the C# chain of the selection decision point.</summary>
    /// <value>
    /// A function that receives the <c>click</c> or <c>focusin</c> about to activate the tab named by
    /// <see cref="PendingSelection"/>, runs the consumer's handler and the request, and returns
    /// <see cref="DecisionPolicy.Stop"/> when the consumer called <c>PreventPartHandler()</c>; <see langword="null"/> always
    /// lets it. It runs only on WebAssembly, synchronously inside the dispatch, and must not await.
    /// </value>
    public Func<EventSnapshot, DecisionPolicy>? SelectionDecider { get; set; }

    /// <summary>Gets the activation the module handed over for the selection decision in progress.</summary>
    /// <value>The tab's id and the measured direction, or <see langword="null"/> outside a selection decision.</value>
    public TabsSelection? PendingSelection => pendingSelection;

    /// <summary>Receives the activation the next selection decision is about, inside the dispatch, on WebAssembly only.</summary>
    /// <param name="payloadJson">The JSON of a <see cref="TabsSelection"/> without its event.</param>
    /// <exception cref="System.Text.Json.JsonException">The payload is not valid.</exception>
    [JSInvokable]
    public void OnSelecting(string payloadJson)
    {
        pendingSelection = Deserialize(payloadJson, BlazixJsonContext.Default.TabsSelection);
    }

    /// <summary>Receives an activation that was not decided inside the dispatch.</summary>
    /// <param name="payloadJson">The JSON of a <see cref="TabsSelection"/>.</param>
    /// <exception cref="System.Text.Json.JsonException">The payload is not valid.</exception>
    [JSInvokable]
    public void OnSelect(string payloadJson)
    {
        TabsSelection selection = Deserialize(payloadJson, BlazixJsonContext.Default.TabsSelection);
        SelectionReported?.Invoke(selection);
    }

    /// <summary>Receives a click or focus on a tab that the module did not activate.</summary>
    /// <param name="payloadJson">The JSON of a <see cref="TabsTabEvent"/>.</param>
    /// <exception cref="System.Text.Json.JsonException">The payload is not valid.</exception>
    [JSInvokable]
    public void OnTabEvent(string payloadJson)
    {
        TabsTabEvent tabEvent = Deserialize(payloadJson, BlazixJsonContext.Default.TabsTabEvent);
        TabEventReported?.Invoke(tabEvent);
    }

    /// <summary>Receives a change of the activation direction.</summary>
    /// <param name="payloadJson">The JSON of a <see cref="TabsDirectionChange"/>.</param>
    /// <exception cref="System.Text.Json.JsonException">The payload is not valid.</exception>
    [JSInvokable]
    public void OnDirectionChange(string payloadJson)
    {
        TabsDirectionChange change = Deserialize(payloadJson, BlazixJsonContext.Default.TabsDirectionChange);
        DirectionChanged?.Invoke(TabsLiterals.ParseDirection(change.Direction));
    }

    /// <summary>Receives a change of an indicator's measurement.</summary>
    /// <param name="payloadJson">The JSON of a <see cref="TabsIndicatorMeasurement"/>.</param>
    /// <exception cref="System.Text.Json.JsonException">The payload is not valid.</exception>
    [JSInvokable]
    public void OnMeasure(string payloadJson)
    {
        TabsIndicatorMeasurement measurement = Deserialize(payloadJson, BlazixJsonContext.Default.TabsIndicatorMeasurement);
        MeasurementChanged?.Invoke(measurement);
    }

    /// <inheritdoc />
    /// <remarks>
    /// The module has one decision point, <see cref="SelectPoint"/>; it is answered by <see cref="SelectionDecider"/> while
    /// an activation was handed over, and the hand-over is consumed. Any other point, or a point without a decider or a
    /// hand-over, answers <see cref="DecisionPolicy.Run"/>.
    /// </remarks>
    protected override DecisionPolicy OnDecide(string point, EventSnapshot snapshot)
    {
        if (!string.Equals(point, SelectPoint, StringComparison.Ordinal))
        {
            return DecisionPolicy.Run;
        }

        try
        {
            return pendingSelection is not null && SelectionDecider is { } decider ? decider(snapshot) : DecisionPolicy.Run;
        }
        finally
        {
            pendingSelection = null;
        }
    }
}

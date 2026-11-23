using Microsoft.JSInterop;

namespace Iodine.Headless.Interop;

// ADR 04 §1 (the `number-field` component module, its scrub role) and §3 (decision points; the `scrub-start` point,
// fallback start, design point 1 (a), the name chosen by the user 2026-11-18); issue 277, 6b. One twin per part: a
// NumberFieldScrubArea's and a NumberFieldScrubAreaCursor's; only the scrub area's receives reports.
/// <summary>
/// The C# half of the <c>number-field</c> module's scrub role: the pointer lock, the move accumulator and the release of a
/// <c>NumberFieldScrubArea</c>, and the virtual cursor's transform of a <c>NumberFieldScrubAreaCursor</c>.
/// </summary>
/// <param name="jsRuntime">The runtime of the part's circuit or WebAssembly application.</param>
internal sealed class NumberFieldScrubTwin(IJSRuntime jsRuntime)
    : TwinBase<NumberFieldScrubTwin, NumberFieldScrubOptions>(jsRuntime, LibraryModule("number-field"), IodineJsonContext.Default.NumberFieldScrubOptions)
{
    /// <summary>Represents the part literal of a scrub area's handle.</summary>
    public const string ScrubPart = "scrub";

    /// <summary>Represents the part literal of a scrub area cursor's handle.</summary>
    public const string CursorPart = "cursor";

    /// <summary>Represents the name of the decision point the module asks about before a scrub starts.</summary>
    public const string ScrubStartPoint = "scrub-start";

    /// <summary>Occurs when a primary pointerdown started a scrub.</summary>
    public event Action<NumberFieldScrubStartReport>? ScrubStarted;

    /// <summary>Occurs when a pointer-lock request settled differently from the last one.</summary>
    public event Action<NumberFieldScrubLockReport>? LockSettled;

    /// <summary>Occurs when the pointer moved past the scrub area's sensitivity.</summary>
    public event Action<NumberFieldScrubStepReport>? ScrubStepped;

    /// <summary>Occurs when the pointer that started a scrub was released.</summary>
    public event Action<NumberFieldScrubEndReport>? ScrubEnded;

    /// <summary>Gets or sets the C# handler chain of the <c>scrub-start</c> point.</summary>
    /// <value>A function that returns <see cref="DecisionPolicy.Stop"/> when a consumer vetoed the scrub; <see langword="null"/> runs.</value>
    public Func<EventSnapshot, DecisionPolicy>? StartDecider { get; set; }

    /// <summary>Receives the start of a scrub.</summary>
    /// <param name="payloadJson">The JSON of a <see cref="NumberFieldScrubStartReport"/>.</param>
    /// <exception cref="System.Text.Json.JsonException">The payload is not valid.</exception>
    [JSInvokable]
    public void OnScrubStart(string payloadJson) => ScrubStarted?.Invoke(Deserialize(payloadJson, IodineJsonContext.Default.NumberFieldScrubStartReport));

    /// <summary>Receives the outcome of a pointer-lock request.</summary>
    /// <param name="payloadJson">The JSON of a <see cref="NumberFieldScrubLockReport"/>.</param>
    /// <exception cref="System.Text.Json.JsonException">The payload is not valid.</exception>
    [JSInvokable]
    public void OnScrubLock(string payloadJson) => LockSettled?.Invoke(Deserialize(payloadJson, IodineJsonContext.Default.NumberFieldScrubLockReport));

    /// <summary>Receives a scrub step.</summary>
    /// <param name="payloadJson">The JSON of a <see cref="NumberFieldScrubStepReport"/>.</param>
    /// <exception cref="System.Text.Json.JsonException">The payload is not valid.</exception>
    [JSInvokable]
    public void OnScrubStep(string payloadJson) => ScrubStepped?.Invoke(Deserialize(payloadJson, IodineJsonContext.Default.NumberFieldScrubStepReport));

    /// <summary>Receives the end of a scrub.</summary>
    /// <param name="payloadJson">The JSON of a <see cref="NumberFieldScrubEndReport"/>.</param>
    /// <exception cref="System.Text.Json.JsonException">The payload is not valid.</exception>
    [JSInvokable]
    public void OnScrubEnd(string payloadJson) => ScrubEnded?.Invoke(Deserialize(payloadJson, IodineJsonContext.Default.NumberFieldScrubEndReport));

    /// <inheritdoc />
    protected override DecisionPolicy OnDecide(string point, EventSnapshot snapshot)
    {
        return point == ScrubStartPoint && StartDecider is { } decider ? decider(snapshot) : DecisionPolicy.Run;
    }
}

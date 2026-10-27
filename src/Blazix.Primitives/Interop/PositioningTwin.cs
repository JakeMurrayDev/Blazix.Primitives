using Microsoft.JSInterop;

namespace Blazix.Primitives.Interop;

// ADR 04 §1 (the `positioning` mechanism module) and Amendments 2026-10-27 (chosen by the user 2026-10-27); ADR 03. One
// twin per Positioner; its Arrow has no handle of its own (the Arrow's id and style travel in the Positioner's options).
// The module asks no decision point (FLT's only dispatch-time prevent is Escape, the `interactions` module's), so
// OnDecide answers run (the DomTwin precedent). ADR 01 relaxation table: "RND-STA-01 for mirrored fields" (FLT-DOM-08),
// "Positioning (FLT-POS-16, FLT-ALG-13) ... met on both modes".
/// <summary>
/// The C# half of the <c>positioning</c> module: one positioner's placement against its anchor, its tracking, the styles
/// and attributes it writes on the positioner and its arrow, and the report of the rendered placement.
/// </summary>
/// <param name="jsRuntime">The runtime of the part's circuit or WebAssembly application.</param>
internal sealed class PositioningTwin(IJSRuntime jsRuntime)
    : TwinBase<PositioningTwin, PositioningOptions>(jsRuntime, LibraryModule("positioning"), BlazixJsonContext.Default.PositioningOptions)
{
    /// <summary>Occurs when the rendered side, alignment, hidden anchor or uncentered arrow changed, once per change.</summary>
    public event Action<PositioningPlacement>? PlacementChanged;

    /// <summary>Receives a change of the rendered placement.</summary>
    /// <param name="payloadJson">The JSON of a <see cref="PositioningPlacement"/>.</param>
    /// <exception cref="System.Text.Json.JsonException">The payload is not valid.</exception>
    [JSInvokable]
    public void OnPlacementChange(string payloadJson)
    {
        PositioningPlacement placement = Deserialize(payloadJson, BlazixJsonContext.Default.PositioningPlacement);
        PlacementChanged?.Invoke(placement);
    }

    /// <inheritdoc />
    /// <remarks>The module has no decision point; every point answers <see cref="DecisionPolicy.Run"/>.</remarks>
    protected override DecisionPolicy OnDecide(string point, EventSnapshot snapshot) => DecisionPolicy.Run;
}

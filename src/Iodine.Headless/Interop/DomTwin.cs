using Microsoft.JSInterop;

namespace Blazix.Primitives.Interop;

// ADR 04 §1 (dom row) and Amendments; ADR 01 relaxation table (AVA-EDGE-02/03, the indeterminate row).
// The module's helpers (owner and shadow DOM, measurement, observers, platform flags) are imported by other modules;
// only the primitives a part drives are reached through this twin. One twin per part.
/// <summary>
/// The C# half of the <c>dom</c> module: the primitives a part drives on its element, which are the <c>checked</c> and
/// <c>indeterminate</c> properties of a hidden input, the live-region announcement marker and the image-load probe.
/// </summary>
/// <param name="jsRuntime">The runtime of the part's circuit or WebAssembly application.</param>
internal sealed class DomTwin(IJSRuntime jsRuntime)
    : TwinBase<DomTwin, DomOptions>(jsRuntime, LibraryModule("dom"), BlazixJsonContext.Default.DomOptions)
{
    /// <summary>Occurs when the status of the image in <see cref="DomOptions.Image"/> changes.</summary>
    /// <remarks>
    /// The argument is <c>loading</c>, <c>loaded</c> or <c>error</c>. A cached image reports <c>loaded</c> (or
    /// <c>error</c>) without <c>loading</c> first.
    /// </remarks>
    public event Action<string>? ImageStatusChanged;

    /// <summary>Receives a change of the image status.</summary>
    /// <param name="payloadJson">The JSON of an <see cref="ImageStatusChange"/>.</param>
    /// <exception cref="System.Text.Json.JsonException">The payload is not valid.</exception>
    [JSInvokable]
    public void OnImageStatus(string payloadJson)
    {
        ImageStatusChange change = Deserialize(payloadJson, BlazixJsonContext.Default.ImageStatusChange);
        ImageStatusChanged?.Invoke(change.Status);
    }

    /// <inheritdoc />
    /// <remarks>The module has no decision point; every point answers <see cref="DecisionPolicy.Run"/>.</remarks>
    protected override DecisionPolicy OnDecide(string point, EventSnapshot snapshot) => DecisionPolicy.Run;
}

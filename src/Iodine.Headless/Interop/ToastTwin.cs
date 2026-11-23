using Microsoft.JSInterop;

namespace Iodine.Headless.Interop;

// ADR 04 §1 (the `toast` component module), its scope in Amendments 2026-11-18 and the 9b design points of 2026-11-19
// (DP1 (a), chosen by the user): one twin per part, with the part literals viewport, root and content, one handle per
// element. The module reports to the viewport's and the root's twins; the content's reports nothing. No decision point:
// no Toast row is a consumer veto of a dispatch the module owns (issue 280), and the Close's and Action's ride on the
// button behavior.
/// <summary>The C# half of the <c>toast</c> module for one part of a toast viewport.</summary>
/// <param name="jsRuntime">The runtime of the part's circuit or WebAssembly application.</param>
internal sealed class ToastTwin(IJSRuntime jsRuntime)
    : TwinBase<ToastTwin, ToastOptions>(jsRuntime, LibraryModule("toast"), IodineJsonContext.Default.ToastOptions)
{
    /// <summary>Represents the part literal of a viewport's handle.</summary>
    public const string ViewportPart = "viewport";

    /// <summary>Represents the part literal of a root's handle.</summary>
    public const string RootPart = "root";

    /// <summary>Represents the part literal of a content's handle.</summary>
    public const string ContentPart = "content";

    /// <summary>Occurs when the module reported a change.</summary>
    public event Action<ToastReport>? Reported;

    /// <summary>Receives one report.</summary>
    /// <param name="payloadJson">The JSON of a <see cref="ToastReport"/>.</param>
    /// <exception cref="System.Text.Json.JsonException">The payload is not valid.</exception>
    [JSInvokable]
    public void OnReport(string payloadJson)
    {
        Reported?.Invoke(Deserialize(payloadJson, IodineJsonContext.Default.ToastReport));
    }

    /// <inheritdoc />
    /// <remarks>The module asks no decision point, so every point answers <see cref="DecisionPolicy.Run"/>.</remarks>
    protected override DecisionPolicy OnDecide(string point, EventSnapshot snapshot) => DecisionPolicy.Run;
}

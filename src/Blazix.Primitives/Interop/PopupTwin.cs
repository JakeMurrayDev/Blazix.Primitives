using Microsoft.JSInterop;

namespace Blazix.Primitives.Interop;

// ADR 04 Amendments 2026-10-27 (the `popup` row, chosen by the user 2026-10-27): the twin of the `popup` mechanism module,
// attached by the parts of every popup family (one twin per part). Its reports: the Viewport's mirrored direction and
// transitioning flag, and the end of a move between triggers (POP-ANI-07; C# writes the trigger-change instant). The
// module asks no decision point yet, so OnDecide answers run (the DomTwin precedent).
/// <summary>The C# twin of the <c>popup</c> module.</summary>
/// <param name="jsRuntime">The JavaScript runtime.</param>
internal sealed class PopupTwin(IJSRuntime jsRuntime)
    : TwinBase<PopupTwin, PopupOptions>(jsRuntime, LibraryModule("popup"), BlazixJsonContext.Default.PopupOptions)
{
    /// <summary>Occurs when the viewport's direction or transitioning flag changed.</summary>
    public event Action<PopupViewportReport>? ViewportChanged;

    /// <summary>Occurs when a move between triggers has finished animating.</summary>
    public event Action? TriggerChangeSettled;

    /// <summary>Receives the viewport's mirrored values.</summary>
    /// <param name="payloadJson">The <see cref="PopupViewportReport"/> JSON.</param>
    [JSInvokable]
    public void OnViewportChange(string payloadJson)
    {
        PopupViewportReport report = Deserialize(payloadJson, BlazixJsonContext.Default.PopupViewportReport);
        ViewportChanged?.Invoke(report);
    }

    /// <summary>Receives the end of a move between triggers.</summary>
    /// <param name="payloadJson">An empty JSON object.</param>
    [JSInvokable]
    public void OnTriggerChangeSettled(string payloadJson)
    {
        _ = payloadJson;
        TriggerChangeSettled?.Invoke();
    }

    /// <inheritdoc />
    protected override DecisionPolicy OnDecide(string point, EventSnapshot snapshot) => DecisionPolicy.Run;
}

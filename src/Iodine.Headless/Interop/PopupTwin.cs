using Microsoft.JSInterop;

namespace Blazix.Primitives.Interop;

// ADR 04 Amendments 2026-10-27 (the `popup` row, chosen by the user 2026-10-27): the twin of the `popup` mechanism module,
// attached by the parts of every popup family (one twin per part). Its reports: the Viewport's mirrored direction and
// transitioning flag, and the end of a move between triggers (POP-ANI-07; C# writes the trigger-change instant). A focused
// trigger guard (design point 5 of M4 track 5, settled by the orchestrator 2026-10-28) is reported on Server after the
// module moved focus; on WebAssembly the module hands the guard over, then asks `guard`, and the close runs inside it
// (the `press` precedent); every decision answers run.
/// <summary>The C# twin of the <c>popup</c> module.</summary>
/// <param name="jsRuntime">The JavaScript runtime.</param>
internal sealed class PopupTwin(IJSRuntime jsRuntime)
    : TwinBase<PopupTwin, PopupOptions>(jsRuntime, LibraryModule("popup"), BlazixJsonContext.Default.PopupOptions)
{
    /// <summary>The decision point a focused trigger guard asks on WebAssembly.</summary>
    public const string GuardPoint = "guard";

    private string? pendingGuard;

    /// <summary>Occurs when the viewport's direction or transitioning flag changed.</summary>
    public event Action<PopupViewportReport>? ViewportChanged;

    /// <summary>Occurs when a move between triggers has finished animating.</summary>
    public event Action? TriggerChangeSettled;

    /// <summary>Occurs when a trigger guard was focused: <c>before</c> or <c>after</c>, with the focus event's snapshot.</summary>
    public event Action<string, EventSnapshot>? GuardFocused;

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

    /// <summary>Receives a focused trigger guard on Server, after the module moved focus.</summary>
    /// <param name="payloadJson">The <see cref="PopupGuardFocus"/> JSON.</param>
    [JSInvokable]
    public void OnGuardFocus(string payloadJson)
    {
        PopupGuardFocus report = Deserialize(payloadJson, BlazixJsonContext.Default.PopupGuardFocus);
        GuardFocused?.Invoke(report.Guard, report.Event);
    }

    /// <summary>Receives the focused guard just before the <c>guard</c> decision on WebAssembly.</summary>
    /// <param name="payloadJson">The <see cref="PopupGuardHandover"/> JSON.</param>
    [JSInvokable]
    public void OnGuardFocusing(string payloadJson)
    {
        pendingGuard = Deserialize(payloadJson, BlazixJsonContext.Default.PopupGuardHandover).Guard;
    }

    /// <inheritdoc />
    protected override DecisionPolicy OnDecide(string point, EventSnapshot snapshot)
    {
        if (point == GuardPoint && pendingGuard is { } guard)
        {
            pendingGuard = null;
            GuardFocused?.Invoke(guard, snapshot);
        }

        return DecisionPolicy.Run;
    }
}

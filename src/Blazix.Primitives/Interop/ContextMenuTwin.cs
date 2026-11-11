using Microsoft.JSInterop;

namespace Blazix.Primitives.Interop;

// ADR 04 §1 (the `context-menu` component module) and Amendments 2026-10-18 and 2026-11-03 (3), (4) (chosen by the user
// 2026-11-03); design points 1-3 of M5 track 7. One twin per part that owns a handle: the Root (by token: the release's
// first arm and rules, the origin anchor, its elements) and each Trigger (the right-click and long-press open, the release
// window, the native menu's suppressor). The decision point `press` runs the consumer's declined handler on WebAssembly
// (fallback `run`); on Server the same event arrives as a report before the open it may veto.
/// <summary>Represents the C# half of the <c>context-menu</c> module.</summary>
internal sealed class ContextMenuTwin(IJSRuntime jsRuntime)
    : TwinBase<ContextMenuTwin, ContextMenuOptions>(jsRuntime, LibraryModule("context-menu"), BlazixJsonContext.Default.ContextMenuOptions)
{
    // ADR 04 Amendments 2026-11-03 (3).
    /// <summary>The decision point asked before the Trigger handles an event the consumer supplied a handler for.</summary>
    public const string PressPoint = "press";

    /// <summary>The part name of the Root's handle.</summary>
    public const string RootPart = "root";

    /// <summary>The part name of a Trigger's handle.</summary>
    public const string TriggerPart = "trigger";

    private ContextMenuPress? pendingPress;

    /// <summary>Occurs when a trigger reported an open request.</summary>
    public event Action<ContextMenuOpenRequest>? OpenRequested;

    /// <summary>Occurs when a trigger reported a <c>cancel-open</c> close request.</summary>
    public event Action<ContextMenuCancelOpen>? CancelOpenRequested;

    /// <summary>Occurs when a trigger reported the event of a consumer handler it did not decide inside its dispatch.</summary>
    public event Action<ContextMenuPress>? Pressed;

    /// <summary>Gets or sets the chain the <c>press</c> decision runs on WebAssembly: the consumer's declined handler.</summary>
    /// <value>
    /// A function that receives the handed-over event, runs the consumer's handler and returns
    /// <see cref="DecisionPolicy.Stop"/> when it called <c>PreventPartHandler()</c>; <see langword="null"/> lets every
    /// event run. It runs synchronously inside the dispatch and must not await.
    /// </value>
    public Func<ContextMenuPress, DecisionPolicy>? PressDecider { get; set; }

    /// <summary>Receives an open request.</summary>
    /// <param name="payloadJson">The <see cref="ContextMenuOpenRequest"/> JSON.</param>
    [JSInvokable]
    public void OnOpen(string payloadJson)
    {
        ContextMenuOpenRequest request = Deserialize(payloadJson, BlazixJsonContext.Default.ContextMenuOpenRequest);
        OpenRequested?.Invoke(request);
    }

    /// <summary>Receives a <c>cancel-open</c> close request.</summary>
    /// <param name="payloadJson">The <see cref="ContextMenuCancelOpen"/> JSON.</param>
    [JSInvokable]
    public void OnCancelOpen(string payloadJson)
    {
        ContextMenuCancelOpen request = Deserialize(payloadJson, BlazixJsonContext.Default.ContextMenuCancelOpen);
        CancelOpenRequested?.Invoke(request);
    }

    /// <summary>Receives the event of a consumer handler on Server, before the open request it may veto.</summary>
    /// <param name="payloadJson">The <see cref="ContextMenuPress"/> JSON.</param>
    [JSInvokable]
    public void OnPress(string payloadJson)
    {
        ContextMenuPress press = Deserialize(payloadJson, BlazixJsonContext.Default.ContextMenuPress);
        Pressed?.Invoke(press);
    }

    /// <summary>Receives, synchronously on WebAssembly, the event the next <c>press</c> decision is about.</summary>
    /// <param name="payloadJson">The <see cref="ContextMenuPress"/> JSON.</param>
    [JSInvokable]
    public void OnPressing(string payloadJson)
    {
        pendingPress = Deserialize(payloadJson, BlazixJsonContext.Default.ContextMenuPress);
    }

    /// <inheritdoc />
    /// <remarks>
    /// <see cref="PressPoint"/> is answered by <see cref="PressDecider"/> with the handed-over event, whose snapshot the
    /// decision's replaces, and the hand-over is consumed; without a decider or a hand-over it answers
    /// <see cref="DecisionPolicy.Run"/>, the module's fallback. Any other point answers <see cref="DecisionPolicy.Run"/>.
    /// </remarks>
    protected override DecisionPolicy OnDecide(string point, EventSnapshot snapshot)
    {
        if (!string.Equals(point, PressPoint, StringComparison.Ordinal) || pendingPress is not { } press)
        {
            return DecisionPolicy.Run;
        }

        pendingPress = null;
        return PressDecider is { } decider ? decider(press with { Event = snapshot }) : DecisionPolicy.Run;
    }
}

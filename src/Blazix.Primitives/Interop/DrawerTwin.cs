using Microsoft.JSInterop;

namespace Blazix.Primitives.Interop;

// ADR 04 §1 (the `drawer` component module), §2 (the Popup's and Backdrop's JS-owned swipe attributes and styles), §3
// as amended 2026-11-18 (the `dismiss` point, fallback run); design point 10 (a) (chosen by the user 2026-11-19).
// M6 track 8b, issue 279; slice 1: the Viewport handle of the mouse and pen dismiss gesture.
/// <summary>
/// The C# half of the <c>drawer</c> module: the swipe gesture of a drawer's popup, run by the viewport's handle. The
/// module reports whether the popup is being swiped and the releases that dismiss the drawer, and asks the
/// <see cref="DismissPoint"/> decision point inside the release where it can.
/// </summary>
/// <param name="jsRuntime">The runtime of the part's circuit or WebAssembly application.</param>
internal sealed class DrawerTwin(IJSRuntime jsRuntime)
    : TwinBase<DrawerTwin, DrawerOptions>(jsRuntime, LibraryModule("drawer"), BlazixJsonContext.Default.DrawerOptions)
{
    /// <summary>Represents the role of the handle a drawer's viewport attaches.</summary>
    public const string ViewportRole = "viewport";

    /// <summary>Represents the name of the decision point the module asks about when a swipe release dismisses the drawer.</summary>
    public const string DismissPoint = "dismiss";

    /// <summary>Occurs when the popup starts or stops being swiped.</summary>
    public event Action<bool>? SwipingChanged;

    /// <summary>Occurs when a swipe release dismisses the drawer and no C# handler could decide it inside the dispatch.</summary>
    public event Action<DrawerDismissRequest>? DismissRequested;

    /// <summary>Gets or sets the C# handler chain of the dismiss decision point.</summary>
    /// <value>
    /// A function that receives the release event, requests the close, and returns <see cref="DecisionPolicy.Run"/> to
    /// let the dismissal proceed or <see cref="DecisionPolicy.Stop"/> when a consumer canceled the request;
    /// <see langword="null"/> always lets it proceed. It runs only on WebAssembly, synchronously inside the dispatch, and
    /// must not await.
    /// </value>
    public Func<EventSnapshot, DecisionPolicy>? DismissDecider { get; set; }

    /// <summary>Receives a change of whether the popup is being swiped.</summary>
    /// <param name="payloadJson">The JSON of a <see cref="DrawerSwipingChange"/>.</param>
    /// <exception cref="System.Text.Json.JsonException">The payload is not valid.</exception>
    [JSInvokable]
    public void OnSwipingChange(string payloadJson)
    {
        DrawerSwipingChange change = Deserialize(payloadJson, BlazixJsonContext.Default.DrawerSwipingChange);
        SwipingChanged?.Invoke(change.Swiping);
    }

    /// <summary>Receives a swipe release that dismisses the drawer.</summary>
    /// <param name="payloadJson">The JSON of a <see cref="DrawerDismissRequest"/>.</param>
    /// <exception cref="System.Text.Json.JsonException">The payload is not valid.</exception>
    [JSInvokable]
    public void OnDismissRequest(string payloadJson)
    {
        DrawerDismissRequest request = Deserialize(payloadJson, BlazixJsonContext.Default.DrawerDismissRequest);
        DismissRequested?.Invoke(request);
    }

    /// <inheritdoc />
    /// <remarks>The module's points are <see cref="DismissPoint"/> and, later, the snap-point one; any other answers <see cref="DecisionPolicy.Run"/>.</remarks>
    protected override DecisionPolicy OnDecide(string point, EventSnapshot snapshot)
    {
        return point == DismissPoint && DismissDecider is { } decider ? decider(snapshot) : DecisionPolicy.Run;
    }
}

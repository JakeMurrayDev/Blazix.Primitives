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

    // Slice 3 (2026-11-21): the Popup measures itself (DRW-STA-10) and shows its nested drawers' progress (DOM-14); an
    // Indent shows its Provider's visual state (DOM-32).
    /// <summary>Represents the role of the handle a drawer's popup attaches.</summary>
    public const string PopupRole = "popup";

    /// <summary>Represents the role of the handle a drawer indent attaches under a provider.</summary>
    public const string IndentRole = "indent";

    // Slice 4 (2026-11-21): the SwipeArea's opening gesture (DRW-PTR-25…31).
    /// <summary>Represents the role of the handle a drawer's swipe area attaches.</summary>
    public const string SwipeAreaRole = "swipe-area";

    /// <summary>Represents the name of the decision point the module asks about when a swipe release dismisses the drawer.</summary>
    public const string DismissPoint = "dismiss";

    // Slice 3 (2026-11-20): the module names the requested snap point in the point, `snap-point:<index>` into the options'
    // snap points or `snap-point:none`, as a decision carries only its point and its event.
    /// <summary>The prefix of the module's snap point decision point, followed by an index into the snap points or <c>none</c>.</summary>
    public const string SnapPointPointPrefix = "snap-point:";

    // M6 track 8c (2026-11-21): DRW-DIS-01, design point D1 (a), chosen by the user 2026-11-21 (ADR 04 §3).
    /// <summary>Represents the name of the decision point the popup's handle asks about inside its close watcher's <c>cancel</c> event.</summary>
    public const string CloseWatcherPoint = "close-watcher";

    /// <summary>Occurs when the popup starts or stops being swiped.</summary>
    public event Action<bool>? SwipingChanged;

    /// <summary>Occurs when a swipe release dismisses the drawer and no C# handler could decide it inside the dispatch.</summary>
    public event Action<DrawerDismissRequest>? DismissRequested;

    /// <summary>Occurs when the module reports a release that moves the drawer to a snap point, on Blazor Server.</summary>
    public event Action<DrawerSnapPointRequest>? SnapPointRequested;

    /// <summary>Occurs when the popup's handle reports a new measured height.</summary>
    public event Action<double>? PopupHeightChanged;

    /// <summary>Occurs when the viewport's handle reports that the drawer, nested in another, starts or stops being swiped.</summary>
    public event Action<bool>? NestedSwipingChanged;

    /// <summary>Occurs when the swipe area's handle asks to open or close the drawer.</summary>
    public event Action<DrawerOpenRequest>? OpenRequested;

    /// <summary>Occurs when the popup's close watcher asks to close the drawer and no C# handler could decide it inside the event.</summary>
    public event Action<DrawerCloseWatcherRequest>? CloseWatcherRequested;

    /// <summary>Gets or sets the C# handler chain of the dismiss decision point.</summary>
    /// <value>
    /// A function that receives the release event, requests the close, and returns <see cref="DecisionPolicy.Run"/> to
    /// let the dismissal proceed or <see cref="DecisionPolicy.Stop"/> when a consumer canceled the request;
    /// <see langword="null"/> always lets it proceed. It runs only on WebAssembly, synchronously inside the dispatch, and
    /// must not await.
    /// </value>
    public Func<EventSnapshot, DecisionPolicy>? DismissDecider { get; set; }

    /// <summary>Gets or sets the chain the snap point decision point runs: the requested index, or <see langword="null"/> for none, and the release.</summary>
    public Func<int?, EventSnapshot, DecisionPolicy>? SnapPointDecider { get; set; }

    /// <summary>Gets or sets the chain the close-watcher decision point runs on the watcher's <c>cancel</c> event.</summary>
    /// <value>
    /// A function that receives the event, requests the close, and returns <see cref="DecisionPolicy.Prevent"/> when a
    /// consumer canceled it, so the watcher stays, or <see cref="DecisionPolicy.Run"/>; <see langword="null"/> always runs.
    /// It runs only on WebAssembly, synchronously inside the event, and must not await.
    /// </value>
    public Func<EventSnapshot, DecisionPolicy>? CloseWatcherDecider { get; set; }

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

    /// <summary>Receives a swipe release that moves the drawer to a snap point, numbered for the options to answer.</summary>
    /// <param name="payloadJson">The JSON of a <see cref="DrawerSnapPointRequest"/>.</param>
    /// <exception cref="System.Text.Json.JsonException">The payload is not valid.</exception>
    [JSInvokable]
    public void OnSnapPointRequest(string payloadJson)
    {
        DrawerSnapPointRequest request = Deserialize(payloadJson, BlazixJsonContext.Default.DrawerSnapPointRequest);
        SnapPointRequested?.Invoke(request);
    }

    /// <summary>Receives the popup's measured height.</summary>
    /// <param name="payloadJson">The JSON of a <see cref="DrawerPopupHeightChange"/>.</param>
    /// <exception cref="System.Text.Json.JsonException">The payload is not valid.</exception>
    [JSInvokable]
    public void OnPopupHeightChange(string payloadJson)
    {
        DrawerPopupHeightChange change = Deserialize(payloadJson, BlazixJsonContext.Default.DrawerPopupHeightChange);
        PopupHeightChanged?.Invoke(change.Height);
    }

    /// <summary>Receives a change of whether the drawer, nested in another, is being swiped.</summary>
    /// <param name="payloadJson">The JSON of a <see cref="DrawerSwipingChange"/>.</param>
    /// <exception cref="System.Text.Json.JsonException">The payload is not valid.</exception>
    [JSInvokable]
    public void OnNestedSwipingChange(string payloadJson)
    {
        DrawerSwipingChange change = Deserialize(payloadJson, BlazixJsonContext.Default.DrawerSwipingChange);
        NestedSwipingChanged?.Invoke(change.Swiping);
    }

    /// <summary>Receives a swipe area's request to open or close the drawer, numbered for the options to answer.</summary>
    /// <param name="payloadJson">The JSON of a <see cref="DrawerOpenRequest"/>.</param>
    /// <exception cref="System.Text.Json.JsonException">The payload is not valid.</exception>
    [JSInvokable]
    public void OnOpenRequest(string payloadJson)
    {
        DrawerOpenRequest request = Deserialize(payloadJson, BlazixJsonContext.Default.DrawerOpenRequest);
        OpenRequested?.Invoke(request);
    }

    /// <summary>Receives a close request of the popup's close watcher, reported where the point could not decide it.</summary>
    /// <param name="payloadJson">The JSON of a <see cref="DrawerCloseWatcherRequest"/>.</param>
    /// <exception cref="System.Text.Json.JsonException">The payload is not valid.</exception>
    [JSInvokable]
    public void OnCloseWatcherRequest(string payloadJson)
    {
        DrawerCloseWatcherRequest request = Deserialize(payloadJson, BlazixJsonContext.Default.DrawerCloseWatcherRequest);
        CloseWatcherRequested?.Invoke(request);
    }

    /// <inheritdoc />
    /// <remarks>The module's points are <see cref="DismissPoint"/>, <c>snap-point:&lt;index&gt;</c> (<see cref="SnapPointPointPrefix"/>) and <see cref="CloseWatcherPoint"/>; any other answers <see cref="DecisionPolicy.Run"/>.</remarks>
    protected override DecisionPolicy OnDecide(string point, EventSnapshot snapshot)
    {
        if (point == DismissPoint)
        {
            return DismissDecider is { } decider ? decider(snapshot) : DecisionPolicy.Run;
        }

        if (point == CloseWatcherPoint)
        {
            return CloseWatcherDecider is { } watcherDecider ? watcherDecider(snapshot) : DecisionPolicy.Run;
        }

        if (point.StartsWith(SnapPointPointPrefix, StringComparison.Ordinal) && SnapPointDecider is { } snapDecider)
        {
            ReadOnlySpan<char> target = point.AsSpan(SnapPointPointPrefix.Length);
            if (target.SequenceEqual("none"))
            {
                return snapDecider(null, snapshot);
            }

            if (int.TryParse(target, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out int index))
            {
                return snapDecider(index, snapshot);
            }
        }

        return DecisionPolicy.Run;
    }
}

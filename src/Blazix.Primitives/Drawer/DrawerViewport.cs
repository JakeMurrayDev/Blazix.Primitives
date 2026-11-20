using Blazix.Primitives.Interop;
using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Blazix.Primitives;

// DRW §3.10, DRW-STA-01: Dialog.Viewport's state object (dialog/viewport/DialogViewport.tsx:35-40) under its Drawer name.
// TransitionStatus is mirrored (ADR 04 §2). NestedDialogOpen is a field but never an attribute (DRW-DOM-22).
/// <summary>Represents the state of a drawer's viewport, which the part exposes as data attributes and passes to its class and style functions.</summary>
/// <param name="Open">A value indicating whether the drawer is open; projected as <c>data-open</c> or <c>data-closed</c>.</param>
/// <param name="TransitionStatus">
/// The phase of the drawer's enter or exit transition, or <see langword="null"/> while none is in progress; its
/// <c>data-starting-style</c> and <c>data-ending-style</c> attributes are written by the browser-side behavior.
/// </param>
/// <param name="Nested">A value indicating whether the drawer is nested in another dialog; projected as <c>data-nested</c>.</param>
/// <param name="NestedDialogOpen">A value indicating whether a dialog nested in this one is open; not projected as an attribute.</param>
[PartState]
public sealed partial record DrawerViewportState(bool Open, [property: MirroredField] TransitionStatus? TransitionStatus, bool Nested, bool NestedDialogOpen);

// DRW-STR-02 (drawer/viewport/DrawerViewportDataAttributes.ts): Dialog's without data-nested-dialog-open (DRW-DOM-22).
/// <summary>Provides the names of the data attributes a drawer's viewport renders.</summary>
public static class DrawerViewportDataAttributes
{
    /// <summary>Represents the attribute present while the drawer is open: <c>data-open</c>.</summary>
    public const string Open = "data-open";

    /// <summary>Represents the attribute present while the drawer is closed: <c>data-closed</c>.</summary>
    public const string Closed = "data-closed";

    /// <summary>Represents the attribute present while the drawer begins animating in: <c>data-starting-style</c>.</summary>
    public const string StartingStyle = "data-starting-style";

    /// <summary>Represents the attribute present while the drawer is animating out: <c>data-ending-style</c>.</summary>
    public const string EndingStyle = "data-ending-style";

    /// <summary>Represents the attribute present while the drawer is nested in another dialog: <c>data-nested</c>.</summary>
    public const string Nested = "data-nested";
}

// DRW-STR-02 (drawer/viewport/DrawerViewportCssVars.ts).
/// <summary>Provides the names of the CSS variables a drawer's viewport sets.</summary>
public static class DrawerViewportCssVars
{
    /// <summary>
    /// Represents how far the on-screen keyboard overlaps the viewport in pixels, set while a
    /// <see cref="DrawerVirtualKeyboardProvider"/> keeps a field visible: <c>--drawer-keyboard-inset</c>.
    /// </summary>
    public const string KeyboardInset = "--drawer-keyboard-inset";
}

// Base UI's Drawer.Viewport (drawer/viewport/DrawerViewport.tsx:83-1062); the contract is docs/components/drawer.md (§3.10;
// DRW-STR-04 over dialog.md §3.5, §5.4). Its own type over DialogViewportBehavior (decision 20 (a) of the M6 plan, chosen by
// the user 2026-11-18), cascading the context its popup reads (DRW-STR-04, EDGE-01). The gesture, the touch arbitration and
// the keyboard role are the `drawer` module's Viewport handle (8b, 8c).
/// <summary>
/// Represents a positioning container for the drawer's popup that can be made scrollable, Base UI's
/// <c>Drawer.Viewport</c>, rendered as a <c>div</c>.
/// </summary>
/// <remarks>Place it inside the drawer's portal, around the popup.</remarks>
public sealed class DrawerViewport : PartBase<DrawerViewportState>
{
    // DRW-DOM-22: the popup's open mapping, and nestedDialogOpen never projected (Nested keeps the default, data-nested).
    private static readonly StateAttributesMapping<DrawerViewportState> s_mapping = PopupStateMapping.CreateOpen<DrawerViewportState>()
        .Map<bool>(nameof(DrawerViewportState.NestedDialogOpen), static _ => null);

    private readonly DrawerViewportContext viewport = new();
    private RenderFragment? consumerContent;
    private RenderFragment? cascade;
    private DialogViewportBehavior? behavior;
    private DrawerViewportState state = new(false, null, false, false);
    private DrawerTwin? twin;
    private TwinHandle<DrawerOptions>? handle;
    private Task<TwinHandle<DrawerOptions>>? attaching;
    private DrawerOptions? sentOptions;
    private IDisposable? rootSubscription;
    private IDisposable? drawerSubscription;
    private readonly TaskCompletionSource gestureAttached = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int answeredDismiss;
    private int answeredSnap;
    private int canceledSnap;
    private bool dropNextDismissal;
    private bool deciding;
    private ElementReference? sentPopup;
    private ElementReference? sentBackdrop;

    [Inject]
    private IJSRuntime JSRuntime { get; set; } = null!;

    [CascadingParameter]
    private IDialogRootContext? Context { get; set; }

    [CascadingParameter]
    private DrawerRootContext? Drawer { get; set; }

    [CascadingParameter]
    private PopupPortalContext? Portal { get; set; }

    /// <inheritdoc />
    protected override string DefaultTagName => "div";

    /// <inheritdoc />
    protected override DrawerViewportState State
    {
        get
        {
            IDialogRootContext root = behavior!.Context;
            bool nestedOpen = root.NestedOpenDialogCount > 0;
            if (state.Open != root.Open || state.TransitionStatus != root.TransitionStatus || state.Nested != root.Nested || state.NestedDialogOpen != nestedOpen)
            {
                state = new DrawerViewportState(root.Open, root.TransitionStatus, root.Nested, nestedOpen);
            }

            return state;
        }
    }

    /// <inheritdoc />
    protected override StateAttributesMapping<DrawerViewportState>? StateAttributesMapping => s_mapping;

    // For the fixtures: completes once the transitions handle is attached.
    internal Task TwinAttached => behavior?.TwinAttached ?? Task.CompletedTask;

    // For the fixtures: completes once the `drawer` module's handle is attached.
    internal Task GestureAttached => gestureAttached.Task;

    // For the tests: the context the popup reads.
    internal DrawerViewportContext ViewportContext => viewport;

    /// <inheritdoc />
    public override Task SetParametersAsync(ParameterView parameters)
    {
        ChildContent = null;
        return base.SetParametersAsync(parameters);
    }

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        // DRW-EDGE-02 (DrawerViewport.tsx:100): the Drawer root's own state is required too.
        if (Context is null || Drawer is null)
        {
            throw DialogParts.MissingRoot("Drawer");
        }

        behavior = new DialogViewportBehavior("Drawer", JSRuntime, StateHasChanged, InvokeAsync);

        // M6 track 8b, slice 1: the `drawer` module's Viewport handle runs the dismiss gesture on the Popup.
        twin = new DrawerTwin(JSRuntime);
        twin.SwipingChanged += swiping => _ = InvokeAsync(() => viewport.SetSwiping(swiping));
        twin.DismissRequested += request => _ = InvokeAsync(() => DismissAsync(request));
        twin.DismissDecider = DecideDismiss;
        twin.SnapPointRequested += request => _ = InvokeAsync(() => SnapAsync(request));
        twin.SnapPointDecider = DecideSnapPoint;
        base.OnInitialized();
    }

    // DRW-STR-04: the viewport's context around the consumer's content, rebuilt only when the content changes.
    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        if (cascade is null || !ReferenceEquals(consumerContent, ChildContent))
        {
            consumerContent = ChildContent;
            cascade = viewport.Cascade(consumerContent);
        }

        ChildContent = cascade;
    }

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        await behavior!.OnAfterRenderAsync(Enabled, Element, Portal);
        await SyncTwinAsync();
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            rootSubscription?.Dispose();
            drawerSubscription?.Dispose();
            if (twin is not null)
            {
                _ = twin.DisposeAsync().AsTask();
            }
        }

        if (disposing && behavior is not null)
        {
            _ = behavior.DisposeAsync().AsTask();
        }

        base.Dispose(disposing);
    }

    /// <inheritdoc />
    protected override void SubscribeSelectors()
    {
        behavior!.Subscribe(Context!, Portal);

        // The handle's options: the fields the gesture reads, and the elements it writes on. They reach the module without a
        // render of the viewport.
        rootSubscription = Context!.Subscribe(DialogFields.Open | DialogFields.Mounted | DialogFields.Nested | DialogFields.NestedOpenDrawerCount, SyncTwinLater);
        drawerSubscription = Drawer!.Subscribe(DrawerFields.Settings | DrawerFields.Elements, SyncTwinLater);
    }

    /// <inheritdoc />
    protected override void AddPartAttributes(AttributeList attributes) => behavior!.AddAttributes(attributes);

    // DRW-DOM-22 (DrawerViewport.tsx:1052-1054): the attribute is never rendered, even when the consumer passes it; the
    // document names it part-owned, so the part's removal wins over the consumer (ADR 02 Amendments 2026-10-21).
    /// <inheritdoc />
    protected override void FinishAttributes(AttributeList attributes) => attributes.Remove("data-nested-dialog-open");

    // M6 track 8b, slice 1: attaches the Viewport's handle once the Viewport renders, then sends each change of its
    // options or of the elements it writes on.
    private async Task SyncTwinAsync()
    {
        if (deciding || !Enabled || Element is not { } element || twin is null)
        {
            return;
        }

        DrawerRootContext drawer = Drawer!;
        IDialogRootContext root = Context!;
        string direction = SwipeDirectionLiterals.ToLiteral(drawer.SwipeDirection);
        if (handle is not null && sentOptions is { } sent
            && sent.Open == root.Open && sent.Mounted == root.Mounted && sent.Nested == root.Nested
            && sent.NestedOpenDrawerCount == root.NestedOpenDrawerCount && string.Equals(sent.SwipeDirection, direction, StringComparison.Ordinal)
            && sent.AnsweredDismiss == answeredDismiss && ReferenceEquals(sent.SnapPoints, drawer.SnapPoints)
            && Nullable.Equals(sent.ActiveSnapPoint, drawer.ActiveSnapPoint) && sent.SnapToSequentialPoints == drawer.SnapToSequentialPoints
            && sent.AnsweredSnap == answeredSnap && sent.CanceledSnap == canceledSnap && SameElements())
        {
            // ADR 16: an unchanged render sends nothing and allocates nothing here.
            return;
        }

        TwinReferences references = new();
        int? popup = drawer.PopupElement is { } popupElement ? references.Add(popupElement) : null;
        int? backdrop = drawer.BackdropElement is { } backdropElement ? references.Add(backdropElement) : null;
        DrawerOptions options = new()
        {
            Open = root.Open,
            Mounted = root.Mounted,
            Nested = root.Nested,
            NestedOpenDrawerCount = root.NestedOpenDrawerCount,
            SwipeDirection = direction,
            AnsweredDismiss = answeredDismiss,
            SnapPoints = drawer.SnapPoints,
            ActiveSnapPoint = drawer.ActiveSnapPoint,
            SnapToSequentialPoints = drawer.SnapToSequentialPoints,
            AnsweredSnap = answeredSnap,
            CanceledSnap = canceledSnap,
            Popup = popup,
            Backdrop = backdrop,
        };

        if (handle is null)
        {
            if (attaching is not null)
            {
                return;
            }

            sentOptions = options;
            sentPopup = drawer.PopupElement;
            sentBackdrop = drawer.BackdropElement;
            attaching = twin.AttachAsync(element, options, references).AsTask();
            handle = await attaching;
            gestureAttached.TrySetResult();

            // A change made while the attach was in flight.
            await SyncTwinAsync();
            return;
        }

        // The references are compared through the options: an element change moves no index, so the elements themselves
        // are compared too.
        if (options == sentOptions && SameElements())
        {
            return;
        }

        sentOptions = options;
        sentPopup = drawer.PopupElement;
        sentBackdrop = drawer.BackdropElement;
        await handle.UpdateAsync(options, references);
    }

    // DRW-PTR-14 on Server: the release a round trip later. The request is answered whatever its outcome, so the module
    // rolls the dismissal back when the drawer is still open (design point 10 (a)).
    private async Task DismissAsync(DrawerDismissRequest request)
    {
        // DRW-ALG-01: the dismissal of a release whose change to no snap point was canceled is dropped, as Base UI settles
        // that release in place; Blazor Server learns of the cancel only after the module started the dismissal.
        if (dropNextDismissal)
        {
            dropNextDismissal = false;
        }
        else
        {
            OpenChangeEventDetails details = new(EventReason.Swipe, request.Event);
            await Context!.RequestOpenAsync(false, details);
        }

        answeredDismiss = Math.Max(answeredDismiss, request.Request);
        await SyncTwinAsync();
    }

    // DRW-PTR-14 on WebAssembly: the `dismiss` point, inside the release. Only an accepted request counts, as the module
    // numbers only the dismissals it does not roll back at once.
    // Nothing reaches the module while the dispatch runs: the close re-renders this part inline, and an update sent then
    // would arrive before the module numbers the dismissal it is deciding. One sync follows, after the dispatch returns.
    private DecisionPolicy DecideDismiss(EventSnapshot snapshot)
    {
        OpenChangeEventDetails details = new(EventReason.Swipe, snapshot);
        deciding = true;
        try
        {
            _ = Context!.RequestOpenAsync(false, details);
        }
        finally
        {
            deciding = false;
            SyncTwinLater();
        }

        if (details.IsCanceled)
        {
            return DecisionPolicy.Stop;
        }

        answeredDismiss += 1;
        return DecisionPolicy.Run;
    }

    // DRW-PTR-11, STA-03 on Server (slice 3, 2026-11-20): the module's snap point request a round trip later, answered
    // whatever its outcome; from the answer on the module shows C#'s active snap point, so a canceled request rolls back.
    private async Task SnapAsync(DrawerSnapPointRequest request)
    {
        ChangeEventDetails details = new(EventReason.Swipe, request.Event);
        await Drawer!.RequestSnapPointAsync(SnapPointAt(request.Index), details);
        dropNextDismissal = request.Index is null && details.IsCanceled;
        if (dropNextDismissal)
        {
            canceledSnap = request.Request;
        }

        answeredSnap = Math.Max(answeredSnap, request.Request);
        await SyncTwinAsync();
    }

    // DRW-PTR-11 on WebAssembly: the `snap-point:<index>` point, inside the release; the module numbers no request then.
    private DecisionPolicy DecideSnapPoint(int? index, EventSnapshot snapshot)
    {
        ChangeEventDetails details = new(EventReason.Swipe, snapshot);
        deciding = true;
        try
        {
            _ = Drawer!.RequestSnapPointAsync(SnapPointAt(index), details);
        }
        finally
        {
            deciding = false;
            SyncTwinLater();
        }

        return details.IsCanceled ? DecisionPolicy.Stop : DecisionPolicy.Run;
    }

    private SnapPointValue? SnapPointAt(int? index) =>
        index is { } at && Drawer!.SnapPoints is { } snapPoints && at >= 0 && at < snapPoints.Count ? (SnapPointValue?)snapPoints[at] : null;

    // Yields first, so a sync asked for inside the `dismiss` dispatch runs after the dispatch has returned to the module.
    private void SyncTwinLater() => _ = InvokeAsync(async () =>
    {
        await Task.Yield();
        await SyncTwinAsync();
    });

    private bool SameElements() =>
        string.Equals(sentPopup?.Id, Drawer!.PopupElement?.Id, StringComparison.Ordinal)
        && string.Equals(sentBackdrop?.Id, Drawer.BackdropElement?.Id, StringComparison.Ordinal);
}

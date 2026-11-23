using System.Globalization;
using Iodine.Headless.Interop;
using Iodine.Headless.Rendering;
using Iodine.Headless.Store;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.JSInterop;

namespace Iodine.Headless;

// TST §3.4 state object (toast/root/ToastRoot.tsx:497-504); TST-STA-26. Three fields are mirrored: the `transitions`
// module writes data-starting-style and data-ending-style from the transition status, and the `toast` module writes
// data-swiping and data-swipe-direction (M6 track 9b); C# projects none of them.
/// <summary>Represents the state of a toast's root, which the part exposes as data attributes.</summary>
/// <param name="TransitionStatus">The toast's enter or exit phase, or <see langword="null"/> while it is settled.</param>
/// <param name="Expanded">A value indicating whether the stack of toasts is expanded.</param>
/// <param name="Limited">A value indicating whether the toast is beyond the provider's limit.</param>
/// <param name="Type">The toast's type, or <see langword="null"/>.</param>
/// <param name="Swiping">A value indicating whether a swipe gesture is in progress.</param>
/// <param name="SwipeDirection">The armed or dismissing swipe direction, or <see langword="null"/>.</param>
[PartState]
public sealed partial record ToastRootState(
    [property: MirroredField] TransitionStatus? TransitionStatus,
    bool Expanded,
    bool Limited,
    string? Type,
    [property: MirroredField] bool Swiping,
    [property: MirroredField] SwipeDirection? SwipeDirection);

// toast/root/ToastRootDataAttributes.ts:7-27 (TST-DOM-06, TST-DOM-07).
/// <summary>Provides the data attributes a toast's root renders.</summary>
public static class ToastRootDataAttributes
{
    /// <summary>Present while the stack of toasts is expanded.</summary>
    public const string Expanded = "data-expanded";

    /// <summary>Present while the toast is beyond the provider's limit.</summary>
    public const string Limited = "data-limited";

    /// <summary>The toast's type, when it has one.</summary>
    public const string Type = "data-type";

    /// <summary>Present while a swipe gesture is in progress.</summary>
    public const string Swiping = "data-swiping";

    /// <summary>The armed or dismissing swipe direction: <c>up</c>, <c>down</c>, <c>left</c> or <c>right</c>.</summary>
    public const string SwipeDirection = "data-swipe-direction";

    /// <summary>Present from the toast's add until it is first measured.</summary>
    public const string StartingStyle = "data-starting-style";

    /// <summary>Present while the toast is closing.</summary>
    public const string EndingStyle = "data-ending-style";
}

// toast/root/ToastRootCssVars.ts:5-25 (TST-DOM-10..13).
/// <summary>Provides the CSS variables a toast's root sets.</summary>
public static class ToastRootCssVars
{
    /// <summary>The toast's position among the toasts that are not closing, newest first; while closing, its position in the list.</summary>
    public const string Index = "--toast-index";

    /// <summary>The sum of the heights of the toasts before this one, in pixels.</summary>
    public const string OffsetY = "--toast-offset-y";

    /// <summary>The toast's measured height, in pixels, once it is measured and while it is not closing.</summary>
    public const string Height = "--toast-height";

    /// <summary>The horizontal swipe offset, in pixels; <c>0px</c> at rest.</summary>
    public const string SwipeMovementX = "--toast-swipe-movement-x";

    /// <summary>The vertical swipe offset, in pixels; <c>0px</c> at rest.</summary>
    public const string SwipeMovementY = "--toast-swipe-movement-y";
}

// Base UI's Toast.Root (toast/root/ToastRoot.tsx); TST-STR-03, TST-API-13, TST-DOM-06, TST-DOM-08..12, TST-ARIA-02..06,
// TST-EDGE-10. Every lookup is by the toast's id (STR-03, EDGE-10): the Root selects its toast, its indexes and its offset
// from the Provider's store by id, so a consumer may pass a fresh copy on every render, and it reads the store's copy when
// there is one. C# renders the role, tabindex, aria-modal, the label references, the high-priority aria-hidden, inert and
// the state attributes. The `toast` module (M6 track 9b; ADR 04 Amendments 2026-11-18) takes the style over at attach:
// from then on C# freezes the rendered style and sends its merged string (the stack variables, then the consumer's) as the
// writer's consumer band, under which the module writes the swipe's movement variables, transition and transform
// ('component-under') and over which the measurement's transient height ('component'). The module reports the
// measurement, the gesture and Escape; each report carries a revision the next options send back, and a new lifecycle (a
// new toast id, or a re-add) is the rollback (decision 6 (a), chosen by the user 2026-11-18). A `transitions` handle on the
// same element writes data-starting-style and data-ending-style from the store's status, with the handover at attach, and
// its exit completion removes the toast (the M6 plan's decision 19 (a), chosen by the user 2026-11-18). The Root registers
// its element in the store under its toast's id, for the focus hand-off (DP3 (i), chosen by the user 2026-11-19). The Root
// publishes a non-generic view of its toast to its parts (ToastRootContext).
// ADR 08 Amendments 2026-11-18 (chosen by the user 2026-11-18): TData is inferred through the Toast parameter.
/// <summary>Represents one toast. Base UI's <c>Toast.Root</c>; it renders a <c>div</c>.</summary>
/// <typeparam name="TData">The type of the custom data the toast carries.</typeparam>
public sealed class ToastRoot<TData> : PartBase<ToastRootState>
{
    private const string RestingMovement = "--toast-swipe-movement-x: 0px; --toast-swipe-movement-y: 0px";

    private readonly RenderFragment renderPart;
    private readonly Action<ToastReport> onReport;
    private readonly Action<bool> onTransitionCompleted;
    private ToastProviderContext<TData>? provider;
    private ToastPartHandle? handle;
    private TransitionsTwin? transitionsTwin;
    private TwinHandle<TransitionsOptions>? transitionsHandle;
    private Task? transitionsAttaching;
    private ElementReference? transitionsElement;
    private TransitionsOptions? transitionsSent;
    private string? registeredId;
    private string? lastId;
    private TransitionStatus? lastStatus;
    private TransitionStatus? initialStatus;
    private bool initialized;
    private int lifecycle;
    private int appliedRevision;
    private string? sentStyle;
    private ToastOptions? options;
    private IReadOnlyList<SwipeDirection>? directionsFor;
    private string[] directionLiterals = [];
    private bool disposed;
    private ToastRootContext? context;
    private SelectorSubscription<ToastStoreState<TData>>? subscription;
    private StoreSelection<ToastStoreState<TData>, string, ToastObject<TData>?>? stored;
    private StoreSelection<ToastStoreState<TData>, string, int>? domIndex;
    private StoreSelection<ToastStoreState<TData>, string, int>? visibleIndex;
    private StoreSelection<ToastStoreState<TData>, string, double>? offsetY;
    private StoreSelection<ToastStoreState<TData>, bool>? expanded;
    private StoreSelection<ToastStoreState<TData>, bool>? focused;
    private RootView rendered;
    private RootView view;
    private static readonly IReadOnlyList<SwipeDirection> s_defaultSwipeDirections = [Headless.SwipeDirection.Down, Headless.SwipeDirection.Right];
    private static readonly IReadOnlyList<SwipeDirection> s_noSwipeDirections = [];

    private ToastRootState state = new(null, false, false, null, false, null);

    /// <summary>Initializes a new instance of the <see cref="ToastRoot{TData}"/> class.</summary>
    public ToastRoot()
    {
        renderPart = RenderPart;
        onReport = report => _ = InvokeAsync(() => ApplyReport(report));
        onTransitionCompleted = open => _ = InvokeAsync(() => CompleteTransition(open));
    }

    // The store's copy of the toast when it holds one, else the parameter.
    private ToastObject<TData> Current => view.Toast ?? Toast;

    [Inject]
    private IJSRuntime JSRuntime { get; set; } = null!;

    [CascadingParameter]
    private ToastProviderContext<TData>? ProviderContext { get; set; }

    /// <summary>Gets or sets the toast to render.</summary>
    /// <value>
    /// The toast, typically one of the viewport's toasts. It is found by its <see cref="ToastObject{TData}.Id"/>, so a copy
    /// of it may be passed.
    /// </value>
    [EditorRequired]
    [Parameter]
    public ToastObject<TData> Toast { get; set; } = null!;

    // TST-API-14, TST-I18N-01: ordered (a release takes the first allowed direction in the order given), physical, so a
    // list rather than a [Flags] enum; Base UI's single string is a one-element list.
    /// <summary>Gets or sets the directions in which a swipe dismisses the toast.</summary>
    /// <value>
    /// The directions, in the order a release tries them; or <see langword="null"/> (the default) for down, then right. The
    /// directions are physical. A toast whose positioner settings set an anchor cannot be swiped away.
    /// </value>
    [Parameter]
    public IReadOnlyList<SwipeDirection>? SwipeDirections { get; set; }

    /// <inheritdoc />
    protected override string DefaultTagName => "div";

    /// <inheritdoc />
    protected override ToastRootState State
    {
        get
        {
            ToastObject<TData> toast = Current;
            bool isExpanded = view.Expanded;
            // The swipe fields are mirrored from the module's reports (ApplyReport).
            if (state.TransitionStatus != toast.TransitionStatus || state.Expanded != isExpanded || state.Limited != toast.Limited || !string.Equals(state.Type, toast.Type, StringComparison.Ordinal))
            {
                state = state with { TransitionStatus = toast.TransitionStatus, Expanded = isExpanded, Limited = toast.Limited, Type = toast.Type };
            }

            return state;
        }
    }

    // TST-API-14, TST-PTR-22: the directions the `toast` module's gesture uses (M6 track 9b); an anchored toast has none.
    internal IReadOnlyList<SwipeDirection> EffectiveSwipeDirections =>
        Current.Positioner is { HasAnchor: true } ? s_noSwipeDirections : SwipeDirections ?? s_defaultSwipeDirections;

    // For the fixtures: completes when the root's `toast` handle has attached.
    internal Task TwinAttached => handle!.Attached;

    // For the tests: the part's twin, whose reports the tests send.
    internal ToastTwin? Twin => handle?.Twin;

    // For the tests: the merged style the root sends as the writer's consumer band (the stack variables, then the consumer's).
    internal string? SentStyle => sentStyle;

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        provider = ToastProviderContext<TData>.Require(ProviderContext);
        ToastStore<TData> store = provider.Store;
        context = new ToastRootContext(id => store.CloseToast(id)) { RootElement = () => Element };
        context.TitleId.Changed = StateHasChanged;
        context.DescriptionId.Changed = StateHasChanged;
        handle = new ToastPartHandle(JSRuntime, handsOverStyle: true);
        handle.Twin.Reported += onReport;
        base.OnInitialized();
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        ArgumentNullException.ThrowIfNull(Toast);

        // TST-ANI-08: a Root reused for another toast (an index-keyed list) follows the new id.
        string id = Toast.Id;
        stored!.Arg = id;
        domIndex!.Arg = id;
        visibleIndex!.Arg = id;
        offsetY!.Arg = id;

        // TST-ANI-08, TST-ANI-09: the element-handle cell follows the id this Root renders.
        if (!string.Equals(registeredId, id, StringComparison.Ordinal))
        {
            ToastStore<TData> store = provider!.Store;
            if (registeredId is not null)
            {
                store.UnregisterElement(registeredId, this);
            }

            store.RegisterElement(id, this, () => Element);
            registeredId = id;
        }
    }

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        // The Viewport's render reaches this Root before the Root's own subscription is notified of the same write, so the
        // Root reads the store's current state, not its selections' last values.
        view = Read();
        rendered = view;
        TrackLifecycle();
        context!.Publish(Current, view.Expanded, view.VisibleIndex);
        builder.OpenComponent<CascadingValue<ToastRootContext>>(0);
        builder.AddComponentParameter(1, nameof(CascadingValue<ToastRootContext>.Value), context);
        builder.AddComponentParameter(2, nameof(CascadingValue<ToastRootContext>.IsFixed), true);
        builder.AddComponentParameter(3, nameof(CascadingValue<ToastRootContext>.ChildContent), renderPart);
        builder.CloseComponent();
    }

    /// <inheritdoc />
    protected override Task OnAfterRenderAsync(bool firstRender)
    {
        context!.Flush();
        if (disposed)
        {
            return Task.CompletedTask;
        }

        // The `transitions` update goes first: a re-add can find the element hidden by an exit that a keyed move of the
        // element cut short, and the re-open shows it again before the `toast` module measures the new lifecycle.
        Task transitions = SyncTransitionsAsync();
        Task module = SyncAsync();
        return module.IsCompletedSuccessfully ? transitions : transitions.IsCompletedSuccessfully ? module : Task.WhenAll(module, transitions);
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing && !disposed)
        {
            disposed = true;
            subscription?.Dispose();
            subscription = null;
            if (context is not null)
            {
                context.TitleId.Changed = null;
                context.DescriptionId.Changed = null;
            }

            if (registeredId is not null)
            {
                provider?.Store.UnregisterElement(registeredId, this);
            }

            // Issue 132: every handle's disposal starts together.
            if (handle is not null)
            {
                handle.Twin.Reported -= onReport;
                handle.Dispose();
            }

            if (transitionsTwin is not null)
            {
                transitionsTwin.TransitionCompleted -= onTransitionCompleted;
                _ = transitionsTwin.DisposeAsync().AsTask();
            }
        }

        base.Dispose(disposing);
    }

    /// <inheritdoc />
    protected override void SubscribeSelectors()
    {
        // One subscription (ADR 05 §1), whose callback renders only when what the Root last rendered changed: a write that
        // also renders the Viewport has already rendered this Root (the FieldRoot precedent; ADR 16 rule 8).
        subscription = new SelectorSubscription<ToastStoreState<TData>>(provider!.Store, OnSelectionChanged);
        string id = Toast?.Id ?? string.Empty;
        stored = subscription.Select(ToastSelectors<TData>.Toast, id);
        domIndex = subscription.Select(ToastSelectors<TData>.ToastIndex, id);
        visibleIndex = subscription.Select(ToastSelectors<TData>.ToastVisibleIndex, id);
        offsetY = subscription.Select(ToastSelectors<TData>.ToastOffsetY, id);
        expanded = subscription.Select(ToastSelectors<TData>.Expanded);
        focused = subscription.Select(ToastSelectors<TData>.Focused);
    }

    /// <inheritdoc />
    protected override void AddPartAttributes(AttributeList attributes)
    {
        ToastObject<TData> toast = Current;
        bool high = toast.Priority == ToastPriority.High;

        // TST-ARIA-02, TST-DOM-08, TST-ARIA-03..06.
        attributes.Set("role", high ? "alertdialog" : "dialog");
        attributes.Set("tabindex", "0");
        attributes.Set("aria-modal", "false");
        if (context!.TitleId.Id is { } titleId)
        {
            attributes.Set("aria-labelledby", titleId);
        }

        if (context.DescriptionId.Id is { } descriptionId)
        {
            attributes.Set("aria-describedby", descriptionId);
        }

        if (high && !view.Focused)
        {
            attributes.Set("aria-hidden", "true");
        }

        // TST-DOM-09 (UTL-DOM-16): present and empty while limited.
        if (toast.Limited)
        {
            attributes.Set("inert", string.Empty);
        }

        // TST-DOM-07: the status the element was created with, under the handover; the `transitions` module owns the two
        // attributes from its attach (ADR 04 Amendments 2026-10-18).
        if (initialStatus == TransitionStatus.Starting)
        {
            attributes.Set(ToastRootDataAttributes.StartingStyle, string.Empty);
        }
        else if (initialStatus == TransitionStatus.Ending)
        {
            attributes.Set(ToastRootDataAttributes.EndingStyle, string.Empty);
        }

        // TST-DOM-10..12: the visible index, or the DOM index while ending; the offset; the height when positive.
        int index = toast.TransitionStatus == TransitionStatus.Ending ? view.DomIndex : view.VisibleIndex;
        string style = toast.Height is > 0 and double height
            ? string.Create(CultureInfo.InvariantCulture, $"{ToastRootCssVars.Index}: {index}; {ToastRootCssVars.OffsetY}: {view.OffsetY}px; {ToastRootCssVars.Height}: {height}px")
            : string.Create(CultureInfo.InvariantCulture, $"{ToastRootCssVars.Index}: {index}; {ToastRootCssVars.OffsetY}: {view.OffsetY}px");
        attributes.Set("style", style);
    }

    // The handover of the style: the merged string (the stack variables, then the consumer's) is the writer's consumer band;
    // the element shows it with the resting movement variables until the attach, and keeps that string from then on.
    /// <inheritdoc />
    protected override void FinishAttributes(AttributeList attributes)
    {
        string? merged = attributes.TryGetValue(MergeProps.StyleAttribute, out AttributeValue style) && style.TryGetValue(out string text) ? text : null;
        sentStyle = merged;
        // Frozen from the attach on: no merge then. Before it, the resting variables lead, so a consumer declaration wins.
        string? rendered = handle!.IsFrozen ? handle.RenderStyle(null) : handle.RenderStyle(merged is null ? RestingMovement : $"{RestingMovement}; {merged}");
        if (rendered is null)
        {
            attributes.Remove(MergeProps.StyleAttribute);
        }
        else
        {
            attributes.Set(MergeProps.StyleAttribute, rendered);
        }
    }

    private void RenderPart(RenderTreeBuilder builder) => base.BuildRenderTree(builder);

    // TST-ANI-07, TST-ANI-08, TST-ANI-11: a new toast id, or a re-add (a status back to starting), starts a lifecycle, which
    // resets the mirrored swipe state and is the module's rollback.
    private void TrackLifecycle()
    {
        ToastObject<TData> toast = Current;
        if (!initialized)
        {
            initialized = true;
            initialStatus = toast.TransitionStatus;
        }
        else if (!string.Equals(lastId, toast.Id, StringComparison.Ordinal)
            || (toast.TransitionStatus == TransitionStatus.Starting && lastStatus != TransitionStatus.Starting))
        {
            lifecycle++;
            state = state with { Swiping = false, SwipeDirection = null };
        }

        lastId = toast.Id;
        lastStatus = toast.TransitionStatus;
    }

    // A report of the root's role (ADR 04 Amendments 2026-11-18, the `toast` scope).
    private Task ApplyReport(ToastReport report)
    {
        if (disposed)
        {
            return Task.CompletedTask;
        }

        ToastStore<TData> store = provider!.Store;
        string id = Current.Id;
        ToastRootState previous = state;
        switch (report.Kind)
        {
            // TST-ANI-02, TST-ANI-03, TST-ANI-10: a height measured in an earlier lifecycle is another toast's.
            case "height":
                if (report.Lifecycle == lifecycle && report.Height is { } height)
                {
                    store.SetMeasuredHeight(id, height);
                }

                break;

            // TST-PTR-10: hovering, without a pause (ToastRoot.tsx:276).
            case "swipe-start":
                state = state with { Swiping = true };
                store.SetHovering(true);
                break;

            // TST-PTR-14, TST-PTR-15.
            case "direction":
                state = state with { SwipeDirection = SwipeDirectionLiterals.FromLiteral(report.Direction) };
                break;

            // TST-PTR-17..19: a dismissal closes the toast; the module keeps its direction through the exit.
            case "release":
                state = state with { Swiping = false, SwipeDirection = report.Dismiss == true ? SwipeDirectionLiterals.FromLiteral(report.Direction) : null };
                if (report.Dismiss == true)
                {
                    store.CloseToast(id);
                }

                break;

            // TST-KEY-05.
            case "escape":
                store.CloseToast(id);
                break;

            // TST-PTR-09.
            case "touch-press":
                store.PauseTimers();
                break;
        }

        appliedRevision = Math.Max(appliedRevision, report.Revision);
        store.OnReportApplied(report.Kind);

        // The mirrored fields reach only a consumer's state functions (RND-STA-01; the issue 291 pattern).
        if (!ReferenceEquals(previous, state) && (ClassValue is not null || StyleValue is not null || RenderFunction is not null))
        {
            StateHasChanged();
        }

        return SyncAsync();
    }

    // TST-ANI-05, TST-ANI-06 (ToastRoot.tsx:105-113): the exit's completion removes the toast if it is still ending.
    private void CompleteTransition(bool open)
    {
        if (disposed || open)
        {
            return;
        }

        string id = Current.Id;
        if (provider!.Store.State.Toasts.Find(id) is { TransitionStatus: TransitionStatus.Ending })
        {
            provider.Store.RemoveToast(id);
        }
    }

    private Task SyncAsync()
    {
        // A render that changed nothing the module reads reuses the options it last built (a hover renders every Root).
        if (options is { } built && built.Revision == appliedRevision && built.Lifecycle == lifecycle
            && string.Equals(built.Style, sentStyle, StringComparison.Ordinal) && ReferenceEquals(EffectiveSwipeDirections, directionsFor))
        {
            return handle!.SyncAsync(Element, built, []);
        }

        IReadOnlyList<SwipeDirection> directions = EffectiveSwipeDirections;
        if (!ReferenceEquals(directions, directionsFor))
        {
            directionsFor = directions;
            string[] literals = new string[directions.Count];
            for (int i = 0; i < literals.Length; i++)
            {
                literals[i] = SwipeDirectionLiterals.ToLiteral(directions[i]);
            }

            if (!literals.AsSpan().SequenceEqual(directionLiterals))
            {
                directionLiterals = literals;
            }
        }

        options = new()
        {
            Part = ToastTwin.RootPart,
            Revision = appliedRevision,
            Directions = directionLiterals,
            Lifecycle = lifecycle,
            Style = sentStyle,
        };
        return handle!.SyncAsync(Element, options, []);
    }

    // The `transitions` handle: open while the toast is not ending; the status the element was created with at the attach.
    private Task SyncTransitionsAsync()
    {
        bool open = Current.TransitionStatus != TransitionStatus.Ending;
        if (transitionsHandle is not null && ToastPartHandle.SameElement(Element, transitionsElement) && transitionsSent?.Open == open)
        {
            return Task.CompletedTask;
        }

        return SyncTransitionsCoreAsync();
    }

    private async Task SyncTransitionsCoreAsync()
    {
        ElementReference? element = Element;
        if (!ToastPartHandle.SameElement(element, transitionsElement))
        {
            TwinHandle<TransitionsOptions>? released = transitionsHandle;
            transitionsHandle = null;
            transitionsAttaching = null;
            transitionsSent = null;
            transitionsElement = element;
            if (released is not null)
            {
                await released.DisposeAsync();
            }
        }

        if (element is not { } target)
        {
            return;
        }

        TransitionsOptions options = new()
        {
            Open = Current.TransitionStatus != TransitionStatus.Ending,
            Mounted = true,
            Status = initialStatus,
        };
        if (transitionsHandle is null)
        {
            if (transitionsAttaching is null)
            {
                if (transitionsTwin is null)
                {
                    transitionsTwin = new TransitionsTwin(JSRuntime);
                    transitionsTwin.TransitionCompleted += onTransitionCompleted;
                }

                transitionsSent = options;
                transitionsAttaching = AttachTransitionsAsync(target, options);
            }

            await transitionsAttaching;
            if (transitionsHandle is null || !ToastPartHandle.SameElement(element, transitionsElement))
            {
                return;
            }
        }

        if (options.Open != transitionsSent?.Open)
        {
            transitionsSent = options;
            await transitionsHandle.UpdateAsync(options);
        }
    }

    private async Task AttachTransitionsAsync(ElementReference target, TransitionsOptions options)
    {
        TwinHandle<TransitionsOptions> attached = await transitionsTwin!.AttachAsync(target, options);
        if (ToastPartHandle.SameElement(transitionsElement, target) && !disposed)
        {
            transitionsHandle = attached;
        }
        else
        {
            await attached.DisposeAsync();
        }
    }

    private RootView Read()
    {
        ToastStoreState<TData> current = provider!.Store.State;
        string id = Toast.Id;
        return new RootView(
            ToastSelectors<TData>.Toast(current, id),
            ToastSelectors<TData>.ToastIndex(current, id),
            ToastSelectors<TData>.ToastVisibleIndex(current, id),
            ToastSelectors<TData>.ToastOffsetY(current, id),
            ToastSelectors<TData>.Expanded(current),
            ToastSelectors<TData>.Focused(current));
    }

    private void OnSelectionChanged()
    {
        if (!Read().Equals(rendered))
        {
            StateHasChanged();
        }
    }

    // What the Root renders from the store; the toast compares as a record, so an equal copy renders nothing new.
    private readonly record struct RootView(ToastObject<TData>? Toast, int DomIndex, int VisibleIndex, double OffsetY, bool Expanded, bool Focused);
}

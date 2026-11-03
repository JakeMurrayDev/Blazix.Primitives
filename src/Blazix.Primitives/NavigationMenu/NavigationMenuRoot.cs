using Blazix.Primitives.Interop;
using Blazix.Primitives.Rendering;
using Blazix.Primitives.Store;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace Blazix.Primitives;

// NAV-STA-16, §3.1 state object (navigation-menu/root/NavigationMenuRoot.tsx:351-354): the default mapping only, so
// `data-open` and `data-nested` (NAV-DOM-01, §16 Q2).
/// <summary>Represents the state of a navigation menu root, which the part exposes as data attributes and passes to its class and style functions.</summary>
/// <param name="Open">Whether the menu is open; projected as <c>data-open</c>.</param>
/// <param name="Nested">Whether the root is nested in another menu; projected as <c>data-nested</c>.</param>
[PartState]
public sealed partial record NavigationMenuRootState(bool Open, bool Nested);

// Base UI's NavigationMenu.Root (navigation-menu/root/NavigationMenuRoot.tsx); the contract is docs/components/navigation-menu.md
// §3.1, §4, §6.4, §7.
// - A `nav`, or a `div` when nested (NAV-STR-01), with no handler and no role (NAV-DOM-02). Nested is "an enclosing popup's
//   token exists" (design point 7, a fidelity correction settled by the orchestrator 2026-11-04: Base UI's nested is
//   useFloatingParentNodeId() != null), read from the family-agnostic cascade a Content renders (ADR 04 Amendments
//   2026-11-03 (5)).
// - The value is a Controlled<TValue?> fixed at the first parameter set; `null` is closed and a non-nullable value-type
//   TValue is an always-on error (ADR 08 Amendments 2026-11-03 (2), chosen by the user 2026-11-03); falsy values open
//   (NAV-STA-03); requests dedupe by Equality.StrictEquals<TValue> (NAV-EVT-06).
// - The store composes track 2's open lifecycle (ADR 05 Amendments 2026-11-03), created at the first parameter set through
//   PopupLifecycleRootBehavior; a request runs OnValueChange, a cancel stops it, then the commit and the relay (FLT-EVT-02's
//   port order), then a nested link-press close propagates to the parent with the same details (NAV-EVT-10).
// - One interactions session per Root (design point 2, settled by the orchestrator 2026-11-04): Escape with the session's
//   verdicts, the outside press intentional for both pointer types, the cross-menu selector (track 2's option), enabled
//   only while a Positioner is registered or the menu is closed (NAV-STA-08, NAV-DIS-08, §16 Q4 replicated).
// - The `navigation-menu` module's Root handle (ADR 04 Amendments 2026-11-03 (4)): the menu's state on the Root element,
//   focus return at the close's completion (NAV-FOC-05, 06).
/// <summary>
/// Groups all parts of a navigation menu and owns its value: Base UI's <c>NavigationMenu.Root</c>, rendered as a
/// <c>nav</c>, or a <c>div</c> when nested in another navigation menu.
/// </summary>
/// <typeparam name="TValue">The type of the value that identifies the open item; it must accept <see langword="null"/>.</typeparam>
/// <remarks>
/// <para>
/// <see langword="null"/> means closed, so <typeparamref name="TValue"/> must be a reference type such as <see cref="string"/>
/// or a nullable value type such as <c>int?</c>. Every other value, <c>0</c>, <c>""</c> and <see langword="false"/>
/// included, opens the item whose value it is.
/// </para>
/// <para>
/// <see cref="OnValueChange"/> receives a <see cref="ValueChangeEventArgs{TValue}"/> whose <c>Details</c> is an
/// <see cref="OpenChangeEventDetails"/> at run time: cast it, as in
/// <c>((OpenChangeEventDetails)args.Details).PreventUnmountOnClose()</c>, to keep the popup mounted after a close until
/// <see cref="Unmount"/> is called.
/// </para>
/// </remarks>
[CascadingTypeParameter(nameof(TValue))]
public sealed class NavigationMenuRoot<TValue> : PartBase<NavigationMenuRootState>
{
    // NAV-PTR-16, NAV-PTR-17: an intentional outside press for both pointer types; a press on any NavigationMenu trigger is
    // never outside (track 2's option, the cross-menu selector of NavigationMenuList.tsx:54-65).
    private static readonly InteractionsDismissOptions s_dismiss = new()
    {
        OutsidePressEvent = new InteractionsPressEvents("intentional", "intentional"),
        OutsidePressExemptSelector = "[" + NavigationMenuMarkers.Trigger + "]",
    };

    private readonly PopupLifecycleRootBehavior<NavigationMenuStore, NavigationMenuStoreState> behavior;
    private readonly RenderFragment behaviorContent;
    private Controlled<TValue?>? valueState;
    private NavigationMenuRootContext<TValue>? context;
    private InteractionsSessionBehavior? session;
    private NavigationMenuModule? module;
    private SelectorSubscription<NavigationMenuStoreState>? committed;
    private StoreSelection<NavigationMenuStoreState, bool>? open;
    private NavigationMenuRootState? state;
    private RenderFragment? cascadedContent;
    private bool rendered;
    private bool disposed;

    /// <summary>Initializes a new instance of the <see cref="NavigationMenuRoot{TValue}"/> class.</summary>
    public NavigationMenuRoot()
    {
        behavior = new((popupId, token, logger) => new NavigationMenuStore(popupId, token, logger), StateHasChanged);
        behaviorContent = behavior.BuildRenderTree;
    }

    [Inject]
    private PartIdGenerator Ids { get; set; } = null!;

    [Inject]
    private ILogger<NavigationMenuRoot<TValue>> Logger { get; set; } = null!;

    [Inject]
    private IJSRuntime JSRuntime { get; set; } = null!;

    [CascadingParameter]
    private NavigationMenuRootContext? EnclosingRoot { get; set; }

    // ADR 04 Amendments 2026-11-03 (5): the enclosing popup of any family, its session the parent node.
    [CascadingParameter]
    private IFloatingNodeContext? FloatingParent { get; set; }

    [CascadingParameter]
    private DirectionProviderContext? DirectionContext { get; set; }

    // NAV-API-01, NAV-STA-01..03; ADR 05 §2, ADR 08 Amendments 2026-11-03 (2)
    /// <summary>Gets or sets the value of the open item, when the consumer owns the value.</summary>
    /// <value>
    /// The value; <see langword="null"/> closes the menu. Supplying it at the first render makes the value the consumer's for
    /// the lifetime of the root: a user interaction then only raises <see cref="OnValueChange"/> and <see cref="ValueChanged"/>.
    /// </value>
    [Parameter]
    public TValue? Value { get; set; }

    // ADR 05 §2: the binding half of @bind-Value.
    /// <summary>Gets or sets the callback that receives the new value after a change that was not canceled.</summary>
    /// <value>The callback <c>@bind-Value</c> supplies, invoked after <see cref="OnValueChange"/>.</value>
    [Parameter]
    public EventCallback<TValue?> ValueChanged { get; set; }

    // NAV-API-02
    /// <summary>Gets or sets the value of the item open at the first render, when the root owns the value.</summary>
    /// <value>The value; <see langword="null"/>, the default, starts closed. Read at the first render only.</value>
    [Parameter]
    public TValue? DefaultValue { get; set; }

    // NAV-API-03, NAV-EVT-01..11
    /// <summary>Gets or sets the callback invoked when the value is about to change.</summary>
    /// <value>
    /// The callback, which receives the requested value and an <see cref="OpenChangeEventDetails"/> as its <c>Details</c>;
    /// calling <see cref="ChangeEventDetails.Cancel"/> keeps the current value. It is not invoked for a request of the value
    /// the root already has.
    /// </value>
    [Parameter]
    public EventCallback<ValueChangeEventArgs<TValue?>> OnValueChange { get; set; }

    // NAV-API-05, NAV-EVT-07, §16 Q8
    /// <summary>Gets or sets the callback invoked once a close has finished: its exit animations, or <see cref="Unmount"/>.</summary>
    /// <value>Receives <see langword="false"/>; it is never invoked after an open.</value>
    [Parameter]
    public EventCallback<bool> OnOpenChangeComplete { get; set; }

    // NAV-API-06; ADR 08 Amendments 2026-10-21 and 2026-10-27
    /// <summary>Gets or sets how long the pointer must rest on a trigger before its item opens.</summary>
    /// <value>The delay; <see langword="null"/>, the default, is 50 milliseconds.</value>
    [Parameter]
    public Duration? Delay { get; set; }

    // NAV-API-07
    /// <summary>Gets or sets how long after the pointer leaves the menu it closes.</summary>
    /// <value>The delay; <see langword="null"/>, the default, is 50 milliseconds.</value>
    [Parameter]
    public Duration? CloseDelay { get; set; }

    // NAV-API-08; ADR 08 Amendments 2026-10-26 (non-nullable, the family default).
    /// <summary>Gets or sets the axis of the top-level list.</summary>
    /// <value>
    /// <see cref="Primitives.Orientation.Horizontal"/>, the default: the left and right arrow keys move between the
    /// triggers and the down arrow opens one; or <see cref="Primitives.Orientation.Vertical"/>.
    /// </value>
    [Parameter]
    public Orientation Orientation { get; set; } = Orientation.Horizontal;

    /// <inheritdoc />
    protected override string DefaultTagName => context!.Nested ? "div" : "nav";

    // The pipeline reads the state before the part's attributes, so the record follows the store here.
    /// <inheritdoc />
    protected override NavigationMenuRootState State
    {
        get
        {
            bool isOpen = open!.Value;
            bool nested = context!.Nested;
            if (state is null || state.Open != isOpen || state.Nested != nested)
            {
                state = new NavigationMenuRootState(isOpen, nested);
            }

            return state;
        }
    }

    /// <summary>Gets a task that completes when the root's session and module handle are attached in the browser.</summary>
    internal Task TwinsAttached => Task.WhenAll(session?.TwinAttached ?? Task.CompletedTask, module?.Attached ?? Task.CompletedTask);

    /// <summary>Gets the root's store.</summary>
    internal NavigationMenuStore Store => behavior.Store;

    /// <inheritdoc />
    public override Task SetParametersAsync(ParameterView parameters)
    {
        if (valueState is null)
        {
            // ADR 08 Amendments 2026-11-03 (2): `null` is closed, so a TValue that cannot be null has no closed state.
            if (default(TValue) is not null)
            {
                throw new InvalidOperationException(
                    "Blazix: `NavigationMenuRoot` needs a `TValue` that can be `null`, which means closed: use a reference type such as `string` or a nullable value type such as `int?`.");
            }

            valueState = new Controlled<TValue?>("NavigationMenu", "value", Logger);
        }

        valueState.SetParameters(parameters, nameof(Value), nameof(DefaultValue));
        behavior.EnsureStore(Ids, Logger);
        return base.SetParametersAsync(parameters);
    }

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        NavigationMenuStore store = behavior.Store;
        context = new NavigationMenuRootContext<TValue>(store, () => Element, RequestAsync);
        store.ConsumerStep = RunLifecycleRequestAsync;
        store.Unmounted += OnUnmounted;
        session = new InteractionsSessionBehavior(JSRuntime, store.SessionToken!, request => context.RouteAsync(request), DispatchExceptionAsync);
        module = new NavigationMenuModule(JSRuntime);
        open = behavior.Subscription.Select(NavigationMenuSelectors.Open);
        committed = new SelectorSubscription<NavigationMenuStoreState>(store, OnCommitted);
        committed.Select(NavigationMenuSelectors.Open);
        committed.Select(NavigationMenuSelectors.Mounted);
        committed.Select(NavigationMenuSelectors.ActiveTriggerId);
        committed.Select(NavigationMenuSelectors.OpenChange);
        committed.Select(NavigationMenuSelectors.TransitionStatus);
        committed.Select(NavigationMenuSelectors.PopupId);
        committed.Select(NavigationMenuSelectors.HasPositioner);
        committed.Select(NavigationMenuSelectors.CloseReason);
        committed.Select(NavigationMenuSelectors.Answered);
        base.OnInitialized();
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        NavigationMenuStore store = behavior.Store;
        context!.Parent = EnclosingRoot;
        context.Nested = FloatingParent?.SessionToken is not null;
        context.Orientation = Orientation;
        context.Direction = DirectionProviderContext.Read(DirectionContext);
        context.DelayMs = (int)(Delay?.ToMilliseconds() ?? NavigationMenuRootContext.DefaultDelay);
        context.CloseDelayMs = (int)(CloseDelay?.ToMilliseconds() ?? NavigationMenuRootContext.DefaultCloseDelay);
        store.OnOpenChangeComplete = OnOpenChangeComplete;

        // NAV-STA-01, NAV-EVT-11: a controlled value is applied as it arrives, with no callback, no close reason, no opt-out;
        // the lifecycle's open follows it.
        bool wasActiveValue = context.Value is not null;
        context.SetValue(valueState!.Value);
        store.ApplyOpenParameters(valueState.IsControlled ? valueState.Value is not null : null, DefaultValue is not null);
        if (wasActiveValue != (context.Value is not null))
        {
            // NavigationMenuRoot.tsx:198-200: viewportInert resets on every value change.
            store.SetViewportInert(false);
        }

        session!.ParentToken = FloatingParent?.SessionToken;

        if (cascadedContent is null || !ReferenceEquals(ChildContent, behaviorContent))
        {
            cascadedContent = ChildContent;
            behavior.ChildContent = NavigationMenuCascades.Cascade<NavigationMenuRootContext>(context, cascadedContent);
        }

        ChildContent = behaviorContent;
    }

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        rendered = true;
        CopySessionInputs();
        Task sync = session!.AfterRenderAsync();
        Task moduleSync = Element is { } element ? module!.SyncAsync(element, ModuleOptions(), null) : Task.CompletedTask;
        await behavior.OnAfterRenderAsync();
        await sync;
        await moduleSync;
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing && !disposed)
        {
            disposed = true;
            committed?.Dispose();
            if (context is not null)
            {
                behavior.Store.Unmounted -= OnUnmounted;
            }

            behavior.Dispose();
            if (session is not null)
            {
                _ = session.DisposeAsync().AsTask();
            }

            if (module is not null)
            {
                _ = module.DisposeAsync().AsTask();
            }
        }

        base.Dispose(disposing);
    }

    // NAV-API-04 (actionsRef.close), NAV-EVT-08.
    /// <summary>Closes the menu, with the reason <see cref="EventReason.ImperativeAction"/>.</summary>
    /// <returns>A task that completes when the close request has run.</returns>
    public Task CloseAsync() => InvokeAsync(() => context!.RequestCloseAsync(new OpenChangeEventDetails(EventReason.ImperativeAction, EventSnapshot.CreatePlaceholder())));

    // NAV-API-04 (actionsRef.unmount), NAV-EVT-09, TRN-ANI-19..22.
    /// <summary>
    /// Ends the closing phase of a menu whose close called <c>PreventUnmountOnClose()</c>; a call while the menu is open is
    /// kept until the end of the render batch and dropped unless the menu closed in it.
    /// </summary>
    public void Unmount()
    {
        Task task = InvokeAsync(() => behavior.Store.UnmountAsync());
        if (!task.IsCompletedSuccessfully)
        {
            _ = task.ContinueWith(t => DispatchExceptionAsync(t.Exception!.GetBaseException()), CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default);
        }
    }

    // NAV-EVT-01..10; NavigationMenuRoot.tsx:202-243.
    private async Task<bool> RequestAsync(TValue? next, OpenChangeEventDetails details)
    {
        if (disposed)
        {
            return false;
        }

        NavigationMenuStore store = behavior.Store;
        TValue? current = valueState!.Value;
        bool closing = next is null;

        // NAV-STA-15: every close request records its reason, a redundant one included.
        if (closing)
        {
            store.SetCloseReason(details.Reason);
        }

        bool changed = !Equality.StrictEquals(current, next);
        if (changed && !await valueState.NotifyAsync(next, details, OnValueChange))
        {
            return false;
        }

        if (details.IsCanceled)
        {
            return false;
        }

        if (closing)
        {
            // NAV-STA-06: the direction resets with the close.
            store.SetActivationDirection(null);
        }

        if (changed)
        {
            await valueState.CommitAsync(next, ValueChanged);
            if (!valueState.IsControlled || current is not null && next is not null)
            {
                // The lifecycle's commit: an open change, or a switch between two open values, relayed as a revision (design
                // point 2); a controlled open change follows the parameter that the consumer sets.
                store.CommitOpenChange(!closing, details);
            }

            context!.SetValue(valueState.Value);
            store.SetViewportInert(false);
        }

        // NAV-EVT-10, NAV-DIS-09: a nested link-press close reaches the parent with the same details.
        if (closing && context!.Nested && details.Reason == EventReason.LinkPress && EnclosingRoot is { } parent)
        {
            await parent.RequestCloseAsync(details);
        }

        return true;
    }

    // A request the lifecycle itself starts (its CloseAsync): only a close has a value to request.
    private Task<bool> RunLifecycleRequestAsync(bool isOpen, OpenChangeEventDetails details) =>
        isOpen ? Task.FromResult(false) : RequestAsync(default, details);

    // NavigationMenuRoot.tsx:133-158: after the close completes, the direction and the close reason reset; focus return is
    // the module's, from the mounted flag this sends.
    private void OnUnmounted()
    {
        NavigationMenuStore store = behavior.Store;
        store.SetActivationDirection(null);
        store.SetCloseReason(null);
    }

    private void OnCommitted()
    {
        if (rendered && !disposed)
        {
            CopySessionInputs();
            _ = session!.AfterRenderAsync();
            if (Element is { } element)
            {
                _ = module!.SyncAsync(element, ModuleOptions(), null);
            }
        }
    }

    private void CopySessionInputs()
    {
        NavigationMenuStoreState current = behavior.Store.State;
        session!.Open = current.Open;
        session.ActiveTriggerId = current.Mounted || current.Open ? current.ActiveTriggerId : null;
        session.FloatingId = current.PopupId ?? current.Lifecycle.FloatingId;
        session.Ending = current.TransitionStatus == TransitionStatus.Ending;
        session.Mounted = current.Mounted;
        session.Relay = current.Lifecycle.OpenChange;

        // NAV-STA-08, NAV-DIS-08 (§16 Q4 replicated): the dismissal is on only while a Positioner is registered or closed.
        session.Dismiss = current.HasPositioner || !current.Open ? s_dismiss : null;
    }

    private NavigationMenuOptions ModuleOptions()
    {
        NavigationMenuStoreState current = behavior.Store.State;
        return new NavigationMenuOptions
        {
            Part = NavigationMenuTwin.RootPart,
            Orientation = CompositeLiterals.ToLiteral(Orientation),
            Rtl = context!.Direction == TextDirection.Rtl,
            Nested = context.Nested,
            Mounted = current.Mounted,
            CloseReason = current.CloseReason?.ToLiteral(),
            SessionToken = behavior.Store.SessionToken,
            Answered = current.Answered,
        };
    }
}

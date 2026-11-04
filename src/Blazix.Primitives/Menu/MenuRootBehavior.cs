using Blazix.Primitives.Interop;
using Blazix.Primitives.Rendering;
using Blazix.Primitives.Store;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace Blazix.Primitives;

// MNU §3.1, §3.17 (MenuRoot.tsx, MenuSubmenuRoot.tsx): what MenuRoot<TPayload>, MenuSubmenuRoot and ContextMenuRoot (track
// 7) hand their behaviour after their parameters are assigned. Open and TriggerId are read from the ParameterView by
// PopupRootBehavior, since whether they were supplied decides control.
/// <summary>Holds the parameters of a menu root, as its behavior applies them.</summary>
/// <typeparam name="TPayload">The type of the payload the menu's triggers carry.</typeparam>
/// <param name="Popup">The shared popup root parameters.</param>
/// <param name="Modal">The root's <c>Modal</c> as supplied (a submenu passes <see langword="null"/>).</param>
/// <param name="Disabled">The root's <c>Disabled</c>.</param>
/// <param name="LoopFocus">The root's <c>LoopFocus</c>.</param>
/// <param name="HighlightItemOnHover">The root's <c>HighlightItemOnHover</c>.</param>
/// <param name="Orientation">The root's <c>Orientation</c>.</param>
/// <param name="CloseParentOnEsc">The root's <c>CloseParentOnEsc</c>.</param>
/// <param name="OnItemHighlighted">The root's <c>OnItemHighlighted</c>.</param>
internal readonly record struct MenuRootParameters<TPayload>(
    PopupRootParameters<TPayload> Popup,
    bool? Modal,
    bool Disabled,
    bool LoopFocus,
    bool HighlightItemOnHover,
    Orientation Orientation,
    bool CloseParentOnEsc,
    EventCallback<MenuItemHighlightedEventArgs> OnItemHighlighted);

// Base UI's MenuRootInternal (menu/root/MenuRoot.tsx:57-783) as one internal behaviour (decision 12 of the M5 plan; ADR 08
// Amendments 2026-11-03 (3), chosen by the user): MenuRoot<TPayload>, MenuSubmenuRoot (NoPayload, a submenu) and
// ContextMenuRoot (track 7, NoPayload, its parent passed in) compose it and render nothing of their own.
// - The store: PopupRootBehavior over MenuStore (the popup id and the session token from PartIdGenerator), the handle, the
//   render-batch boundary; CloseOnActiveTriggerUnmount (MNU-DIS-13).
// - The `interactions` session by token: Escape through `escape` (PreventAndStop innermost), BubblesEscapeKey =
//   CloseParentOnEsc under a menu parent (MenuRoot.tsx:503), outside press `sloppy` (useDismiss's default), the grace of a
//   context-menu parent (MNU-DIS-10); ParentToken from the family-agnostic cascade (IFloatingNodeContext).
// - The list: ListNavigationBehavior (track 1), its options per parameter set (MenuRoot.tsx:516-568), FlushAsync from
//   OnAfterRenderAsync; OnItemHighlighted from its HighlightChanged (MNU-EVT-10/11; MenuHighlightReporter).
// - PlatformFlags.EnsureAsync from the first interactive OnAfterRenderAsync (ADR 04 Amendments 2026-11-04).
// - It cascades, fixed, MenuRootContext and the ListNavigationContext above the store's own cascade.
/// <summary>The behavior of a menu root: the store, the session, the list navigation and the cascades.</summary>
/// <typeparam name="TPayload">The type of the payload the menu's triggers carry.</typeparam>
internal sealed class MenuRootBehavior<TPayload> : IAsyncDisposable
{
    // internals/constants.ts:3 (TYPEAHEAD_RESET_MS); plan §Settled.
    private const int TypeaheadResetMs = 500;

    private static readonly IReadOnlyList<int> s_noDisabledIndices = [];
    private static readonly InteractionsDismissOptions s_dismiss = new();

    private readonly string familyName;
    private readonly bool isSubmenu;
    private readonly IJSRuntime jsRuntime;
    private readonly PlatformFlags platform;
    private readonly Func<Func<Task>, Task> invokeAsync;
    private readonly Func<Exception, Task> dispatchException;
    private readonly PopupRootBehavior<MenuStore<TPayload>, MenuStoreState<TPayload>, TPayload> root;
    private readonly RenderFragment contextCascade;
    private readonly RenderFragment listCascade;
    private readonly RenderFragment storeCascade;
    private readonly MenuHighlightReporter reporter = new();
    private readonly Dictionary<string, string?> labels = new(StringComparer.Ordinal);
    private MenuRootContext<TPayload>? context;
    private InteractionsSessionBehavior? session;
    private ListNavigationBehavior? list;
    private SelectorSubscription<MenuStoreState<TPayload>>? committed;
    private IDisposable? childRegistration;
    private MenuSubscription? parentMenuSubscription;
    private IDisposable? parentContextSubscription;
    private MenuParent parent;
    private EventCallback<MenuItemHighlightedEventArgs> onItemHighlighted;
    private string? rootId;
    private bool closeParentOnEsc;
    private bool rendered;
    private bool applied;
    private bool flagsRequested;

    /// <summary>Initializes a new instance of the <see cref="MenuRootBehavior{TPayload}"/> class.</summary>
    /// <param name="familyName">The root's family, such as <c>Menu</c>, which names its parts in warnings and messages.</param>
    /// <param name="isSubmenu">Whether the root is a submenu's.</param>
    /// <param name="jsRuntime">The runtime the twins import their modules through.</param>
    /// <param name="platform">The platform hints the submenu triggers render with.</param>
    /// <param name="requestRender">Asks the root to render.</param>
    /// <param name="invokeAsync">Runs work on the root's renderer.</param>
    /// <param name="dispatchException">Routes an exception to the root.</param>
    public MenuRootBehavior(string familyName, bool isSubmenu, IJSRuntime jsRuntime, PlatformFlags platform, Action requestRender, Func<Func<Task>, Task> invokeAsync, Func<Exception, Task> dispatchException)
    {
        this.familyName = familyName;
        this.isSubmenu = isSubmenu;
        this.jsRuntime = jsRuntime;
        this.platform = platform;
        this.invokeAsync = invokeAsync;
        this.dispatchException = dispatchException;
        root = new((popupId, token, logger) => new MenuStore<TPayload>(popupId, token, logger: logger), requestRender);
        contextCascade = RenderContextCascade;
        listCascade = RenderListCascade;
        storeCascade = root.BuildRenderTree;
    }

    /// <summary>Gets the root's store, created at the first parameter set.</summary>
    public MenuStore<TPayload> Store => root.Store;

    /// <summary>Gets the context the root cascades to its parts.</summary>
    public MenuRootContext Context => context!;

    /// <summary>Gets the root's list navigation.</summary>
    public ListNavigationBehavior List => list!;

    /// <summary>Gets a task that completes when the menu's session is attached in the browser.</summary>
    public Task SessionAttached => session?.TwinAttached ?? Task.CompletedTask;

    /// <summary>Renders the cascades and the root's content.</summary>
    /// <param name="builder">The root's render tree builder.</param>
    public void BuildRenderTree(RenderTreeBuilder builder) => contextCascade(builder);

    /// <summary>Syncs the session and the list after the render and runs the batch boundary.</summary>
    /// <param name="firstRender">Whether this is the root's first render.</param>
    /// <returns>A task that completes when the modules have their options.</returns>
    public async Task OnAfterRenderAsync(bool firstRender)
    {
        _ = firstRender;
        rendered = true;
        if (!flagsRequested)
        {
            // ADR 04 Amendments 2026-11-04: the platform hints the submenu triggers render with, read once per scope from the
            // first after-render, which a prerender never runs.
            flagsRequested = true;
            // The flags are hints: a read that fails leaves the defaults (ADR 01's row of 2026-11-04), so its fault is
            // observed and not routed to the root.
            _ = platform.EnsureAsync(jsRuntime).ContinueWith(static t => _ = t.Exception, CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default);
        }

        CopySessionInputs();
        Task sync = session!.AfterRenderAsync();
        await root.OnAfterRenderAsync();
        await sync;
        await list!.FlushAsync();
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        root.Dispose();
        committed?.Dispose();
        childRegistration?.Dispose();
        parentMenuSubscription?.Dispose();
        parentContextSubscription?.Dispose();
        context?.Release();
        if (list is not null)
        {
            list.HighlightChanged -= OnHighlightChanged;
            list.Dispose();
        }

        if (session is not null)
        {
            await session.DisposeAsync();
        }
    }

    /// <summary>Reads the controlled parameters and creates the store at the first parameter set.</summary>
    /// <param name="parameters">The root's parameters.</param>
    /// <param name="ids">The id generator the store's ids come from.</param>
    /// <param name="logger">The root's logger.</param>
    public void SetParameters(ParameterView parameters, PartIdGenerator ids, ILogger? logger)
    {
        root.SetParameters(parameters, ids, logger);
        if (session is not null)
        {
            return;
        }

        MenuStore<TPayload> store = root.Store;
        rootId = ids.Next();
        context = (MenuRootContext<TPayload>)store.CreateContext(familyName);
        context.DispatchException = dispatchException;
        session = new InteractionsSessionBehavior(jsRuntime, store.SessionToken!, RouteAsync, dispatchException);
        list = new ListNavigationBehavior(store.SessionToken!, session.TwinAttached, RouteAsync, dispatchException);
        list.HighlightChanged += OnHighlightChanged;
        context.ListNavigation = list;
        committed = new SelectorSubscription<MenuStoreState<TPayload>>(store, OnCommitted);
        committed.Select(MenuSelectors.Open);
        committed.Select(MenuSelectors.Mounted);
        committed.Select(MenuSelectors.ActiveTriggerId);
        committed.Select(MenuSelectors.OpenChange);
        committed.Select(MenuSelectors.TransitionStatus);
        committed.Select(MenuSelectors.PopupId);
        committed.Select(MenuSelectors.Disabled);
    }

    /// <summary>Applies the root's parameters and its resolved surroundings.</summary>
    /// <param name="parameters">The root's parameters.</param>
    /// <param name="parent">The menu's resolved parent.</param>
    /// <param name="floatingParent">The enclosing popup of any family, from the cascade, or <see langword="null"/>.</param>
    /// <param name="direction">The enclosing direction provider's context, or <see langword="null"/>.</param>
    public void ApplyParameters(in MenuRootParameters<TPayload> parameters, MenuParent parent, IFloatingNodeContext? floatingParent, DirectionProviderContext? direction)
    {
        MenuStore<TPayload> store = root.Store;
        MenuRootContext<TPayload> rootContext = context!;
        PopupStoreState<TPayload> before = store.State.Popup;
        root.ApplyParameters(parameters.Popup);

        // TRN-ANI-28 (useTriggerSwitchTransition.ts), Popover's step: a controlled `TriggerId` that moves the open menu to
        // another trigger clears the instant type in this parameter set, before the parts render.
        PopupStoreState<TPayload> after = store.State.Popup;
        if (before.EffectiveOpen && after.EffectiveOpen && before.EffectiveActiveTriggerId is { } previous
            && after.EffectiveActiveTriggerId is { } next && !string.Equals(previous, next, StringComparison.Ordinal))
        {
            store.BeginTriggerChange();
        }

        rootContext.Handled = parameters.Popup.Handle is not null;
        rootContext.RootOrientation = parameters.Orientation;
        onItemHighlighted = parameters.OnItemHighlighted;
        closeParentOnEsc = parameters.CloseParentOnEsc;
        if (!applied)
        {
            applied = true;
            Attach(parent, store);
        }

        store.SetParent(parent);
        store.SetRootFields(parameters.Disabled, parameters.Modal, rootId, parameters.HighlightItemOnHover);
        WarnNestedModal(parameters.Modal, store.Logger);
        session!.ParentToken = floatingParent?.SessionToken;
        SetListOptions(parameters, direction);
    }

    // MNU-API-12 (actionsRef.close).
    /// <summary>Closes the menu, with the reason <see cref="EventReason.ImperativeAction"/> and no trigger.</summary>
    /// <returns>A task that completes when the close request has run.</returns>
    public Task CloseAsync() => root.Store.CloseAsync();

    // MNU-API-12 (actionsRef.unmount), MNU-ANI-10.
    /// <summary>Ends the closing phase of a menu whose close called <c>PreventUnmountOnClose()</c>.</summary>
    /// <returns>A task that completes when the unmount has run.</returns>
    public Task UnmountAsync() => root.Store.UnmountAsync();

    // MenuStore.ts:132-148: the relay is the parent's, through the parent chain.
    private static ElementReference? RelayTargetOf(MenuParent menuParent)
    {
        MenuParent current = menuParent;
        while (current is { Kind: MenuParentKind.Menu, Menu: { } menu })
        {
            current = menu.Parent;
        }

        return current.Context?.RelayTarget;
    }

    // MNU-STA-08 (MenuStore.ts:190-218), MNU-STR-11: a submenu registers with its parent for the tree's closes and reads
    // the tree's values through it; the subscription is released with the submenu.
    private void Attach(MenuParent resolved, MenuStore<TPayload> store)
    {
        parent = resolved;
        if (resolved is { Kind: MenuParentKind.Menu, Menu: { } menu })
        {
            childRegistration = menu.RegisterChild(context!);
            parentMenuSubscription = menu.Subscribe(store.NotifyParentChanged);
            parentMenuSubscription.Select(MenuSelectors.RootId);

            // MNU-ANI-04 (MenuRoot.tsx:139-160): an initially open submenu that mounts during its parent's enter mirrors the
            // parent's instant type.
            if (isSubmenu && store.State.Popup.EffectiveOpen && menu.State.TransitionStatus == TransitionStatus.Starting)
            {
                store.SeedInstantType(menu.State.InstantType);
            }
        }
        else if (resolved.Context is { } parentContext)
        {
            parentContextSubscription = parentContext.Subscribe(store.NotifyParentChanged);
        }
    }

    // MenuRoot.tsx:207-213 (MNU-EDGE-02): a nested menu ignores a supplied modal, warning in development on every render.
    private void WarnNestedModal(bool? modal, ILogger? logger)
    {
        if (modal is not null && parent.Kind != MenuParentKind.None && logger is not null && DevelopmentWarnings.IsEnabled)
        {
            DevelopmentWarnings.Write(logger, "Blazix: The `Modal` parameter is not supported on nested menus. It will be ignored.");
        }
    }

    // MenuRoot.tsx:516-568: the list's options (CMP §6.8, §6.9).
    private void SetListOptions(in MenuRootParameters<TPayload> parameters, DirectionProviderContext? direction)
    {
        ListNavigationBehavior navigation = list!;
        IMenuState state = root.Store.State;
        bool disabled = state.Disabled;
        string orientation = parameters.Orientation == Orientation.Horizontal ? "horizontal" : "vertical";
        navigation.Enabled = !disabled;
        navigation.LoopFocus = parameters.LoopFocus;
        navigation.Orientation = orientation;
        navigation.TriggerOrientation = orientation;
        navigation.Rtl = DirectionProviderContext.Read(direction) == TextDirection.Rtl;
        navigation.Nested = parent.Kind != MenuParentKind.None;
        navigation.ParentOrientation = parent.Kind == MenuParentKind.Menubar
            ? (parent.Context!.Orientation == Orientation.Horizontal ? "horizontal" : "vertical")
            : null;
        navigation.OpenOnArrowKeyDown = parent.Kind != MenuParentKind.ContextMenu;
        navigation.FocusItemOnHover = parameters.HighlightItemOnHover;
        navigation.ResetOnPointerLeave = true;
        navigation.DisabledIndices = s_noDisabledIndices;
        navigation.KeepOpenOnCrossOrientationKey = parent.Context?.KeepOpenOnCrossOrientationKey ?? false;
        navigation.RelayTarget = RelayTargetOf(parent);
        navigation.SetTypeahead(!disabled, TypeaheadResetMs, "element");
    }

    // An interactions or list request (a press, a hover, Escape, an outside press, a focus-out, a navigation key): a
    // press's interaction type is the open method (PSH-PTR-01/02), written only while closed.
    private Task RouteAsync(OpenChangeRequest request)
    {
        MenuStore<TPayload> store = root.Store;
        if (request.InteractionType is { } type)
        {
            store.SetOpenInteractionType(type);
        }

        return store.RequestOpenAsync(request.Open, request.Details);
    }

    private void OnCommitted()
    {
        if (rendered)
        {
            CopySessionInputs();
            _ = session!.AfterRenderAsync();
        }
    }

    private void CopySessionInputs()
    {
        MenuStoreState<TPayload> state = root.Store.State;
        PopupStoreState<TPayload> popup = state.Popup;
        session!.Open = popup.EffectiveOpen;
        session.ActiveTriggerId = popup.Mounted || popup.EffectiveOpen ? popup.EffectiveActiveTriggerId : null;
        session.FloatingId = popup.PopupId;
        session.Ending = popup.DerivedTransitionStatus == TransitionStatus.Ending;
        session.Relay = popup.OpenChange;
        session.Dismiss = ((IMenuState)state).Disabled ? null : DismissOptions();
    }

    // MenuRoot.tsx:500-512.
    private InteractionsDismissOptions DismissOptions()
    {
        bool bubbles = closeParentOnEsc && parent.Kind == MenuParentKind.Menu;
        int grace = parent.Kind == MenuParentKind.ContextMenu ? parent.Context!.OutsidePressGrace : 0;
        return bubbles || grace > 0 ? new InteractionsDismissOptions { BubblesEscapeKey = bubbles ? true : null, OutsidePressGrace = grace } : s_dismiss;
    }

    // MNU-EVT-10/11: a highlight report of the list's mirror becomes an OnItemHighlighted notification. Design point 9
    // (settled by the orchestrator 2026-11-04 as (a)): the label will come with the composite module's report (the
    // registry's label, else the item's text); until that grant, only an explicit Label is reported (a dated note on EVT-10).
    private void OnHighlightChanged(CompositeListHighlight report)
    {
        string? label = report.ItemId is { } itemId && labels.TryGetValue(itemId, out string? known) ? known : null;
        if (reporter.Report(report.Index, report.ItemId, report.Event, label) is { } args && onItemHighlighted.HasDelegate)
        {
            _ = invokeAsync(() => onItemHighlighted.InvokeAsync(args));
        }
    }

    private void RenderContextCascade(RenderTreeBuilder builder)
    {
        builder.OpenComponent<CascadingValue<MenuRootContext>>(0);
        builder.AddComponentParameter(1, nameof(CascadingValue<MenuRootContext>.Value), context);
        builder.AddComponentParameter(2, nameof(CascadingValue<MenuRootContext>.IsFixed), true);
        builder.AddComponentParameter(3, nameof(CascadingValue<MenuRootContext>.ChildContent), listCascade);
        builder.CloseComponent();
    }

    private void RenderListCascade(RenderTreeBuilder builder)
    {
        builder.OpenComponent<CascadingValue<ListNavigationContext>>(0);
        builder.AddComponentParameter(1, nameof(CascadingValue<ListNavigationContext>.Value), list!.Context);
        builder.AddComponentParameter(2, nameof(CascadingValue<ListNavigationContext>.IsFixed), true);
        builder.AddComponentParameter(3, nameof(CascadingValue<ListNavigationContext>.ChildContent), storeCascade);
        builder.CloseComponent();
    }
}

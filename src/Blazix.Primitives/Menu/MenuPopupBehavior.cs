using Blazix.Primitives.Interop;
using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace Blazix.Primitives;

// Base UI's Menu.Popup (menu/popup/MenuPopup.tsx) as one internal behaviour (the DialogPopupBehavior form; ADR 08
// Amendments 2026-11-03 (3)): ContextMenuPopup (track 7) is a shell over it. It configures the modules and re-implements
// none (MNU §11):
// - the focus manager: modal only under a context menu (MNU-FOC-13), disabled while unmounted, initial focus per parent
//   kind (the list element when no item is highlighted; MenuPopup.tsx:81-96), FinalFocus/FinalFocusResolver (ADR 08
//   Amendments 2026-10-28; MNU-API-31), PreviousFocusableElement = the active trigger, NextFocusableElement and
//   InsideElements at top level (FLT-ARIA-03), restore focus;
// - the transitions handle (status, completion, `transition: none` while starting);
// - the `interactions` floating hover (MNU-PTR-07), off under a context menu or a bar (MenuPopup.tsx:123-126);
// - the `popup` module's element role: StopCompositeKeys under a Toolbar (MNU-KEY-20), after the portal moved;
// - the list's floating handle (a composite root in list mode, CompositeListRole.Floating) on the popup unless a MenuList
//   takes it (role="presentation" then), cascading its CompositeRootContext to the items;
// - role="menu", aria-orientation when horizontal, aria-labelledby by resolvePopupLabel (UTL-ARIA-01), id, tabindex="-1",
//   data-base-ui-focusable, data-rootownerid; Side and Align from the positioner's mirror.
/// <summary>The behavior of a menu popup: focus, transitions, its list handle and its labels.</summary>
internal sealed class MenuPopupBehavior : IAsyncDisposable
{
    private const string OpenSelector = "[data-open]";
    private const string StartingStyle = "transition: none";

    private static readonly PopupOptions s_freeKeys = new() { Popup = new PopupElementOptions { StopCompositeKeys = false } };
    private static readonly PopupOptions s_stoppedKeys = new() { Popup = new PopupElementOptions { StopCompositeKeys = true } };

    private readonly string familyName;
    private readonly IJSRuntime jsRuntime;
    private readonly ILogger logger;
    private readonly Action requestRender;
    private readonly Action mirrorChanged;
    private readonly Func<Func<Task>, Task> invokeAsync;
    private readonly CompositeRootBehavior list;
    private readonly PopupModuleAttachment module;
    private readonly TaskCompletionSource attached = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly PopupTransitionAttributes transitionAttributes = new();
    private MenuRootContext? root;
    private PositionerContext? positioner;
    private PopupPortalContext? portal;
    private ToolbarRootContext? toolbar;
    private MenuSubscription? subscription;
    private MenuSelection<bool>? open;
    private MenuSelection<bool>? mounted;
    private MenuSelection<TransitionStatus?>? status;
    private MenuSelection<MenuInstantType?>? instant;
    private MenuSelection<int>? unmountRequests;
    private MenuSelection<InteractionType?>? openMethod;
    private MenuSelection<string?>? activeTriggerId;
    private MenuSelection<PopupTriggerElement?>? activeTrigger;
    private MenuSelection<EventSnapshot?>? closeEvent;
    private MenuSelection<OpenChangeRelay?>? relay;
    private MenuSelection<bool>? retainExit;
    private MenuSelection<string?>? listElementId;
    private MenuSelection<EventReason?>? openChangeReason;
    private MenuSelection<bool>? keyboardOpen;
    private IDisposable? boundaryPart;
    private IDisposable? retainRegistration;
    private IDisposable? inputIdSubscription;
    private MenuFilterModuleAttachment? filterModule;
    private FocusManagerBehavior? focus;
    private TransitionsTwin? transitions;
    private TwinHandle<TransitionsOptions>? transitionsHandle;
    private TransitionsOptions? sentTransitions;
    private Task? attaching;
    private string? frozenStyle;
    private string? consumerStyle;
    private FocusTargetValue? finalFocus;
    private Func<InteractionType, FocusTargetValue?>? finalFocusResolver;
    private EventSnapshot? finalResolvedFor;
    private FocusTargetValue? resolvedFinal;
    private string? renderedId;
    private IReadOnlyDictionary<string, object>? consumerAttributes;
    private ElementReference? element;
    private bool announcedRetain;
    private bool warnedFinalFocus;
    private bool disposed;

    /// <summary>Initializes a new instance of the <see cref="MenuPopupBehavior"/> class.</summary>
    /// <param name="familyName">The popup's family, such as <c>Menu</c>.</param>
    /// <param name="jsRuntime">The runtime the twins import their modules through.</param>
    /// <param name="logger">The part's logger, which receives development warnings.</param>
    /// <param name="idGenerator">The scoped id generator.</param>
    /// <param name="requestRender">Asks the part to render, after a change of the menu's state.</param>
    /// <param name="mirrorChanged">Tells the part that the positioner's mirrored placement changed, which renders only when its state record changed.</param>
    /// <param name="invokeAsync">Runs work on the part's renderer.</param>
    /// <param name="dispatchException">Routes an exception to the part.</param>
    /// <param name="onKeyDown">The part's own <c>onkeydown</c> handler, which calls <see cref="HandleKeyDownAsync"/>.</param>
    public MenuPopupBehavior(string familyName, IJSRuntime jsRuntime, ILogger logger, PartIdGenerator idGenerator, Action requestRender, Action mirrorChanged, Func<Func<Task>, Task> invokeAsync, Func<Exception, Task> dispatchException, EventCallback onKeyDown)
    {
        this.familyName = familyName;
        this.jsRuntime = jsRuntime;
        this.logger = logger;
        this.requestRender = requestRender;
        this.mirrorChanged = mirrorChanged;
        this.invokeAsync = invokeAsync;
        list = new CompositeRootBehavior(jsRuntime, idGenerator, requestRender, dispatchException, onKeyDown);
        module = new PopupModuleAttachment(jsRuntime);
    }

    /// <summary>Gets the root's context.</summary>
    public MenuRootContext Context => root!;

    /// <summary>Gets a value indicating whether the menu is open.</summary>
    public bool Open => open!.Value;

    /// <summary>Gets the transition status the popup renders.</summary>
    public TransitionStatus? TransitionStatus => status!.Value;

    /// <summary>Gets the side the positioner mirrors.</summary>
    public Side? Side => positioner!.Mirror.Side;

    /// <summary>Gets the alignment the positioner mirrors.</summary>
    public Align? Align => positioner!.Mirror.Align;

    /// <summary>Gets a value indicating whether the menu is a submenu.</summary>
    public bool Nested => root!.Parent.Kind == MenuParentKind.Menu;

    /// <summary>Gets the instant type the popup renders.</summary>
    public MenuInstantType? Instant => instant!.Value;

    /// <summary>Gets a task that completes once the popup's handles have attached.</summary>
    public Task TwinAttached => Task.WhenAll(attached.Task, list.TwinAttached);

    /// <summary>Attaches the handles after the render, sends their changed options and runs the batch boundary.</summary>
    /// <param name="element">The popup's element.</param>
    /// <param name="id">The id the popup rendered.</param>
    /// <returns>A task that completes when the modules have their options.</returns>
    public async Task OnAfterRenderAsync(ElementReference? element, string? id)
    {
        MenuRootContext menu = root!;
        this.element = element;
        Task listSync = Task.CompletedTask;
        if (element is { } target)
        {
            menu.SetPopupElement(target, id);
            await PopupPortalContext.WhenMoved(portal);
            Task transitionsSync = SyncTransitionsAsync(target);

            // MNU-KEY-20 (MenuPopup.tsx:169-173): inside a toolbar the composite keys stop at the popup.
            Task moduleSync = module.SyncAsync(target, toolbar is null ? s_freeKeys : s_stoppedKeys);

            // useMenuFilterPopup.ts (M5 track 12): a filterable popup's `menu-filter` handle, the input's refocus.
            Task filterSync = filterModule?.SyncAsync(target, new MenuFilterOptions
            {
                Part = MenuFilterTwin.PopupPart,
                MenuKey = menu.SessionToken,
                Open = open!.Value,

                // MenuFilterSubmenuRoot.tsx:278-292: a filterable submenu's popup closes on its close key, back to the parent.
                Submenu = menu.Parent.Kind == MenuParentKind.Menu && menu.Filter is not null ? true : null,

                // useVirtualFocusParentHandoff.ts: a plain submenu of a filterable parent hands focus and the parent's
                // highlight back itself.
                Handoff = menu.Filter is null ? true : null,
                Reason = openChangeReason!.Value?.ToLiteral(),
                ParentKey = menu.Parent.Kind == MenuParentKind.Menu ? menu.Parent.Menu?.SessionToken : null,
                ParentOrientation = menu.Parent.Kind == MenuParentKind.Menu ? (menu.Parent.Menu?.Orientation == Orientation.Horizontal ? "horizontal" : "vertical") : null,
            }) ?? Task.CompletedTask;

            // The list's handle waits for the session inside track 1's behavior; the batch boundary does not wait for it.
            listSync = list.AfterRenderAsync(true, target, id);
            focus!.SetInputs(FocusInputs());
            await focus.AfterRenderAsync(target, id);
            await moduleSync;
            await filterSync;
            await transitionsSync;
        }

        await menu.AtBatchBoundaryAsync();
        await listSync;
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        positioner?.RemoveListener(mirrorChanged);
        subscription?.Dispose();
        subscription = null;
        retainRegistration?.Dispose();
        inputIdSubscription?.Dispose();
        if (root?.Filter is { } listFilter && ReferenceEquals(listFilter.ListRoot, list))
        {
            listFilter.ListRoot = null;
        }

        boundaryPart?.Dispose();
        root?.ClearPopupElement();
        list.Dispose();
        ValueTask transitionsDisposed = transitions?.DisposeAsync() ?? ValueTask.CompletedTask;
        ValueTask moduleDisposed = module.DisposeAsync();
        ValueTask filterDisposed = filterModule?.DisposeAsync() ?? ValueTask.CompletedTask;
        ValueTask focusDisposed = focus?.DisposeAsync() ?? ValueTask.CompletedTask;
        await transitionsDisposed;
        await moduleDisposed;
        await filterDisposed;
        await focusDisposed;
    }

    /// <summary>Checks the part's placement and subscribes to the root's state, once.</summary>
    /// <param name="root">The enclosing root's context, or <see langword="null"/>.</param>
    /// <param name="positioner">The enclosing positioner's context, or <see langword="null"/>.</param>
    /// <param name="portal">The enclosing portal node's context, or <see langword="null"/>.</param>
    /// <param name="toolbar">The enclosing toolbar's context, or <see langword="null"/>.</param>
    /// <exception cref="InvalidOperationException">The part is outside a root or a positioner.</exception>
    public void Subscribe(MenuRootContext? root, PositionerContext? positioner, PopupPortalContext? portal, ToolbarRootContext? toolbar)
    {
        MenuRootContext menu = MenuRootContext.Require(root, familyName);
        this.positioner = positioner ?? throw MenuParts.MissingPositioner(familyName);
        this.root = menu;
        this.portal = portal;
        this.toolbar = toolbar;
        subscription = menu.Subscribe(OnChanged);
        open = subscription.Select(MenuSelectors.Open);
        mounted = subscription.Select(MenuSelectors.Mounted);
        status = subscription.Select(MenuSelectors.TransitionStatus);
        instant = subscription.Select(MenuSelectors.InstantType);
        unmountRequests = subscription.Select(MenuSelectors.UnmountRequests);
        openMethod = subscription.Select(MenuSelectors.OpenMethod);
        activeTriggerId = subscription.Select(MenuSelectors.ActiveTriggerId);
        activeTrigger = subscription.Select(MenuSelectors.ActiveTriggerElement);
        closeEvent = subscription.Select(MenuSelectors.CloseEvent);
        relay = subscription.Select(MenuSelectors.OpenChange);
        retainExit = subscription.Select(MenuSelectors.PreventUnmountingOnClose);
        listElementId = subscription.Select(MenuSelectors.ListElementId);
        openChangeReason = subscription.Select(MenuSelectors.OpenChangeReason);
        keyboardOpen = subscription.Select(MenuSelectors.KeyboardOpen);
        subscription.Select(MenuSelectors.RootId);
        retainRegistration = menu.OnUnmountPreventionChanging(AnnounceRetainExit);
        focus = new FocusManagerBehavior(jsRuntime, invokeAsync, snapshot =>
            menu.RequestOpenAsync(false, new OpenChangeEventDetails(EventReason.FocusOut) { Event = snapshot }));
        boundaryPart = menu.RegisterBoundaryPart();
        this.positioner.AddListener(mirrorChanged);
        list.SetList(menu.List, CompositeListRole.Floating);

        // MenuFilterPopup.tsx:38-50 (M5 track 12): a filterable popup's initial focus is its input, named by its id.
        inputIdSubscription = menu.Filter?.SubscribeInputId(OnChanged);
        if (menu.Filter is { } listFilter)
        {
            listFilter.ListRoot = list;
        }

        if (menu.Filter is not null || (menu.Parent.Kind == MenuParentKind.Menu && menu.Parent.Menu?.Filter is not null))
        {
            filterModule = new MenuFilterModuleAttachment(jsRuntime);

            // MenuFilterSubmenuRoot.tsx:203-213 (MNU-KEY-32): a filterable submenu's close key closes it with the
            // list-navigation reason; the module returns focus to the parent.
            filterModule.Twin.SubmenuKey += report => _ = invokeAsync(() =>
                menu.RequestOpenAsync(report.Open, new OpenChangeEventDetails(EventReason.ListNavigation, report.Event)));
        }
    }

    /// <summary>Applies the part's focus parameters and its rendered id.</summary>
    /// <param name="finalFocus">The part's <c>FinalFocus</c>.</param>
    /// <param name="finalFocusResolver">The part's <c>FinalFocusResolver</c>.</param>
    /// <param name="id">The id the popup renders.</param>
    public void Apply(FocusTargetValue? finalFocus, Func<InteractionType, FocusTargetValue?>? finalFocusResolver, string? id)
    {
        this.finalFocus = finalFocus;
        this.finalFocusResolver = finalFocusResolver;
        renderedId = id;
        focus!.Portal = portal;
        if (!warnedFinalFocus && finalFocus is not null && finalFocusResolver is not null && DevelopmentWarnings.IsEnabled)
        {
            warnedFinalFocus = true;
            DevelopmentWarnings.Write(logger, $"Blazix: `{familyName}Popup` received both `FinalFocus` and `FinalFocusResolver`; the resolver is used.");
        }

        focus.SetInputs(FocusInputs());
    }

    /// <summary>Adds the popup's attributes.</summary>
    /// <param name="attributes">The part's attributes.</param>
    /// <param name="hasConsumerId">Whether the consumer supplied an <c>id</c>.</param>
    public void AddAttributes(AttributeList attributes, bool hasConsumerId)
    {
        MenuRootContext menu = root!;
        IMenuState state = menu.State;
        if (!hasConsumerId)
        {
            attributes.Set("id", state.FloatingId);
        }

        // MNU-ARIA-06 (MenuFilterPopup.tsx:52-56, useMenuFilterPopup.ts:97-101; M5 track 12): a filterable menu's popup is a
        // dialog labelled as the menu would be, without aria-orientation; its list keeps the menu semantics.
        if (menu.Filter is not null)
        {
            attributes.Set("role", "dialog");
            if (PopupLabel.Resolve(consumerAttributes, activeTriggerId!.Value) is { } dialogLabel)
            {
                attributes.Set("aria-labelledby", dialogLabel);
            }
        }

        // MenuPopup.tsx:151-164: a rendered list takes the menu semantics.
        else if (!string.IsNullOrEmpty(listElementId!.Value))
        {
            attributes.Set("role", "presentation");
        }
        else
        {
            attributes.Set("role", "menu");
            if (menu.Orientation == Orientation.Horizontal)
            {
                attributes.Set("aria-orientation", "horizontal");
            }

            if (PopupLabel.Resolve(consumerAttributes, activeTriggerId!.Value) is { } label)
            {
                attributes.Set("aria-labelledby", label);
            }
        }

        attributes.Set("tabindex", "-1");
        attributes.Set(PopupMarkerAttributes.Focusable, string.Empty);

        // D11 (TRN-STA-03): the status attributes are C#'s until the transitions handle attaches (PopupTransitionAttributes).
        transitionAttributes.Add(attributes, status!.Value);
        if (state.RootId is { } rootId)
        {
            attributes.Set("data-rootownerid", rootId);
        }

        list.AddAttributes(attributes, hasConsumerId ? null : state.FloatingId);
    }

    // ADR 04 Amendments 2026-10-18 (handover at attach), the PopoverPopup form: before attach C# renders `transition: none`
    // while starting (TRN-DOM-05, MNU-DOM-09) before the consumer's style; from attach the transitions module owns it.
    /// <summary>Finishes the popup's attributes after the consumer's: the handover of its style at attach.</summary>
    /// <param name="attributes">The part's attributes.</param>
    public void FinishAttributes(AttributeList attributes)
    {
        consumerStyle = attributes.TryGetValue(MergeProps.StyleAttribute, out AttributeValue merged) && merged.TryGetValue(out string text) && text.Length > 0 ? text : null;
        if (frozenStyle is null)
        {
            string? own = status!.Value == Primitives.TransitionStatus.Starting ? StartingStyle : null;
            string? style = own is null ? consumerStyle : consumerStyle is null ? own : own + "; " + consumerStyle;
            if (style is null)
            {
                attributes.Remove(MergeProps.StyleAttribute);
                return;
            }

            attributes.Set(MergeProps.StyleAttribute, style);
            if (transitionsHandle is not null || attaching is not null)
            {
                frozenStyle = style;
            }

            return;
        }

        attributes.Set(MergeProps.StyleAttribute, frozenStyle);
    }

    /// <summary>Determines whether the part forwards a consumer handler of the given event.</summary>
    /// <param name="eventName">The event's attribute name, such as <c>onkeydown</c>.</param>
    /// <returns><see langword="false"/> for a handler the list runs from its own.</returns>
    public bool ForwardsConsumerHandler(string eventName) => list.ForwardsConsumerHandler(eventName);

    /// <summary>Wraps the popup's content in the list's cascade, while the popup holds the list.</summary>
    /// <param name="content">The consumer's content.</param>
    /// <returns>The content to render inside the popup.</returns>
    public RenderFragment? CascadeContent(RenderFragment? content) => list.Context.Cascade(content);

    /// <summary>Renders the inside guard before the popup.</summary>
    /// <param name="builder">The part's render tree builder.</param>
    public void BuildBeforeElement(RenderTreeBuilder builder) => focus!.BuildBeforeElement(builder);

    /// <summary>Renders the inside guard after the popup.</summary>
    /// <param name="builder">The part's render tree builder.</param>
    public void BuildAfterElement(RenderTreeBuilder builder) => focus!.BuildAfterElement(builder);

    /// <summary>Handles the popup's <c>keydown</c> for the list.</summary>
    /// <param name="args">The key.</param>
    /// <returns>A task that completes when the consumer's handler has run.</returns>
    public Task HandleKeyDownAsync(KeyboardEventArgs args) =>
        // MNU-API-59, EVT-14 (M5 track 12, option (a)): a key the filter's Input kept for its caret never reaches the
        // popup's consumer handler, as Base UI's router stops it.
        root?.Filter is { } filter && filter.TakeOwnedKey(args.Key) ? Task.CompletedTask : list.HandleKeyDownAsync(args);

    /// <summary>Takes the consumer's attributes, whose own handler and label the popup reads.</summary>
    /// <param name="attributes">The consumer's attributes.</param>
    public void SetConsumerAttributes(IReadOnlyDictionary<string, object>? attributes)
    {
        consumerAttributes = attributes;
        if (root?.Filter is { } filter)
        {
            filter.PopupHandlesKeyDown = attributes?.ContainsKey("onkeydown") == true;
        }
        list.SetConsumerAttributes(attributes);
    }

    // The commit path: the focus manager's inputs first, then the render that applies them.
    private void OnChanged()
    {
        focus?.SetInputs(FocusInputs());
        requestRender();
    }

    // MenuFilterPopup.tsx:28-50 (M5 track 12): the input holds real focus, so the popup is never the target. A submenu
    // may focus its input only after a keyboard, list-navigation, hover or press open; a hover open, or a touch or pen
    // tap that would raise the on-screen keyboard, leaves focus where it is.
    private FocusTargetValue FilterInitialFocus(MenuParentKind? kind, InteractionType? method, string? inputId)
    {
        bool isOpen = open!.Value;
        EventReason? reason = openChangeReason!.Value;
        bool mayFocusInput = kind != MenuParentKind.Menu
            || (isOpen && (method == InteractionType.Keyboard || reason is EventReason.ListNavigation or EventReason.TriggerHover or EventReason.TriggerPress));
        bool hoverOpen = isOpen && reason == EventReason.TriggerHover;
        bool touchOpen = method is InteractionType.Touch or InteractionType.Pen;
        return mayFocusInput && !hoverOpen && !touchOpen && inputId is not null ? new FocusTargetValue(inputId) : new FocusTargetValue(false);
    }

    // MenuPopup.tsx:81-96, 176-216.
    private FocusManagerInputs FocusInputs()
    {
        MenuRootContext menu = root!;
        MenuParentKind? kind = menu.Parent.Kind;
        InteractionType? method = openMethod!.Value;
        IMenuTriggerPart? trigger = kind is null ? activeTrigger!.Value?.Owner as IMenuTriggerPart : null;
        ElementReference? preGuard = trigger?.PreGuard;

        // The initial focus: none for a submenu (list navigation highlights), else the list element when no item is
        // highlighted (a pointer open lands on the list), else the default. Base UI decides when the focus manager runs,
        // after a keyboard open's highlight; the highlight reaches C# later, so a keyboard open of a list with items
        // counts as highlighted.
        bool highlighted = menu.HighlightedItemId is not null || (keyboardOpen!.Value && menu.Items.Count > 0);
        FocusTargetValue? initial = kind == MenuParentKind.Menu
            ? new FocusTargetValue(false)
            : !string.IsNullOrEmpty(listElementId!.Value) ? (highlighted ? new FocusTargetValue(false) : new FocusTargetValue(listElementId.Value!)) : default(FocusTargetValue?);
        if (menu.Filter is { } filter)
        {
            initial = FilterInitialFocus(kind, method, filter.InputId);
        }

        // The return focus: the consumer's, else the trigger for a top-level or context menu, a menu with an active
        // trigger, or a bar's menu not closed by an outside press.
        bool returnFocus = kind is null or MenuParentKind.ContextMenu || activeTrigger!.Value is not null
            || (kind == MenuParentKind.Menubar && openChangeReason!.Value != EventReason.OutsidePress);
        return new FocusManagerInputs
        {
            Open = open!.Value,
            Modal = kind == MenuParentKind.ContextMenu,
            Disabled = !mounted!.Value,
            InitialFocus = initial,
            ReturnFocus = ResolveFinalFocus() ?? FilterReturnFocus(menu, kind) ?? new FocusTargetValue(returnFocus),
            RestoreFocus = FocusManagerTwin.RestorePopup,
            OpenInteractionType = method,
            CloseEvent = closeEvent!.Value,
            DomReferenceId = activeTriggerId!.Value,
            TriggerFocusGuards = kind is null,
            PreviousFocusableElement = activeTrigger!.Value?.Element,
            NextFocusableElement = trigger?.FocusTarget,
            InsideElements = preGuard is { } guard ? [guard] : null,
            Relay = relay!.Value,
            SessionToken = menu.SessionToken,
        };
    }

    // MenuFilterSubmenuRoot.tsx:120-125, 164-170 and useVirtualFocusParentHandoff.ts:28-30 (M5 track 12): a submenu of a
    // filterable parent, filterable or plain, returns focus to the parent's input, which holds the parent's virtual focus,
    // rather than to the trigger.
    // The menu-filter popup handle returns focus there itself, only when focus was left behind in the popup or on the
    // body (MNU-FOC-14, EVT-07): an explicit target would take focus back from wherever the user moved it.
    private static FocusTargetValue? FilterReturnFocus(MenuRootContext menu, MenuParentKind? kind) =>
        kind == MenuParentKind.Menu && menu.Parent.Menu?.Filter is not null
            ? new FocusTargetValue(false)
            : default(FocusTargetValue?);

    // The close interaction type as the focus manager classifies it (FLT-FOC-10), called once per committed close.
    private FocusTargetValue? ResolveFinalFocus()
    {
        if (finalFocusResolver is { } resolver)
        {
            if (open!.Value || closeEvent!.Value is not { } snapshot)
            {
                return default(FocusTargetValue?);
            }

            if (!ReferenceEquals(finalResolvedFor, snapshot))
            {
                finalResolvedFor = snapshot;
                resolvedFinal = resolver(CloseTypeClassifier.Classify(snapshot, focus!.RecordedInteractionType)) ?? new FocusTargetValue(false);
            }

            return resolvedFinal;
        }

        return finalFocus;
    }

    private async Task SyncTransitionsAsync(ElementReference element)
    {
        if (disposed)
        {
            return;
        }

        if (transitions is null)
        {
            transitions = new TransitionsTwin(jsRuntime);
            transitions.StatusChanged += change => root?.MirrorStatus(change.Status);
            transitions.TransitionCompleted += opened => _ = invokeAsync(() => root!.CompleteTransitionAsync(opened));
        }

        if (transitionsHandle is null)
        {
            attaching ??= AttachTransitionsAsync(element, transitions);
            await attaching;
        }

        // Issue 226 (issue 213's shape, PR 215): a render during the attach sends what the attach did not carry.
        if (transitionsHandle is not null && !disposed && TransitionsOptions() is { } options && options != sentTransitions)
        {
            sentTransitions = options;
            await transitionsHandle.UpdateAsync(options);
        }
    }

    private TransitionsOptions TransitionsOptions() => new()
    {
        Open = open!.Value,
        Mounted = mounted!.Value,
        Status = status!.Value,
        ReportOpenComplete = true,
        OpenSelector = OpenSelector,
        UnmountRequests = unmountRequests!.Value,
        RetainExit = retainExit!.Value,
        DisableTransitionWhileStarting = true,
        Style = consumerStyle ?? string.Empty,
    };

    // Issue 147 ((B1), ADR 04 Amendments 2026-10-29), the PopoverPopup form: the store's opt-out reaches the transitions
    // module ahead of any render of the close.
    private void AnnounceRetainExit(bool retain)
    {
        if (transitions is null || element is not { } target || announcedRetain == retain)
        {
            return;
        }

        announcedRetain = retain;
        _ = transitions.RetainExitAsync(target, retain).AsTask();
    }

    private async Task AttachTransitionsAsync(ElementReference element, TransitionsTwin twin)
    {
        await twin.PrepareAsync();
        if (disposed || transitions != twin)
        {
            return;
        }

        TransitionsOptions options = TransitionsOptions();
        sentTransitions = options;
        transitionAttributes.HandOver();
        transitionsHandle = await twin.AttachAsync(element, options);
        attaching = null;
        attached.TrySetResult();
    }
}

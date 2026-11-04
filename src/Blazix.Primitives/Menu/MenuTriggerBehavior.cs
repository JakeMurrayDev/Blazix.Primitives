using Blazix.Primitives.Interop;
using Blazix.Primitives.Rendering;
using Blazix.Primitives.Store;
using Blazix.Primitives.Utilities;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace Blazix.Primitives;

// MNU §3.2 (menu/trigger/MenuTrigger.tsx): the parameters a menu trigger hands its behaviour.
/// <summary>Holds the parameters of a menu trigger, as its behavior applies them.</summary>
/// <param name="NativeButton">The trigger's <c>NativeButton</c>.</param>
/// <param name="Disabled">The trigger's <c>Disabled</c>.</param>
/// <param name="OpenOnHover">The trigger's <c>OpenOnHover</c> as supplied.</param>
/// <param name="Delay">The trigger's <c>Delay</c> (the rest time of a top-level trigger; 100 milliseconds when <see langword="null"/>).</param>
/// <param name="CloseDelay">The trigger's <c>CloseDelay</c> (no delay when <see langword="null"/>).</param>
internal readonly record struct MenuTriggerParameters(bool NativeButton, bool Disabled, bool? OpenOnHover, Duration? Delay, Duration? CloseDelay);

// Base UI's Menu.Trigger (menu/trigger/MenuTrigger.tsx) as one internal behaviour (decision 12 of the M5 plan; ADR 08
// Amendments 2026-11-03 (3)): MenuTrigger<TPayload> is a thin shell over it, and Menubar's triggers (track 8) are the same
// part inside a bar. Four handles on the trigger's own element (issue 115: the bag's id is the trigger id), all started in
// the same render (the PopoverTrigger and FixtureListTrigger shape); the list handle awaits the session inside track 1's
// behaviour:
// - ButtonBehavior (native per NativeButton);
// - PopupTriggerBehavior with the focus guards (the `popup` module's `guard`), outside a bar only (MNU-STR-16);
// - the `interactions` trigger handle with onMouseDown (ADR 01 Amendments 2026-11-04): Click Event = "mousedown" ("click"
//   while a bar menu it owns is open), Toggle, StickIfOpen with the patient-click window at top level; Hover RestMs (top
//   level), DelayClose, SafePolygon, BlockPointerEvents outside a bar, MouseOnly; the drag-release arm
//   (InteractionsTriggerOptions.Menu);
// - a composite root in list mode: Reference for the active trigger, Trigger when inactive or detached (track 1).
// A keydown reaches one gate (the ToolbarButton rule): Enter and Space the button's, every other key the list's.
// In a bar (MenuTrigger.tsx:173-213, 258-285): role="menuitem", a composite item of the bar, no guards, hover only while
// the bar has a menu open and this trigger has not mounted it, MouseDownAction "open" (design point 6 (b), settled by
// the orchestrator 2026-11-04), the `trigger-focus` open while the bar has a menu open.
/// <summary>The behavior of a menu trigger: its registration, its button, its press and hover, and its list keys.</summary>
/// <typeparam name="TPayload">The type of the payload the trigger carries.</typeparam>
internal sealed class MenuTriggerBehavior<TPayload> : IAsyncDisposable, IMenuTriggerPart
{
    // menu/utils/constants (MNU-API-20).
    private const int DefaultDelay = 100;

    private static readonly InteractionsFocusOptions s_focus = new();

    private readonly string familyName;
    private readonly PopupTriggerBehavior<MenuStore<TPayload>, MenuStoreState<TPayload>, TPayload> trigger;
    private readonly ButtonBehavior button;
    private readonly InteractionsTriggerBehavior interactions;
    private readonly CompositeRootBehavior reference;
    private readonly PopupModuleAttachment guards;
    private readonly Func<Func<Task>, Task> invokeAsync;
    private readonly EventCallback onKeyDown;
    private readonly Action requestRender;
    private readonly Action<ElementReference> capturePreGuard;
    private readonly Action<ElementReference> captureFocusTarget;
    private CompositeItemBehavior? barItem;
    private IMenuParentContext? parent;
    private IDisposable? parentSubscription;
    private StoreSelection<MenuStoreState<TPayload>, string?, bool>? focusGuards;
    private StoreSelection<MenuStoreState<TPayload>, string?, string?>? controlsId;
    private StoreSelection<MenuStoreState<TPayload>, bool>? rootDisabled;
    private MenuTriggerParameters parameters;
    private string? id;
    private ElementReference? preGuard;
    private ElementReference? focusTarget;
    private bool disposed;

    /// <summary>Initializes a new instance of the <see cref="MenuTriggerBehavior{TPayload}"/> class.</summary>
    /// <param name="owner">The trigger part, whose identity registers in the store.</param>
    /// <param name="familyName">The trigger's family, such as <c>Menu</c>, which names it in messages.</param>
    /// <param name="jsRuntime">The runtime the twins import their modules through.</param>
    /// <param name="logger">The part's logger.</param>
    /// <param name="developmentLog">The button mechanism's development log.</param>
    /// <param name="idGenerator">The scoped id generator.</param>
    /// <param name="requestRender">Asks the part to render, its <c>StateHasChanged</c>.</param>
    /// <param name="invokeAsync">Runs work on the part's renderer.</param>
    /// <param name="dispatchException">Routes an exception to the part.</param>
    /// <param name="onClick">The part's own <c>onclick</c> handler, which calls <see cref="HandleClickAsync"/>.</param>
    /// <param name="onMouseDown">The part's own <c>onmousedown</c> handler, which calls <see cref="HandleMouseDownAsync"/>.</param>
    /// <param name="onKeyDown">The part's own <c>onkeydown</c> handler, which calls <see cref="HandleKeyDownAsync"/>.</param>
    /// <param name="onKeyUp">The part's own <c>onkeyup</c> handler, which calls <see cref="HandleKeyUpAsync"/>.</param>
    public MenuTriggerBehavior(
        object owner,
        string familyName,
        IJSRuntime jsRuntime,
        ILogger logger,
        ButtonDevelopmentLog developmentLog,
        PartIdGenerator idGenerator,
        Action requestRender,
        Func<Func<Task>, Task> invokeAsync,
        Func<Exception, Task> dispatchException,
        EventCallback onClick,
        EventCallback onMouseDown,
        EventCallback onKeyDown,
        EventCallback onKeyUp)
    {
        this.familyName = familyName;
        this.invokeAsync = invokeAsync;
        this.onKeyDown = onKeyDown;
        this.requestRender = requestRender;
        // The registration's owner is the behavior, which the popup reads the guards through (IMenuTriggerPart).
        _ = owner;
        trigger = new(this, familyName + "Trigger", requestRender, AddSelections);
        button = new ButtonBehavior(jsRuntime, logger, developmentLog, idGenerator, requestRender, dispatchException, onKeyDown, onKeyUp);
        interactions = new InteractionsTriggerBehavior(jsRuntime, RouteAsync, dispatchException, onClick, onMouseDown);
        reference = new CompositeRootBehavior(jsRuntime, idGenerator, requestRender, dispatchException, onKeyDown);
        guards = new PopupModuleAttachment(jsRuntime);
        guards.Twin.GuardFocused += OnGuardFocused;
        capturePreGuard = element => preGuard = element;
        captureFocusTarget = element => focusTarget = element;
    }

    private bool HasFocusGuards => !InBar && (focusGuards?.Value ?? false);

    private int CloseDelayMilliseconds => (int)(parameters.CloseDelay?.ToMilliseconds() ?? 0);

    /// <summary>Gets the store the trigger belongs to: its handle's, else its root's.</summary>
    public MenuStore<TPayload> Store => trigger.Store;

    /// <summary>Gets a value indicating whether the trigger ignores user interaction: its own, its root's or its menubar's <c>Disabled</c>.</summary>
    public bool Disabled => parameters.Disabled || (rootDisabled?.Value ?? false) || (InBar && parent!.Disabled);

    /// <summary>Gets a value indicating whether the menu is open and this trigger owns it.</summary>
    public bool Open => trigger.IsOpenedByTrigger;

    /// <summary>Gets a value indicating whether the trigger is one of a menubar's.</summary>
    public bool InBar => parent?.Kind == MenuParentKind.Menubar;

    /// <inheritdoc />
    public ElementReference? PreGuard => preGuard;

    /// <inheritdoc />
    public ElementReference? FocusTarget => focusTarget;

    /// <summary>Gets a task that completes when the trigger's handles are attached.</summary>
    public Task TwinsAttached => Task.WhenAll(button.TwinAttached, interactions.TwinAttached, reference.TwinAttached);

    /// <summary>Attaches the handles after the render, in their pinned order, and re-registers the trigger.</summary>
    /// <param name="enabled">Whether the part rendered its element.</param>
    /// <param name="element">The trigger's element.</param>
    /// <param name="id">The id the trigger rendered.</param>
    /// <returns>A task that completes when the handles have their options.</returns>
    public async Task OnAfterRenderAsync(bool enabled, ElementReference? element, string? id)
    {
        await trigger.OnAfterRenderAsync(element);
        MenuStore<TPayload> store = trigger.Store;
        if (trigger.IsMountedByTrigger)
        {
            store.ForwardTriggerState(CloseDelayMilliseconds);
        }

        SyncInteractions();
        SyncList();

        // The handles start attaching in the same render; the list handle awaits the session inside its behavior.
        Task interactionsSync = interactions.AfterRenderAsync(enabled, element, id);
        Task referenceSync = reference.AfterRenderAsync(enabled, element, id);
        Task barSync = barItem?.AfterRenderAsync(element) ?? Task.CompletedTask;
        await button.AfterRenderAsync(enabled, element, id, composite: InBar);
        await interactionsSync;
        await referenceSync;
        await barSync;

        // The `popup` module's Trigger role listens on the guards, attached the first time they render and kept after.
        if (element is { } target && (HasFocusGuards || guards.Attached.IsCompleted))
        {
            await guards.SyncAsync(target, new PopupOptions
            {
                SessionToken = store.SessionToken,
                Trigger = new PopupTriggerOptions { Guards = HasFocusGuards, PopupId = ((IMenuState)store.State).PopupId },
            });
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        trigger.Dispose();
        button.Dispose();
        reference.Dispose();
        barItem?.Dispose();
        parentSubscription?.Dispose();
        guards.Twin.GuardFocused -= OnGuardFocused;
        ValueTask interactionsDisposed = interactions.DisposeAsync();
        ValueTask guardsDisposed = guards.DisposeAsync();
        await interactionsDisposed;
        await guardsDisposed;
    }

    /// <summary>Applies the trigger's parameters and its surroundings.</summary>
    /// <param name="rootStore">The enclosing root's store, or <see langword="null"/>.</param>
    /// <param name="handle">The trigger's <c>Handle</c>, or <see langword="null"/>.</param>
    /// <param name="parent">The enclosing menubar's context, or <see langword="null"/>.</param>
    /// <param name="barRoot">The enclosing menubar's composite root, whose item the trigger is inside a bar.</param>
    /// <param name="id">The trigger's id.</param>
    /// <param name="element">The trigger's element, or <see langword="null"/>.</param>
    /// <param name="payloadSupplied">Whether the <c>Payload</c> parameter was supplied.</param>
    /// <param name="payload">The trigger's <c>Payload</c>.</param>
    /// <param name="parameters">The trigger's other parameters.</param>
    /// <param name="attributes">The consumer's attributes.</param>
    /// <exception cref="InvalidOperationException">The trigger is neither inside a root nor given a handle.</exception>
    public void Apply(
        MenuStore<TPayload>? rootStore,
        MenuHandle<TPayload>? handle,
        IMenuParentContext? parent,
        CompositeRootContext? barRoot,
        string? id,
        ElementReference? element,
        bool payloadSupplied,
        TPayload? payload,
        in MenuTriggerParameters parameters,
        IReadOnlyDictionary<string, object>? attributes)
    {
        this.parameters = parameters;
        this.id = id;

        // MenuTrigger.tsx:84, 394-411: a trigger's own parent is a menubar or none.
        IMenuParentContext? bar = parent?.Kind == MenuParentKind.Menubar ? parent : null;
        if (!ReferenceEquals(this.parent, bar))
        {
            parentSubscription?.Dispose();
            this.parent = bar;
            parentSubscription = bar?.Subscribe(OnParentChanged);
        }

        if (InBar && barItem is null)
        {
            barItem = new CompositeItemBehavior(barRoot, familyName + "Trigger", onKeyDown, requestRender);
        }

        trigger.Apply(rootStore, handle, id, element, payloadSupplied, payload, MenuParts.MissingTriggerRoot(familyName).Message);
        focusGuards!.Arg = id;
        controlsId!.Arg = id;
        MenuStore<TPayload> store = trigger.Store;
        if (store.Popup.IsTriggerActive(id) && store.Popup.EffectiveOpen)
        {
            store.ForwardTriggerState(CloseDelayMilliseconds);
        }

        bool disabled = Disabled;
        button.Native = parameters.NativeButton;
        button.Disabled = disabled;
        button.SetConsumerAttributes(attributes);
        interactions.SetConsumerAttributes(attributes);
        reference.SetConsumerAttributes(attributes);
        barItem?.SetConsumerAttributes(attributes);
        barItem?.SetRegistration(id, attributes, disabled);
        SyncInteractions();
        SyncList();
    }

    /// <summary>Adds the trigger's attributes.</summary>
    /// <param name="attributes">The part's attributes.</param>
    /// <param name="id">The trigger's id.</param>
    public void AddAttributes(AttributeList attributes, string? id)
    {
        button.Disabled = Disabled;
        attributes.Set(PopupMarkerAttributes.ClickTrigger, string.Empty);
        trigger.AddAttributes(attributes, ariaExpanded: true, ariaHasPopup: "menu");

        // MNU-ARIA-03 (MenuStore.ts:100-103): the list when one holds the menu role, else the popup.
        if (controlsId!.Value is { } controls)
        {
            attributes.Set("aria-controls", controls);
        }
        else
        {
            attributes.Remove("aria-controls");
        }

        button.AddAttributes(attributes, id, composite: InBar);
        interactions.AddAttributes(attributes);
        reference.AddAttributes(attributes, id);
        if (barItem is not null)
        {
            barItem.AddAttributes(attributes);

            // MenuTrigger.tsx:258: a bar's trigger is a menu item.
            attributes.Set("role", "menuitem");
        }
    }

    /// <summary>Determines whether the part forwards a consumer handler of the given event.</summary>
    /// <param name="eventName">The event's attribute name, such as <c>onclick</c>.</param>
    /// <returns><see langword="false"/> for a handler the trigger runs from its own.</returns>
    public bool ForwardsConsumerHandler(string eventName) =>
        button.ForwardsConsumerHandler(eventName) && interactions.ForwardsConsumerHandler(eventName) && reference.ForwardsConsumerHandler(eventName)
        && (barItem?.ForwardsConsumerHandler(eventName) ?? true);

    /// <summary>Renders the guard before the trigger while it owns the open popup.</summary>
    /// <param name="builder">The part's render tree builder.</param>
    public void BuildBeforeElement(RenderTreeBuilder builder)
    {
        if (HasFocusGuards)
        {
            RenderGuard(builder, capturePreGuard);
        }
        else
        {
            preGuard = null;
        }
    }

    /// <summary>Renders the guard after the trigger while it owns the open popup.</summary>
    /// <param name="builder">The part's render tree builder.</param>
    public void BuildAfterElement(RenderTreeBuilder builder)
    {
        if (HasFocusGuards)
        {
            RenderGuard(builder, captureFocusTarget);
        }
        else
        {
            focusTarget = null;
        }
    }

    /// <summary>Handles the trigger's <c>click</c>.</summary>
    /// <param name="args">The click.</param>
    /// <returns>A task that completes when the press has run.</returns>
    public Task HandleClickAsync(MouseEventArgs args) => interactions.HandleClickAsync(args);

    /// <summary>Handles the trigger's <c>mousedown</c>, the consumer's veto of the press.</summary>
    /// <param name="args">The <c>mousedown</c>.</param>
    /// <returns>A task that completes when the press has run.</returns>
    public Task HandleMouseDownAsync(MouseEventArgs args) => interactions.HandleMouseDownAsync(args);

    /// <summary>Handles the trigger's <c>keydown</c>.</summary>
    /// <param name="args">The key.</param>
    /// <returns>A task that completes when the key has run.</returns>
    public Task HandleKeyDownAsync(KeyboardEventArgs args)
    {
        if (MenuKeys.IsButtonKey(args.Key))
        {
            return button.HandleKeyDownAsync(args);
        }

        return barItem is not null && MenuKeys.IsNavigationKey(args.Key) ? barItem.HandleKeyDownAsync(args) : reference.HandleKeyDownAsync(args);
    }

    /// <summary>Handles the trigger's <c>keyup</c>.</summary>
    /// <param name="args">The key.</param>
    /// <returns>A task that completes when the key has run.</returns>
    public Task HandleKeyUpAsync(KeyboardEventArgs args) => button.HandleKeyUpAsync(args);

    // FocusGuard.tsx:10-42 (the PopoverTrigger shape): the VoiceOver swap is the `popup` module's write after attach.
    private static void RenderGuard(RenderTreeBuilder builder, Action<ElementReference> capture)
    {
        builder.OpenRegion(0);
        builder.OpenElement(0, "span");
        builder.AddAttribute(1, "tabindex", "0");
        builder.AddAttribute(2, PopupMarkerAttributes.FocusGuard, string.Empty);
        builder.AddAttribute(3, "aria-hidden", "true");
        builder.AddAttribute(4, "style", VisuallyHidden.Style);
        builder.AddElementReferenceCapture(5, capture);
        builder.CloseElement();
        builder.CloseRegion();
    }

    private void AddSelections(SelectorSubscription<MenuStoreState<TPayload>> subscription)
    {
        focusGuards = subscription.Select(MenuSelectors.HasTriggerFocusGuards, id);
        controlsId = subscription.Select(MenuSelectors.TriggerControlsId, id);
        rootDisabled = subscription.Select(MenuSelectors.Disabled);
    }

    // The interactions handle's options (MenuTrigger.tsx:173-213).
    private void SyncInteractions()
    {
        MenuStore<TPayload> store = trigger.Store;
        IMenuState state = store.State;
        interactions.SessionToken = store.SessionToken;
        bool disabled = Disabled;
        bool topLevel = state.Parent.Kind == MenuParentKind.None;
        bool barMenuOpen = InBar && parent!.HasSubmenuOpen;
        bool openOnHover = parameters.OpenOnHover ?? barMenuOpen;
        bool hover = openOnHover && !disabled && (!InBar || (barMenuOpen && !trigger.IsMountedByTrigger));
        InteractionsHoverOptions? hoverOptions = hover
            ? new InteractionsHoverOptions
            {
                RestMs = topLevel ? (int)(parameters.Delay?.ToMilliseconds() ?? DefaultDelay) : 0,
                DelayClose = CloseDelayMilliseconds,
                Move = false,
                MouseOnly = true,
                SafePolygon = true,
                BlockPointerEvents = !InBar,
            }
            : null;
        InteractionsClickOptions? click = disabled
            ? null
            : new InteractionsClickOptions
            {
                Event = Open && InBar ? "click" : "mousedown",
                StickIfOpen = topLevel ? null : false,
                PatientClick = topLevel,
                MouseDownAction = InBar ? "open" : null,
            };
        InteractionsMenuTriggerOptions menu = new() { RootId = state.RootId, Release = topLevel, TrackMouseEnter = true };
        InteractionsFocusOptions? focus = !disabled && barMenuOpen ? s_focus : null;
        InteractionsTriggerOptions current = interactions.Options;
        if (!string.Equals(current.TriggerId, id, StringComparison.Ordinal) || !Equals(current.Click, click) || !Equals(current.Hover, hoverOptions)
            || !Equals(current.Focus, focus) || !Equals(current.Menu, menu))
        {
            interactions.Options = new InteractionsTriggerOptions { TriggerId = id, Click = click, Hover = hoverOptions, Focus = focus, Menu = menu };
        }
    }

    // MenuRoot.tsx:604-636: the active trigger carries the list's reference handle, an inactive or detached one the trigger
    // handle (track 1's roles).
    private void SyncList()
    {
        MenuStore<TPayload> store = trigger.Store;
        CompositeListRole role = store.Popup.IsTriggerActive(id) ? CompositeListRole.Reference : CompositeListRole.Trigger;
        reference.SetList(store.Context?.List, role);
    }

    // A press or a hover of this trigger reaches its store; a press's interaction type is the open method (PSH-PTR-01/02).
    private Task RouteAsync(OpenChangeRequest request)
    {
        MenuStore<TPayload> store = trigger.Store;
        if (request.InteractionType is { } type)
        {
            store.SetOpenInteractionType(type);
        }

        return store.RequestOpenAsync(request.Open, request.Details);
    }

    // useTriggerFocusGuards.ts:42-49: a focused guard closes with `focus-out`, its event and the guard as the trigger.
    private void OnGuardFocused(string guard, EventSnapshot snapshot)
    {
        ElementReference? element = guard == "before" ? preGuard : focusTarget;
        _ = invokeAsync(() => trigger.Store.RequestOpenAsync(false, new OpenChangeEventDetails(EventReason.FocusOut)
        {
            Event = snapshot,
            Trigger = new EventTrigger(null, element),
        }));
    }

    private void OnParentChanged()
    {
        SyncInteractions();
        requestRender();
    }
}

// MenuPopup.tsx:203-212 (FLT-ARIA-03): what the popup's focus manager reads from the active trigger of a top-level menu.
/// <summary>Defines a menu trigger as the popup's focus manager reads its registration.</summary>
internal interface IMenuTriggerPart
{
    /// <summary>Gets the element of the guard before the trigger, while the guards render.</summary>
    ElementReference? PreGuard { get; }

    /// <summary>Gets the element of the guard after the trigger, while the guards render.</summary>
    ElementReference? FocusTarget { get; }
}

using Blazix.Primitives.Interop;
using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace Blazix.Primitives;

// MNU §3.19 (menu/submenu-trigger/MenuSubmenuTrigger.tsx:37-43, 313-342): the parameters a submenu trigger hands its
// behaviour.
/// <summary>Holds the parameters of a menu submenu trigger, as its behavior applies them.</summary>
/// <param name="OpenOnHover">The trigger's <c>OpenOnHover</c>.</param>
/// <param name="Delay">The trigger's <c>Delay</c> (100 milliseconds when <see langword="null"/>).</param>
/// <param name="CloseDelay">The trigger's <c>CloseDelay</c> (no delay when <see langword="null"/>; published for the sibling close).</param>
/// <param name="Item">The trigger's parameters as an item of the parent's list.</param>
internal readonly record struct MenuSubmenuTriggerParameters(bool OpenOnHover, Duration? Delay, Duration? CloseDelay, MenuItemParameters Item);

// Base UI's Menu.SubmenuTrigger (menu/submenu-trigger/MenuSubmenuTrigger.tsx) as one internal behaviour (ADR 08 Amendments
// 2026-11-03 (3)): an item of the parent's list (MenuItemBehavior, kind SubmenuTrigger, no release dispatch) and its
// submenu's trigger, on one element: PopupTriggerBehavior (aria-haspopup="menu", aria-controls while open), the
// `interactions` trigger handle (click on `mousedown`, IgnoreMouse and StickIfOpen = OpenOnHover; hover Move, GuardStaleOpen,
// SafePolygon with BlockPointerEvents, RequireParentMouseEnter with a delay, RequireHoverEnabled), and the submenu list's
// reference handle (the cross-axis open key). aria-expanded is omitted after a keyboard open under VoiceOver
// (PlatformFlags.VoiceOver; ADR 01 Amendments 2026-11-04; MNU-A11Y-01).
/// <summary>The behavior of a menu submenu trigger: an item of the parent menu and the trigger of its submenu.</summary>
internal sealed class MenuSubmenuTriggerBehavior : IAsyncDisposable
{
    // MenuSubmenuTrigger.tsx:39 (MNU-API-51).
    private const int DefaultDelay = 100;

    private readonly string familyName;
    private readonly PlatformFlags platform;
    private readonly Action requestRender;
    private readonly EventCallback onKeyDown;
    private readonly ButtonBehavior button;
    private readonly InteractionsTriggerBehavior interactions;
    private readonly CompositeRootBehavior reference;
    private PopupTriggerBehavior<MenuStore<NoPayload>, MenuStoreState<NoPayload>, NoPayload>? trigger;
    private CompositeItemBehavior? item;
    private MenuRootContext<NoPayload>? submenu;
    private MenuSubscription? parentSubscription;
    private MenuSelection<bool>? parentDisabled;
    private MenuSubmenuTriggerParameters parameters;
    private string? id;
    private bool disposed;

    /// <summary>Initializes a new instance of the <see cref="MenuSubmenuTriggerBehavior"/> class.</summary>
    /// <param name="owner">The trigger part, whose identity registers in the submenu's store.</param>
    /// <param name="familyName">The trigger's family, such as <c>Menu</c>.</param>
    /// <param name="jsRuntime">The runtime the twins import their modules through.</param>
    /// <param name="logger">The part's logger, which receives development warnings.</param>
    /// <param name="developmentLog">The button mechanism's development log.</param>
    /// <param name="idGenerator">The scoped id generator.</param>
    /// <param name="platform">The platform hints.</param>
    /// <param name="requestRender">Asks the part to render.</param>
    /// <param name="invokeAsync">Runs work on the part's renderer.</param>
    /// <param name="dispatchException">Routes an exception to the part.</param>
    /// <param name="onClick">The part's own <c>onclick</c> handler, which calls <see cref="HandleClickAsync"/>.</param>
    /// <param name="onMouseDown">The part's own <c>onmousedown</c> handler, which calls <see cref="HandleMouseDownAsync"/>.</param>
    /// <param name="onKeyDown">The part's own <c>onkeydown</c> handler, which calls <see cref="HandleKeyDownAsync"/>.</param>
    /// <param name="onKeyUp">The part's own <c>onkeyup</c> handler, which calls <see cref="HandleKeyUpAsync"/>.</param>
    public MenuSubmenuTriggerBehavior(
        object owner,
        string familyName,
        IJSRuntime jsRuntime,
        ILogger logger,
        ButtonDevelopmentLog developmentLog,
        PartIdGenerator idGenerator,
        PlatformFlags platform,
        Action requestRender,
        Func<Func<Task>, Task> invokeAsync,
        Func<Exception, Task> dispatchException,
        EventCallback onClick,
        EventCallback onMouseDown,
        EventCallback onKeyDown,
        EventCallback onKeyUp)
    {
        _ = owner;
        _ = invokeAsync;
        this.familyName = familyName;
        this.platform = platform;
        this.requestRender = requestRender;
        this.onKeyDown = onKeyDown;
        button = new ButtonBehavior(jsRuntime, logger, developmentLog, idGenerator, requestRender, dispatchException, onKeyDown, onKeyUp);
        interactions = new InteractionsTriggerBehavior(jsRuntime, RouteAsync, dispatchException, onClick, onMouseDown);
        reference = new CompositeRootBehavior(jsRuntime, idGenerator, requestRender, dispatchException, onKeyDown);
    }

    private int CloseDelayMilliseconds => (int)(parameters.CloseDelay?.ToMilliseconds() ?? 0);

    /// <summary>Gets a value indicating whether the trigger ignores user interaction.</summary>
    public bool Disabled => parameters.Item.Disabled || (submenu?.State.Disabled ?? false) || (parentDisabled?.Value ?? false);

    /// <summary>Gets a value indicating whether the trigger is highlighted in its parent's list, the list handle's mirror.</summary>
    public bool Highlighted => item?.Highlighted ?? false;

    /// <summary>Gets a value indicating whether the submenu is open.</summary>
    public bool Open => trigger?.IsOpenedByTrigger ?? false;

    /// <summary>Gets a task that completes when the trigger's handles are attached.</summary>
    public Task TwinsAttached => Task.WhenAll(button.TwinAttached, interactions.TwinAttached, reference.TwinAttached);

    /// <summary>Attaches the handles after the render, in their pinned order.</summary>
    /// <param name="enabled">Whether the part rendered its element.</param>
    /// <param name="element">The trigger's element.</param>
    /// <param name="id">The id the trigger rendered.</param>
    /// <returns>A task that completes when the handles have their options.</returns>
    public async Task OnAfterRenderAsync(bool enabled, ElementReference? element, string? id)
    {
        await trigger!.OnAfterRenderAsync(element);
        if (trigger.IsMountedByTrigger)
        {
            submenu!.Store.ForwardTriggerState(CloseDelayMilliseconds);
        }

        SyncInteractions();
        Task interactionsSync = interactions.AfterRenderAsync(enabled, element, id);
        Task referenceSync = reference.AfterRenderAsync(enabled, element, id);
        Task itemSync = item!.AfterRenderAsync(element);
        button.Disabled = Disabled;
        await button.AfterRenderAsync(enabled, element, id, composite: true);
        await interactionsSync;
        await referenceSync;
        await itemSync;
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        submenu?.Parent.Menu?.Items.Remove(this);
        parentSubscription?.Dispose();
        trigger?.Dispose();
        button.Dispose();
        reference.Dispose();
        item?.Dispose();
        await interactions.DisposeAsync();
    }

    /// <summary>Checks the part's placement and subscribes to both menus, once.</summary>
    /// <param name="submenu">The enclosing submenu root's context, or <see langword="null"/>.</param>
    /// <param name="parentList">The parent list's composite root, or <see langword="null"/>.</param>
    /// <exception cref="InvalidOperationException">The part is outside a submenu root.</exception>
    public void Subscribe(MenuRootContext? submenu, CompositeRootContext? parentList)
    {
        // MNU-EDGE-03 (MenuSubmenuTrigger.tsx:47-50).
        if (submenu is not MenuRootContext<NoPayload> { Parent.Kind: MenuParentKind.Menu } context)
        {
            throw MenuParts.MissingSubmenuRoot(familyName);
        }

        this.submenu = context;

        // MenuSubmenuTrigger.tsx:129-132: the parent menu's disabled state disables the trigger too, and follows it.
        if (context.Parent.Menu is { } parent)
        {
            parentSubscription = parent.Subscribe(requestRender);
            parentDisabled = parentSubscription.Select(MenuSelectors.Disabled);
        }

        trigger = new(this, familyName + "SubmenuTrigger", requestRender);
        item = new CompositeItemBehavior(parentList, familyName + "SubmenuTrigger", onKeyDown, requestRender);
    }

    /// <summary>Applies the trigger's parameters and its registration.</summary>
    /// <param name="id">The trigger's id.</param>
    /// <param name="element">The trigger's element, or <see langword="null"/>.</param>
    /// <param name="parameters">The trigger's parameters.</param>
    /// <param name="attributes">The consumer's attributes.</param>
    public void Apply(string? id, ElementReference? element, in MenuSubmenuTriggerParameters parameters, IReadOnlyDictionary<string, object>? attributes)
    {
        this.parameters = parameters;
        this.id = id;
        MenuStore<NoPayload> store = submenu!.Store;
        trigger!.Apply(store, null, id, element, false, default, MenuParts.MissingSubmenuRoot(familyName).Message);
        bool disabled = Disabled;
        item!.SetRegistration(id, attributes, disabled, focusableWhenDisabled: true, label: parameters.Item.Label);
        item.SetConsumerAttributes(attributes);
        if (id is not null)
        {
            submenu.Parent.Menu?.Items.Set(this, id);
        }

        button.Native = parameters.Item.NativeButton;
        button.Disabled = disabled;
        button.FocusableWhenDisabled = true;
        button.SetConsumerAttributes(attributes);
        interactions.SetConsumerAttributes(attributes);
        reference.SetConsumerAttributes(attributes);
        reference.SetList(submenu.List, CompositeListRole.Reference);
        SyncInteractions();
    }

    /// <summary>Adds the trigger's attributes.</summary>
    /// <param name="attributes">The part's attributes.</param>
    /// <param name="id">The trigger's id.</param>
    public void AddAttributes(AttributeList attributes, string? id)
    {
        MenuRootContext<NoPayload> menu = submenu!;
        bool open = Open;
        trigger!.AddAttributes(attributes, ariaExpanded: true, ariaHasPopup: "menu");

        // MNU-A11Y-01 (ADR 01 Amendments 2026-11-04): aria-expanded is left out after a keyboard open under VoiceOver.
        if (open && menu.State.KeyboardOpen && platform.VoiceOver == true)
        {
            attributes.Remove("aria-expanded");
        }

        if (open && menu.State.PopupId is { } controls)
        {
            attributes.Set("aria-controls", string.IsNullOrEmpty(menu.State.ListElementId) ? controls : menu.State.ListElementId);
        }
        else
        {
            attributes.Remove("aria-controls");
        }

        button.Disabled = Disabled;
        button.AddAttributes(attributes, id, composite: true);
        interactions.AddAttributes(attributes);
        reference.AddAttributes(attributes, id);
        item!.AddAttributes(attributes);
        attributes.Set("role", "menuitem");
    }

    /// <summary>Determines whether the part forwards a consumer handler of the given event.</summary>
    /// <param name="eventName">The event's attribute name, such as <c>onclick</c>.</param>
    /// <returns><see langword="false"/> for a handler the trigger runs from its own.</returns>
    public bool ForwardsConsumerHandler(string eventName) =>
        button.ForwardsConsumerHandler(eventName) && interactions.ForwardsConsumerHandler(eventName) && reference.ForwardsConsumerHandler(eventName)
        && item!.ForwardsConsumerHandler(eventName);

    /// <summary>Handles the trigger's <c>click</c>.</summary>
    /// <param name="args">The click.</param>
    /// <returns>A task that completes when the press has run.</returns>
    public Task HandleClickAsync(MouseEventArgs args) => interactions.HandleClickAsync(args);

    /// <summary>Handles the trigger's <c>mousedown</c>.</summary>
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

        return MenuKeys.IsNavigationKey(args.Key) ? item!.HandleKeyDownAsync(args) : reference.HandleKeyDownAsync(args);
    }

    /// <summary>Handles the trigger's <c>keyup</c>.</summary>
    /// <param name="args">The key.</param>
    /// <returns>A task that completes when the key has run.</returns>
    public Task HandleKeyUpAsync(KeyboardEventArgs args) => button.HandleKeyUpAsync(args);

    // MenuSubmenuTrigger.tsx:176-204.
    private void SyncInteractions()
    {
        MenuStore<NoPayload> store = submenu!.Store;
        interactions.SessionToken = store.SessionToken;
        bool disabled = Disabled;
        bool openOnHover = parameters.OpenOnHover;
        int delay = (int)(parameters.Delay?.ToMilliseconds() ?? DefaultDelay);
        InteractionsHoverOptions? hover = openOnHover && !disabled
            ? new InteractionsHoverOptions
            {
                DelayOpen = delay,
                DelayClose = CloseDelayMilliseconds,
                RestMs = delay,
                Move = true,
                MouseOnly = true,
                SafePolygon = true,
                BlockPointerEvents = true,
                GuardStaleOpen = true,
                RequireParentMouseEnter = delay > 0,
                RequireHoverEnabled = true,
            }
            : null;
        InteractionsClickOptions? click = disabled ? null : new InteractionsClickOptions { Event = "mousedown", IgnoreMouse = openOnHover, StickIfOpen = openOnHover };
        InteractionsTriggerOptions current = interactions.Options;
        if (!string.Equals(current.TriggerId, id, StringComparison.Ordinal) || !Equals(current.Click, click) || !Equals(current.Hover, hover))
        {
            interactions.Options = new InteractionsTriggerOptions { TriggerId = id, Click = click, Hover = hover };
        }
    }

    // A press or a hover of this trigger reaches the submenu's store; a press's interaction type is its open method.
    private Task RouteAsync(OpenChangeRequest request)
    {
        MenuStore<NoPayload> store = submenu!.Store;
        if (request.InteractionType is { } type)
        {
            store.SetOpenInteractionType(type);
        }

        return store.RequestOpenAsync(request.Open, request.Details);
    }
}

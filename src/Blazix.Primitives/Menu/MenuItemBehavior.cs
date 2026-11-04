using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace Blazix.Primitives;

// useMenuItem.ts:11 and MenuSubmenuTrigger.tsx:152 (`itemMetadata.type`): which item a behaviour serves.
/// <summary>Specifies the kind of a menu item.</summary>
internal enum MenuItemKind
{
    /// <summary>A <c>MenuItem</c>.</summary>
    Item,

    /// <summary>A <c>MenuLinkItem</c>, rendered as an anchor.</summary>
    LinkItem,

    /// <summary>A <c>MenuCheckboxItem</c>.</summary>
    CheckboxItem,

    /// <summary>A <c>MenuRadioItem</c>.</summary>
    RadioItem,

    /// <summary>A <c>MenuSubmenuTrigger</c>, an item of its parent's list.</summary>
    SubmenuTrigger,
}

// MNU §3.9-§3.13 (menu/item/useMenuItem.ts, useMenuItemCommonProps.ts): the parameters an item hands its behaviour.
/// <summary>Holds the parameters of a menu item, as its behavior applies them.</summary>
/// <param name="Disabled">The item's <c>Disabled</c>.</param>
/// <param name="Label">The item's <c>Label</c>, the typeahead text.</param>
/// <param name="NativeButton">The item's <c>NativeButton</c>.</param>
/// <param name="CloseOnClick">The item's <c>CloseOnClick</c>.</param>
internal readonly record struct MenuItemParameters(bool Disabled, string? Label, bool NativeButton, bool CloseOnClick);

// Base UI's useMenuItem and useMenuItemCommonProps as one internal behaviour, composed by every item part and by ContextMenu's
// item shells (track 7):
// - a composite item of the menu's list (track 1's CompositeItemBehavior; SetRegistration(..., focusableWhenDisabled: true,
//   label: Label)); C# renders tabindex="-1" and no data-highlighted (ADR 04 Amendments 2026-11-03 (2), chosen by the
//   user); Highlighted is the mirror, a mirrored field of each item's state record;
// - ButtonBehavior in composite mode (Enter and Space; a typed Space is prevented and skipped, button.ts:516-523), native
//   only with NativeButton, focusable when disabled; a LinkItem is an anchor with no button behaviour;
// - activation: a C# onclick per item (plan §Settled; the Popover and Dialog Close precedent) runs the consumer's handler
//   (PreventBaseUIHandler() keeps the menu open), the part's toggle (a checkbox or radio item's) and, with CloseOnClick, the
//   tree's `item-press` close (MenuRootContext.CloseTreeAsync). It does not fire inside a shadow-root container (ADR 04
//   Amendments 2026-10-29, issue 146), a documented deviation.
/// <summary>The behavior of a menu item: its registration in the list, its button and its activation.</summary>
internal sealed class MenuItemBehavior : IDisposable
{
    private readonly string familyName;
    private readonly string partName;
    private readonly Action requestRender;
    private readonly EventCallback onClick;
    private readonly EventCallback onKeyDown;
    private readonly ButtonBehavior? button;
    private CompositeItemBehavior? item;
    private MenuRootContext? root;
    private MenuSubscription? rootSubscription;
    private MenuSelection<bool>? rootDisabled;
    private MenuItemParameters parameters;
    private string? id;
    private IReadOnlyDictionary<string, object>? attributes;

    /// <summary>Initializes a new instance of the <see cref="MenuItemBehavior"/> class.</summary>
    /// <param name="familyName">The item's family, such as <c>Menu</c>.</param>
    /// <param name="kind">The item's kind.</param>
    /// <param name="jsRuntime">The runtime the twins import their modules through.</param>
    /// <param name="logger">The part's logger.</param>
    /// <param name="developmentLog">The button mechanism's development log.</param>
    /// <param name="idGenerator">The scoped id generator.</param>
    /// <param name="requestRender">Asks the part to render.</param>
    /// <param name="dispatchException">Routes an exception to the part.</param>
    /// <param name="onClick">The part's own <c>onclick</c> handler, which calls <see cref="HandleClickAsync"/>.</param>
    /// <param name="onKeyDown">The part's own <c>onkeydown</c> handler, which calls <see cref="HandleKeyDownAsync"/>.</param>
    /// <param name="onKeyUp">The part's own <c>onkeyup</c> handler, which calls <see cref="HandleKeyUpAsync"/>.</param>
    public MenuItemBehavior(
        string familyName,
        MenuItemKind kind,
        IJSRuntime jsRuntime,
        ILogger logger,
        ButtonDevelopmentLog developmentLog,
        PartIdGenerator idGenerator,
        Action requestRender,
        Func<Exception, Task> dispatchException,
        EventCallback onClick,
        EventCallback onKeyDown,
        EventCallback onKeyUp)
    {
        this.familyName = familyName;
        this.requestRender = requestRender;
        this.onClick = onClick;
        this.onKeyDown = onKeyDown;
        partName = familyName + kind switch
        {
            MenuItemKind.LinkItem => "LinkItem",
            MenuItemKind.CheckboxItem => "CheckboxItem",
            MenuItemKind.RadioItem => "RadioItem",
            MenuItemKind.SubmenuTrigger => "SubmenuTrigger",
            _ => "Item",
        };

        // MNU-API-40: a link item is an anchor with no button behavior.
        if (kind != MenuItemKind.LinkItem)
        {
            button = new ButtonBehavior(jsRuntime, logger, developmentLog, idGenerator, requestRender, dispatchException, onKeyDown, onKeyUp);
        }
    }

    /// <summary>Gets or sets the work an activation runs before the close, such as a checkbox item's toggle.</summary>
    /// <value>A function of the click's snapshot, or <see langword="null"/>.</value>
    public Func<EventSnapshot, Task>? Activated { get; set; }

    /// <summary>Gets the root's context.</summary>
    public MenuRootContext Context => root!;

    /// <summary>Gets a value indicating whether the item ignores user interaction: its own <c>Disabled</c>, or its root's.</summary>
    public bool Disabled => parameters.Disabled || (rootDisabled?.Value ?? false);

    /// <summary>Gets a value indicating whether the item is highlighted, the list handle's mirror.</summary>
    public bool Highlighted => item?.Highlighted ?? false;

    /// <summary>Gets a task that completes when the item's handles are attached.</summary>
    public Task TwinsAttached => button?.TwinAttached ?? Task.CompletedTask;

    /// <summary>Attaches the handles after the render.</summary>
    /// <param name="enabled">Whether the part rendered its element.</param>
    /// <param name="element">The item's element.</param>
    /// <param name="id">The id the item rendered.</param>
    /// <returns>A task that completes when the handles have their options.</returns>
    public async Task OnAfterRenderAsync(bool enabled, ElementReference? element, string? id)
    {
        Task itemSync = item!.AfterRenderAsync(element);
        if (button is not null)
        {
            button.Disabled = Disabled;
            await button.AfterRenderAsync(enabled, element, id, composite: true);
        }

        await itemSync;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        root?.Items.Remove(this);
        rootSubscription?.Dispose();
        button?.Dispose();
        item?.Dispose();
    }

    /// <summary>Checks the part's placement, once.</summary>
    /// <param name="root">The enclosing root's context, or <see langword="null"/>.</param>
    /// <param name="list">The enclosing list's composite root, or <see langword="null"/>.</param>
    /// <exception cref="InvalidOperationException">The part is outside a root.</exception>
    public void Subscribe(MenuRootContext? root, CompositeRootContext? list)
    {
        // MNU-EDGE-03: the Root context error, before the composite item's own.
        this.root = MenuRootContext.Require(root, familyName);
        item = new CompositeItemBehavior(list, partName, onKeyDown, requestRender);

        // MenuItem.tsx:32-37, MenuCheckboxItem.tsx:41-46, MenuRadioItem.tsx:47-49: the root's disabled state disables an item
        // too, and follows it. A LinkItem, which has no button behavior, has no disabled state.
        if (button is not null)
        {
            rootSubscription = this.root.Subscribe(OnRootChanged);
            rootDisabled = rootSubscription.Select(MenuSelectors.Disabled);
        }
    }

    /// <summary>Applies the item's parameters and its registration.</summary>
    /// <param name="id">The item's id.</param>
    /// <param name="parameters">The item's parameters.</param>
    /// <param name="attributes">The consumer's attributes.</param>
    public void Apply(string? id, in MenuItemParameters parameters, IReadOnlyDictionary<string, object>? attributes)
    {
        this.parameters = parameters;
        this.id = id;
        this.attributes = attributes;
        Register();
    }

    /// <summary>Adds the item's attributes.</summary>
    /// <param name="attributes">The part's attributes.</param>
    /// <param name="id">The item's id.</param>
    public void AddAttributes(AttributeList attributes, string? id)
    {
        // The button mechanism writes role="button" on a non-native element; the item's role is written after it, as
        // useMenuItem's props are merged over useButton's.
        attributes.Set("onclick", onClick);
        button?.AddAttributes(attributes, id, composite: true);
        attributes.Set("role", "menuitem");
        item!.AddAttributes(attributes);
    }

    /// <summary>Determines whether the part forwards a consumer handler of the given event.</summary>
    /// <param name="eventName">The event's attribute name, such as <c>onclick</c>.</param>
    /// <returns><see langword="false"/> for a handler the item runs from its own.</returns>
    public bool ForwardsConsumerHandler(string eventName) => (button?.ForwardsConsumerHandler(eventName) ?? true) && item!.ForwardsConsumerHandler(eventName);

    /// <summary>Handles the item's <c>click</c>: the consumer's handler, the activation work and the tree's close.</summary>
    /// <param name="args">The click.</param>
    /// <returns>A task that completes when the activation has run.</returns>
    public async Task HandleClickAsync(MouseEventArgs args)
    {
        if (Disabled)
        {
            return;
        }

        EventSnapshot snapshot = EventSnapshot.FromEventArgs(args);
        if (Activated is { } activated)
        {
            await activated(snapshot);
        }

        // useMenuItemCommonProps.ts:100-106: the tree's `close` with `item-press` (plan §Settled: C# store work).
        if (parameters.CloseOnClick)
        {
            await root!.CloseTreeAsync(EventReason.ItemPress, snapshot);
        }
    }

    /// <summary>Handles the item's <c>keydown</c>.</summary>
    /// <param name="args">The key.</param>
    /// <returns>A task that completes when the key has run.</returns>
    public Task HandleKeyDownAsync(KeyboardEventArgs args) =>
        button is not null && MenuKeys.IsButtonKey(args.Key) ? button.HandleKeyDownAsync(args) : item!.HandleKeyDownAsync(args);

    /// <summary>Handles the item's <c>keyup</c>.</summary>
    /// <param name="args">The key.</param>
    /// <returns>A task that completes when the key has run.</returns>
    public Task HandleKeyUpAsync(KeyboardEventArgs args) => button?.HandleKeyUpAsync(args) ?? Task.CompletedTask;

    // useMenuItem.ts:29-35: focusable when disabled, composite (Space activates on keydown), native only on request.
    private void Register()
    {
        bool disabled = Disabled;
        item!.SetRegistration(id, attributes, disabled, focusableWhenDisabled: true, label: parameters.Label);
        item.SetConsumerAttributes(attributes);
        if (id is not null)
        {
            root!.Items.Set(this, id);
        }

        if (button is not null)
        {
            button.Native = parameters.NativeButton;
            button.Disabled = disabled;
            button.FocusableWhenDisabled = true;
            button.SetConsumerAttributes(attributes);
        }
    }

    private void OnRootChanged()
    {
        Register();
        requestRender();
    }
}

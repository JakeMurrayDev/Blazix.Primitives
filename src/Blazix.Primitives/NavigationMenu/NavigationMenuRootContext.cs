using Blazix.Primitives.Interop;
using Blazix.Primitives.Store;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Blazix.Primitives;

// NAV §11 (NavigationMenuRootContext); navigation-menu/root/NavigationMenuRootContext.ts. The non-generic base every part
// below the Root reads (the Tabs precedent of ADR 08 Amendments 2026-10-26): the store, the Root's options, the element
// registry (NAV-STA-08), the Items, the requests. The value and the Items' comparison live in the generic half, which the
// Items read; an Item tells its Trigger, Icon and Content when its own active state flips, so a switch renders only the
// parts whose state changed (ADR 16 rules 8 and 9).
/// <summary>The state a navigation menu root shares with every part in its subtree, independent of the type of its value.</summary>
internal abstract class NavigationMenuRootContext
{
    // NAV-API-06, NAV-API-07; navigation-menu/utils/constants.ts:1-2 (OPEN_DELAY, CLOSE_DELAY).
    /// <summary>The hover-open delay, in milliseconds, when the root supplies none.</summary>
    public const int DefaultDelay = 50;

    /// <summary>The hover-close delay, in milliseconds, when the root supplies none.</summary>
    public const int DefaultCloseDelay = 50;

    private readonly List<NavigationMenuItemContext> items = [];
    private readonly List<Action> targetListeners = [];
    private readonly Func<ElementReference?> rootElement;

    /// <summary>Initializes a new instance of the <see cref="NavigationMenuRootContext"/> class.</summary>
    /// <param name="store">The root's store.</param>
    /// <param name="rootElement">Reads the root's element after a render.</param>
    protected NavigationMenuRootContext(NavigationMenuStore store, Func<ElementReference?> rootElement)
    {
        Store = store;
        this.rootElement = rootElement;
    }

    /// <summary>Gets the root's store.</summary>
    public NavigationMenuStore Store { get; }

    /// <summary>Gets the root's interactions session token, never rendered.</summary>
    public string SessionToken => Store.SessionToken!;

    /// <summary>Gets the root's element after its first render, which every part's module handle joins.</summary>
    public ElementReference? RootElement => rootElement();

    /// <summary>Gets a value indicating whether the root is nested in another menu's content.</summary>
    public bool Nested { get; internal set; }

    /// <summary>Gets the enclosing root's context, or <see langword="null"/> for a top-level root.</summary>
    public NavigationMenuRootContext? Parent { get; internal set; }

    /// <summary>Gets the axis of the top-level list.</summary>
    public Orientation Orientation { get; internal set; } = Orientation.Horizontal;

    /// <summary>Gets the reading direction.</summary>
    public TextDirection Direction { get; internal set; } = TextDirection.Ltr;

    /// <summary>Gets the hover-open delay in milliseconds.</summary>
    public int DelayMs { get; internal set; } = DefaultDelay;

    /// <summary>Gets the hover-close delay in milliseconds.</summary>
    public int CloseDelayMs { get; internal set; } = DefaultCloseDelay;

    /// <summary>Gets the element the contents move into: the viewport, or its inner target without a positioner.</summary>
    public ElementReference? ViewportTarget { get; private set; }

    /// <summary>Gets the Items of the root, in registration order.</summary>
    public IReadOnlyList<NavigationMenuItemContext> Items => items;

    /// <summary>Throws the error of a navigation menu part rendered outside a root.</summary>
    /// <param name="context">The cascaded context, or <see langword="null"/>.</param>
    /// <returns>The context.</returns>
    /// <exception cref="InvalidOperationException"><paramref name="context"/> is <see langword="null"/>.</exception>
    public static NavigationMenuRootContext Require(NavigationMenuRootContext? context)
    {
        // NAV-EDGE-01: thrown in every build (ADR 02 Amendments 2026-10-20).
        return context ?? throw new InvalidOperationException(
            "Blazix: The `NavigationMenuRoot` context is missing. Navigation Menu parts must be placed within `<NavigationMenuRoot>`.");
    }

    /// <summary>Registers an Item of the root.</summary>
    /// <param name="item">The item's context.</param>
    public void RegisterItem(NavigationMenuItemContext item)
    {
        items.Add(item);
        item.Refresh();
        PublishActiveTrigger();
    }

    /// <summary>Unregisters an Item.</summary>
    /// <param name="item">The item's context.</param>
    public void UnregisterItem(NavigationMenuItemContext item)
    {
        items.Remove(item);
        PublishActiveTrigger();
    }

    /// <summary>Records that an Item's trigger id changed, so the session's active trigger follows.</summary>
    public void TriggerChanged() => PublishActiveTrigger();

    /// <summary>Adds a listener that runs when the viewport target changes.</summary>
    /// <param name="listener">The listener.</param>
    public void AddTargetListener(Action listener) => targetListeners.Add(listener);

    /// <summary>Removes a listener added with <see cref="AddTargetListener"/>.</summary>
    /// <param name="listener">The listener.</param>
    public void RemoveTargetListener(Action listener) => targetListeners.Remove(listener);

    // NAV-STA-08: the Viewport registers the element its contents move into, and unregisters it when it leaves.
    /// <summary>Registers the viewport target, or unregisters it with <see langword="null"/>.</summary>
    /// <param name="target">The element, or <see langword="null"/>.</param>
    public void SetViewportTarget(ElementReference? target)
    {
        if (ViewportTarget is { } current && target is { } next && current.Id == next.Id)
        {
            return;
        }

        if (ViewportTarget is null && target is null)
        {
            return;
        }

        ViewportTarget = target;
        Store.SetViewportTarget(target is not null);
        for (int i = targetListeners.Count - 1; i >= 0; i--)
        {
            if (i < targetListeners.Count)
            {
                targetListeners[i]();
            }
        }
    }

    /// <summary>Requests a close with the given details: the value becomes <see langword="null"/>.</summary>
    /// <param name="details">The details.</param>
    /// <returns><see langword="true"/> if the request was not canceled.</returns>
    public abstract Task<bool> RequestCloseAsync(OpenChangeEventDetails details);

    /// <summary>Routes an interactions request: an open names its trigger, whose Item's value it requests; a close requests <see langword="null"/>.</summary>
    /// <param name="request">The request.</param>
    /// <returns>A task that completes when the request has run.</returns>
    public abstract Task RouteAsync(OpenChangeRequest request);

    // NAV-FOC-10, NAV-FOC-11, NAV-KEY-11; ADR 04 Amendments 2026-10-28: the module's focus-out report is answered, committed
    // or canceled, so it sends the next one.
    /// <summary>Runs a focus-out close the module reported.</summary>
    /// <param name="snapshot">The focus event.</param>
    /// <returns>A task that completes when the request has run.</returns>
    public async Task FocusOutAsync(EventSnapshot? snapshot)
    {
        try
        {
            await RequestCloseAsync(new OpenChangeEventDetails(EventReason.FocusOut, snapshot));
        }
        finally
        {
            Store.Answer();
        }
    }

    /// <summary>Runs an activation a trigger reported for one of the root's Items.</summary>
    /// <param name="item">The item.</param>
    /// <param name="activation">The activation.</param>
    /// <returns>A task that completes when the request, if any, has run.</returns>
    public abstract Task ActivateAsync(NavigationMenuItemContext item, NavigationMenuActivation activation);

    /// <summary>Notifies every Item of a change of the root's value.</summary>
    protected void PublishValue()
    {
        for (int i = items.Count - 1; i >= 0; i--)
        {
            if (i < items.Count)
            {
                items[i].Refresh();
            }
        }

        PublishActiveTrigger();
    }

    // Design point 2 (settled by the orchestrator 2026-11-04): the session's active trigger is the first active Item's.
    private void PublishActiveTrigger()
    {
        string? triggerId = null;
        foreach (NavigationMenuItemContext item in items)
        {
            if (item.IsActive && item.TriggerId is { } id)
            {
                triggerId = id;
                break;
            }
        }

        Store.SetActiveTriggerId(triggerId);
    }
}

// NAV-STA-01..03, NAV-EVT-01..11; navigation-menu/root/NavigationMenuRoot.tsx:202-243. ADR 08 Amendments 2026-11-03 (2):
// `null` is closed; Equality.StrictEquals<TValue> is Base UI's `===` (NAV-STA-03, NAV-EVT-06).
/// <summary>The state a navigation menu root shares with the Items in its subtree.</summary>
/// <typeparam name="TValue">The type of the root's value.</typeparam>
internal sealed class NavigationMenuRootContext<TValue> : NavigationMenuRootContext
{
    private readonly Func<TValue?, OpenChangeEventDetails, Task<bool>> request;

    /// <summary>Initializes a new instance of the <see cref="NavigationMenuRootContext{TValue}"/> class.</summary>
    /// <param name="store">The root's store.</param>
    /// <param name="rootElement">Reads the root's element after a render.</param>
    /// <param name="request">Runs a request for a value: the change callback, the commit, the parent's link-press close.</param>
    public NavigationMenuRootContext(NavigationMenuStore store, Func<ElementReference?> rootElement, Func<TValue?, OpenChangeEventDetails, Task<bool>> request)
        : base(store, rootElement)
    {
        this.request = request;
    }

    /// <summary>Gets the root's value; <see langword="null"/> while closed.</summary>
    public TValue? Value { get; private set; }

    // NAV-STA-11: "active item" is `open && value === itemValue`, and open is `value != null`.
    /// <summary>Determines whether an Item with the given value is the active one.</summary>
    /// <param name="itemValue">The item's value.</param>
    /// <returns><see langword="true"/> when the root's value is not <see langword="null"/> and strictly equals <paramref name="itemValue"/>.</returns>
    public bool IsActive(TValue? itemValue) => Value is not null && Equality.StrictEquals(Value, itemValue);

    /// <summary>Takes the root's value and notifies the Items.</summary>
    /// <param name="value">The value.</param>
    public void SetValue(TValue? value)
    {
        if (Equality.StrictEquals(Value, value))
        {
            return;
        }

        Value = value;
        PublishValue();
    }

    /// <summary>Requests a value.</summary>
    /// <param name="value">The value, or <see langword="null"/> to close.</param>
    /// <param name="details">The details.</param>
    /// <returns><see langword="true"/> if the request was not canceled.</returns>
    public Task<bool> RequestAsync(TValue? value, OpenChangeEventDetails details) => request(value, details);

    /// <inheritdoc />
    public override Task<bool> RequestCloseAsync(OpenChangeEventDetails details) => request(default, details);

    /// <inheritdoc />
    public override Task RouteAsync(OpenChangeRequest request)
    {
        if (!request.Open)
        {
            return RequestCloseAsync(request.Details);
        }

        string? triggerId = request.Details.Trigger?.Id;
        foreach (NavigationMenuItemContext item in Items)
        {
            if (item is NavigationMenuItemContext<TValue> typed && string.Equals(item.TriggerId, triggerId, StringComparison.Ordinal))
            {
                return this.request(typed.Value, request.Details);
            }
        }

        return Task.CompletedTask;
    }

    // NAV-PTR-18, NAV-STA-06, NAV-KEY-01: the direction first, then the request, as Base UI's handleActivation sets the
    // direction inside the flushSync before setValue.
    /// <inheritdoc />
    public override Task ActivateAsync(NavigationMenuItemContext item, NavigationMenuActivation activation)
    {
        if (activation.Direction is { } direction)
        {
            Store.SetActivationDirection(direction);
        }

        if (activation.Reason is not { } reason || item is not NavigationMenuItemContext<TValue> typed)
        {
            return Task.CompletedTask;
        }

        OpenChangeEventDetails details = new(InteractionsLiterals.ParseReason(reason), activation.Event, item.TriggerId is { } id ? new EventTrigger(id, null) : null);
        return request(typed.Value, details);
    }
}

// NAV-STA-10, NAV-STA-11; navigation-menu/item/NavigationMenuItemContext.ts. The non-generic base the Trigger, the Icon and
// the Content read: whether the Item is the active one, which the generic half computes against the Root's value.
/// <summary>The state an Item shares with its Trigger, Icon and Content.</summary>
internal abstract class NavigationMenuItemContext
{
    private readonly List<Action> listeners = [];

    /// <summary>Initializes a new instance of the <see cref="NavigationMenuItemContext"/> class.</summary>
    /// <param name="root">The root's context.</param>
    protected NavigationMenuItemContext(NavigationMenuRootContext root)
    {
        Root = root;
    }

    /// <summary>Gets the root's context.</summary>
    public NavigationMenuRootContext Root { get; }

    /// <summary>Gets a value indicating whether the Item is the active one.</summary>
    public bool IsActive { get; private set; }

    /// <summary>Gets the id of the Item's trigger, or <see langword="null"/> while it renders none.</summary>
    public string? TriggerId { get; private set; }

    /// <summary>Gets a value indicating whether the Item renders a content.</summary>
    public bool HasContent { get; private set; }

    /// <summary>Throws the error of an Item part rendered outside an Item.</summary>
    /// <param name="context">The cascaded context, or <see langword="null"/>.</param>
    /// <returns>The context.</returns>
    /// <exception cref="InvalidOperationException"><paramref name="context"/> is <see langword="null"/>.</exception>
    public static NavigationMenuItemContext Require(NavigationMenuItemContext? context)
    {
        // NAV-EDGE-02: thrown in every build.
        return context ?? throw new InvalidOperationException(
            "Blazix: `NavigationMenuItem` parts must be used within a `<NavigationMenuItem>`.");
    }

    /// <summary>Adds a listener that runs when the Item's active state flips.</summary>
    /// <param name="listener">The listener.</param>
    public void AddListener(Action listener) => listeners.Add(listener);

    /// <summary>Removes a listener added with <see cref="AddListener"/>.</summary>
    /// <param name="listener">The listener.</param>
    public void RemoveListener(Action listener) => listeners.Remove(listener);

    /// <summary>Records the id of the Item's trigger.</summary>
    /// <param name="triggerId">The id, or <see langword="null"/> when the trigger leaves.</param>
    public void SetTriggerId(string? triggerId)
    {
        if (!string.Equals(TriggerId, triggerId, StringComparison.Ordinal))
        {
            TriggerId = triggerId;
            Root.TriggerChanged();
        }
    }

    /// <summary>Records whether the Item renders a content.</summary>
    /// <param name="hasContent">The new value.</param>
    public void SetHasContent(bool hasContent) => HasContent = hasContent;

    /// <summary>Recomputes whether the Item is the active one and notifies its parts when it flipped.</summary>
    public void Refresh()
    {
        bool active = ComputeActive();
        if (active == IsActive)
        {
            return;
        }

        IsActive = active;
        for (int i = listeners.Count - 1; i >= 0; i--)
        {
            if (i < listeners.Count)
            {
                listeners[i]();
            }
        }
    }

    /// <summary>Runs an activation the Item's trigger reported.</summary>
    /// <param name="activation">The activation.</param>
    /// <returns>A task that completes when the request, if any, has run.</returns>
    public Task ActivateAsync(NavigationMenuActivation activation) => Root.ActivateAsync(this, activation);

    /// <summary>Computes whether the Item is the active one against the root's value.</summary>
    /// <returns><see langword="true"/> when the root's value is the Item's.</returns>
    protected abstract bool ComputeActive();
}

// NAV-API-11, NAV-STA-10; navigation-menu/item/NavigationMenuItem.tsx:21-22. ADR 08 Amendments 2026-10-26 (the Accordion
// rule): an Item without a value takes a generated identity only under TValue = string.
/// <summary>The state an Item of a root with values of <typeparamref name="TValue"/> shares with its parts.</summary>
/// <typeparam name="TValue">The type of the root's value.</typeparam>
internal sealed class NavigationMenuItemContext<TValue> : NavigationMenuItemContext
{
    private readonly NavigationMenuRootContext<TValue> root;

    /// <summary>Initializes a new instance of the <see cref="NavigationMenuItemContext{TValue}"/> class.</summary>
    /// <param name="root">The root's context.</param>
    public NavigationMenuItemContext(NavigationMenuRootContext<TValue> root)
        : base(root)
    {
        this.root = root;
    }

    /// <summary>Gets the Item's effective value.</summary>
    public TValue? Value { get; private set; }

    /// <summary>Sets the Item's effective value and recomputes its active state.</summary>
    /// <param name="value">The value.</param>
    public void SetValue(TValue? value)
    {
        Value = value;
        Refresh();
        Root.TriggerChanged();
    }

    /// <inheritdoc />
    protected override bool ComputeActive() => root.IsActive(Value);
}

// The cascades the Root and the Item render, fixed (BLZX2001), built once per content (the TabsRoot and Meter precedent).
internal static class NavigationMenuCascades
{
    public static RenderFragment Cascade<TContext>(TContext context, RenderFragment? content)
        where TContext : class => builder =>
        {
            builder.OpenComponent<CascadingValue<TContext>>(0);
            builder.AddComponentParameter(1, nameof(CascadingValue<TContext>.Value), context);
            builder.AddComponentParameter(2, nameof(CascadingValue<TContext>.IsFixed), true);
            builder.AddComponentParameter(3, nameof(CascadingValue<TContext>.ChildContent), content);
            builder.CloseComponent();
        };

    public static void Render<TContext>(RenderTreeBuilder builder, TContext context, RenderFragment? content)
        where TContext : class => Cascade(context, content)(builder);
}

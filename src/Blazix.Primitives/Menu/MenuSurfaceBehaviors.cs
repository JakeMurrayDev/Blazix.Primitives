using System.Globalization;
using Blazix.Primitives.Interop;
using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.JSInterop;

namespace Blazix.Primitives;

// Base UI's Menu.Backdrop (menu/backdrop/MenuBackdrop.tsx), the PopoverBackdrop form: role="presentation", hidden while
// unmounted, `user-select: none`, `pointer-events: none` after a hover open (MNU-DOM-10), its transitions handle, and its
// element registered with a context menu parent (MNU-EDGE-15). Both MenuBackdrop and ContextMenuBackdrop (track 7) are
// shells over it.
/// <summary>The behavior of a menu backdrop.</summary>
internal sealed class MenuBackdropBehavior : IAsyncDisposable
{
    private const string BaseStyle = "user-select: none; -webkit-user-select: none";
    private const string HoverStyle = "user-select: none; -webkit-user-select: none; pointer-events: none";
    private const string OpenSelector = "[data-open]";

    private readonly string familyName;
    private readonly IJSRuntime jsRuntime;
    private readonly Action requestRender;
    private MenuRootContext? root;
    private IMenuParentContext? parentContext;
    private MenuSubscription? subscription;
    private MenuSelection<bool>? open;
    private MenuSelection<bool>? mounted;
    private MenuSelection<TransitionStatus?>? status;
    private MenuSelection<bool>? hoverOpened;
    private MenuSelection<int>? unmountRequests;
    private MenuSelection<bool>? retainExit;
    private IDisposable? retainRegistration;
    private TransitionsTwin? transitions;
    private TwinHandle<TransitionsOptions>? transitionsHandle;
    private TransitionsOptions? sentTransitions;
    private Task? attaching;
    private ElementReference? element;
    private bool announcedRetain;
    private bool disposed;

    /// <summary>Initializes a new instance of the <see cref="MenuBackdropBehavior"/> class.</summary>
    /// <param name="familyName">The backdrop's family, such as <c>Menu</c>.</param>
    /// <param name="jsRuntime">The runtime the twin imports its module through.</param>
    /// <param name="requestRender">Asks the part to render.</param>
    /// <param name="invokeAsync">Runs work on the part's renderer.</param>
    public MenuBackdropBehavior(string familyName, IJSRuntime jsRuntime, Action requestRender, Func<Func<Task>, Task> invokeAsync)
    {
        _ = invokeAsync;
        this.familyName = familyName;
        this.jsRuntime = jsRuntime;
        this.requestRender = requestRender;
    }

    /// <summary>Gets a value indicating whether the menu is open.</summary>
    public bool Open => open!.Value;

    /// <summary>Gets the transition status the backdrop renders.</summary>
    public TransitionStatus? TransitionStatus => status!.Value;

    /// <summary>Attaches the transitions handle after the render.</summary>
    /// <param name="element">The backdrop's element.</param>
    /// <returns>A task that completes when the handle has its options.</returns>
    public async Task OnAfterRenderAsync(ElementReference? element)
    {
        this.element = element;
        if (root!.Parent.Kind == MenuParentKind.ContextMenu || parentContext?.Kind == MenuParentKind.ContextMenu)
        {
            parentContext?.SetBackdropElement(element);
        }

        if (element is not { } target || disposed)
        {
            return;
        }

        transitions ??= new TransitionsTwin(jsRuntime);
        if (transitionsHandle is null)
        {
            attaching ??= AttachAsync(target, transitions);
            await attaching;
        }
        else if (TransitionsOptions() is { } options && options != sentTransitions)
        {
            sentTransitions = options;
            await transitionsHandle.UpdateAsync(options);
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        disposed = true;
        subscription?.Dispose();
        subscription = null;
        retainRegistration?.Dispose();
        if (parentContext?.Kind == MenuParentKind.ContextMenu)
        {
            parentContext.SetBackdropElement(null);
        }

        if (transitions is not null)
        {
            await transitions.DisposeAsync();
        }
    }

    /// <summary>Checks the part's placement and subscribes to the root's state, once.</summary>
    /// <param name="root">The enclosing root's context, or <see langword="null"/>.</param>
    /// <param name="parentContext">The enclosing menubar's or context menu's context, or <see langword="null"/>.</param>
    /// <exception cref="InvalidOperationException">The part is outside a root.</exception>
    public void Subscribe(MenuRootContext? root, IMenuParentContext? parentContext)
    {
        // MNU-STR-07's correction, MNU-EDGE-03 (MenuBackdrop.tsx:23): the backdrop throws the Root context error.
        MenuRootContext menu = MenuRootContext.Require(root, familyName);
        this.root = menu;
        this.parentContext = parentContext;
        subscription = menu.Subscribe(requestRender);
        open = subscription.Select(MenuSelectors.Open);
        mounted = subscription.Select(MenuSelectors.Mounted);
        status = subscription.Select(MenuSelectors.TransitionStatus);
        hoverOpened = subscription.Select(MenuSelectors.HoverOpened);
        unmountRequests = subscription.Select(MenuSelectors.UnmountRequests);
        retainExit = subscription.Select(MenuSelectors.PreventUnmountingOnClose);
        retainRegistration = menu.OnUnmountPreventionChanging(AnnounceRetainExit);
    }

    /// <summary>Adds the backdrop's attributes.</summary>
    /// <param name="attributes">The part's attributes.</param>
    public void AddAttributes(AttributeList attributes)
    {
        attributes.Set("role", "presentation");
        if (!mounted!.Value)
        {
            attributes.Set("hidden", true);
        }

        attributes.Set(MergeProps.StyleAttribute, hoverOpened!.Value ? HoverStyle : BaseStyle);
    }

    private TransitionsOptions TransitionsOptions() => new()
    {
        Open = open!.Value,
        Mounted = mounted!.Value,
        Status = status!.Value,
        UnmountRequests = unmountRequests!.Value,
        OpenSelector = OpenSelector,
        RetainExit = retainExit!.Value,
    };

    private void AnnounceRetainExit(bool retain)
    {
        if (transitions is null || element is not { } target || announcedRetain == retain)
        {
            return;
        }

        announcedRetain = retain;
        _ = transitions.RetainExitAsync(target, retain).AsTask();
    }

    private async Task AttachAsync(ElementReference target, TransitionsTwin twin)
    {
        await twin.PrepareAsync();
        if (disposed || transitions != twin)
        {
            return;
        }

        TransitionsOptions options = TransitionsOptions();
        sentTransitions = options;
        transitionsHandle = await twin.AttachAsync(target, options);
        attaching = null;
    }
}

// Base UI's Menu.Viewport (menu/viewport/MenuViewport.tsx) over the `popup` module's viewport role (PopupTwin), as
// Popover's (the Viewport Writer cells are JS: a correction of MNU §3.7 by a dated note, Interop/PopupPayloads.cs:8). A
// mounted viewport turns on the positioner's adaptive origin (MNU-POS-08).
/// <summary>The behavior of a menu viewport.</summary>
internal sealed class MenuViewportBehavior : IAsyncDisposable
{
    private readonly string familyName;
    private readonly Action requestRender;
    private readonly Func<Func<Task>, Task> invokeAsync;
    private readonly PopupModuleAttachment module;
    private readonly RenderFragment current;
    private MenuRootContext? root;
    private PopupPortalContext? portal;
    private MenuSubscription? subscription;
    private MenuSelection<MenuInstantType?>? instant;
    private MenuSelection<string?>? activeTriggerId;
    private MenuSelection<int>? payloadVersion;
    private MenuSelection<bool>? open;
    private MenuSelection<string?>? popupId;
    private RenderFragment? consumerContent;
    private string? keyedTriggerId;
    private int keyedPayloadVersion;
    private bool payloadPending;
    private int contentKey;
    private string? key;

    /// <summary>Initializes a new instance of the <see cref="MenuViewportBehavior"/> class.</summary>
    /// <param name="familyName">The viewport's family, such as <c>Menu</c>.</param>
    /// <param name="jsRuntime">The runtime the twin imports its module through.</param>
    /// <param name="requestRender">Asks the part to render.</param>
    /// <param name="invokeAsync">Runs work on the part's renderer.</param>
    public MenuViewportBehavior(string familyName, IJSRuntime jsRuntime, Action requestRender, Func<Func<Task>, Task> invokeAsync)
    {
        this.familyName = familyName;
        this.requestRender = requestRender;
        this.invokeAsync = invokeAsync;
        module = new PopupModuleAttachment(jsRuntime);
        module.Twin.ViewportChanged += report => _ = invokeAsync(() =>
        {
            Mirror(report.ActivationDirection, report.Transitioning);
            return Task.CompletedTask;
        });
        current = RenderCurrent;
    }

    /// <summary>Gets the mirrored activation direction.</summary>
    public string? ActivationDirection { get; private set; }

    /// <summary>Gets a value indicating whether the viewport is moving between two triggers' contents, as mirrored.</summary>
    public bool Transitioning { get; private set; }

    /// <summary>Gets the instant type the viewport renders.</summary>
    public MenuInstantType? Instant => instant!.Value;

    /// <summary>Attaches the viewport role after the render.</summary>
    /// <param name="element">The viewport's element.</param>
    /// <returns>A task that completes when the handle has its options.</returns>
    public async Task OnAfterRenderAsync(ElementReference? element)
    {
        if (element is not { } target)
        {
            return;
        }

        await PopupPortalContext.WhenMoved(portal);
        await module.SyncAsync(target, new PopupOptions
        {
            SessionToken = root!.SessionToken,
            Viewport = new PopupViewportOptions
            {
                Open = open!.Value,
                PopupId = popupId!.Value,
                ContentVersion = payloadVersion!.Value,
            },
        });
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        subscription?.Dispose();
        subscription = null;
        root?.SetAdaptiveOrigin(false);
        await module.DisposeAsync();
    }

    /// <summary>Checks the part's placement and subscribes to the root's state, once.</summary>
    /// <param name="root">The enclosing root's context, or <see langword="null"/>.</param>
    /// <param name="positioner">The enclosing positioner's context, or <see langword="null"/>.</param>
    /// <param name="portal">The enclosing portal node's context, or <see langword="null"/>.</param>
    /// <exception cref="InvalidOperationException">The part is outside a root or a positioner.</exception>
    public void Subscribe(MenuRootContext? root, PositionerContext? positioner, PopupPortalContext? portal)
    {
        MenuRootContext menu = MenuRootContext.Require(root, familyName);
        if (positioner is null)
        {
            throw MenuParts.MissingPositioner(familyName);
        }

        this.root = menu;
        this.portal = portal;
        subscription = menu.Subscribe(requestRender);
        instant = subscription.Select(MenuSelectors.InstantType);
        activeTriggerId = subscription.Select(MenuSelectors.ActiveTriggerId);
        payloadVersion = subscription.Select(MenuSelectors.PayloadVersion);
        open = subscription.Select(MenuSelectors.Open);
        popupId = subscription.Select(MenuSelectors.PopupId);
        keyedTriggerId = activeTriggerId.Value;
        keyedPayloadVersion = payloadVersion.Value;
        menu.SetAdaptiveOrigin(true);
    }

    /// <summary>Wraps the consumer's content in the keyed current container.</summary>
    /// <param name="content">The consumer's content.</param>
    /// <returns>The content to render inside the viewport.</returns>
    public RenderFragment CascadeContent(RenderFragment? content)
    {
        consumerContent = content;
        return current;
    }

    // usePopupViewport.tsx:368-399 (usePopupContentKey): remount on a trigger change, and once more if the payload had not
    // caught up with it.
    /// <summary>Updates the current container's key before a render: a new key on a trigger change remounts the content.</summary>
    public void BeforeRender()
    {
        string? triggerId = activeTriggerId!.Value;
        int version = payloadVersion!.Value;
        bool triggerChanged = !string.Equals(triggerId, keyedTriggerId, StringComparison.Ordinal);
        bool payloadChanged = version != keyedPayloadVersion;
        if (triggerChanged)
        {
            contentKey++;
            payloadPending = !payloadChanged;
            key = null;
        }
        else if (payloadPending && payloadChanged)
        {
            contentKey++;
            payloadPending = false;
            key = null;
        }

        keyedTriggerId = triggerId;
        keyedPayloadVersion = version;
        key ??= string.Create(CultureInfo.InvariantCulture, $"{triggerId ?? "current"}-{contentKey}");
    }

    private void Mirror(string? direction, bool isTransitioning)
    {
        if (string.Equals(ActivationDirection, direction, StringComparison.Ordinal) && Transitioning == isTransitioning)
        {
            return;
        }

        ActivationDirection = direction;
        Transitioning = isTransitioning;
        requestRender();
    }

    private void RenderCurrent(RenderTreeBuilder builder)
    {
        builder.OpenElement(0, "div");
        builder.SetKey(key);
        builder.AddAttribute(1, PopupViewportDataAttributes.Current, string.Empty);
        builder.AddContent(2, consumerContent);
        builder.CloseElement();
    }
}

// Base UI's Menu.Arrow (menu/arrow/MenuArrow.tsx) over PositionerArrowBehavior, as Popover's: aria-hidden, Side, Align and
// Uncentered mirrored (MNU-DOM-11, POS-07).
/// <summary>The behavior of a menu arrow.</summary>
internal sealed class MenuArrowBehavior : IDisposable
{
    private readonly string familyName;
    private readonly Action requestRender;
    private readonly PositionerArrowBehavior arrow;
    private MenuSubscription? subscription;
    private MenuSelection<bool>? open;

    /// <summary>Initializes a new instance of the <see cref="MenuArrowBehavior"/> class.</summary>
    /// <param name="familyName">The arrow's family, such as <c>Menu</c>.</param>
    /// <param name="requestRender">Asks the part to render, after a change of the menu's state.</param>
    /// <param name="mirrorChanged">Tells the part that the mirrored placement changed, which renders only when its state record changed.</param>
    public MenuArrowBehavior(string familyName, Action requestRender, Action mirrorChanged)
    {
        this.familyName = familyName;
        this.requestRender = requestRender;
        arrow = new PositionerArrowBehavior(mirrorChanged);
    }

    /// <summary>Gets a value indicating whether the menu is open.</summary>
    public bool Open => open!.Value;

    /// <summary>Gets the mirrored side.</summary>
    public Side? Side => arrow.Mirror.Side;

    /// <summary>Gets the mirrored alignment.</summary>
    public Align? Align => arrow.Mirror.Align;

    /// <summary>Gets a value indicating whether the arrow cannot point at the anchor's center, as mirrored.</summary>
    public bool Uncentered => arrow.Mirror.Uncentered;

    /// <summary>Flushes the positioner after the render, which places the arrow.</summary>
    /// <returns>A task that completes when the positioner has flushed.</returns>
    public Task OnAfterRenderAsync() => arrow.AfterRenderAsync();

    /// <inheritdoc />
    public void Dispose()
    {
        subscription?.Dispose();
        subscription = null;
        arrow.Dispose();
    }

    /// <summary>Checks the part's placement and subscribes, once.</summary>
    /// <param name="root">The enclosing root's context, or <see langword="null"/>.</param>
    /// <param name="positioner">The enclosing positioner's context, or <see langword="null"/>.</param>
    /// <exception cref="InvalidOperationException">The part is outside a root or a positioner.</exception>
    public void Subscribe(MenuRootContext? root, PositionerContext? positioner)
    {
        MenuRootContext menu = MenuRootContext.Require(root, familyName);
        if (positioner is null)
        {
            throw MenuParts.MissingPositioner(familyName);
        }

        subscription = menu.Subscribe(requestRender);
        open = subscription.Select(MenuSelectors.Open);
    }

    /// <summary>Applies the positioner's context.</summary>
    /// <param name="positioner">The enclosing positioner's context.</param>
    public void Apply(PositionerContext positioner) => arrow.SetContext(positioner);

    /// <summary>Adds the arrow's attributes.</summary>
    /// <param name="attributes">The part's attributes.</param>
    public void AddAttributes(AttributeList attributes) => arrow.AddAttributes(attributes);

    /// <summary>Finishes the arrow's attributes after the consumer's: the handover of its style at attach.</summary>
    /// <param name="attributes">The part's attributes.</param>
    /// <param name="id">The id the arrow renders.</param>
    public void FinishAttributes(AttributeList attributes, string id) => arrow.FinishAttributes(attributes, id);
}

// Base UI's plain Menu.List (menu/list/MenuList.tsx): role="menu", tabindex="-1", aria-orientation when horizontal, the
// popup's label (resolvePopupLabel, UTL-ARIA-01) and its id published to the store for the trigger's aria-controls; the
// popup turns presentational while it is mounted (MNU-STR-13, DOM-24, ARIA-05). List navigation stays on the popup, whose
// element Base UI gives the list's floating props (MenuRoot.tsx:650-680). The filter List is track 12's.
/// <summary>The behavior of a menu list.</summary>
internal sealed class MenuListBehavior : IDisposable
{
    private readonly string familyName;
    private readonly Action requestRender;
    private MenuRootContext? root;
    private MenuSubscription? subscription;
    private MenuSelection<string?>? activeTriggerId;
    private IReadOnlyDictionary<string, object>? attributes;
    private string? id;

    /// <summary>Initializes a new instance of the <see cref="MenuListBehavior"/> class.</summary>
    /// <param name="familyName">The list's family, such as <c>Menu</c>.</param>
    /// <param name="requestRender">Asks the part to render.</param>
    public MenuListBehavior(string familyName, Action requestRender)
    {
        this.familyName = familyName;
        this.requestRender = requestRender;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        subscription?.Dispose();
        subscription = null;
        if (root is not null && string.Equals(root.State.ListElementId, id, StringComparison.Ordinal))
        {
            root.SetListElementId(null);
        }
    }

    /// <summary>Checks the part's placement and subscribes to the root's state, once.</summary>
    /// <param name="root">The enclosing root's context, or <see langword="null"/>.</param>
    /// <exception cref="InvalidOperationException">The part is outside a root.</exception>
    public void Subscribe(MenuRootContext? root)
    {
        MenuRootContext menu = MenuRootContext.Require(root, familyName);
        this.root = menu;
        subscription = menu.Subscribe(requestRender);
        activeTriggerId = subscription.Select(MenuSelectors.ActiveTriggerId);
    }

    /// <summary>Applies the list's rendered id and publishes it to the store.</summary>
    /// <param name="id">The id the list renders.</param>
    /// <param name="attributes">The consumer's attributes, whose own label wins.</param>
    public void Apply(string? id, IReadOnlyDictionary<string, object>? attributes)
    {
        this.id = id;
        this.attributes = attributes;

        // MenuList.tsx:23-33 (MNU-EDGE-09): the trigger's aria-controls follows the list's id.
        root!.SetListElementId(id);
    }

    /// <summary>Adds the list's attributes.</summary>
    /// <param name="attributes">The part's attributes.</param>
    public void AddAttributes(AttributeList attributes)
    {
        attributes.Set("role", "menu");
        attributes.Set("tabindex", "-1");
        if (root!.Orientation == Orientation.Horizontal)
        {
            attributes.Set("aria-orientation", "horizontal");
        }

        if (PopupLabel.Resolve(this.attributes, activeTriggerId!.Value) is { } label)
        {
            attributes.Set("aria-labelledby", label);
        }
    }
}

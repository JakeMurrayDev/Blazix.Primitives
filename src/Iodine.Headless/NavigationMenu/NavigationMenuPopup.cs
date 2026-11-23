using Blazix.Primitives.Interop;
using Blazix.Primitives.Rendering;
using Blazix.Primitives.Store;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Blazix.Primitives;

// NAV-STA-16, §3.11 state object (navigation-menu/popup/NavigationMenuPopup.tsx:32-38). TransitionStatus is the
// `transitions` module's (ADR 04 §2): mirrored. Side, Align and AnchorHidden are C#-rendered from the Positioner's mirror
// (the POP-DOM-27 precedent; contradiction 5 of sheet F), a round trip behind the Positioner's on Server.
/// <summary>Represents the state of a navigation menu popup, which the part exposes as data attributes and passes to its class and style functions.</summary>
/// <param name="Open">Whether the menu is open; projected as <c>data-open</c> or <c>data-closed</c>.</param>
/// <param name="TransitionStatus">The popup's transition status, rendered as <c>data-starting-style</c> and <c>data-ending-style</c> by the browser-side behavior.</param>
/// <param name="Side">The side the popup is placed on; projected as <c>data-side</c>.</param>
/// <param name="Align">The alignment of the popup; projected as <c>data-align</c>.</param>
/// <param name="AnchorHidden">Whether the anchor is hidden; projected as <c>data-anchor-hidden</c>.</param>
[PartState]
public sealed partial record NavigationMenuPopupState(
    bool Open,
    [property: MirroredField] TransitionStatus? TransitionStatus,
    Side? Side,
    Align? Align,
    bool AnchorHidden);

// navigation-menu/popup/NavigationMenuPopupDataAttributes.ts (NAV-DOM-32).
/// <summary>Provides the names of the data attributes a navigation menu popup renders.</summary>
public static class NavigationMenuPopupDataAttributes
{
    /// <summary>Represents the attribute present while the menu is open: <c>data-open</c>.</summary>
    public const string Open = PopupDataAttributes.Open;

    /// <summary>Represents the attribute present while the menu is closed: <c>data-closed</c>.</summary>
    public const string Closed = PopupDataAttributes.Closed;

    /// <summary>Represents the attribute present while the popup is animating in: <c>data-starting-style</c>.</summary>
    public const string StartingStyle = PopupDataAttributes.StartingStyle;

    /// <summary>Represents the attribute present while the popup is animating out: <c>data-ending-style</c>.</summary>
    public const string EndingStyle = PopupDataAttributes.EndingStyle;

    /// <summary>Represents the attribute present while the anchor is hidden: <c>data-anchor-hidden</c>.</summary>
    public const string AnchorHidden = PopupDataAttributes.AnchorHidden;

    /// <summary>Represents the attribute whose value is the side the popup is placed on: <c>data-side</c>.</summary>
    public const string Side = PopupDataAttributes.Side;

    /// <summary>Represents the attribute whose value is the alignment of the popup: <c>data-align</c>.</summary>
    public const string Align = PopupDataAttributes.Align;
}

// navigation-menu/popup/NavigationMenuPopupCssVars.ts (NAV-DOM-33).
/// <summary>Provides the names of the CSS variables a navigation menu popup carries.</summary>
public static class NavigationMenuPopupCssVars
{
    /// <summary>Represents the variable that holds the popup's width during a size transition: <c>--popup-width</c>.</summary>
    public const string PopupWidth = PopupCssVars.PopupWidth;

    /// <summary>Represents the variable that holds the popup's height during a size transition: <c>--popup-height</c>.</summary>
    public const string PopupHeight = PopupCssVars.PopupHeight;
}

// Base UI's NavigationMenu.Popup (navigation-menu/popup/NavigationMenuPopup.tsx); the contract is
// docs/components/navigation-menu.md §3.11, §5.11, §9.
// - A `nav` with the floating id unless the consumer's (NAV-API-17, NAV-DOM-30), tabindex="-1"; its id is the Root's
//   registry entry the active Trigger's aria-controls names (NAV-ARIA-02) and the session's floating id.
// - Its TransitionsTwin: the completion watcher (NAV-STA-05, NAV-ANI-02), OpenSelector [data-open], UnmountRequests,
//   RetainExit for the opt-out (NAV-EVT-05), DisableTransitionWhileStarting (NAV-ANI-15), no ReportOpenComplete (§16 Q8);
//   its style is handed over at attach, so the modules own it: `transition: none`, the size variables and the anchoring
//   styles (NAV-DOM-31; design point 6, settled by the orchestrator 2026-11-04: the `navigation-menu` module's).
// - It selects the open and mounted state, so it registers as a boundary part (ADR 16 Amendments 2026-10-27).
/// <summary>
/// Represents the container of the navigation menu's content: Base UI's <c>NavigationMenu.Popup</c>, rendered as a
/// <c>nav</c>.
/// </summary>
public sealed class NavigationMenuPopup : PartBase<NavigationMenuPopupState>
{
    private const string OpenSelector = "[data-open]";
    private const string StartingStyle = "transition: none";

    private static readonly StateAttributesMapping<NavigationMenuPopupState> s_mapping = PopupStateMapping.CreatePopup<NavigationMenuPopupState>();

    private readonly Action positionerChanged;
    private NavigationMenuRootContext? root;
    private NavigationMenuTransitions? transitions;
    private NavigationMenuModule? module;
    private StoreSelection<NavigationMenuStoreState, bool>? open;
    private StoreSelection<NavigationMenuStoreState, bool>? mounted;
    private StoreSelection<NavigationMenuStoreState, TransitionStatus?>? status;
    private StoreSelection<NavigationMenuStoreState, int>? unmountRequests;
    private StoreSelection<NavigationMenuStoreState, bool>? retainExit;
    private StoreSelection<NavigationMenuStoreState, string?>? floatingId;
    private IDisposable? boundaryPart;
    private IDisposable? retainRegistration;
    private NavigationMenuPopupState? state;
    private string? frozenStyle;
    private string? consumerStyle;
    private bool disposed;

    /// <summary>Initializes a new instance of the <see cref="NavigationMenuPopup"/> class.</summary>
    public NavigationMenuPopup()
    {
        positionerChanged = OnPositionerChanged;
    }

    [Inject]
    private IJSRuntime JSRuntime { get; set; } = null!;

    private bool HasConsumerId => AdditionalAttributes is not null && AdditionalAttributes.ContainsKey("id");

    private string? RenderedId => HasConsumerId ? AdditionalAttributes!["id"] as string : floatingId!.Value;

    [CascadingParameter]
    private NavigationMenuRootContext? RootContext { get; set; }

    [CascadingParameter]
    private PositionerContext? PositionerContext { get; set; }

    [CascadingParameter]
    private PopupPortalContext? Portal { get; set; }

    /// <inheritdoc />
    protected override string DefaultTagName => "nav";

    // The pipeline reads the state before the part's attributes, so the record follows the store and the mirror here.
    /// <inheritdoc />
    protected override NavigationMenuPopupState State
    {
        get
        {
            PositionerMirror mirror = PositionerContext!.Mirror;
            bool isOpen = open!.Value;
            TransitionStatus? current = status!.Value;
            if (state is null || state.Open != isOpen || state.TransitionStatus != current || state.Side != mirror.Side || state.Align != mirror.Align || state.AnchorHidden != mirror.AnchorHidden)
            {
                state = new NavigationMenuPopupState(isOpen, current, mirror.Side, mirror.Align, mirror.AnchorHidden);
            }

            return state;
        }
    }

    /// <inheritdoc />
    protected override StateAttributesMapping<NavigationMenuPopupState>? StateAttributesMapping => s_mapping;

    /// <summary>Gets a task that completes once the popup's handles have attached.</summary>
    internal Task TwinsAttached => Task.WhenAll(transitions!.Attached, module!.Attached);

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        root = NavigationMenuRootContext.Require(RootContext);
        NavigationMenuArrow.RequirePositioner(PositionerContext);
        NavigationMenuStore store = root.Store;
        transitions = new NavigationMenuTransitions(JSRuntime);
        transitions.Twin.StatusChanged += change => _ = InvokeAsync(() => root.Store.MirrorStatus(change.Status));
        transitions.Twin.TransitionCompleted += opened => _ = InvokeAsync(() => root.Store.CompleteTransitionAsync(opened));
        module = new NavigationMenuModule(JSRuntime);

        // Issue 147 ((B1)): the opt-out is announced through the transitions module's export ahead of any render of the close.
        retainRegistration = store.OnUnmountPreventionChanging(retain => transitions.AnnounceRetainExit(Element, retain));
        boundaryPart = store.RegisterBoundaryPart();
        PositionerContext!.AddListener(positionerChanged);
        base.OnInitialized();
    }

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        NavigationMenuRootContext menu = root!;
        if (Element is { } element && !disposed)
        {
            menu.Store.SetPopupId(RenderedId);
            await PopupPortalContext.WhenMoved(Portal);
            if (!disposed)
            {
                Task transitionsSync = transitions!.SyncAsync(element, TransitionsOptions);
                await module!.SyncAsync(element, new NavigationMenuOptions { Part = NavigationMenuTwin.PopupPart }, menu.RootElement);
                await transitionsSync;
            }
        }

        await menu.Store.AtBatchBoundaryAsync();
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing && !disposed)
        {
            disposed = true;
            PositionerContext?.RemoveListener(positionerChanged);
            retainRegistration?.Dispose();
            boundaryPart?.Dispose();
            root?.Store.SetPopupId(null);
            if (transitions is not null)
            {
                _ = transitions.DisposeAsync().AsTask();
            }

            if (module is not null)
            {
                _ = module.DisposeAsync().AsTask();
            }
        }

        base.Dispose(disposing);
    }

    /// <inheritdoc />
    protected override void SubscribeSelectors()
    {
        SelectorSubscription<NavigationMenuStoreState> subscription = SubscribeTo(RootContext!.Store);
        open = subscription.Select(NavigationMenuSelectors.Open);
        mounted = subscription.Select(NavigationMenuSelectors.Mounted);
        status = subscription.Select(NavigationMenuSelectors.TransitionStatus);
        unmountRequests = subscription.Select(NavigationMenuSelectors.UnmountRequests);
        retainExit = subscription.Select(NavigationMenuSelectors.PreventUnmountingOnClose);
        floatingId = subscription.Select(NavigationMenuSelectors.FloatingId);
    }

    /// <inheritdoc />
    protected override void AddPartAttributes(AttributeList attributes)
    {
        if (!HasConsumerId)
        {
            attributes.Set("id", floatingId!.Value);
        }

        attributes.Set("tabindex", "-1");
    }

    // ADR 04 Amendments 2026-10-18 (handover at attach): before attach C# renders `transition: none` while starting
    // (NAV-ANI-15) before the consumer's style; from attach the style is frozen and the modules own it.
    /// <inheritdoc />
    protected override void FinishAttributes(AttributeList attributes)
    {
        consumerStyle = attributes.TryGetValue(MergeProps.StyleAttribute, out AttributeValue merged) && merged.TryGetValue(out string text) && text.Length > 0 ? text : null;
        if (frozenStyle is null)
        {
            string? own = status!.Value == TransitionStatus.Starting ? StartingStyle : null;
            string? style = own is null ? consumerStyle : consumerStyle is null ? own : own + "; " + consumerStyle;
            if (style is null)
            {
                attributes.Remove(MergeProps.StyleAttribute);
                return;
            }

            attributes.Set(MergeProps.StyleAttribute, style);
            if (transitions!.HandedOver)
            {
                frozenStyle = style;
            }

            return;
        }

        attributes.Set(MergeProps.StyleAttribute, frozenStyle);
    }

    private TransitionsOptions TransitionsOptions() => new()
    {
        Open = open!.Value,
        Mounted = mounted!.Value,
        Status = status!.Value,
        OpenSelector = OpenSelector,
        UnmountRequests = unmountRequests!.Value,
        RetainExit = retainExit!.Value,
        DisableTransitionWhileStarting = true,
        Style = consumerStyle ?? string.Empty,
    };

    // The Positioner's mirror moved: render only when this part's state record changed.
    private void OnPositionerChanged()
    {
        NavigationMenuPopupState? previous = state;
        if (previous is null || previous != State)
        {
            StateHasChanged();
        }
    }
}

// NAV-STA-16, §3.9 state object (navigation-menu/backdrop/NavigationMenuBackdrop.tsx:23-26). TransitionStatus is the
// Root's, the `transitions` module's on this element (ADR 04 §2): mirrored.
/// <summary>Represents the state of a navigation menu backdrop, which the part exposes as data attributes and passes to its class and style functions.</summary>
/// <param name="Open">Whether the menu is open; projected as <c>data-open</c> or <c>data-closed</c>.</param>
/// <param name="TransitionStatus">The popup's transition status, rendered as <c>data-starting-style</c> and <c>data-ending-style</c> by the browser-side behavior.</param>
[PartState]
public sealed partial record NavigationMenuBackdropState(bool Open, [property: MirroredField] TransitionStatus? TransitionStatus);

// navigation-menu/backdrop/NavigationMenuBackdropDataAttributes.ts (NAV-DOM-23).
/// <summary>Provides the names of the data attributes a navigation menu backdrop renders.</summary>
public static class NavigationMenuBackdropDataAttributes
{
    /// <summary>Represents the attribute present while the menu is open: <c>data-open</c>.</summary>
    public const string Open = PopupDataAttributes.Open;

    /// <summary>Represents the attribute present while the menu is closed: <c>data-closed</c>.</summary>
    public const string Closed = PopupDataAttributes.Closed;

    /// <summary>Represents the attribute present while the backdrop is animating in: <c>data-starting-style</c>.</summary>
    public const string StartingStyle = PopupDataAttributes.StartingStyle;

    /// <summary>Represents the attribute present while the backdrop is animating out: <c>data-ending-style</c>.</summary>
    public const string EndingStyle = PopupDataAttributes.EndingStyle;
}

// Base UI's NavigationMenu.Backdrop (navigation-menu/backdrop/NavigationMenuBackdrop.tsx); the contract is
// docs/components/navigation-menu.md §3.9, §5.9: role="presentation", `hidden` while unmounted and `user-select: none`
// before the consumer's style (NAV-DOM-24), the Root's open and status (NAV-DOM-23) through its own transitions handle.
/// <summary>
/// Represents an overlay behind the navigation menu's popup: Base UI's <c>NavigationMenu.Backdrop</c>, rendered as a
/// <c>div</c>.
/// </summary>
public sealed class NavigationMenuBackdrop : PartBase<NavigationMenuBackdropState>
{
    private const string OpenSelector = "[data-open]";
    private const string BaseStyle = "user-select: none; -webkit-user-select: none";

    private static readonly StateAttributesMapping<NavigationMenuBackdropState> s_mapping = PopupStateMapping.CreateOpen<NavigationMenuBackdropState>();

    private NavigationMenuRootContext? root;
    private NavigationMenuTransitions? transitions;
    private StoreSelection<NavigationMenuStoreState, bool>? open;
    private StoreSelection<NavigationMenuStoreState, bool>? mounted;
    private StoreSelection<NavigationMenuStoreState, TransitionStatus?>? status;
    private StoreSelection<NavigationMenuStoreState, int>? unmountRequests;
    private StoreSelection<NavigationMenuStoreState, bool>? retainExit;
    private IDisposable? retainRegistration;
    private NavigationMenuBackdropState? state;
    private bool disposed;

    [Inject]
    private IJSRuntime JSRuntime { get; set; } = null!;

    [CascadingParameter]
    private NavigationMenuRootContext? RootContext { get; set; }

    [CascadingParameter]
    private PopupPortalContext? Portal { get; set; }

    /// <inheritdoc />
    protected override string DefaultTagName => "div";

    /// <inheritdoc />
    protected override NavigationMenuBackdropState State
    {
        get
        {
            bool isOpen = open!.Value;
            TransitionStatus? current = status!.Value;
            if (state is null || state.Open != isOpen || state.TransitionStatus != current)
            {
                state = new NavigationMenuBackdropState(isOpen, current);
            }

            return state;
        }
    }

    /// <inheritdoc />
    protected override StateAttributesMapping<NavigationMenuBackdropState>? StateAttributesMapping => s_mapping;

    /// <inheritdoc />
    protected override bool UsesGeneratedId => RenderFunction is not null;

    /// <summary>Gets a task that completes once the backdrop's handle has attached.</summary>
    internal Task TwinAttached => transitions!.Attached;

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        root = NavigationMenuRootContext.Require(RootContext);
        transitions = new NavigationMenuTransitions(JSRuntime);
        retainRegistration = root.Store.OnUnmountPreventionChanging(retain => transitions.AnnounceRetainExit(Element, retain));
        base.OnInitialized();
    }

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (Element is not { } element)
        {
            return;
        }

        await PopupPortalContext.WhenMoved(Portal);
        if (!disposed)
        {
            await transitions!.SyncAsync(element, TransitionsOptions);
        }
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing && !disposed)
        {
            disposed = true;
            retainRegistration?.Dispose();
            if (transitions is not null)
            {
                _ = transitions.DisposeAsync().AsTask();
            }
        }

        base.Dispose(disposing);
    }

    /// <inheritdoc />
    protected override void SubscribeSelectors()
    {
        SelectorSubscription<NavigationMenuStoreState> subscription = SubscribeTo(RootContext!.Store);
        open = subscription.Select(NavigationMenuSelectors.Open);
        mounted = subscription.Select(NavigationMenuSelectors.Mounted);
        status = subscription.Select(NavigationMenuSelectors.TransitionStatus);
        unmountRequests = subscription.Select(NavigationMenuSelectors.UnmountRequests);
        retainExit = subscription.Select(NavigationMenuSelectors.PreventUnmountingOnClose);
    }

    /// <inheritdoc />
    protected override void AddPartAttributes(AttributeList attributes)
    {
        attributes.Set("role", "presentation");
        if (!mounted!.Value)
        {
            attributes.Set("hidden", true);
        }

        attributes.Set(MergeProps.StyleAttribute, BaseStyle);
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
}

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

// NAV-STA-16, §3.4 state object (navigation-menu/trigger/NavigationMenuTrigger.tsx:642-645, 787-796).
/// <summary>Represents the state of a navigation menu trigger, which the part exposes as data attributes and passes to its class and style functions.</summary>
/// <param name="Open">Whether the trigger's item is the open one; projected as <c>data-popup-open</c> and <c>data-pressed</c>.</param>
/// <param name="Disabled">Whether the trigger ignores user interaction; projected as <c>data-disabled</c>.</param>
[PartState]
public sealed partial record NavigationMenuTriggerState(bool Open, bool Disabled);

// navigation-menu/trigger/NavigationMenuTriggerDataAttributes.ts (NAV-DOM-07, 08).
/// <summary>Provides the names of the data attributes a navigation menu trigger renders.</summary>
public static class NavigationMenuTriggerDataAttributes
{
    /// <summary>Represents the attribute present while the trigger's item is open: <c>data-popup-open</c>.</summary>
    public const string PopupOpen = PopupTriggerDataAttributes.PopupOpen;

    /// <summary>Represents the attribute present while the trigger's item is open: <c>data-pressed</c>.</summary>
    public const string Pressed = PopupTriggerDataAttributes.Pressed;

    /// <summary>Represents the attribute present while the trigger is disabled: <c>data-disabled</c>.</summary>
    public const string Disabled = "data-disabled";
}

// navigation-menu/utils/constants.ts:3 (NAV-DOM-09): the marker every Trigger renders, which the outside-press dismissal
// ignores (NAV-PTR-17). Base UI does not export it.
internal static class NavigationMenuMarkers
{
    public const string Trigger = "data-base-ui-navigation-menu-trigger";
}

// Base UI's NavigationMenu.Trigger (navigation-menu/trigger/NavigationMenuTrigger.tsx); the contract is
// docs/components/navigation-menu.md §3.4, §5.4, §6.
// - A button with focusableWhenDisabled (UBT §6.2's row; NAV-ARIA-03), a composite item of the enclosing List or Content
//   registered with the consumer form of `tabindex="0"`, so the roving tabindex never writes it (NAV-DOM-06; ADR 04
//   Amendments 2026-10-22's exception; the ToolbarButton precedent).
// - The interactions trigger role on the Root's session (design point 2, settled by the orchestrator 2026-11-04): click
//   with the patient-click window and the active Item's toggle while a Positioner is registered or the menu is closed;
//   hover (rest = the Root's delay, or 0 while mounted with a Positioner, NAV-PTR-02 and §16 Q12 replicated; the close
//   delay; the safe polygon) while a floating element is registered or the menu is closed, with track 2's NavigationMenu
//   options (the lock's scope, the trigger's own lock, the inline close context; ADR 04 Amendments 2026-11-03 (7)).
//   Disabled: neither role (NAV-EDGE-06).
// - The `navigation-menu` module's trigger handle: the size pass, the activation report, the open key, the guards' routing
//   and the blur check (ADR 04 Amendments 2026-11-03 (4); design points 3-5, settled by the orchestrator 2026-11-04).
// - C# renders aria-expanded, aria-controls while active (§16 Q3 replicated), the marker, and while active the guard, the
//   aria-owns owner and the guard after the element (NAV-DOM-10, NAV-ARIA-06); its onfocus clears viewportInert while
//   active (NAV-STA-09; ADR 16 rule 6, a state result).
/// <summary>
/// Represents the trigger of a navigation menu item, which opens its content on hover or press: Base UI's
/// <c>NavigationMenu.Trigger</c>, rendered as a <c>button</c>.
/// </summary>
public sealed class NavigationMenuTrigger : PartBase<NavigationMenuTriggerState>
{
    private const string ArrowKeyPrefix = "Arrow";
    private const string ListScope = "ul";

    private static readonly StateAttributesMapping<NavigationMenuTriggerState> s_mapping = PopupStateMapping.CreatePressableTrigger<NavigationMenuTriggerState>();

    private readonly EventCallback onKeyDown;
    private readonly EventCallback onKeyUp;
    private readonly EventCallback onClick;
    private readonly EventCallback onFocus;
    private readonly Action itemChanged;
    private NavigationMenuItemContext? item;
    private NavigationMenuRootContext? root;
    private ButtonBehavior? button;
    private CompositeItemBehavior? composite;
    private InteractionsTriggerBehavior? interactions;
    private NavigationMenuModule? module;
    private StoreSelection<NavigationMenuStoreState, bool>? open;
    private StoreSelection<NavigationMenuStoreState, bool>? mounted;
    private StoreSelection<NavigationMenuStoreState, bool>? hasPositioner;
    private StoreSelection<NavigationMenuStoreState, string?>? popupId;
    private StoreSelection<NavigationMenuStoreState, string?>? viewportId;
    private NavigationMenuTriggerState? state;
    private Dictionary<string, object>? registrationAttributes;
    private IReadOnlyDictionary<string, object>? registrationSource;

    /// <summary>Initializes a new instance of the <see cref="NavigationMenuTrigger"/> class.</summary>
    public NavigationMenuTrigger()
    {
        onKeyDown = CreateHandler<KeyboardEventArgs>("onkeydown", HandleKeyDownAsync);
        onKeyUp = CreateHandler<KeyboardEventArgs>("onkeyup", e => button!.HandleKeyUpAsync(e));
        onClick = CreateHandler<MouseEventArgs>("onclick", e => interactions!.HandleClickAsync(e));
        onFocus = CreateHandler<FocusEventArgs>("onfocus", OnFocus);
        itemChanged = OnItemChanged;
    }

    [Inject]
    private IJSRuntime JSRuntime { get; set; } = null!;

    [Inject]
    private ILogger<NavigationMenuTrigger> Logger { get; set; } = null!;

    [Inject]
    private ButtonDevelopmentLog DevelopmentLog { get; set; } = null!;

    [Inject]
    private PartIdGenerator IdGenerator { get; set; } = null!;

    // NAV-STA-11: `open && value === itemValue`.
    private bool IsActive => item!.IsActive && open!.Value;

    [CascadingParameter]
    private NavigationMenuRootContext? RootContext { get; set; }

    [CascadingParameter]
    private NavigationMenuItemContext? ItemContext { get; set; }

    [CascadingParameter]
    private CompositeRootContext? CompositeContext { get; set; }

    // NAV-API-09, UBT-API-03
    /// <summary>Gets or sets a value indicating whether the part renders a native <c>button</c>.</summary>
    /// <value>
    /// <see langword="true"/>, the default; set it to <see langword="false"/> when <c>Render</c> substitutes an element that
    /// is not a button.
    /// </value>
    [Parameter]
    public bool NativeButton { get; set; } = true;

    // NAV-API-10, NAV-EDGE-06
    /// <summary>Gets or sets a value indicating whether the trigger ignores user interaction.</summary>
    /// <value><see langword="false"/> by default; a disabled trigger stays focusable.</value>
    [Parameter]
    public bool Disabled { get; set; }

    /// <inheritdoc />
    protected override string DefaultTagName => "button";

    // The pipeline reads the state before the part's attributes, so the record follows the Item here.
    /// <inheritdoc />
    protected override NavigationMenuTriggerState State
    {
        get
        {
            bool active = IsActive;
            if (state is null || state.Open != active || state.Disabled != Disabled)
            {
                state = new NavigationMenuTriggerState(active, Disabled);
            }

            return state;
        }
    }

    /// <inheritdoc />
    protected override StateAttributesMapping<NavigationMenuTriggerState>? StateAttributesMapping => s_mapping;

    /// <inheritdoc />
    protected override bool UsesGeneratedId => true;

    /// <summary>Gets a task that completes once the trigger's handles have attached.</summary>
    internal Task TwinsAttached => Task.WhenAll(button!.TwinAttached, interactions!.TwinAttached, module!.Attached);

    /// <summary>Gets the trigger's <c>navigation-menu</c> twin, whose reports the tests play.</summary>
    internal NavigationMenuTwin ModuleTwin => module!.Twin;

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        root = NavigationMenuRootContext.Require(RootContext);
        item = NavigationMenuItemContext.Require(ItemContext);
        button = new ButtonBehavior(JSRuntime, Logger, DevelopmentLog, IdGenerator, StateHasChanged, DispatchExceptionAsync, onKeyDown, onKeyUp)
        {
            FocusableWhenDisabled = true,
        };
        composite = new CompositeItemBehavior(CompositeContext, "NavigationMenuTrigger", onKeyDown);
        interactions = new InteractionsTriggerBehavior(JSRuntime, request => root.RouteAsync(request), DispatchExceptionAsync, onClick);
        module = new NavigationMenuModule(JSRuntime);
        module.Twin.Activated += activation => _ = InvokeAsync(() => item.ActivateAsync(activation));
        module.Twin.FocusedOut += focusOut => _ = InvokeAsync(() => root.FocusOutAsync(focusOut.Event));
        module.Twin.ViewportInertChanged += inert => _ = InvokeAsync(() => root.Store.SetViewportInert(inert));
        item.AddListener(itemChanged);
        base.OnInitialized();
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        item!.SetTriggerId(Id);
        button!.Native = NativeButton;
        button.Disabled = Disabled;
        button.SetConsumerAttributes(AdditionalAttributes);
        interactions!.SetConsumerAttributes(AdditionalAttributes);

        // NAV-DOM-06: the consumer form of the registration, `tabindex="0"`, so the module never writes this item's tabindex.
        composite!.SetRegistration(Id, RegistrationAttributes(), Disabled, focusableWhenDisabled: true);
        composite.SetConsumerAttributes(Disabled ? null : AdditionalAttributes);
        SyncInteractions();
    }

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        SyncInteractions();
        Task interactionsSync = interactions!.AfterRenderAsync(Enabled, Element, Id);
        Task moduleSync = Element is { } element
            ? module!.SyncAsync(element, new NavigationMenuOptions { Part = NavigationMenuTwin.TriggerPart, HasContent = item!.HasContent ? null : false }, root!.RootElement)
            : Task.CompletedTask;
        await button!.AfterRenderAsync(Enabled, Element, Id, composite: true);
        await composite!.AfterRenderAsync(Element);
        await interactionsSync;
        await moduleSync;
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            item?.RemoveListener(itemChanged);
            item?.SetTriggerId(null);
            button?.Dispose();
            composite?.Dispose();
            if (interactions is not null)
            {
                _ = interactions.DisposeAsync().AsTask();
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
        hasPositioner = subscription.Select(NavigationMenuSelectors.HasPositioner);
        popupId = subscription.Select(NavigationMenuSelectors.PopupId);
        viewportId = subscription.Select(NavigationMenuSelectors.ViewportId);
    }

    /// <inheritdoc />
    protected override void AddPartAttributes(AttributeList attributes)
    {
        bool active = IsActive;
        attributes.Set(NavigationMenuMarkers.Trigger, string.Empty);
        attributes.Set("aria-expanded", active ? "true" : "false");
        if (active && popupId!.Value is { } controls)
        {
            attributes.Set("aria-controls", controls);
        }

        attributes.Set("onclick", onClick);
        attributes.Set("onfocus", onFocus);
        button!.Disabled = Disabled;
        button.AddAttributes(attributes, Id, composite: true);
        interactions!.AddAttributes(attributes);
        composite!.AddAttributes(attributes);

        // NAV-DOM-06: every Trigger is a sequential tab stop.
        attributes.Set("tabindex", "0");
    }

    /// <inheritdoc />
    protected override bool ForwardsConsumerHandler(string eventName) =>
        button!.ForwardsConsumerHandler(eventName) && composite!.ForwardsConsumerHandler(eventName) && interactions!.ForwardsConsumerHandler(eventName);

    // NAV-DOM-10, NAV-ARIA-06, PSH-STR-08: the guard, the owner and the guard, only while active.
    /// <inheritdoc />
    protected override void BuildAfterElement(RenderTreeBuilder builder)
    {
        if (!IsActive)
        {
            return;
        }

        RenderGuard(builder);
        builder.OpenElement(10, "span");
        if (viewportId!.Value is { } owns)
        {
            builder.AddAttribute(11, "aria-owns", owns);
        }

        builder.AddAttribute(12, "style", VisuallyHidden.OwnerStyle);
        builder.CloseElement();
        RenderGuard(builder);
    }

    // FocusGuard.tsx: the VoiceOver swap to role="button" is the module's write after attach (ARIA-05). Both guards share one
    // region's sequence numbers.
    private static void RenderGuard(RenderTreeBuilder builder)
    {
        builder.OpenRegion(0);
        builder.OpenElement(0, "span");
        builder.AddAttribute(1, "tabindex", "0");
        builder.AddAttribute(2, PopupMarkerAttributes.FocusGuard, string.Empty);
        builder.AddAttribute(3, "aria-hidden", "true");
        builder.AddAttribute(4, "style", VisuallyHidden.Style);
        builder.CloseElement();
        builder.CloseRegion();
    }

    private static bool IsItemKey(string? key) => key is not null && key.StartsWith(ArrowKeyPrefix, StringComparison.Ordinal);

    // The consumer's attributes with `tabindex="0"`, the registration's consumer form, unless the consumer gave one.
    private IReadOnlyDictionary<string, object>? RegistrationAttributes()
    {
        if (AdditionalAttributes is not null && AdditionalAttributes.ContainsKey("tabindex"))
        {
            return AdditionalAttributes;
        }

        if (registrationAttributes is null || !ReferenceEquals(registrationSource, AdditionalAttributes))
        {
            registrationSource = AdditionalAttributes;
            registrationAttributes = AdditionalAttributes is null ? new(StringComparer.Ordinal) : new(AdditionalAttributes, StringComparer.Ordinal);
            registrationAttributes["tabindex"] = "0";
        }

        return registrationAttributes;
    }

    // The arrows reach the composite's gate, whose decision point may have run the consumer's handler; the rest the button's.
    private Task HandleKeyDownAsync(KeyboardEventArgs e) => IsItemKey(e.Key) ? composite!.HandleKeyDownAsync(e) : button!.HandleKeyDownAsync(e);

    // NAV-STA-09; NavigationMenuTrigger.tsx:665-670.
    private void OnFocus(FocusEventArgs e)
    {
        if (IsActive)
        {
            root!.Store.SetViewportInert(false);
        }
    }

    private void OnItemChanged() => StateHasChanged();

    // NavigationMenuTrigger.tsx:126-130, 486, 511-541; NAV-PTR-01..20.
    private void SyncInteractions()
    {
        NavigationMenuRootContext menu = root!;
        interactions!.SessionToken = menu.SessionToken;
        bool closed = !open!.Value;
        bool positioner = hasPositioner!.Value;
        bool floating = positioner || viewportId!.Value is not null;
        bool active = IsActive;
        InteractionsClickOptions? click = (positioner || closed) && !Disabled
            ? new InteractionsClickOptions { Toggle = active, PatientClick = true }
            : null;
        InteractionsHoverOptions? hover = (floating || closed) && !Disabled
            ? new InteractionsHoverOptions
            {
                RestMs = mounted!.Value && positioner ? 0 : menu.DelayMs,
                DelayClose = menu.CloseDelayMs,
                Move = false,
                MouseOnly = true,
                SafePolygon = true,
                BlockPointerEvents = true,
                PointerEventsScopeSelector = menu.Nested && positioner ? null : ListScope,
                BlockPointerEventsOnEnter = !menu.Nested || !positioner,
                InlineCloseContext = menu.Nested && !positioner && viewportId!.Value is { } viewport ? new InteractionsInlineCloseContext(viewport, menu.SessionToken) : null,
            }
            : null;
        InteractionsTriggerOptions current = interactions.Options;
        if (!string.Equals(current.TriggerId, Id, StringComparison.Ordinal) || !Equals(current.Click, click) || !Equals(current.Hover, hover))
        {
            interactions.Options = new InteractionsTriggerOptions { TriggerId = Id, Click = click, Hover = hover };
        }
    }
}

// NAV-STA-16, §3.5 state object (navigation-menu/icon/NavigationMenuIcon.tsx:20-24).
/// <summary>Represents the state of a navigation menu icon, which the part exposes as data attributes and passes to its class and style functions.</summary>
/// <param name="Open">Whether the icon's item is the open one; projected as <c>data-popup-open</c>.</param>
[PartState]
public sealed partial record NavigationMenuIconState(bool Open);

// navigation-menu/icon/NavigationMenuIconDataAttributes.ts (NAV-DOM-13).
/// <summary>Provides the names of the data attributes a navigation menu icon renders.</summary>
public static class NavigationMenuIconDataAttributes
{
    /// <summary>Represents the attribute present while the icon's item is open: <c>data-popup-open</c>.</summary>
    public const string PopupOpen = PopupTriggerDataAttributes.PopupOpen;
}

// Base UI's NavigationMenu.Icon (navigation-menu/icon/NavigationMenuIcon.tsx); the contract is
// docs/components/navigation-menu.md §3.5, §5.5: a `span aria-hidden="true"` whose default child is ▼ (NAV-DOM-12), with the
// trigger mapping, never data-pressed (NAV-DOM-13).
/// <summary>
/// Represents an icon that shows whether its item is open: Base UI's <c>NavigationMenu.Icon</c>, rendered as a <c>span</c>
/// hidden from assistive technology.
/// </summary>
public sealed class NavigationMenuIcon : PartBase<NavigationMenuIconState>
{
    private static readonly StateAttributesMapping<NavigationMenuIconState> s_mapping = PopupStateMapping.CreateTrigger<NavigationMenuIconState>();
    private static readonly RenderFragment s_defaultContent = static builder => builder.AddContent(0, "▼");

    private readonly Action itemChanged;
    private NavigationMenuItemContext? item;
    private StoreSelection<NavigationMenuStoreState, bool>? open;
    private NavigationMenuIconState? state;

    /// <summary>Initializes a new instance of the <see cref="NavigationMenuIcon"/> class.</summary>
    public NavigationMenuIcon()
    {
        itemChanged = () => StateHasChanged();
    }

    [CascadingParameter]
    private NavigationMenuRootContext? RootContext { get; set; }

    [CascadingParameter]
    private NavigationMenuItemContext? ItemContext { get; set; }

    /// <inheritdoc />
    protected override string DefaultTagName => "span";

    /// <inheritdoc />
    protected override NavigationMenuIconState State
    {
        get
        {
            bool active = item!.IsActive && open!.Value;
            if (state is null || state.Open != active)
            {
                state = new NavigationMenuIconState(active);
            }

            return state;
        }
    }

    /// <inheritdoc />
    protected override StateAttributesMapping<NavigationMenuIconState>? StateAttributesMapping => s_mapping;

    /// <inheritdoc />
    public override Task SetParametersAsync(ParameterView parameters)
    {
        ChildContent = null;
        return base.SetParametersAsync(parameters);
    }

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        NavigationMenuRootContext.Require(RootContext);
        item = NavigationMenuItemContext.Require(ItemContext);
        item.AddListener(itemChanged);
        base.OnInitialized();
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        ChildContent ??= s_defaultContent;
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            item?.RemoveListener(itemChanged);
        }

        base.Dispose(disposing);
    }

    /// <inheritdoc />
    protected override void SubscribeSelectors()
    {
        open = SubscribeTo(RootContext!.Store).Select(NavigationMenuSelectors.Open);
    }

    /// <inheritdoc />
    protected override void AddPartAttributes(AttributeList attributes) => attributes.Set("aria-hidden", "true");
}

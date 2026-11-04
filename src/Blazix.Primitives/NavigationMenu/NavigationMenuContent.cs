using Blazix.Primitives.Interop;
using Blazix.Primitives.Rendering;
using Blazix.Primitives.Store;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace Blazix.Primitives;

// NAV-STA-16, §3.6 state object (navigation-menu/content/NavigationMenuContent.tsx:97-101, 182-195). TransitionStatus is the
// Content's own, the `transitions` module's (ADR 04 §2): mirrored. ActivationDirection is C#'s (design point 4, sent
// 2026-11-04, the user's).
/// <summary>Represents the state of a navigation menu content, which the part exposes as data attributes and passes to its class and style functions.</summary>
/// <param name="Open">Whether the content's item is open while the popup is mounted; projected as <c>data-open</c> or <c>data-closed</c>.</param>
/// <param name="TransitionStatus">
/// The content's own transition status, rendered as <c>data-starting-style</c> and <c>data-ending-style</c> by the
/// browser-side behavior.
/// </param>
/// <param name="ActivationDirection">
/// The direction from the previous trigger to the active one, <c>left</c>, <c>right</c>, <c>up</c> or <c>down</c>; projected
/// as <c>data-activation-direction</c>, and <see langword="null"/> while the menu is closed.
/// </param>
[PartState]
public sealed partial record NavigationMenuContentState(bool Open, [property: MirroredField] TransitionStatus? TransitionStatus, string? ActivationDirection);

// navigation-menu/content/NavigationMenuContentDataAttributes.ts (NAV-DOM-14, 15, 18).
/// <summary>Provides the names of the data attributes a navigation menu content renders.</summary>
public static class NavigationMenuContentDataAttributes
{
    /// <summary>Represents the attribute present while the content is open: <c>data-open</c>.</summary>
    public const string Open = PopupDataAttributes.Open;

    /// <summary>Represents the attribute present while the content is closed: <c>data-closed</c>.</summary>
    public const string Closed = PopupDataAttributes.Closed;

    /// <summary>Represents the attribute present while the content is animating in: <c>data-starting-style</c>.</summary>
    public const string StartingStyle = PopupDataAttributes.StartingStyle;

    /// <summary>Represents the attribute present while the content is animating out: <c>data-ending-style</c>.</summary>
    public const string EndingStyle = PopupDataAttributes.EndingStyle;

    /// <summary>Represents the attribute whose value is the activation direction: <c>data-activation-direction</c>.</summary>
    public const string ActivationDirection = PopupViewportDataAttributes.ActivationDirection;
}

// Base UI's NavigationMenu.Content (navigation-menu/content/NavigationMenuContent.tsx); the contract is
// docs/components/navigation-menu.md §3.6, §5.6, §9.6.
// - The Content move (ADR 04 Amendments 2026-11-03 (4); the probe of 2026-11-04 held at L3): a lite `portal` handle per
//   Content with the Viewport target as its container, through PopupPortalBehavior and the pipeline's host wrapper, so
//   Blazor removes the wrapper in place and never meets the moved node. The target is sent only while the Root's registry
//   holds it (a reference to a removed target revives as null, which the module would read as the body).
// - NAV-ANI-16: with KeepMounted and no target yet, rendered inline in the Item with `hidden`; once a target exists, latched
//   in the Viewport, rendering nothing while no target exists. Without KeepMounted, rendered only while its own mounted
//   flag holds and a target exists (NAV-ANI-01).
// - Its own transitions handle (NAV-STA-12, NAV-ANI-18): open = the popup mounted and its Item active; mounted until its
//   exit finishes, forced off when the popup unmounts (NAV-ANI-20).
// - A composite root (orientation both, loopFocus true, Home and End off; NAV-KEY-09; CMP-STR-05: its element is the root's).
// - Exiting: `position: absolute; top: 0; left: 0` before the consumer's style (NAV-DOM-17), the style the portal module
//   owns from the move; the exiting `inert` unless focus is inside is the `navigation-menu` module's (NAV-FOC-09).
// - It cascades the family-agnostic node with its Root's token (ADR 04 Amendments 2026-11-03 (5); NAV-STA-04), so a Root,
//   a Dialog or a Popover inside it is the Root's child in the floating tree.
/// <summary>
/// Represents the content of a navigation menu item, which moves into the viewport while the item is open: Base UI's
/// <c>NavigationMenu.Content</c>, rendered as a <c>div</c>.
/// </summary>
public sealed class NavigationMenuContent : PartBase<NavigationMenuContentState>
{
    private const string OpenSelector = "[data-open]";
    private const string ExitingStyle = "position: absolute; top: 0; left: 0";

    private static readonly StateDataAttribute[] s_left = [new(PopupViewportDataAttributes.ActivationDirection, "left")];
    private static readonly StateDataAttribute[] s_right = [new(PopupViewportDataAttributes.ActivationDirection, "right")];
    private static readonly StateDataAttribute[] s_up = [new(PopupViewportDataAttributes.ActivationDirection, "up")];
    private static readonly StateDataAttribute[] s_down = [new(PopupViewportDataAttributes.ActivationDirection, "down")];
    private static readonly StateAttributesMapping<NavigationMenuContentState> s_mapping =
        PopupStateMapping.CreateOpen<NavigationMenuContentState>().Map<string?>(nameof(NavigationMenuContentState.ActivationDirection), ProjectDirection);

    private readonly EventCallback onKeyDown;
    private readonly Action itemChanged;
    private readonly Action targetChanged;
    private readonly RenderFragment cascadedContent;
    private NavigationMenuRootContext? root;
    private NavigationMenuItemContext? item;
    private CompositeRootBehavior? composite;
    private PopupPortalBehavior? portal;
    private NavigationMenuTransitions? transitions;
    private NavigationMenuModule? module;
    private FloatingNodeContext? node;
    private NavigationMenuContentScope? scope;
    private StoreSelection<NavigationMenuStoreState, bool>? popupMounted;
    private StoreSelection<NavigationMenuStoreState, string?>? direction;
    private StoreSelection<NavigationMenuStoreState, bool>? hasTarget;
    private NavigationMenuContentState? state;
    private RenderFragment? consumerContent;
    private TransitionStatus? status;
    private bool mounted;
    private bool latched;
    private bool inPortal;
    private bool disposed;

    /// <summary>Initializes a new instance of the <see cref="NavigationMenuContent"/> class.</summary>
    public NavigationMenuContent()
    {
        onKeyDown = CreateHandler<KeyboardEventArgs>("onkeydown", e => composite!.HandleKeyDownAsync(e));
        itemChanged = OnItemChanged;
        targetChanged = () => StateHasChanged();
        cascadedContent = RenderCascade;
    }

    [Inject]
    private IJSRuntime JSRuntime { get; set; } = null!;

    [Inject]
    private PartIdGenerator IdGenerator { get; set; } = null!;

    // NAV-STA-11: `popupMounted && value === itemValue`.
    private bool IsOpen => popupMounted!.Value && item!.IsActive;

    [CascadingParameter]
    private NavigationMenuRootContext? RootContext { get; set; }

    [CascadingParameter]
    private NavigationMenuItemContext? ItemContext { get; set; }

    [CascadingParameter]
    private DirectionProviderContext? DirectionContext { get; set; }

    // NAV-API-12, NAV-ANI-16
    /// <summary>Gets or sets a value indicating whether the content stays in the page while its item is closed.</summary>
    /// <value><see langword="false"/> by default; a kept content is present, hidden, before the menu first opens.</value>
    [Parameter]
    public bool KeepMounted { get; set; }

    /// <inheritdoc />
    protected override string DefaultTagName => "div";

    /// <inheritdoc />
    protected override NavigationMenuContentState State
    {
        get
        {
            bool isOpen = IsOpen;
            string? activation = direction!.Value;
            if (state is null || state.Open != isOpen || state.TransitionStatus != status || !string.Equals(state.ActivationDirection, activation, StringComparison.Ordinal))
            {
                state = new NavigationMenuContentState(isOpen, status, activation);
            }

            return state;
        }
    }

    /// <inheritdoc />
    protected override StateAttributesMapping<NavigationMenuContentState>? StateAttributesMapping => s_mapping;

    /// <inheritdoc />
    protected override bool UsesGeneratedId => true;

    /// <inheritdoc />
    protected override string? HostWrapperTagName => "div";

    /// <summary>Gets a task that completes once the content's handles have attached.</summary>
    internal Task TwinsAttached => Task.WhenAll(composite!.TwinAttached, transitions!.Attached, module!.Attached);

    /// <inheritdoc />
    public override Task SetParametersAsync(ParameterView parameters)
    {
        // The part replaces ChildContent with its own cascade, so a parameter set without content must not keep it.
        ChildContent = null;
        return base.SetParametersAsync(parameters);
    }

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        root = NavigationMenuRootContext.Require(RootContext);
        item = NavigationMenuItemContext.Require(ItemContext);
        item.SetHasContent(true);
        composite = new CompositeRootBehavior(JSRuntime, IdGenerator, StateHasChanged, DispatchExceptionAsync, onKeyDown)
        {
            LoopFocus = true,
            EnableHomeAndEndKeys = false,
        };
        portal = new PopupPortalBehavior(JSRuntime, StateHasChanged) { Lite = true };
        transitions = new NavigationMenuTransitions(JSRuntime);
        transitions.Twin.StatusChanged += change => _ = InvokeAsync(() => MirrorStatus(change.Status));
        transitions.Twin.TransitionCompleted += opened => _ = InvokeAsync(() => CompleteTransition(opened));
        module = new NavigationMenuModule(JSRuntime);
        node = new FloatingNodeContext(root.SessionToken);
        scope = new NavigationMenuContentScope(root);
        item.AddListener(itemChanged);
        root.AddTargetListener(targetChanged);
        base.OnInitialized();
        mounted = IsOpen;
        status = mounted ? TransitionStatus.Starting : null;
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        composite!.SetDirectionContext(DirectionContext);
        composite.SetConsumerAttributes(AdditionalAttributes);
        consumerContent = ChildContent;
        ChildContent = composite.Context.Cascade(cascadedContent);
    }

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        // NAV-ANI-20: a Content whose exit the popup's unmount cut short re-enters through `starting`.
        if (mounted && !popupMounted!.Value)
        {
            mounted = false;
            status = null;
        }

        // NAV-STA-12: an open Content mounts and starts, whichever of the Item and the popup changed last.
        if (IsOpen && !mounted)
        {
            mounted = true;
            status = TransitionStatus.Starting;
        }

        bool hasContainer = hasTarget!.Value && root!.ViewportTarget is not null;
        if (KeepMounted && hasContainer)
        {
            latched = true;
        }

        bool inline = KeepMounted && !hasContainer && !latched;
        inPortal = !inline && hasContainer && (mounted || KeepMounted);
        Enabled = inline || inPortal;
        base.BuildRenderTree(builder);
    }

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (disposed)
        {
            return;
        }

        // The probe's rule: the target crosses only while the registry holds it; otherwise the handle lets the node go.
        ElementReference? target = root!.ViewportTarget;
        bool move = inPortal && Element is not null && target is not null;
        await portal!.OnAfterRenderAsync(move ? Element : null, move ? (PortalContainerValue?)new PortalContainerValue(target!.Value) : null);
        if (Element is not { } element || !Enabled)
        {
            return;
        }

        Task transitionsSync = transitions!.SyncAsync(element, TransitionsOptions);
        Task moduleSync = module!.SyncAsync(element, new NavigationMenuOptions { Part = NavigationMenuTwin.ContentPart }, root.RootElement);
        await composite!.AfterRenderAsync(Enabled, element, Id);
        await transitionsSync;
        await moduleSync;
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing && !disposed)
        {
            disposed = true;
            item?.RemoveListener(itemChanged);
            item?.SetHasContent(false);
            root?.RemoveTargetListener(targetChanged);
            composite?.Dispose();
            if (portal is not null)
            {
                _ = portal.DisposeAsync().AsTask();
            }

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
        popupMounted = subscription.Select(NavigationMenuSelectors.Mounted);
        direction = subscription.Select(NavigationMenuSelectors.ActivationDirection);
        hasTarget = subscription.Select(NavigationMenuSelectors.HasViewportTarget);
    }

    // NAV-DOM-16, NAV-DOM-17: `hidden` while a kept content is not mounted (or inline before a target exists); the exiting
    // style while closed and mounted.
    /// <inheritdoc />
    protected override void AddPartAttributes(AttributeList attributes)
    {
        bool isOpen = IsOpen;
        if (KeepMounted && (!mounted || !inPortal))
        {
            attributes.Set("hidden", true);
        }

        if (!isOpen && mounted)
        {
            attributes.Set(MergeProps.StyleAttribute, ExitingStyle);
        }

        composite!.AddAttributes(attributes, Id);
    }

    /// <inheritdoc />
    protected override void FinishAttributes(AttributeList attributes)
    {
        if (inPortal)
        {
            portal!.FinishAttributes(attributes);
        }
    }

    /// <inheritdoc />
    protected override void AddHostWrapperAttributes(AttributeList attributes) => PopupPortalBehavior.AddWrapperAttributes(attributes);

    /// <inheritdoc />
    protected override bool ForwardsConsumerHandler(string eventName) => composite!.ForwardsConsumerHandler(eventName);

    private static StateDataAttribute[]? ProjectDirection(string? value) => value switch
    {
        "left" => s_left,
        "right" => s_right,
        "up" => s_up,
        "down" => s_down,
        _ => null,
    };

    private TransitionsOptions TransitionsOptions() => new()
    {
        Open = IsOpen,
        Mounted = mounted,
        Status = status,
        KeepMounted = KeepMounted,
        OpenSelector = OpenSelector,
    };

    // The cascades the Content renders to its subtree: the family-agnostic node, the content marker, the consumer's content.
    private void RenderCascade(RenderTreeBuilder builder)
    {
        builder.OpenComponent<CascadingValue<IFloatingNodeContext>>(0);
        builder.AddComponentParameter(1, nameof(CascadingValue<IFloatingNodeContext>.Value), node);
        builder.AddComponentParameter(2, nameof(CascadingValue<IFloatingNodeContext>.IsFixed), true);
        builder.AddComponentParameter(3, nameof(CascadingValue<IFloatingNodeContext>.ChildContent), (RenderFragment)(inner =>
        {
            inner.OpenComponent<CascadingValue<NavigationMenuContentScope>>(0);
            inner.AddComponentParameter(1, nameof(CascadingValue<NavigationMenuContentScope>.Value), scope);
            inner.AddComponentParameter(2, nameof(CascadingValue<NavigationMenuContentScope>.IsFixed), true);
            inner.AddComponentParameter(3, nameof(CascadingValue<NavigationMenuContentScope>.ChildContent), consumerContent);
            inner.CloseComponent();
        }));
        builder.CloseComponent();
    }

    // NAV-STA-12: the Item's active state flips the Content's open.
    private void OnItemChanged() => StateHasChanged();

    private void MirrorStatus(TransitionStatus? next)
    {
        if (status != next)
        {
            status = next;
            StateHasChanged();
        }
    }

    // NAV-ANI-18: the exit finished, so the Content unmounts (or hides, kept).
    private void CompleteTransition(bool opened)
    {
        if (!opened && !IsOpen && mounted)
        {
            mounted = false;
            status = null;
            StateHasChanged();
        }
    }
}

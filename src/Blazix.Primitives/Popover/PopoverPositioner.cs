using Blazix.Primitives.Interop;
using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Blazix.Primitives;

// POP-STA-13, §3.4 state object (popover/positioner/PopoverPositioner.tsx:109-115, 136-157). Side, Align and AnchorHidden
// are the `positioning` module's from attach (ADR 04 §2): mirrored. Instant is C#'s (ADR 04 Amendments 2026-10-27).
/// <summary>Represents the state of a popover positioner, which the part exposes as data attributes and passes to its class and style functions.</summary>
/// <param name="Open">A value indicating whether the popover is open; projected as <c>data-open</c> or <c>data-closed</c>.</param>
/// <param name="Side">The side of the anchor the popover is placed on, after collisions; its <c>data-side</c> is written by the browser-side behavior.</param>
/// <param name="Align">The alignment against the anchor, after collisions; its <c>data-align</c> is written by the browser-side behavior.</param>
/// <param name="AnchorHidden">A value indicating whether the anchor is hidden; its <c>data-anchor-hidden</c> is written by the browser-side behavior.</param>
/// <param name="Instant">Why the next transition is skipped, or <see langword="null"/>; projected as <c>data-instant</c>.</param>
[PartState]
public sealed partial record PopoverPositionerState(
    bool Open,
    [property: MirroredField] Side? Side,
    [property: MirroredField] Align? Align,
    [property: MirroredField] bool AnchorHidden,
    PopoverInstantType? Instant);

// popover/positioner/PopoverPositionerDataAttributes.ts (POP-DOM-15..18).
/// <summary>Provides the names of the data attributes a popover positioner renders.</summary>
public static class PopoverPositionerDataAttributes
{
    /// <summary>Represents the attribute present while the popover is open: <c>data-open</c>.</summary>
    public const string Open = PopupDataAttributes.Open;

    /// <summary>Represents the attribute present while the popover is closed: <c>data-closed</c>.</summary>
    public const string Closed = PopupDataAttributes.Closed;

    /// <summary>Represents the attribute whose value is the side of the anchor: <c>data-side</c>.</summary>
    public const string Side = PopupDataAttributes.Side;

    /// <summary>Represents the attribute whose value is the alignment against the anchor: <c>data-align</c>.</summary>
    public const string Align = PopupDataAttributes.Align;

    /// <summary>Represents the attribute present while the anchor is hidden: <c>data-anchor-hidden</c>.</summary>
    public const string AnchorHidden = PopupDataAttributes.AnchorHidden;

    /// <summary>Represents the attribute whose value says why the next transition is skipped: <c>data-instant</c>.</summary>
    public const string Instant = "data-instant";
}

// popover/positioner/PopoverPositionerCssVars.ts (POP-DOM-20).
/// <summary>Provides the names of the CSS variables a popover positioner sets.</summary>
public static class PopoverPositionerCssVars
{
    /// <summary>Represents the anchor's width: <c>--anchor-width</c>.</summary>
    public const string AnchorWidth = "--anchor-width";

    /// <summary>Represents the anchor's height: <c>--anchor-height</c>.</summary>
    public const string AnchorHeight = "--anchor-height";

    /// <summary>Represents the space between the anchor and the edge of the viewport, horizontally: <c>--available-width</c>.</summary>
    public const string AvailableWidth = "--available-width";

    /// <summary>Represents the space between the anchor and the edge of the viewport, vertically: <c>--available-height</c>.</summary>
    public const string AvailableHeight = "--available-height";

    /// <summary>Represents the point the popover grows from: <c>--transform-origin</c>.</summary>
    public const string TransformOrigin = "--transform-origin";

    /// <summary>Represents the popup's measured width while a viewport is mounted: <c>--positioner-width</c>.</summary>
    public const string PositionerWidth = "--positioner-width";

    /// <summary>Represents the popup's measured height while a viewport is mounted: <c>--positioner-height</c>.</summary>
    public const string PositionerHeight = "--positioner-height";
}

// Base UI's Popover.Positioner (popover/positioner/PopoverPositioner.tsx); the contract is docs/components/popover.md §3.4,
// §5.3, §10.
// - Track 2's PositionerBehavior with Popover's defaults (POP-POS-01/02: bottom, center, padding 5, fallback axis side end,
//   which equal the module's defaults for null inputs); no KeepMounted parameter (POP-EDGE-10): the Portal's is read; the
//   anchor is the active trigger, kept through the exit (POP-POS-03); the handover at attach through FinishAttributes; a
//   portaled first attach after the move. AdaptiveOrigin while a Viewport is mounted (PSH-POS-02).
// - Track 3's floating handle: a tree node (POP-POS-10) and the hover-close of a hover-enabled active trigger (POP-PTR-12,
//   the Popup's useHoverFloatingInteraction, whose floating element is this one).
// - The store's positioner element; a closed popover drops a stored trigger-change instant (useTriggerSwitchTransition).
/// <summary>
/// Represents the element that positions a popover against its trigger, Base UI's <c>Popover.Positioner</c>, rendered as
/// a <c>div</c>.
/// </summary>
public sealed class PopoverPositioner : PartBase<PopoverPositionerState>
{
    private static readonly StateAttributesMapping<PopoverPositionerState> s_mapping = PopupStateMapping.CreateOpen<PopoverPositionerState>();

    private PositionerBehavior? positioner;
    private InteractionsFloatingBehavior? floating;
    private PopupModuleAttachment? module;
    private PopoverSubscription? subscription;
    private PopoverSelection<bool>? open;
    private PopoverSelection<bool>? mounted;
    private PopoverSelection<TransitionStatus?>? status;
    private PopoverSelection<PopoverInstantType?>? instant;
    private PopoverSelection<string?>? activeTriggerId;
    private PopoverSelection<bool>? adaptiveOrigin;
    private PopoverSelection<bool>? openOnHover;
    private PopoverSelection<bool>? disabled;
    private PopoverSelection<int>? closeDelay;
    private PopoverSelection<PopoverModalKind>? modal;
    private PopoverSelection<bool>? hoverOpened;
    private PopoverSelection<InteractionType?>? openMethod;
    private PopoverPositionerState? state;
    private string? switchedFrom;
    private RenderFragment? cascadedContent;
    private RenderFragment? cascade;

    [Inject]
    private IJSRuntime JSRuntime { get; set; } = null!;

    [CascadingParameter]
    private PopoverRootContext? RootContext { get; set; }

    [CascadingParameter]
    private PopoverPortalContext? PortalContext { get; set; }

    [CascadingParameter]
    private PopupPortalContext? Portal { get; set; }

    [CascadingParameter]
    private DirectionProviderContext? DirectionContext { get; set; }

    // POP-POS-03, POP-POS-04
    /// <summary>Gets or sets the element or point the popover is positioned against.</summary>
    /// <value>An element, an element id or a virtual rectangle; <see langword="null"/>, the default, for the active trigger.</value>
    [Parameter]
    public AnchorValue? Anchor { get; set; }

    /// <summary>Gets or sets the CSS position of the positioner.</summary>
    /// <value><see cref="Primitives.PositionMethod.Absolute"/> when <see langword="null"/>, the default.</value>
    [Parameter]
    public PositionMethod? PositionMethod { get; set; }

    /// <summary>Gets or sets the side of the anchor the popover is placed on.</summary>
    /// <value><see cref="Primitives.Side.Bottom"/> when <see langword="null"/>, the default.</value>
    [Parameter]
    public Side? Side { get; set; }

    /// <summary>Gets or sets the distance between the anchor and the popover.</summary>
    /// <value>A number of pixels or an <see cref="Offset"/>; 0 when <see langword="null"/>, the default.</value>
    [Parameter]
    public OffsetValue? SideOffset { get; set; }

    /// <summary>Gets or sets how the popover is aligned against the anchor.</summary>
    /// <value><see cref="Primitives.Align.Center"/> when <see langword="null"/>, the default.</value>
    [Parameter]
    public Align? Align { get; set; }

    /// <summary>Gets or sets the shift of the popover along the alignment axis.</summary>
    /// <value>A number of pixels or an <see cref="Offset"/>; 0 when <see langword="null"/>, the default.</value>
    [Parameter]
    public OffsetValue? AlignOffset { get; set; }

    /// <summary>Gets or sets the area the popover is kept inside when it collides.</summary>
    /// <value>The clipping ancestors when <see langword="null"/>, the default.</value>
    [Parameter]
    public CollisionBoundaryValue? CollisionBoundary { get; set; }

    /// <summary>Gets or sets the space kept between the popover and the collision boundary.</summary>
    /// <value>5 pixels when <see langword="null"/>, the default.</value>
    [Parameter]
    public CollisionPaddingValue? CollisionPadding { get; set; }

    /// <summary>Gets or sets a value indicating whether the popover stays in view when the anchor scrolls out of it.</summary>
    /// <value><see langword="false"/> by default.</value>
    [Parameter]
    public bool Sticky { get; set; }

    /// <summary>Gets or sets the minimum distance between the arrow and the popover's corners.</summary>
    /// <value>5 pixels when <see langword="null"/>, the default.</value>
    [Parameter]
    public double? ArrowPadding { get; set; }

    // POP-POS-05
    /// <summary>Gets or sets a value indicating whether the popover stops following the anchor on scroll, resize and layout shifts.</summary>
    /// <value><see langword="false"/> by default.</value>
    [Parameter]
    public bool DisableAnchorTracking { get; set; }

    /// <summary>Gets or sets how the popover avoids collisions.</summary>
    /// <value>Flip on both axes, with the fallback axis side at the end, when <see langword="null"/>, the default.</value>
    [Parameter]
    public CollisionAvoidance? CollisionAvoidance { get; set; }

    /// <inheritdoc />
    protected override string DefaultTagName => "div";

    // The pipeline reads the state before the part's attributes, so the record follows the store and the mirror here.
    /// <inheritdoc />
    protected override PopoverPositionerState State
    {
        get
        {
            PositionerMirror mirror = positioner!.Mirror;
            bool isOpen = open!.Value;
            PopoverInstantType? instantType = instant!.Value;
            if (state is null || state.Open != isOpen || state.Side != mirror.Side || state.Align != mirror.Align || state.AnchorHidden != mirror.AnchorHidden || state.Instant != instantType)
            {
                state = new PopoverPositionerState(isOpen, mirror.Side, mirror.Align, mirror.AnchorHidden, instantType);
            }

            return state;
        }
    }

    /// <inheritdoc />
    protected override StateAttributesMapping<PopoverPositionerState>? StateAttributesMapping => s_mapping;

    // ADR 02 decision 1: under a substitute callback the twins find the element by id.
    /// <inheritdoc />
    protected override bool UsesGeneratedId => RenderFunction is not null;

    /// <summary>Gets a task that completes when the positioner's behaviors are first attached.</summary>
    internal Task TwinAttached => Task.WhenAll(positioner!.TwinAttached, floating!.TwinAttached, module!.Attached);

    /// <summary>Gets the positioner's <c>popup</c> twin, for the tests.</summary>
    internal PopupTwin PopupTwin => module!.Twin;

    /// <summary>Gets the positioner's behavior, for the tests.</summary>
    internal PositionerBehavior Behavior => positioner!;

    /// <inheritdoc />
    public override Task SetParametersAsync(ParameterView parameters)
    {
        ChildContent = null;
        return base.SetParametersAsync(parameters);
    }

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">The positioner is not inside a <see cref="PopoverRoot{TPayload}"/> and a <see cref="PopoverPortal"/>.</exception>
    protected override void OnInitialized()
    {
        PopoverRootContext root = PopoverRootContext.Require(RootContext);
        PopoverPortalContext.Require(PortalContext);
        positioner = new PositionerBehavior(JSRuntime, OnMirrorChanged, InvokeAsync);
        floating = new InteractionsFloatingBehavior(JSRuntime, request => root.RequestOpenAsync(request.Open, request.Details), DispatchExceptionAsync);
        subscription = root.Subscribe(StateHasChanged);
        open = subscription.Select(PopoverSelectors.Open);
        mounted = subscription.Select(PopoverSelectors.Mounted);
        status = subscription.Select(PopoverSelectors.TransitionStatus);
        instant = subscription.Select(PopoverSelectors.InstantType);
        activeTriggerId = subscription.Select(PopoverSelectors.ActiveTriggerId);
        adaptiveOrigin = subscription.Select(PopoverSelectors.AdaptiveOrigin);
        openOnHover = subscription.Select(PopoverSelectors.OpenOnHover);
        disabled = subscription.Select(PopoverSelectors.Disabled);
        closeDelay = subscription.Select(PopoverSelectors.CloseDelay);
        modal = subscription.Select(PopoverSelectors.Modal);
        hoverOpened = subscription.Select(PopoverSelectors.HoverOpened);
        openMethod = subscription.Select(PopoverSelectors.OpenMethod);
        module = new PopupModuleAttachment(JSRuntime);
        module.Twin.TriggerChangeSettled += () => _ = InvokeAsync(root.CompleteTriggerChange);
        switchedFrom = activeTriggerId.Value;
        base.OnInitialized();
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        positioner!.SetDirectionContext(DirectionContext);
        positioner.SetPortalContext(Portal);
        floating!.PortalContext = Portal;
        floating.SessionToken = RootContext!.SessionToken;
        SetInputs();
        if (cascade is null || !ReferenceEquals(cascadedContent, ChildContent))
        {
            cascadedContent = ChildContent;
            cascade = positioner.Context.Cascade(cascadedContent);
        }

        ChildContent = cascade;
    }

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        PopoverRootContext root = RootContext!;
        root.SetPositionerElement(Enabled ? Element : null);
        bool isOpen = open!.Value;
        string? triggerId = activeTriggerId!.Value;
        if (!isOpen)
        {
            root.ClearTriggerChangeWhenClosed();
        }
        else if (switchedFrom is not null && triggerId is not null && !string.Equals(switchedFrom, triggerId, StringComparison.Ordinal))
        {
            // useTriggerSwitchTransition.ts: the instant type is cleared while the popup moves between triggers.
            root.BeginTriggerChange();
        }

        if (triggerId is not null)
        {
            switchedFrom = triggerId;
        }

        // The three handles start in the same render; the positioning one awaits the portal's move inside its behavior.
        SetFloatingOptions();
        Task floatingSync = floating!.AfterRenderAsync(Enabled, Element, Id);
        Task moduleSync = SyncPopupModuleAsync(isOpen, triggerId);
        await positioner!.AfterRenderAsync(Element, Id);
        await floatingSync;
        await moduleSync;
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            subscription?.Dispose();
            subscription = null;
            positioner?.Dispose();
            if (floating is not null)
            {
                _ = floating.DisposeAsync().AsTask();
            }

            if (module is not null)
            {
                _ = module.DisposeAsync().AsTask();
            }

            if (RootContext is not null && RootContext.State.Mounted)
            {
                RootContext.SetPositionerElement(null);
            }
        }

        base.Dispose(disposing);
    }

    /// <inheritdoc />
    protected override void AddPartAttributes(AttributeList attributes)
    {
        SetInputs();
        positioner!.AddAttributes(attributes);
    }

    /// <inheritdoc />
    protected override void FinishAttributes(AttributeList attributes) => positioner!.FinishAttributes(attributes);

    private void SetInputs()
    {
        positioner!.SetInputs(new PositionerInputs
        {
            Anchor = Anchor,
            PositionMethod = PositionMethod,
            Side = Side,
            SideOffset = SideOffset,
            Align = Align,
            AlignOffset = AlignOffset,
            CollisionBoundary = CollisionBoundary,
            CollisionPadding = CollisionPadding,
            Sticky = Sticky,
            ArrowPadding = ArrowPadding,
            DisableAnchorTracking = DisableAnchorTracking,
            CollisionAvoidance = CollisionAvoidance,
            Open = open!.Value,
            Mounted = mounted!.Value,
            KeepMounted = PortalContext!.KeepMounted,
            TransitionStatus = status!.Value,
            AdaptiveOrigin = adaptiveOrigin!.Value,
            TriggerId = activeTriggerId!.Value,
            SessionToken = RootContext!.SessionToken,
        });
    }

    // POP-PTR-12 (PopoverPopup.tsx:66): the hover-close is on while the active trigger opens on hover and is enabled.
    private void SetFloatingOptions()
    {
        bool hover = openOnHover!.Value && !disabled!.Value;
        InteractionsFloatingOptions current = floating!.Options;
        int? delay = hover ? closeDelay!.Value : null;
        if (current.Node != true || current.Hover?.CloseDelay != delay || (current.Hover is null) == hover)
        {
            floating.Options = new InteractionsFloatingOptions { Node = true, Hover = hover ? new InteractionsFloatingHoverOptions(delay) : null };
        }
    }

    // POP-STR-13, POP-PTR-19 (PopoverPositioner.tsx:98-105): the backdrop and the lock while modal is true and the popover
    // was not opened by hover; a touch open locks only a popover that spans the viewport (the module's width test).
    private async Task SyncPopupModuleAsync(bool isOpen, string? triggerId)
    {
        if (Element is not { } element)
        {
            return;
        }

        await PopupPortalContext.WhenMoved(Portal);
        bool trueModalNonHover = modal!.Value == PopoverModalKind.Modal && !hoverOpened!.Value;
        await module!.SyncAsync(element, new PopupOptions
        {
            Positioner = new PopupPositionerOptions
            {
                Open = isOpen,
                Cutout = mounted!.Value && trueModalNonHover,
                ScrollLock = isOpen && trueModalNonHover,
                TouchOpen = openMethod!.Value == InteractionType.Touch,
                ActiveTriggerId = triggerId,
            },
        });
    }

    // A part renders on a mirror change only when its state record changed (track 2's rule): the Arrow and the Popup listen
    // to the positioner context themselves.
    private void OnMirrorChanged()
    {
        PopoverPositionerState? previous = state;
        if (previous is null || previous != State)
        {
            StateHasChanged();
        }
    }
}

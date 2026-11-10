using Blazix.Primitives.Interop;
using Blazix.Primitives.Rendering;
using Blazix.Primitives.Store;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.JSInterop;

namespace Blazix.Primitives;

// SEL-STA-20 (select/positioner/SelectPositioner.tsx:114, 117-122, 235-252). Side, Align and AnchorHidden are the
// `positioning` module's from attach (ADR 04 §2): mirrored; Side is `none` while aligned placement is active, written by
// the positioning module when told aligned (decision 8, chosen by the user 2026-11-03; design point 2 of M5 track 9).
/// <summary>Represents the state of a select positioner, which the part exposes as data attributes and passes to its class and style functions.</summary>
/// <param name="Open">A value indicating whether the popup is open; projected as <c>data-open</c> or <c>data-closed</c>.</param>
/// <param name="Side">
/// The side of the anchor the popup is placed on, or <see cref="SelectSide.None"/> while it is placed over the trigger; its
/// <c>data-side</c> is written by the browser-side behavior.
/// </param>
/// <param name="Align">The alignment against the anchor; its <c>data-align</c> is written by the browser-side behavior.</param>
/// <param name="AnchorHidden">A value indicating whether the anchor is hidden; its <c>data-anchor-hidden</c> is written by the browser-side behavior.</param>
[PartState]
public sealed partial record SelectPositionerState(
    bool Open,
    [property: MirroredField] SelectSide? Side,
    [property: MirroredField] Align? Align,
    [property: MirroredField] bool AnchorHidden);

// select/positioner/SelectPositionerDataAttributes.ts (SEL-DOM-19).
/// <summary>Provides the names of the data attributes a select positioner renders.</summary>
public static class SelectPositionerDataAttributes
{
    /// <summary>Represents the attribute present while the popup is open: <c>data-open</c>.</summary>
    public const string Open = PopupDataAttributes.Open;

    /// <summary>Represents the attribute present while the popup is closed: <c>data-closed</c>.</summary>
    public const string Closed = PopupDataAttributes.Closed;

    /// <summary>Represents the attribute present while the anchor is hidden: <c>data-anchor-hidden</c>.</summary>
    public const string AnchorHidden = PopupDataAttributes.AnchorHidden;

    /// <summary>Represents the attribute whose value is the side of the anchor, or <c>none</c> while placed over the trigger: <c>data-side</c>.</summary>
    public const string Side = PopupDataAttributes.Side;

    /// <summary>Represents the attribute whose value is the alignment against the anchor: <c>data-align</c>.</summary>
    public const string Align = PopupDataAttributes.Align;
}

// select/positioner/SelectPositionerCssVars.ts (SEL-DOM-22).
/// <summary>Provides the names of the CSS variables a select positioner sets.</summary>
public static class SelectPositionerCssVars
{
    /// <summary>Represents the anchor's width: <c>--anchor-width</c>.</summary>
    public const string AnchorWidth = "--anchor-width";

    /// <summary>Represents the anchor's height: <c>--anchor-height</c>.</summary>
    public const string AnchorHeight = "--anchor-height";

    /// <summary>Represents the space between the anchor and the edge of the viewport, horizontally: <c>--available-width</c>.</summary>
    public const string AvailableWidth = "--available-width";

    /// <summary>Represents the space between the anchor and the edge of the viewport, vertically: <c>--available-height</c>.</summary>
    public const string AvailableHeight = "--available-height";

    /// <summary>Represents the point the popup grows from: <c>--transform-origin</c>.</summary>
    public const string TransformOrigin = "--transform-origin";
}

// SEL §11 (SelectPositionerContext); select/positioner/SelectPositionerContext.ts. The positioner's element (the
// `select` module's references), its placement mirror (the Arrow's) and the side its descendants render.
/// <summary>The state a select positioner shares with its popup, arrow and scroll arrows.</summary>
internal sealed class SelectPositionerContext
{
    private readonly SelectPositioner owner;

    /// <summary>Initializes a new instance of the <see cref="SelectPositionerContext"/> class.</summary>
    /// <param name="owner">The positioner.</param>
    public SelectPositionerContext(SelectPositioner owner)
    {
        this.owner = owner;
    }

    /// <summary>Gets the positioner's element, or <see langword="null"/> before its first render.</summary>
    public ElementReference? Element => owner.Element;

    /// <summary>Gets the positioner's placement mirror and listeners.</summary>
    public PositionerContext Positioning => owner.Positioning;

    /// <summary>Gets the side the positioner's descendants render: <see cref="SelectSide.None"/> while aligned.</summary>
    public SelectSide? Side => owner.RenderedSide;

    /// <summary>Gets the alignment the positioner's descendants render.</summary>
    public Align? Align => owner.Positioning.Mirror.Align;

    /// <summary>Gets or sets the popup's element, which aligned placement measures.</summary>
    public ElementReference? PopupElement { get; set; }

    /// <summary>Gets or sets the list's element, the scroll container while a list is mounted.</summary>
    public ElementReference? ListElement { get; set; }

    /// <summary>Sends the positioner's browser-side options again, after a descendant's element changed.</summary>
    /// <returns>A task that completes when the options were sent.</returns>
    public Task SyncAsync() => owner.SyncSelectModuleAsync();

    /// <summary>Throws the error of a select part rendered outside a select positioner.</summary>
    /// <param name="context">The cascaded context, or <see langword="null"/>.</param>
    /// <returns>The context.</returns>
    /// <exception cref="InvalidOperationException"><paramref name="context"/> is <see langword="null"/>.</exception>
    public static SelectPositionerContext Require(SelectPositionerContext? context)
    {
        // SEL-EDGE-01, SEL-STR-03.
        return context ?? throw new InvalidOperationException(
            "Blazix: `SelectPositionerContext` is missing. SelectPositioner parts must be placed within `<SelectPositioner>`.");
    }
}

// Base UI's Select.Positioner (select/positioner/SelectPositioner.tsx); the contract is docs/components/select.md §3.8,
// §5.7, §10.
// - Track 2 of M4's PositionerBehavior with Select's defaults (SEL-POS-01: bottom, center, padding 5, the dropdown
//   collision avoidance with no fallback axis side, SEL-API-37), KeepMounted always (SEL-POS-02), anchor tracking off while
//   aligned unless set (SEL-API-38), the trigger as the anchor.
// - The `interactions` floating handle (a tree node: a select inside a popover is its child, SEL-DIS-10, SEL-EDGE-13).
// - The `popup` module's positioner role: the internal backdrop's cut-out of the trigger while mounted and modal
//   (SEL-STR-07, SEL-DOM-54), the anchored scroll lock while open and aligned or modal, with the touch width test
//   (SEL-POS-21, SEL-PTR-23).
// - The `select` module's positioner role: aligned placement, scroll-grow, the window resize close, the items' activation.
/// <summary>
/// Represents the element that positions a select's popup, over the trigger or against it, Base UI's
/// <c>Select.Positioner</c>, rendered as a <c>div</c>.
/// </summary>
public sealed class SelectPositioner : PartBase<SelectPositionerState>
{
    private static readonly StateAttributesMapping<SelectPositionerState> s_mapping = PopupStateMapping.CreateOpen<SelectPositionerState>();
    private static readonly CollisionAvoidance s_dropdown = new(FallbackAxisSide: FallbackAxisSide.None);
    private static readonly InteractionsFloatingOptions s_floating = new() { Node = true };

    private readonly SelectPositionerContext selectContext;
    private readonly RenderFragment cascadedContent;
    private PositionerBehavior? positioner;
    private InteractionsFloatingBehavior? floating;
    private PopupModuleAttachment? popupModule;
    private SelectModuleAttachment? selectModule;
    private SelectStore? store;
    private StoreSelection<SelectStoreState, bool>? open;
    private StoreSelection<SelectStoreState, bool>? mounted;
    private StoreSelection<SelectStoreState, TransitionStatus?>? status;
    private StoreSelection<SelectStoreState, bool>? modal;
    private StoreSelection<SelectStoreState, bool>? aligned;
    private bool alignmentRequested;
    private StoreSelection<SelectStoreState, bool>? touchOpened;
    private StoreSelection<SelectStoreState, string?>? triggerId;
    private StoreSelection<SelectStoreState, bool>? latched;
    private StoreSelection<SelectStoreState, bool>? highlightOnHover;
    private StoreSelection<SelectStoreState, bool>? hasSelectedValue;
    private StoreSelection<SelectStoreState, int>? textVersion;
    private SelectPositionerState? state;
    private RenderFragment? cascade;
    private RenderFragment? cascadeFor;
    private RenderFragment? consumerContent;

    /// <summary>Initializes a new instance of the <see cref="SelectPositioner"/> class.</summary>
    public SelectPositioner()
    {
        selectContext = new SelectPositionerContext(this);
        cascadedContent = RenderCascade;
    }

    [Inject]
    private IJSRuntime JSRuntime { get; set; } = null!;

    [CascadingParameter]
    private SelectStore? RootStore { get; set; }

    [CascadingParameter]
    private PopupPortalContext? Portal { get; set; }

    [CascadingParameter]
    private DirectionProviderContext? DirectionContext { get; set; }

    // SEL-API-35
    /// <summary>Gets or sets a value indicating whether the popup is placed over the trigger, the selected item's text aligned with the value's.</summary>
    /// <value>
    /// <see langword="true"/> by default. It applies to mouse and keyboard opening only and falls back to placement against
    /// the trigger when there is not enough room; a change while the popup is open applies after it closes.
    /// </value>
    [Parameter]
    public bool AlignItemWithTrigger { get; set; } = true;

    // SEL-API-34
    /// <summary>Gets or sets the element or point the popup is positioned against outside aligned placement.</summary>
    /// <value>An element, an element id or a virtual rectangle; <see langword="null"/>, the default, for the trigger.</value>
    [Parameter]
    public AnchorValue? Anchor { get; set; }

    /// <summary>Gets or sets the CSS position of the positioner outside aligned placement.</summary>
    /// <value><see cref="Primitives.PositionMethod.Absolute"/> when <see langword="null"/>, the default.</value>
    [Parameter]
    public PositionMethod? PositionMethod { get; set; }

    /// <summary>Gets or sets the side of the anchor the popup is placed on outside aligned placement.</summary>
    /// <value><see cref="Primitives.Side.Bottom"/> when <see langword="null"/>, the default.</value>
    [Parameter]
    public Side? Side { get; set; }

    /// <summary>Gets or sets the distance between the anchor and the popup.</summary>
    /// <value>A number of pixels or an <see cref="Offset"/>; 0 when <see langword="null"/>, the default.</value>
    [Parameter]
    public OffsetValue? SideOffset { get; set; }

    /// <summary>Gets or sets how the popup is aligned against the anchor outside aligned placement.</summary>
    /// <value><see cref="Primitives.Align.Center"/> when <see langword="null"/>, the default.</value>
    [Parameter]
    public Align? Align { get; set; }

    /// <summary>Gets or sets the shift of the popup along the alignment axis.</summary>
    /// <value>A number of pixels or an <see cref="Offset"/>; 0 when <see langword="null"/>, the default.</value>
    [Parameter]
    public OffsetValue? AlignOffset { get; set; }

    /// <summary>Gets or sets the area the popup is kept inside when it collides.</summary>
    /// <value>The clipping ancestors when <see langword="null"/>, the default.</value>
    [Parameter]
    public CollisionBoundaryValue? CollisionBoundary { get; set; }

    /// <summary>Gets or sets the space kept between the popup and the collision boundary.</summary>
    /// <value>5 pixels when <see langword="null"/>, the default.</value>
    [Parameter]
    public CollisionPaddingValue? CollisionPadding { get; set; }

    // SEL-API-37
    /// <summary>Gets or sets how the popup avoids collisions.</summary>
    /// <value>
    /// When <see langword="null"/>, the default, the popup flips between the sides of the main axis and never to the
    /// perpendicular one.
    /// </value>
    [Parameter]
    public CollisionAvoidance? CollisionAvoidance { get; set; }

    /// <summary>Gets or sets the minimum distance between the arrow and the popup's corners.</summary>
    /// <value>5 pixels when <see langword="null"/>, the default.</value>
    [Parameter]
    public double? ArrowPadding { get; set; }

    /// <summary>Gets or sets a value indicating whether the popup stays in view when the anchor scrolls out of it.</summary>
    /// <value><see langword="false"/> by default.</value>
    [Parameter]
    public bool Sticky { get; set; }

    // SEL-API-38
    /// <summary>Gets or sets a value indicating whether the popup stops following the anchor on scroll, resize and layout shifts.</summary>
    /// <value>
    /// <see langword="null"/>, the default, stops it exactly while the popup is placed over the trigger; a value applies
    /// whatever the placement.
    /// </value>
    [Parameter]
    public bool? DisableAnchorTracking { get; set; }

    /// <inheritdoc />
    protected override string DefaultTagName => "div";

    /// <inheritdoc />
    protected override SelectPositionerState State
    {
        get
        {
            PositionerMirror mirror = positioner!.Mirror;
            bool isOpen = open!.Value;
            SelectSide? side = RenderedSide;
            if (state is null || state.Open != isOpen || state.Side != side || state.Align != mirror.Align || state.AnchorHidden != mirror.AnchorHidden)
            {
                state = new SelectPositionerState(isOpen, side, mirror.Align, mirror.AnchorHidden);
            }

            return state;
        }
    }

    /// <inheritdoc />
    protected override StateAttributesMapping<SelectPositionerState>? StateAttributesMapping => s_mapping;

    /// <inheritdoc />
    protected override bool UsesGeneratedId => RenderFunction is not null;

    /// <summary>Gets the positioner's placement context.</summary>
    internal PositionerContext Positioning => positioner!.Context;

    /// <summary>Gets the side the positioner renders: <see cref="SelectSide.None"/> while aligned placement is active.</summary>
    internal SelectSide? RenderedSide => SelectSideAttributes.From(positioner!.Mirror.Side, aligned!.Value);

    /// <summary>Gets a task that completes when the positioner's behaviors are first attached.</summary>
    internal Task TwinAttached => Task.WhenAll(positioner!.TwinAttached, floating!.TwinAttached, popupModule!.Attached);

    /// <summary>Gets the positioner's behavior, for the tests.</summary>
    internal PositionerBehavior Behavior => positioner!;

    /// <inheritdoc />
    public override Task SetParametersAsync(ParameterView parameters)
    {
        ChildContent = null;
        return base.SetParametersAsync(parameters);
    }

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">The positioner is not inside a <see cref="SelectRoot{TValue}"/>.</exception>
    protected override void OnInitialized()
    {
        store = SelectStore.Require(RootStore);
        positioner = new PositionerBehavior(JSRuntime, OnMirrorChanged, InvokeAsync);
        floating = new InteractionsFloatingBehavior(JSRuntime, request => store.RequestOpenAsync(request.Open, request.Details), DispatchExceptionAsync);
        popupModule = new PopupModuleAttachment(JSRuntime);
        selectModule = new SelectModuleAttachment(JSRuntime);
        SelectTwin twin = selectModule.Twin;
        SelectStore current = store;
        twin.AlignedChanged += aligned => _ = InvokeAsync(() =>
        {
            if (!aligned)
            {
                current.LatchFallback();
            }
        });
        twin.ArrowsChanged += visibility => _ = InvokeAsync(() =>
        {
            current.SetScrollArrowVisible(up: true, visibility.Up);
            current.SetScrollArrowVisible(up: false, visibility.Down);
        });
        twin.Placed += () => _ = InvokeAsync(current.HighlightFirstItemAfterPlacementAsync);
        twin.WindowResized += snapshot => _ = InvokeAsync(() => current.RequestOpenAsync(false, new OpenChangeEventDetails(EventReason.WindowResize, snapshot)));
        twin.ActivationReported += activation => _ = InvokeAsync(() => current.ActivateItemAsync(activation.ItemId, activation.Event ?? EventSnapshot.CreatePlaceholder()));
        twin.ActivationDecider = DecideActivation;
        base.OnInitialized();
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        store!.SetAlignItemWithTrigger(AlignItemWithTrigger, first: !alignmentRequested);
        alignmentRequested = true;
        positioner!.SetDirectionContext(DirectionContext);
        positioner.SetPortalContext(Portal);
        floating!.PortalContext = Portal;
        floating.SessionToken = store.SessionToken;
        floating.Options = s_floating;
        SetInputs();
        consumerContent = ChildContent;
        ChildContent = cascadedContent;
    }

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        SetInputs();
        store!.SetPopupSide(positioner!.Mirror.Side);
        store.PositionerElement = Element;
        Task floatingSync = floating!.AfterRenderAsync(Enabled, Element, Id);
        Task moduleSync = SyncPopupModuleAsync();
        await positioner.AfterRenderAsync(Element, Id);
        await floatingSync;
        await moduleSync;
        store.PositionerRendered?.Invoke();

        // The `select` handle calls the `positioning` handle of the same element, so it attaches after it.
        if (Element is { } element)
        {
            await positioner.TwinAttached;
            await SyncSelectAsync(element);
        }
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            positioner?.Dispose();
            if (floating is not null)
            {
                _ = floating.DisposeAsync().AsTask();
            }

            if (popupModule is not null)
            {
                _ = popupModule.DisposeAsync().AsTask();
            }

            if (selectModule is not null)
            {
                selectModule.Twin.ActivationDecider = null;
                _ = selectModule.DisposeAsync().AsTask();
            }
        }

        base.Dispose(disposing);
    }

    /// <inheritdoc />
    protected override void SubscribeSelectors()
    {
        SelectorSubscription<SelectStoreState> subscription = SubscribeTo<SelectStoreState>(store!);
        open = subscription.Select(SelectStoreState.Open);
        mounted = subscription.Select(SelectStoreState.Mounted);
        status = subscription.Select(SelectStoreState.TransitionStatus);
        modal = subscription.Select(SelectStoreState.ModalSelector);
        aligned = subscription.Select(SelectStoreState.AlignedActiveSelector);
        touchOpened = subscription.Select(SelectStoreState.TouchOpened);
        triggerId = subscription.Select(SelectStoreState.TriggerIdSelector);
        latched = subscription.Select(static state => state.AlignItemWithTrigger);
        highlightOnHover = subscription.Select(SelectStoreState.HighlightItemOnHoverSelector);
        hasSelectedValue = subscription.Select(SelectStoreState.HasSelectedValueSelector);
        textVersion = subscription.Select(SelectStoreState.TextVersionSelector);
    }

    /// <inheritdoc />
    protected override void AddPartAttributes(AttributeList attributes)
    {
        SetInputs();
        positioner!.AddAttributes(attributes);
    }

    /// <inheritdoc />
    protected override void FinishAttributes(AttributeList attributes) => positioner!.FinishAttributes(attributes);

    // SEL-STR-07, SEL-DOM-54 (SelectPositioner.tsx:228): the internal backdrop while mounted and modal, inert while closed.
    /// <inheritdoc />
    protected override void BuildBeforeElement(RenderTreeBuilder builder)
    {
        if (mounted!.Value && modal!.Value)
        {
            PopupInternalBackdrop.Render(builder, store!.State.Lifecycle.FloatingId + "-backdrop", inert: !open!.Value);
        }
    }

    /// <summary>Sends the positioner's <c>select</c> options again, after a descendant's element changed.</summary>
    /// <returns>A task that completes when the options were sent.</returns>
    internal Task SyncSelectModuleAsync() => Element is { } element ? SyncSelectAsync(element) : Task.CompletedTask;

    private void SetInputs()
    {
        bool isAligned = aligned!.Value;
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
            DisableAnchorTracking = DisableAnchorTracking ?? isAligned,
            CollisionAvoidance = CollisionAvoidance ?? s_dropdown,
            Open = open!.Value,
            Mounted = mounted!.Value,
            KeepMounted = true,
            TransitionStatus = status!.Value,
            TriggerId = triggerId!.Value,
            SessionToken = store!.SessionToken,
        });
    }

    // SEL-POS-21, SEL-PTR-23 (useAnchoredPopupScrollLock((alignItemWithTriggerActive || modal) && open, …)).
    private async Task SyncPopupModuleAsync()
    {
        if (Element is not { } element)
        {
            return;
        }

        await PopupPortalContext.WhenMoved(Portal);
        bool isOpen = open!.Value;
        await popupModule!.SyncAsync(element, new PopupOptions
        {
            Positioner = new PopupPositionerOptions
            {
                Cutout = mounted!.Value && modal!.Value,
                ScrollLock = isOpen && (aligned!.Value || modal!.Value),
                TouchOpen = touchOpened!.Value,
                ActiveTriggerId = triggerId!.Value,
            },
        });
    }

    // SEL-POS-03..19, SEL-DIS-06, SEL-PTR-05..09 (the `select` module's positioner role).
    private Task SyncSelectAsync(ElementReference element)
    {
        SelectStore current = store!;
        bool rtl = positioner!.Direction == TextDirection.Rtl;
        return selectModule!.SyncAsync(
            element,
            new SelectOptions
            {
                Part = SelectTwin.PositionerPart,
                Open = open!.Value,
                Mounted = mounted!.Value,
                Ending = status!.Value == TransitionStatus.Ending,
                AlignItemWithTrigger = latched!.Value,
                TouchOpened = touchOpened!.Value,
                HighlightItemOnHover = highlightOnHover!.Value,
                Rtl = rtl,
                HasSelectedValue = hasSelectedValue!.Value,
                SelectedTextId = current.SelectedTextId,
                FirstTextId = current.FirstTextId,
                ItemsRevision = current.OrderRevision,
            },
            trigger: current.TriggerElement,
            popup: selectContext.PopupElement,
            list: selectContext.ListElement,
            value: current.ValueElement);
    }

    // `activate` on WebAssembly (ADR 04 Amendments 2026-11-03 (4)): the item's consumer onclick and the commit run inside
    // the click; a veto that completes synchronously stops it.
    private DecisionPolicy DecideActivation(EventSnapshot snapshot)
    {
        if (selectModule!.Twin.PendingActivation is not { } pending)
        {
            return DecisionPolicy.Run;
        }

        Task<bool> activation = store!.ActivateItemAsync(pending.ItemId, snapshot);
        if (activation.IsCompletedSuccessfully)
        {
            return activation.Result ? DecisionPolicy.Stop : DecisionPolicy.Run;
        }

        _ = ObserveAsync(activation);
        return DecisionPolicy.Run;
    }

    private async Task ObserveAsync(Task task)
    {
        try
        {
            await task;
        }
        catch (Exception exception) when (!task.IsCanceled)
        {
            await DispatchExceptionAsync(exception);
        }
    }

    private void RenderCascade(RenderTreeBuilder builder)
    {
        if (cascade is null || !ReferenceEquals(cascadeFor, consumerContent))
        {
            cascadeFor = consumerContent;
            cascade = positioner!.Context.Cascade(consumerContent);
        }

        builder.OpenComponent<CascadingValue<SelectPositionerContext>>(0);
        builder.AddComponentParameter(1, nameof(CascadingValue<SelectPositionerContext>.Value), selectContext);
        builder.AddComponentParameter(2, nameof(CascadingValue<SelectPositionerContext>.IsFixed), true);
        builder.AddComponentParameter(3, nameof(CascadingValue<SelectPositionerContext>.ChildContent), cascade);
        builder.CloseComponent();
    }

    // A part renders on a mirror change only when its state record changed (track 2's rule); SEL-STA-28: the resolved
    // side reaches the store, which the trigger renders.
    private void OnMirrorChanged()
    {
        store?.SetPopupSide(positioner!.Mirror.Side);
        SelectPositionerState? previous = state;
        if (previous is null || previous != State)
        {
            StateHasChanged();
        }
    }
}

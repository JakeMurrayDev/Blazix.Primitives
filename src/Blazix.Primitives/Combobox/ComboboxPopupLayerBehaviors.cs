using Blazix.Primitives.Interop;
using Blazix.Primitives.Rendering;
using Blazix.Primitives.Store;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.RenderTree;

namespace Blazix.Primitives;

// combobox/portal/ComboboxPortal.tsx (CMB §3.12, API-48/49): rendered iff mounted, kept mounted or force-mounted, over the
// shared PopupPortalBehavior (the lite portal of ADR 04 Amendments 2026-10-27); its KeepMounted reaches the Positioner.
/// <summary>The behavior of a combobox-family portal.</summary>
internal sealed class ComboboxPortalBehavior : IAsyncDisposable
{
    private readonly ComboboxPartServices services;

    private readonly PopupPortalBehavior portal;

    private readonly ComboboxPortalContext portalContext = new();

    private readonly RenderFragment cascadedContent;

    private ComboboxStore? store;

    private IDisposable? boundaryPart;

    private RenderFragment? consumerContent;

    /// <summary>Initializes a new instance of the <see cref="ComboboxPortalBehavior"/> class.</summary>
    /// <param name="services">The part's services.</param>
    public ComboboxPortalBehavior(ComboboxPartServices services)
    {
        this.services = services;
        portal = new PopupPortalBehavior(services.JSRuntime, services.RequestRender);
        cascadedContent = RenderCascade;
    }

    /// <summary>Gets the portal context the parts inside read.</summary>
    public PopupPortalContext Context => portal.Context;

    /// <summary>Gets the shared portal behavior, for the part's pipeline hooks.</summary>
    public PopupPortalBehavior Portal => portal;

    /// <summary>Gets the root's store.</summary>
    public ComboboxStore Store => store ?? throw new InvalidOperationException($"Blazix: `{services.Family}Portal` is used before its first render.");

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        boundaryPart?.Dispose();
        boundaryPart = null;
        await portal.DisposeAsync();
    }

    /// <summary>Reads the root and subscribes the part.</summary>
    /// <param name="cascaded">The cascaded store, or <see langword="null"/> outside a root.</param>
    /// <param name="subscribe">The part's subscription factory over the store.</param>
    public void Subscribe(ComboboxStore? cascaded, Func<ComboboxStore, SelectorSubscription<ComboboxStoreState>> subscribe)
    {
        store = ComboboxParts.RequireRoot(cascaded, services.Family);
        SelectorSubscription<ComboboxStoreState> subscription = subscribe(store);
        subscription.Select(ComboboxStoreState.Mounted);
        subscription.Select(static state => state.ForceMounted);
        boundaryPart = store.RegisterBoundaryPart();
    }

    /// <summary>Applies the part's parameters and returns the content to render inside the portal.</summary>
    /// <param name="keepMounted">The part's <c>KeepMounted</c>.</param>
    /// <param name="enclosing">The enclosing portal's context, or <see langword="null"/>.</param>
    /// <param name="nodeId">The portal node's id.</param>
    /// <param name="childContent">The consumer's content.</param>
    /// <returns>The content, inside the portal's and the combobox portal's cascades.</returns>
    public RenderFragment Apply(bool keepMounted, PopupPortalContext? enclosing, string? nodeId, RenderFragment? childContent)
    {
        portal.Enclosing = enclosing;
        portal.NodeId = nodeId;
        portalContext.KeepMounted = keepMounted;
        consumerContent = childContent;
        return portal.CascadeContent(cascadedContent);
    }

    /// <summary>Determines whether the portal renders its node.</summary>
    /// <param name="keepMounted">The part's <c>KeepMounted</c>.</param>
    /// <param name="container">The part's <c>Container</c>.</param>
    /// <returns><see langword="true"/> while mounted, kept mounted or force-mounted and the container is known.</returns>
    public bool Shown(bool keepMounted, PortalContainerValue? container)
    {
        ComboboxStoreState state = Store.State;
        return (keepMounted || state.Lifecycle.Mounted || state.ForceMounted) && PopupPortalBehavior.RendersNode(container);
    }

    /// <summary>Moves the portal's node and runs the render-batch boundary.</summary>
    /// <param name="node">The portal's node, or <see langword="null"/> while it does not render.</param>
    /// <param name="container">The part's <c>Container</c>.</param>
    /// <returns>A task that completes when the move and the boundary have run.</returns>
    public async Task AfterRenderAsync(ElementReference? node, PortalContainerValue? container)
    {
        await portal.OnAfterRenderAsync(node, container);
        await Store.AtBatchBoundaryAsync();
    }

    private void RenderCascade(RenderTreeBuilder builder)
    {
        builder.OpenComponent<CascadingValue<ComboboxPortalContext>>(0);
        builder.AddComponentParameter(1, nameof(CascadingValue<ComboboxPortalContext>.Value), portalContext);
        builder.AddComponentParameter(2, nameof(CascadingValue<ComboboxPortalContext>.IsFixed), true);
        builder.AddComponentParameter(3, nameof(CascadingValue<ComboboxPortalContext>.ChildContent), consumerContent);
        builder.CloseComponent();
    }
}

/// <summary>The combobox portal's state its positioner reads: whether the popup stays mounted while closed.</summary>
internal sealed class ComboboxPortalContext
{
    /// <summary>Gets or sets a value indicating whether the portal keeps the popup mounted while closed.</summary>
    public bool KeepMounted { get; set; }
}

// combobox/backdrop/ComboboxBackdrop.tsx (CMB §3.13): role="presentation", hidden while unmounted, user-select: none, the
// popup transition mapping, a TransitionsTwin whose status is mirrored.
/// <summary>The behavior of a combobox-family backdrop.</summary>
internal sealed class ComboboxBackdropBehavior : IAsyncDisposable
{
    private readonly ComboboxPartServices services;

    private ComboboxStore? store;

    private PopupPortalContext? portal;

    private TransitionsTwin? transitions;

    private readonly ComboboxHandleSync<TransitionsTwin, TransitionsOptions> sync = new();

    private TransitionStatus? mirrored;

    private bool disposed;

    /// <summary>Initializes a new instance of the <see cref="ComboboxBackdropBehavior"/> class.</summary>
    /// <param name="services">The part's services.</param>
    public ComboboxBackdropBehavior(ComboboxPartServices services)
    {
        this.services = services;
    }

    /// <summary>Gets the root's store.</summary>
    public ComboboxStore Store => store ?? throw new InvalidOperationException($"Blazix: `{services.Family}Backdrop` is used before its first render.");

    /// <summary>Gets the mirrored transition status.</summary>
    public TransitionStatus? TransitionStatus => mirrored ?? Store.Lifecycle.DerivedTransitionStatus;

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        disposed = true;
        if (transitions is not null)
        {
            await transitions.DisposeAsync();
        }
    }

    /// <summary>Reads the root and the portal and subscribes the part.</summary>
    /// <param name="cascaded">The cascaded store, or <see langword="null"/> outside a root.</param>
    /// <param name="portalContext">The cascaded portal context, or <see langword="null"/>.</param>
    /// <param name="subscribe">The part's subscription factory over the store.</param>
    public void Subscribe(ComboboxStore? cascaded, PopupPortalContext? portalContext, Func<ComboboxStore, SelectorSubscription<ComboboxStoreState>> subscribe)
    {
        store = ComboboxParts.RequireRoot(cascaded, services.Family);
        portal = portalContext;
        SelectorSubscription<ComboboxStoreState> subscription = subscribe(store);
        subscription.Select(ComboboxStoreState.Open);
        subscription.Select(ComboboxStoreState.Mounted);
        subscription.Select(ComboboxStoreState.Status);
    }

    /// <summary>Adds the backdrop's attributes.</summary>
    /// <param name="attributes">The part's attributes.</param>
    public void AddPartAttributes(AttributeList attributes)
    {
        attributes.Set("role", "presentation");
        if (!Store.Lifecycle.Mounted)
        {
            attributes.Set("hidden", true);
        }

        attributes.Set("style", "user-select: none; -webkit-user-select: none");
    }

    /// <summary>Attaches the transitions sync.Handle.</summary>
    /// <param name="element">The backdrop's element.</param>
    /// <returns>A task that completes when the sync.Handle has its options.</returns>
    public async Task AfterRenderAsync(ElementReference? element)
    {
        if (disposed || element is not { } target)
        {
            return;
        }

        await PopupPortalContext.WhenMoved(portal);
        PopupLifecycleState lifecycle = Store.Lifecycle;
        TransitionsOptions options = new() { Open = lifecycle.EffectiveOpen, Mounted = lifecycle.Mounted, Status = lifecycle.DerivedTransitionStatus, OpenSelector = "[data-open]" };
        if (transitions is null)
        {
            transitions = new TransitionsTwin(services.JSRuntime);
            transitions.StatusChanged += change =>
            {
                mirrored = change.Status;
                services.RequestRender();
            };
        }

        await sync.SyncAsync(transitions, target, options);
    }
}

// combobox/positioner/ComboboxPositioner.tsx (CMB §3.14, §10): PositionerBehavior anchored to InputGroup ?? Input (the
// Trigger with the input inside), the dropdown collision preset (fallbackAxisSide none); the `interactions` floating role;
// when modal, the internal backdrop with the cut-out (POS-03) and the anchored scroll lock (API-24) through the `popup`
// module. Its mirror's side is the store's PopupSide (STA-09, POS-04). Base UI's `lazyFlip: true` needs PositionerInputs to
// carry it (a Floating/ grant, asked of the orchestrator; CMB-POS-02 stays open until then).
/// <summary>The behavior of a combobox-family positioner.</summary>
internal sealed class ComboboxPositionerBehavior : IAsyncDisposable
{
    private static readonly CollisionAvoidance s_dropdown = new(FallbackAxisSide: FallbackAxisSide.None);

    private static readonly InteractionsFloatingOptions s_floating = new() { Node = true };

    private readonly ComboboxPartServices services;

    private readonly PositionerBehavior positioner;

    private readonly InteractionsFloatingBehavior floating;

    private readonly PopupModuleAttachment module;

    private ComboboxStore? store;

    private ComboboxPortalContext? portalContext;

    private PopupPortalContext? portal;

    private Side? reportedSide;

    private RenderFragment? cascadedContent;

    private RenderFragment? cascade;

    private ComboboxPositionerContext? familyContext;

    private bool disposed;

    /// <summary>Initializes a new instance of the <see cref="ComboboxPositionerBehavior"/> class.</summary>
    /// <param name="services">The part's services.</param>
    /// <param name="mirrorChanged">Runs when the mirrored placement changed.</param>
    public ComboboxPositionerBehavior(ComboboxPartServices services, Action mirrorChanged)
    {
        this.services = services;
        positioner = new PositionerBehavior(services.JSRuntime, () =>
        {
            ReportSide();
            mirrorChanged();
        }, action => services.InvokeAsync(() => { action(); return Task.CompletedTask; }));
        floating = new InteractionsFloatingBehavior(services.JSRuntime, request => Store.RequestOpenAsync(request.Open, request.Details), services.DispatchException) { Options = s_floating };
        module = new PopupModuleAttachment(services.JSRuntime);
    }

    /// <summary>Gets the root's store.</summary>
    public ComboboxStore Store => store ?? throw new InvalidOperationException($"Blazix: `{services.Family}Positioner` is used before its first render.");

    /// <summary>Gets the positioner context the popup, the arrow, the input and the list read.</summary>
    public PositionerContext Context => positioner.Context;

    /// <summary>Gets the mirror of the side, alignment and anchor visibility.</summary>
    public PositionerMirror Mirror => positioner.Mirror;

    /// <summary>Gets the shared positioner behavior, for the part's pipeline hooks.</summary>
    public PositionerBehavior Positioner => positioner;

    /// <summary>Gets a task that completes once the positioner's handles have attached.</summary>
    public Task TwinAttached => Task.WhenAll(positioner.TwinAttached, floating.TwinAttached);

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        disposed = true;
        positioner.Dispose();
        store?.SetPopupSide(null);
        await Task.WhenAll(floating.DisposeAsync().AsTask(), module.DisposeAsync().AsTask());
    }

    // Review B1 of the final review: the family marker (review B6) lives here, so Autocomplete's positioner shell cascades
    // it by calling this, as Base UI's re-exported part does; the shared positioner context sits inside it.
    /// <summary>Wraps the content in the family's positioner marker and the shared positioner context, cached per content.</summary>
    /// <param name="content">The part's content.</param>
    /// <returns>The content inside both cascades.</returns>
    public RenderFragment Cascade(RenderFragment? content)
    {
        if (cascade is null || !ReferenceEquals(cascadedContent, content))
        {
            cascadedContent = content;
            RenderFragment shared = positioner.Context.Cascade(content);
            ComboboxPositionerContext marker = familyContext ??= new ComboboxPositionerContext(positioner.Context);
            cascade = builder =>
            {
                builder.OpenComponent<CascadingValue<ComboboxPositionerContext>>(0);
                builder.AddComponentParameter(1, nameof(CascadingValue<ComboboxPositionerContext>.Value), marker);
                builder.AddComponentParameter(2, nameof(CascadingValue<ComboboxPositionerContext>.IsFixed), true);
                builder.AddComponentParameter(3, nameof(CascadingValue<ComboboxPositionerContext>.ChildContent), shared);
                builder.CloseComponent();
            };
        }

        return cascade;
    }

    /// <summary>Reads the root and the portal and subscribes the part.</summary>
    /// <param name="cascaded">The cascaded store, or <see langword="null"/> outside a root.</param>
    /// <param name="comboboxPortal">The cascaded combobox portal context, or <see langword="null"/> outside a portal.</param>
    /// <param name="portalContext">The cascaded portal context, or <see langword="null"/>.</param>
    /// <param name="subscribe">The part's subscription factory over the store.</param>
    public void Subscribe(ComboboxStore? cascaded, ComboboxPortalContext? comboboxPortal, PopupPortalContext? portalContext, Func<ComboboxStore, SelectorSubscription<ComboboxStoreState>> subscribe)
    {
        store = ComboboxParts.RequireRoot(cascaded, services.Family);
        this.portalContext = comboboxPortal ?? throw ComboboxParts.MissingPortal(services.Family);
        portal = portalContext;
        SelectorSubscription<ComboboxStoreState> subscription = subscribe(store);
        subscription.Select(ComboboxStoreState.Open);
        subscription.Select(ComboboxStoreState.Mounted);
        subscription.Select(ComboboxStoreState.Status);
        subscription.Select(ComboboxStoreState.ListEmpty);
        subscription.Select(static state => state.Modal);
        subscription.Select(static state => state.InputInsidePopup);
        subscription.Select(static state => state.ElementIds);
        subscription.Select(static state => state.Lifecycle.OpenMethod);
    }

    /// <summary>Applies the part's positioning parameters.</summary>
    /// <param name="inputs">The consumer's positioning inputs; the anchor, the open state and the collision preset are filled here.</param>
    /// <param name="direction">The direction context, or <see langword="null"/>.</param>
    public void Apply(PositionerInputs inputs, DirectionProviderContext? direction)
    {
        positioner.SetDirectionContext(direction);
        positioner.SetPortalContext(portal);
        floating.PortalContext = portal;
        floating.SessionToken = Store.SessionToken;
        SetInputs(inputs);
    }

    /// <summary>Sends the positioning inputs with the store's open state and the resolved anchor.</summary>
    /// <param name="inputs">The consumer's positioning inputs.</param>
    public void SetInputs(PositionerInputs inputs)
    {
        ComboboxStoreState state = Store.State;
        ComboboxElementIds ids = state.ElementIds;
        string? anchorId = state.InputInsidePopup ? ids.Trigger : ids.InputGroup ?? ids.Input;
        positioner.SetInputs(inputs with
        {
            Anchor = inputs.Anchor ?? (anchorId is null ? (AnchorValue?)null : new AnchorValue(anchorId)),
            CollisionAvoidance = inputs.CollisionAvoidance ?? s_dropdown,
            Open = state.Lifecycle.EffectiveOpen,
            Mounted = state.Lifecycle.Mounted,
            KeepMounted = portalContext!.KeepMounted,
            TransitionStatus = state.Lifecycle.DerivedTransitionStatus,
            SessionToken = Store.SessionToken,
        });
    }

    /// <summary>Renders the internal backdrop before the positioner while mounted and modal.</summary>
    /// <param name="builder">The part's render tree builder.</param>
    public void BuildBeforeElement(RenderTreeBuilder builder)
    {
        ComboboxStoreState state = Store.State;
        if (state.Lifecycle.Mounted && state.Modal)
        {
            PopupInternalBackdrop.Render(builder, state.Lifecycle.FloatingId + "-backdrop", inert: !state.Lifecycle.EffectiveOpen);
        }
    }

    /// <summary>Attaches the handles and sends their changed options.</summary>
    /// <param name="element">The positioner's element.</param>
    /// <param name="id">The id the part renders.</param>
    /// <returns>A task that completes when the handles have their options.</returns>
    public async Task AfterRenderAsync(ElementReference? element, string? id)
    {
        if (disposed)
        {
            return;
        }

        // The mirror starts on the requested side, which a first report that confirms it does not change: the side reaches
        // the store once the positioner renders, for the trigger's and the input's data-popup-side (ComboboxTrigger.tsx:84).
        ReportSide();
        Task floatingSync = floating.AfterRenderAsync(true, element, id);
        Task moduleSync = SyncPopupModuleAsync(element);
        await positioner.AfterRenderAsync(element, id);
        await floatingSync;
        await moduleSync;
    }

    // CMB-POS-03, API-24 (ComboboxPositioner.tsx:96-101, 124-132): the cut-out of InputGroup ?? Input ?? Trigger while mounted
    // and modal; the anchored scroll lock while open and modal, a touch open locking only a popup that spans the viewport.
    private async Task SyncPopupModuleAsync(ElementReference? element)
    {
        if (element is not { } target)
        {
            return;
        }

        await PopupPortalContext.WhenMoved(portal);
        ComboboxStoreState state = Store.State;
        ComboboxElementIds ids = state.ElementIds;
        await module.SyncAsync(target, new PopupOptions
        {
            SessionToken = Store.SessionToken,
            Positioner = new PopupPositionerOptions
            {
                Cutout = state.Lifecycle.Mounted && state.Modal,
                CutoutElementId = ids.InputGroup ?? ids.Input ?? ids.Trigger,
                ScrollLock = state.Lifecycle.EffectiveOpen && state.Modal,
                TouchOpen = state.Lifecycle.OpenMethod == InteractionType.Touch,
            },
        });
    }

    private void ReportSide()
    {
        Side side = positioner.Mirror.Side;
        if (reportedSide != side)
        {
            reportedSide = side;
            Store.SetPopupSide(side);
        }
    }
}

// combobox/popup/ComboboxPopup.tsx (CMB §3.15, §6.3): role presentation (dialog with the input inside), tabindex="-1";
// FocusManagerBehavior with Modal = !inside || Modal, TypeableComboboxAnchor, InsideElementIds = the two dismiss spans,
// Disabled = !mounted, its focus-out close `focus-out` (DIS-04); a TransitionsTwin; the end dismiss span after it
// (BuildAfterElement); the `combobox` `popup` handle's focus redirect to the input.
/// <summary>The behavior of a combobox-family popup.</summary>
internal sealed class ComboboxPopupBehavior : IAsyncDisposable
{
    private readonly ComboboxPartServices services;

    private readonly FocusManagerBehavior focus;

    private ComboboxStore? store;

    private PositionerContext? positioner;

    private PopupPortalContext? portal;

    private TransitionsTwin? transitions;

    private TwinHandle<TransitionsOptions>? transitionsHandle;

    private TransitionsOptions? sentTransitions;

    private ComboboxTwin? twin;

    private TwinHandle<ComboboxOptions>? twinHandle;
    private ComboboxOptions? sentTwinOptions;

    private IDisposable? boundaryPart;

    private FocusTargetValue? initialFocus;

    private Func<InteractionType, FocusTargetValue?>? initialFocusResolver;

    private FocusTargetValue? finalFocus;

    private Func<InteractionType, FocusTargetValue?>? finalFocusResolver;

    private string? consumerId;

    private Action? positionerChanged;

    private bool published;

    private string? id;

    private string? dismissId;

    private bool disposed;

    /// <summary>Initializes a new instance of the <see cref="ComboboxPopupBehavior"/> class.</summary>
    /// <param name="services">The part's services.</param>
    public ComboboxPopupBehavior(ComboboxPartServices services)
    {
        this.services = services;
        focus = new FocusManagerBehavior(services.JSRuntime, services.InvokeAsync, snapshot => Store.RequestOpenAsync(false, new OpenChangeEventDetails(EventReason.FocusOut, snapshot)));
    }

    /// <summary>Gets the root's store.</summary>
    public ComboboxStore Store => store ?? throw new InvalidOperationException($"Blazix: `{services.Family}Popup` is used before its first render.");

    /// <summary>Gets the positioner's mirror the popup projects.</summary>
    public PositionerMirror Mirror => positioner!.Mirror;

    /// <summary>Gets the transition status.</summary>
    public TransitionStatus? TransitionStatus => Store.Lifecycle.DerivedTransitionStatus;

    /// <summary>Gets the id the popup renders.</summary>
    public string? Id => id;

    /// <summary>Gets a task that completes once the popup's handles have attached.</summary>
    public Task TwinAttached => focus.TwinAttached;

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        if (positionerChanged is not null)
        {
            positioner?.RemoveListener(positionerChanged);
        }

        boundaryPart?.Dispose();
        store?.SetElementIds(static ids => ids with { Popup = null, EndDismiss = null });
        Task transitionsDisposal = transitions?.DisposeAsync().AsTask() ?? Task.CompletedTask;
        Task twinDisposal = twin?.DisposeAsync().AsTask() ?? Task.CompletedTask;
        await Task.WhenAll(focus.DisposeAsync().AsTask(), transitionsDisposal, twinDisposal);
    }

    /// <summary>Reads the root and the positioner and subscribes the part.</summary>
    /// <param name="cascaded">The cascaded store, or <see langword="null"/> outside a root.</param>
    /// <param name="positionerContext">The cascaded positioner context, or <see langword="null"/>.</param>
    /// <param name="portalContext">The cascaded portal context, or <see langword="null"/>.</param>
    /// <param name="subscribe">The part's subscription factory over the store.</param>
    /// <param name="onPositionerChanged">Called when the positioner's mirrored placement changed (CMB-DOM-12).</param>
    public void Subscribe(ComboboxStore? cascaded, PositionerContext? positionerContext, PopupPortalContext? portalContext, Func<ComboboxStore, SelectorSubscription<ComboboxStoreState>> subscribe, Action onPositionerChanged)
    {
        store = ComboboxParts.RequireRoot(cascaded, services.Family);
        positioner = positionerContext ?? throw ComboboxParts.MissingPositioner(services.Family);
        positionerChanged = onPositionerChanged;
        positioner.AddListener(onPositionerChanged);
        portal = portalContext;
        focus.Portal = portalContext;
        SelectorSubscription<ComboboxStoreState> subscription = subscribe(store);
        subscription.Select(ComboboxStoreState.Open);
        subscription.Select(ComboboxStoreState.Mounted);
        subscription.Select(ComboboxStoreState.Status);
        subscription.Select(ComboboxStoreState.ListEmpty);
        subscription.Select(static state => state.Modal);
        subscription.Select(static state => state.InputInsidePopup);
        subscription.Select(static state => state.Lifecycle.OpenMethod);
        subscription.Select(static state => state.Lifecycle.OpenChange);
        subscription.Select(static state => state.ElementIds.Input);
        subscription.Select(static state => state.ElementIds.Trigger);
        subscription.Select(static state => state.ElementIds.StartDismiss);
        boundaryPart = store.RegisterBoundaryPart();
    }

    /// <summary>Applies the part's focus parameters and its rendered id.</summary>
    /// <param name="initial">The part's <c>InitialFocus</c>.</param>
    /// <param name="initialResolver">The part's <c>InitialFocusResolver</c>.</param>
    /// <param name="final">The part's <c>FinalFocus</c>.</param>
    /// <param name="finalResolver">The part's <c>FinalFocusResolver</c>.</param>
    /// <param name="render">The part's substitute element, or <see langword="null"/>.</param>
    /// <param name="consumerAttributes">The consumer's attributes, for its <c>id</c>.</param>
    public void Apply(FocusTargetValue? initial, Func<InteractionType, FocusTargetValue?>? initialResolver, FocusTargetValue? final, Func<InteractionType, FocusTargetValue?>? finalResolver, RenderFragment? render, IReadOnlyDictionary<string, object>? consumerAttributes)
    {
        initialFocus = initial;
        initialFocusResolver = initialResolver;
        finalFocus = final;
        finalFocusResolver = finalResolver;

        // CMB-DOM-24 (ComboboxPopup.tsx:56-62): the store's popup id is the rendered element's, so a substitute element's own
        // id wins over the consumer's (RND-DOM-32 precedence (1)). A substitute callback is opaque to C#: its id is not read.
        string? consumer = consumerAttributes is not null && consumerAttributes.TryGetValue("id", out object? supplied) ? supplied as string : null;
        consumerId = render is not null && SubstituteElementId(render) is { Length: > 0 } own ? own : consumer;
        Resolve();
        focus.SetInputs(FocusInputs());
    }

    /// <summary>Adds the popup's attributes.</summary>
    /// <param name="attributes">The part's attributes.</param>
    public void AddPartAttributes(AttributeList attributes)
    {
        // The layout and the root's id can change with no parameter set of the popup.
        Resolve();
        ComboboxStoreState state = Store.State;
        if (id is not null)
        {
            attributes.Set("id", id);
        }

        attributes.Set("role", state.InputInsidePopup ? "dialog" : "presentation");
        attributes.Set("tabindex", "-1");
        attributes.Set(PopupMarkerAttributes.Focusable, string.Empty);
    }

    /// <summary>Renders the focus manager's guard before the popup.</summary>
    /// <param name="builder">The part's render tree builder.</param>
    public void BuildBeforeElement(RenderTreeBuilder builder) => focus.BuildBeforeElement(builder);

    /// <summary>Renders the end dismiss button and the focus manager's guard after the popup.</summary>
    /// <param name="builder">The part's render tree builder.</param>
    public void BuildAfterElement(RenderTreeBuilder builder)
    {
        ComboboxStoreState state = Store.State;
        if (!state.InputInsidePopup || state.Modal)
        {
            dismissId ??= services.Ids.Next();
            string own = dismissId;
            Store.SetElementIds(ids => ids with { EndDismiss = own });
            builder.OpenRegion(0);
            ComboboxDismissButton.Build(builder, own);
            builder.CloseRegion();
        }

        focus.BuildAfterElement(builder);
    }

    /// <summary>Attaches the handles and sends their changed options.</summary>
    /// <param name="element">The popup's element.</param>
    /// <returns>A task that completes when the handles have their options.</returns>
    public async Task AfterRenderAsync(ElementReference? element)
    {
        if (disposed)
        {
            return;
        }

        focus.SetInputs(FocusInputs());
        Task focusSync = focus.AfterRenderAsync(element, id);
        if (element is { } target)
        {
            await PopupPortalContext.WhenMoved(portal);
            await Task.WhenAll(SyncTransitionsAsync(target), SyncTwinAsync(target));
        }

        await focusSync;
        await Store.AtBatchBoundaryAsync();
    }

    // The id attribute of a substitute element's root: the first element frame's own attributes, or the start tag of a markup
    // block Razor folded (as the render pipeline reads them, ADR 02 decision 1).
    private static string? SubstituteElementId(RenderFragment render)
    {
        using RenderTreeBuilder builder = new();
        render(builder);
#pragma warning disable BL0006 // The frames are read through the public GetFrames(), as the render pipeline does (ADR 02 decision 1).
        ArrayRange<RenderTreeFrame> frames = builder.GetFrames();
        for (int i = 0; i < frames.Count; i++)
        {
            RenderTreeFrame frame = frames.Array[i];
            if (frame.FrameType == RenderTreeFrameType.Element)
            {
                for (int j = i + 1; j < frames.Count && frames.Array[j].FrameType == RenderTreeFrameType.Attribute; j++)
                {
                    if (string.Equals(frames.Array[j].AttributeName, "id", StringComparison.Ordinal))
                    {
                        return frames.Array[j].AttributeValue as string;
                    }
                }

                return null;
            }

            if (frame.FrameType == RenderTreeFrameType.Markup)
            {
                if (!MarkupRootParser.TryParse(frame.MarkupContent, out MarkupRootParser.Root root, out _))
                {
                    return null;
                }

                foreach (KeyValuePair<string, string> attribute in root.Attributes)
                {
                    if (string.Equals(attribute.Key, "id", StringComparison.Ordinal))
                    {
                        return attribute.Value;
                    }
                }

                return null;
            }
        }
#pragma warning restore BL0006

        return null;
    }

    // CMB-DOM-24 (ComboboxPopup.tsx:65, root/utils/index.ts:13-15, derived at every render): the consumer's id, else
    // `{rootId}-popup` with the input inside; published over the popup's own previous id (review F20).
    private void Resolve()
    {
        ComboboxStoreState state = Store.State;
        string? next = consumerId ?? (state.InputInsidePopup ? state.Id + "-popup" : null);
        if (published && string.Equals(next, id, StringComparison.Ordinal))
        {
            return;
        }

        string? previous = id;
        id = next;
        published = true;
        Store.SetElementIds(ids => ids.Popup is null || ids.Popup == previous ? ids with { Popup = next } : ids);
    }

    private FocusManagerInputs FocusInputs()
    {
        ComboboxStoreState state = Store.State;
        PopupLifecycleState lifecycle = state.Lifecycle;
        bool inside = state.InputInsidePopup;
        bool modal = !inside || state.Modal;
        InteractionType method = lifecycle.OpenMethod ?? InteractionType.Unknown;
        List<string> insideIds = [];
        if (state.ElementIds.StartDismiss is { } start)
        {
            insideIds.Add(start);
        }

        if (state.ElementIds.EndDismiss is { } end)
        {
            insideIds.Add(end);
        }

        return new FocusManagerInputs
        {
            Open = lifecycle.EffectiveOpen,
            Modal = modal,
            Disabled = !lifecycle.Mounted,
            InitialFocus = ResolveInitialFocus(method, inside, state),
            ReturnFocus = ResolveFinalFocus(method, inside),
            OpenInteractionType = lifecycle.OpenMethod,
            TypeableComboboxAnchor = !inside,
            DomReferenceId = inside ? state.ElementIds.Trigger : state.ElementIds.Input,
            InsideElementIds = insideIds.Count > 0 ? insideIds : null,
            Relay = lifecycle.OpenChange,
            SessionToken = Store.SessionToken,
        };
    }

    // CMB-API-51 (ComboboxPopup.tsx:125-133): outside, no initial focus; inside, the popup for a touch open, else the input.
    private FocusTargetValue? ResolveInitialFocus(InteractionType method, bool inside, ComboboxStoreState state)
    {
        if (initialFocusResolver is { } resolver)
        {
            return resolver(method) ?? new FocusTargetValue(false);
        }

        if (initialFocus is { } value)
        {
            return value;
        }

        if (!inside)
        {
            return new FocusTargetValue(false);
        }

        if (method == InteractionType.Touch && id is { } popupId)
        {
            return new FocusTargetValue(popupId);
        }

        return state.ElementIds.Input is { } inputId ? new FocusTargetValue(inputId) : (FocusTargetValue?)null;
    }

    // CMB-API-52 (ComboboxPopup.tsx:134-140): the default inside (the trigger), none outside.
    private FocusTargetValue? ResolveFinalFocus(InteractionType method, bool inside)
    {
        if (finalFocusResolver is { } resolver)
        {
            return resolver(method) ?? new FocusTargetValue(false);
        }

        return finalFocus ?? (inside ? (FocusTargetValue?)null : new FocusTargetValue(false));
    }

    private async Task SyncTransitionsAsync(ElementReference element)
    {
        PopupLifecycleState lifecycle = Store.Lifecycle;
        TransitionsOptions options = new()
        {
            Open = lifecycle.EffectiveOpen,
            Mounted = lifecycle.Mounted,
            Status = lifecycle.DerivedTransitionStatus,
            ReportOpenComplete = true,
            OpenSelector = "[data-open]",
            UnmountRequests = lifecycle.UnmountRequests,
            DisableTransitionWhileStarting = true,
        };
        if (transitions is null)
        {
            transitions = new TransitionsTwin(services.JSRuntime);
            transitions.StatusChanged += change => Store.MirrorStatus(change.Status);
            transitions.TransitionCompleted += opened => _ = Observe(services.InvokeAsync(() => Store.CompleteTransitionAsync(opened)));
            sentTransitions = options;
            transitionsHandle = await transitions.AttachAsync(element, options);
            return;
        }

        if (transitionsHandle is not null && options != sentTransitions)
        {
            sentTransitions = options;
            await transitionsHandle.UpdateAsync(options);
        }
    }

    private async Task SyncTwinAsync(ElementReference element)
    {
        ComboboxStoreState state = Store.State;
        ComboboxOptions options = new()
        {
            Part = ComboboxTwin.PopupPart,
            InsidePopup = state.InputInsidePopup,
            InputId = state.ElementIds.Input,
            ListId = state.ElementIds.List,
            TouchOpened = state.Lifecycle.OpenMethod == InteractionType.Touch,
            DismissId = state.ElementIds.EndDismiss,
        };
        if (twinHandle is null)
        {
            // ComboboxInternalDismissButton.tsx:28: close-press with the button as its trigger.
            twin = new ComboboxTwin(services.JSRuntime);
            twin.DismissPressed += snapshot => _ = Observe(services.InvokeAsync(
                () => Store.RequestOpenAsync(false, new OpenChangeEventDetails(EventReason.ClosePress, snapshot, new EventTrigger(Store.State.ElementIds.EndDismiss, null)))));
            sentTwinOptions = options;
            twinHandle = await twin.AttachAsync(element, options);
            return;
        }

        if (options != sentTwinOptions)
        {
            sentTwinOptions = options;
            await twinHandle.UpdateAsync(options);
        }
    }

    private async Task Observe(Task task)
    {
        try
        {
            await task;
        }
        catch (Exception exception) when (!task.IsCanceled)
        {
            await services.DispatchException(exception);
        }
    }
}

// combobox/arrow/ComboboxArrow.tsx (CMB §3.16) over PositionerArrowBehavior: data-side, data-align and data-uncentered
// C# → JS (OwnershipRegistry.cs's popover row).
/// <summary>The behavior of a combobox-family arrow.</summary>
internal sealed class ComboboxArrowBehavior : IDisposable
{
    private readonly ComboboxPartServices services;

    private readonly PositionerArrowBehavior arrow;

    private ComboboxStore? store;

    /// <summary>Initializes a new instance of the <see cref="ComboboxArrowBehavior"/> class.</summary>
    /// <param name="services">The part's services.</param>
    public ComboboxArrowBehavior(ComboboxPartServices services)
    {
        this.services = services;
        arrow = new PositionerArrowBehavior(services.RequestRender);
    }

    /// <summary>Gets the arrow's behavior.</summary>
    public PositionerArrowBehavior Arrow => arrow;

    /// <summary>Gets a value indicating whether the popup is open.</summary>
    public bool Open => store?.Lifecycle.EffectiveOpen ?? false;

    /// <inheritdoc />
    public void Dispose() => arrow.Dispose();

    /// <summary>Reads the root and the positioner and subscribes the part.</summary>
    /// <param name="cascaded">The cascaded store, or <see langword="null"/> outside a root.</param>
    /// <param name="positioner">The cascaded positioner context, or <see langword="null"/>.</param>
    /// <param name="subscribe">The part's subscription factory over the store.</param>
    public void Subscribe(ComboboxStore? cascaded, PositionerContext? positioner, Func<ComboboxStore, SelectorSubscription<ComboboxStoreState>> subscribe)
    {
        store = ComboboxParts.RequireRoot(cascaded, services.Family);
        arrow.SetContext(positioner ?? throw ComboboxParts.MissingPositioner(services.Family));
        subscribe(store).Select(ComboboxStoreState.Open);
    }

    /// <summary>Adds the arrow's attributes.</summary>
    /// <param name="attributes">The part's attributes.</param>
    public void AddPartAttributes(AttributeList attributes)
    {
        attributes.Set("aria-hidden", "true");
        arrow.AddAttributes(attributes);
    }
}

// combobox/status/ComboboxStatus.tsx and combobox/empty/ComboboxEmpty.tsx (CMB §3.17, §3.18, ARIA-13): role="status",
// aria-live="polite", aria-atomic="true", a DomTwin with LiveRegion = true (UTL-A11Y-01..04, the first C# consumer). The
// Empty variant shows its children only while the filtered list is empty and registers itself (design point 5's "no Empty").
/// <summary>The behavior of a combobox-family live region: a status or an empty state.</summary>
internal sealed class ComboboxLiveRegionBehavior : IAsyncDisposable
{
    private readonly ComboboxPartServices services;

    private readonly bool empty;

    private ComboboxStore? store;

    private IDisposable? registration;

    private DomTwin? twin;

    private bool attached;

    /// <summary>Initializes a new instance of the <see cref="ComboboxLiveRegionBehavior"/> class.</summary>
    /// <param name="services">The part's services.</param>
    /// <param name="isEmpty">Whether the region is the empty state.</param>
    public ComboboxLiveRegionBehavior(ComboboxPartServices services, bool isEmpty)
    {
        this.services = services;
        empty = isEmpty;
    }

    private ComboboxStore Store => store ?? throw new InvalidOperationException($"Blazix: `{services.Family}{(empty ? "Empty" : "Status")}` is used before its first render.");

    /// <summary>Gets a value indicating whether the region renders its children.</summary>
    public bool ShowsChildren => !empty || Store.State.FilteredCount == 0;

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        registration?.Dispose();
        registration = null;
        if (twin is not null)
        {
            await twin.DisposeAsync();
        }
    }

    /// <summary>Reads the root and subscribes the part.</summary>
    /// <param name="cascaded">The cascaded store, or <see langword="null"/> outside a root.</param>
    /// <param name="subscribe">The part's subscription factory over the store.</param>
    public void Subscribe(ComboboxStore? cascaded, Func<ComboboxStore, SelectorSubscription<ComboboxStoreState>> subscribe)
    {
        store = ComboboxParts.RequireRoot(cascaded, services.Family);
        if (empty)
        {
            subscribe(store).Select(ComboboxStoreState.ListEmpty);
            registration = store.RegisterEmpty();
        }
    }

    /// <summary>Adds the region's attributes.</summary>
    /// <param name="attributes">The part's attributes.</param>
    public void AddPartAttributes(AttributeList attributes)
    {
        attributes.Set("role", "status");
        attributes.Set("aria-live", "polite");
        attributes.Set("aria-atomic", "true");
    }

    /// <summary>Attaches the live-region marker.</summary>
    /// <param name="element">The region's element.</param>
    /// <returns>A task that completes when the handle has its options.</returns>
    public async Task AfterRenderAsync(ElementReference? element)
    {
        if (attached || element is not { } target)
        {
            return;
        }

        attached = true;
        twin = new DomTwin(services.JSRuntime);
        await twin.AttachAsync(target, new DomOptions { LiveRegion = true });
    }
}

// The structural parts (CMB-STR-08; ADR 16 rule 10: no behavior object where there is no state): the Icon's span, the
// Row's role and the Separator's attributes over ListboxSeparatorAttributes.
/// <summary>Writes the fixed attributes of the combobox-family parts that have no behavior.</summary>
internal static class ComboboxStaticAttributes
{
    /// <summary>Gets the icon's default content, <c>▼</c>.</summary>
    public static RenderFragment IconContent { get; } = static builder => builder.AddContent(0, "▼");

    /// <summary>Writes the icon's attributes: <c>aria-hidden</c>.</summary>
    /// <param name="attributes">The part's attributes.</param>
    public static void WriteIcon(AttributeList attributes) => attributes.Set("aria-hidden", "true");

    /// <summary>Writes the row's attributes: <c>role="row"</c>.</summary>
    /// <param name="attributes">The part's attributes.</param>
    public static void WriteRow(AttributeList attributes) => attributes.Set("role", "row");

    /// <summary>Writes the separator's attributes: <c>role="presentation"</c>.</summary>
    /// <param name="attributes">The part's attributes.</param>
    public static void WriteSeparator(AttributeList attributes) => Utilities.ListboxSeparatorAttributes.Write(attributes);
}

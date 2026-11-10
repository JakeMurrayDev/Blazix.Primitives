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

// SEL-STA-21 (select/popup/SelectPopup.tsx:188-193, 511-528). Side and Align are C#-rendered from the Positioner's mirror
// (the POP Popup precedent, POP-DOM-27; `none` while aligned), a round trip behind the Positioner's on Server;
// TransitionStatus is the `transitions` module's (ADR 04 §2): mirrored.
/// <summary>Represents the state of a select popup, which the part exposes as data attributes and passes to its class and style functions.</summary>
/// <param name="Open">A value indicating whether the popup is open; projected as <c>data-open</c> or <c>data-closed</c>.</param>
/// <param name="TransitionStatus">
/// The phase of the popup's enter or exit transition, or <see langword="null"/>; its <c>data-starting-style</c> and
/// <c>data-ending-style</c> are written by the browser-side behavior.
/// </param>
/// <param name="Side">The side of the anchor the popup is placed on, or <see cref="SelectSide.None"/> while it is placed over the trigger; projected as <c>data-side</c>.</param>
/// <param name="Align">The alignment against the anchor; projected as <c>data-align</c>.</param>
[PartState]
public sealed partial record SelectPopupState(bool Open, [property: MirroredField] TransitionStatus? TransitionStatus, SelectSide? Side, Align? Align);

// select/popup/SelectPopupDataAttributes.ts (SEL-DOM-26).
/// <summary>Provides the names of the data attributes a select popup renders.</summary>
public static class SelectPopupDataAttributes
{
    /// <summary>Represents the attribute present while the popup is open: <c>data-open</c>.</summary>
    public const string Open = PopupDataAttributes.Open;

    /// <summary>Represents the attribute present while the popup is closed: <c>data-closed</c>.</summary>
    public const string Closed = PopupDataAttributes.Closed;

    /// <summary>Represents the attribute present while the popup is animating in: <c>data-starting-style</c>.</summary>
    public const string StartingStyle = PopupDataAttributes.StartingStyle;

    /// <summary>Represents the attribute present while the popup is animating out: <c>data-ending-style</c>.</summary>
    public const string EndingStyle = PopupDataAttributes.EndingStyle;

    /// <summary>Represents the attribute whose value is the side of the anchor, or <c>none</c> while placed over the trigger: <c>data-side</c>.</summary>
    public const string Side = PopupDataAttributes.Side;

    /// <summary>Represents the attribute whose value is the alignment against the anchor: <c>data-align</c>.</summary>
    public const string Align = PopupDataAttributes.Align;
}

// Base UI's Select.Popup (select/popup/SelectPopup.tsx); the contract is docs/components/select.md §3.9, §5.8, §6.3, §9.
// - SEL-DOM-23..25: the listbox without a List (`role`, `<root id>-list`, `aria-multiselectable`, `aria-readonly`), a
//   presentational wrapper with one; `tabindex="-1"` and `data-base-ui-focusable` (the popup focus element).
// - The floating list handle (SetList(…, Floating), ReportItems; Base UI's floating props are the Popup's,
//   SelectRoot.tsx:432-441), cascaded to the items, attached after the portal's move (M4 track 1's design point 3); its
//   order report joins the item values (ADR 04 Amendments 2026-11-03 (1)).
// - The focus manager (SelectPopup.tsx:479-486): non-modal, disabled while unmounted, the return focus from FinalFocus or
//   FinalFocusResolver, restoring to the last tabbable element, the focus-out close; the open method.
// - Its TransitionsTwin with TRN §6.1's Select options (no enter on the initial open, ReportOpenComplete; §16 Q10
//   replicated: no `true` completion without a Popup), `transition: none` while starting.
// - SEL-KEY-14: the composite keys stop at the popup inside a toolbar (the `popup` module's StopCompositeKeys).
// - SEL-DOM-28: the scrollbar-hiding style before the element, with the CSP nonce, and the class while aligned without a List.
/// <summary>Represents the container of a select's list, Base UI's <c>Select.Popup</c>, rendered as a <c>div</c>.</summary>
public sealed class SelectPopup : PartBase<SelectPopupState>
{
    private const string OpenSelector = "[data-open]";
    private const string StartingStyle = "transition: none";

    private static readonly StateAttributesMapping<SelectPopupState> s_mapping = PopupStateMapping.CreateOpen<SelectPopupState>();
    private static readonly PopupOptions s_freeKeys = new() { Popup = new PopupElementOptions() };
    private static readonly PopupOptions s_stoppedKeys = new() { Popup = new PopupElementOptions { StopCompositeKeys = true } };

    private readonly TaskCompletionSource attached = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly EventCallback onKeyDown;
    private readonly Action positionerChanged;
    private SelectStore? store;
    private CompositeRootBehavior? list;
    private FocusManagerBehavior? focus;
    private PopupModuleAttachment? popupModule;
    private TransitionsTwin? transitions;
    private TwinHandle<TransitionsOptions>? transitionsHandle;
    private TransitionsOptions? sentTransitions;
    private Task? attaching;
    private IDisposable? boundaryPart;
    private IDisposable? retainRegistration;
    private StoreSelection<SelectStoreState, bool>? open;
    private StoreSelection<SelectStoreState, bool>? mounted;
    private StoreSelection<SelectStoreState, TransitionStatus?>? status;
    private StoreSelection<SelectStoreState, int>? unmountRequests;
    private StoreSelection<SelectStoreState, bool>? retainExit;
    private StoreSelection<SelectStoreState, string?>? rootId;
    private StoreSelection<SelectStoreState, bool>? hasList;
    private StoreSelection<SelectStoreState, bool>? multiple;
    private StoreSelection<SelectStoreState, bool>? readOnly;
    private StoreSelection<SelectStoreState, bool>? aligned;
    private StoreSelection<SelectStoreState, InteractionType?>? openMethod;
    private StoreSelection<SelectStoreState, OpenChangeRelay?>? relay;
    private StoreSelection<SelectStoreState, string?>? triggerId;
    private StoreSelection<SelectStoreState, EventSnapshot?>? closeEvent;
    private SelectPopupState? state;
    private RenderFragment? cascadedContent;
    private RenderFragment? cascade;
    private string? frozenStyle;
    private string? consumerStyle;
    private bool announcedRetain;
    private bool disposed;
    private EventSnapshot? finalResolvedFor;
    private FocusTargetValue? resolvedFinal;
    private bool warnedFinalFocus;

    /// <summary>Initializes a new instance of the <see cref="SelectPopup"/> class.</summary>
    public SelectPopup()
    {
        onKeyDown = CreateHandler<KeyboardEventArgs>("onkeydown", e => list!.HandleKeyDownAsync(e));
        positionerChanged = OnPositionerChanged;
    }

    [Inject]
    private IJSRuntime JSRuntime { get; set; } = null!;

    [Inject]
    private PartIdGenerator IdGenerator { get; set; } = null!;

    [CascadingParameter]
    private SelectStore? RootStore { get; set; }

    [CascadingParameter]
    private SelectPositionerContext? PositionerContext { get; set; }

    [CascadingParameter]
    private ListNavigationContext? ListContext { get; set; }

    [CascadingParameter]
    private PopupPortalContext? Portal { get; set; }

    [CascadingParameter]
    private ToolbarRootContext? ToolbarContext { get; set; }

    [CascadingParameter]
    private CspProviderContext? Csp { get; set; }

    // SEL-API-39
    /// <summary>Gets or sets where focus returns when the popup closes.</summary>
    /// <value>
    /// <see langword="false"/> to move no focus, <see langword="true"/> for the default, an element or an element's id;
    /// <see langword="null"/>, the default, for the trigger.
    /// </value>
    [Parameter]
    public FocusTargetValue? FinalFocus { get; set; }

    /// <summary>Gets or sets a function that chooses where focus returns when the popup closes, from how it was closed.</summary>
    /// <value>
    /// A function of the close interaction type that returns the target, <see langword="true"/> for the default, or
    /// <see langword="null"/> to move no focus; it wins over <see cref="FinalFocus"/>. The default is <see langword="null"/>.
    /// </value>
    [Parameter]
    public Func<InteractionType, FocusTargetValue?>? FinalFocusResolver { get; set; }

    /// <inheritdoc />
    protected override string DefaultTagName => "div";

    /// <inheritdoc />
    protected override SelectPopupState State
    {
        get
        {
            bool isOpen = open!.Value;
            TransitionStatus? current = status!.Value;
            SelectSide? side = PositionerContext!.Side;
            Align? align = PositionerContext.Align;
            if (state is null || state.Open != isOpen || state.TransitionStatus != current || state.Side != side || state.Align != align)
            {
                state = new SelectPopupState(isOpen, current, side, align);
            }

            return state;
        }
    }

    /// <inheritdoc />
    protected override StateAttributesMapping<SelectPopupState>? StateAttributesMapping => s_mapping;

    /// <summary>Gets a task that completes once the popup's transitions handle has attached the first time.</summary>
    internal Task TwinAttached => attached.Task;

    /// <summary>Gets a task that completes once the popup's list handle has attached the first time.</summary>
    internal Task ListAttached => list!.TwinAttached;

    /// <inheritdoc />
    public override Task SetParametersAsync(ParameterView parameters)
    {
        ChildContent = null;
        return base.SetParametersAsync(parameters);
    }

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">The popup is not inside a <see cref="SelectRoot{TValue}"/> and a <see cref="SelectPositioner"/>.</exception>
    protected override void OnInitialized()
    {
        store = SelectStore.Require(RootStore);
        SelectPositionerContext.Require(PositionerContext);
        list = new CompositeRootBehavior(JSRuntime, IdGenerator, StateHasChanged, DispatchExceptionAsync, onKeyDown) { ReportItems = true };
        list.ItemsChanged += OnItemsChanged;
        focus = new FocusManagerBehavior(JSRuntime, InvokeAsync, snapshot => store.RequestOpenAsync(false, new OpenChangeEventDetails(EventReason.FocusOut) { Event = snapshot }));
        popupModule = new PopupModuleAttachment(JSRuntime);
        boundaryPart = store.RegisterBoundaryPart();
        retainRegistration = store.OnUnmountPreventionChanging(AnnounceRetainExit);
        PositionerContext!.Positioning.AddListener(positionerChanged);
        base.OnInitialized();
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        store!.SetListboxConsumerId(list: false, ConsumerId());
        list!.SetList(ListContext, CompositeListRole.Floating);
        list.SetConsumerAttributes(AdditionalAttributes);
        if (cascade is null || !ReferenceEquals(cascadedContent, ChildContent))
        {
            cascadedContent = ChildContent;
            cascade = list.Context.Cascade(cascadedContent);
        }

        ChildContent = cascade;
        focus!.Portal = Portal;
        WarnBothFocusForms();
    }

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        focus!.SetInputs(FocusInputs());
        base.BuildRenderTree(builder);
    }

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        SelectStore current = store!;
        if (Element is { } element)
        {
            await PopupPortalContext.WhenMoved(Portal);
            if (disposed)
            {
                return;
            }

            SelectPositionerContext positionerContext = PositionerContext!;
            if (!string.Equals(positionerContext.PopupElement?.Id, element.Id, StringComparison.Ordinal))
            {
                positionerContext.PopupElement = element;
                _ = positionerContext.SyncAsync();
            }

            Task transitionsSync = SyncTransitionsAsync(element);
            Task moduleSync = popupModule!.SyncAsync(element, ToolbarContext is null ? s_freeKeys : s_stoppedKeys);
            Task listSync = list!.AfterRenderAsync(Enabled, element, Id);
            focus!.SetInputs(FocusInputs());
            await focus.AfterRenderAsync(element, Id);
            await listSync;
            current.PopupListAttached = !disposed && list.TwinAttached.IsCompletedSuccessfully;
            await moduleSync;
            await transitionsSync;
        }

        await current.AtBatchBoundaryAsync();
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            disposed = true;
            store?.SetListboxConsumerId(list: false, null);
            if (store is not null)
            {
                store.PopupListAttached = false;
            }

            PositionerContext?.Positioning.RemoveListener(positionerChanged);
            if (PositionerContext is { } positionerContext)
            {
                positionerContext.PopupElement = null;
            }

            boundaryPart?.Dispose();
            boundaryPart = null;
            retainRegistration?.Dispose();
            retainRegistration = null;
            if (list is not null)
            {
                list.ItemsChanged -= OnItemsChanged;
                list.Dispose();
            }

            if (transitions is not null)
            {
                _ = transitions.DisposeAsync().AsTask();
                transitions = null;
                transitionsHandle = null;
            }

            if (popupModule is not null)
            {
                _ = popupModule.DisposeAsync().AsTask();
            }

            if (focus is not null)
            {
                _ = focus.DisposeAsync().AsTask();
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
        unmountRequests = subscription.Select(SelectStoreState.UnmountRequests);
        retainExit = subscription.Select(SelectStoreState.PreventUnmountingOnClose);
        rootId = subscription.Select(SelectStoreState.RootIdSelector);
        hasList = subscription.Select(SelectStoreState.HasListSelector);
        multiple = subscription.Select(SelectStoreState.MultipleSelector);
        readOnly = subscription.Select(SelectStoreState.ReadOnlySelector);
        aligned = subscription.Select(SelectStoreState.AlignedActiveSelector);
        openMethod = subscription.Select(SelectStoreState.OpenMethod);
        relay = subscription.Select(SelectStoreState.OpenChange);
        triggerId = subscription.Select(SelectStoreState.TriggerIdSelector);
        closeEvent = subscription.Select(SelectStoreState.CloseEventSelector);
    }

    // SEL-DOM-28: the scrollbar style first, then the focus manager's guard.
    /// <inheritdoc />
    protected override void BuildBeforeElement(RenderTreeBuilder builder)
    {
        builder.OpenRegion(0);
        DisableScrollbarStyle.Render(builder, Csp);
        builder.CloseRegion();
        focus!.BuildBeforeElement(builder);
    }

    /// <inheritdoc />
    protected override void BuildAfterElement(RenderTreeBuilder builder) => focus!.BuildAfterElement(builder);

    /// <inheritdoc />
    protected override void AddPartAttributes(AttributeList attributes)
    {
        if (hasList!.Value)
        {
            attributes.Set("role", "presentation");
        }
        else
        {
            attributes.Set("role", "listbox");
            if (rootId!.Value is { } id)
            {
                attributes.Set("id", id + "-list");
            }

            if (multiple!.Value)
            {
                attributes.Set("aria-multiselectable", "true");
            }

            if (readOnly!.Value)
            {
                attributes.Set("aria-readonly", "true");
            }

            if (aligned!.Value)
            {
                attributes.Set(MergeProps.ClassAttribute, DisableScrollbarStyle.ClassName);
            }
        }

        attributes.Set("tabindex", "-1");
        attributes.Set(PopupMarkerAttributes.Focusable, string.Empty);
        list!.AddAttributes(attributes, Id);
    }

    // ADR 04 Amendments 2026-10-18 (handover at attach): before attach C# renders `transition: none` while starting
    // (SEL-DOM-30), then the modules own the style, the transitions module's starting band and the `select` module's
    // aligned band (SEL-DOM-27, SEL-DOM-29) over the consumer's.
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
            if (transitionsHandle is not null || attaching is not null)
            {
                frozenStyle = style;
            }

            return;
        }

        attributes.Set(MergeProps.StyleAttribute, frozenStyle);
    }

    /// <inheritdoc />
    protected override bool ForwardsConsumerHandler(string eventName) => list!.ForwardsConsumerHandler(eventName);

    private async Task SyncTransitionsAsync(ElementReference element)
    {
        if (disposed)
        {
            return;
        }

        if (transitions is null)
        {
            transitions = new TransitionsTwin(JSRuntime);
            transitions.StatusChanged += change => store?.MirrorStatus(change.Status);
            transitions.TransitionCompleted += opened => _ = InvokeAsync(() => store!.CompleteTransitionAsync(opened));
        }

        if (transitionsHandle is null)
        {
            attaching ??= AttachTransitionsAsync(element, transitions);
            await attaching;
        }
        else if (TransitionsOptions() is { } options && options != sentTransitions)
        {
            sentTransitions = options;
            await transitionsHandle.UpdateAsync(options);
        }
    }

    private TransitionsOptions TransitionsOptions() => new()
    {
        Open = open!.Value,
        Mounted = mounted!.Value,
        Status = status!.Value,
        ReportOpenComplete = true,
        OpenSelector = OpenSelector,
        UnmountRequests = unmountRequests!.Value,
        RetainExit = retainExit!.Value,
        DisableTransitionWhileStarting = true,
        Style = consumerStyle ?? string.Empty,
    };

    private void AnnounceRetainExit(bool retain)
    {
        if (transitions is null || Element is not { } element || announcedRetain == retain)
        {
            return;
        }

        announcedRetain = retain;
        _ = transitions.RetainExitAsync(element, retain).AsTask();
    }

    private async Task AttachTransitionsAsync(ElementReference element, TransitionsTwin twin)
    {
        await twin.PrepareAsync();
        if (disposed || transitions != twin)
        {
            return;
        }

        TransitionsOptions options = TransitionsOptions();
        sentTransitions = options;
        transitionsHandle = await twin.AttachAsync(element, options);
        attaching = null;
        attached.TrySetResult();
    }

    // SEL-STA-04..06, 12..14: the floating handle's order report reaches the store, which joins the item values to it.
    private void OnItemsChanged() => _ = InvokeAsync(() => store!.PublishOrderAsync(OrderedIds()));

    private IReadOnlyList<string> OrderedIds()
    {
        IReadOnlyList<CompositeItemBehavior> ordered = list!.OrderedItems;
        string[] ids = new string[ordered.Count];
        for (int i = 0; i < ids.Length; i++)
        {
            ids[i] = ordered[i].Registration.Id;
        }

        return ids;
    }

    // SEL-FOC-02..05, 12 (SelectPopup.tsx:479-486).
    private FocusManagerInputs FocusInputs()
    {
        InteractionType? method = openMethod!.Value;
        OpenChangeRelay? last = relay!.Value;
        bool isOpen = open!.Value;
        return new FocusManagerInputs
        {
            Open = isOpen,
            Modal = false,
            Disabled = !mounted!.Value,
            ReturnFocus = ResolveFinalFocus(isOpen ? null : closeEvent!.Value),
            RestoreFocus = FocusManagerTwin.RestoreTabbable,
            OpenInteractionType = method,
            CloseEvent = isOpen ? null : closeEvent!.Value,
            DomReferenceId = triggerId!.Value,
            Relay = last,
            SessionToken = store!.SessionToken,
        };
    }

    // ADR 08 Amendments 2026-10-28: the resolver wins, called once per committed close with the close interaction type
    // the focus manager classifies (FLT-FOC-10); SEL-EVT-17's item-press `''` is §16 Q5's known gap, replicated.
    private FocusTargetValue? ResolveFinalFocus(EventSnapshot? closeEvent)
    {
        if (FinalFocusResolver is { } resolver)
        {
            if (closeEvent is not { } snapshot)
            {
                return default(FocusTargetValue?);
            }

            if (!ReferenceEquals(finalResolvedFor, snapshot))
            {
                finalResolvedFor = snapshot;
                resolvedFinal = resolver(CloseTypeClassifier.Classify(snapshot, focus!.RecordedInteractionType)) ?? new FocusTargetValue(false);
            }

            return resolvedFinal;
        }

        return FinalFocus;
    }

    private void WarnBothFocusForms()
    {
        ILogger? logger = store!.Logger;
        if (logger is null || !DevelopmentWarnings.IsEnabled || warnedFinalFocus || FinalFocus is null || FinalFocusResolver is null)
        {
            return;
        }

        warnedFinalFocus = true;
        DevelopmentWarnings.Write(logger, "Blazix: `SelectPopup` received both `FinalFocus` and `FinalFocusResolver`; the resolver is used.");
    }

    // The Popup renders the Positioner's mirrored placement only when its state record changed.
    private void OnPositionerChanged()
    {
        SelectPopupState? previous = state;
        if (previous is null || previous != State)
        {
            StateHasChanged();
        }
    }

    private string? ConsumerId() => AdditionalAttributes is not null && AdditionalAttributes.TryGetValue("id", out object? supplied) && supplied is string text ? text : null;
}

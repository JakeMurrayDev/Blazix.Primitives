using Blazix.Primitives.Interop;
using Blazix.Primitives.Rendering;
using Blazix.Primitives.Store;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace Blazix.Primitives;

// COL-STA-07, §3.2 state object: the root's state, unchanged (collapsible/trigger/CollapsibleTrigger.tsx:31,50), so
// Disabled is the root's even when the trigger's own Disabled overrides it for the button (COL-DOM-05 as corrected
// 2026-10-21, COL-API-06). TransitionStatus is the root's, mirrored (ADR 04 §2).
/// <summary>Represents the state of a collapsible trigger, which the part exposes as data attributes and passes to its class and style functions.</summary>
/// <param name="Open">A value indicating whether the panel is open; projected as <c>data-panel-open</c> while open.</param>
/// <param name="Disabled">
/// A value indicating whether the collapsible ignores user interaction, as its root says; projected as
/// <c>data-disabled</c>. A trigger's own <c>Disabled</c> changes how its button behaves, not this value.
/// </param>
/// <param name="TransitionStatus">
/// The phase of the panel's enter or exit transition, or <see langword="null"/> while none is in progress; its
/// <c>data-starting-style</c> and <c>data-ending-style</c> attributes are written by the browser-side behavior.
/// </param>
[PartState]
public sealed partial record CollapsibleTriggerState(bool Open, bool Disabled, [property: MirroredField] TransitionStatus? TransitionStatus);

// collapsible/trigger/CollapsibleTriggerDataAttributes.ts (COL-DOM-04).
/// <summary>Provides the names of the data attributes a collapsible trigger renders.</summary>
public static class CollapsibleTriggerDataAttributes
{
    /// <summary>Represents the attribute present while the panel is open: <c>data-panel-open</c>.</summary>
    public const string PanelOpen = "data-panel-open";
}

// Base UI's Collapsible.Trigger (collapsible/trigger/CollapsibleTrigger.tsx); the contract is docs/components/collapsible.md
// (§3.2, §5.2, §5.4, §6). The button mechanism is ButtonBehavior (button-behavior.md §6.2: native = NativeButton,
// disabled = effective, focusableWhenDisabled unset, composite false), one ButtonTwin; the click requests !open with reason
// trigger-press and the click's snapshot (COL-PTR-01, COL-EVT-01/02, ADR 04 Amendments 2026-10-21: EventSnapshot.FromEventArgs).
// Its data-starting-style/data-ending-style follow the root's status through its own TransitionsTwin (openSelector
// [data-panel-open], ExternalExit), as the Root's do. aria-controls points at the registered or default panel id while open
// (COL-ARIA-02..04, COL-STR-04, §16 Q4: kept as Base UI does).
/// <summary>
/// Represents the button that opens and closes a collapsible's panel, Base UI's <c>Collapsible.Trigger</c>, rendered as
/// a <c>button</c>.
/// </summary>
public sealed class CollapsibleTrigger : PartBase<CollapsibleTriggerState>
{
    private const string DefaultTag = "button";
    private const string OpenAttributeSelector = "[data-panel-open]";

    private static readonly StateDataAttribute[] s_panelOpen = [new(CollapsibleTriggerDataAttributes.PanelOpen, string.Empty)];
    private static readonly StateAttributesMapping<CollapsibleTriggerState> s_mapping = new StateAttributesMapping<CollapsibleTriggerState>()
        .Map<bool>(nameof(CollapsibleTriggerState.Open), static open => open ? s_panelOpen : null);

    private readonly EventCallback onClick;
    private readonly EventCallback onKeyDown;
    private readonly EventCallback onKeyUp;
    private readonly TaskCompletionSource transitionsAttached = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private ButtonBehavior? button;
    private StoreSelection<CollapsibleRootStoreState, bool>? open;
    private StoreSelection<CollapsibleRootStoreState, bool>? rootDisabled;
    private StoreSelection<CollapsibleRootStoreState, TransitionStatus?>? status;
    private StoreSelection<CollapsibleRootStoreState, string?>? panelId;
    private StoreSelection<CollapsibleRootStoreState, int>? unmountRequests;
    private CollapsibleTriggerState? state;
    private TransitionsTwin? transitions;
    private TwinHandle<TransitionsOptions>? transitionsHandle;
    private int sentUnmountRequests;

    /// <summary>Initializes a new instance of the <see cref="CollapsibleTrigger"/> class.</summary>
    public CollapsibleTrigger()
    {
        onClick = CreateHandler<MouseEventArgs>("onclick", HandleClickAsync);
        onKeyDown = CreateHandler<KeyboardEventArgs>("onkeydown", e => button!.HandleKeyDownAsync(e));
        onKeyUp = CreateHandler<KeyboardEventArgs>("onkeyup", e => button!.HandleKeyUpAsync(e));
    }

    [Inject]
    private IJSRuntime JSRuntime { get; set; } = null!;

    [Inject]
    private ILogger<CollapsibleTrigger> Logger { get; set; } = null!;

    [Inject]
    private ButtonDevelopmentLog DevelopmentLog { get; set; } = null!;

    [Inject]
    private PartIdGenerator IdGenerator { get; set; } = null!;

    private bool EffectiveDisabled => Disabled ?? rootDisabled!.Value;

    [CascadingParameter]
    private CollapsibleRootContext? RootContext { get; set; }

    // COL-API-05
    /// <summary>Gets or sets a value indicating whether the rendered element is a native <c>button</c>.</summary>
    /// <value>
    /// <see langword="true"/>, the default, for a <c>button</c> element; <see langword="false"/> when
    /// <see cref="PartBase{TState}.Render"/> supplies another element, which then gets <c>role="button"</c>, a
    /// <c>tabindex</c>, and clicks from Enter and Space.
    /// </value>
    [Parameter]
    public bool NativeButton { get; set; } = true;

    // COL-API-06
    /// <summary>Gets or sets a value indicating whether this trigger ignores user interaction.</summary>
    /// <value>
    /// <see langword="true"/> or <see langword="false"/> to override the root's <c>Disabled</c> for this trigger;
    /// <see langword="null"/>, the default, to follow the root.
    /// </value>
    [Parameter]
    public bool? Disabled { get; set; }

    /// <inheritdoc />
    protected override string DefaultTagName => DefaultTag;

    /// <inheritdoc />
    protected override CollapsibleTriggerState State
    {
        get
        {
            bool disabled = rootDisabled!.Value;
            if (state is null || state.Open != open!.Value || state.Disabled != disabled || state.TransitionStatus != status!.Value)
            {
                state = new CollapsibleTriggerState(open!.Value, disabled, status!.Value);
            }

            return state;
        }
    }

    /// <inheritdoc />
    protected override StateAttributesMapping<CollapsibleTriggerState>? StateAttributesMapping => s_mapping;

    // ADR 02 decision 1: under a substitute callback the twins find the element by id.
    /// <inheritdoc />
    protected override bool UsesGeneratedId => RenderFunction is not null;

    // For the fixtures: completes once both behaviors are attached to the trigger's element.
    /// <summary>Gets a task that completes when the trigger's behaviors are attached to its element.</summary>
    /// <value>A task that completes after the first interactive render has attached them.</value>
    internal Task TwinAttached => Task.WhenAll(button!.TwinAttached, transitionsAttached.Task);

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">The trigger is not inside a <see cref="CollapsibleRoot"/>.</exception>
    protected override void OnInitialized()
    {
        if (RootContext is null)
        {
            throw CollapsiblePanel.MissingRoot();
        }

        button = new ButtonBehavior(JSRuntime, Logger, DevelopmentLog, IdGenerator, StateHasChanged, DispatchExceptionAsync, onKeyDown, onKeyUp);
        base.OnInitialized();
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        button!.Native = NativeButton;
        button.FocusableWhenDisabled = null;
        button.SetConsumerAttributes(AdditionalAttributes);
    }

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        button!.Disabled = EffectiveDisabled;
        await button.AfterRenderAsync(Enabled, Element, Id, composite: false);
        if (!Enabled)
        {
            if (transitionsHandle is { } released)
            {
                transitionsHandle = null;
                await released.DisposeAsync();
            }

            return;
        }

        CollapsibleRootStoreState shared = RootContext!.State;
        if (transitionsHandle is not null)
        {
            if (shared.UnmountRequests != sentUnmountRequests)
            {
                sentUnmountRequests = shared.UnmountRequests;
                await transitionsHandle.UpdateAsync(TransitionsOptionsFor(shared));
            }

            return;
        }

        string? elementId = Element is null ? Id ?? button.LateId : null;
        if (Element is null && elementId is null)
        {
            return;
        }

        transitions ??= new TransitionsTwin(JSRuntime);
        sentUnmountRequests = shared.UnmountRequests;
        TransitionsOptions options = TransitionsOptionsFor(shared);
        transitionsHandle = elementId is null
            ? await transitions.AttachAsync(Element!.Value, options)
            : await transitions.AttachAsync(elementId, options);
        transitionsAttached.TrySetResult();
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            button?.Dispose();
            if (transitions is not null)
            {
                _ = transitions.DisposeAsync().AsTask();
                transitions = null;
                transitionsHandle = null;
            }
        }

        base.Dispose(disposing);
    }

    /// <inheritdoc />
    protected override void SubscribeSelectors()
    {
        SelectorSubscription<CollapsibleRootStoreState> subscription = SubscribeTo(RootContext!);
        open = subscription.Select(CollapsibleRootStoreState.OpenSelector);
        rootDisabled = subscription.Select(CollapsibleRootStoreState.DisabledSelector);
        status = subscription.Select(CollapsibleRootStoreState.StatusSelector);
        panelId = subscription.Select(CollapsibleRootStoreState.PanelIdSelector);
        unmountRequests = subscription.Select(CollapsibleRootStoreState.UnmountRequestsSelector);
    }

    /// <inheritdoc />
    protected override void AddPartAttributes(AttributeList attributes)
    {
        // The gate reads the effective state of this render (the root's Disabled can change without a parameter set).
        button!.Disabled = EffectiveDisabled;
        bool isOpen = open!.Value;
        if (isOpen && panelId!.Value is { } controls)
        {
            attributes.Set("aria-controls", controls);
        }

        attributes.Set("aria-expanded", isOpen ? "true" : "false");
        attributes.Set("onclick", onClick);
        button.AddAttributes(attributes, Id, composite: false);
    }

    /// <inheritdoc />
    protected override bool ForwardsConsumerHandler(string eventName)
    {
        return button!.ForwardsConsumerHandler(eventName);
    }

    // The values the trigger's element was rendered with: like the root's, it never renders the two attributes itself.
    private static TransitionsOptions TransitionsOptionsFor(CollapsibleRootStoreState shared)
    {
        return new TransitionsOptions
        {
            Open = shared.Open,
            Mounted = shared.Mounted,
            Status = shared.TransitionStatus is TransitionStatus.Starting or TransitionStatus.Ending ? null : shared.TransitionStatus,
            EnableIdleState = true,
            DeferEndingState = true,
            KeepMounted = true,
            ExternalExit = true,
            UnmountRequests = shared.UnmountRequests,
            OpenSelector = OpenAttributeSelector,
        };
    }

    // COL-PTR-01/02, COL-EVT-01/02/03/07: a disabled trigger requests nothing; the request carries no trigger element.
    private Task HandleClickAsync(MouseEventArgs e)
    {
        if (EffectiveDisabled)
        {
            return Task.CompletedTask;
        }

        CollapsibleRootContext root = RootContext!;
        return root.RequestOpenAsync(!root.State.Open, new ChangeEventDetails(EventReason.TriggerPress, EventSnapshot.FromEventArgs(e)));
    }
}

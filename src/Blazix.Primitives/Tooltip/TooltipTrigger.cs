using Blazix.Primitives.Interop;
using Blazix.Primitives.Rendering;
using Blazix.Primitives.Store;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Blazix.Primitives;

// TIP §3.3 (tooltip/trigger/TooltipTrigger.tsx:277-282).
/// <summary>Represents the state of a tooltip trigger, which the part exposes as data attributes and passes to its class and style functions.</summary>
/// <param name="Open">Whether the tooltip is open and anchored to this trigger; rendered as <c>data-popup-open</c>.</param>
[PartState]
public sealed partial record TooltipTriggerState(bool Open);

// TIP §3.3; tooltip/trigger/TooltipTriggerDataAttributes.ts, in its order.
/// <summary>Provides the names of the data attributes a tooltip trigger renders.</summary>
public static class TooltipTriggerDataAttributes
{
    /// <summary>Represents the attribute present while the tooltip is open and anchored to the trigger: <c>data-popup-open</c>.</summary>
    public const string PopupOpen = PopupTriggerDataAttributes.PopupOpen;

    /// <summary>Represents the attribute present while the trigger is disabled: <c>data-trigger-disabled</c>.</summary>
    public const string TriggerDisabled = "data-trigger-disabled";
}

// Base UI's Tooltip.Trigger (tooltip/trigger/TooltipTrigger.tsx); the contract is docs/components/tooltip.md §3.3, §5.1, §6.
// - The popup half is PopupTriggerBehavior (M4 track 1): registration by id at parameter set, the store from the handle or
//   the root, the payload forwarded while active. The id is the bag `id`, else the generated one, rendered on the trigger's
//   own element (ADR 08 Amendments 2026-10-27; issue 115). Closing state forwarded with the payload (TIP-STA-09):
//   CloseOnClick and the close delay without the provider's fallback (TIP §16 Q16, replicated, ADR 04 Amendments
//   2026-10-18).
// - The browser half is one `interactions` trigger handle (M4 track 3: InteractionsTriggerBehavior): hover with the rest
//   timer, the safe polygon and nested-trigger detection, focus-to-open, the reference press and the press cancel, cursor
//   anchoring; every request reaches the store with the delay group's phase (ADR 04 Amendments 2026-10-27). The delays
//   are resolved here (`Delay ?? Provider ?? 600`, `CloseDelay ?? Provider ?? 0`; issue 121), so the trigger renders
//   again when its provider's delays change. The handle attaches to the trigger's own element, or by the id it renders
//   under a substitute without one (design point 13.1 (a) of M4 track 6, settled by the orchestrator 2026-10-28).
// - No ButtonBehavior (TIP §16 Q10), no aria-* (TIP-ARIA-02/05), never the native `disabled` (TIP-API-18). The consumer's
//   handlers pass through ungated (TIP-DOM-05).
// - The Root's interaction bag (TIP-DOM-05 bag (3), TIP-STA-15) is the reference press and the cursor anchor, carried while
//   this trigger owns the mounted tooltip or the root tracks the pointer (TooltipTrigger.tsx:216-219), so a bystander
//   trigger renders nothing on an open, a close or a hand-off (TIP-EDGE-11).
/// <summary>
/// Opens a tooltip on hover or keyboard focus: Base UI's <c>Tooltip.Trigger</c>, rendered as a <c>button</c>.
/// </summary>
/// <typeparam name="TPayload">The type of the payload the tooltip's triggers carry.</typeparam>
/// <remarks>
/// <para>
/// The trigger must be placed within a <see cref="TooltipRoot{TPayload}"/> or be given a <see cref="Handle"/>. Its
/// <c>id</c>, or a generated one, is its trigger id.
/// </para>
/// <para>
/// A trigger substituted with a component that renders the <c>id</c> it receives on an inner element is not found by its
/// browser-side behavior: give the component's root element the <c>id</c>, or substitute an element.
/// </para>
/// </remarks>
[CascadingTypeParameter(nameof(TPayload))]
public sealed class TooltipTrigger<TPayload> : PartBase<TooltipTriggerState>
{
    // TooltipTrigger.tsx:31.
    private const string TriggerMarker = "data-base-ui-tooltip-trigger";

    private static readonly StateAttributesMapping<TooltipTriggerState> s_mapping = PopupStateMapping.CreateTrigger<TooltipTriggerState>();
    private static readonly InteractionsFocusOptions s_focus = new();
    private static readonly InteractionsClientPointOptions s_clientPointX = new("x");
    private static readonly InteractionsClientPointOptions s_clientPointY = new("y");
    private static readonly InteractionsClientPointOptions s_clientPointBoth = new("both");

    private readonly PopupTriggerBehavior<TooltipStore<TPayload>, TooltipStoreState<TPayload>, TPayload> popup;
    private readonly Action providerChanged;
    private InteractionsTriggerBehavior? interactions;
    private TooltipProviderContext? subscribedProvider;
    private TooltipTriggerState state = new(false);
    private bool payloadSupplied;
    private bool forwarded;
    private OptionInputs builtInputs;
    private InteractionsTriggerOptions? builtOptions;

    /// <summary>Initializes a new instance of the <see cref="TooltipTrigger{TPayload}"/> class.</summary>
    public TooltipTrigger()
    {
        popup = new(this, "TooltipTrigger", StateHasChanged, AddSelections);
        providerChanged = StateHasChanged;
    }

    [Inject]
    private IJSRuntime JSRuntime { get; set; } = null!;

    private bool EffectiveDisabled => Disabled ?? popup.Store.Popup.Disabled;

    [CascadingParameter]
    private TooltipStore<TPayload>? CascadedStore { get; set; }

    [CascadingParameter]
    private TooltipProviderContext? Provider { get; set; }

    /// <summary>Gets or sets the handle that connects this trigger to a root rendered elsewhere.</summary>
    /// <value>The root's <see cref="TooltipHandle{TPayload}"/>, or <see langword="null"/> to use the enclosing root.</value>
    [Parameter]
    public TooltipHandle<TPayload>? Handle { get; set; }

    /// <summary>Gets or sets the payload the root's <c>PayloadContent</c> receives while this trigger is active.</summary>
    /// <value>The payload; when omitted, the root receives the default value.</value>
    [Parameter]
    public TPayload? Payload { get; set; }

    /// <summary>Gets or sets how long to wait before opening the tooltip on hover.</summary>
    /// <value>
    /// The delay, or <see langword="null"/> (the default) for the enclosing <see cref="TooltipProvider"/>'s, else 600
    /// milliseconds. While the provider's group is active the tooltip opens at once.
    /// </value>
    [Parameter]
    public Duration? Delay { get; set; }

    /// <summary>Gets or sets how long to wait before closing the tooltip after the pointer leaves this trigger.</summary>
    /// <value>
    /// The delay, or <see langword="null"/> (the default) for the enclosing <see cref="TooltipProvider"/>'s, else none.
    /// Leaving the popup uses this trigger's own value, without the provider's.
    /// </value>
    [Parameter]
    public Duration? CloseDelay { get; set; }

    /// <summary>Gets or sets a value indicating whether a press of this trigger closes the tooltip and cancels a pending open.</summary>
    /// <value><see langword="true"/> (the default) to close on press.</value>
    [Parameter]
    public bool CloseOnClick { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether this trigger is disabled.</summary>
    /// <value>
    /// <see langword="true"/> to keep the tooltip from opening from this trigger; <see langword="false"/> to enable it in
    /// a disabled root; <see langword="null"/> (the default) to follow the root. The native <c>disabled</c> attribute is
    /// not set.
    /// </value>
    [Parameter]
    public bool? Disabled { get; set; }

    /// <inheritdoc />
    protected override string DefaultTagName => "button";

    /// <inheritdoc />
    protected override bool UsesGeneratedId => true;

    // The pipeline reads the state before the part's attributes, so the record follows the store here.
    /// <inheritdoc />
    protected override TooltipTriggerState State
    {
        get
        {
            bool open = popup.IsOpenedByTrigger;
            if (state.Open != open)
            {
                state = new TooltipTriggerState(open);
            }

            return state;
        }
    }

    /// <inheritdoc />
    protected override StateAttributesMapping<TooltipTriggerState>? StateAttributesMapping => s_mapping;

    // For the fixtures (tests/Blazix.Primitives.Fixtures.Client).
    /// <summary>Gets a task that completes when the trigger's interactions handle is attached, or at once while no root is attached to its handle.</summary>
    internal Task TwinAttached => interactions is null || popup.Store.SessionToken is null ? Task.CompletedTask : interactions.TwinAttached;

    /// <inheritdoc />
    public override Task SetParametersAsync(ParameterView parameters)
    {
        // An unconstrained TPayload? is TPayload for a value type, so an omitted payload is told by its absence (issue 116).
        payloadSupplied = parameters.TryGetValue<TPayload>(nameof(Payload), out _);
        return base.SetParametersAsync(parameters);
    }

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        interactions = new InteractionsTriggerBehavior(JSRuntime, RequestAsync, DispatchExceptionAsync, default);
        base.OnInitialized();
    }

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">The trigger is placed outside a <see cref="TooltipRoot{TPayload}"/> and has no <see cref="Handle"/>.</exception>
    protected override void OnParametersSet()
    {
        if (!ReferenceEquals(subscribedProvider, Provider))
        {
            subscribedProvider?.RemoveListener(providerChanged);
            subscribedProvider = Provider;
            subscribedProvider?.AddListener(providerChanged);
        }

        // TIP-EDGE-01 (TooltipTrigger.tsx:62-69), in every build.
        popup.Apply(CascadedStore, Handle, Id, Element, payloadSupplied, Payload, "Blazix: `<TooltipTrigger>` must be either used within a `<TooltipRoot>` component or provided with a handle.");
        interactions!.SetConsumerAttributes(AdditionalAttributes);
    }

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        TooltipStore<TPayload> store = popup.Store;
        Task sync = Task.CompletedTask;
        if (store.SessionToken is { } token)
        {
            interactions!.SessionToken = token;
            interactions.Options = BuildOptions(store);
            sync = interactions.AfterRenderAsync(Enabled, Element, Id);
        }

        ForwardState(store);
        await popup.OnAfterRenderAsync(Element);
        await sync;
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            subscribedProvider?.RemoveListener(providerChanged);
            subscribedProvider = null;
            popup.Dispose();
            if (interactions is not null)
            {
                _ = interactions.DisposeAsync().AsTask();
            }
        }

        base.Dispose(disposing);
    }

    /// <inheritdoc />
    protected override void AddPartAttributes(AttributeList attributes)
    {
        // TIP-DOM-03/04 (TooltipTrigger.tsx:260-261): from the effective disabled state, merged before the consumer's.
        if (EffectiveDisabled)
        {
            attributes.Set(TooltipTriggerDataAttributes.TriggerDisabled, string.Empty);
        }
        else
        {
            attributes.Set(TriggerMarker, string.Empty);
        }
    }

    private static void AddSelections(SelectorSubscription<TooltipStoreState<TPayload>> subscription)
    {
        // The root's inputs the trigger renders or sends; none of them moves on an open, a close or a hand-off.
        subscription.Select(TooltipSelectors<TPayload>.Disabled);
        subscription.Select(TooltipSelectors<TPayload>.TrackCursorAxis);
        subscription.Select(TooltipSelectors<TPayload>.DisableHoverablePopup);
    }

    private static InteractionsClientPointOptions ClientPoint(TooltipTrackCursorAxis axis) => axis switch
    {
        TooltipTrackCursorAxis.X => s_clientPointX,
        TooltipTrackCursorAxis.Y => s_clientPointY,
        _ => s_clientPointBoth,
    };

    // TooltipTrigger.tsx:146-166 (hover and focus, enabled by the trigger's own disabled state) and TooltipRoot.tsx:249-282
    // with :216-219 (the reference press and the cursor anchor, enabled by the root's). ADR 16 rule 7: the options are built
    // only when one of their inputs changed, so an unchanged render allocates none.
    private InteractionsTriggerOptions BuildOptions(TooltipStore<TPayload> store)
    {
        TooltipStoreState<TPayload> current = store.State;
        OptionInputs inputs = new(
            Id,
            CloseOnClick,
            EffectiveDisabled,
            current.Popup.Disabled,
            current.TrackCursorAxis,
            current.DisableHoverablePopup,
            popup.IsMountedByTrigger,
            TooltipDelays.Open(Delay, Provider),
            TooltipDelays.Close(CloseDelay, Provider),
            Provider?.Token);
        if (builtOptions is not null && inputs == builtInputs)
        {
            return builtOptions;
        }

        bool tracking = !inputs.RootDisabled && inputs.Axis != TooltipTrackCursorAxis.None;
        builtInputs = inputs;
        builtOptions = new InteractionsTriggerOptions
        {
            TriggerId = inputs.Id,
            ReferencePress = inputs.CloseOnClick && !inputs.RootDisabled && (inputs.MountedByTrigger || inputs.Axis != TooltipTrackCursorAxis.None),
            CancelPendingOpenOnPress = inputs.CloseOnClick,
            Hover = inputs.Disabled ? null : new InteractionsHoverOptions
            {
                RestMs = inputs.OpenDelay,
                DelayClose = inputs.CloseDelay,
                Move = false,
                MouseOnly = true,
                SafePolygon = !inputs.DisableHoverablePopup && inputs.Axis != TooltipTrackCursorAxis.Both,
                NestedTooltipTriggers = true,
                DelayGroupToken = inputs.ProviderToken,
            },
            Focus = inputs.Disabled ? null : s_focus,
            ClientPoint = tracking ? ClientPoint(inputs.Axis) : null,
        };
        return builtOptions;
    }

    // TIP-STA-09 (useTriggerDataForwarding): written when this trigger comes to own the mounted tooltip and whenever its
    // values change while it does.
    private void ForwardState(TooltipStore<TPayload> store)
    {
        if (!popup.IsMountedByTrigger)
        {
            forwarded = false;
            return;
        }

        TooltipStoreState<TPayload> current = store.State;
        int closeDelay = TooltipDelays.Forwarded(CloseDelay);
        if (!forwarded || current.CloseOnClick != CloseOnClick || current.CloseDelay != closeDelay)
        {
            forwarded = true;
            store.SetForwardedState(CloseOnClick, closeDelay);
        }
    }

    private Task RequestAsync(OpenChangeRequest request) => popup.Store.RequestAsync(request);

    private readonly record struct OptionInputs(
        string? Id,
        bool CloseOnClick,
        bool Disabled,
        bool RootDisabled,
        TooltipTrackCursorAxis Axis,
        bool DisableHoverablePopup,
        bool MountedByTrigger,
        int OpenDelay,
        int CloseDelay,
        string? ProviderToken);
}

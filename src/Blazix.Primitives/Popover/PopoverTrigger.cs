using Blazix.Primitives.Interop;
using Blazix.Primitives.Rendering;
using Blazix.Primitives.Utilities;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace Blazix.Primitives;

// POP-STA-13/14, §3.2 state object (popover/trigger/PopoverTrigger.tsx:127-130, 179-188).
/// <summary>Represents the state of a popover trigger, which the part exposes as data attributes and passes to its class and style functions.</summary>
/// <param name="Disabled">A value indicating whether the trigger ignores user interaction; projected as <c>data-disabled</c>.</param>
/// <param name="Open">
/// A value indicating whether the popover is open and this trigger owns it; projected as <c>data-popup-open</c>, with
/// <c>data-pressed</c> when a press opened it.
/// </param>
[PartState]
public sealed partial record PopoverTriggerState(bool Disabled, bool Open);

// popover/trigger/PopoverTriggerDataAttributes.ts (POP-DOM-01..03).
/// <summary>Provides the names of the data attributes a popover trigger renders.</summary>
public static class PopoverTriggerDataAttributes
{
    /// <summary>Represents the attribute present while the popover is open and this trigger owns it: <c>data-popup-open</c>.</summary>
    public const string PopupOpen = PopupTriggerDataAttributes.PopupOpen;

    /// <summary>Represents the attribute present while a press on this trigger opened the popover: <c>data-pressed</c>.</summary>
    public const string Pressed = PopupTriggerDataAttributes.Pressed;

    /// <summary>Represents the attribute present while the trigger is disabled: <c>data-disabled</c>.</summary>
    public const string Disabled = "data-disabled";
}

// Base UI's Popover.Trigger (popover/trigger/PopoverTrigger.tsx); the contract is docs/components/popover.md §3.2, §5.1,
// §6.1, §6.2. Generic with the Root (ADR 08 Amendments 2026-10-27, issue 116's finding).
// - The store: the handle's, else the Root's, through PopupTriggerBehavior (registration by id at parameter set; the id is
//   the one rendered on the Trigger's own element, the lesson of issue 115). The active mounted trigger forwards Disabled,
//   OpenOnHover and CloseDelay (POP-STA-05) beside the payload.
// - The button mechanism (ButtonBehavior { Disabled, Native = NativeButton }; `activate` stays the click veto) and the
//   `interactions` trigger handle (ADR 04 and 01 Amendments 2026-10-27): `press` toggles with the patient-click window
//   (the module's), the hover in track 3's names (rest Delay, close CloseDelay, mouse only, safePolygon) while
//   !Disabled && OpenOnHover && !isTouchPressOpen (POP §11).
// - data-pressed beside data-popup-open from the store's reason (POP-DOM-02): added in AddPartAttributes, so the state
//   record stays POP-STA-13's (ADR 02 Amendments 2026-10-26).
// - The focus guards (POP-STR-14, POP-DOM-08) render before and after the element iff hasTriggerFocusGuards; the element
//   is never replaced. Their listeners are the `popup` module's.
/// <summary>
/// Represents a button that opens a popover, Base UI's <c>Popover.Trigger</c>, rendered as a <c>button</c>.
/// </summary>
/// <typeparam name="TPayload">The type of the payload this trigger carries; inferred from its root or <see cref="Handle"/>.</typeparam>
/// <remarks>
/// A trigger inside a <see cref="PopoverRoot{TPayload}"/> belongs to it; a trigger with a <see cref="Handle"/> belongs to
/// the root that uses the same handle, wherever it is rendered. The trigger's <c>id</c>, given or generated, is the id
/// <see cref="PopoverRoot{TPayload}.TriggerId"/> and <see cref="PopupHandle{TPayload}.OpenAsync"/> name.
/// </remarks>
[CascadingTypeParameter(nameof(TPayload))]
public sealed class PopoverTrigger<TPayload> : PartBase<PopoverTriggerState>, IPopoverTriggerPart
{
    // POP-OPEN_DELAY (popover/utils/constants.ts:1).
    private const int OpenDelay = 300;
    private const string MissingRootMessage = "Blazix: `<PopoverTrigger>` must be either used within a `<PopoverRoot>` component or provided with a handle.";

    private static readonly StateAttributesMapping<PopoverTriggerState> s_mapping = PopupStateMapping.CreateTrigger<PopoverTriggerState>();
    private static readonly InteractionsClickOptions s_click = new() { PatientClick = true };

    private readonly PopupTriggerBehavior<PopoverStore<TPayload>, PopoverStoreState<TPayload>, TPayload> behavior;
    private readonly EventCallback onClick;
    private readonly EventCallback onKeyDown;
    private readonly EventCallback onKeyUp;
    private readonly Action<ElementReference> capturePreGuard;
    private readonly Action<ElementReference> captureFocusTarget;
    private ButtonBehavior? button;
    private InteractionsTriggerBehavior? interactions;
    private PopupModuleAttachment? guards;
    private StoreSelectionHolder selections;
    private PopoverTriggerState state = new(false, false);
    private bool payloadSupplied;
    private ElementReference? preGuard;
    private ElementReference? focusTarget;

    /// <summary>Initializes a new instance of the <see cref="PopoverTrigger{TPayload}"/> class.</summary>
    public PopoverTrigger()
    {
        behavior = new(this, "PopoverTrigger", StateHasChanged, AddSelections);
        onClick = CreateHandler<MouseEventArgs>("onclick", e => interactions!.HandleClickAsync(e));
        onKeyDown = CreateHandler<KeyboardEventArgs>("onkeydown", e => button!.HandleKeyDownAsync(e));
        onKeyUp = CreateHandler<KeyboardEventArgs>("onkeyup", e => button!.HandleKeyUpAsync(e));
        capturePreGuard = element => preGuard = element;
        captureFocusTarget = element => focusTarget = element;
    }

    private int CloseDelayMilliseconds => (int)(CloseDelay?.ToMilliseconds() ?? 0);

    [Inject]
    private IJSRuntime JSRuntime { get; set; } = null!;

    [Inject]
    private ILogger<PopoverTrigger<TPayload>> Logger { get; set; } = null!;

    [Inject]
    private ButtonDevelopmentLog DevelopmentLog { get; set; } = null!;

    [Inject]
    private PartIdGenerator IdGenerator { get; set; } = null!;

    [CascadingParameter]
    private PopoverStore<TPayload>? RootStore { get; set; }

    // POP-API-11
    /// <summary>Gets or sets a value indicating whether the rendered element is a native <c>button</c>.</summary>
    /// <value>
    /// <see langword="true"/>, the default, for a <c>button</c> element; <see langword="false"/> when
    /// <see cref="PartBase{TState}.Render"/> supplies another element, which then gets <c>role="button"</c>, a
    /// <c>tabindex</c>, and clicks from Enter and Space.
    /// </value>
    [Parameter]
    public bool NativeButton { get; set; } = true;

    // POP-API-12
    /// <summary>Gets or sets a value indicating whether the trigger ignores user interaction.</summary>
    /// <value><see langword="false"/> by default.</value>
    [Parameter]
    public bool Disabled { get; set; }

    // POP-API-13
    /// <summary>Gets or sets the handle that associates this trigger with the root that uses the same handle.</summary>
    /// <value>A <see cref="PopoverHandle{TPayload}"/>, or <see langword="null"/> for a trigger inside its root.</value>
    [Parameter]
    public PopoverHandle<TPayload>? Handle { get; set; }

    // POP-API-14
    /// <summary>Gets or sets the payload the popover receives while this trigger is active.</summary>
    /// <value>The payload, which the root's <c>PayloadContent</c> receives; when the parameter is not supplied, none.</value>
    [Parameter]
    public TPayload? Payload { get; set; }

    // POP-API-16
    /// <summary>Gets or sets a value indicating whether hovering the trigger also opens the popover.</summary>
    /// <value><see langword="false"/> by default.</value>
    [Parameter]
    public bool OpenOnHover { get; set; }

    // POP-API-17
    /// <summary>Gets or sets how long the pointer must rest on the trigger before a hover opens the popover.</summary>
    /// <value>A duration; <see langword="null"/>, the default, means 300 milliseconds. Needs <see cref="OpenOnHover"/>.</value>
    [Parameter]
    public Duration? Delay { get; set; }

    // POP-API-18
    /// <summary>Gets or sets how long to wait before closing a popover that a hover opened.</summary>
    /// <value>A duration; <see langword="null"/>, the default, means no delay. Needs <see cref="OpenOnHover"/>.</value>
    [Parameter]
    public Duration? CloseDelay { get; set; }

    /// <inheritdoc />
    string? IPopoverTriggerPart.TriggerId => Id;

    /// <inheritdoc />
    protected override string DefaultTagName => "button";

    // The pipeline reads the state before the part's attributes, so the record follows the store here.
    /// <inheritdoc />
    protected override PopoverTriggerState State
    {
        get
        {
            bool open = behavior.IsOpenedByTrigger;
            if (state.Open != open || state.Disabled != Disabled)
            {
                state = new PopoverTriggerState(Disabled, open);
            }

            return state;
        }
    }

    /// <inheritdoc />
    protected override StateAttributesMapping<PopoverTriggerState>? StateAttributesMapping => s_mapping;

    /// <inheritdoc />
    protected override bool UsesGeneratedId => true;

    /// <inheritdoc />
    ElementReference? IPopoverTriggerPart.PreGuard => preGuard;

    /// <inheritdoc />
    ElementReference? IPopoverTriggerPart.FocusTarget => focusTarget;

    /// <summary>Gets a task that completes when the trigger's behaviors are attached.</summary>
    internal Task TwinsAttached => Task.WhenAll(button!.TwinAttached, interactions!.TwinAttached);

    /// <inheritdoc />
    public override Task SetParametersAsync(ParameterView parameters)
    {
        payloadSupplied = parameters.TryGetValue<TPayload>(nameof(Payload), out _);
        return base.SetParametersAsync(parameters);
    }

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        button = new ButtonBehavior(JSRuntime, Logger, DevelopmentLog, IdGenerator, StateHasChanged, DispatchExceptionAsync, onKeyDown, onKeyUp);
        interactions = new InteractionsTriggerBehavior(JSRuntime, RouteAsync, DispatchExceptionAsync, onClick);
        guards = new PopupModuleAttachment(JSRuntime);
        guards.Twin.GuardFocused += OnGuardFocused;
        base.OnInitialized();
    }

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">The trigger is neither inside a <see cref="PopoverRoot{TPayload}"/> nor given a <see cref="Handle"/>.</exception>
    protected override void OnParametersSet()
    {
        behavior.Apply(RootStore, Handle, Id, Element, payloadSupplied, Payload, MissingRootMessage);
        selections.SetArgs(Id, OpenOnHover);
        PopoverStore<TPayload> store = behavior.Store;
        if (store.Popup.IsTriggerActive(Id) && store.Popup.EffectiveOpen)
        {
            // POP-STA-05 at registration: a trigger that registers while it owns the open popover forwards its values.
            store.ForwardTriggerState(Disabled, OpenOnHover, CloseDelayMilliseconds);
        }

        button!.Native = NativeButton;
        button.Disabled = Disabled;
        button.SetConsumerAttributes(AdditionalAttributes);
        interactions!.SetConsumerAttributes(AdditionalAttributes);
        SyncInteractions();
    }

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        await behavior.OnAfterRenderAsync(Element);
        if (behavior.IsMountedByTrigger)
        {
            behavior.Store.ForwardTriggerState(Disabled, OpenOnHover, CloseDelayMilliseconds);
        }

        SyncInteractions();

        // Both handles start attaching in the same render (the FixtureInteractionsTrigger shape).
        Task interactionsSync = interactions!.AfterRenderAsync(Enabled, Element, Id);
        await button!.AfterRenderAsync(Enabled, Element, Id, composite: false);
        await interactionsSync;

        // Design point 5 of M4 track 5: the `popup` module's Trigger role listens on the guards, attached the first time
        // they render and kept after, so a trigger that never owned the open popover costs nothing.
        if (Element is { } element && (selections.HasFocusGuards || guards!.Attached.IsCompleted))
        {
            PopoverStore<TPayload> store = behavior.Store;
            await guards!.SyncAsync(element, new PopupOptions
            {
                SessionToken = store.SessionToken,
                Trigger = new PopupTriggerOptions { Guards = selections.HasFocusGuards, PopupId = ((IPopoverState)store.State).PopupId },
            });
        }
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            behavior.Dispose();
            button?.Dispose();
            if (interactions is not null)
            {
                _ = interactions.DisposeAsync().AsTask();
            }

            if (guards is not null)
            {
                guards.Twin.GuardFocused -= OnGuardFocused;
                _ = guards.DisposeAsync().AsTask();
            }
        }

        base.Dispose(disposing);
    }

    /// <inheritdoc />
    protected override void AddPartAttributes(AttributeList attributes)
    {
        button!.Disabled = Disabled;
        attributes.Set(PopupMarkerAttributes.ClickTrigger, string.Empty);
        behavior.AddAttributes(attributes, ariaExpanded: true, ariaHasPopup: "dialog");
        if (selections.PressOpened)
        {
            attributes.Set(PopupTriggerDataAttributes.Pressed, string.Empty);
        }

        attributes.Set("onclick", onClick);
        button.AddAttributes(attributes, Id, composite: false);
        interactions!.AddAttributes(attributes);
    }

    /// <inheritdoc />
    protected override bool ForwardsConsumerHandler(string eventName) => button!.ForwardsConsumerHandler(eventName) && interactions!.ForwardsConsumerHandler(eventName);

    // POP-STR-14, POP-DOM-08: the pre-guard.
    /// <inheritdoc />
    protected override void BuildBeforeElement(RenderTreeBuilder builder)
    {
        if (selections.HasFocusGuards)
        {
            RenderGuard(builder, capturePreGuard);
        }
        else
        {
            preGuard = null;
        }
    }

    // POP-STR-14, POP-DOM-08: the focus target.
    /// <inheritdoc />
    protected override void BuildAfterElement(RenderTreeBuilder builder)
    {
        if (selections.HasFocusGuards)
        {
            RenderGuard(builder, captureFocusTarget);
        }
        else
        {
            focusTarget = null;
        }
    }

    // FocusGuard.tsx:10-42: the VoiceOver swap to role="button" is the `popup` module's write after attach (ADR 04
    // Amendments 2026-10-27). Both guards share one region's sequence numbers.
    private static void RenderGuard(RenderTreeBuilder builder, Action<ElementReference> capture)
    {
        builder.OpenRegion(0);
        builder.OpenElement(0, "span");
        builder.AddAttribute(1, "tabindex", "0");
        builder.AddAttribute(2, PopupMarkerAttributes.FocusGuard, string.Empty);
        builder.AddAttribute(3, "aria-hidden", "true");
        builder.AddAttribute(4, "style", VisuallyHidden.Style);
        builder.AddElementReferenceCapture(5, capture);
        builder.CloseElement();
        builder.CloseRegion();
    }

    private void AddSelections(Store.SelectorSubscription<PopoverStoreState<TPayload>> subscription)
    {
        selections = new StoreSelectionHolder(
            subscription.Select(PopoverSelectors.IsPressOpenedByTrigger, Id),
            subscription.Select(PopoverSelectors.IsTouchPressOpen, OpenOnHover),
            subscription.Select(PopoverSelectors.HasTriggerFocusGuards, Id));
    }

    // The interactions handle's options: press toggles with the module's patient-click window; hover per POP §11.
    private void SyncInteractions()
    {
        PopoverStore<TPayload> store = behavior.Store;
        interactions!.SessionToken = store.SessionToken;
        bool hover = !Disabled && OpenOnHover && !selections.TouchPressOpen;
        InteractionsTriggerOptions current = interactions.Options;
        InteractionsHoverOptions? hoverOptions = hover
            ? new InteractionsHoverOptions
            {
                RestMs = (int)(Delay?.ToMilliseconds() ?? OpenDelay),
                DelayClose = CloseDelayMilliseconds,
                Move = false,
                MouseOnly = true,
                SafePolygon = true,
            }
            : null;
        if (!string.Equals(current.TriggerId, Id, StringComparison.Ordinal) || current.Click is null || !Equals(current.Hover, hoverOptions))
        {
            interactions.Options = new InteractionsTriggerOptions { TriggerId = Id, Click = s_click, Hover = hoverOptions };
        }
    }

    // A press or a hover of this trigger reaches its store; a press's interaction type is the open method (PSH-PTR-01/02).
    private Task RouteAsync(OpenChangeRequest request)
    {
        PopoverStore<TPayload> store = behavior.Store;
        if (request.InteractionType is { } type)
        {
            store.SetOpenInteractionType(type);
        }

        return store.RequestOpenAsync(request.Open, request.Details);
    }

    // useTriggerFocusGuards.ts:42-49: a focused guard closes with `focus-out`, its event and the guard as the trigger (design
    // point 4's sub-point (i)); on WebAssembly this runs inside the `guard` decision, so the close commits before the module
    // moves focus.
    private void OnGuardFocused(string guard, EventSnapshot snapshot)
    {
        ElementReference? element = guard == "before" ? preGuard : focusTarget;
        _ = InvokeAsync(() => behavior.Store.RequestOpenAsync(false, new OpenChangeEventDetails(EventReason.FocusOut)
        {
            Event = snapshot,
            Trigger = new EventTrigger(null, element),
        }));
    }

    // The family's selections, which follow the store the behavior subscribed to; their arguments follow the parameters.
    private struct StoreSelectionHolder(
        Store.StoreSelection<PopoverStoreState<TPayload>, string?, bool> pressOpened,
        Store.StoreSelection<PopoverStoreState<TPayload>, bool, bool> touchPressOpen,
        Store.StoreSelection<PopoverStoreState<TPayload>, string?, bool> focusGuards)
    {
        /// <summary>Gets a value indicating whether this trigger opened the popup by a press.</summary>
        public readonly bool PressOpened => pressOpened?.Value ?? false;

        /// <summary>Gets a value indicating whether a touch press opened the popup while hover opening is on.</summary>
        public readonly bool TouchPressOpen => touchPressOpen?.Value ?? false;

        /// <summary>Gets a value indicating whether this trigger renders the focus guards around itself.</summary>
        public readonly bool HasFocusGuards => focusGuards?.Value ?? false;

        /// <summary>Sets the selections' arguments from the trigger's parameters.</summary>
        /// <param name="id">The trigger's id.</param>
        /// <param name="openOnHover">Whether the trigger opens the popup on hover.</param>
        public readonly void SetArgs(string? id, bool openOnHover)
        {
            if (pressOpened is null)
            {
                return;
            }

            pressOpened.Arg = id;
            touchPressOpen.Arg = openOnHover;
            focusGuards.Arg = id;
        }
    }
}

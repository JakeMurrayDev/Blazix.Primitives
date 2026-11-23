using Blazix.Primitives.Interop;
using Blazix.Primitives.Rendering;
using Blazix.Primitives.Store;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Blazix.Primitives;

// PRV §3.2 (preview-card/trigger/PreviewCardTrigger.tsx:152-157); PRV-STA-04.
/// <summary>Represents the state of a preview card trigger, which the part exposes as data attributes and passes to its class and style functions.</summary>
/// <param name="Open">Whether the card is open and anchored to this trigger; rendered as <c>data-popup-open</c>.</param>
[PartState]
public sealed partial record PreviewCardTriggerState(bool Open);

// PRV-DOM-02/03; preview-card/trigger/PreviewCardTriggerDataAttributes.ts.
/// <summary>Provides the names of the data attributes a preview card trigger renders.</summary>
public static class PreviewCardTriggerDataAttributes
{
    /// <summary>Represents the attribute present while the card is open and anchored to the trigger: <c>data-popup-open</c>.</summary>
    public const string PopupOpen = PopupTriggerDataAttributes.PopupOpen;
}

// Base UI's PreviewCard.Trigger (preview-card/trigger/PreviewCardTrigger.tsx); the contract is docs/components/preview-card.md
// §3.2, §5.1, §6, in TooltipTrigger's shape (M4 track 6).
// - The popup half is PopupTriggerBehavior (M4 track 1): registration by id at parameter set, the store from the handle or
//   the root, the payload and the close delay forwarded while active (PRV-API-13, PRV-STA-11). The id is the bag `id`,
//   else the generated one, rendered on the trigger's own element (PRV-API-14; issue 115).
// - The browser half is one `interactions` trigger handle (M4 track 3: InteractionsTriggerBehavior): hover with the open
//   delay, mouse only, no move, the safe polygon always (PRV-PTR-01/04/15); focus after the same delay with the immediate
//   switch and the Escape and window-blur blocks (PRV-KEY-01/02/04, PRV-FOC-03); the hovered-line recording (PSH-PTR-08/29);
//   no press handling (PRV-PTR-11). The delays are resolved here (`Delay ?? 600`, `CloseDelay ?? 300`; ADR 08 Amendments
//   2026-10-27).
// - An `<a>` with no `href`, `tabindex`, `role` or `type` of its own (PRV-API-15), no ButtonBehavior, no aria-*, no other
//   state attribute (PRV-DOM-03). The consumer's handlers pass through ungated (PRV §3.2).
/// <summary>
/// Opens a preview card on hover or keyboard focus: Base UI's <c>PreviewCard.Trigger</c>, rendered as an <c>a</c>.
/// </summary>
/// <typeparam name="TPayload">The type of the payload the card's triggers carry.</typeparam>
/// <remarks>
/// <para>
/// The trigger must be placed within a <see cref="PreviewCardRoot{TPayload}"/> or be given a <see cref="Handle"/>. Its
/// <c>id</c>, or a generated one, is its trigger id.
/// </para>
/// <para>
/// The trigger adds no <c>href</c> or <c>tabindex</c>: give it an <c>href</c> to make it a link that keyboard users can reach.
/// </para>
/// </remarks>
[CascadingTypeParameter(nameof(TPayload))]
public sealed class PreviewCardTrigger<TPayload> : PartBase<PreviewCardTriggerState>
{
    private static readonly StateAttributesMapping<PreviewCardTriggerState> s_mapping = PopupStateMapping.CreateTrigger<PreviewCardTriggerState>();

    private readonly PopupTriggerBehavior<PreviewCardStore<TPayload>, PreviewCardStoreState<TPayload>, TPayload> popup;
    private InteractionsTriggerBehavior? interactions;
    private PreviewCardTriggerState state = new(false);
    private bool payloadSupplied;
    private bool forwarded;
    private OptionInputs builtInputs;
    private InteractionsTriggerOptions? builtOptions;

    /// <summary>Initializes a new instance of the <see cref="PreviewCardTrigger{TPayload}"/> class.</summary>
    public PreviewCardTrigger()
    {
        popup = new(this, "PreviewCardTrigger", StateHasChanged, static _ => { });
    }

    [Inject]
    private IJSRuntime JSRuntime { get; set; } = null!;

    [CascadingParameter]
    private PreviewCardStore<TPayload>? CascadedStore { get; set; }

    /// <summary>Gets or sets the handle that connects this trigger to a root rendered elsewhere.</summary>
    /// <value>The root's <see cref="PreviewCardHandle{TPayload}"/>, or <see langword="null"/> to use the enclosing root.</value>
    [Parameter]
    public PreviewCardHandle<TPayload>? Handle { get; set; }

    /// <summary>Gets or sets the payload the root's <c>PayloadContent</c> receives while this trigger is active.</summary>
    /// <value>The payload; when omitted, the root receives the default value.</value>
    [Parameter]
    public TPayload? Payload { get; set; }

    /// <summary>Gets or sets how long to wait before opening the card on hover or keyboard focus.</summary>
    /// <value>The delay, or <see langword="null"/> (the default) for 600 milliseconds.</value>
    [Parameter]
    public Duration? Delay { get; set; }

    /// <summary>Gets or sets how long to wait before closing the card after the pointer leaves this trigger or the card.</summary>
    /// <value>The delay, or <see langword="null"/> (the default) for 300 milliseconds.</value>
    [Parameter]
    public Duration? CloseDelay { get; set; }

    /// <inheritdoc />
    protected override string DefaultTagName => "a";

    /// <inheritdoc />
    protected override bool UsesGeneratedId => true;

    // The pipeline reads the state before the part's attributes, so the record follows the store here.
    /// <inheritdoc />
    protected override PreviewCardTriggerState State
    {
        get
        {
            bool open = popup.IsOpenedByTrigger;
            if (state.Open != open)
            {
                state = new PreviewCardTriggerState(open);
            }

            return state;
        }
    }

    /// <inheritdoc />
    protected override StateAttributesMapping<PreviewCardTriggerState>? StateAttributesMapping => s_mapping;

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
    /// <exception cref="InvalidOperationException">The trigger is placed outside a <see cref="PreviewCardRoot{TPayload}"/> and has no <see cref="Handle"/>.</exception>
    protected override void OnParametersSet()
    {
        // PRV-EDGE-02 (PreviewCardTrigger.tsx), in every build.
        popup.Apply(CascadedStore, Handle, Id, Element, payloadSupplied, Payload, "Blazix: `<PreviewCardTrigger>` must be either used within a `<PreviewCardRoot>` component or provided with a handle.");
        interactions!.SetConsumerAttributes(AdditionalAttributes);
    }

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        // The token is read from the current store at each after-render: a detached trigger rendered before its root takes the
        // root's store through a store-change render with no parameter set (track 8's note on PR 136's rebinding).
        PreviewCardStore<TPayload> store = popup.Store;
        Task sync = Task.CompletedTask;
        if (store.SessionToken is { } token)
        {
            interactions!.SessionToken = token;
            interactions.Options = BuildOptions();
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
            popup.Dispose();
            if (interactions is not null)
            {
                _ = interactions.DisposeAsync().AsTask();
            }
        }

        base.Dispose(disposing);
    }

    // PreviewCardTrigger.tsx:70-80 (hover and focus) and inlineRect.ts:203-224 (the hovered line). ADR 16 rule 7: the
    // options are built only when one of their inputs changed, so an unchanged render allocates none.
    private InteractionsTriggerOptions BuildOptions()
    {
        OptionInputs inputs = new(Id, PreviewCardDelays.Open(Delay), PreviewCardDelays.Close(CloseDelay));
        if (builtOptions is not null && inputs == builtInputs)
        {
            return builtOptions;
        }

        builtInputs = inputs;
        builtOptions = new InteractionsTriggerOptions
        {
            TriggerId = inputs.Id,
            Hover = new InteractionsHoverOptions
            {
                DelayOpen = inputs.OpenDelay,
                DelayClose = inputs.CloseDelay,
                Move = false,
                MouseOnly = true,
                SafePolygon = true,
            },
            Focus = new InteractionsFocusOptions(inputs.OpenDelay),
            HoveredLine = true,
        };
        return builtOptions;
    }

    // PRV-STA-11 (useTriggerDataForwarding): written when this trigger comes to own the mounted card and whenever its close
    // delay changes while it does.
    private void ForwardState(PreviewCardStore<TPayload> store)
    {
        if (!popup.IsMountedByTrigger)
        {
            forwarded = false;
            return;
        }

        int closeDelay = PreviewCardDelays.Close(CloseDelay);
        if (!forwarded || store.State.CloseDelay != closeDelay)
        {
            forwarded = true;
            store.SetCloseDelay(closeDelay);
        }
    }

    private Task RequestAsync(OpenChangeRequest request) => popup.Store.RequestOpenAsync(request.Open, request.Details);

    private readonly record struct OptionInputs(string? Id, int OpenDelay, int CloseDelay);
}

using Blazix.Primitives.Interop;
using Blazix.Primitives.Rendering;
using Blazix.Primitives.Store;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace Blazix.Primitives;

// COL-STA-07, §3.1 state object; collapsible/root/CollapsibleRoot.tsx:43-50,75-78. ADR 08 §3: sealed, partial, [PartState].
// TransitionStatus is a mirrored field (ADR 04 §2): the transitions module writes data-starting-style/data-ending-style.
/// <summary>Represents the state of a collapsible root, which the part exposes as data attributes and passes to its class and style functions.</summary>
/// <param name="Open">A value indicating whether the panel is open; projected as <c>data-open</c> or <c>data-closed</c>.</param>
/// <param name="Disabled">A value indicating whether the collapsible ignores user interaction; projected as <c>data-disabled</c>.</param>
/// <param name="TransitionStatus">
/// The phase of the panel's enter or exit transition, or <see langword="null"/> while none is in progress; its
/// <c>data-starting-style</c> and <c>data-ending-style</c> attributes are written by the browser-side behavior.
/// </param>
[PartState]
public sealed partial record CollapsibleRootState(bool Open, bool Disabled, [property: MirroredField] TransitionStatus? TransitionStatus);

// collapsible/root/CollapsibleRootDataAttributes.ts (COL-DOM-01/03).
/// <summary>Provides the names of the data attributes a collapsible root renders.</summary>
public static class CollapsibleRootDataAttributes
{
    /// <summary>Represents the attribute present while the collapsible is open: <c>data-open</c>.</summary>
    public const string Open = "data-open";

    /// <summary>Represents the attribute present while the collapsible is closed: <c>data-closed</c>.</summary>
    public const string Closed = "data-closed";

    /// <summary>Represents the attribute present while the collapsible begins animating in: <c>data-starting-style</c>.</summary>
    public const string StartingStyle = "data-starting-style";

    /// <summary>Represents the attribute present while the collapsible is animating out: <c>data-ending-style</c>.</summary>
    public const string EndingStyle = "data-ending-style";
}

// Base UI's Collapsible.Root (collapsible/root/CollapsibleRoot.tsx); the contract is docs/components/collapsible.md. The
// state logic is CollapsibleRootContext (useCollapsibleRoot), which Accordion.Item reuses. The Root renders no handler
// (COL-STR-01); its data-starting-style/data-ending-style are the transitions module's (one TransitionsTwin, ADR 04 §1),
// reading the open state from data-open (openSelector, ADR 04 Amendments 2026-10-21) and ending its exit when the panel's
// ends (ExternalExit and UnmountRequests). Its status is mirrored into the shared state (COL-STA-05/07). The context is
// cascaded around the Root's element, as Base UI's provider wraps it.
/// <summary>
/// Represents the root of a collapsible, Base UI's <c>Collapsible.Root</c>, rendered as a <c>div</c>: it owns the open
/// state that its <see cref="CollapsibleTrigger"/> toggles and its <see cref="CollapsiblePanel"/> shows.
/// </summary>
public sealed class CollapsibleRoot : PartBase<CollapsibleRootState>
{
    private const string DefaultTag = "div";
    private const string OpenAttributeSelector = "[data-open]";

    private static readonly StateDataAttribute[] s_open = [new(CollapsibleRootDataAttributes.Open, string.Empty)];
    private static readonly StateDataAttribute[] s_closed = [new(CollapsibleRootDataAttributes.Closed, string.Empty)];
    private static readonly StateAttributesMapping<CollapsibleRootState> s_mapping = new StateAttributesMapping<CollapsibleRootState>()
        .Map<bool>(nameof(CollapsibleRootState.Open), static open => open ? s_open : s_closed);

    private readonly RenderFragment renderPart;
    private readonly TaskCompletionSource attachedSource = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private CollapsibleRootContext? context;
    private StoreSelection<CollapsibleRootStoreState, bool>? open;
    private StoreSelection<CollapsibleRootStoreState, bool>? disabled;
    private StoreSelection<CollapsibleRootStoreState, TransitionStatus?>? status;
    private StoreSelection<CollapsibleRootStoreState, int>? unmountRequests;
    private CollapsibleRootState? state;
    private TransitionsTwin? twin;
    private TwinHandle<TransitionsOptions>? handle;
    private int sentUnmountRequests;
    private string? lateId;
    private bool parametersSet;

    /// <summary>Initializes a new instance of the <see cref="CollapsibleRoot"/> class.</summary>
    public CollapsibleRoot()
    {
        renderPart = base.BuildRenderTree;
    }

    [Inject]
    private IJSRuntime JSRuntime { get; set; } = null!;

    [Inject]
    private ILogger<CollapsibleRoot> Logger { get; set; } = null!;

    [Inject]
    private PartIdGenerator IdGenerator { get; set; } = null!;

    // COL-API-01
    /// <summary>Gets or sets a value indicating whether the panel is open.</summary>
    /// <value>
    /// <see langword="true"/> to open the panel. Supplying this parameter on the first render makes the open state the
    /// consumer's: the collapsible then only requests changes through <see cref="OnOpenChange"/> and
    /// <see cref="OpenChanged"/>. To let the collapsible own the state, use <see cref="DefaultOpen"/> instead.
    /// </value>
    [Parameter]
    public bool Open { get; set; }

    /// <summary>Gets or sets the callback invoked with the new open state when a request is accepted.</summary>
    /// <value>The binding callback of <c>@bind-Open</c>.</value>
    [Parameter]
    public EventCallback<bool> OpenChanged { get; set; }

    // COL-API-02
    /// <summary>Gets or sets a value indicating whether the panel is initially open when the collapsible owns the open state.</summary>
    /// <value><see langword="true"/> to start open; the default is <see langword="false"/>. Read once, on the first render.</value>
    [Parameter]
    public bool DefaultOpen { get; set; }

    // COL-API-03, COL-EVT-01..07
    /// <summary>Gets or sets the callback invoked when the collapsible is about to open or close.</summary>
    /// <value>
    /// A callback that receives the requested open state and the request's details, whose reason is
    /// <see cref="EventReason.TriggerPress"/> for a trigger press and <see cref="EventReason.None"/> for a find-in-page
    /// reveal. Calling <see cref="ChangeEventDetails.Cancel"/> keeps the current state.
    /// </value>
    [Parameter]
    public EventCallback<ValueChangeEventArgs<bool>> OnOpenChange { get; set; }

    // COL-API-04
    /// <summary>Gets or sets a value indicating whether the collapsible ignores user interaction.</summary>
    /// <value>
    /// <see langword="true"/> to disable every trigger that does not set its own <c>Disabled</c>; the default is
    /// <see langword="false"/>. The open state can still change through <see cref="Open"/>.
    /// </value>
    [Parameter]
    public bool Disabled { get; set; }

    /// <inheritdoc />
    protected override string DefaultTagName => DefaultTag;

    /// <inheritdoc />
    protected override CollapsibleRootState State
    {
        get
        {
            if (state is null || state.Open != open!.Value || state.Disabled != disabled!.Value || state.TransitionStatus != status!.Value)
            {
                state = new CollapsibleRootState(open!.Value, disabled!.Value, status!.Value);
            }

            return state;
        }
    }

    /// <inheritdoc />
    protected override StateAttributesMapping<CollapsibleRootState>? StateAttributesMapping => s_mapping;

    // ADR 02 decision 1: under a substitute callback the twin finds the element by id.
    /// <inheritdoc />
    protected override bool UsesGeneratedId => RenderFunction is not null;

    // For the fixtures: completes once the transitions behavior is attached to the root's element.
    /// <summary>Gets a task that completes when the root's transition behavior is attached to its element.</summary>
    /// <value>A task that completes after the first interactive render has attached the behavior.</value>
    internal Task TwinAttached => attachedSource.Task;

    /// <summary>Gets the shared state the root cascades to its trigger and panel.</summary>
    /// <value>The context; available from the first render.</value>
    internal CollapsibleRootContext Context => context!;

    /// <inheritdoc />
    public override Task SetParametersAsync(ParameterView parameters)
    {
        context ??= new CollapsibleRootContext(IdGenerator.Next(), Logger);
        context.OpenState.SetParameters(parameters, nameof(Open), nameof(DefaultOpen));
        return base.SetParametersAsync(parameters);
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        context!.OnOpenChange = OnOpenChange;
        context.OpenChanged = OpenChanged;
        context.ApplyParameters(Disabled, firstSet: !parametersSet);
        parametersSet = true;
    }

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        // Base UI's provider wraps the root's element; the cascade is fixed and parts subscribe (ADR 16 rule 9).
        builder.OpenComponent<CascadingValue<CollapsibleRootContext>>(0);
        builder.AddComponentParameter(1, nameof(CascadingValue<CollapsibleRootContext>.Value), context);
        builder.AddComponentParameter(2, nameof(CascadingValue<CollapsibleRootContext>.IsFixed), true);
        builder.AddComponentParameter(3, nameof(CascadingValue<CollapsibleRootContext>.ChildContent), renderPart);
        builder.CloseComponent();
    }

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!Enabled)
        {
            if (handle is { } released)
            {
                handle = null;
                await released.DisposeAsync();
            }

            return;
        }

        CollapsibleRootStoreState shared = context!.State;
        if (handle is not null)
        {
            if (shared.UnmountRequests != sentUnmountRequests)
            {
                sentUnmountRequests = shared.UnmountRequests;
                await handle.UpdateAsync(Options(shared));
            }

            return;
        }

        string? elementId = null;
        if (Element is null)
        {
            elementId = Id ?? lateId;
            if (elementId is null)
            {
                // A substitute whose root is a component leaves no element reference: render an id, attach next time.
                lateId = IdGenerator.Next();
                StateHasChanged();
                return;
            }
        }

        twin ??= CreateTwin();
        sentUnmountRequests = shared.UnmountRequests;
        TransitionsOptions options = Options(shared);
        handle = elementId is null
            ? await twin.AttachAsync(Element!.Value, options)
            : await twin.AttachAsync(elementId, options);
        attachedSource.TrySetResult();
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing && twin is not null)
        {
            twin.StatusChanged -= MirrorStatus;
            _ = twin.DisposeAsync().AsTask();
            twin = null;
            handle = null;
        }

        base.Dispose(disposing);
    }

    /// <inheritdoc />
    protected override void SubscribeSelectors()
    {
        SelectorSubscription<CollapsibleRootStoreState> subscription = SubscribeTo(context!);
        open = subscription.Select(CollapsibleRootStoreState.OpenSelector);
        disabled = subscription.Select(CollapsibleRootStoreState.DisabledSelector);
        status = subscription.Select(CollapsibleRootStoreState.StatusSelector);
        unmountRequests = subscription.Select(CollapsibleRootStoreState.UnmountRequestsSelector);
    }

    /// <inheritdoc />
    protected override void AddPartAttributes(AttributeList attributes)
    {
        if (lateId is not null && Id is null)
        {
            attributes.Set("id", lateId);
        }
    }

    private TransitionsTwin CreateTwin()
    {
        TransitionsTwin created = new(JSRuntime);
        created.StatusChanged += MirrorStatus;
        return created;
    }

    // The values the root's element was rendered with: it never renders data-starting-style or data-ending-style itself,
    // because its first render is either closed (unset) or open and idle.
    private static TransitionsOptions Options(CollapsibleRootStoreState shared)
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

    private void MirrorStatus(TransitionStatusChange change) => context!.MirrorStatus(change.Status);
}

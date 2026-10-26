using Blazix.Primitives.Interop;
using Blazix.Primitives.Rendering;
using Blazix.Primitives.Store;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.JSInterop;

namespace Blazix.Primitives;

// TAB-STA-20, TAB-STA-22; tabs/panel/TabsPanel.tsx:55-60,117-126. ADR 04 §2: the transition status is mirrored (the
// `transitions` module writes data-starting-style and data-ending-style); ADR 04 Amendments 2026-10-26: so is the direction.
/// <summary>Represents the state of a tab panel, which the part exposes as data attributes and passes to its class and style functions.</summary>
/// <param name="Hidden">A value indicating whether the panel is closed and its exit transition has finished; projected as <c>data-hidden</c>.</param>
/// <param name="Orientation">The axis along which the tabs are laid out; projected as <c>data-orientation</c>.</param>
/// <param name="TabActivationDirection">The direction from the previously active tab to the active one, rendered as <c>data-activation-direction</c> by the browser-side behavior.</param>
/// <param name="TransitionStatus">
/// The phase of the panel's enter or exit transition, or <see langword="null"/> when it is not transitioning; never
/// <see cref="Primitives.TransitionStatus.Idle"/>.
/// </param>
[PartState]
public sealed partial record TabsPanelState(
    bool Hidden,
    Orientation Orientation,
    [property: MirroredField] TabsActivationDirection TabActivationDirection,
    [property: MirroredField] TransitionStatus? TransitionStatus);

// TAB §3.5; tabs/panel/TabsPanelDataAttributes.ts, in its order.
/// <summary>Provides the names of the data attributes a tab panel renders.</summary>
public static class TabsPanelDataAttributes
{
    /// <summary>Represents the attribute whose value is the panel's index among the root's rendered panels: <c>data-index</c>.</summary>
    public const string Index = "data-index";

    /// <summary>Represents the attribute whose value is the activation direction, <c>left</c>, <c>right</c>, <c>up</c>, <c>down</c> or <c>none</c>: <c>data-activation-direction</c>.</summary>
    public const string ActivationDirection = "data-activation-direction";

    /// <summary>Represents the attribute whose value is the orientation, <c>horizontal</c> or <c>vertical</c>: <c>data-orientation</c>.</summary>
    public const string Orientation = "data-orientation";

    /// <summary>Represents the attribute present, with an empty value, while the panel is hidden: <c>data-hidden</c>.</summary>
    public const string Hidden = "data-hidden";

    /// <summary>Represents the attribute present, with an empty value, while the panel plays its enter transition: <c>data-starting-style</c>.</summary>
    public const string StartingStyle = "data-starting-style";

    /// <summary>Represents the attribute present, with an empty value, while the panel plays its exit transition: <c>data-ending-style</c>.</summary>
    public const string EndingStyle = "data-ending-style";
}

// Base UI's Tabs.Panel (tabs/panel/TabsPanel.tsx); the contract is docs/components/tabs.md §9.
// - Open is `value === root value` (TAB-STA-22); the element is rendered while KeepMounted || mounted (TAB-DOM-12),
//   with role="tabpanel", a generated id (a consumer id overrides the attribute only, TAB-EDGE-12, §16 Q1 as 1.9.0:
//   design point 9 of the Tabs track, settled by the orchestrator 2026-10-26), hidden and data-hidden while not
//   mounted, inert while not open, tabindex 0 or -1 (TAB-DOM-13..16).
// - The default machine (TRN-ALG-01 with no idle state, no deferred ending, no initial-open animation): the handover of
//   the Checkbox and Radio indicators (ADR 04 Amendments 2026-10-18) for an element C# creates open; from attach the
//   `transitions` module writes the two transition attributes, reads the open input from the applied batch through
//   OpenSelector (design point 1 of the Tabs track, ":not([inert])", settled by the orchestrator 2026-10-26) and
//   reports its status, which C# mirrors; its completion message unmounts the element (ADR 01's TRN-ANI-03/09/10 row,
//   with the convergent `hidden`).
// - The Root's mounted-panel registry holds the generated id for every render that renders the element (TAB-STA-21,
//   TAB-ANI-05). The id is published in the render that creates the element (ADR 01 Amendments 2026-10-22), so the Tab's
//   aria-controls lands in the same batch.
// - Not here yet (open until track 1's E1 and E2 are on `main`): data-index (rendered "-1", the value before the first
//   publication, TAB-DOM-17) and aria-labelledby (TAB-ARIA-08).
/// <summary>
/// Represents the content shown while its tab is active: Base UI's <c>Tabs.Panel</c>, rendered as a <c>div</c> with
/// <c>role="tabpanel"</c> while its tab is active and through its exit transition, or always with <see cref="KeepMounted"/>.
/// </summary>
/// <typeparam name="TValue">The type of the panel's value, which is the type of its root's value.</typeparam>
/// <remarks>
/// The part must be placed within a <see cref="TabsRoot{TValue}"/>. The panel's own <c>inert</c> attribute, present while
/// its tab is not active, also tells the browser-side behavior when to play the enter and exit transitions, so an
/// <c>inert</c> attribute supplied by the consumer replaces it for both.
/// </remarks>
public sealed class TabsPanel<TValue> : PartBase<TabsPanelState>
{
    private const string DefaultTag = "div";
    private const string UnpublishedIndex = "-1";

    // Design point 1 of the Tabs track (settled by the orchestrator 2026-10-26): the panel renders `inert` exactly
    // while it is not open.
    private const string OpenSelector = ":not([inert])";

    private readonly Action rootChanged;
    private readonly Action<TransitionStatusChange> onTransitionStatus;
    private readonly Action<bool> onTransitionCompleted;
    private readonly TaskCompletionSource attachedSource = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private TabsRootContext<TValue>? root;
    private TabsPanelState? state;
    private bool open;
    private bool mounted;
    private TransitionStatus? transitionStatus;
    private TransitionStatus? renderedTransition;
    private string? renderedDirection;
    private bool registered;
    private TValue registeredValue = default!;
    private TransitionsTwin? transitionsTwin;
    private TwinHandle<TransitionsOptions>? transitionsHandle;
    private TransitionsOptions? sentOptions;
    private bool disposed;

    /// <summary>Initializes a new instance of the <see cref="TabsPanel{TValue}"/> class.</summary>
    public TabsPanel()
    {
        rootChanged = OnRootChanged;
        onTransitionStatus = change => _ = InvokeAsync(() => MirrorTransition(change.Status, change.Mounted));
        onTransitionCompleted = completedOpen => _ = InvokeAsync(() => CompleteTransition(completedOpen));
    }

    [Inject]
    private IJSRuntime JSRuntime { get; set; } = null!;

    private bool Rendered => KeepMounted || mounted;

    [CascadingParameter]
    private TabsRootContext? AnyRootContext { get; set; }

    [CascadingParameter]
    private TabsRootContext<TValue>? RootContext { get; set; }

    // TAB-API-12
    /// <summary>Gets or sets the value of the tab whose activation shows the panel.</summary>
    /// <value>The value, compared with the root's as a tab's is. <see langword="null"/> never shows the panel.</value>
    [Parameter, EditorRequired]
    public TValue Value { get; set; } = default!;

    // TAB-API-13
    /// <summary>Gets or sets a value indicating whether the panel stays in the document while its tab is not active.</summary>
    /// <value>
    /// <see langword="true"/> to keep the closed panel, hidden with the <c>hidden</c> attribute once its exit transition has
    /// finished; <see langword="false"/>, the default, to remove it then.
    /// </value>
    [Parameter]
    public bool KeepMounted { get; set; }

    /// <inheritdoc />
    protected override string DefaultTagName => DefaultTag;

    /// <inheritdoc />
    protected override TabsPanelState State => state!;

    // TAB-DOM-13: the generated id is the one the registry holds.
    /// <inheritdoc />
    protected override bool UsesGeneratedId => true;

    // For the fixtures (tests/Blazix.Primitives.Fixtures.Client): a browser test waits for the module before it selects.
    /// <summary>Gets a task that completes when the panel's transitions behavior is first attached to its element.</summary>
    /// <value>A task that completes after the first interactive render that showed the element.</value>
    internal Task TwinAttached => attachedSource.Task;

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">
    /// The panel is not placed within a <see cref="TabsRoot{TValue}"/>, or its <typeparamref name="TValue"/> differs from
    /// the root's.
    /// </exception>
    protected override void OnInitialized()
    {
        TabsRootContext.Require(AnyRootContext);
        if (RootContext is null)
        {
            // Design point R7 of M2 (ADR 02 Amendments 2026-10-20, part-misuse errors are always on).
            throw new InvalidOperationException(
                "Blazix: A `TabsPanel` must have the `TValue` of its `TabsRoot`. Set `TValue` on the panel, or give `Value` the root's type, for example `Value=\"(int?)1\"` in a `TabsRoot` of `int?`.");
        }

        root = RootContext;
        root.AddListener(rootChanged);
        base.OnInitialized();

        // TAB-ANI-02, TRN-STA-13: open at mount means mounted, without a 'starting' phase.
        open = root.IsActive(Value);
        (mounted, transitionStatus) = TransitionStatusRules.Initial(open, enableIdleState: false, animateInitialOpen: false);
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        ApplyOpenState();
        SyncRegistration();
        UpdateState();
    }

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        if (!Rendered)
        {
            // The element is gone: a later one is created with the direction of its own first render.
            renderedDirection = null;
            ((IPartHost)this).SetElement(null);
            return;
        }

        base.BuildRenderTree(builder);
    }

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (disposed)
        {
            return;
        }

        await SyncTransitionAsync();
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing && !disposed)
        {
            disposed = true;
            root?.RemoveListener(rootChanged);
            if (registered)
            {
                registered = false;
                root!.UnregisterPanel(registeredValue, GeneratedId!);
            }

            if (transitionsTwin is not null)
            {
                transitionsTwin.StatusChanged -= onTransitionStatus;
                transitionsTwin.TransitionCompleted -= onTransitionCompleted;
                _ = transitionsTwin.DisposeAsync().AsTask();
                transitionsTwin = null;
            }

            transitionsHandle = null;
        }

        base.Dispose(disposing);
    }

    /// <inheritdoc />
    protected override void AddPartAttributes(AttributeList attributes)
    {
        // TAB-ARIA-07, TAB-DOM-14..17: the consumer's role, hidden, inert and tabindex still win at the merge.
        attributes.Set("role", "tabpanel");
        if (!mounted)
        {
            attributes.Set("hidden", true);
        }

        if (!open)
        {
            attributes.Set("inert", true);
        }

        attributes.Set("tabindex", open ? "0" : "-1");
        attributes.Set(TabsPanelDataAttributes.Index, UnpublishedIndex);

        // ADR 04 Amendments 2026-10-18 and 2026-10-26 (the handover): the direction the element is created with, and the
        // starting style of an element C# creates open (TAB-ANI-01).
        renderedDirection ??= TabsLiterals.ToLiteral(root!.Direction);
        attributes.Set(TabsPanelDataAttributes.ActivationDirection, renderedDirection);
        if (renderedTransition == TransitionStatus.Starting)
        {
            attributes.Set(TabsPanelDataAttributes.StartingStyle, string.Empty);
        }
    }

    // A change of the root's value, orientation, mirrored direction or panel registry.
    private void OnRootChanged()
    {
        if (disposed)
        {
            return;
        }

        bool changed = ApplyOpenState();
        SyncRegistration();
        if (UpdateState() || changed)
        {
            StateHasChanged();
        }
    }

    // The C# half of the transition machine for this render (TransitionStatusRules): an element created open starts in
    // 'starting'; an element with no transitions twin to animate it leaves at once. Returns whether anything changed.
    private bool ApplyOpenState()
    {
        bool nextOpen = root!.IsActive(Value);
        if (nextOpen == open && !(nextOpen && !mounted))
        {
            return false;
        }

        open = nextOpen;
        bool creating = open && !mounted && (!KeepMounted || transitionsHandle is null);
        (bool nextMounted, TransitionStatus? nextStatus) = TransitionStatusRules.Evaluate(open, mounted, transitionStatus, deferEndingState: false);
        if (creating)
        {
            renderedTransition = nextStatus;
        }
        else if (!open && mounted && transitionsHandle is null && Element is null)
        {
            // Nothing can play the exit: a substitute callback leaves no element to watch.
            nextMounted = false;
            nextStatus = null;
            renderedTransition = null;
        }

        mounted = nextMounted;
        transitionStatus = nextStatus;
        return true;
    }

    // TAB-STA-21, TAB-ANI-05: registered for every render that renders the element.
    private void SyncRegistration()
    {
        bool shouldRegister = Rendered && !disposed;
        if (registered && (!shouldRegister || !Equality.Is(registeredValue, Value)))
        {
            registered = false;
            root!.UnregisterPanel(registeredValue, GeneratedId!);
        }

        if (shouldRegister && !registered)
        {
            registered = true;
            registeredValue = Value;
            root!.RegisterPanel(Value, GeneratedId!);
        }
    }

    private void MirrorTransition(TransitionStatus? next, bool nextMounted)
    {
        if (disposed || (next == transitionStatus && nextMounted == mounted))
        {
            return;
        }

        transitionStatus = next;
        mounted = nextMounted;
        SyncRegistration();
        UpdateState();
        StateHasChanged();
    }

    private void CompleteTransition(bool completedOpen)
    {
        if (disposed || completedOpen || root!.IsActive(Value))
        {
            return;
        }

        // TAB-ANI-03: the exit finished: the element is unmounted (removed unless kept) and its status unset.
        mounted = false;
        transitionStatus = null;
        renderedTransition = null;
        SyncRegistration();
        UpdateState();
        StateHasChanged();
    }

    private async Task SyncTransitionAsync()
    {
        ElementReference? element = Rendered ? Element : null;
        if (element is null || (transitionsHandle is not null && !string.Equals(transitionsHandle.Key, element.Value.Id, StringComparison.Ordinal)))
        {
            if (transitionsHandle is { } released)
            {
                transitionsHandle = null;
                sentOptions = null;
                await released.DisposeAsync();
            }

            if (element is null)
            {
                return;
            }
        }

        if (transitionsHandle is not null)
        {
            // With OpenSelector the module reads the open state from the applied batch; only KeepMounted is sent again.
            if (sentOptions!.KeepMounted != KeepMounted)
            {
                sentOptions = sentOptions with { KeepMounted = KeepMounted };
                await transitionsHandle.UpdateAsync(sentOptions);
            }

            return;
        }

        if (transitionsTwin is null)
        {
            transitionsTwin = new TransitionsTwin(JSRuntime);
            transitionsTwin.StatusChanged += onTransitionStatus;
            transitionsTwin.TransitionCompleted += onTransitionCompleted;
        }

        // TAB §9, TRN §6.1: the default machine; Mounted and Status are what the element was rendered with, read on attach
        // only. A kept panel is hidden at exit end (TAB-DOM-14), so the module's convergent write covers it too.
        sentOptions = new TransitionsOptions
        {
            Open = open,
            Mounted = mounted,
            Status = renderedTransition,
            KeepMounted = KeepMounted,
            HiddenWhenUnmounted = true,
            OpenSelector = OpenSelector,
        };
        transitionsHandle = await transitionsTwin.AttachAsync(element.Value, sentOptions);
        attachedSource.TrySetResult();
    }

    // Returns whether the state changed; a new record is made only then (ADR 16 rule 7).
    private bool UpdateState()
    {
        bool hidden = !mounted;
        Orientation orientation = root!.Orientation;
        TabsActivationDirection direction = root.Direction;
        if (state is { } current
            && current.Hidden == hidden
            && current.Orientation == orientation
            && current.TabActivationDirection == direction
            && current.TransitionStatus == transitionStatus)
        {
            return false;
        }

        state = new TabsPanelState(hidden, orientation, direction, transitionStatus);
        return true;
    }
}

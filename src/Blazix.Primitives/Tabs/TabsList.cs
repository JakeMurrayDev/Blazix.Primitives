using Blazix.Primitives.Interop;
using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace Blazix.Primitives;

// TAB-STA-20; tabs/list/TabsList.tsx:84-87 (the Root's two fields). The direction is mirrored (ADR 04 Amendments 2026-10-26).
/// <summary>Represents the state of a tab list, which the part exposes as data attributes and passes to its class and style functions.</summary>
/// <param name="Orientation">The axis along which the tabs are laid out; projected as <c>data-orientation</c>.</param>
/// <param name="TabActivationDirection">The direction from the previously active tab to the active one, rendered as <c>data-activation-direction</c> by the browser-side behavior.</param>
[PartState]
public sealed partial record TabsListState(Orientation Orientation, [property: MirroredField] TabsActivationDirection TabActivationDirection);

// TAB §3.2; tabs/list/TabsListDataAttributes.ts, in its order.
/// <summary>Provides the names of the data attributes a tab list renders.</summary>
public static class TabsListDataAttributes
{
    /// <summary>Represents the attribute whose value is the activation direction, <c>left</c>, <c>right</c>, <c>up</c>, <c>down</c> or <c>none</c>: <c>data-activation-direction</c>.</summary>
    public const string ActivationDirection = "data-activation-direction";

    /// <summary>Represents the attribute whose value is the orientation, <c>horizontal</c> or <c>vertical</c>: <c>data-orientation</c>.</summary>
    public const string Orientation = "data-orientation";
}

// Base UI's Tabs.List (tabs/list/TabsList.tsx); the contract is docs/components/tabs.md.
// - A `div` with role="tablist", aria-orientation="vertical" only when vertical (TAB-STR-02, TAB-ARIA-01/02).
// - The composite root of its Tabs (CompositeRootBehavior; TAB §6.1 and §11): the Root's orientation, the List's
//   LoopFocus, Home and End handled, an empty DisabledIndices (so an aria-disabled Tab stays navigable, TAB-KEY-07), every
//   modifier blocking, propagation stopped, no hover highlight, the direction from the nearest DirectionProvider. Every
//   Tab's tabindex is the composite module's from attach (ADR 04 Amendments 2026-10-22). Its consumer onkeydown is declined
//   and run at `navigate` (option (i), chosen by the user 2026-10-23).
// - Its own TabsTwin, the List's handle of the `tabs` module (ADR 04 Amendments 2026-10-26, chosen by the user 2026-10-26):
//   the Tabs' click and focus, the `select` decision point, the pressing state, data-activation-direction on the five
//   parts and the ResizeObserver its Indicators re-measure on. The activation reaches the Tab by its element id
//   (TabsListContext), in the `select` decider on WebAssembly and from the module's report on Server.
// - Not here yet (open until track 1's E2 and E5 are on `main`): the ordered-ids report that feeds the Root's tab
//   registry, and the highlight that follows the active Tab (TAB-STA-19, TAB-FOC-04..07).
/// <summary>
/// Represents the group of tabs: Base UI's <c>Tabs.List</c>, rendered as a <c>div</c> with <c>role="tablist"</c>. The
/// list is one tab stop, and the arrow keys, Home and End move focus between its tabs.
/// </summary>
/// <remarks>The part must be placed within a <see cref="TabsRoot{TValue}"/>.</remarks>
public sealed class TabsList : PartBase<TabsListState>
{
    private const string DefaultTag = "div";

    private static readonly IReadOnlyList<int> s_noDisabledIndices = [];

    private readonly EventCallback onKeyDown;
    private readonly Action rootChanged;
    private readonly Action<TabsSelection> onSelection;
    private readonly Action<TabsTabEvent> onTabEvent;
    private readonly Action<TabsActivationDirection> onDirection;
    private readonly TaskCompletionSource tabsAttachedSource = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private TabsRootContext? root;
    private CompositeRootBehavior? composite;
    private TabsListContext? listContext;
    private TabsListState? state;
    private string? renderedDirection;
    private RenderFragment? cascade;
    private RenderFragment? cascadedContent;
    private TabsTwin? twin;
    private TwinHandle<TabsOptions>? handle;
    private TabsOptions? sentOptions;
    private bool disposed;

    /// <summary>Initializes a new instance of the <see cref="TabsList"/> class.</summary>
    public TabsList()
    {
        onKeyDown = CreateHandler<KeyboardEventArgs>("onkeydown", e => composite!.HandleKeyDownAsync(e));
        rootChanged = OnRootChanged;
        onSelection = selection => _ = InvokeAsync(() => ActivateFromReportAsync(selection));
        onTabEvent = tabEvent => _ = InvokeAsync(() => RunTabEventAsync(tabEvent));
        onDirection = direction => _ = InvokeAsync(() => root!.MirrorDirection(direction));
    }

    [Inject]
    private IJSRuntime JSRuntime { get; set; } = null!;

    [Inject]
    private PartIdGenerator IdGenerator { get; set; } = null!;

    private string? IdParameter => AdditionalAttributes is not null
        && AdditionalAttributes.TryGetValue("id", out object? value)
        && value is string { Length: > 0 } text ? text : null;

    [CascadingParameter]
    private TabsRootContext? RootContext { get; set; }

    [CascadingParameter]
    private DirectionProviderContext? DirectionContext { get; set; }

    // TAB-API-05, TAB-KEY-10, TAB-PTR-05/06
    /// <summary>Gets or sets a value indicating whether a tab is activated when it receives focus.</summary>
    /// <value>
    /// <see langword="true"/> to activate the focused tab, whether focus came from an arrow key, a primary-button press or
    /// code; <see langword="false"/>, the default, to activate a tab only with a click, Enter or Space.
    /// </value>
    [Parameter]
    public bool ActivateOnFocus { get; set; }

    // TAB-API-06, TAB-KEY-04/05
    /// <summary>Gets or sets a value indicating whether the arrow keys wrap from the last tab to the first and back.</summary>
    /// <value><see langword="true"/>, the default, to wrap; <see langword="false"/> to stop at either end.</value>
    [Parameter]
    public bool LoopFocus { get; set; } = true;

    /// <inheritdoc />
    protected override string DefaultTagName => DefaultTag;

    /// <inheritdoc />
    protected override TabsListState State => state!;

    // ADR 02 decision 1: under a substitute callback the twins find the element by id (the Button and RadioGroup precedent).
    /// <inheritdoc />
    protected override bool UsesGeneratedId => RenderFunction is not null;

    // For the fixtures (tests/Blazix.Primitives.Fixtures.Client): a browser test waits for both twins' listeners.
    /// <summary>Gets a task that completes when the list's composite and tabs behaviors are attached to its element.</summary>
    /// <value>A task that completes after the first interactive render has attached them.</value>
    internal Task TwinAttached => Task.WhenAll(composite!.TwinAttached, tabsAttachedSource.Task);

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">The list is not placed within a <see cref="TabsRoot{TValue}"/>.</exception>
    protected override void OnInitialized()
    {
        root = TabsRootContext.Require(RootContext);
        root.AddListener(rootChanged);
        base.OnInitialized();
        listContext = new TabsListContext(OnHandlersChanged);
        composite = new CompositeRootBehavior(JSRuntime, IdGenerator, StateHasChanged, DispatchExceptionAsync, onKeyDown)
        {
            EnableHomeAndEndKeys = true,
            DisabledIndices = s_noDisabledIndices,
        };
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        composite!.Orientation = CompositeLiterals.ToLiteral(root!.Orientation);
        composite.LoopFocus = LoopFocus;
        composite.SetDirectionContext(DirectionContext);
        composite.SetConsumerAttributes(AdditionalAttributes);
        UpdateState();

        // The cascade wraps the consumer's content inside the list's element: the list's registry, then the composite's.
        if (cascade is null || (!ReferenceEquals(ChildContent, cascade) && !ReferenceEquals(cascadedContent, ChildContent)))
        {
            cascadedContent = ChildContent;
            cascade = listContext!.Cascade(composite.Context.Cascade(cascadedContent));
        }

        ChildContent = cascade;
    }

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (disposed)
        {
            return;
        }

        string? id = IdParameter ?? GeneratedId;
        await composite!.AfterRenderAsync(Enabled, Element, id);
        await SyncTabsAsync(id);
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing && !disposed)
        {
            disposed = true;
            root?.RemoveListener(rootChanged);
            composite?.Dispose();
            if (twin is not null)
            {
                twin.SelectionReported -= onSelection;
                twin.TabEventReported -= onTabEvent;
                twin.DirectionChanged -= onDirection;
                twin.SelectionDecider = null;
                _ = twin.DisposeAsync().AsTask();
                twin = null;
                handle = null;
            }
        }

        base.Dispose(disposing);
    }

    // TAB-ARIA-01/02, TAB-ARIA-10: the consumer's role and accessible name win at the merge. ADR 04 Amendments 2026-10-18
    // and 2026-10-26: the direction the element is created with is rendered on every render (the handover).
    /// <inheritdoc />
    protected override void AddPartAttributes(AttributeList attributes)
    {
        composite!.AddAttributes(attributes, IdParameter ?? GeneratedId);
        attributes.Set("role", "tablist");
        if (root!.Orientation == Orientation.Vertical)
        {
            attributes.Set("aria-orientation", "vertical");
        }

        renderedDirection ??= TabsLiterals.ToLiteral(root.Direction);
        attributes.Set(TabsListDataAttributes.ActivationDirection, renderedDirection);
    }

    /// <inheritdoc />
    protected override bool ForwardsConsumerHandler(string eventName) => composite!.ForwardsConsumerHandler(eventName);

    private void OnRootChanged()
    {
        if (disposed)
        {
            return;
        }

        string orientation = CompositeLiterals.ToLiteral(root!.Orientation);
        bool orientationChanged = !string.Equals(orientation, composite!.Orientation, StringComparison.Ordinal);
        composite.Orientation = orientation;
        if (UpdateState() || orientationChanged)
        {
            StateHasChanged();
        }
    }

    private void OnHandlersChanged()
    {
        // The flags travel in the next update; a Tab that gained a handler renders anyway, and the List renders with it.
        if (!disposed && handle is not null && NextOptions() is not null)
        {
            StateHasChanged();
        }
    }

    // The decision point `select`, on WebAssembly only: the Tab the module handed over runs its consumer handler and its
    // request inside the dispatch (TAB-PTR-09, TAB-EVT-02; ADR 04 Amendments 2026-10-26).
    private DecisionPolicy DecideSelection(EventSnapshot snapshot)
    {
        TabsSelection? pending = twin?.PendingSelection;
        if (pending is null || listContext!.Find(pending.TabId) is not { } tab)
        {
            return DecisionPolicy.Run;
        }

        return tab.DecideActivation(snapshot, TabsLiterals.ParseDirection(pending.Direction));
    }

    // The module's report of an activation no C# chain ran inside the dispatch (Server): the consumer's handler, then the
    // request, a round trip late (ADR 01's "C#-origin consumer vetoes" row).
    private async Task ActivateFromReportAsync(TabsSelection selection)
    {
        if (disposed || selection.Event is null || listContext!.Find(selection.TabId) is not { } tab)
        {
            return;
        }

        try
        {
            await tab.ActivateAsync(selection.Event, TabsLiterals.ParseDirection(selection.Direction));
        }
        catch (Exception exception)
        {
            await DispatchExceptionAsync(exception);
        }
    }

    // A click or focus that activated nothing reaches the Tab's consumer handler (design point 5, settled by the
    // orchestrator 2026-10-26).
    private async Task RunTabEventAsync(TabsTabEvent tabEvent)
    {
        if (disposed || listContext!.Find(tabEvent.TabId) is not { } tab)
        {
            return;
        }

        try
        {
            await tab.RunConsumerHandlerAsync(tabEvent.Event);
        }
        catch (Exception exception)
        {
            await DispatchExceptionAsync(exception);
        }
    }

    private async Task SyncTabsAsync(string? id)
    {
        if (!Enabled)
        {
            if (handle is { } released)
            {
                handle = null;
                sentOptions = null;
                await released.DisposeAsync();
            }

            return;
        }

        if (handle is not null)
        {
            TabsOptions? next = NextOptions();
            if (next is not null)
            {
                sentOptions = next;
                await handle.UpdateAsync(next);
            }

            return;
        }

        string? elementId = Element is null ? id ?? composite!.LateId : null;
        if (Element is null && elementId is null)
        {
            // The composite draws the late id of a component root and renders it; the next render attaches.
            return;
        }

        if (twin is null)
        {
            twin = new TabsTwin(JSRuntime) { SelectionDecider = DecideSelection };
            twin.SelectionReported += onSelection;
            twin.TabEventReported += onTabEvent;
            twin.DirectionChanged += onDirection;
        }

        TabsOptions options = BuildOptions();
        sentOptions = options;
        handle = elementId is null ? await twin.AttachAsync(Element!.Value, options) : await twin.AttachAsync(elementId, options);
        tabsAttachedSource.TrySetResult();
    }

    private TabsOptions BuildOptions()
    {
        return new TabsOptions
        {
            Part = TabsTwin.ListPart,
            Orientation = CompositeLiterals.ToLiteral(root!.Orientation),
            ActivateOnFocus = ActivateOnFocus,
            Controlled = root.Controlled,
            ReportClicks = listContext!.AnyClickHandler,
            ReportFocus = listContext.AnyFocusHandler,
        };
    }

    // Compares before constructing, so an unchanged render allocates no options record (ADR 16 rule 7).
    private TabsOptions? NextOptions()
    {
        if (sentOptions is { } sent
            && string.Equals(sent.Orientation, CompositeLiterals.ToLiteral(root!.Orientation), StringComparison.Ordinal)
            && sent.ActivateOnFocus == ActivateOnFocus
            && sent.ReportClicks == listContext!.AnyClickHandler
            && sent.ReportFocus == listContext.AnyFocusHandler)
        {
            return null;
        }

        return BuildOptions();
    }

    // Returns whether the state changed; a new record is made only then (ADR 16 rule 7).
    private bool UpdateState()
    {
        Orientation orientation = root!.Orientation;
        TabsActivationDirection direction = root.Direction;
        if (state is { } current && current.Orientation == orientation && current.TabActivationDirection == direction)
        {
            return false;
        }

        state = new TabsListState(orientation, direction);
        return true;
    }
}

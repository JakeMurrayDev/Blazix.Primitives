using Blazix.Primitives.Interop;
using Blazix.Primitives.Rendering;
using Blazix.Primitives.Store;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace Blazix.Primitives;

// ACC-STA-03, §3.5 state object; accordion/panel/AccordionPanel.tsx:97-103: the item's state plus the panel's transition status
// (COL-STA-06), mirrored (ADR 04 §2): the collapsible module writes data-starting-style and data-ending-style.
/// <summary>Represents the state of an accordion panel, which the part exposes as data attributes and passes to its class and style functions.</summary>
/// <typeparam name="TValue">The type of the items' values.</typeparam>
/// <param name="Value">The values of the root's open items; never projected as an attribute.</param>
/// <param name="Disabled">A value indicating whether the item ignores user interaction, its own or its root's; projected as <c>data-disabled</c>.</param>
/// <param name="Orientation">The root's orientation; projected as <c>data-orientation</c>.</param>
/// <param name="Hidden">A value indicating whether the panel is closed and its exit has ended; projected as <c>data-hidden</c>.</param>
/// <param name="Index">The item's position among its root's items, or -1 until it is known; projected as <c>data-index</c>.</param>
/// <param name="Open">A value indicating whether the panel is open; projected as <c>data-open</c> or <c>data-closed</c>.</param>
/// <param name="TransitionStatus">
/// The phase of the panel's enter or exit transition, or <see langword="null"/> while none is in progress; it reads
/// <see cref="TransitionStatus.Idle"/> while an open skips its motion. Its <c>data-starting-style</c> and
/// <c>data-ending-style</c> attributes are written by the browser-side behavior.
/// </param>
[PartState]
public sealed partial record AccordionPanelState<TValue>(
    [property: NotProjected] IReadOnlyList<TValue> Value,
    bool Disabled,
    Orientation Orientation,
    bool Hidden,
    int Index,
    bool Open,
    [property: MirroredField] TransitionStatus? TransitionStatus);

// accordion/panel/AccordionPanelDataAttributes.ts, in Base UI's order.
/// <summary>Provides the names of the data attributes an accordion panel renders.</summary>
public static class AccordionPanelDataAttributes
{
    /// <summary>Represents the attribute that carries the item's index: <c>data-index</c>.</summary>
    public const string Index = "data-index";

    /// <summary>Represents the attribute present while the panel is open: <c>data-open</c>.</summary>
    public const string Open = "data-open";

    /// <summary>Represents the attribute that carries the root's orientation: <c>data-orientation</c>.</summary>
    public const string Orientation = "data-orientation";

    /// <summary>Represents the attribute present while the item is disabled: <c>data-disabled</c>.</summary>
    public const string Disabled = "data-disabled";

    /// <summary>Represents the attribute present while the panel begins animating in: <c>data-starting-style</c>.</summary>
    public const string StartingStyle = "data-starting-style";

    /// <summary>Represents the attribute present while the panel is animating out: <c>data-ending-style</c>.</summary>
    public const string EndingStyle = "data-ending-style";
}

// accordion/panel/AccordionPanelCssVars.ts (ACC-DOM-17); ADR 08 §3 companion type, named as CollapsiblePanelCssVars is (settled by
// the orchestrator 2026-10-25).
/// <summary>Provides the names of the CSS variables an accordion panel sets on its element.</summary>
public static class AccordionPanelCssVars
{
    /// <summary>Represents the panel's height: <c>--accordion-panel-height</c>, <c>auto</c> or the measured size in pixels.</summary>
    public const string AccordionPanelHeight = "--accordion-panel-height";

    /// <summary>Represents the panel's width: <c>--accordion-panel-width</c>, <c>auto</c> or the measured size in pixels.</summary>
    public const string AccordionPanelWidth = "--accordion-panel-width";
}

// Base UI's Accordion.Panel (accordion/panel/AccordionPanel.tsx); the contract is docs/components/accordion.md (§3.5, §5.4, §9).
// - The behavior is CollapsiblePanelBehavior over the item's collapsible state ("Accordion.Panel attaches the same module", ADR
//   04 Amendments 2026-10-21), with Accordion's inputs (ADR 04 note dated 2026-10-26): the --accordion-panel-* variables
//   (ACC-DOM-17), an empty id rendered as written and not registered (ACC-API-18), the Accordion message (ACC-EDGE-05) and
//   the status report, which the item's shared state asks for (ACC-STA-03/09; design points D1 and D5, option (a), settled
//   by the orchestrator 2026-10-26).
// - KeepMounted and HiddenUntilFound are bool?: null takes the root's (ACC-API-16/17; settled by the orchestrator 2026-10-25).
// - role="region" and aria-labelledby the item's trigger id (ACC-ARIA-03/04, ACC-STR-06); the consumer's win at the merge, and
//   the behavior's FinishAttributes puts the handed-over style above them (ACC-DOM-18).
// - ACC-EDGE-03: the root's context is looked up first (ACC-EDGE-01's message), then the collapsible state (COL-EDGE-01's).
/// <summary>
/// Represents the panel of an accordion item, Base UI's <c>Accordion.Panel</c>, rendered as a <c>div</c> with
/// <c>role="region"</c>: the content its <see cref="AccordionTrigger{TValue}"/> shows and hides. Its measured size is exposed
/// as the <c>--accordion-panel-height</c> and <c>--accordion-panel-width</c> CSS variables, so that its height or width can
/// be animated between 0 and the content's size.
/// </summary>
/// <typeparam name="TValue">The type of the items' values, which the panel takes from its item.</typeparam>
public sealed class AccordionPanel<TValue> : PartBase<AccordionPanelState<TValue>>
{
    private const string DefaultTag = "div";

    private static readonly StateAttributesMapping<AccordionPanelState<TValue>> s_mapping = AccordionStateMapping.CreateItem<AccordionPanelState<TValue>>();

    private readonly Action stateChanged;
    private readonly Action itemChanged;
    private readonly Action rootChanged;
    private readonly Func<Exception, Task> dispatchException;
    private AccordionRootContext? root;
    private CollapsibleRootContext? collapsible;
    private AccordionItemContext<TValue>? item;
    private CollapsiblePanelBehavior? behavior;
    private StoreSelection<CollapsibleRootStoreState, bool>? open;
    private StoreSelection<CollapsibleRootStoreState, bool>? mounted;
    private StoreSelection<CollapsibleRootStoreState, TransitionStatus?>? status;
    private AccordionItemState<TValue>? renderedItemState;
    private string? renderedTriggerId;
    private AccordionPanelState<TValue>? state;
    private bool rendered;

    /// <summary>Initializes a new instance of the <see cref="AccordionPanel{TValue}"/> class.</summary>
    public AccordionPanel()
    {
        stateChanged = StateHasChanged;
        itemChanged = OnItemChanged;
        rootChanged = OnRootChanged;
        dispatchException = DispatchExceptionAsync;
    }

    [Inject]
    private IJSRuntime JSRuntime { get; set; } = null!;

    [Inject]
    private ILogger<AccordionPanel<TValue>> Logger { get; set; } = null!;

    [Inject]
    private CollapsibleDevelopmentLog DevelopmentLog { get; set; } = null!;

    [CascadingParameter]
    private AccordionRootContext? RootContext { get; set; }

    [CascadingParameter]
    private CollapsibleRootContext? CollapsibleContext { get; set; }

    [CascadingParameter]
    private AccordionItemContext<TValue>? ItemContext { get; set; }

    [CascadingParameter]
    private AccordionItemContext? AnyItemContext { get; set; }

    // ACC-API-16
    /// <summary>Gets or sets a value indicating whether the element stays in the document while the panel is closed.</summary>
    /// <value>
    /// <see langword="true"/> to keep the closed panel in the document, hidden with the <c>hidden</c> attribute;
    /// <see langword="false"/> to remove it once its closing transition has finished; <see langword="null"/>, the default,
    /// to follow the root's <c>KeepMounted</c>.
    /// </value>
    [Parameter]
    public bool? KeepMounted { get; set; }

    // ACC-API-17
    /// <summary>Gets or sets a value indicating whether the browser's find-in-page can find and expand the closed panel's content.</summary>
    /// <value>
    /// <see langword="true"/> to keep the closed panel in the document hidden with <c>hidden="until-found"</c>, which implies
    /// <see cref="KeepMounted"/>; <see langword="false"/> to turn it off whatever the root says; <see langword="null"/>, the
    /// default, to follow the root's <c>HiddenUntilFound</c>.
    /// </value>
    [Parameter]
    public bool? HiddenUntilFound { get; set; }

    /// <inheritdoc />
    protected override string DefaultTagName => DefaultTag;

    /// <inheritdoc />
    protected override AccordionPanelState<TValue> State
    {
        get
        {
            AccordionItemState<TValue> current = item!.State;
            TransitionStatus? panelStatus = behavior!.Status(status!.Value);
            if (state is null || !ReferenceEquals(current, renderedItemState) || state.TransitionStatus != panelStatus)
            {
                renderedItemState = current;
                state = new AccordionPanelState<TValue>(current.Value, current.Disabled, current.Orientation, current.Hidden, current.Index, current.Open, panelStatus);
            }

            return state;
        }
    }

    /// <inheritdoc />
    protected override StateAttributesMapping<AccordionPanelState<TValue>>? StateAttributesMapping => s_mapping;

    // For the fixtures: completes once the collapsible behavior is attached to the panel's element.
    /// <summary>Gets a task that completes when the panel's behavior is first attached to its element.</summary>
    /// <value>A task that completes after the first interactive render that showed the element.</value>
    internal Task TwinAttached => behavior!.TwinAttached;

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">
    /// The panel is not inside an <see cref="AccordionRoot{TValue}"/>, an <see cref="AccordionItem{TValue}"/>, or an item of its value type.
    /// </exception>
    protected override void OnInitialized()
    {
        root = RootContext ?? throw AccordionRoot<TValue>.MissingRoot();
        collapsible = CollapsibleContext ?? throw CollapsiblePanel.MissingRoot();
        item = ItemContext ?? throw (AnyItemContext is null ? AccordionItem<TValue>.MissingItem() : AccordionRoot<TValue>.TypeMismatch("AccordionPanel"));
        behavior = new CollapsiblePanelBehavior(collapsible, JSRuntime, DevelopmentLog, Logger, stateChanged, dispatchException, CollapsiblePanelVariables.Accordion)
        {
            RendersEmptyId = true,
            KeepMountedIgnoredMessage = CollapsibleDevelopmentLog.AccordionPanelKeepMountedIgnoredMessage,
        };
        item.AddListener(itemChanged);
        root.AddListener(rootChanged);
        base.OnInitialized();
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        ApplyEffectiveFlags();
        behavior!.SetId(AdditionalAttributes is not null && AdditionalAttributes.TryGetValue("id", out object? id) ? id as string : null);
    }

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        rendered = Enabled && behavior!.RendersElement(open!.Value, mounted!.Value);
        if (!rendered)
        {
            // ACC-DOM-16: nothing is rendered; the id registration stays.
            ((IPartHost)this).SetElement(null);
            return;
        }

        base.BuildRenderTree(builder);
    }

    /// <inheritdoc />
    protected override Task OnAfterRenderAsync(bool firstRender)
    {
        return behavior!.AfterRenderAsync(rendered, Element);
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            item?.RemoveListener(itemChanged);
            root?.RemoveListener(rootChanged);
            behavior?.Dispose();
        }

        base.Dispose(disposing);
    }

    /// <inheritdoc />
    protected override void SubscribeSelectors()
    {
        SelectorSubscription<CollapsibleRootStoreState> subscription = SubscribeTo(collapsible!);
        open = subscription.Select(CollapsibleRootStoreState.OpenSelector);
        mounted = subscription.Select(CollapsibleRootStoreState.MountedSelector);
        status = subscription.Select(CollapsibleRootStoreState.StatusSelector);
    }

    /// <inheritdoc />
    protected override void AddPartAttributes(AttributeList attributes)
    {
        behavior!.AddAttributes(attributes, open!.Value, mounted!.Value, behavior.Status(status!.Value));
        attributes.Set("role", "region");
        renderedTriggerId = item!.TriggerId;
        if (renderedTriggerId is not null)
        {
            attributes.Set("aria-labelledby", renderedTriggerId);
        }
    }

    /// <inheritdoc />
    protected override void FinishAttributes(AttributeList attributes)
    {
        behavior!.FinishAttributes(attributes, open!.Value);
    }

    // ACC-API-16/17 with the root's defaults; ACC-EDGE-05: an explicit KeepMounted="false" on the panel with the effective
    // HiddenUntilFound logs once (a root-level false alone logs nothing).
    private void ApplyEffectiveFlags()
    {
        bool hiddenUntilFound = HiddenUntilFound ?? root!.HiddenUntilFound;
        behavior!.KeepMounted = KeepMounted ?? root!.KeepMounted;
        behavior.HiddenUntilFound = hiddenUntilFound;
        if (KeepMounted == false && hiddenUntilFound)
        {
            behavior.ReportKeepMountedIgnored();
        }
    }

    private void OnItemChanged()
    {
        if (!ReferenceEquals(item!.State, renderedItemState) || !string.Equals(item.TriggerId, renderedTriggerId, StringComparison.Ordinal))
        {
            StateHasChanged();
        }
    }

    private void OnRootChanged()
    {
        bool keepMounted = behavior!.KeepMounted;
        bool hiddenUntilFound = behavior.HiddenUntilFound;
        ApplyEffectiveFlags();
        if (keepMounted != behavior.KeepMounted || hiddenUntilFound != behavior.HiddenUntilFound)
        {
            StateHasChanged();
        }
    }
}

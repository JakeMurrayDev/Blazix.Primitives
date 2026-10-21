using Blazix.Primitives.Rendering;
using Blazix.Primitives.Store;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace Blazix.Primitives;

// COL-STA-06/07, §3.3 state object; collapsible/panel/CollapsiblePanel.tsx:92-95,135-140. TransitionStatus is mirrored
// (ADR 04 §2): the collapsible module writes data-starting-style/data-ending-style.
/// <summary>Represents the state of a collapsible panel, which the part exposes as data attributes and passes to its class and style functions.</summary>
/// <param name="Open">A value indicating whether the panel is open; projected as <c>data-open</c> or <c>data-closed</c>.</param>
/// <param name="Disabled">A value indicating whether the collapsible ignores user interaction; projected as <c>data-disabled</c>.</param>
/// <param name="TransitionStatus">
/// The phase of the panel's enter or exit transition, or <see langword="null"/> while none is in progress; it reads
/// <see cref="TransitionStatus.Idle"/> while an open skips its motion. Its <c>data-starting-style</c> and
/// <c>data-ending-style</c> attributes are written by the browser-side behavior.
/// </param>
[PartState]
public sealed partial record CollapsiblePanelState(bool Open, bool Disabled, [property: MirroredField] TransitionStatus? TransitionStatus);

// collapsible/panel/CollapsiblePanelDataAttributes.ts (COL-DOM-11/12).
/// <summary>Provides the names of the data attributes a collapsible panel renders.</summary>
public static class CollapsiblePanelDataAttributes
{
    /// <summary>Represents the attribute present while the panel is open: <c>data-open</c>.</summary>
    public const string Open = "data-open";

    /// <summary>Represents the attribute present while the panel is closed: <c>data-closed</c>.</summary>
    public const string Closed = "data-closed";

    /// <summary>Represents the attribute present while the panel begins animating in: <c>data-starting-style</c>.</summary>
    public const string StartingStyle = "data-starting-style";

    /// <summary>Represents the attribute present while the panel is animating out: <c>data-ending-style</c>.</summary>
    public const string EndingStyle = "data-ending-style";
}

// collapsible/panel/CollapsiblePanelCssVars.ts (COL-DOM-17); ADR 08 §3 companion type.
/// <summary>Provides the names of the CSS variables a collapsible panel sets on its element.</summary>
public static class CollapsiblePanelCssVars
{
    /// <summary>Represents the panel's height: <c>--collapsible-panel-height</c>, <c>auto</c> or the measured size in pixels.</summary>
    public const string CollapsiblePanelHeight = "--collapsible-panel-height";

    /// <summary>Represents the panel's width: <c>--collapsible-panel-width</c>, <c>auto</c> or the measured size in pixels.</summary>
    public const string CollapsiblePanelWidth = "--collapsible-panel-width";
}

// Base UI's Collapsible.Panel (collapsible/panel/CollapsiblePanel.tsx); the contract is docs/components/collapsible.md
// (§3.3, §5.3, §9). The behavior is CollapsiblePanelBehavior, which Accordion.Panel reuses; the measurement passes are the
// `collapsible` module's (one CollapsibleTwin per part). The part renders nothing while the panel is closed, unmounted and
// not kept (COL-DOM-16), keeping its id registration. COL-STR-03, COL-EDGE-01: the part throws outside a root.
/// <summary>
/// Represents the panel of a collapsible, Base UI's <c>Collapsible.Panel</c>, rendered as a <c>div</c>: the content
/// that its <see cref="CollapsibleTrigger"/> shows and hides. Its measured size is exposed as the
/// <c>--collapsible-panel-height</c> and <c>--collapsible-panel-width</c> CSS variables, so that its height or width can be
/// animated between 0 and the content's size.
/// </summary>
public sealed class CollapsiblePanel : PartBase<CollapsiblePanelState>
{
    private const string DefaultTag = "div";

    private static readonly StateDataAttribute[] s_open = [new(CollapsiblePanelDataAttributes.Open, string.Empty)];
    private static readonly StateDataAttribute[] s_closed = [new(CollapsiblePanelDataAttributes.Closed, string.Empty)];
    private static readonly StateAttributesMapping<CollapsiblePanelState> s_mapping = new StateAttributesMapping<CollapsiblePanelState>()
        .Map<bool>(nameof(CollapsiblePanelState.Open), static open => open ? s_open : s_closed);

    private readonly Action stateChanged;
    private readonly Func<Exception, Task> dispatchException;
    private CollapsiblePanelBehavior? behavior;
    private StoreSelection<CollapsibleRootStoreState, bool>? open;
    private StoreSelection<CollapsibleRootStoreState, bool>? disabled;
    private StoreSelection<CollapsibleRootStoreState, bool>? mounted;
    private StoreSelection<CollapsibleRootStoreState, TransitionStatus?>? status;
    private CollapsiblePanelState? state;
    private bool rendered;
    private bool pendingKeepMountedWarning;

    /// <summary>Initializes a new instance of the <see cref="CollapsiblePanel"/> class.</summary>
    public CollapsiblePanel()
    {
        stateChanged = StateHasChanged;
        dispatchException = DispatchExceptionAsync;
    }

    [Inject]
    private IJSRuntime JSRuntime { get; set; } = null!;

    [Inject]
    private ILogger<CollapsiblePanel> Logger { get; set; } = null!;

    [Inject]
    private CollapsibleDevelopmentLog DevelopmentLog { get; set; } = null!;

    [CascadingParameter]
    private CollapsibleRootContext? RootContext { get; set; }

    // COL-API-08
    /// <summary>Gets or sets a value indicating whether the element stays in the document while the panel is closed.</summary>
    /// <value>
    /// <see langword="true"/> to keep the closed panel in the document, hidden with the <c>hidden</c> attribute; the default
    /// is <see langword="false"/>, which removes it once its closing transition has finished.
    /// </value>
    [Parameter]
    public bool KeepMounted { get; set; }

    // COL-API-09
    /// <summary>Gets or sets a value indicating whether the browser's find-in-page can find and expand the closed panel's content.</summary>
    /// <value>
    /// <see langword="true"/> to keep the closed panel in the document hidden with <c>hidden="until-found"</c>, which
    /// implies <see cref="KeepMounted"/>; the default is <see langword="false"/>.
    /// </value>
    [Parameter]
    public bool HiddenUntilFound { get; set; }

    /// <inheritdoc />
    protected override string DefaultTagName => DefaultTag;

    /// <inheritdoc />
    protected override CollapsiblePanelState State
    {
        get
        {
            TransitionStatus? panelStatus = behavior!.Status(status!.Value);
            if (state is null || state.Open != open!.Value || state.Disabled != disabled!.Value || state.TransitionStatus != panelStatus)
            {
                state = new CollapsiblePanelState(open!.Value, disabled!.Value, panelStatus);
            }

            return state;
        }
    }

    /// <inheritdoc />
    protected override StateAttributesMapping<CollapsiblePanelState>? StateAttributesMapping => s_mapping;

    // For the fixtures: completes once the collapsible behavior is attached to the panel's element.
    /// <summary>Gets a task that completes when the panel's behavior is first attached to its element.</summary>
    /// <value>A task that completes after the first interactive render that showed the element.</value>
    internal Task TwinAttached => behavior!.TwinAttached;

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">The panel is not inside a <see cref="CollapsibleRoot"/>.</exception>
    public override Task SetParametersAsync(ParameterView parameters)
    {
        // COL-EDGE-02: HiddenUntilFound overrides an explicit KeepMounted="false".
        if (parameters.TryGetValue(nameof(KeepMounted), out bool keepMounted) && !keepMounted
            && parameters.TryGetValue(nameof(HiddenUntilFound), out bool hiddenUntilFound) && hiddenUntilFound)
        {
            pendingKeepMountedWarning = true;
        }

        return base.SetParametersAsync(parameters);
    }

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        CollapsibleRootContext root = RootContext ?? throw MissingRoot();
        behavior = new CollapsiblePanelBehavior(root, JSRuntime, DevelopmentLog, Logger, stateChanged, dispatchException);
        base.OnInitialized();
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        behavior!.KeepMounted = KeepMounted;
        behavior.HiddenUntilFound = HiddenUntilFound;
        behavior.SetId(AdditionalAttributes is not null && AdditionalAttributes.TryGetValue("id", out object? id) ? id as string : null);
        if (pendingKeepMountedWarning)
        {
            pendingKeepMountedWarning = false;
            behavior.ReportKeepMountedIgnored();
        }
    }

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        rendered = Enabled && behavior!.RendersElement(open!.Value, mounted!.Value);
        if (!rendered)
        {
            // COL-DOM-16: nothing is rendered; the id registration stays.
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
            behavior?.Dispose();
        }

        base.Dispose(disposing);
    }

    /// <inheritdoc />
    protected override void SubscribeSelectors()
    {
        SelectorSubscription<CollapsibleRootStoreState> subscription = SubscribeTo(RootContext!);
        open = subscription.Select(CollapsibleRootStoreState.OpenSelector);
        disabled = subscription.Select(CollapsibleRootStoreState.DisabledSelector);
        mounted = subscription.Select(CollapsibleRootStoreState.MountedSelector);
        status = subscription.Select(CollapsibleRootStoreState.StatusSelector);
    }

    /// <inheritdoc />
    protected override void AddPartAttributes(AttributeList attributes)
    {
        behavior!.AddAttributes(attributes, open!.Value, mounted!.Value, behavior.Status(status!.Value));
    }

    /// <inheritdoc />
    protected override void FinishAttributes(AttributeList attributes)
    {
        behavior!.FinishAttributes(attributes, open!.Value);
    }

    internal static InvalidOperationException MissingRoot() =>
        new("Blazix: The `CollapsibleRoot` context is missing. Collapsible parts must be placed within `<CollapsibleRoot>`.");
}

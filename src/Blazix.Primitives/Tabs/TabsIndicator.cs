using Blazix.Primitives.Interop;
using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.JSInterop;

namespace Blazix.Primitives;

// TAB-STA-20, TAB-DOM-22/24; tabs/indicator/TabsIndicator.tsx:19-23,139-144. ADR 04 §2 (the Tabs.Indicator row) and
// Amendments 2026-10-26: the `tabs` module writes the six --active-tab-* variables, `hidden` and
// data-activation-direction, so the measurement and the direction are mirrored fields (RND-STA-01 row of ADR 01); the
// measurement is never projected (TAB-DOM-22). Design point 2 of the Tabs track (record classes, one report per change)
// was settled by the orchestrator 2026-10-26.
/// <summary>Represents the state of a tab indicator, which the part exposes as data attributes and passes to its class and style functions.</summary>
/// <param name="Orientation">The axis along which the tabs are laid out; projected as <c>data-orientation</c>.</param>
/// <param name="ActiveTabPosition">The active tab's offsets the browser-side behavior last measured, or <see langword="null"/> while none is measurable.</param>
/// <param name="ActiveTabSize">The active tab's size the browser-side behavior last measured, or <see langword="null"/> while none is measurable.</param>
/// <param name="TabActivationDirection">The direction from the previously active tab to the active one, rendered as <c>data-activation-direction</c> by the browser-side behavior.</param>
[PartState]
public sealed partial record TabsIndicatorState(
    Orientation Orientation,
    [property: MirroredField] TabsTabPosition? ActiveTabPosition,
    [property: MirroredField] TabsTabSize? ActiveTabSize,
    [property: MirroredField] TabsActivationDirection TabActivationDirection);

// TAB §3.4; tabs/indicator/TabsIndicatorDataAttributes.ts, in its order.
/// <summary>Provides the names of the data attributes a tab indicator renders.</summary>
public static class TabsIndicatorDataAttributes
{
    /// <summary>Represents the attribute whose value is the activation direction, <c>left</c>, <c>right</c>, <c>up</c>, <c>down</c> or <c>none</c>: <c>data-activation-direction</c>.</summary>
    public const string ActivationDirection = "data-activation-direction";

    /// <summary>Represents the attribute whose value is the orientation, <c>horizontal</c> or <c>vertical</c>: <c>data-orientation</c>.</summary>
    public const string Orientation = "data-orientation";
}

// TAB §3.4; tabs/indicator/TabsIndicatorCssVars.ts, in its order (Base UI's TabsIndicatorCssVariables; ADR 08 §3's
// `<Part>CssVars` row).
/// <summary>Provides the names of the CSS variables a tab indicator sets on itself.</summary>
public static class TabsIndicatorCssVars
{
    /// <summary>Represents the variable holding the active tab's distance from the list's left edge: <c>--active-tab-left</c>.</summary>
    public const string ActiveTabLeft = "--active-tab-left";

    /// <summary>Represents the variable holding the active tab's distance from the right edge of the list's content: <c>--active-tab-right</c>.</summary>
    public const string ActiveTabRight = "--active-tab-right";

    /// <summary>Represents the variable holding the active tab's distance from the list's top edge: <c>--active-tab-top</c>.</summary>
    public const string ActiveTabTop = "--active-tab-top";

    /// <summary>Represents the variable holding the active tab's distance from the bottom edge of the list's content: <c>--active-tab-bottom</c>.</summary>
    public const string ActiveTabBottom = "--active-tab-bottom";

    /// <summary>Represents the variable holding the active tab's width: <c>--active-tab-width</c>.</summary>
    public const string ActiveTabWidth = "--active-tab-width";

    /// <summary>Represents the variable holding the active tab's height: <c>--active-tab-height</c>.</summary>
    public const string ActiveTabHeight = "--active-tab-height";
}

// Base UI's Tabs.Indicator (tabs/indicator/TabsIndicator.tsx); the contract is docs/components/tabs.md §5.5.
// - A `span` with role="presentation" (TAB-ARIA-09), rendered while the root holds a value (TAB-DOM-19).
// - Its own TabsTwin, an Indicator handle of the `tabs` module (ADR 04 Amendments 2026-10-26, chosen by the user
//   2026-10-26): from attach the module measures the active Tab on every batch that moves `[data-active]` and on every
//   resize notification of the List, and writes `hidden` and the element's whole style (the consumer's string, sent as
//   Style, under the six variables' precedence of TAB-DOM-32). C# renders `hidden` and the consumer's merged style under
//   the handover (ADR 04 Amendments 2026-10-18) and freezes both from attach, so Blazor never writes them over the
//   module; a consumer `hidden` is sent as Hidden and the module never writes over it (TAB-DOM-23). The prerendered and
//   the first interactive Indicator are hidden with no variables (TAB-EDGE-09; no pre-hydration script, ADR 06 Amendments
//   2026-10-26).
// - The measurement is mirrored once per report (design point 2 of the Tabs track, settled by the orchestrator
//   2026-10-26): a report renders the Indicator alone, never the List or the Tabs (TAB-DOM-31 restated as a mirror
//   count).
/// <summary>
/// Represents a visual marker of the active tab: Base UI's <c>Tabs.Indicator</c>, rendered as a <c>span</c> that carries
/// the active tab's position and size as CSS variables, so it can be styled to follow the active tab.
/// </summary>
/// <remarks>The part must be placed within a <see cref="TabsList"/>.</remarks>
public sealed class TabsIndicator : PartBase<TabsIndicatorState>
{
    private const string DefaultTag = "span";

    private readonly Action rootChanged;
    private readonly Action<TabsIndicatorMeasurement> onMeasurement;
    private readonly TaskCompletionSource attachedSource = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private TabsRootContext? root;
    private TabsIndicatorState? state;
    private TabsTabPosition? position;
    private TabsTabSize? size;
    private string? renderedDirection;
    private string? consumerStyle;
    private bool? consumerHidden;
    private bool frozen;
    private string? frozenStyle;
    private bool frozenHidden;
    private string? renderedStyle;
    private bool renderedHidden;
    private bool renderedShown;
    private TabsTwin? twin;
    private TwinHandle<TabsOptions>? handle;
    private TabsOptions? sentOptions;
    private bool disposed;

    /// <summary>Initializes a new instance of the <see cref="TabsIndicator"/> class.</summary>
    public TabsIndicator()
    {
        rootChanged = OnRootChanged;
        onMeasurement = measurement => _ = InvokeAsync(() => MirrorMeasurement(measurement));
    }

    [Inject]
    private IJSRuntime JSRuntime { get; set; } = null!;

    [CascadingParameter]
    private TabsRootContext? RootContext { get; set; }

    [CascadingParameter]
    private TabsListContext? ListContext { get; set; }

    /// <inheritdoc />
    protected override string DefaultTagName => DefaultTag;

    /// <inheritdoc />
    protected override TabsIndicatorState State => state!;

    // ADR 02 decision 1: under a substitute callback the twin finds the element by id.
    /// <inheritdoc />
    protected override bool UsesGeneratedId => RenderFunction is not null;

    // For the fixtures (tests/Blazix.Primitives.Fixtures.Client): a browser test waits for the module before it measures.
    /// <summary>Gets a task that completes when the indicator's tabs behavior is first attached to its element.</summary>
    /// <value>A task that completes after the first interactive render that showed the element.</value>
    internal Task TwinAttached => attachedSource.Task;

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">The indicator is not placed within a <see cref="TabsRoot{TValue}"/> and a <see cref="TabsList"/>.</exception>
    protected override void OnInitialized()
    {
        // TAB-EDGE-01, then TAB-EDGE-02 (tabs/indicator/TabsIndicator.tsx:47-50: the Root lookup first).
        root = TabsRootContext.Require(RootContext);
        TabsListContext.Require(ListContext);
        root.AddListener(rootChanged);
        base.OnInitialized();
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        consumerHidden = FindConsumerHidden();
        UpdateState();
    }

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        // TAB-DOM-19: nothing while the root's value is null; a later element is created with the direction of its own
        // first render.
        renderedShown = root!.HasValue;
        if (!renderedShown)
        {
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

        await SyncTwinAsync();
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing && !disposed)
        {
            disposed = true;
            root?.RemoveListener(rootChanged);
            if (twin is not null)
            {
                twin.MeasurementChanged -= onMeasurement;
                _ = twin.DisposeAsync().AsTask();
                twin = null;
                handle = null;
            }
        }

        base.Dispose(disposing);
    }

    /// <inheritdoc />
    protected override void AddPartAttributes(AttributeList attributes)
    {
        // TAB-ARIA-09, TAB-DOM-23: the consumer's role and hidden still win at the merge.
        attributes.Set("role", "presentation");
        attributes.Set("hidden", true);

        // ADR 04 Amendments 2026-10-18 and 2026-10-26: the direction the element is created with (the handover).
        renderedDirection ??= TabsLiterals.ToLiteral(root!.Direction);
        attributes.Set(TabsIndicatorDataAttributes.ActivationDirection, renderedDirection);
    }

    // ADR 02 Amendments 2026-10-21 (FinishAttributes), ADR 04 Amendments 2026-10-18 (the handover): `hidden` and `style` are
    // the module's from attach, so the values the element was attached with are rendered from then on; the consumer's later
    // values travel as Hidden and Style.
    /// <inheritdoc />
    protected override void FinishAttributes(AttributeList attributes)
    {
        consumerStyle = attributes.TryGetValue(MergeProps.StyleAttribute, out AttributeValue style) && style.TryGetValue(out string text) ? text : null;
        bool hidden = consumerHidden ?? true;
        string? rendered = frozen ? frozenStyle : consumerStyle;
        renderedHidden = frozen ? frozenHidden : hidden;
        renderedStyle = rendered;
        if (rendered is null)
        {
            attributes.Remove(MergeProps.StyleAttribute);
        }
        else
        {
            attributes.Set(MergeProps.StyleAttribute, rendered);
        }

        attributes.Set("hidden", renderedHidden);
    }

    private bool? FindConsumerHidden()
    {
        if (AdditionalAttributes is null || !AdditionalAttributes.TryGetValue("hidden", out object? value))
        {
            return null;
        }

        return value switch
        {
            bool flag => flag,
            null => false,
            string text => !string.Equals(text, "false", StringComparison.OrdinalIgnoreCase),
            _ => true,
        };
    }

    private void OnRootChanged()
    {
        if (disposed)
        {
            return;
        }

        if (UpdateState() || root!.HasValue != renderedShown)
        {
            StateHasChanged();
        }
    }

    private void MirrorMeasurement(TabsIndicatorMeasurement measurement)
    {
        if (disposed)
        {
            return;
        }

        position = measurement.Position;
        size = measurement.Size;
        if (UpdateState())
        {
            StateHasChanged();
        }
    }

    private async Task SyncTwinAsync()
    {
        bool shown = root!.HasValue && Enabled;
        string? elementId = Element is null && shown ? GeneratedId : null;
        string? key = Element?.Id ?? elementId;
        if (!shown || key is null || (handle is not null && !string.Equals(handle.Key, key, StringComparison.Ordinal)))
        {
            if (handle is { } released)
            {
                handle = null;
                sentOptions = null;
                frozen = false;
                frozenStyle = null;
                await released.DisposeAsync();
            }

            if (!shown || key is null)
            {
                // A new element shows no measurement until the module measures it.
                if (position is not null || size is not null)
                {
                    position = null;
                    size = null;
                    UpdateState();
                }

                return;
            }
        }

        if (handle is not null)
        {
            if (sentOptions is { } sent && sent.Hidden == consumerHidden && string.Equals(sent.Style, consumerStyle, StringComparison.Ordinal))
            {
                return;
            }

            sentOptions = BuildOptions();
            await handle.UpdateAsync(sentOptions);
            return;
        }

        if (twin is null)
        {
            twin = new TabsTwin(JSRuntime);
            twin.MeasurementChanged += onMeasurement;
        }

        TabsOptions options = BuildOptions();
        sentOptions = options;

        // Frozen from the attach call on: a render that lands before the module answers keeps the values it attached with.
        frozen = true;
        frozenStyle = renderedStyle;
        frozenHidden = renderedHidden;
        handle = Element is { } element ? await twin.AttachAsync(element, options) : await twin.AttachAsync(elementId!, options);
        attachedSource.TrySetResult();
    }

    private TabsOptions BuildOptions()
    {
        return new TabsOptions
        {
            Part = TabsTwin.IndicatorPart,
            Hidden = consumerHidden,
            Style = consumerStyle,
        };
    }

    // Returns whether the state changed; a new record is made only then (ADR 16 rule 7).
    private bool UpdateState()
    {
        Orientation orientation = root!.Orientation;
        TabsActivationDirection direction = root.Direction;
        if (state is { } current
            && current.Orientation == orientation
            && current.TabActivationDirection == direction
            && Equals(current.ActiveTabPosition, position)
            && Equals(current.ActiveTabSize, size))
        {
            return false;
        }

        state = new TabsIndicatorState(orientation, position, size, direction);
        return true;
    }
}

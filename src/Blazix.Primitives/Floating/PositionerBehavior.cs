using System.Text;
using Blazix.Primitives.Interop;
using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Blazix.Primitives;

// The C# half of floating positioning a family's Positioner part composes (floating-and-positioning.md, FLT; ADR 09
// Amendments 2026-10-27: the folder Floating/; composed into the part as ButtonBehavior is, never a nested component, ADR
// 16 rule 10). Base UI's useAnchorPositioning and usePositioner run in the `positioning` module (ADR 03, ADR 01 decision
// 2); C# renders what Base UI renders before the first positioning pass and freezes it at attach (ADR 04 Amendments
// 2026-10-18 and 2026-10-27, chosen by the user 2026-10-27; the TabsIndicator precedent):
// - role="presentation" and `hidden` while not mounted (FLT-DOM-01; C#'s, live);
// - the requested data-side and data-align (FLT-POS-01, FLT-I18N-02), then the module's from attach;
// - the style: FLT-DOM-02's `position: fixed; top: 0px; left: 0px; opacity: 0`, FLT-DOM-05's seeded
//   `--available-width: 100vw; --available-height: 100vh`, `pointer-events: none` while closed (FLT-DOM-01) and
//   `transition: none` while starting (FLT-ANI-05), then the consumer's merged style, which wins (usePositioner's merge
//   order). From attach the module composes the element's style and the consumer's later styles travel in `update`.
// One `update` per change of the options (value equality, ADR 04 and ADR 16 Amendments 2026-10-27). The module's report
// is mirrored once per change: the Positioner renders again only when its side, align or anchorHidden changed, the Arrow
// only when its side, align or uncentered changed (the render-count lesson of issue 110). The element cases of AnchorValue
// and CollisionBoundaryValue cross beside the options as references (ADR 04 Amendments 2026-10-27, element-typed option
// values): the options carry their indexes, so an update is also sent when only an element changed. Under a Portal the
// first attach awaits the portal's move (settled by the orchestrator 2026-10-27, the Toolbar first-attach precedent):
// until the portal module has moved its node, the node is `display: none` in place and a pass would measure nothing.
/// <summary>
/// The positioning mechanism of one positioner part: its <see cref="PositioningTwin"/>, the attributes and style rendered
/// before the first positioning pass and frozen at attach, the reading direction, the arrow's registration through
/// <see cref="Context"/> and the mirror of the module's report.
/// </summary>
internal sealed class PositionerBehavior : IDisposable
{
    private readonly IJSRuntime jsRuntime;
    private readonly Action stateChanged;
    private readonly Func<Action, Task> invokeAsync;
    private readonly Action directionChanged;
    private readonly Action<PositioningPlacement> onPlacement;
    private readonly TaskCompletionSource attachedSource = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private PositionerInputs inputs = new();
    private PositionerMirror? mirror;
    private DirectionProviderContext? directionContext;
    private PopupPortalContext? portal;
    private string? consumerStyle;
    private string? renderedStyle;
    private string? renderedSide;
    private string? renderedAlign;
    private bool frozen;
    private string? frozenStyle;
    private string? frozenSide;
    private string? frozenAlign;
    private PositioningTwin? twin;
    private TwinHandle<PositioningOptions>? handle;
    private PositioningOptions? sentOptions;
    private AnchorValue? sentAnchor;
    private CollisionBoundaryValue? sentBoundary;
    private ElementReference? target;
    private string? targetId;
    private bool attaching;
    private bool dirty;
    private bool disposed;

    /// <summary>Initializes a new instance of the <see cref="PositionerBehavior"/> class.</summary>
    /// <param name="jsRuntime">The runtime the twin imports its module through.</param>
    /// <param name="stateChanged">Renders the positioner again; called when its side, alignment or hidden anchor mirror changes.</param>
    /// <param name="invokeAsync">Runs work on the part's renderer, as <c>ComponentBase.InvokeAsync</c>.</param>
    public PositionerBehavior(IJSRuntime jsRuntime, Action stateChanged, Func<Action, Task> invokeAsync)
    {
        this.jsRuntime = jsRuntime;
        this.stateChanged = stateChanged;
        this.invokeAsync = invokeAsync;
        directionChanged = OnDirectionChanged;
        onPlacement = placement => _ = invokeAsync(() => ApplyPlacement(placement));
        Context = new PositionerContext(this);
    }

    /// <summary>Gets the state the positioner shares with its arrow; the part cascades it to its content.</summary>
    /// <value>The context.</value>
    public PositionerContext Context { get; }

    /// <summary>Gets the placement the browser-side behavior last rendered.</summary>
    /// <value>
    /// The mirrored side, alignment, hidden anchor and uncentered arrow; before the first report, the requested side and
    /// alignment, a shown anchor and an uncentered arrow.
    /// </value>
    public PositionerMirror Mirror => mirror ??= InitialMirror();

    /// <summary>Gets the reading direction the logical sides resolve with.</summary>
    /// <value>The nearest provider's direction, or <see cref="TextDirection.Ltr"/> without one.</value>
    public TextDirection Direction => DirectionProviderContext.Read(directionContext);

    /// <summary>Gets a task that completes when the behavior is first attached to the positioner's element.</summary>
    /// <value>A task that completes after the first interactive render that showed the element.</value>
    public Task TwinAttached => attachedSource.Task;

    /// <inheritdoc />
    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        directionContext?.RemoveListener(directionChanged);
        directionContext = null;
        if (twin is not null)
        {
            twin.PlacementChanged -= onPlacement;
            _ = twin.DisposeAsync().AsTask();
            twin = null;
            handle = null;
        }
    }

    /// <summary>Subscribes to the nearest direction provider, so a change of direction is sent.</summary>
    /// <param name="context">The cascaded context, or <see langword="null"/> without a provider.</param>
    public void SetDirectionContext(DirectionProviderContext? context)
    {
        if (ReferenceEquals(context, directionContext) || disposed)
        {
            return;
        }

        directionContext?.RemoveListener(directionChanged);
        directionContext = context;
        directionContext?.AddListener(directionChanged);
    }

    /// <summary>Sets the enclosing portal's context, whose move the first attach awaits.</summary>
    /// <param name="context">The cascaded context, or <see langword="null"/> outside any portal.</param>
    public void SetPortalContext(PopupPortalContext? context) => portal = context;

    /// <summary>Sets the inputs of this parameter set; the part's after-render sends them when they changed the options.</summary>
    /// <param name="next">The inputs.</param>
    public void SetInputs(PositionerInputs next)
    {
        if (!next.Equals(inputs))
        {
            inputs = next;
            dirty = true;
        }

        mirror ??= InitialMirror();
    }

    /// <summary>Adds the positioner's own attributes to a render: <c>role</c>, <c>hidden</c> while not mounted, and the placement.</summary>
    /// <param name="attributes">The part-supplied attributes.</param>
    public void AddAttributes(AttributeList attributes)
    {
        attributes.Set("role", "presentation");
        if (!inputs.Mounted)
        {
            attributes.Set("hidden", true);
        }

        renderedSide = frozen ? frozenSide : FloatingLiterals.ToLiteral(inputs.Side ?? Side.Bottom);
        renderedAlign = frozen ? frozenAlign : FloatingLiterals.ToLiteral(inputs.Align ?? Align.Center);
        attributes.Set("data-side", renderedSide!);
        attributes.Set("data-align", renderedAlign!);
    }

    // ADR 02 Amendments 2026-10-21 (FinishAttributes), ADR 04 Amendments 2026-10-18 and 2026-10-27 (the handover): the
    // style, data-side, data-align and data-anchor-hidden are the module's from attach, so the values the element was
    // attached with are rendered from then on, whatever the state projection or the consumer supplied.
    /// <summary>Renders the handed-over attributes over the merged ones: the pre-position style, or the values frozen at attach.</summary>
    /// <param name="attributes">The merged attributes.</param>
    public void FinishAttributes(AttributeList attributes)
    {
        string? merged = attributes.TryGetValue(MergeProps.StyleAttribute, out AttributeValue style) && style.TryGetValue(out string text) ? text : null;
        if (!string.Equals(merged, consumerStyle, StringComparison.Ordinal))
        {
            consumerStyle = merged;
            dirty = true;
        }

        renderedStyle = frozen ? frozenStyle : BuildInitialStyle();
        attributes.Set(MergeProps.StyleAttribute, renderedStyle!);
        attributes.Set("data-side", renderedSide!);
        attributes.Set("data-align", renderedAlign!);
        attributes.Remove("data-anchor-hidden");
    }

    /// <summary>Attaches the twin after the first render that showed the element, then sends options that changed.</summary>
    /// <param name="element">The element, or <see langword="null"/> under a substitute callback.</param>
    /// <param name="id">The part's id, which the twin attaches by under a substitute callback.</param>
    /// <returns>A task that completes when the module has the options.</returns>
    public async Task AfterRenderAsync(ElementReference? element, string? id)
    {
        if (disposed)
        {
            return;
        }

        target = element;
        targetId = id;
        string? key = element?.Id ?? id;
        if (key is null || (handle is not null && !string.Equals(handle.Key, key, StringComparison.Ordinal)))
        {
            // The element was removed or replaced: the module let go of the old one; a new one is attached by id.
            await ReleaseAsync();
            if (key is null)
            {
                return;
            }
        }

        if (handle is not null)
        {
            await FlushAsync();
            return;
        }

        if (attaching)
        {
            return;
        }

        Task moved = PopupPortalContext.WhenMoved(portal);
        if (!moved.IsCompleted)
        {
            // A render while the move is pending only records its element; the attach below takes the latest.
            attaching = true;
            try
            {
                await moved;
            }
            finally
            {
                attaching = false;
            }

            element = target;
            id = targetId;
            if (disposed || handle is not null || (element?.Id ?? id) is null)
            {
                return;
            }
        }

        if (twin is null)
        {
            twin = new PositioningTwin(jsRuntime);
            twin.PlacementChanged += onPlacement;
        }

        dirty = false;
        (PositioningOptions options, TwinReferences? references) = BuildOptions();
        Sent(options);

        // Frozen from the attach call on: a render that lands before the module answers keeps the values it attached with.
        frozen = true;
        frozenStyle = renderedStyle;
        frozenSide = renderedSide;
        frozenAlign = renderedAlign;
        attaching = true;
        try
        {
            handle = element is { } attached ? await twin.AttachAsync(attached, options, references) : await twin.AttachAsync(id!, options, references);
        }
        finally
        {
            attaching = false;
        }

        HandOverArrow(options);
        attachedSource.TrySetResult();

        // An arrow or a parameter set that arrived while the attach was in flight.
        await FlushAsync();
    }

    /// <summary>Sends the options when they differ from the last ones sent; nothing for an equal re-render.</summary>
    /// <returns>A task that completes when the module has the options.</returns>
    internal async Task FlushAsync()
    {
        // Only an input, the consumer's style, the direction or the arrow can change the options; while none did, nothing
        // is built (ADR 16 rule 7).
        if (disposed || handle is null || attaching || !dirty)
        {
            return;
        }

        dirty = false;
        (PositioningOptions options, TwinReferences? references) = BuildOptions();
        if (options.Equals(sentOptions) && Nullable.Equals(inputs.Anchor, sentAnchor) && Nullable.Equals(inputs.CollisionBoundary, sentBoundary))
        {
            return;
        }

        Sent(options);
        await handle.UpdateAsync(options, references);
        HandOverArrow(options);
    }

    /// <summary>Records that the arrow's id or style changed, so the next flush builds the options.</summary>
    internal void MarkDirty() => dirty = true;

    /// <summary>Sends the options after the current batch, for a change no render of the positioner carries.</summary>
    internal void ScheduleFlush()
    {
        if (!disposed)
        {
            dirty = true;
            _ = invokeAsync(() => _ = FlushAsync());
        }
    }

    private static void AppendDeclaration(StringBuilder builder, string declaration)
    {
        if (builder.Length > 0)
        {
            builder.Append(' ');
        }

        builder.Append(declaration);
    }

    // An element reference not captured yet has no id to send: the popup positions against the trigger and the clipping
    // ancestors, as Base UI does for an empty handle.
    private static bool IsRendered(ElementReference element) => !string.IsNullOrEmpty(element.Id);

    private PositionerMirror InitialMirror() => new(inputs.Side ?? Side.Bottom, inputs.Align ?? Align.Center, false, true);

    private string BuildInitialStyle()
    {
        var builder = new StringBuilder("position: fixed; top: 0px; left: 0px; --available-width: 100vw; --available-height: 100vh; opacity: 0;");
        if (!inputs.Open)
        {
            AppendDeclaration(builder, "pointer-events: none;");
        }

        if (inputs.TransitionStatus == TransitionStatus.Starting)
        {
            AppendDeclaration(builder, "transition: none;");
        }

        if (!string.IsNullOrEmpty(consumerStyle))
        {
            AppendDeclaration(builder, consumerStyle);
        }

        return builder.ToString();
    }

    // The anchor element is the first reference and the boundary elements follow it, so their indexes are consecutive.
    private (PositioningOptions Options, TwinReferences? References) BuildOptions()
    {
        PositionerArrowBehavior? arrow = Context.Arrow;
        AnchorValue anchor = inputs.Anchor ?? default;
        CollisionBoundaryValue boundary = inputs.CollisionBoundary ?? default;
        CollisionPaddingValue padding = inputs.CollisionPadding ?? default;
        OffsetValue sideOffset = inputs.SideOffset ?? default;
        OffsetValue alignOffset = inputs.AlignOffset ?? default;
        TwinReferences? references = null;
        int? anchorRef = null;
        if (anchor.TryGetValue(out ElementReference anchorElement) && IsRendered(anchorElement))
        {
            references = new();
            anchorRef = references.Add(anchorElement);
        }

        PositioningIndexes? boundaryRefs = null;
        if (boundary.TryGetValue(out ElementReference boundaryElement))
        {
            if (IsRendered(boundaryElement))
            {
                references ??= new();
                boundaryRefs = new PositioningIndexes(references.Add(boundaryElement), 1);
            }
        }
        else if (boundary.TryGetValue(out IReadOnlyList<ElementReference>? boundaryElements))
        {
            references ??= new();
            int start = references.Count;
            for (int i = 0; i < boundaryElements.Count; i++)
            {
                if (IsRendered(boundaryElements[i]))
                {
                    references.Add(boundaryElements[i]);
                }
            }

            boundaryRefs = new PositioningIndexes(start, references.Count - start);
        }

        PositioningOptions options = new()
        {
            SessionToken = inputs.SessionToken,
            KeepMounted = inputs.KeepMounted,
            TransitionStatus = FloatingLiterals.ToLiteral(inputs.TransitionStatus),
            TriggerId = inputs.TriggerId,
            AnchorId = anchor.TryGetValue(out string? anchorId) ? anchorId : null,
            AnchorRef = anchorRef,
            VirtualAnchor = anchor.TryGetValue(out VirtualAnchor? virtualAnchor) ? new PositioningRect(virtualAnchor.X, virtualAnchor.Y, virtualAnchor.Width, virtualAnchor.Height) : null,
            PositionMethod = FloatingLiterals.ToLiteral(inputs.PositionMethod ?? PositionMethod.Absolute),
            Side = FloatingLiterals.ToLiteral(inputs.Side ?? Side.Bottom),
            Align = FloatingLiterals.ToLiteral(inputs.Align ?? Align.Center),
            SideOffset = sideOffset.TryGetValue(out double sidePixels) ? sidePixels : 0,
            SideOffsetFunction = sideOffset.TryGetValue(out Offset? sideForm) ? ToOptions(sideForm) : null,
            AlignOffset = alignOffset.TryGetValue(out double alignPixels) ? alignPixels : 0,
            AlignOffsetFunction = alignOffset.TryGetValue(out Offset? alignForm) ? ToOptions(alignForm) : null,
            CollisionBoundary = boundary.IsClippingAncestors ? "clipping-ancestors" : null,
            BoundaryRect = boundary.TryGetValue(out Rect? rect) ? new PositioningRect(rect.X, rect.Y, rect.Width, rect.Height) : null,
            BoundaryRefs = boundaryRefs,
            CollisionPadding = padding.TryGetValue(out double all) ? all : null,
            CollisionPaddingSides = padding.TryGetValue(out PaddingSides? sides) ? new PositioningPadding(sides.Top, sides.Right, sides.Bottom, sides.Left) : null,
            Sticky = inputs.Sticky,
            ArrowPadding = inputs.ArrowPadding ?? 5,
            DisableAnchorTracking = inputs.DisableAnchorTracking,
            CollisionAvoidance = inputs.CollisionAvoidance is { } avoidance
                ? new PositioningCollisionAvoidance(
                    avoidance.Side is { } side ? FloatingLiterals.ToLiteral(side) : null,
                    avoidance.Align is { } align ? FloatingLiterals.ToLiteral(align) : null,
                    avoidance.FallbackAxisSide is { } fallback ? FloatingLiterals.ToLiteral(fallback) : null)
                : null,
            AdaptiveOrigin = inputs.AdaptiveOrigin,
            Direction = CompositeLiterals.ToLiteral(Direction),
            ArrowId = arrow?.Id,
            ArrowStyle = arrow?.ConsumerStyle,
            Style = consumerStyle,
        };
        return (options, references);
    }

    // The anchor and boundary inputs the sent options were built from: an element that changed at the same index changes
    // the references, not the options.
    private void Sent(PositioningOptions options)
    {
        sentOptions = options;
        sentAnchor = inputs.Anchor;
        sentBoundary = inputs.CollisionBoundary;
    }

    private static PositioningOffset ToOptions(Offset offset)
    {
        PositioningOffsetOverride[]? overrides = null;
        if (offset.Overrides is { Count: > 0 } source)
        {
            overrides = new PositioningOffsetOverride[source.Count];
            for (int i = 0; i < source.Count; i++)
            {
                OffsetOverride entry = source[i];
                overrides[i] = new PositioningOffsetOverride(
                    entry.Side is { } side ? FloatingLiterals.ToLiteral(side) : null,
                    entry.Align is { } align ? FloatingLiterals.ToLiteral(align) : null,
                    entry.Constant,
                    entry.AnchorWidth,
                    entry.AnchorHeight,
                    entry.PositionerWidth,
                    entry.PositionerHeight);
            }
        }

        return new PositioningOffset(offset.Constant, offset.AnchorWidth, offset.AnchorHeight, offset.PositionerWidth, offset.PositionerHeight, overrides);
    }

    // Design point 6, settled by the orchestrator 2026-10-27: the Arrow freezes from the first attach or update that
    // carries its id, which C# knows it sent.
    private void HandOverArrow(PositioningOptions options)
    {
        if (options.ArrowId is { } arrowId && Context.Arrow is { } arrow && string.Equals(arrow.Id, arrowId, StringComparison.Ordinal))
        {
            arrow.HandOver();
        }
    }

    private async Task ReleaseAsync()
    {
        if (handle is not { } released)
        {
            return;
        }

        handle = null;
        sentOptions = null;
        sentAnchor = null;
        sentBoundary = null;
        dirty = true;
        frozen = false;
        frozenStyle = null;
        frozenSide = null;
        frozenAlign = null;
        await released.DisposeAsync();
    }

    // A change of direction changes nothing C# renders (data-side is the requested or the frozen literal), only the
    // options, so it is sent without a render of the part.
    private void OnDirectionChanged() => ScheduleFlush();

    private void ApplyPlacement(PositioningPlacement placement)
    {
        if (disposed)
        {
            return;
        }

        PositionerMirror previous = Mirror;
        var next = new PositionerMirror(FloatingLiterals.ParseSide(placement.Side), FloatingLiterals.ParseAlign(placement.Align), placement.AnchorHidden, placement.Uncentered);
        if (next.Equals(previous))
        {
            return;
        }

        mirror = next;
        bool placementChanged = next.Side != previous.Side || next.Align != previous.Align;
        if (placementChanged || next.AnchorHidden != previous.AnchorHidden)
        {
            stateChanged();
        }

        if (placementChanged || next.Uncentered != previous.Uncentered)
        {
            Context.NotifyArrowChanged();
        }
    }
}

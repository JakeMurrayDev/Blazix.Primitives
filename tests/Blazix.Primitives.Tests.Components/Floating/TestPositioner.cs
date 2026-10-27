using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Blazix.Primitives.Tests.Components;

/// <summary>The state of <see cref="TestPositioner"/>: its open input and the mirror of the module's report.</summary>
[PartState]
public sealed partial record TestPositionerState(bool Open, Side Side, Align Align, bool AnchorHidden);

/// <summary>
/// A test-only positioner (the <see cref="TestCompositeRoot"/> precedent; ADR 12 Amendments 2026-10-27 item 1): a
/// <c>div</c> that composes <see cref="PositionerBehavior"/> as a family's Positioner part will (Popover, Tooltip and
/// PreviewCard in M4 tracks 5 to 7), with every input of the behavior as a parameter. It renders <c>data-open</c> while
/// open, which the module reads as the open input, and cascades the positioner's context to its content.
/// </summary>
internal sealed class TestPositioner : PartBase<TestPositionerState>
{
    private PositionerBehavior? behavior;
    private TestPositionerState? state;
    private RenderFragment? cascadedContent;
    private RenderFragment? cascade;

    [Inject]
    private IJSRuntime JSRuntime { get; set; } = null!;

    [CascadingParameter]
    private DirectionProviderContext? DirectionContext { get; set; }

    [Parameter]
    public AnchorValue? Anchor { get; set; }

    [Parameter]
    public PositionMethod? PositionMethod { get; set; }

    [Parameter]
    public Side? Side { get; set; }

    [Parameter]
    public OffsetValue? SideOffset { get; set; }

    [Parameter]
    public Align? Align { get; set; }

    [Parameter]
    public OffsetValue? AlignOffset { get; set; }

    [Parameter]
    public CollisionBoundaryValue? CollisionBoundary { get; set; }

    [Parameter]
    public CollisionPaddingValue? CollisionPadding { get; set; }

    [Parameter]
    public bool Sticky { get; set; }

    [Parameter]
    public double? ArrowPadding { get; set; }

    [Parameter]
    public bool DisableAnchorTracking { get; set; }

    [Parameter]
    public CollisionAvoidance? CollisionAvoidance { get; set; }

    [Parameter]
    public bool Open { get; set; } = true;

    [Parameter]
    public bool Mounted { get; set; } = true;

    [Parameter]
    public bool KeepMounted { get; set; }

    [Parameter]
    public TransitionStatus? TransitionStatus { get; set; }

    [Parameter]
    public bool AdaptiveOrigin { get; set; }

    [Parameter]
    public string? TriggerId { get; set; } = "trigger";

    [Parameter]
    public string? SessionToken { get; set; }

    public PositionerBehavior Behavior => behavior!;

    public Task TwinAttached => behavior!.TwinAttached;

    /// <inheritdoc />
    protected override string DefaultTagName => "div";

    /// <inheritdoc />
    protected override TestPositionerState State => state!;

    /// <inheritdoc />
    protected override bool UsesGeneratedId => RenderFunction is not null;

    /// <inheritdoc />
    public override Task SetParametersAsync(ParameterView parameters)
    {
        ChildContent = null;
        return base.SetParametersAsync(parameters);
    }

    /// <summary>Requests a render with the current parameters, for the update and allocation tests.</summary>
    public void Refresh() => StateHasChanged();

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        behavior = new PositionerBehavior(JSRuntime, OnMirrorChanged, InvokeAsync);
        base.OnInitialized();
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        behavior!.SetDirectionContext(DirectionContext);
        behavior.SetInputs(new PositionerInputs
        {
            Anchor = Anchor,
            PositionMethod = PositionMethod,
            Side = Side,
            SideOffset = SideOffset,
            Align = Align,
            AlignOffset = AlignOffset,
            CollisionBoundary = CollisionBoundary,
            CollisionPadding = CollisionPadding,
            Sticky = Sticky,
            ArrowPadding = ArrowPadding,
            DisableAnchorTracking = DisableAnchorTracking,
            CollisionAvoidance = CollisionAvoidance,
            Open = Open,
            Mounted = Mounted,
            KeepMounted = KeepMounted,
            TransitionStatus = TransitionStatus,
            AdaptiveOrigin = AdaptiveOrigin,
            TriggerId = TriggerId,
            SessionToken = SessionToken,
        });
        UpdateState();

        if (cascade is null || !ReferenceEquals(cascadedContent, ChildContent))
        {
            cascadedContent = ChildContent;
            cascade = behavior.Context.Cascade(cascadedContent);
        }

        ChildContent = cascade;
    }

    /// <inheritdoc />
    protected override Task OnAfterRenderAsync(bool firstRender) => behavior!.AfterRenderAsync(Element, Id);

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
    protected override void AddPartAttributes(AttributeList attributes)
    {
        behavior!.AddAttributes(attributes);
        if (Open)
        {
            attributes.Set("data-open", true);
        }
    }

    /// <inheritdoc />
    protected override void FinishAttributes(AttributeList attributes) => behavior!.FinishAttributes(attributes);

    // A render of the parent may already have rendered this part with the new mirror (the Positioner renders before its
    // Arrow is notified), so the part renders only when its state record changed: once per report.
    private void OnMirrorChanged()
    {
        if (UpdateState())
        {
            StateHasChanged();
        }
    }

    // Returns whether the state changed; a new record is made only then (ADR 16 rule 7).
    private bool UpdateState()
    {
        PositionerMirror mirror = behavior!.Mirror;
        if (state is { } current && current.Open == Open && current.Side == mirror.Side && current.Align == mirror.Align && current.AnchorHidden == mirror.AnchorHidden)
        {
            return false;
        }

        state = new TestPositionerState(Open, mirror.Side, mirror.Align, mirror.AnchorHidden);
        return true;
    }
}

using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Blazix.Primitives.Fixtures.Client.FloatingFixtures;

/// <summary>
/// A test-only positioner for the <c>positioning-*</c> fixtures (tests/browser/positioning.spec.ts), the counterpart of
/// <c>TestPositioner</c> in the L2 project: a <c>div</c> composing <see cref="PositionerBehavior"/> with every input as a
/// parameter (public, because Razor finds only public components), because no Positioner part ships before M4 tracks 5
/// to 7. It renders <c>data-open</c> while open, which the module reads as the open input, and the C# mirror of the
/// module's report as <c>data-mirrored-side</c>, <c>data-mirrored-align</c> and <c>data-mirrored-anchor-hidden</c>. Under a
/// portal it hands the behavior the portal's context, as a family's Positioner part will.
/// </summary>
public sealed class FixturePositioner : PartBase<EmptyState>
{
    private PositionerBehavior? behavior;
    private RenderFragment? cascadedContent;
    private RenderFragment? cascade;

    [Inject]
    private IJSRuntime JSRuntime { get; set; } = null!;

    [CascadingParameter]
    private DirectionProviderContext? DirectionContext { get; set; }

    [CascadingParameter]
    private PopupPortalContext? Portal { get; set; }

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

    /// <summary>Gets a task that completes when the positioner's behavior is first attached.</summary>
    public Task TwinAttached => behavior!.TwinAttached;

    /// <inheritdoc />
    protected override string DefaultTagName => "div";

    /// <inheritdoc />
    protected override EmptyState State => EmptyState.Instance;

    /// <inheritdoc />
    protected override bool UsesGeneratedId => RenderFunction is not null;

    /// <inheritdoc />
    public override Task SetParametersAsync(ParameterView parameters)
    {
        ChildContent = null;
        return base.SetParametersAsync(parameters);
    }

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        behavior = new PositionerBehavior(JSRuntime, StateHasChanged, InvokeAsync);
        base.OnInitialized();
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        behavior!.SetDirectionContext(DirectionContext);
        behavior.SetPortalContext(Portal);
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
        });

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

        PositionerMirror mirror = behavior.Mirror;
        attributes.Set("data-mirrored-side", FloatingLiterals.ToLiteral(mirror.Side));
        attributes.Set("data-mirrored-align", FloatingLiterals.ToLiteral(mirror.Align));
        attributes.Set("data-mirrored-anchor-hidden", mirror.AnchorHidden ? "true" : "false");
    }

    /// <inheritdoc />
    protected override void FinishAttributes(AttributeList attributes) => behavior!.FinishAttributes(attributes);
}

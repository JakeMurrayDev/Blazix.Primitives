using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;

namespace Blazix.Primitives.Tests.Components;

/// <summary>The state of <see cref="TestPositionerArrow"/>: the mirror of the module's report.</summary>
[PartState]
public sealed partial record TestPositionerArrowState(Side Side, Align Align, bool Uncentered);

/// <summary>
/// A test-only arrow (ADR 12 Amendments 2026-10-27 item 1): a <c>div</c> with a generated id that composes
/// <see cref="PositionerArrowBehavior"/> as a family's Arrow part will, inside a <see cref="TestPositioner"/>.
/// </summary>
internal sealed class TestPositionerArrow : PartBase<TestPositionerArrowState>
{
    private PositionerArrowBehavior? behavior;
    private TestPositionerArrowState? state;

    [CascadingParameter]
    private PositionerContext? PositionerContext { get; set; }

    /// <inheritdoc />
    protected override string DefaultTagName => "div";

    /// <inheritdoc />
    protected override TestPositionerArrowState State => state!;

    /// <inheritdoc />
    protected override bool UsesGeneratedId => true;

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        behavior = new PositionerArrowBehavior(OnMirrorChanged);
        base.OnInitialized();
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        behavior!.SetContext(PositionerContext ?? throw new InvalidOperationException("TestPositionerArrow needs a TestPositioner."));
        UpdateState();
    }

    /// <inheritdoc />
    protected override Task OnAfterRenderAsync(bool firstRender) => behavior!.AfterRenderAsync();

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
    protected override void AddPartAttributes(AttributeList attributes) => behavior!.AddAttributes(attributes);

    /// <inheritdoc />
    protected override void FinishAttributes(AttributeList attributes) => behavior!.FinishAttributes(attributes, Id!);

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
        if (state is { } current && current.Side == mirror.Side && current.Align == mirror.Align && current.Uncentered == mirror.Uncentered)
        {
            return false;
        }

        state = new TestPositionerArrowState(mirror.Side, mirror.Align, mirror.Uncentered);
        return true;
    }
}

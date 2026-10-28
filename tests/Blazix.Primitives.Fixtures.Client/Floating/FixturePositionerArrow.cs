using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;

namespace Blazix.Primitives.Fixtures.Client.FloatingFixtures;

/// <summary>
/// A test-only arrow for the <c>positioning-*</c> fixtures, the counterpart of <c>TestPositionerArrow</c>: a <c>div</c>
/// with a generated id composing <see cref="PositionerArrowBehavior"/> inside a <see cref="FixturePositioner"/>, which
/// renders the C# mirror as <c>data-mirrored-side</c>, <c>data-mirrored-align</c> and <c>data-mirrored-uncentered</c>.
/// </summary>
public sealed class FixturePositionerArrow : PartBase<EmptyState>
{
    private PositionerArrowBehavior? behavior;

    [CascadingParameter]
    private PositionerContext? PositionerContext { get; set; }

    /// <inheritdoc />
    protected override string DefaultTagName => "div";

    /// <inheritdoc />
    protected override EmptyState State => EmptyState.Instance;

    /// <inheritdoc />
    protected override bool UsesGeneratedId => true;

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        behavior = new PositionerArrowBehavior(StateHasChanged);
        base.OnInitialized();
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        behavior!.SetContext(PositionerContext ?? throw new InvalidOperationException("FixturePositionerArrow needs a FixturePositioner."));
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
    protected override void AddPartAttributes(AttributeList attributes)
    {
        behavior!.AddAttributes(attributes);
        PositionerMirror mirror = behavior.Mirror;
        attributes.Set("data-mirrored-side", FloatingLiterals.ToLiteral(mirror.Side));
        attributes.Set("data-mirrored-align", FloatingLiterals.ToLiteral(mirror.Align));
        attributes.Set("data-mirrored-uncentered", mirror.Uncentered ? "true" : "false");
    }

    /// <inheritdoc />
    protected override void FinishAttributes(AttributeList attributes) => behavior!.FinishAttributes(attributes, Id!);
}

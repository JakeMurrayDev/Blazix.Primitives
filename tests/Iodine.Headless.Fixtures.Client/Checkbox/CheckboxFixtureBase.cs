using Microsoft.AspNetCore.Components;

namespace Blazix.Primitives.Fixtures.Client.CheckboxFixtures;

/// <summary>
/// The base of the Checkbox fixtures: renders <c>data-attached</c> once every listed box has its root and hidden-input
/// behaviors attached, and every listed indicator its transitions twin (the specs wait for it before clicking), and counts
/// the sync button the specs click as a barrier.
/// </summary>
public abstract class CheckboxFixtureBase : ComponentBase
{
    protected bool Attached { get; private set; }

    protected int Sync { get; set; }

    /// <summary>The boxes of the page, read after the first interactive render.</summary>
    protected abstract IEnumerable<CheckboxRoot?> Boxes { get; }

    /// <summary>The indicators rendered at the first interactive render, read after it.</summary>
    protected virtual IEnumerable<CheckboxIndicator?> Indicators => [];

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender || !RendererInfo.IsInteractive)
        {
            return;
        }

        await Task.WhenAll(Boxes.Select(root => root!.TwinAttached).Concat(Indicators.Select(indicator => indicator!.TwinAttached)));
        Attached = true;
        StateHasChanged();
    }
}

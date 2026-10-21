using Microsoft.AspNetCore.Components;

namespace Blazix.Primitives.Fixtures.Client.ButtonFixtures;

/// <summary>
/// The base of the Button fixtures: renders <c>data-attached</c> once every listed Button has its behavior attached
/// (the specs wait for it before pressing keys) and counts the sync button the specs click as a barrier.
/// </summary>
public abstract class ButtonFixtureBase : ComponentBase
{
    protected bool Attached { get; private set; }

    protected int Sync { get; set; }

    /// <summary>The Buttons of the page, read after the first interactive render.</summary>
    protected abstract IEnumerable<Button?> Buttons { get; }

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender || !RendererInfo.IsInteractive)
        {
            return;
        }

        await Task.WhenAll(Buttons.Select(button => button!.TwinAttached));
        Attached = true;
        StateHasChanged();
    }
}

using Microsoft.AspNetCore.Components;

namespace Blazix.Primitives.Fixtures.Client.CollapsibleFixtures;

/// <summary>
/// The base of the Collapsible fixtures (tests/browser/collapsible.spec.ts): renders <c>data-attached</c> once every listed
/// part has its browser-side behavior attached (the specs wait for it before they press anything) and counts the sync
/// button the specs click as a barrier.
/// </summary>
public abstract class CollapsibleFixtureBase : ComponentBase
{
    protected bool Attached { get; private set; }

    protected int Sync { get; set; }

    /// <summary>The roots, triggers and initially rendered panels of the page, read after the first interactive render.</summary>
    protected abstract IEnumerable<object?> Parts { get; }

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender || !RendererInfo.IsInteractive)
        {
            return;
        }

        await Task.WhenAll(Parts.Select(part => part switch
        {
            CollapsibleRoot root => root.TwinAttached,
            CollapsibleTrigger trigger => trigger.TwinAttached,
            CollapsiblePanel panel => panel.TwinAttached,
            _ => Task.CompletedTask,
        }));
        Attached = true;
        StateHasChanged();
    }
}

using Blazix.Primitives.Fixtures.Client.CompositeFixtures;
using Microsoft.AspNetCore.Components;

namespace Blazix.Primitives.Fixtures.Client.ToggleFixtures;

/// <summary>
/// The base of the Toggle and ToggleGroup fixtures: renders <c>data-attached</c> once every tracked toggle has its button
/// behavior attached and every tracked group or test root its composite behavior (the specs wait for it before they
/// click or press a key), and counts the sync button the specs click as a barrier. A part is tracked by assigning it to
/// <see cref="Toggle"/>, <see cref="Group"/> or <see cref="Root"/> with <c>@ref</c>, which a fixture may do from as many
/// parts as it renders.
/// </summary>
public abstract class ToggleFixtureBase : ComponentBase
{
    private readonly List<Func<Task>> attached = [];

    protected bool Attached { get; private set; }

    protected int Sync { get; set; }

    /// <summary>Sets a toggle rendered at the first interactive render to track.</summary>
    protected Toggle? Toggle
    {
        set => Track(value, toggle => toggle.TwinAttached);
    }

    /// <summary>Sets a group rendered at the first interactive render to track.</summary>
    protected ToggleGroup? Group
    {
        set => Track(value, group => group.TwinAttached);
    }

    /// <summary>Sets a test-only composite root rendered at the first interactive render to track.</summary>
    protected FixtureCompositeRoot? Root
    {
        set => Track(value, root => root.TwinAttached);
    }

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender || !RendererInfo.IsInteractive)
        {
            return;
        }

        await Task.WhenAll(attached.Select(task => task()));
        Attached = true;
        StateHasChanged();
    }

    private void Track<T>(T? part, Func<T, Task> twinAttached)
        where T : class
    {
        if (part is not null && !Attached)
        {
            attached.Add(() => twinAttached(part));
        }
    }
}

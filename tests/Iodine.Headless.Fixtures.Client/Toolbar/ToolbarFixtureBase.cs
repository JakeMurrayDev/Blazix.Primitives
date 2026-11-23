using Microsoft.AspNetCore.Components;

namespace Blazix.Primitives.Fixtures.Client.ToolbarFixtures;

/// <summary>
/// The base of the Toolbar fixtures: renders <c>data-attached</c> once every tracked toolbar has its composite behavior
/// attached and every tracked button, input, switch or toggle its button behavior (the specs wait for it before they click
/// or press a key), and counts the sync button the specs click as a barrier. A part is tracked by assigning it to
/// <see cref="Root"/>, <see cref="Button"/>, <see cref="Input"/>, <see cref="Switch"/> or <see cref="Toggle"/> with
/// <c>@ref</c>, which a fixture may do from as many parts as it renders.
/// </summary>
public abstract class ToolbarFixtureBase : ComponentBase
{
    private readonly List<Func<Task>> attached = [];

    protected bool Attached { get; private set; }

    protected int Sync { get; set; }

    /// <summary>Sets a toolbar rendered at the first interactive render to track.</summary>
    protected ToolbarRoot? Root
    {
        set => Track(value, root => root.TwinAttached);
    }

    /// <summary>Sets a toolbar button rendered at the first interactive render to track.</summary>
    protected ToolbarButton? Button
    {
        set => Track(value, button => button.TwinAttached);
    }

    /// <summary>Sets a toolbar input rendered at the first interactive render to track.</summary>
    protected ToolbarInput? Input
    {
        set => Track(value, input => input.TwinAttached);
    }

    /// <summary>Sets a switch substituted into a toolbar button at the first interactive render to track.</summary>
    protected SwitchRoot? Switch
    {
        set => Track(value, root => root.TwinAttached);
    }

    /// <summary>Sets a toggle rendered at the first interactive render to track.</summary>
    protected Toggle? Toggle
    {
        set => Track(value, toggle => toggle.TwinAttached);
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

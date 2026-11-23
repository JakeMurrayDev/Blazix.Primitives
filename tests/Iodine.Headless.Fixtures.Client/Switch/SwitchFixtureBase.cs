using Microsoft.AspNetCore.Components;

namespace Blazix.Primitives.Fixtures.Client.SwitchFixtures;

/// <summary>
/// The base of the Switch fixtures: renders <c>data-attached</c> once every listed switch has its root and hidden-input
/// behaviors attached, and every listed form and field label its own (the specs wait for it before clicking), and
/// counts the sync button the specs click as a barrier.
/// </summary>
public abstract class SwitchFixtureBase : ComponentBase
{
    protected bool Attached { get; private set; }

    protected int Sync { get; set; }

    /// <summary>The switches of the page, read after the first interactive render.</summary>
    protected abstract IEnumerable<SwitchRoot?> Switches { get; }

    /// <summary>The other parts of the page that attach a module (forms and field labels; an error attaches only when shown).</summary>
    protected virtual IEnumerable<object?> OtherParts => [];

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender || !RendererInfo.IsInteractive)
        {
            return;
        }

        await Task.WhenAll(Switches.Select(root => root!.TwinAttached));
        await Task.WhenAll(OtherParts.Select(part => part switch
        {
            Form form => form.TwinAttached,
            FieldLabel label => label.TwinAttached,
            _ => Task.CompletedTask,
        }));
        Attached = true;
        StateHasChanged();
    }
}

using Microsoft.AspNetCore.Components;

namespace Blazix.Primitives.Fixtures.Client.PopupFixtures;

/// <summary>Runs a callback once, after its first render: Base UI's <c>OpenOnMount</c> layout effect, at the batch boundary.</summary>
public sealed class OpenOnMount : ComponentBase
{
    [Parameter]
    public Func<Task>? Run { get; set; }

    protected override Task OnAfterRenderAsync(bool firstRender) => firstRender && Run is not null ? Run() : Task.CompletedTask;
}

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Blazix.Primitives.Tests.Components;

/// <summary>
/// Counts the renders of the component whose content it is: its <see cref="ChildContent"/> is a fragment, which Blazor
/// treats as changed whenever the parent renders, so it renders exactly when its parent does.
/// </summary>
public sealed class RenderCounter : ComponentBase
{
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    [Parameter]
    public Action? Rendered { get; set; }

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        Rendered?.Invoke();
        builder.AddContent(0, ChildContent);
    }
}

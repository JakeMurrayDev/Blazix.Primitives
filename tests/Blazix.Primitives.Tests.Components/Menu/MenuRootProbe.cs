using Microsoft.AspNetCore.Components;

namespace Blazix.Primitives.Tests.Components;

/// <summary>Records whether the menu it renders in is filterable: its cascaded root context's <c>Filter</c>.</summary>
public sealed class MenuRootProbe : ComponentBase
{
    [Parameter]
    public string Name { get; set; } = "";

    [Parameter]
    public Dictionary<string, bool> Seen { get; set; } = [];

    [CascadingParameter]
    private MenuRootContext? Root { get; set; }

    protected override void OnParametersSet() => Seen[Name] = Root?.Filter is not null;
}

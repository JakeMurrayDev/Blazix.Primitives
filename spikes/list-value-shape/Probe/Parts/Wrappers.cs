using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Probe.Parts;

// Non-generic wrappers in the Portal/Positioner/Popup shape, between the root and its items.
public abstract class WrapperBase : ComponentBase
{
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    protected abstract string Tag { get; }

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.OpenElement(0, Tag);
        builder.AddContent(1, ChildContent);
        builder.CloseElement();
    }
}

public sealed class ListPortal : WrapperBase
{
    protected override string Tag => "list-portal";
}

public sealed class ListPositioner : WrapperBase
{
    protected override string Tag => "list-positioner";
}

public sealed class ListPopup : WrapperBase
{
    protected override string Tag => "list-popup";
}

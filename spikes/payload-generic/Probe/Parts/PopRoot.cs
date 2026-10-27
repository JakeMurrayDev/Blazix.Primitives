using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Probe.Parts;

// The non-generic Root, for plain popups: no handle, no payload.
public sealed class PopRoot : ComponentBase
{
    [Parameter] public RenderFragment? ChildContent { get; set; }

    [Parameter] public bool DefaultOpen { get; set; }

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.OpenElement(0, "root");
        builder.AddAttribute(1, "kind", "non-generic");
        builder.AddContent(2, ChildContent);
        builder.CloseElement();
    }
}

// The generic Root of the same tag name, for payload users: TPayload inferred from Handle.
[CascadingTypeParameter(nameof(TPayload))]
public sealed class PopRoot<TPayload> : ComponentBase
{
    [Parameter] public RenderFragment<TPayload?>? ChildContent { get; set; }

    [Parameter] public PopHandle<TPayload>? Handle { get; set; }

    [Parameter] public bool DefaultOpen { get; set; }

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.OpenElement(0, "root");
        builder.AddAttribute(1, "kind", $"generic<{typeof(TPayload).Name}>");
        builder.AddContent(2, ChildContent, default(TPayload));
        builder.CloseElement();
    }
}

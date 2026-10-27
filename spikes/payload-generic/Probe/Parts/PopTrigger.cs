using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Probe.Parts;

public sealed class PopTrigger : ComponentBase
{
    [Parameter] public RenderFragment? ChildContent { get; set; }

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.OpenElement(0, "trigger");
        builder.AddAttribute(1, "kind", "non-generic");
        builder.AddContent(2, ChildContent);
        builder.CloseElement();
    }
}

[CascadingTypeParameter(nameof(TPayload))]
public sealed class PopTrigger<TPayload> : ComponentBase
{
    [Parameter] public RenderFragment? ChildContent { get; set; }

    [Parameter] public PopHandle<TPayload>? Handle { get; set; }

    [Parameter] public TPayload? Payload { get; set; }

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.OpenElement(0, "trigger");
        builder.AddAttribute(1, "kind", $"generic<{typeof(TPayload).Name}>");
        builder.AddAttribute(2, "payload", Payload?.ToString() ?? "null");
        builder.AddContent(3, ChildContent);
        builder.CloseElement();
    }
}

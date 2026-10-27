using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Probe.Parts;

// Generic-only parts (no non-generic twin of the same tag name), for question (ii) and the cascading type parameter.
[CascadingTypeParameter(nameof(TPayload))]
public sealed class GenRoot<TPayload> : ComponentBase
{
    [Parameter] public RenderFragment<TPayload?>? ChildContent { get; set; }

    [Parameter] public PopHandle<TPayload>? Handle { get; set; }

    [Parameter] public bool DefaultOpen { get; set; }

    [Parameter] public TPayload? Seed { get; set; }

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.OpenElement(0, "root");
        builder.AddAttribute(1, "kind", $"generic<{typeof(TPayload).Name}>");
        builder.AddContent(2, ChildContent, Seed);
        builder.CloseElement();
    }
}

[CascadingTypeParameter(nameof(TPayload))]
public sealed class GenTrigger<TPayload> : ComponentBase
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

// The second shape's Root: ChildContent is a plain RenderFragment, the payload is a separate template parameter.
[CascadingTypeParameter(nameof(TPayload))]
public sealed class TplRoot<TPayload> : ComponentBase
{
    [Parameter] public RenderFragment? ChildContent { get; set; }

    [Parameter] public RenderFragment<TPayload?>? PayloadContent { get; set; }

    [Parameter] public PopHandle<TPayload>? Handle { get; set; }

    [Parameter] public TPayload? Seed { get; set; }

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.OpenElement(0, "root");
        builder.AddAttribute(1, "kind", $"generic<{typeof(TPayload).Name}>");
        builder.AddContent(2, ChildContent);
        builder.AddContent(3, PayloadContent, Seed);
        builder.CloseElement();
    }
}

/// <summary>The marker of the second shape.</summary>
public sealed record NoPayload;

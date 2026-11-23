using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Blazix.Primitives.Tests.Components.Rendering.Conformance;

/// <summary>
/// Base UI's conformance <c>Wrapper</c> (<c>test/conformanceTests/renderProp.tsx</c>): a consumer component that
/// forwards the handle and spreads the received attributes onto the element it renders, optionally inside one
/// wrapper <c>div</c>. The port's form of "forwarding the ref" is capturing the inner element and exposing it.
/// </summary>
public sealed class ConformanceWrapper : ComponentBase
{
    private ElementReference inner;

    [Parameter]
    public string Tag { get; set; } = "div";

    [Parameter]
    public bool WrappingAllowed { get; set; } = true;

    [Parameter(CaptureUnmatchedValues = true)]
    public IReadOnlyDictionary<string, object>? Attributes { get; set; }

    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>The element the attributes were spread onto, once rendered.</summary>
    public ElementReference Inner => inner;

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        if (WrappingAllowed)
        {
            builder.OpenElement(0, "div");
            builder.AddAttribute(1, "data-testid", "base-ui-wrapper");
        }

        builder.OpenElement(2, Tag);
        builder.AddMultipleAttributes(3, Attributes);
        builder.AddAttribute(4, "data-testid", "wrapped");
        builder.AddElementReferenceCapture(5, element => inner = element);
        builder.AddContent(6, ChildContent);
        builder.CloseElement();

        if (WrappingAllowed)
        {
            builder.CloseElement();
        }
    }
}

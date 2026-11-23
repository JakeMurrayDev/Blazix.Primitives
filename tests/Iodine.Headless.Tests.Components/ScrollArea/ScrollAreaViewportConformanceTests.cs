using Blazix.Primitives.Rendering;
using Blazix.Primitives.Tests.Components.Rendering.Conformance;
using Bunit;
using Microsoft.AspNetCore.Components;

// The namespace is the project's root: a namespace named after the family would hide the parts' types.
namespace Blazix.Primitives.Tests.Components;

// ScrollAreaViewport.test.tsx: describeConformance(<ScrollArea.Viewport />, () => ({ render inside <ScrollArea.Root>, refInstanceof: window.HTMLDivElement })). SCR-STR-02.
/// <summary>The conformance suite on <see cref="ScrollAreaViewport"/> inside a <see cref="ScrollAreaRoot"/>.</summary>
public sealed class ScrollAreaViewportConformanceTests : PartConformanceTests<ScrollAreaViewport>
{
    public ScrollAreaViewportConformanceTests()
    {
        // The twins' imports and attaches are not the subject here; the family's tests record them.
        Context.JSInterop.Mode = JSRuntimeMode.Loose;
        Context.RenderTree.Add<ScrollAreaRoot>();
    }

    /// <inheritdoc />
    protected override ConformanceOptions<ScrollAreaViewport> Options { get; } = new("div");

    /// <inheritdoc />
    protected override ElementReference? ElementOf(ScrollAreaViewport part) => part.Element;

    /// <inheritdoc />
    protected override void SetRenderFunction(ComponentParameterCollectionBuilder<ScrollAreaViewport> p, Func<IReadOnlyDictionary<string, object>, RenderFragment> render)
    {
        p.Add(x => x.RenderFunction, (RenderFragment<RenderProps<ScrollAreaViewportState>>)(props => render(props.Attributes)));
    }

    /// <inheritdoc />
    protected override void SetClassValue(ComponentParameterCollectionBuilder<ScrollAreaViewport> p, Func<string?> classValue)
    {
        p.Add(x => x.ClassValue, (Func<ScrollAreaViewportState, string?>)(_ => classValue()));
    }
}

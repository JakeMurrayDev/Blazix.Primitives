using Iodine.Headless.Rendering;
using Iodine.Headless.Tests.Components.Rendering.Conformance;
using Bunit;
using Microsoft.AspNetCore.Components;

// The namespace is the project's root: a namespace named after the family would hide the parts' types.
namespace Iodine.Headless.Tests.Components;

// ScrollAreaRoot.test.tsx: describeConformance(<ScrollArea.Root />, () => ({ refInstanceof: window.HTMLDivElement })). SCR-STR-01.
/// <summary>The conformance suite on <see cref="ScrollAreaRoot"/> standing alone.</summary>
public sealed class ScrollAreaRootConformanceTests : PartConformanceTests<ScrollAreaRoot>
{
    public ScrollAreaRootConformanceTests()
    {
        // The twins' imports and attaches are not the subject here; the family's tests record them.
        Context.JSInterop.Mode = JSRuntimeMode.Loose;
    }

    /// <inheritdoc />
    protected override ConformanceOptions<ScrollAreaRoot> Options { get; } = new("div");

    /// <inheritdoc />
    protected override ElementReference? ElementOf(ScrollAreaRoot part) => part.Element;

    /// <inheritdoc />
    protected override void SetRenderFunction(ComponentParameterCollectionBuilder<ScrollAreaRoot> p, Func<IReadOnlyDictionary<string, object>, RenderFragment> render)
    {
        p.Add(x => x.RenderFunction, (RenderFragment<RenderProps<ScrollAreaRootState>>)(props => render(props.Attributes)));
    }

    /// <inheritdoc />
    protected override void SetClassValue(ComponentParameterCollectionBuilder<ScrollAreaRoot> p, Func<string?> classValue)
    {
        p.Add(x => x.ClassValue, (Func<ScrollAreaRootState, string?>)(_ => classValue()));
    }
}

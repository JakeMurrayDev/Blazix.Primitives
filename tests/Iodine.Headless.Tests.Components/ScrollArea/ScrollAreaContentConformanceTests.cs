using Iodine.Headless.Rendering;
using Iodine.Headless.Tests.Components.Rendering.Conformance;
using Bunit;
using Microsoft.AspNetCore.Components;

// The namespace is the project's root: a namespace named after the family would hide the parts' types.
namespace Iodine.Headless.Tests.Components;

// ScrollAreaContent.test.tsx: describeConformance(<ScrollArea.Content />, () => ({ render inside <ScrollArea.Root><ScrollArea.Viewport>, refInstanceof: window.HTMLDivElement })). SCR-STR-03.
/// <summary>The conformance suite on <see cref="ScrollAreaContent"/> inside a <see cref="ScrollAreaViewport"/> of a <see cref="ScrollAreaRoot"/>.</summary>
public sealed class ScrollAreaContentConformanceTests : PartConformanceTests<ScrollAreaContent>
{
    public ScrollAreaContentConformanceTests()
    {
        // The twins' imports and attaches are not the subject here; the family's tests record them.
        Context.JSInterop.Mode = JSRuntimeMode.Loose;
        Context.RenderTree.Add<ScrollAreaRoot>();
        Context.RenderTree.Add<ScrollAreaViewport>();
    }

    /// <inheritdoc />
    protected override ConformanceOptions<ScrollAreaContent> Options { get; } = new("div");

    /// <inheritdoc />
    protected override ElementReference? ElementOf(ScrollAreaContent part) => part.Element;

    /// <inheritdoc />
    protected override void SetRenderFunction(ComponentParameterCollectionBuilder<ScrollAreaContent> p, Func<IReadOnlyDictionary<string, object>, RenderFragment> render)
    {
        p.Add(x => x.RenderFunction, (RenderFragment<RenderProps<ScrollAreaContentState>>)(props => render(props.Attributes)));
    }

    /// <inheritdoc />
    protected override void SetClassValue(ComponentParameterCollectionBuilder<ScrollAreaContent> p, Func<string?> classValue)
    {
        p.Add(x => x.ClassValue, (Func<ScrollAreaContentState, string?>)(_ => classValue()));
    }
}

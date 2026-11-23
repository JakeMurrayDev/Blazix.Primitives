using Iodine.Headless.Rendering;
using Iodine.Headless.Tests.Components.Rendering.Conformance;
using Bunit;
using Microsoft.AspNetCore.Components;

// The namespace is the project's root: a namespace named after the family would hide the parts' types.
namespace Iodine.Headless.Tests.Components;

// ScrollAreaThumb.test.tsx: describeConformance(<ScrollArea.Thumb />, () => ({ render inside <ScrollArea.Root><ScrollArea.Scrollbar keepMounted>, refInstanceof: window.HTMLDivElement })). SCR-STR-05.
/// <summary>The conformance suite on <see cref="ScrollAreaThumb"/> inside a kept-mounted <see cref="ScrollAreaScrollbar"/> of a <see cref="ScrollAreaRoot"/>.</summary>
public sealed class ScrollAreaThumbConformanceTests : PartConformanceTests<ScrollAreaThumb>
{
    public ScrollAreaThumbConformanceTests()
    {
        // The twins' imports and attaches are not the subject here; the family's tests record them.
        Context.JSInterop.Mode = JSRuntimeMode.Loose;
        Context.RenderTree.Add<ScrollAreaRoot>();
        Context.RenderTree.Add<ScrollAreaScrollbar>(p => p.Add(x => x.KeepMounted, true));
    }

    /// <inheritdoc />
    protected override ConformanceOptions<ScrollAreaThumb> Options { get; } = new("div");

    /// <inheritdoc />
    protected override ElementReference? ElementOf(ScrollAreaThumb part) => part.Element;

    /// <inheritdoc />
    protected override void SetRenderFunction(ComponentParameterCollectionBuilder<ScrollAreaThumb> p, Func<IReadOnlyDictionary<string, object>, RenderFragment> render)
    {
        p.Add(x => x.RenderFunction, (RenderFragment<RenderProps<ScrollAreaThumbState>>)(props => render(props.Attributes)));
    }

    /// <inheritdoc />
    protected override void SetClassValue(ComponentParameterCollectionBuilder<ScrollAreaThumb> p, Func<string?> classValue)
    {
        p.Add(x => x.ClassValue, (Func<ScrollAreaThumbState, string?>)(_ => classValue()));
    }
}

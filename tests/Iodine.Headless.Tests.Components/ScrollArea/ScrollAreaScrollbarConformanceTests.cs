using Iodine.Headless.Rendering;
using Iodine.Headless.Tests.Components.Rendering.Conformance;
using Bunit;
using Microsoft.AspNetCore.Components;

// The namespace is the project's root: a namespace named after the family would hide the parts' types.
namespace Iodine.Headless.Tests.Components;

// ScrollAreaScrollbar.test.tsx: describeConformance(<ScrollArea.Scrollbar keepMounted />, () => ({ render inside <ScrollArea.Root>, refInstanceof: window.HTMLDivElement })). SCR-STR-04.
/// <summary>The conformance suite on <see cref="ScrollAreaScrollbar"/> kept mounted inside a <see cref="ScrollAreaRoot"/>.</summary>
public sealed class ScrollAreaScrollbarConformanceTests : PartConformanceTests<ScrollAreaScrollbar>
{
    public ScrollAreaScrollbarConformanceTests()
    {
        // The twins' imports and attaches are not the subject here; the family's tests record them.
        Context.JSInterop.Mode = JSRuntimeMode.Loose;
        Context.RenderTree.Add<ScrollAreaRoot>();
    }

    /// <inheritdoc />
    protected override ConformanceOptions<ScrollAreaScrollbar> Options { get; } = new("div")
    {
        Required = p => p.Add(x => x.KeepMounted, true),
    };

    /// <inheritdoc />
    protected override ElementReference? ElementOf(ScrollAreaScrollbar part) => part.Element;

    /// <inheritdoc />
    protected override void SetRenderFunction(ComponentParameterCollectionBuilder<ScrollAreaScrollbar> p, Func<IReadOnlyDictionary<string, object>, RenderFragment> render)
    {
        p.Add(x => x.RenderFunction, (RenderFragment<RenderProps<ScrollAreaScrollbarState>>)(props => render(props.Attributes)));
    }

    /// <inheritdoc />
    protected override void SetClassValue(ComponentParameterCollectionBuilder<ScrollAreaScrollbar> p, Func<string?> classValue)
    {
        p.Add(x => x.ClassValue, (Func<ScrollAreaScrollbarState, string?>)(_ => classValue()));
    }
}

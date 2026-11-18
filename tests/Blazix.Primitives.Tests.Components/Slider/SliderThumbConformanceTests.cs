using Blazix.Primitives.Rendering;
using Blazix.Primitives.Tests.Components.Rendering.Conformance;
using Bunit;
using Microsoft.AspNetCore.Components;

// The namespace is the project's root: a namespace named after the family would hide the parts' types.
namespace Blazix.Primitives.Tests.Components;

// SliderThumb.test.tsx: describeConformance(<Slider.Thumb />, () => ({ render inside <Slider.Root>, refInstanceof: window.HTMLDivElement })). SLD-API-25.
/// <summary>The conformance suite on <see cref="SliderThumb"/> inside a <see cref="SliderRoot"/>: the shared RND-DOM-36..49 tests with Base UI's titles.</summary>
public sealed class SliderThumbConformanceTests : PartConformanceTests<SliderThumb>
{
    public SliderThumbConformanceTests()
    {
        // The twins' imports and attaches are not the subject here; the family's tests record them.
        Context.JSInterop.Mode = JSRuntimeMode.Loose;
        Context.RenderTree.Add<SliderRoot>();
    }

    /// <inheritdoc />
    protected override ConformanceOptions<SliderThumb> Options { get; } = new("div");

    /// <inheritdoc />
    protected override ElementReference? ElementOf(SliderThumb part) => part.Element;

    /// <inheritdoc />
    protected override void SetRenderFunction(ComponentParameterCollectionBuilder<SliderThumb> p, Func<IReadOnlyDictionary<string, object>, RenderFragment> render)
    {
        p.Add(x => x.RenderFunction, (RenderFragment<RenderProps<SliderThumbState>>)(props => render(props.Attributes)));
    }

    /// <inheritdoc />
    protected override void SetClassValue(ComponentParameterCollectionBuilder<SliderThumb> p, Func<string?> classValue)
    {
        p.Add(x => x.ClassValue, (Func<SliderThumbState, string?>)(_ => classValue()));
    }
}

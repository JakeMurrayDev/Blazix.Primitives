using Blazix.Primitives.Rendering;
using Blazix.Primitives.Tests.Components.Rendering.Conformance;
using Bunit;
using Microsoft.AspNetCore.Components;

// The namespace is the project's root: a namespace named after the family would hide the parts' types.
namespace Blazix.Primitives.Tests.Components;

// SliderIndicator.test.tsx: describeConformance(<Slider.Indicator />, () => ({ render inside <Slider.Root>, refInstanceof: window.HTMLDivElement })). SLD-API-28.
/// <summary>The conformance suite on <see cref="SliderIndicator"/> inside a <see cref="SliderRoot"/>: the shared RND-DOM-36..49 tests with Base UI's titles.</summary>
public sealed class SliderIndicatorConformanceTests : PartConformanceTests<SliderIndicator>
{
    public SliderIndicatorConformanceTests()
    {
        // The twins' imports and attaches are not the subject here; the family's tests record them.
        Context.JSInterop.Mode = JSRuntimeMode.Loose;
        Context.RenderTree.Add<SliderRoot>();
    }

    /// <inheritdoc />
    protected override ConformanceOptions<SliderIndicator> Options { get; } = new("div");

    /// <inheritdoc />
    protected override ElementReference? ElementOf(SliderIndicator part) => part.Element;

    /// <inheritdoc />
    protected override void SetRenderFunction(ComponentParameterCollectionBuilder<SliderIndicator> p, Func<IReadOnlyDictionary<string, object>, RenderFragment> render)
    {
        p.Add(x => x.RenderFunction, (RenderFragment<RenderProps<SliderIndicatorState>>)(props => render(props.Attributes)));
    }

    /// <inheritdoc />
    protected override void SetClassValue(ComponentParameterCollectionBuilder<SliderIndicator> p, Func<string?> classValue)
    {
        p.Add(x => x.ClassValue, (Func<SliderIndicatorState, string?>)(_ => classValue()));
    }
}

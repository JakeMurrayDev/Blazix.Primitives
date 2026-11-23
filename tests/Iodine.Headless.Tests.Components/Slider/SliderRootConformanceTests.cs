using Iodine.Headless.Rendering;
using Iodine.Headless.Tests.Components.Rendering.Conformance;
using Bunit;
using Microsoft.AspNetCore.Components;

// The namespace is the project's root: a namespace named after the family would hide the parts' types.
namespace Iodine.Headless.Tests.Components;

// SliderRoot.test.tsx: describeConformance(<Slider.Root defaultValue={50} />, () => ({ render, refInstanceof: window.HTMLDivElement })). SLD-API-15.
/// <summary>The conformance suite on <see cref="SliderRoot"/>: the shared RND-DOM-36..49 tests with Base UI's titles.</summary>
public sealed class SliderRootConformanceTests : PartConformanceTests<SliderRoot>
{
    public SliderRootConformanceTests()
    {
        // The twins' imports and attaches are not the subject here; the family's tests record them.
        Context.JSInterop.Mode = JSRuntimeMode.Loose;
    }

    /// <inheritdoc />
    protected override ConformanceOptions<SliderRoot> Options { get; } = new("div")
    {
        Required = p => p.Add(x => x.DefaultValue, 50),
    };

    /// <inheritdoc />
    protected override ElementReference? ElementOf(SliderRoot part) => part.Element;

    /// <inheritdoc />
    protected override void SetRenderFunction(ComponentParameterCollectionBuilder<SliderRoot> p, Func<IReadOnlyDictionary<string, object>, RenderFragment> render)
    {
        p.Add(x => x.RenderFunction, (RenderFragment<RenderProps<SliderRootState>>)(props => render(props.Attributes)));
    }

    /// <inheritdoc />
    protected override void SetClassValue(ComponentParameterCollectionBuilder<SliderRoot> p, Func<string?> classValue)
    {
        p.Add(x => x.ClassValue, (Func<SliderRootState, string?>)(_ => classValue()));
    }
}

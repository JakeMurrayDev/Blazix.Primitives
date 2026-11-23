using Iodine.Headless.Rendering;
using Iodine.Headless.Tests.Components.Rendering.Conformance;
using Bunit;
using Microsoft.AspNetCore.Components;

// The namespace is the project's root: a namespace named after the family would hide the parts' types.
namespace Iodine.Headless.Tests.Components;

// SliderValue.test.tsx: describeConformance(<Slider.Value />, () => ({ render inside <Slider.Root>, refInstanceof: window.HTMLOutputElement })). SLD-API-26.
/// <summary>The conformance suite on <see cref="SliderValue"/> inside a <see cref="SliderRoot"/>: the shared RND-DOM-36..49 tests with Base UI's titles.</summary>
public sealed class SliderValueConformanceTests : PartConformanceTests<SliderValue>
{
    public SliderValueConformanceTests()
    {
        // The twins' imports and attaches are not the subject here; the family's tests record them.
        Context.JSInterop.Mode = JSRuntimeMode.Loose;
        Context.RenderTree.Add<SliderRoot>();
    }

    /// <inheritdoc />
    protected override ConformanceOptions<SliderValue> Options { get; } = new("output");

    /// <inheritdoc />
    protected override ElementReference? ElementOf(SliderValue part) => part.Element;

    /// <inheritdoc />
    protected override void SetRenderFunction(ComponentParameterCollectionBuilder<SliderValue> p, Func<IReadOnlyDictionary<string, object>, RenderFragment> render)
    {
        p.Add(x => x.RenderFunction, (RenderFragment<RenderProps<SliderValueState>>)(props => render(props.Attributes)));
    }

    /// <inheritdoc />
    protected override void SetClassValue(ComponentParameterCollectionBuilder<SliderValue> p, Func<string?> classValue)
    {
        p.Add(x => x.ClassValue, (Func<SliderValueState, string?>)(_ => classValue()));
    }
}

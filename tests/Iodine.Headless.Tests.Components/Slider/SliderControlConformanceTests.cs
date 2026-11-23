using Iodine.Headless.Rendering;
using Iodine.Headless.Tests.Components.Rendering.Conformance;
using Bunit;
using Microsoft.AspNetCore.Components;

// The namespace is the project's root: a namespace named after the family would hide the parts' types.
namespace Iodine.Headless.Tests.Components;

// SliderControl.test.tsx: describeConformance(<Slider.Control />, () => ({ render inside <Slider.Root>, refInstanceof: window.HTMLDivElement })). SLD-API-28.
/// <summary>The conformance suite on <see cref="SliderControl"/> inside a <see cref="SliderRoot"/>: the shared RND-DOM-36..49 tests with Base UI's titles.</summary>
public sealed class SliderControlConformanceTests : PartConformanceTests<SliderControl>
{
    public SliderControlConformanceTests()
    {
        // The twins' imports and attaches are not the subject here; the family's tests record them.
        Context.JSInterop.Mode = JSRuntimeMode.Loose;
        Context.RenderTree.Add<SliderRoot>();
    }

    /// <inheritdoc />
    protected override ConformanceOptions<SliderControl> Options { get; } = new("div");

    /// <inheritdoc />
    protected override ElementReference? ElementOf(SliderControl part) => part.Element;

    /// <inheritdoc />
    protected override void SetRenderFunction(ComponentParameterCollectionBuilder<SliderControl> p, Func<IReadOnlyDictionary<string, object>, RenderFragment> render)
    {
        p.Add(x => x.RenderFunction, (RenderFragment<RenderProps<SliderControlState>>)(props => render(props.Attributes)));
    }

    /// <inheritdoc />
    protected override void SetClassValue(ComponentParameterCollectionBuilder<SliderControl> p, Func<string?> classValue)
    {
        p.Add(x => x.ClassValue, (Func<SliderControlState, string?>)(_ => classValue()));
    }
}

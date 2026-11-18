using Blazix.Primitives.Rendering;
using Blazix.Primitives.Tests.Components.Rendering.Conformance;
using Bunit;
using Microsoft.AspNetCore.Components;

// The namespace is the project's root: a namespace named after the family would hide the parts' types.
namespace Blazix.Primitives.Tests.Components;

// SliderTrack.test.tsx: describeConformance(<Slider.Track />, () => ({ render inside <Slider.Root>, refInstanceof: window.HTMLDivElement })). SLD-STR-04, SLD-DOM-04, SLD-API-28.
/// <summary>The conformance suite on <see cref="SliderTrack"/> inside a <see cref="SliderRoot"/>: the shared RND-DOM-36..49 tests with Base UI's titles.</summary>
public sealed class SliderTrackConformanceTests : PartConformanceTests<SliderTrack>
{
    public SliderTrackConformanceTests()
    {
        // The twins' imports and attaches are not the subject here; the family's tests record them.
        Context.JSInterop.Mode = JSRuntimeMode.Loose;
        Context.RenderTree.Add<SliderRoot>();
    }

    /// <inheritdoc />
    protected override ConformanceOptions<SliderTrack> Options { get; } = new("div");

    /// <inheritdoc />
    protected override ElementReference? ElementOf(SliderTrack part) => part.Element;

    /// <inheritdoc />
    protected override void SetRenderFunction(ComponentParameterCollectionBuilder<SliderTrack> p, Func<IReadOnlyDictionary<string, object>, RenderFragment> render)
    {
        p.Add(x => x.RenderFunction, (RenderFragment<RenderProps<SliderTrackState>>)(props => render(props.Attributes)));
    }

    /// <inheritdoc />
    protected override void SetClassValue(ComponentParameterCollectionBuilder<SliderTrack> p, Func<string?> classValue)
    {
        p.Add(x => x.ClassValue, (Func<SliderTrackState, string?>)(_ => classValue()));
    }
}

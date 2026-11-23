using Blazix.Primitives.Rendering;
using Blazix.Primitives.Tests.Components.Rendering.Conformance;
using Bunit;
using Microsoft.AspNetCore.Components;

// The namespace is the project's root: a namespace named after the family would hide the parts' types.
namespace Blazix.Primitives.Tests.Components;

// SliderLabel.test.tsx: describeConformance(<Slider.Label />, () => ({ refInstanceof: window.HTMLDivElement, render inside <Slider.Root defaultValue={50}> beside a Control and a Thumb })). SLD-API-27.
/// <summary>The conformance suite on <see cref="SliderLabel"/> inside a <see cref="SliderRoot"/>: the shared RND-DOM-36..49 tests with Base UI's titles.</summary>
public sealed class SliderLabelConformanceTests : PartConformanceTests<SliderLabel>
{
    public SliderLabelConformanceTests()
    {
        // The twins' imports and attaches are not the subject here; the family's tests record them.
        Context.JSInterop.Mode = JSRuntimeMode.Loose;
        // The root's own aria-labelledby wins over the label's id (SLD-ARIA-07), so the label's publication renders the root
        // and the label again in no title; the second render is not what the suite tests, and bUnit's markup of a render that
        // keeps an element drops its capture id.
        Context.RenderTree.Add<SliderRoot>(p => p.Add(x => x.DefaultValue, 50).AddUnmatched("aria-labelledby", "external"));
    }

    /// <inheritdoc />
    protected override ConformanceOptions<SliderLabel> Options { get; } = new("div");

    /// <inheritdoc />
    protected override ElementReference? ElementOf(SliderLabel part) => part.Element;

    /// <inheritdoc />
    protected override void SetRenderFunction(ComponentParameterCollectionBuilder<SliderLabel> p, Func<IReadOnlyDictionary<string, object>, RenderFragment> render)
    {
        p.Add(x => x.RenderFunction, (RenderFragment<RenderProps<SliderLabelState>>)(props => render(props.Attributes)));
    }

    /// <inheritdoc />
    protected override void SetClassValue(ComponentParameterCollectionBuilder<SliderLabel> p, Func<string?> classValue)
    {
        p.Add(x => x.ClassValue, (Func<SliderLabelState, string?>)(_ => classValue()));
    }
}

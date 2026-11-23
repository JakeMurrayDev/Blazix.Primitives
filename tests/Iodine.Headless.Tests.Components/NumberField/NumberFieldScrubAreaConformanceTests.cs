using Iodine.Headless.Rendering;
using Iodine.Headless.Tests.Components.Rendering.Conformance;
using Bunit;
using Microsoft.AspNetCore.Components;

// The namespace is the project's root: a namespace named after the family would hide the parts' types.
namespace Iodine.Headless.Tests.Components;

// NumberFieldScrubArea.test.tsx: describeConformance(<NumberField.ScrubArea />, () => ({ refInstanceof: window.HTMLSpanElement, render: inside a Root })).
// NUM-DOM-12, NUM-API-20, NUM-API-21, NUM-API-25.
/// <summary>The conformance suite on <see cref="NumberFieldScrubArea"/>: the shared RND-DOM-36..49 tests with Base UI's titles.</summary>
public sealed class NumberFieldScrubAreaConformanceTests : PartConformanceTests<NumberFieldScrubArea>
{
    public NumberFieldScrubAreaConformanceTests()
    {
        // The twins' imports and attaches are not the subject here.
        Context.JSInterop.Mode = JSRuntimeMode.Loose;
        Context.RenderTree.Add<NumberFieldRoot>();
    }

    /// <inheritdoc />
    protected override ConformanceOptions<NumberFieldScrubArea> Options { get; } = new("span");

    /// <inheritdoc />
    protected override ElementReference? ElementOf(NumberFieldScrubArea part) => part.Element;

    /// <inheritdoc />
    protected override void SetRenderFunction(ComponentParameterCollectionBuilder<NumberFieldScrubArea> p, Func<IReadOnlyDictionary<string, object>, RenderFragment> render)
    {
        p.Add(x => x.RenderFunction, (RenderFragment<RenderProps<NumberFieldScrubAreaState>>)(props => render(props.Attributes)));
    }

    /// <inheritdoc />
    protected override void SetClassValue(ComponentParameterCollectionBuilder<NumberFieldScrubArea> p, Func<string?> classValue)
    {
        p.Add(x => x.ClassValue, (Func<NumberFieldScrubAreaState, string?>)(_ => classValue()));
    }
}

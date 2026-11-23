using Blazix.Primitives.Rendering;
using Bunit;
using Microsoft.AspNetCore.Components;

namespace Blazix.Primitives.Tests.Components.Rendering.Conformance;

/// <summary>The conformance suite on <see cref="SampleRoot"/> (issue 31): the first run of RND-DOM-36..49 at L2.</summary>
public sealed class SampleRootConformanceTests : PartConformanceTests<SampleRoot>
{
    /// <inheritdoc />
    protected override ConformanceOptions<SampleRoot> Options { get; } = new("div");

    /// <inheritdoc />
    protected override ElementReference? ElementOf(SampleRoot part) => part.Element;

    /// <inheritdoc />
    protected override void SetRenderFunction(ComponentParameterCollectionBuilder<SampleRoot> p, Func<IReadOnlyDictionary<string, object>, RenderFragment> render)
    {
        p.Add(x => x.RenderFunction, (RenderFragment<RenderProps<SampleRootState>>)(props => render(props.Attributes)));
    }

    /// <inheritdoc />
    protected override void SetClassValue(ComponentParameterCollectionBuilder<SampleRoot> p, Func<string?> classValue)
    {
        p.Add(x => x.ClassValue, (Func<SampleRootState, string?>)(_ => classValue()));
    }
}

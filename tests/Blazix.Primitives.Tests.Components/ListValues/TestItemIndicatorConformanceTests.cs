using Blazix.Primitives.Rendering;
using Blazix.Primitives.Tests.Components.Rendering.Conformance;
using Bunit;
using Microsoft.AspNetCore.Components;

namespace Blazix.Primitives.Tests.Components;

// RND-STR-10 (SelectItemIndicator.test.tsx: describeConformance, a `span`): the conformance suite on the test-only indicator,
// selected so that it renders.
/// <summary>The conformance suite on <see cref="TestItemIndicator"/>.</summary>
public sealed class TestItemIndicatorConformanceTests : PartConformanceTests<TestItemIndicator>
{
    public TestItemIndicatorConformanceTests()
    {
        Context.JSInterop.Mode = JSRuntimeMode.Loose;
    }

    /// <inheritdoc />
    protected override ConformanceOptions<TestItemIndicator> Options { get; } = new("span") { Required = p => p.Add(x => x.Selected, true) };

    /// <inheritdoc />
    protected override ElementReference? ElementOf(TestItemIndicator part) => part.Element;

    /// <inheritdoc />
    protected override void SetRenderFunction(ComponentParameterCollectionBuilder<TestItemIndicator> p, Func<IReadOnlyDictionary<string, object>, RenderFragment> render)
    {
        p.Add(x => x.RenderFunction, (RenderFragment<RenderProps<TestItemIndicatorState>>)(props => render(props.Attributes)));
    }

    /// <inheritdoc />
    protected override void SetClassValue(ComponentParameterCollectionBuilder<TestItemIndicator> p, Func<string?> classValue)
    {
        p.Add(x => x.ClassValue, (Func<TestItemIndicatorState, string?>)(_ => classValue()));
    }
}

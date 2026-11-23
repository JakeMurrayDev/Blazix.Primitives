using Blazix.Primitives.Rendering;
using Blazix.Primitives.Tests.Components.Rendering.Conformance;
using Bunit;
using Microsoft.AspNetCore.Components;

// The namespace is the project's root: a namespace named after the family would hide the parts' types.
namespace Blazix.Primitives.Tests.Components;

// MeterIndicator.test.tsx: describeConformance(<Meter.Indicator />, () => ({ render: inside <Meter.Root value={30}>, refInstanceof: window.HTMLDivElement })). MTR-DOM-09, MTR-API-08, MTR-API-13.
// RND-DOM-49 does not apply (no NativeButton).
/// <summary>The conformance suite on <see cref="MeterIndicator"/> inside a <see cref="MeterRoot"/>: the shared RND-DOM-36..49 tests with Base UI's titles.</summary>
public sealed class MeterIndicatorConformanceTests : PartConformanceTests<MeterIndicator>
{
    public MeterIndicatorConformanceTests()
    {
        Context.RenderTree.Add<MeterRoot>(p => p.Add(x => x.Value, 30));
    }

    /// <inheritdoc />
    protected override ConformanceOptions<MeterIndicator> Options { get; } = new("div");

    /// <inheritdoc />
    protected override ElementReference? ElementOf(MeterIndicator part) => part.Element;

    /// <inheritdoc />
    protected override void SetRenderFunction(ComponentParameterCollectionBuilder<MeterIndicator> p, Func<IReadOnlyDictionary<string, object>, RenderFragment> render)
    {
        p.Add(x => x.RenderFunction, (RenderFragment<RenderProps<EmptyState>>)(props => render(props.Attributes)));
    }

    /// <inheritdoc />
    protected override void SetClassValue(ComponentParameterCollectionBuilder<MeterIndicator> p, Func<string?> classValue)
    {
        p.Add(x => x.ClassValue, (Func<EmptyState, string?>)(_ => classValue()));
    }
}

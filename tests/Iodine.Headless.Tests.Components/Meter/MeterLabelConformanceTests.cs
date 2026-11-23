using Blazix.Primitives.Rendering;
using Blazix.Primitives.Tests.Components.Rendering.Conformance;
using Bunit;
using Microsoft.AspNetCore.Components;

// The namespace is the project's root: a namespace named after the family would hide the parts' types.
namespace Blazix.Primitives.Tests.Components;

// MeterLabel.test.tsx: describeConformance(<Meter.Label />, () => ({ render: inside <Meter.Root value={50}>, refInstanceof: window.HTMLSpanElement })). MTR-DOM-09, MTR-API-08, MTR-API-13.
// RND-DOM-49 does not apply (no NativeButton).
/// <summary>The conformance suite on <see cref="MeterLabel"/> inside a <see cref="MeterRoot"/>: the shared RND-DOM-36..49 tests with Base UI's titles.</summary>
public sealed class MeterLabelConformanceTests : PartConformanceTests<MeterLabel>
{
    public MeterLabelConformanceTests()
    {
        // The label publishes its id to the root, which renders again; bUnit drops the element-reference marker of an element
        // that renders again, which RND-DOM-44/45 look for. A root with its own aria-labelledby ignores the published id and
        // renders nothing more (MTR-ARIA-06), so the suite sees the first render of the label.
        Context.RenderTree.Add<MeterRoot>(p => p.Add(x => x.Value, 50).AddUnmatched("aria-labelledby", "conformance"));
    }

    /// <inheritdoc />
    protected override ConformanceOptions<MeterLabel> Options { get; } = new("span");

    /// <inheritdoc />
    protected override ElementReference? ElementOf(MeterLabel part) => part.Element;

    /// <inheritdoc />
    protected override void SetRenderFunction(ComponentParameterCollectionBuilder<MeterLabel> p, Func<IReadOnlyDictionary<string, object>, RenderFragment> render)
    {
        p.Add(x => x.RenderFunction, (RenderFragment<RenderProps<EmptyState>>)(props => render(props.Attributes)));
    }

    /// <inheritdoc />
    protected override void SetClassValue(ComponentParameterCollectionBuilder<MeterLabel> p, Func<string?> classValue)
    {
        p.Add(x => x.ClassValue, (Func<EmptyState, string?>)(_ => classValue()));
    }
}

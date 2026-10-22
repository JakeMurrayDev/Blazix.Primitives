using Blazix.Primitives.Rendering;
using Blazix.Primitives.Tests.Components.Rendering.Conformance;
using Bunit;
using Microsoft.AspNetCore.Components;

// The namespace is the project's root: a namespace named after the family would hide the parts' types.
namespace Blazix.Primitives.Tests.Components;

// FieldsetLegend.test.tsx: describeConformance(<Fieldset.Legend />, () => ({ refInstanceof: window.HTMLDivElement, render: inside <Fieldset.Root> })). FST-DOM-10, FST-STR-04, FST-API-05. The legend publishes its id and the root renders again; a root with its own aria-labelledby renders nothing new, so the suite sees the legend's first render (Meter hand-over fact 1).
/// <summary>The conformance suite on <see cref="FieldsetLegend"/> inside a <see cref="FieldsetRoot"/>: the shared RND-DOM-36..49 tests with Base UI's titles.</summary>
public sealed class FieldsetLegendConformanceTests : PartConformanceTests<FieldsetLegend>
{
    public FieldsetLegendConformanceTests()
    {
        Context.RenderTree.Add<FieldsetRoot>(p => p.AddUnmatched("aria-labelledby", "conformance"));
    }

    /// <inheritdoc />
    protected override ConformanceOptions<FieldsetLegend> Options { get; } = new("div");

    /// <inheritdoc />
    protected override ElementReference? ElementOf(FieldsetLegend part) => part.Element;

    /// <inheritdoc />
    protected override void SetRenderFunction(ComponentParameterCollectionBuilder<FieldsetLegend> p, Func<IReadOnlyDictionary<string, object>, RenderFragment> render)
    {
        p.Add(x => x.RenderFunction, (RenderFragment<RenderProps<FieldsetLegendState>>)(props => render(props.Attributes)));
    }

    /// <inheritdoc />
    protected override void SetClassValue(ComponentParameterCollectionBuilder<FieldsetLegend> p, Func<string?> classValue)
    {
        p.Add(x => x.ClassValue, (Func<FieldsetLegendState, string?>)(_ => classValue()));
    }
}

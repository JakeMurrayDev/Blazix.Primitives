using Blazix.Primitives.Rendering;
using Blazix.Primitives.Tests.Components.Rendering.Conformance;
using Bunit;
using Microsoft.AspNetCore.Components;

// The namespace is the project's root: a namespace named after the family would hide the parts' types.
namespace Blazix.Primitives.Tests.Components;

// FieldRoot.test.tsx: describeConformance(<Field.Root />, () => ({ refInstanceof: window.HTMLDivElement, render })). FLD-DOM-19, FLD-DOM-01, FLD-API-09/10.
/// <summary>The conformance suite on <see cref="FieldRoot"/>: the shared RND-DOM-36..49 tests with Base UI's titles.</summary>
public sealed class FieldRootConformanceTests : PartConformanceTests<FieldRoot>
{
    /// <inheritdoc />
    protected override ConformanceOptions<FieldRoot> Options { get; } = new("div");

    /// <inheritdoc />
    protected override ElementReference? ElementOf(FieldRoot part) => part.Element;

    /// <inheritdoc />
    protected override void SetRenderFunction(ComponentParameterCollectionBuilder<FieldRoot> p, Func<IReadOnlyDictionary<string, object>, RenderFragment> render)
    {
        p.Add(x => x.RenderFunction, (RenderFragment<RenderProps<FieldRootState>>)(props => render(props.Attributes)));
    }

    /// <inheritdoc />
    protected override void SetClassValue(ComponentParameterCollectionBuilder<FieldRoot> p, Func<string?> classValue)
    {
        p.Add(x => x.ClassValue, (Func<FieldRootState, string?>)(_ => classValue()));
    }
}

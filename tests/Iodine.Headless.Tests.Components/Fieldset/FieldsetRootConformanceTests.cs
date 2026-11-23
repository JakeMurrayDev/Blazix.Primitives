using Blazix.Primitives.Rendering;
using Blazix.Primitives.Tests.Components.Rendering.Conformance;
using Bunit;
using Microsoft.AspNetCore.Components;

// The namespace is the project's root: a namespace named after the family would hide the parts' types.
namespace Blazix.Primitives.Tests.Components;

// FieldsetRoot.test.tsx: describeConformance(<Fieldset.Root />, () => ({ refInstanceof: window.HTMLFieldSetElement, render })). FST-DOM-10, FST-API-02/03.
/// <summary>The conformance suite on <see cref="FieldsetRoot"/>: the shared RND-DOM-36..49 tests with Base UI's titles.</summary>
public sealed class FieldsetRootConformanceTests : PartConformanceTests<FieldsetRoot>
{
    /// <inheritdoc />
    protected override ConformanceOptions<FieldsetRoot> Options { get; } = new("fieldset");

    /// <inheritdoc />
    protected override ElementReference? ElementOf(FieldsetRoot part) => part.Element;

    /// <inheritdoc />
    protected override void SetRenderFunction(ComponentParameterCollectionBuilder<FieldsetRoot> p, Func<IReadOnlyDictionary<string, object>, RenderFragment> render)
    {
        p.Add(x => x.RenderFunction, (RenderFragment<RenderProps<FieldsetRootState>>)(props => render(props.Attributes)));
    }

    /// <inheritdoc />
    protected override void SetClassValue(ComponentParameterCollectionBuilder<FieldsetRoot> p, Func<string?> classValue)
    {
        p.Add(x => x.ClassValue, (Func<FieldsetRootState, string?>)(_ => classValue()));
    }
}

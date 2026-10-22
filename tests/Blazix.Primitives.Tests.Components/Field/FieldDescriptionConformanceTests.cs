using Blazix.Primitives.Rendering;
using Blazix.Primitives.Tests.Components.Rendering.Conformance;
using Bunit;
using Microsoft.AspNetCore.Components;

// The namespace is the project's root: a namespace named after the family would hide the parts' types.
namespace Blazix.Primitives.Tests.Components;

// FieldDescription.test.tsx: describeConformance(<Field.Description />, () => ({ refInstanceof: window.HTMLParagraphElement, render: inside <Field.Root> })). FLD-DOM-19, FLD-API-25/26.
/// <summary>The conformance suite on <see cref="FieldDescription"/> inside a <see cref="FieldRoot"/>: the shared RND-DOM-36..49 tests with Base UI's titles.</summary>
public sealed class FieldDescriptionConformanceTests : PartConformanceTests<FieldDescription>
{
    public FieldDescriptionConformanceTests()
    {
        Context.RenderTree.Add<FieldRoot>();
    }

    /// <inheritdoc />
    protected override ConformanceOptions<FieldDescription> Options { get; } = new("p");

    /// <inheritdoc />
    protected override ElementReference? ElementOf(FieldDescription part) => part.Element;

    /// <inheritdoc />
    protected override void SetRenderFunction(ComponentParameterCollectionBuilder<FieldDescription> p, Func<IReadOnlyDictionary<string, object>, RenderFragment> render)
    {
        p.Add(x => x.RenderFunction, (RenderFragment<RenderProps<FieldDescriptionState>>)(props => render(props.Attributes)));
    }

    /// <inheritdoc />
    protected override void SetClassValue(ComponentParameterCollectionBuilder<FieldDescription> p, Func<string?> classValue)
    {
        p.Add(x => x.ClassValue, (Func<FieldDescriptionState, string?>)(_ => classValue()));
    }
}
